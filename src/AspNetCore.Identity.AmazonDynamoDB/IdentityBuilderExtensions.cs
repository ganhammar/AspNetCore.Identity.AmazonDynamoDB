using AspNetCore.Identity.AmazonDynamoDB;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class IdentityBuilderExtensions
{
  public static DynamoDbBuilder AddDynamoDbStores(this IdentityBuilder builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    AddStores(builder.Services, builder.UserType, builder.RoleType);

    return new DynamoDbBuilder(builder.Services);
  }

  private static void AddStores(IServiceCollection services, Type userType, Type? roleType)
  {
    var userStoreType = typeof(DynamoDbUserStore<>).MakeGenericType(userType);
    services.TryAddSingleton(typeof(IUserStore<>).MakeGenericType(userType), userStoreType);

    if (roleType != default)
    {
      var roleStoreType = typeof(DynamoDbRoleStore<>).MakeGenericType(roleType);
      services.TryAddSingleton(typeof(IRoleStore<>).MakeGenericType(roleType), roleStoreType);
    }
  }
}
