using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AspNetCore.Identity.AmazonDynamoDB.Tests;

public class DynamoDbExtensionsTests
{
  [Fact]
  public void Should_ThrowException_When_CallingUseDynamoDbAndBuilderIsNull()
  {
    // Arrange, Act & Assert
    var exception = Assert.Throws<ArgumentNullException>(() =>
        Microsoft.Extensions.DependencyInjection.IdentityBuilderExtensions.AddDynamoDbStores(null!));

    Assert.Equal("builder", exception.ParamName);
  }

  [Fact]
  public void Should_ThrowException_When_CallingGenericAddDynamoDbStoresAndBuilderIsNull()
  {
    // Arrange, Act & Assert
    var exception = Assert.Throws<ArgumentNullException>(() =>
        Microsoft.Extensions.DependencyInjection.IdentityBuilderExtensions.AddDynamoDbStores<DynamoDbUser>(null!));

    Assert.Equal("builder", exception.ParamName);
  }

  [Fact]
  public void Should_RegisterUserStore_When_AddingStoresForUserType()
  {
    // Arrange
    var services = new ServiceCollection();

    // Act
    services.AddIdentityCore<CustomUser>().AddDynamoDbStores<CustomUser>();

    // Assert
    var store = services.Single(x => x.ServiceType == typeof(IUserStore<CustomUser>));
    Assert.Equal(typeof(DynamoDbUserStore<CustomUser>), store.ImplementationType);
    Assert.DoesNotContain(services, x => x.ServiceType == typeof(IRoleStore<DynamoDbRole>));
  }

  [Fact]
  public void Should_RegisterUserAndRoleStores_When_AddingStoresForUserAndRoleTypes()
  {
    // Arrange
    var services = new ServiceCollection();

    // Act
    services.AddIdentityCore<DynamoDbUser>().AddRoles<DynamoDbRole>().AddDynamoDbStores<DynamoDbUser, DynamoDbRole>();

    // Assert
    var userStore = services.Single(x => x.ServiceType == typeof(IUserStore<DynamoDbUser>));
    var roleStore = services.Single(x => x.ServiceType == typeof(IRoleStore<DynamoDbRole>));
    Assert.Equal(typeof(DynamoDbUserStore<DynamoDbUser>), userStore.ImplementationType);
    Assert.Equal(typeof(DynamoDbRoleStore<DynamoDbRole>), roleStore.ImplementationType);
  }

  [Fact]
  public void Should_ThrowException_When_UserTypeDiffersFromTheBuilder()
  {
    // Arrange
    var builder = new ServiceCollection().AddIdentityCore<CustomUser>();

    // Act & Assert
    var exception = Assert.Throws<InvalidOperationException>(() => builder.AddDynamoDbStores<DynamoDbUser>());
    Assert.Contains("CustomUser", exception.Message);
  }

  [Fact]
  public void Should_ThrowException_When_RoleTypeDiffersFromTheBuilder()
  {
    // Arrange
    var builder = new ServiceCollection().AddIdentityCore<DynamoDbUser>().AddRoles<IdentityRole>();

    // Act & Assert
    var exception = Assert.Throws<InvalidOperationException>(() => builder.AddDynamoDbStores<DynamoDbUser, DynamoDbRole>());
    Assert.Contains("IdentityRole", exception.Message);
  }

  [Fact]
  public void Should_ThrowException_When_BuilderHasRolesButOnlyTheUserTypeIsGiven()
  {
    // Arrange
    var builder = new ServiceCollection().AddIdentityCore<DynamoDbUser>().AddRoles<DynamoDbRole>();

    // Act & Assert
    var exception = Assert.Throws<InvalidOperationException>(() => builder.AddDynamoDbStores<DynamoDbUser>());
    Assert.Contains("AddDynamoDbStores<TUser, TRole>", exception.Message);
  }
}
