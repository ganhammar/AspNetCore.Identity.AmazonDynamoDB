using System.Security.Claims;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Xunit;

namespace AspNetCore.Identity.AmazonDynamoDB.Tests;

[Collection("Sequential")]
public class DynamoDbUserStoreTests
{
    [Fact]
    public void Should_ThrowArgumentNullException_When_OptionsIsNotSet()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new DynamoDbUserStore<DynamoDbUser>(null!));

            Assert.Equal("optionsMonitor", exception.ParamName);
        }
    }

    [Fact]
    public void Should_ThrowArgumentNullException_When_DatabaseIsNotSet()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new DynamoDbUserStore<DynamoDbUser>(TestUtils.GetOptions(new())));

            Assert.Equal("_options.Database", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToCreateUserThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.CreateAsync(default!, CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_CreateUser_When_UserIsValid()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var context = new DynamoDBContext(database.Client);
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);
            var user = new DynamoDbUser
            {
                UserName = Guid.NewGuid().ToString(),
            };

            // Act
            await userStore.CreateAsync(user, CancellationToken.None);

            // Assert
            var databaseUser = await context.LoadAsync<DynamoDbUser>(user.Id);
            Assert.NotNull(databaseUser);
            Assert.Equal(user.UserName, databaseUser.UserName);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToAddClaimsToAUserThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.AddClaimsAsync(default!, new List<Claim>(), CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_DoNothing_When_ClaimsIsEmpty()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act
            await userStore.AddClaimsAsync(new(), new List<Claim>(), CancellationToken.None);

            // Assert
            var response = await database.Client.DescribeTableAsync(new DescribeTableRequest
            {
                TableName = Constants.DefaultUserClaimsTableName,
            });
            Assert.Equal(0, response.Table.ItemCount);
        }
    }

    [Fact]
    public async Task Should_AddClaims_When_ClaimsIsNotEmpty()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act
            await userStore.AddClaimsAsync(new(), new List<Claim>
            {
                new Claim(ClaimTypes.Country, "se"),
                new Claim(ClaimTypes.Email, "test@test.se"),
            }, CancellationToken.None);

            // Assert
            var response = await database.Client.DescribeTableAsync(new DescribeTableRequest
            {
                TableName = Constants.DefaultUserClaimsTableName,
            });
            Assert.Equal(2, response.Table.ItemCount);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToAddLoginToAUserThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.AddLoginAsync(default!, new("test", "test", "test"), CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToAddLoginToAUserAndTheLoginIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.AddLoginAsync(new(), default!, CancellationToken.None));
            Assert.Equal("login", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_AddLogin_When_ParametersIsCorrect()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act
            await userStore.AddLoginAsync(
                new(), new("test", "test", "test"), CancellationToken.None);

            // Assert
            var response = await database.Client.DescribeTableAsync(new DescribeTableRequest
            {
                TableName = Constants.DefaultUserLoginsTableName,
            });
            Assert.Equal(1, response.Table.ItemCount);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToAddRoleToAUserThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.AddToRoleAsync(default!, "test", CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToAddRoleToAUserAndTheRoleIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.AddToRoleAsync(new(), default!, CancellationToken.None));
            Assert.Equal("roleName", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_AddRole_When_ParametersIsCorrect()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act
            await userStore.AddToRoleAsync(
                new(), "test", CancellationToken.None);

            // Assert
            var response = await database.Client.DescribeTableAsync(new DescribeTableRequest
            {
                TableName = Constants.DefaultUserRolesTableName,
            });
            Assert.Equal(1, response.Table.ItemCount);
        }
    }
}