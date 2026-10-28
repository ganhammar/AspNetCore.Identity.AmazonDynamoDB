using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AspNetCore.Identity.AmazonDynamoDB.Tests;

// A table created and filled by version 2 has the global secondary indexes and no lookup items;
// the migration adds the lookup items and the legacy indexes are removed afterwards
[Collection(Constants.DatabaseCollection)]
public class MigrationTests
{
  [Fact]
  public async Task Should_WriteLookupItems_When_MigratingLegacyTable()
  {
    // Arrange
    var tableName = await LegacyTable.CreateAsync(DatabaseFixture.Client);

    try
    {
      var seed = await SeedLegacyData(tableName);
      var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client, DefaultTableName = tableName });
      var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
      var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
      Assert.Null(await userStore.FindByNameAsync(seed.Anna.NormalizedUserName!, CancellationToken.None));

      // Act
      var result = await DynamoDbTableSetup.MigrateAsync(options.CurrentValue, DatabaseFixture.Client);

      // Assert
      Assert.Equal(new MigrationResult(Users: 2, Roles: 1, Claims: 1, Logins: 1, UserRoles: 1, Passkeys: 1), result);
      Assert.Equal(seed.Anna.Id, (await userStore.FindByNameAsync(seed.Anna.NormalizedUserName!, CancellationToken.None))!.Id);
      Assert.Equal(seed.Anna.Id, (await userStore.FindByEmailAsync(seed.Anna.NormalizedEmail!, CancellationToken.None))!.Id);
      Assert.Equal(seed.Bob.Id, (await userStore.FindByEmailAsync(seed.Bob.NormalizedEmail!, CancellationToken.None))!.Id);
      Assert.Equal(seed.Anna.Id, (await userStore.FindByLoginAsync("GitHub", seed.ProviderKey, CancellationToken.None))!.Id);
      Assert.Equal(seed.Anna.Id, Assert.Single(await userStore.GetUsersForClaimAsync(new("plan", "pro"), CancellationToken.None)).Id);
      Assert.Equal(seed.Bob.Id, Assert.Single(await userStore.GetUsersInRoleAsync("ADMIN", CancellationToken.None)).Id);
      Assert.Equal(seed.Role.Id, (await roleStore.FindByNameAsync("ADMIN", CancellationToken.None))!.Id);
#if NET10_0_OR_GREATER
      Assert.Equal(seed.Anna.Id, (await userStore.FindByPasskeyIdAsync(seed.CredentialId, CancellationToken.None))!.Id);
#endif
      var resolving = new DynamoDbUserStore<DynamoDbUser>(TestUtils.GetOptions(new()
      {
        Database = DatabaseFixture.Client,
        DefaultTableName = tableName,
        ResolveRoleNames = true,
      }));
      Assert.Equal("Admin", Assert.Single(await resolving.GetRolesAsync(seed.Bob, CancellationToken.None)));
    }
    finally
    {
      await DatabaseFixture.Client.DeleteTableAsync(tableName);
    }
  }

  [Fact]
  public async Task Should_WriteNothingNew_When_MigratingTwice()
  {
    // Arrange
    var tableName = await LegacyTable.CreateAsync(DatabaseFixture.Client);

    try
    {
      await SeedLegacyData(tableName);
      var options = new DynamoDbOptions { Database = DatabaseFixture.Client, DefaultTableName = tableName };
      var first = await DynamoDbTableSetup.MigrateAsync(options, DatabaseFixture.Client);
      var itemsAfterFirst = await LegacyTable.CountItemsAsync(DatabaseFixture.Client, tableName);

      // Act
      var second = await DynamoDbTableSetup.MigrateAsync(options, DatabaseFixture.Client);

      // Assert
      Assert.Equal(first, second);
      Assert.Equal(7, first.Total);
      Assert.Equal(itemsAfterFirst, await LegacyTable.CountItemsAsync(DatabaseFixture.Client, tableName));
    }
    finally
    {
      await DatabaseFixture.Client.DeleteTableAsync(tableName);
    }
  }

  [Fact]
  public async Task Should_CoverItemsWrittenAfterTheFirstRun_When_MigratingAgain()
  {
    // Arrange, an instance on version 2 keeps writing without lookups until it is replaced
    var tableName = await LegacyTable.CreateAsync(DatabaseFixture.Client);

    try
    {
      var options = new DynamoDbOptions { Database = DatabaseFixture.Client, DefaultTableName = tableName };
      await DynamoDbTableSetup.MigrateAsync(options, DatabaseFixture.Client);
      var context = new DynamoDBContextBuilder().WithDynamoDBClient(() => DatabaseFixture.Client).Build();
      var late = new DynamoDbUser { UserName = "late", NormalizedUserName = "LATE" };
      await context.SaveAsync(late, new SaveConfig { OverrideTableName = tableName });
      var userStore = new DynamoDbUserStore<DynamoDbUser>(TestUtils.GetOptions(new() { Database = DatabaseFixture.Client, DefaultTableName = tableName }));
      Assert.Null(await userStore.FindByNameAsync("LATE", CancellationToken.None));

      // Act
      var result = await DynamoDbTableSetup.MigrateAsync(options, DatabaseFixture.Client);

      // Assert
      Assert.Equal(1, result.Users);
      Assert.Equal(late.Id, (await userStore.FindByNameAsync("LATE", CancellationToken.None))!.Id);
    }
    finally
    {
      await DatabaseFixture.Client.DeleteTableAsync(tableName);
    }
  }

  [Fact]
  public async Task Should_KeepStoresWorking_When_LegacyIndexesAreStillPresent()
  {
    // Arrange, version 3 runs against the table before the indexes are removed
    var tableName = await LegacyTable.CreateAsync(DatabaseFixture.Client);

    try
    {
      var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client, DefaultTableName = tableName });
      await DynamoDbTableSetup.MigrateAsync(options.CurrentValue, DatabaseFixture.Client);
      var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
      var user = new DynamoDbUser { UserName = "new", NormalizedUserName = "NEW", Email = "new@test.se", NormalizedEmail = "NEW@TEST.SE" };

      // Act
      var created = await userStore.CreateAsync(user, CancellationToken.None);
      await userStore.AddLoginAsync(user, new UserLoginInfo("GitHub", "7", "GitHub"), CancellationToken.None);
      var updated = await userStore.UpdateAsync(user, CancellationToken.None);

      // Assert
      Assert.True(created.Succeeded);
      Assert.True(updated.Succeeded);
      Assert.Equal(user.Id, (await userStore.FindByNameAsync("NEW", CancellationToken.None))!.Id);
      Assert.Equal(user.Id, (await userStore.FindByLoginAsync("GitHub", "7", CancellationToken.None))!.Id);
      Assert.True((await userStore.DeleteAsync(user, CancellationToken.None)).Succeeded);
      Assert.Null(await userStore.FindByNameAsync("NEW", CancellationToken.None));
    }
    finally
    {
      await DatabaseFixture.Client.DeleteTableAsync(tableName);
    }
  }

  [Fact]
  public async Task Should_RemoveLegacyIndexes_When_Requested()
  {
    // Arrange
    var tableName = await LegacyTable.CreateAsync(DatabaseFixture.Client);

    try
    {
      var seed = await SeedLegacyData(tableName);
      var options = new DynamoDbOptions { Database = DatabaseFixture.Client, DefaultTableName = tableName };
      await DynamoDbTableSetup.MigrateAsync(options, DatabaseFixture.Client);

      // Act
      await DynamoDbTableSetup.RemoveLegacyIndexesAsync(options, DatabaseFixture.Client);

      // Assert
      var table = await DatabaseFixture.Client.DescribeTableAsync(tableName);
      Assert.Equal(TableStatus.ACTIVE, table.Table.TableStatus);
      Assert.Empty(table.Table.GlobalSecondaryIndexes ?? []);
      var userStore = new DynamoDbUserStore<DynamoDbUser>(TestUtils.GetOptions(new() { Database = DatabaseFixture.Client, DefaultTableName = tableName }));
      Assert.Equal(seed.Anna.Id, (await userStore.FindByNameAsync(seed.Anna.NormalizedUserName!, CancellationToken.None))!.Id);

      // Running it again on a table without the indexes is a no-op
      await DynamoDbTableSetup.RemoveLegacyIndexesAsync(options, DatabaseFixture.Client);
    }
    finally
    {
      await DatabaseFixture.Client.DeleteTableAsync(tableName);
    }
  }

  [Fact]
  public async Task Should_LogWarning_When_EnsureInitializedSeesLegacyIndexes()
  {
    // Arrange
    var tableName = await LegacyTable.CreateAsync(DatabaseFixture.Client);

    try
    {
      var loggerProvider = new TestLoggerProvider();
      var services = new ServiceCollection();
      services.AddLogging(x => x.AddProvider(loggerProvider));
      services.AddIdentityCore<DynamoDbUser>().AddRoles<DynamoDbRole>().AddDynamoDbStores<DynamoDbUser, DynamoDbRole>()
        .UseDatabase(DatabaseFixture.Client).SetDefaultTableName(tableName);
      var provider = services.BuildServiceProvider();

      // Act
      await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(provider);
      await AspNetCoreIdentityDynamoDbSetup.MigrateAsync(provider);
      await AspNetCoreIdentityDynamoDbSetup.RemoveLegacyIndexesAsync(provider);
      loggerProvider.Entries.Clear();
      await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(provider);

      // Assert
      Assert.DoesNotContain(loggerProvider.Entries, x => x.Level == LogLevel.Warning);
      Assert.Empty((await DatabaseFixture.Client.DescribeTableAsync(tableName)).Table.GlobalSecondaryIndexes ?? []);
    }
    finally
    {
      await DatabaseFixture.Client.DeleteTableAsync(tableName);
    }
  }

  [Fact]
  public async Task Should_LogWarning_When_RemovingIndexesIsDenied()
  {
    // Arrange
    var database = new Mock<IAmazonDynamoDB>();
    database
      .Setup(x => x.DescribeTableAsync(It.IsAny<DescribeTableRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new DescribeTableResponse
      {
        Table = new()
        {
          TableName = "legacy",
          TableStatus = TableStatus.ACTIVE,
          GlobalSecondaryIndexes = LegacyTable.IndexNames
            .Select(x => new GlobalSecondaryIndexDescription { IndexName = x, IndexStatus = IndexStatus.ACTIVE })
            .ToList(),
        },
      });
    database
      .Setup(x => x.UpdateTableAsync(It.IsAny<UpdateTableRequest>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new AmazonDynamoDBException("Access denied") { ErrorCode = "AccessDeniedException" });
    var loggerProvider = new TestLoggerProvider();
    var services = new ServiceCollection();
    services.AddLogging(x => x.AddProvider(loggerProvider));
    services.AddIdentityCore<DynamoDbUser>().AddRoles<DynamoDbRole>().AddDynamoDbStores<DynamoDbUser, DynamoDbRole>()
      .UseDatabase(database.Object).SetDefaultTableName("legacy");

    // Act
    await AspNetCoreIdentityDynamoDbSetup.RemoveLegacyIndexesAsync(services.BuildServiceProvider());

    // Assert
    database.Verify(x => x.UpdateTableAsync(It.IsAny<UpdateTableRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    Assert.Contains(loggerProvider.Entries, x =>
      x.Level == LogLevel.Warning && x.Message.Contains("Unable to remove global secondary index NormalizedEmail-index"));
  }

  [Fact]
  public async Task Should_PointAtOneUserAndWarn_When_LegacyUsersShareAUserName()
  {
    // Arrange, version 2 enforced no uniqueness, so a race could leave two users with one name
    var tableName = await LegacyTable.CreateAsync(DatabaseFixture.Client);

    try
    {
      var context = new DynamoDBContextBuilder().WithDynamoDBClient(() => DatabaseFixture.Client).Build();
      var save = new SaveConfig { OverrideTableName = tableName };
      var first = new DynamoDbUser { UserName = "twin", NormalizedUserName = "TWIN" };
      var second = new DynamoDbUser { UserName = "twin", NormalizedUserName = "TWIN" };
      await context.SaveAsync(first, save);
      await context.SaveAsync(second, save);
      var loggerProvider = new TestLoggerProvider();
      var services = new ServiceCollection();
      services.AddLogging(x => x.AddProvider(loggerProvider));
      services.AddIdentityCore<DynamoDbUser>().AddRoles<DynamoDbRole>().AddDynamoDbStores<DynamoDbUser, DynamoDbRole>()
        .UseDatabase(DatabaseFixture.Client).SetDefaultTableName(tableName);

      // Act
      var result = await AspNetCoreIdentityDynamoDbSetup.MigrateAsync(services.BuildServiceProvider());

      // Assert
      Assert.Equal(2, result.Users);
      var userStore = new DynamoDbUserStore<DynamoDbUser>(TestUtils.GetOptions(new() { Database = DatabaseFixture.Client, DefaultTableName = tableName }));
      var found = await userStore.FindByNameAsync("TWIN", CancellationToken.None);
      Assert.Contains(found!.Id, new[] { first.Id, second.Id });
      Assert.Contains(loggerProvider.Entries, x => x.Level == LogLevel.Warning && x.Message.Contains("share the value of USERNAME#TWIN"));
    }
    finally
    {
      await DatabaseFixture.Client.DeleteTableAsync(tableName);
    }
  }

  private sealed record LegacySeed(DynamoDbUser Anna, DynamoDbUser Bob, DynamoDbRole Role, string ProviderKey, byte[] CredentialId);

  // Writes items the way version 2 did, through the object persistence model and without lookups
  private static async Task<LegacySeed> SeedLegacyData(string tableName)
  {
    var context = new DynamoDBContextBuilder().WithDynamoDBClient(() => DatabaseFixture.Client).Build();
    var save = new SaveConfig { OverrideTableName = tableName };
    var anna = new DynamoDbUser { UserName = "anna", NormalizedUserName = "ANNA", Email = "anna@test.se", NormalizedEmail = "ANNA@TEST.SE" };
    var bob = new DynamoDbUser { UserName = "bob", NormalizedUserName = "BOB", Email = "bob@test.se", NormalizedEmail = "BOB@TEST.SE" };
    var role = new DynamoDbRole { Name = "Admin", NormalizedName = "ADMIN" };
    var providerKey = Guid.NewGuid().ToString();
    var credentialId = Guid.NewGuid().ToByteArray();

    await context.SaveAsync(anna, save);
    await context.SaveAsync(bob, save);
    await context.SaveAsync(role, save);
    await context.SaveAsync(new DynamoDbUserLogin { UserId = anna.Id, LoginProvider = "GitHub", ProviderKey = providerKey }, save);
    await context.SaveAsync(new DynamoDbUserClaim { UserId = anna.Id, ClaimType = "plan", ClaimValue = "pro" }, save);
    await context.SaveAsync(new DynamoDbUserRole { UserId = bob.Id, RoleName = "ADMIN" }, save);
    await context.SaveAsync(new DynamoDbUserPasskey
    {
      UserId = anna.Id,
      CredentialId = Convert.ToBase64String(credentialId).TrimEnd('=').Replace('+', '-').Replace('/', '_'),
      PublicKey = "cGs",
      Name = "key",
      CreatedAt = DateTimeOffset.UtcNow,
      Transports = ["internal"],
      AttestationObject = "YXR0",
      ClientDataJson = "Y2Rq",
    }, save);

    return new LegacySeed(anna, bob, role, providerKey, credentialId);
  }
}
