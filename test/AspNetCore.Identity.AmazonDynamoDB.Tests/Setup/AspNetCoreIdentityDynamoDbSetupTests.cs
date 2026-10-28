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
  public async Task Should_CreateTableWithoutIndexes_When_TableIsNew()
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
      await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

      // Assert
      var table = await DatabaseFixture.Client.DescribeTableAsync(tableName);
      Assert.Equal(TableStatus.ACTIVE, table.Table.TableStatus);
      Assert.Empty(table.Table.GlobalSecondaryIndexes ?? []);
      Assert.Equal(2, table.Table.AttributeDefinitions.Count);
    }
    finally
    {
      await DatabaseFixture.Client.DeleteTableAsync(tableName);
    }
  }

  [Fact]
  public async Task Should_CreateProvisionedTable_When_OptionsAreProvisioned()
  {
    // Arrange
    var tableName = Guid.NewGuid().ToString();
    var options = TestUtils.GetOptions(new()
    {
      Database = DatabaseFixture.Client,
      DefaultTableName = tableName,
      BillingMode = BillingMode.PROVISIONED,
      ProvisionedThroughput = new ProvisionedThroughput { ReadCapacityUnits = 2, WriteCapacityUnits = 3 },
    });

    try
    {
      // Act
      await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

      // Assert
      var table = await DatabaseFixture.Client.DescribeTableAsync(tableName);
      Assert.Equal(2, table.Table.ProvisionedThroughput.ReadCapacityUnits);
      Assert.Equal(3, table.Table.ProvisionedThroughput.WriteCapacityUnits);
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
      Assert.Empty(table.Table.GlobalSecondaryIndexes ?? []);
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
      .AddDynamoDbStores<DynamoDbUser, DynamoDbRole>()
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
  public async Task Should_LogWarning_When_TableHasLegacyIndexes()
  {
    // Arrange
    var database = new Mock<IAmazonDynamoDB>();
    database
      .Setup(x => x.DescribeTableAsync(It.IsAny<DescribeTableRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(CreateDescribeTableResponse(TableStatus.ACTIVE, LegacyTable.IndexNames));
    var loggerProvider = new TestLoggerProvider();

    // Act
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(
      CreateServiceProvider(database.Object, loggerProvider));

    // Assert
    database.Verify(x => x.UpdateTableAsync(It.IsAny<UpdateTableRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    database.Verify(x => x.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    Assert.Contains(loggerProvider.Entries, x =>
      x.Level == LogLevel.Warning && x.Message.Contains("indexes of version 2") && x.Message.Contains("NormalizedEmail-index"));
  }

  [Fact]
  public async Task Should_NotLogWarning_When_TableHasNoLegacyIndexes()
  {
    // Arrange
    var database = new Mock<IAmazonDynamoDB>();
    database
      .Setup(x => x.DescribeTableAsync(It.IsAny<DescribeTableRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(CreateDescribeTableResponse(TableStatus.ACTIVE, []));
    var loggerProvider = new TestLoggerProvider();

    // Act
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(
      CreateServiceProvider(database.Object, loggerProvider));

    // Assert
    Assert.DoesNotContain(loggerProvider.Entries, x => x.Level == LogLevel.Warning);
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
  public async Task Should_WaitForTable_When_ItIsBeingCreatedElsewhere()
  {
    // Arrange
    var database = new Mock<IAmazonDynamoDB>();
    database
      .SetupSequence(x => x.DescribeTableAsync(It.IsAny<DescribeTableRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(CreateDescribeTableResponse(TableStatus.CREATING, []))
      .ReturnsAsync(CreateDescribeTableResponse(TableStatus.CREATING, []))
      .ReturnsAsync(CreateDescribeTableResponse(TableStatus.ACTIVE, []));

    // Act
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(
      CreateServiceProvider(database.Object, new TestLoggerProvider()));

    // Assert
    database.Verify(x => x.DescribeTableAsync(It.IsAny<DescribeTableRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    database.Verify(x => x.CreateTableAsync(It.IsAny<CreateTableRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    database.Verify(x => x.UpdateTableAsync(It.IsAny<UpdateTableRequest>(), It.IsAny<CancellationToken>()), Times.Never);
  }

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

  private static DynamoDbBuilder CreateBuilder(IServiceCollection services) => services
    .AddIdentityCore<DynamoDbUser>()
    .AddRoles<DynamoDbRole>()
    .AddDynamoDbStores<DynamoDbUser, DynamoDbRole>()
    .SetDefaultTableName(DatabaseFixture.TableName);
}
