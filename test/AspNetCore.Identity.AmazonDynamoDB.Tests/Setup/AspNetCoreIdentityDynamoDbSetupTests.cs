using Amazon.DynamoDBv2;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AspNetCore.Identity.AmazonDynamoDB.Tests;

[Collection("Sequential")]
public class AspNetCoreIdentityDynamoDbSetupTests
{
    [Fact]
    public async Task Should_SetupTables_When_CalledSynchronously()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new()
            {
                Database = database.Client,
            });

            // Act
            AspNetCoreIdentityDynamoDbSetup.EnsureInitialized(options);

            // Assert
            var tableNames = await database.Client.ListTablesAsync();
            Assert.Contains(Constants.DefaultUsersTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultUserClaimsTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultUserLoginsTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultUserRolesTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultRolesTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultUserTokensTableName, tableNames.TableNames);
        }
    }

    [Fact]
    public async Task Should_SetupTables_When_CalledSynchronouslyWithServiceProvider()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var services = new ServiceCollection();
            CreateBuilder(services).UseDatabase(database.Client);

            // Act
            AspNetCoreIdentityDynamoDbSetup.EnsureInitialized(services.BuildServiceProvider());

            // Assert
            var tableNames = await database.Client.ListTablesAsync();
            Assert.Contains(Constants.DefaultUsersTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultUserClaimsTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultUserLoginsTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultUserRolesTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultRolesTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultUserTokensTableName, tableNames.TableNames);
        }
    }

    [Fact]
    public async Task Should_SetupTables_When_CalledAsynchronously()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new()
            {
                Database = database.Client,
            });

            // Act
            await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

            // Assert
            var tableNames = await database.Client.ListTablesAsync();
            Assert.Contains(Constants.DefaultUsersTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultUserClaimsTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultUserLoginsTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultUserRolesTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultRolesTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultUserTokensTableName, tableNames.TableNames);
        }
    }

    [Fact]
    public async Task Should_SetupTables_When_CalledAsynchronouslyWithServiceProvider()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var services = new ServiceCollection();
            CreateBuilder(services).UseDatabase(database.Client);

            // Act
            await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(services.BuildServiceProvider());

            // Assert
            var tableNames = await database.Client.ListTablesAsync();
            Assert.Contains(Constants.DefaultUsersTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultUserClaimsTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultUserLoginsTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultUserRolesTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultRolesTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultUserTokensTableName, tableNames.TableNames);
        }
    }

    [Fact]
    public async Task Should_SetupTables_When_CalledAsynchronouslyWithDatbaseInServiceProvider()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<IAmazonDynamoDB>(database.Client);
            CreateBuilder(services);

            // Act
            await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(services.BuildServiceProvider());

            // Assert
            var tableNames = await database.Client.ListTablesAsync();
            Assert.Contains(Constants.DefaultUsersTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultUserClaimsTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultUserLoginsTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultUserRolesTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultRolesTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultUserTokensTableName, tableNames.TableNames);
        }
    }

    [Fact]
    public async Task Should_SetupTables_When_CalledSynchronouslyWithDatbaseInServiceProvider()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<IAmazonDynamoDB>(database.Client);
            CreateBuilder(services);

            // Act
            AspNetCoreIdentityDynamoDbSetup.EnsureInitialized(services.BuildServiceProvider());

            // Assert
            var tableNames = await database.Client.ListTablesAsync();
            Assert.Contains(Constants.DefaultUsersTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultUserClaimsTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultUserLoginsTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultUserRolesTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultRolesTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultUserTokensTableName, tableNames.TableNames);
        }
    }

    [Fact]
    public async Task Should_SetupTablesWithDifferentNames_When_OtherIsSpecified()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var rolesTableName = "roller";
            var usersTableName = "anvandare";
            var userClaimsTableName = "anvandare_ansprak";
            var userLoginsTableName = "anvandare_inloggningar";
            var userRolesTableName = "anvandare_roller";
            var userTokensTableName = "anvandare_bevis";
            var options = TestUtils.GetOptions(new()
            {
                Database = database.Client,
                RolesTableName = rolesTableName,
                UsersTableName = usersTableName,
                UserClaimsTableName = userClaimsTableName,
                UserLoginsTableName = userLoginsTableName,
                UserRolesTableName = userRolesTableName,
                UserTokensTableName = userTokensTableName,
            });

            // Act
            await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

            // Assert
            var tableNames = await database.Client.ListTablesAsync();
            Assert.Contains(usersTableName, tableNames.TableNames);
            Assert.Contains(userClaimsTableName, tableNames.TableNames);
            Assert.Contains(userLoginsTableName, tableNames.TableNames);
            Assert.Contains(userRolesTableName, tableNames.TableNames);
            Assert.Contains(rolesTableName, tableNames.TableNames);
            Assert.Contains(userTokensTableName, tableNames.TableNames);
        }
    }

    private static DynamoDbBuilder CreateBuilder(IServiceCollection services)
        => services.AddIdentityCore<DynamoDbUser>().AddRoles<DynamoDbRole>().AddDynamoDbStores();
}