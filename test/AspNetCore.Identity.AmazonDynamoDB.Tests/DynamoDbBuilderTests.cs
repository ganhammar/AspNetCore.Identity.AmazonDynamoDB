using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace AspNetCore.Identity.AmazonDynamoDB.Tests;

public class DynamoDbBuilderTests
{
  [Fact]
  public void Should_ThrowException_When_ServicesIsNullInConstructor()
  {
    // Arrange
    var services = (IServiceCollection)null!;

    // Act & Assert
    var exception = Assert.Throws<ArgumentNullException>(() => new DynamoDbBuilder(services));
    Assert.Equal("services", exception.ParamName);
  }

  [Fact]
  public void Should_ThrowException_When_TryingToConfigureAndActionIsNull()
  {
    // Arrange
    var services = new ServiceCollection();

    // Act & Assert
    var exception = Assert.Throws<ArgumentNullException>(() =>
        CreateBuilder(services).Configure(null!));
    Assert.Equal("configuration", exception.ParamName);
  }

  [Fact]
  public void Should_ThrowException_When_SetDefaultTableNameAndNameIsNull()
  {
    // Arrange
    var services = new ServiceCollection();

    // Act & Assert
    var exception = Assert.Throws<ArgumentNullException>(() =>
        CreateBuilder(services).SetDefaultTableName(null!));
    Assert.Equal("name", exception.ParamName);
  }

  [Fact]
  public void Should_SetTableName_When_CallingSetDefaultTableName()
  {
    // Arrange
    var services = new ServiceCollection();

    // Act
    CreateBuilder(services).SetDefaultTableName("test");

    // Assert
    var serviceProvider = services.BuildServiceProvider();
    var options = serviceProvider.GetRequiredService<IOptionsMonitor<DynamoDbOptions>>().CurrentValue;
    Assert.Equal("test", options.DefaultTableName);
  }

  [Fact]
  public void Should_ThrowException_When_CallingUseDatabaseAndDatabaseIsNull()
  {
    // Arrange
    var services = new ServiceCollection();

    // Act & Assert
    var exception = Assert.Throws<ArgumentNullException>(() =>
        CreateBuilder(services).UseDatabase(null!));
    Assert.Equal("database", exception.ParamName);
  }

  [Fact]
  public void Should_SetDatabase_When_CallingUseDatabase()
  {
    // Arrange
    var services = new ServiceCollection();

    // Act
    CreateBuilder(services).UseDatabase(DatabaseFixture.Client);

    // Assert
    var serviceProvider = services.BuildServiceProvider();
    var options = serviceProvider.GetRequiredService<IOptionsMonitor<DynamoDbOptions>>().CurrentValue;
    Assert.Equal(DatabaseFixture.Client, options.Database);
  }

  [Fact]
  public void Should_ThrowException_When_SettingBillingModeAndBillingModeIsNull()
  {
    // Arrange
    var services = new ServiceCollection();

    // Act & Assert
    var exception = Assert.Throws<ArgumentNullException>(() =>
        CreateBuilder(services).SetBillingMode(null!));
    Assert.Equal("billingMode", exception.ParamName);
  }

  [Fact]
  public void Should_SetBillingMode_When_CallingSetBillingMode()
  {
    // Arrange
    var services = new ServiceCollection();

    // Act
    CreateBuilder(services).SetBillingMode(BillingMode.PROVISIONED);

    // Assert
    var serviceProvider = services.BuildServiceProvider();
    var options = serviceProvider.GetRequiredService<IOptionsMonitor<DynamoDbOptions>>().CurrentValue;
    Assert.Equal(BillingMode.PROVISIONED, options.BillingMode);
  }

  [Fact]
  public void Should_ThrowException_When_SettingProvisionedThroughputAndProvisionedThroughputIsNull()
  {
    // Arrange
    var services = new ServiceCollection();

    // Act & Assert
    var exception = Assert.Throws<ArgumentNullException>(() =>
        CreateBuilder(services).SetProvisionedThroughput(null!));
    Assert.Equal("provisionedThroughput", exception.ParamName);
  }

  [Fact]
  public void Should_SetProvisionedThroughput_When_CallingSetProvisionedThroughput()
  {
    // Arrange
    var services = new ServiceCollection();
    var throughput = new ProvisionedThroughput
    {
      ReadCapacityUnits = 99,
      WriteCapacityUnits = 99,
    };

    // Act
    CreateBuilder(services).SetProvisionedThroughput(throughput);

    // Assert
    var serviceProvider = services.BuildServiceProvider();
    var options = serviceProvider.GetRequiredService<IOptionsMonitor<DynamoDbOptions>>().CurrentValue;
    Assert.Equal(throughput, options.ProvisionedThroughput);
  }

  [Fact]
  public async Task Should_SetTableAlias_When_SettingDefaultTableName()
  {
    // Arrange
    var tableName = "test";

    // Act
    var host = Host.CreateDefaultBuilder()
      .ConfigureServices(services => CreateBuilder(services).SetDefaultTableName(tableName))
      .Build();
    await host.StartAsync();

    // Assert
    var serviceProvider = host.Services;
    var options = serviceProvider.GetRequiredService<IOptionsMonitor<DynamoDbOptions>>().CurrentValue;
    Assert.Equal(tableName, options.DefaultTableName);
    Assert.True(AWSConfigsDynamoDB.Context.TableAliases.ContainsKey("identity"));
    Assert.Equal(tableName, AWSConfigsDynamoDB.Context.TableAliases["identity"]);
    await host.StopAsync();
  }

  [Fact]
  public async Task Should_UpdateAlias_When_SettingsChanges()
  {
    // Arrange
    var tableName = "test";

    // Act
    var host = Host.CreateDefaultBuilder()
      .ConfigureServices(services =>
      {
        CreateBuilder(services);
        services.AddSingleton<IOptionsMonitor<DynamoDbOptions>>(sp =>
        {
          var initialOptions = sp.GetRequiredService<IOptions<DynamoDbOptions>>().Value;
          return new TestDynamoDbOptionsMonitor(initialOptions);
        });
      })
      .Build();
    await host.StartAsync();
    var serviceProvider = host.Services;
    var options = serviceProvider.GetRequiredService<IOptionsMonitor<DynamoDbOptions>>();
    (options as TestDynamoDbOptionsMonitor)!.UpdateOptions(new() { DefaultTableName = tableName });

    // Assert
    Assert.Equal(tableName, options.CurrentValue.DefaultTableName);
    Assert.True(AWSConfigsDynamoDB.Context.TableAliases.ContainsKey("identity"));
    Assert.Equal(tableName, AWSConfigsDynamoDB.Context.TableAliases["identity"]);
    await host.StopAsync();
  }

  private static DynamoDbBuilder CreateBuilder(IServiceCollection services)
    => services.AddIdentityCore<DynamoDbUser>().AddRoles<DynamoDbRole>().AddDynamoDbStores();
}
