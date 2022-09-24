using Xunit;

namespace AspNetCore.Identity.AmazonDynamoDB.Tests;

[Collection("Sequential")]
public class DynamoDbSetupTests
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
            DynamoDbSetup.EnsureInitialized(options);

            // Assert
            var tableNames = await database.Client.ListTablesAsync();
            Assert.Contains(Constants.DefaultUsersTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultUserClaimsTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultUserLoginsTableName, tableNames.TableNames);
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
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Assert
            var tableNames = await database.Client.ListTablesAsync();
            Assert.Contains(Constants.DefaultUsersTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultUserClaimsTableName, tableNames.TableNames);
            Assert.Contains(Constants.DefaultUserLoginsTableName, tableNames.TableNames);
        }
    }
}