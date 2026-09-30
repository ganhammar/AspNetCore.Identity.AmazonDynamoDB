using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
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

  [Fact]
  public async Task Should_AddMissingIndex_When_TableIsProvisionedButOptionsAreNot()
  {
    // Arrange
    var tableName = Guid.NewGuid().ToString();
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(TestUtils.GetOptions(new()
    {
      Database = DatabaseFixture.Client,
      DefaultTableName = tableName,
      BillingMode = BillingMode.PROVISIONED,
    }));
    await RemoveIndex(tableName, "CredentialId-index");

    try
    {
      // Act
      await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(TestUtils.GetOptions(new()
      {
        Database = DatabaseFixture.Client,
        DefaultTableName = tableName,
      }));

      // Assert
      var index = await GetIndex(tableName, "CredentialId-index");
      Assert.Equal(IndexStatus.ACTIVE, index.IndexStatus);
      Assert.Equal(1, index.ProvisionedThroughput.ReadCapacityUnits);
      Assert.Equal(1, index.ProvisionedThroughput.WriteCapacityUnits);
    }
    finally
    {
      await DatabaseFixture.Client.DeleteTableAsync(tableName);
    }
  }

  [Fact]
  public async Task Should_AddMissingIndex_When_TableIsOnDemandButOptionsAreProvisioned()
  {
    // Arrange
    var tableName = Guid.NewGuid().ToString();
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(TestUtils.GetOptions(new()
    {
      Database = DatabaseFixture.Client,
      DefaultTableName = tableName,
    }));
    await RemoveIndex(tableName, "CredentialId-index");

    try
    {
      // Act
      await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(TestUtils.GetOptions(new()
      {
        Database = DatabaseFixture.Client,
        DefaultTableName = tableName,
        BillingMode = BillingMode.PROVISIONED,
      }));

      // Assert
      var index = await GetIndex(tableName, "CredentialId-index");
      Assert.Equal(IndexStatus.ACTIVE, index.IndexStatus);
      Assert.Equal(0, index.ProvisionedThroughput?.ReadCapacityUnits ?? 0);
    }
    finally
    {
      await DatabaseFixture.Client.DeleteTableAsync(tableName);
    }
  }

  [Fact]
  public async Task Should_AddMissingIndex_When_SetupRunsConcurrently()
  {
    // Arrange
    var tableName = Guid.NewGuid().ToString();
    var options = TestUtils.GetOptions(new()
    {
      Database = DatabaseFixture.Client,
      DefaultTableName = tableName,
    });
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    await RemoveIndex(tableName, "CredentialId-index");

    try
    {
      // Act
      await Task.WhenAll(Enumerable.Range(0, 3)
        .Select(_ => AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options)));

      // Assert
      var index = await GetIndex(tableName, "CredentialId-index");
      Assert.Equal(IndexStatus.ACTIVE, index.IndexStatus);
    }
    finally
    {
      await DatabaseFixture.Client.DeleteTableAsync(tableName);
    }
  }

  [Fact]
  public async Task Should_CreateTable_When_SetupRunsConcurrently()
  {
    // Arrange
    var tableName = Guid.NewGuid().ToString();
    var options = TestUtils.GetOptions(new()
    {
      Database = DatabaseFixture.Client,
      DefaultTableName = tableName,
    });

    try
    {
      // Act
      await Task.WhenAll(Enumerable.Range(0, 3)
        .Select(_ => AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options)));

      // Assert
      var table = await DatabaseFixture.Client.DescribeTableAsync(tableName);
      Assert.Equal(TableStatus.ACTIVE, table.Table.TableStatus);
      Assert.Equal(7, table.Table.GlobalSecondaryIndexes.Count);
    }
    finally
    {
      await DatabaseFixture.Client.DeleteTableAsync(tableName);
    }
  }

  [Fact]
  public async Task Should_UseExistingTable_When_ItIsNotOnTheFirstPageOfTables()
  {
    // Arrange, table names are listed in alphabetical order, 100 per page
    var prefix = $"zz-{Guid.NewGuid():N}";
    var tableName = $"{prefix}-z";
    var fillerTableNames = Enumerable.Range(0, 101).Select(x => $"{prefix}-{x:D3}").ToList();
    var options = TestUtils.GetOptions(new()
    {
      Database = DatabaseFixture.Client,
      DefaultTableName = tableName,
    });

    try
    {
      await Task.WhenAll(fillerTableNames.Select(x => DatabaseFixture.Client.CreateTableAsync(new CreateTableRequest
      {
        TableName = x,
        BillingMode = BillingMode.PAY_PER_REQUEST,
        KeySchema = new() { new("PartitionKey", KeyType.HASH) },
        AttributeDefinitions = new() { new("PartitionKey", ScalarAttributeType.S) },
      })));
      await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

      // Act
      await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

      // Assert
      var table = await DatabaseFixture.Client.DescribeTableAsync(tableName);
      Assert.Equal(TableStatus.ACTIVE, table.Table.TableStatus);
    }
    finally
    {
      await Task.WhenAll(fillerTableNames.Append(tableName).Select(async x =>
      {
        try
        {
          await DatabaseFixture.Client.DeleteTableAsync(x);
        }
        catch (ResourceNotFoundException)
        {
        }
      }));
    }
  }

  // Passkey lookups query the index for all attributes, which DynamoDB only allows when every
  // attribute is projected, changing the projection breaks earlier versions using the same table
  [Fact]
  public async Task Should_ProjectAllAttributes_When_CreatingCredentialIdIndex()
  {
    // Arrange
    var options = TestUtils.GetOptions(new()
    {
      Database = DatabaseFixture.Client,
    });

    // Act
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Assert
    var index = await GetIndex(DatabaseFixture.TableName, "CredentialId-index");
    Assert.Equal(ProjectionType.ALL, index.Projection.ProjectionType);
  }

  [Fact]
  public async Task Should_LogProgress_When_CreatingTable()
  {
    // Arrange
    var tableName = Guid.NewGuid().ToString();
    var loggerProvider = new TestLoggerProvider();
    var services = new ServiceCollection();
    services.AddLogging(x => x.AddProvider(loggerProvider));
    services
      .AddIdentityCore<DynamoDbUser>()
      .AddRoles<DynamoDbRole>()
      .AddDynamoDbStores()
      .UseDatabase(DatabaseFixture.Client)
      .SetDefaultTableName(tableName);

    try
    {
      // Act
      await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(services.BuildServiceProvider());

      // Assert
      Assert.Contains(loggerProvider.Entries, x =>
        x.Level == LogLevel.Information && x.Message == $"Creating table {tableName}");
    }
    finally
    {
      await DatabaseFixture.Client.DeleteTableAsync(tableName);
    }
  }

  [Fact]
  public async Task Should_OnlyVerifyThatTableExists_When_DescribeTableIsDenied()
  {
    // Arrange
    var database = new Mock<IAmazonDynamoDB>();
    database
      .Setup(x => x.DescribeTableAsync(It.IsAny<DescribeTableRequest>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new AmazonDynamoDBException("Access denied") { ErrorCode = "AccessDeniedException" });
    database
      .Setup(x => x.ListTablesAsync(
        It.Is<ListTablesRequest>(y => y.ExclusiveStartTableName == null), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new ListTablesResponse { TableNames = ["another-table"], LastEvaluatedTableName = "another-table" });
    database
      .Setup(x => x.ListTablesAsync(
        It.Is<ListTablesRequest>(y => y.ExclusiveStartTableName == "another-table"), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new ListTablesResponse { TableNames = [DatabaseFixture.TableName] });
    var loggerProvider = new TestLoggerProvider();

    // Act
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(
      CreateServiceProvider(database.Object, loggerProvider));

    // Assert
    database.Verify(x => x.CreateTableAsync(It.IsAny<CreateTableRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    database.Verify(x => x.UpdateTableAsync(It.IsAny<UpdateTableRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    Assert.Contains(loggerProvider.Entries, x =>
      x.Level == LogLevel.Warning && x.Message.Contains($"Unable to describe table {DatabaseFixture.TableName}"));
  }

  [Fact]
  public async Task Should_LogWarning_When_UpdateTableIsDenied()
  {
    // Arrange
    var database = new Mock<IAmazonDynamoDB>();
    database
      .Setup(x => x.DescribeTableAsync(It.IsAny<DescribeTableRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(CreateDescribeTableResponse(
        TableStatus.ACTIVE, IndexNames.Where(x => x != "CredentialId-index")));
    database
      .Setup(x => x.UpdateTableAsync(It.IsAny<UpdateTableRequest>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new AmazonDynamoDBException("Access denied") { ErrorCode = "AccessDeniedException" });
    var loggerProvider = new TestLoggerProvider();

    // Act
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(
      CreateServiceProvider(database.Object, loggerProvider));

    // Assert
    database.Verify(x => x.UpdateTableAsync(
      It.Is<UpdateTableRequest>(y => y.GlobalSecondaryIndexUpdates[0].Create.IndexName == "CredentialId-index"),
      It.IsAny<CancellationToken>()), Times.Once);
    Assert.Contains(loggerProvider.Entries, x =>
      x.Level == LogLevel.Warning && x.Message.Contains("Unable to add global secondary index CredentialId-index"));
  }

  [Fact]
  public async Task Should_WaitForTable_When_ItIsBeingCreatedElsewhere()
  {
    // Arrange
    var database = new Mock<IAmazonDynamoDB>();
    database
      .SetupSequence(x => x.DescribeTableAsync(It.IsAny<DescribeTableRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(CreateDescribeTableResponse(TableStatus.CREATING, IndexNames))
      .ReturnsAsync(CreateDescribeTableResponse(TableStatus.CREATING, IndexNames))
      .ReturnsAsync(CreateDescribeTableResponse(TableStatus.ACTIVE, IndexNames));

    // Act
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(
      CreateServiceProvider(database.Object, new TestLoggerProvider()));

    // Assert
    database.Verify(x => x.DescribeTableAsync(It.IsAny<DescribeTableRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    database.Verify(x => x.CreateTableAsync(It.IsAny<CreateTableRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    database.Verify(x => x.UpdateTableAsync(It.IsAny<UpdateTableRequest>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  private static readonly string[] IndexNames =
  [
    "NormalizedEmail-index",
    "NormalizedUserName-index",
    "ClaimType-ClaimValue-index",
    "RoleName-index",
    "LoginProvider-ProviderKey-index",
    "CredentialId-index",
    "NormalizedName-index",
  ];

  private static DescribeTableResponse CreateDescribeTableResponse(
    TableStatus tableStatus, IEnumerable<string> indexNames) => new()
    {
      Table = new()
      {
        TableName = DatabaseFixture.TableName,
        TableStatus = tableStatus,
        BillingModeSummary = new() { BillingMode = BillingMode.PAY_PER_REQUEST },
        GlobalSecondaryIndexes = indexNames.Select(x => new GlobalSecondaryIndexDescription
        {
          IndexName = x,
          IndexStatus = Equals(tableStatus, TableStatus.ACTIVE) ? IndexStatus.ACTIVE : IndexStatus.CREATING,
        }).ToList(),
      },
    };

  private static IServiceProvider CreateServiceProvider(IAmazonDynamoDB database, ILoggerProvider loggerProvider)
  {
    var services = new ServiceCollection();
    services.AddLogging(x => x.AddProvider(loggerProvider));
    CreateBuilder(services).UseDatabase(database);
    return services.BuildServiceProvider();
  }

  private static async Task RemoveIndex(string tableName, string indexName)
  {
    await DatabaseFixture.Client.UpdateTableAsync(new UpdateTableRequest
    {
      TableName = tableName,
      GlobalSecondaryIndexUpdates = new()
      {
        new() { Delete = new() { IndexName = indexName } },
      },
    });
    await WaitUntilIndexIsRemoved(tableName, indexName);
  }

  private static async Task<GlobalSecondaryIndexDescription> GetIndex(string tableName, string indexName)
  {
    var table = await DatabaseFixture.Client.DescribeTableAsync(tableName);
    return Assert.Single(table.Table.GlobalSecondaryIndexes, x => x.IndexName == indexName);
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
