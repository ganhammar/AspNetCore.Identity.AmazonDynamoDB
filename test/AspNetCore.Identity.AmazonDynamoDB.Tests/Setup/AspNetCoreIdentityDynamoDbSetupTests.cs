using Amazon.DynamoDBv2;
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

  private static DynamoDbBuilder CreateBuilder(IServiceCollection services) => services
    .AddIdentityCore<DynamoDbUser>()
    .AddRoles<DynamoDbRole>()
    .AddDynamoDbStores()
    .SetDefaultTableName(DatabaseFixture.TableName);
}
