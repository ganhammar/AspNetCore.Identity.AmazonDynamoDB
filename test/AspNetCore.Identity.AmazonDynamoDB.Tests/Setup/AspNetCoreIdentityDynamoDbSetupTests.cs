using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AspNetCore.Identity.AmazonDynamoDB.Tests;

[Collection(Constants.DatabaseCollection)]
public class AspNetCoreIdentityDynamoDbSetupTests
{
  [Fact]
  public async Task Should_SetupTables_When_CalledSynchronously()
  {
    // Arrange
    var options = TestUtils.GetOptions(new()
    {
      Database = DatabaseFixture.Client,
    });

    // Act
    AspNetCoreIdentityDynamoDbSetup.EnsureInitialized(options);

    // Assert
    var tableNames = await DatabaseFixture.Client.ListTablesAsync();
    Assert.Contains(DatabaseFixture.TableName, tableNames.TableNames);
  }

  [Fact]
  public async Task Should_SetupTables_When_CalledSynchronouslyWithServiceProvider()
  {
    // Arrange
    var services = new ServiceCollection();
    CreateBuilder(services).UseDatabase(DatabaseFixture.Client);

    // Act
    AspNetCoreIdentityDynamoDbSetup.EnsureInitialized(services.BuildServiceProvider());

    // Assert
    var tableNames = await DatabaseFixture.Client.ListTablesAsync();
    Assert.Contains(DatabaseFixture.TableName, tableNames.TableNames);
  }

  [Fact]
  public async Task Should_SetupTables_When_CalledAsynchronously()
  {
    // Arrange
    var options = TestUtils.GetOptions(new()
    {
      Database = DatabaseFixture.Client,
    });

    // Act
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Assert
    var tableNames = await DatabaseFixture.Client.ListTablesAsync();
    Assert.Contains(DatabaseFixture.TableName, tableNames.TableNames);
  }

  [Fact]
  public async Task Should_SetupTables_When_CalledAsynchronouslyWithServiceProvider()
  {
    // Arrange
    var services = new ServiceCollection();
    CreateBuilder(services).UseDatabase(DatabaseFixture.Client);

    // Act
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(services.BuildServiceProvider());

    // Assert
    var tableNames = await DatabaseFixture.Client.ListTablesAsync();
    Assert.Contains(DatabaseFixture.TableName, tableNames.TableNames);
  }

  [Fact]
  public async Task Should_SetupTables_When_CalledAsynchronouslyWithDatbaseInServiceProvider()
  {
    // Arrange
    var services = new ServiceCollection();
    services.AddSingleton<IAmazonDynamoDB>(DatabaseFixture.Client);
    CreateBuilder(services);

    // Act
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(services.BuildServiceProvider());

    // Assert
    var tableNames = await DatabaseFixture.Client.ListTablesAsync();
    Assert.Contains(DatabaseFixture.TableName, tableNames.TableNames);
  }

  [Fact]
  public async Task Should_SetupTables_When_CalledSynchronouslyWithDatbaseInServiceProvider()
  {
    // Arrange
    var services = new ServiceCollection();
    services.AddSingleton<IAmazonDynamoDB>(DatabaseFixture.Client);
    CreateBuilder(services);

    // Act
    AspNetCoreIdentityDynamoDbSetup.EnsureInitialized(services.BuildServiceProvider());

    // Assert
    var tableNames = await DatabaseFixture.Client.ListTablesAsync();
    Assert.Contains(DatabaseFixture.TableName, tableNames.TableNames);
  }

  [Fact]
  public async Task Should_AddMissingIndex_When_TableAlreadyExists()
  {
    // Arrange
    var tableName = Guid.NewGuid().ToString();
    var options = TestUtils.GetOptions(new()
    {
      Database = DatabaseFixture.Client,
      DefaultTableName = tableName,
    });
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    await DatabaseFixture.Client.UpdateTableAsync(new UpdateTableRequest
    {
      TableName = tableName,
      GlobalSecondaryIndexUpdates = new()
      {
        new() { Delete = new() { IndexName = "CredentialId-index" } },
      },
    });
    await WaitUntilIndexIsRemoved(tableName, "CredentialId-index");

    try
    {
      // Act
      await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

      // Assert
      var table = await DatabaseFixture.Client.DescribeTableAsync(tableName);
      var index = Assert.Single(table.Table.GlobalSecondaryIndexes, x => x.IndexName == "CredentialId-index");
      Assert.Equal(IndexStatus.ACTIVE, index.IndexStatus);
    }
    finally
    {
      await DatabaseFixture.Client.DeleteTableAsync(tableName);
    }
  }

  private static async Task WaitUntilIndexIsRemoved(string tableName, string indexName)
  {
    for (var attempt = 0; attempt < 30; attempt++)
    {
      var table = await DatabaseFixture.Client.DescribeTableAsync(tableName);
      if (table.Table.GlobalSecondaryIndexes.TrueForAll(x => x.IndexName != indexName))
      {
        return;
      }

      await Task.Delay(TimeSpan.FromSeconds(1));
    }

    throw new TimeoutException($"Index {indexName} was not removed from {tableName}");
  }

  private static DynamoDbBuilder CreateBuilder(IServiceCollection services) => services
    .AddIdentityCore<DynamoDbUser>()
    .AddRoles<DynamoDbRole>()
    .AddDynamoDbStores()
    .SetDefaultTableName(DatabaseFixture.TableName);
}
