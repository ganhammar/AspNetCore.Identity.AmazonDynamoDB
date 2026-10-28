using System.Diagnostics.CodeAnalysis;
using AspNetCore.Identity.AmazonDynamoDB;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class IdentityBuilderExtensions
{
  private const string DynamicCodeMessage =
    "The store types are built from the builder's user and role types at runtime. Use AddDynamoDbStores<TUser>() or AddDynamoDbStores<TUser, TRole>() in an application published with trimming or Native AOT.";

  /// <summary>Adds the DynamoDB user store for an identity builder without roles.</summary>
  public static DynamoDbBuilder AddDynamoDbStores<TUser>(this IdentityBuilder builder)
    where TUser : DynamoDbUser, new()
  {
    ArgumentNullException.ThrowIfNull(builder);

    EnsureType(builder.UserType, typeof(TUser), "user");

    if (builder.RoleType is not null)
    {
      throw new InvalidOperationException(
        $"The identity builder has the role type {builder.RoleType.Name}, call AddDynamoDbStores<TUser, TRole>() instead");
    }

    builder.Services.TryAddSingleton<IUserStore<TUser>, DynamoDbUserStore<TUser>>();

    return new DynamoDbBuilder(builder.Services);
  }

  /// <summary>Adds the DynamoDB user and role stores for an identity builder with roles.</summary>
  public static DynamoDbBuilder AddDynamoDbStores<TUser, TRole>(this IdentityBuilder builder)
    where TUser : DynamoDbUser, new()
    where TRole : DynamoDbRole, new()
  {
    ArgumentNullException.ThrowIfNull(builder);

    EnsureType(builder.UserType, typeof(TUser), "user");
    EnsureType(builder.RoleType, typeof(TRole), "role");

    builder.Services.TryAddSingleton<IUserStore<TUser>, DynamoDbUserStore<TUser>>();
    builder.Services.TryAddSingleton<IRoleStore<TRole>, DynamoDbRoleStore<TRole>>();

    return new DynamoDbBuilder(builder.Services);
  }

  /// <summary>
  /// Adds the DynamoDB stores for the identity builder's user type and, when it has one, role type.
  /// The store types are made at runtime, so an application published with trimming or Native AOT
  /// calls <see cref="AddDynamoDbStores{TUser}"/> or <see cref="AddDynamoDbStores{TUser, TRole}"/>.
  /// </summary>
  [RequiresDynamicCode(DynamicCodeMessage)]
  [RequiresUnreferencedCode(DynamicCodeMessage)]
  public static DynamoDbBuilder AddDynamoDbStores(this IdentityBuilder builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    AddStores(builder.Services, builder.UserType, builder.RoleType);

    return new DynamoDbBuilder(builder.Services);
  }

  [RequiresDynamicCode(DynamicCodeMessage)]
  [RequiresUnreferencedCode(DynamicCodeMessage)]
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

  private static void EnsureType(Type? builderType, Type storeType, string kind)
  {
    if (builderType != storeType)
    {
      throw new InvalidOperationException(
        $"The identity builder has the {kind} type {builderType?.Name ?? "<none>"}, not {storeType.Name}");
    }
  }
}
