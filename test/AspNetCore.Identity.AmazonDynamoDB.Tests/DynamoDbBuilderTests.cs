using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.DependencyInjection;
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
  public void Should_ThrowException_When_SetRolesTableNameAndNameIsNull()
  {
    // Arrange
    var services = new ServiceCollection();

    // Act & Assert
    var exception = Assert.Throws<ArgumentNullException>(() =>
        CreateBuilder(services).SetRolesTableName(null!));
    Assert.Equal("name", exception.ParamName);
  }

  [Fact]
  public void Should_SetTableName_When_CallingSetRolesTableName()
  {
    // Arrange
    var services = new ServiceCollection();

    // Act
    CreateBuilder(services).SetRolesTableName("test");

    // Assert
    var serviceProvider = services.BuildServiceProvider();
    var options = serviceProvider.GetRequiredService<IOptionsMonitor<DynamoDbOptions>>().CurrentValue;
    Assert.Equal("test", options.RolesTableName);
  }

  [Fact]
  public void Should_ThrowException_When_SetUserClaimsTableNameAndNameIsNull()
  {
    // Arrange
    var services = new ServiceCollection();

    // Act & Assert
    var exception = Assert.Throws<ArgumentNullException>(() =>
        CreateBuilder(services).SetUserClaimsTableName(null!));
    Assert.Equal("name", exception.ParamName);
  }

  [Fact]
  public void Should_SetTableName_When_CallingSetUserClaimsTableName()
  {
    // Arrange
    var services = new ServiceCollection();

    // Act
    CreateBuilder(services).SetUserClaimsTableName("test");

    // Assert
    var serviceProvider = services.BuildServiceProvider();
    var options = serviceProvider.GetRequiredService<IOptionsMonitor<DynamoDbOptions>>().CurrentValue;
    Assert.Equal("test", options.UserClaimsTableName);
  }

  [Fact]
  public void Should_ThrowException_When_SetUserTokensTableNameAndNameIsNull()
  {
    // Arrange
    var services = new ServiceCollection();

    // Act & Assert
    var exception = Assert.Throws<ArgumentNullException>(() =>
        CreateBuilder(services).SetUserTokensTableName(null!));
    Assert.Equal("name", exception.ParamName);
  }

  [Fact]
  public void Should_SetTableName_When_CallingSetUserTokensTableName()
  {
    // Arrange
    var services = new ServiceCollection();

    // Act
    CreateBuilder(services).SetUserTokensTableName("test");

    // Assert
    var serviceProvider = services.BuildServiceProvider();
    var options = serviceProvider.GetRequiredService<IOptionsMonitor<DynamoDbOptions>>().CurrentValue;
    Assert.Equal("test", options.UserTokensTableName);
  }

  [Fact]
  public void Should_ThrowException_When_SetUserLoginsTableNameAndNameIsNull()
  {
    // Arrange
    var services = new ServiceCollection();

    // Act & Assert
    var exception = Assert.Throws<ArgumentNullException>(() =>
        CreateBuilder(services).SetUserLoginsTableName(null!));
    Assert.Equal("name", exception.ParamName);
  }

  [Fact]
  public void Should_SetTableName_When_CallingSetUserLoginsTableName()
  {
    // Arrange
    var services = new ServiceCollection();

    // Act
    CreateBuilder(services).SetUserLoginsTableName("test");

    // Assert
    var serviceProvider = services.BuildServiceProvider();
    var options = serviceProvider.GetRequiredService<IOptionsMonitor<DynamoDbOptions>>().CurrentValue;
    Assert.Equal("test", options.UserLoginsTableName);
  }

  [Fact]
  public void Should_ThrowException_When_SetUserRolesTableNameAndNameIsNull()
  {
    // Arrange
    var services = new ServiceCollection();

    // Act & Assert
    var exception = Assert.Throws<ArgumentNullException>(() =>
        CreateBuilder(services).SetUserRolesTableName(null!));
    Assert.Equal("name", exception.ParamName);
  }

  [Fact]
  public void Should_SetTableName_When_CallingSetUserRolesTableName()
  {
    // Arrange
    var services = new ServiceCollection();

    // Act
    CreateBuilder(services).SetUserRolesTableName("test");

    // Assert
    var serviceProvider = services.BuildServiceProvider();
    var options = serviceProvider.GetRequiredService<IOptionsMonitor<DynamoDbOptions>>().CurrentValue;
    Assert.Equal("test", options.UserRolesTableName);
  }

  [Fact]
  public void Should_ThrowException_When_SetUsersTableNameAndNameIsNull()
  {
    // Arrange
    var services = new ServiceCollection();

    // Act & Assert
    var exception = Assert.Throws<ArgumentNullException>(() =>
        CreateBuilder(services).SetUsersTableName(null!));
    Assert.Equal("name", exception.ParamName);
  }

  [Fact]
  public void Should_SetTableName_When_CallingSetUsersTableName()
  {
    // Arrange
    var services = new ServiceCollection();

    // Act
    CreateBuilder(services).SetUsersTableName("test");

    // Assert
    var serviceProvider = services.BuildServiceProvider();
    var options = serviceProvider.GetRequiredService<IOptionsMonitor<DynamoDbOptions>>().CurrentValue;
    Assert.Equal("test", options.UsersTableName);
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
    using (var database = DynamoDbLocalServerUtils.CreateDatabase())
    {
      // Arrange
      var services = new ServiceCollection();

      // Act
      CreateBuilder(services).UseDatabase(database.Client);

      // Assert
      var serviceProvider = services.BuildServiceProvider();
      var options = serviceProvider.GetRequiredService<IOptionsMonitor<DynamoDbOptions>>().CurrentValue;
      Assert.Equal(database.Client, options.Database);
    }
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

  private static DynamoDbBuilder CreateBuilder(IServiceCollection services)
      => services.AddIdentityCore<DynamoDbUser>().AddRoles<DynamoDbRole>().AddDynamoDbStores();
}
