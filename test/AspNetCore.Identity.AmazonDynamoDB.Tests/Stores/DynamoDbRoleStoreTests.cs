using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Xunit;

namespace AspNetCore.Identity.AmazonDynamoDB.Tests;

[Collection("Sequential")]
public class DynamoDbRoleStoreTests
{
    [Fact]
    public void Should_ThrowArgumentNullException_When_OptionsIsNotSet()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new DynamoDbRoleStore<DynamoDbRole>(null!));

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
                new DynamoDbRoleStore<DynamoDbRole>(TestUtils.GetOptions(new())));

            Assert.Equal("_options.Database", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToCreateRoleThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await roleStore.CreateAsync(default!, CancellationToken.None));
            Assert.Equal("role", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_CreateRole_When_RoleIsValid()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var context = new DynamoDBContext(database.Client);
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);
            var role = new DynamoDbRole
            {
                Name = Guid.NewGuid().ToString(),
            };

            // Act
            await roleStore.CreateAsync(role, CancellationToken.None);

            // Assert
            var databaseRole = await context.LoadAsync<DynamoDbRole>(role.Id);
            Assert.NotNull(databaseRole);
            Assert.Equal(role.Name, databaseRole.Name);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToDeleteRoleThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await roleStore.DeleteAsync(default!, CancellationToken.None));
            Assert.Equal("role", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_DeleteRole_When_ParametersIsCorrect()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var context = new DynamoDBContext(database.Client);
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);
            var role = new DynamoDbRole
            {
                Name = "test",
            };
            await context.SaveAsync(role);

            // Act
            await roleStore.DeleteAsync(role, CancellationToken.None);

            // Assert
            var response = await database.Client.DescribeTableAsync(new DescribeTableRequest
            {
                TableName = Constants.DefaultRolesTableName,
            });
            Assert.Equal(0, response.Table.ItemCount);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToAddClaimToRoleThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await roleStore.AddClaimAsync(default!, new("t", "t"), CancellationToken.None));
            Assert.Equal("role", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToAddClaimToRoleAndTheClaimIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await roleStore.AddClaimAsync(new(), default!, CancellationToken.None));
            Assert.Equal("claim", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_AddClaim_When_RequestIsValid()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var context = new DynamoDBContext(database.Client);
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);
            var role = new DynamoDbRole();
            await context.SaveAsync(role);

            var claimType = "test";
            var claimValue = "test";

            // Act
            await roleStore.AddClaimAsync(
                role, new(claimType, claimValue), CancellationToken.None);

            // Assert
            Assert.Contains(role.Claims, x => x.Key == claimType && x.Value == claimValue);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToFindRoleByIdThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await roleStore.FindByIdAsync(default!, CancellationToken.None));
            Assert.Equal("roleId", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ReturnDefault_When_FindingByIdAndRoleDoesntExist()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act
            var role = await roleStore.FindByIdAsync(Guid.NewGuid().ToString(), CancellationToken.None);

            // Assert
            Assert.Null(role);
        }
    }

    [Fact]
    public async Task Should_ReturnRole_When_FindingById()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var context = new DynamoDBContext(database.Client);
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);
            var role = new DynamoDbRole
            {
                Name = "test@test.se",
            };
            await context.SaveAsync(role);

            // Act
            var foundRole = await roleStore.FindByIdAsync(role.Id, CancellationToken.None);

            // Assert
            Assert.Equal(role.Name, foundRole.Name);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToFindByNameThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await roleStore.FindByNameAsync(default!, CancellationToken.None));
            Assert.Equal("normalizedRoleName", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ReturnDefault_When_FindingByNameAndRoleDoesntExist()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act
            var role = await roleStore.FindByNameAsync("doesnt@exi.st", CancellationToken.None);

            // Assert
            Assert.Null(role);
        }
    }

    [Fact]
    public async Task Should_ReturnRole_When_FindingByName()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var context = new DynamoDBContext(database.Client);
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);
            var role = new DynamoDbRole
            {
                Name = "test",
                NormalizedName = "TEST",
            };
            await context.SaveAsync(role);

            // Act
            var foundRole = await roleStore.FindByNameAsync(role.NormalizedName, CancellationToken.None);

            // Assert
            Assert.Equal(role.Id, foundRole.Id);
        }
    }
}