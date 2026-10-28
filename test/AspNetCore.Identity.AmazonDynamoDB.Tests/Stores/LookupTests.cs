using Amazon.DynamoDBv2.Model;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace AspNetCore.Identity.AmazonDynamoDB.Tests;

// The lookup items that replace the indexes: unique values are enforced, lookups move with
// changed values and go away with their user or role
[Collection(Constants.DatabaseCollection)]
public class LookupTests
{
  [Fact]
  public async Task Should_FailWithDuplicateUserName_When_UserNameIsTaken()
  {
    // Arrange
    var userStore = await CreateUserStore();
    var name = Guid.NewGuid().ToString();
    var first = new DynamoDbUser { UserName = name, NormalizedUserName = name.ToUpperInvariant() };
    Assert.True((await userStore.CreateAsync(first, CancellationToken.None)).Succeeded);

    // Act
    var second = new DynamoDbUser { UserName = name, NormalizedUserName = name.ToUpperInvariant() };
    var result = await userStore.CreateAsync(second, CancellationToken.None);

    // Assert
    Assert.False(result.Succeeded);
    Assert.Equal("DuplicateUserName", Assert.Single(result.Errors).Code);
    Assert.Null(await userStore.FindByIdAsync(second.Id, CancellationToken.None));
    Assert.Equal(first.Id, (await userStore.FindByNameAsync(name.ToUpperInvariant(), CancellationToken.None))!.Id);
  }

  [Fact]
  public async Task Should_CreateOneUser_When_CreatingTheSameUserNameConcurrently()
  {
    // Arrange
    var userStore = await CreateUserStore();
    var name = Guid.NewGuid().ToString().ToUpperInvariant();

    // Act
    var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
      userStore.CreateAsync(new DynamoDbUser { UserName = name, NormalizedUserName = name }, CancellationToken.None)));

    // Assert
    Assert.Single(results, x => x.Succeeded);
    Assert.All(results.Where(x => !x.Succeeded), x => Assert.Equal("DuplicateUserName", Assert.Single(x.Errors).Code));
  }

  [Fact]
  public async Task Should_FindOneOfThem_When_UsersShareAnEmail()
  {
    // Arrange, Identity only requires unique emails when configured to, so the store allows them
    var userStore = await CreateUserStore();
    var email = $"{Guid.NewGuid()}@test.se".ToUpperInvariant();
    var first = new DynamoDbUser { Email = email, NormalizedEmail = email };
    var second = new DynamoDbUser { Email = email, NormalizedEmail = email };

    // Act
    var firstResult = await userStore.CreateAsync(first, CancellationToken.None);
    var secondResult = await userStore.CreateAsync(second, CancellationToken.None);

    // Assert
    Assert.True(firstResult.Succeeded);
    Assert.True(secondResult.Succeeded);
    var found = await userStore.FindByEmailAsync(email, CancellationToken.None);
    Assert.Contains(found!.Id, new[] { first.Id, second.Id });
  }

  [Fact]
  public async Task Should_FailWithDuplicateUserName_When_UpdatingToATakenUserName()
  {
    // Arrange
    var userStore = await CreateUserStore();
    var taken = Guid.NewGuid().ToString().ToUpperInvariant();
    var other = new DynamoDbUser { UserName = taken, NormalizedUserName = taken };
    var user = new DynamoDbUser { UserName = "x", NormalizedUserName = Guid.NewGuid().ToString().ToUpperInvariant() };
    await userStore.CreateAsync(other, CancellationToken.None);
    await userStore.CreateAsync(user, CancellationToken.None);
    var stamp = user.ConcurrencyStamp;
    var previousName = user.NormalizedUserName;
    user.NormalizedUserName = taken;

    // Act
    var result = await userStore.UpdateAsync(user, CancellationToken.None);

    // Assert
    Assert.False(result.Succeeded);
    Assert.Equal("DuplicateUserName", Assert.Single(result.Errors).Code);
    Assert.Equal(stamp, user.ConcurrencyStamp);
    Assert.Equal(other.Id, (await userStore.FindByNameAsync(taken, CancellationToken.None))!.Id);
    Assert.Equal(user.Id, (await userStore.FindByNameAsync(previousName!, CancellationToken.None))!.Id);
  }

  [Fact]
  public async Task Should_MoveLookups_When_UserNameAndEmailChange()
  {
    // Arrange
    var userStore = await CreateUserStore();
    var oldName = Guid.NewGuid().ToString().ToUpperInvariant();
    var oldEmail = $"{Guid.NewGuid()}@TEST.SE";
    var user = new DynamoDbUser { UserName = oldName, NormalizedUserName = oldName, Email = oldEmail, NormalizedEmail = oldEmail };
    await userStore.CreateAsync(user, CancellationToken.None);
    var newName = Guid.NewGuid().ToString().ToUpperInvariant();
    var newEmail = $"{Guid.NewGuid()}@TEST.SE";
    user.NormalizedUserName = newName;
    user.NormalizedEmail = newEmail;

    // Act
    var result = await userStore.UpdateAsync(user, CancellationToken.None);

    // Assert
    Assert.True(result.Succeeded);
    Assert.Null(await userStore.FindByNameAsync(oldName, CancellationToken.None));
    Assert.Null(await userStore.FindByEmailAsync(oldEmail, CancellationToken.None));
    Assert.Equal(user.Id, (await userStore.FindByNameAsync(newName, CancellationToken.None))!.Id);
    Assert.Equal(user.Id, (await userStore.FindByEmailAsync(newEmail, CancellationToken.None))!.Id);
    // The old username is free again
    Assert.True((await userStore.CreateAsync(new DynamoDbUser { UserName = oldName, NormalizedUserName = oldName }, CancellationToken.None)).Succeeded);
  }

  [Fact]
  public async Task Should_ClearLookups_When_UserNameAndEmailAreRemoved()
  {
    // Arrange
    var userStore = await CreateUserStore();
    var name = Guid.NewGuid().ToString().ToUpperInvariant();
    var email = $"{Guid.NewGuid()}@TEST.SE";
    var user = new DynamoDbUser { UserName = name, NormalizedUserName = name, Email = email, NormalizedEmail = email };
    await userStore.CreateAsync(user, CancellationToken.None);
    user.NormalizedUserName = null;
    user.NormalizedEmail = null;

    // Act
    var result = await userStore.UpdateAsync(user, CancellationToken.None);

    // Assert
    Assert.True(result.Succeeded);
    Assert.Null(await userStore.FindByNameAsync(name, CancellationToken.None));
    Assert.Null(await userStore.FindByEmailAsync(email, CancellationToken.None));
  }

  [Fact]
  public async Task Should_FailWithLoginAlreadyAssociated_When_LoginBelongsToAnotherUser()
  {
    // Arrange
    var userStore = await CreateUserStore();
    var providerKey = Guid.NewGuid().ToString();
    var owner = new DynamoDbUser();
    var other = new DynamoDbUser();
    await userStore.CreateAsync(owner, CancellationToken.None);
    await userStore.CreateAsync(other, CancellationToken.None);
    await userStore.AddLoginAsync(owner, new UserLoginInfo("GitHub", providerKey, "GitHub"), CancellationToken.None);
    Assert.True((await userStore.UpdateAsync(owner, CancellationToken.None)).Succeeded);
    await userStore.AddLoginAsync(other, new UserLoginInfo("GitHub", providerKey, "GitHub"), CancellationToken.None);

    // Act
    var result = await userStore.UpdateAsync(other, CancellationToken.None);

    // Assert
    Assert.False(result.Succeeded);
    Assert.Equal("LoginAlreadyAssociated", Assert.Single(result.Errors).Code);
    Assert.Equal(owner.Id, (await userStore.FindByLoginAsync("GitHub", providerKey, CancellationToken.None))!.Id);
    Assert.Empty(await userStore.GetLoginsAsync(new DynamoDbUser { Id = other.Id }, CancellationToken.None));
  }

  [Fact]
  public async Task Should_FreeTheLogin_When_ItIsRemoved()
  {
    // Arrange
    var userStore = await CreateUserStore();
    var providerKey = Guid.NewGuid().ToString();
    var user = new DynamoDbUser();
    await userStore.CreateAsync(user, CancellationToken.None);
    await userStore.AddLoginAsync(user, new UserLoginInfo("GitHub", providerKey, "GitHub"), CancellationToken.None);
    await userStore.UpdateAsync(user, CancellationToken.None);
    await userStore.RemoveLoginAsync(user, "GitHub", providerKey, CancellationToken.None);

    // Act
    var result = await userStore.UpdateAsync(user, CancellationToken.None);

    // Assert
    Assert.True(result.Succeeded);
    Assert.Null(await userStore.FindByLoginAsync("GitHub", providerKey, CancellationToken.None));
    Assert.Null(await DynamoDbItems.GetAsync<LookupItem>(
      DatabaseFixture.Client, DatabaseFixture.TableName, Lookups.Login("GitHub", providerKey), Lookups.SortKey, CancellationToken.None, consistentRead: true));
  }

  [Fact]
  public async Task Should_RemoveEveryLookupAndMembership_When_DeletingUser()
  {
    // Arrange
    var userStore = await CreateUserStore();
    var name = Guid.NewGuid().ToString().ToUpperInvariant();
    var email = $"{Guid.NewGuid()}@TEST.SE";
    var providerKey = Guid.NewGuid().ToString();
    var claimValue = Guid.NewGuid().ToString();
    var roleName = Guid.NewGuid().ToString().ToUpperInvariant();
    var user = new DynamoDbUser { UserName = name, NormalizedUserName = name, Email = email, NormalizedEmail = email };
    await userStore.AddClaimsAsync(user, [new("plan", claimValue)], CancellationToken.None);
    await userStore.AddLoginAsync(user, new UserLoginInfo("GitHub", providerKey, "GitHub"), CancellationToken.None);
    await userStore.AddToRoleAsync(user, roleName, CancellationToken.None);
    await userStore.SetTokenAsync(user, "p", "n", "v", CancellationToken.None);
    Assert.True((await userStore.CreateAsync(user, CancellationToken.None)).Succeeded);
    Assert.Single(await userStore.GetUsersForClaimAsync(new("plan", claimValue), CancellationToken.None));

    // Act
    var result = await userStore.DeleteAsync(user, CancellationToken.None);

    // Assert
    Assert.True(result.Succeeded);
    Assert.Null(await userStore.FindByIdAsync(user.Id, CancellationToken.None));
    Assert.Null(await userStore.FindByNameAsync(name, CancellationToken.None));
    Assert.Null(await userStore.FindByEmailAsync(email, CancellationToken.None));
    Assert.Null(await userStore.FindByLoginAsync("GitHub", providerKey, CancellationToken.None));
    Assert.Empty(await userStore.GetUsersForClaimAsync(new("plan", claimValue), CancellationToken.None));
    Assert.Empty(await userStore.GetUsersInRoleAsync(roleName, CancellationToken.None));
    Assert.Empty(await DynamoDbItems.QueryPartitionAsync<MembershipItem>(
      DatabaseFixture.Client, DatabaseFixture.TableName, Lookups.ClaimMembers("plan", claimValue), default, CancellationToken.None, consistentRead: true));
    Assert.Empty(await DynamoDbItems.QueryPartitionAsync<MembershipItem>(
      DatabaseFixture.Client, DatabaseFixture.TableName, Lookups.RoleMembers(roleName), default, CancellationToken.None, consistentRead: true));
    var partition = await DatabaseFixture.Client.QueryAsync(new QueryRequest
    {
      TableName = DatabaseFixture.TableName,
      KeyConditionExpression = "PartitionKey = :pk",
      ExpressionAttributeValues = new() { { ":pk", new AttributeValue { S = user.PartitionKey } } },
      ConsistentRead = true,
    });
    Assert.Empty(partition.Items ?? []);
  }

  [Fact]
  public async Task Should_UpdateMemberships_When_ClaimsAndRolesChange()
  {
    // Arrange
    var userStore = await CreateUserStore();
    var oldValue = Guid.NewGuid().ToString();
    var newValue = Guid.NewGuid().ToString();
    var oldRole = Guid.NewGuid().ToString().ToUpperInvariant();
    var newRole = Guid.NewGuid().ToString().ToUpperInvariant();
    var user = new DynamoDbUser();
    await userStore.AddClaimsAsync(user, [new("plan", oldValue)], CancellationToken.None);
    await userStore.AddToRoleAsync(user, oldRole, CancellationToken.None);
    await userStore.CreateAsync(user, CancellationToken.None);
    await userStore.ReplaceClaimAsync(user, new("plan", oldValue), new("plan", newValue), CancellationToken.None);
    await userStore.RemoveFromRoleAsync(user, oldRole, CancellationToken.None);
    await userStore.AddToRoleAsync(user, newRole, CancellationToken.None);

    // Act
    var result = await userStore.UpdateAsync(user, CancellationToken.None);

    // Assert
    Assert.True(result.Succeeded);
    Assert.Empty(await userStore.GetUsersForClaimAsync(new("plan", oldValue), CancellationToken.None));
    Assert.Equal(user.Id, Assert.Single(await userStore.GetUsersForClaimAsync(new("plan", newValue), CancellationToken.None)).Id);
    Assert.Empty(await userStore.GetUsersInRoleAsync(oldRole, CancellationToken.None));
    Assert.Equal(user.Id, Assert.Single(await userStore.GetUsersInRoleAsync(newRole, CancellationToken.None)).Id);
  }

  [Fact]
  public async Task Should_FailWithConcurrencyFailure_When_UpdatingAUserThatDoesNotExist()
  {
    // Arrange
    var userStore = await CreateUserStore();

    // Act
    var result = await userStore.UpdateAsync(new DynamoDbUser(), CancellationToken.None);

    // Assert
    Assert.False(result.Succeeded);
    Assert.Equal("ConcurrencyFailure", Assert.Single(result.Errors).Code);
  }

  [Fact]
  public async Task Should_FailWithDuplicateRoleName_When_RoleNameIsTaken()
  {
    // Arrange
    var roleStore = await CreateRoleStore();
    var name = Guid.NewGuid().ToString();
    var first = new DynamoDbRole { Name = name, NormalizedName = name.ToUpperInvariant() };
    Assert.True((await roleStore.CreateAsync(first, CancellationToken.None)).Succeeded);

    // Act
    var second = new DynamoDbRole { Name = name, NormalizedName = name.ToUpperInvariant() };
    var result = await roleStore.CreateAsync(second, CancellationToken.None);

    // Assert
    Assert.False(result.Succeeded);
    Assert.Equal("DuplicateRoleName", Assert.Single(result.Errors).Code);
    Assert.Null(await roleStore.FindByIdAsync(second.Id, CancellationToken.None));
  }

  [Fact]
  public async Task Should_MoveLookupAndResolveNewName_When_RoleIsRenamed()
  {
    // Arrange
    var roleStore = await CreateRoleStore();
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client, ResolveRoleNames = true });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    var oldName = Guid.NewGuid().ToString();
    var newName = Guid.NewGuid().ToString();
    var role = new DynamoDbRole { Name = oldName, NormalizedName = oldName.ToUpperInvariant() };
    await roleStore.CreateAsync(role, CancellationToken.None);
    var user = new DynamoDbUser();
    await userStore.AddToRoleAsync(user, newName.ToUpperInvariant(), CancellationToken.None);
    await userStore.CreateAsync(user, CancellationToken.None);
    role.Name = newName;
    role.NormalizedName = newName.ToUpperInvariant();

    // Act
    var result = await roleStore.UpdateAsync(role, CancellationToken.None);

    // Assert
    Assert.True(result.Succeeded);
    Assert.Null(await roleStore.FindByNameAsync(oldName.ToUpperInvariant(), CancellationToken.None));
    Assert.Equal(role.Id, (await roleStore.FindByNameAsync(newName.ToUpperInvariant(), CancellationToken.None))!.Id);
    Assert.Equal(newName, Assert.Single(await userStore.GetRolesAsync(user, CancellationToken.None)));
  }

  [Fact]
  public async Task Should_FailWithDuplicateRoleName_When_RenamingToATakenName()
  {
    // Arrange
    var roleStore = await CreateRoleStore();
    var taken = Guid.NewGuid().ToString().ToUpperInvariant();
    await roleStore.CreateAsync(new DynamoDbRole { Name = taken, NormalizedName = taken }, CancellationToken.None);
    var role = new DynamoDbRole { Name = "r", NormalizedName = Guid.NewGuid().ToString().ToUpperInvariant() };
    await roleStore.CreateAsync(role, CancellationToken.None);
    var stamp = role.ConcurrencyStamp;
    role.NormalizedName = taken;

    // Act
    var result = await roleStore.UpdateAsync(role, CancellationToken.None);

    // Assert
    Assert.False(result.Succeeded);
    Assert.Equal("DuplicateRoleName", Assert.Single(result.Errors).Code);
    Assert.Equal(stamp, role.ConcurrencyStamp);
  }

  [Fact]
  public async Task Should_RemoveLookup_When_RoleIsDeleted()
  {
    // Arrange
    var roleStore = await CreateRoleStore();
    var name = Guid.NewGuid().ToString().ToUpperInvariant();
    var role = new DynamoDbRole { Name = name, NormalizedName = name };
    await roleStore.CreateAsync(role, CancellationToken.None);

    // Act
    var result = await roleStore.DeleteAsync(role, CancellationToken.None);

    // Assert
    Assert.True(result.Succeeded);
    Assert.Null(await roleStore.FindByNameAsync(name, CancellationToken.None));
    Assert.True((await roleStore.CreateAsync(new DynamoDbRole { Name = name, NormalizedName = name }, CancellationToken.None)).Succeeded);
  }

#if NET10_0_OR_GREATER
  [Fact]
  public async Task Should_FailWithDuplicatePasskey_When_CredentialBelongsToAnotherUser()
  {
    // Arrange
    var userStore = await CreateUserStore();
    var credentialId = Guid.NewGuid().ToByteArray();
    var owner = new DynamoDbUser();
    var other = new DynamoDbUser();
    await userStore.AddOrUpdatePasskeyAsync(owner, Passkey(credentialId), CancellationToken.None);
    Assert.True((await userStore.CreateAsync(owner, CancellationToken.None)).Succeeded);
    await userStore.AddOrUpdatePasskeyAsync(other, Passkey(credentialId), CancellationToken.None);

    // Act
    var result = await userStore.CreateAsync(other, CancellationToken.None);

    // Assert
    Assert.False(result.Succeeded);
    Assert.Equal("DuplicatePasskey", Assert.Single(result.Errors).Code);
    Assert.Equal(owner.Id, (await userStore.FindByPasskeyIdAsync(credentialId, CancellationToken.None))!.Id);
  }

  private static UserPasskeyInfo Passkey(byte[] credentialId) => new(
    credentialId, [1, 2, 3], DateTimeOffset.UtcNow, 0, ["internal"], true, true, false, [4, 5], [6, 7])
  {
    Name = "key",
  };
#endif

  private static async Task<DynamoDbUserStore<DynamoDbUser>> CreateUserStore()
  {
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    return new DynamoDbUserStore<DynamoDbUser>(options);
  }

  private static async Task<DynamoDbRoleStore<DynamoDbRole>> CreateRoleStore()
  {
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    return new DynamoDbRoleStore<DynamoDbRole>(options);
  }
}
