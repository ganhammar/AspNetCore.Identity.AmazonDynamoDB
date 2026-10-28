using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AspNetCore.Identity.AmazonDynamoDB.UpgradeTests;

public class UpgradeTests(LegacyTableFixture fixture) : IClassFixture<LegacyTableFixture>
{
  private UserManager<DynamoDbUser> Users => fixture.Services.GetRequiredService<UserManager<DynamoDbUser>>();

  private RoleManager<DynamoDbRole> Roles => fixture.Services.GetRequiredService<RoleManager<DynamoDbRole>>();

  [Fact]
  public void Should_WarnAboutLegacyIndexes_When_SettingUpTheLegacyTable()
  {
    Assert.Contains(fixture.SetupLog, x => x.Level == LogLevel.Warning && x.Message.Contains("indexes of version 2"));
    Assert.Equal(7, fixture.LegacyIndexCountBeforeRemoval);
  }

  [Fact]
  public void Should_WriteOneLookupPerItem_When_Migrating()
  {
    var users = fixture.Seed.Users.Count;
    Assert.Equal(users, fixture.FirstMigration.Users);
    Assert.Equal(fixture.Seed.Roles.Count, fixture.FirstMigration.Roles);
    Assert.Equal(users * 2, fixture.FirstMigration.Claims);
    Assert.Equal(users, fixture.FirstMigration.Logins);
    Assert.Equal(users, fixture.FirstMigration.UserRoles);
    Assert.Equal(users, fixture.FirstMigration.Passkeys);
    Assert.Equal(fixture.FirstMigration, fixture.SecondMigration);
  }

  [Fact]
  public async Task Should_HaveRemovedTheLegacyIndexes()
  {
    var table = await fixture.Client.DescribeTableAsync(fixture.TableName);

    Assert.Equal(TableStatus.ACTIVE, table.Table.TableStatus);
    Assert.Empty(table.Table.GlobalSecondaryIndexes ?? []);
  }

  // The last seeded user is renamed and deleted by Should_UpdateAndDeleteMigratedUsers
  private IEnumerable<SeedUser> UntouchedUsers => fixture.Seed.Users.SkipLast(1);

  [Fact]
  public async Task Should_FindEveryUser_ByNameEmailLoginAndPasskey()
  {
    foreach (var seeded in UntouchedUsers)
    {
      Assert.Equal(seeded.Id, (await Users.FindByNameAsync(seeded.UserName))?.Id);
      Assert.Equal(seeded.Id, (await Users.FindByLoginAsync(seeded.LoginProvider, seeded.ProviderKey))?.Id);
      Assert.Equal(seeded.Id, (await Users.FindByPasskeyIdAsync(Convert.FromBase64String(seeded.PasskeyCredentialId)))?.Id);

      var byEmail = await Users.FindByEmailAsync(seeded.Email);
      Assert.NotNull(byEmail);
      Assert.Equal(seeded.Email, byEmail.Email);
    }
  }

  [Fact]
  public async Task Should_ReadEveryAttribute_ThatVersionTwoWrote()
  {
    foreach (var seeded in UntouchedUsers)
    {
      var user = (await Users.FindByIdAsync(seeded.Id))!;

      Assert.True(await Users.CheckPasswordAsync(user, seeded.Password));
      Assert.Equal(seeded.Role, Assert.Single(await Users.GetRolesAsync(user)));
      Assert.True(await Users.IsInRoleAsync(user, seeded.Role));
      Assert.Equal(seeded.Claims.Select(x => (x.Type, x.Value)).OrderBy(x => x.Type),
        (await Users.GetClaimsAsync(user)).Select(x => (x.Type, x.Value)).OrderBy(x => x.Type));
      Assert.Equal(seeded.TokenValue, await Users.GetAuthenticationTokenAsync(user, "Refresh", "token"));
      Assert.NotNull(await Users.GetAuthenticatorKeyAsync(user));
      Assert.Single(await Users.GetLoginsAsync(user));
      Assert.Single(await Users.GetPasskeysAsync(user));
      Assert.True(user.LockoutEnabled);
      Assert.StartsWith("+46", user.PhoneNumber);
    }
  }

  [Fact]
  public async Task Should_ListUsers_ByRoleAndClaim()
  {
    var editors = UntouchedUsers.Where(x => x.Role == "Editor").Select(x => x.Id).Order();
    var free = UntouchedUsers.Where(x => x.Claims.Any(c => c is { Type: "plan", Value: "free" })).Select(x => x.Id).Order();
    var deleted = fixture.Seed.Users[^1].Id;

    Assert.Equal(editors, (await Users.GetUsersInRoleAsync("Editor")).Select(x => x.Id).Where(x => x != deleted).Order());
    Assert.Equal(free, (await Users.GetUsersForClaimAsync(new("plan", "free"))).Select(x => x.Id).Where(x => x != deleted).Order());
  }

  [Fact]
  public async Task Should_FindRoles_ByName()
  {
    foreach (var seeded in fixture.Seed.Roles)
    {
      var role = await Roles.FindByNameAsync(seeded.Name);
      Assert.Equal(seeded.Id, role?.Id);
      Assert.Single(await Roles.GetClaimsAsync(role!));
    }
  }

  [Fact]
  public async Task Should_EnforceUniqueness_AgainstMigratedData()
  {
    var existing = fixture.Seed.Users[0];

    var duplicateName = await Users.CreateAsync(new DynamoDbUser { UserName = existing.UserName });
    var duplicateRole = await Roles.CreateAsync(new DynamoDbRole { Name = fixture.Seed.Roles[0].Name });

    Assert.Contains(duplicateName.Errors, x => x.Code == "DuplicateUserName");
    Assert.Contains(duplicateRole.Errors, x => x.Code == "DuplicateRoleName");
  }

  [Fact]
  public async Task Should_UpdateAndDeleteMigratedUsers()
  {
    var seeded = fixture.Seed.Users[^1];
    var user = (await Users.FindByIdAsync(seeded.Id))!;
    var newName = $"renamed-{Guid.NewGuid()}";

    Assert.True((await Users.SetUserNameAsync(user, newName)).Succeeded);
    Assert.Null(await Users.FindByNameAsync(seeded.UserName));
    Assert.Equal(seeded.Id, (await Users.FindByNameAsync(newName))?.Id);

    Assert.True((await Users.DeleteAsync(user)).Succeeded);
    Assert.Null(await Users.FindByIdAsync(seeded.Id));
    Assert.Null(await Users.FindByNameAsync(newName));
    Assert.Null(await Users.FindByLoginAsync(seeded.LoginProvider, seeded.ProviderKey));
    Assert.Null(await Users.FindByPasskeyIdAsync(Convert.FromBase64String(seeded.PasskeyCredentialId)));
    Assert.DoesNotContain(await Users.GetUsersInRoleAsync(seeded.Role), x => x.Id == seeded.Id);
  }
}
