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

    [Fact]
    public async Task Should_ThrowException_When_TryingToDeleteUserThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.DeleteAsync(default!, CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_DeleteUser_When_ParametersIsCorrect()
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
                Email = "test@test.se",
            };
            await context.SaveAsync(user);

            // Act
            await userStore.DeleteAsync(user, CancellationToken.None);

            // Assert
            var response = await database.Client.DescribeTableAsync(new DescribeTableRequest
            {
                TableName = Constants.DefaultUsersTableName,
            });
            Assert.Equal(0, response.Table.ItemCount);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToFindByEmailThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.FindByEmailAsync(default!, CancellationToken.None));
            Assert.Equal("normalizedEmail", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ReturnDefault_When_FindingByEmailAndUserDoesntExist()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act
            var user = await userStore.FindByEmailAsync("doesnt@exi.st", CancellationToken.None);

            // Assert
            Assert.Null(user);
        }
    }

    [Fact]
    public async Task Should_ReturnUser_When_FindingByEmail()
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
                Email = "test@test.se",
                NormalizedEmail = "TEST@TEST.SE",
            };
            await context.SaveAsync(user);

            // Act
            var foundUser = await userStore.FindByEmailAsync(user.NormalizedEmail, CancellationToken.None);

            // Assert
            Assert.Equal(user.Id, foundUser.Id);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToFindByIdThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.FindByIdAsync(default!, CancellationToken.None));
            Assert.Equal("userId", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ReturnDefault_When_FindingByIdAndUserDoesntExist()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act
            var user = await userStore.FindByIdAsync(Guid.NewGuid().ToString(), CancellationToken.None);

            // Assert
            Assert.Null(user);
        }
    }

    [Fact]
    public async Task Should_ReturnUser_When_FindingById()
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
                Email = "test@test.se",
            };
            await context.SaveAsync(user);

            // Act
            var foundUser = await userStore.FindByIdAsync(user.Id, CancellationToken.None);

            // Assert
            Assert.Equal(user.Email, foundUser.Email);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToFindByLoginAndLoginProviderIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.FindByLoginAsync(default!, "test", CancellationToken.None));
            Assert.Equal("loginProvider", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToFindByLoginAndProviderKeyIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.FindByLoginAsync("test", default!, CancellationToken.None));
            Assert.Equal("providerKey", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ReturnDefault_When_LoginDoesntExist()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act
            var user = await userStore.FindByLoginAsync(
                "test", Guid.NewGuid().ToString(), CancellationToken.None);

            // Assert
            Assert.Null(user);
        }
    }

    [Fact]
    public async Task Should_ReturnUser_When_FindingByLogin()
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
                Email = "test@test.se",
            };
            await context.SaveAsync(user);
            var login = new DynamoDbUserLogin
            {
                LoginProvider = "test",
                ProviderKey = Guid.NewGuid().ToString(),
                UserId = user.Id,
            };
            await context.SaveAsync(login);

            // Act
            var foundUser = await userStore.FindByLoginAsync(
                login.LoginProvider, login.ProviderKey, CancellationToken.None);

            // Assert
            Assert.Equal(user.Id, foundUser.Id);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToFindByNameThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.FindByNameAsync(default!, CancellationToken.None));
            Assert.Equal("normalizedUserName", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ReturnDefault_When_FindingByNameAndUserDoesntExist()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act
            var user = await userStore.FindByNameAsync("doesnt@exi.st", CancellationToken.None);

            // Assert
            Assert.Null(user);
        }
    }

    [Fact]
    public async Task Should_ReturnUser_When_FindingByName()
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
                UserName = "test",
                NormalizedUserName = "TEST",
            };
            await context.SaveAsync(user);

            // Act
            var foundUser = await userStore.FindByNameAsync(user.NormalizedUserName, CancellationToken.None);

            // Assert
            Assert.Equal(user.Id, foundUser.Id);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToGetAccessFailedCountOnUserThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.GetAccessFailedCountAsync(default!, CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ReturnAccessFailedCount_When_UserIsValid()
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
                AccessFailedCount = 10,
            };
            await context.SaveAsync(user);

            // Act
            var value = await userStore.GetAccessFailedCountAsync(user, CancellationToken.None);

            // Assert
            Assert.Equal(user.AccessFailedCount, value);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToGetEmailOnUserThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.GetEmailAsync(default!, CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ReturnEmail_When_UserIsValid()
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
                Email = "test@test.se",
            };
            await context.SaveAsync(user);

            // Act
            var value = await userStore.GetEmailAsync(user, CancellationToken.None);

            // Assert
            Assert.Equal(user.Email, value);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToGetEmailConfirmedOnUserThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.GetEmailConfirmedAsync(default!, CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ReturnEmailConfirmed_When_UserIsValid()
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
                EmailConfirmed = true,
            };
            await context.SaveAsync(user);

            // Act
            var value = await userStore.GetEmailConfirmedAsync(user, CancellationToken.None);

            // Assert
            Assert.Equal(user.EmailConfirmed, value);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToGetLockoutEnabledOnUserThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.GetLockoutEnabledAsync(default!, CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ReturnLockoutEnabled_When_UserIsValid()
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
                LockoutEnabled = true,
            };
            await context.SaveAsync(user);

            // Act
            var value = await userStore.GetLockoutEnabledAsync(user, CancellationToken.None);

            // Assert
            Assert.Equal(user.LockoutEnabled, value);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToGetLockoutEndOnUserThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.GetLockoutEndDateAsync(default!, CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ReturnLockoutEnd_When_UserIsValid()
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
                LockoutEnd = DateTime.UtcNow,
            };
            await context.SaveAsync(user);

            // Act
            var value = await userStore.GetLockoutEndDateAsync(user, CancellationToken.None);

            // Assert
            Assert.Equal(user.LockoutEnd, value);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToGetNormalizedEmailOnUserThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.GetNormalizedEmailAsync(default!, CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ReturnNormalizedEmail_When_UserIsValid()
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
                NormalizedEmail = "TEST@TEST.SE",
            };
            await context.SaveAsync(user);

            // Act
            var value = await userStore.GetNormalizedEmailAsync(user, CancellationToken.None);

            // Assert
            Assert.Equal(user.NormalizedEmail, value);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToGetNormalizedUserNameOnUserThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.GetNormalizedUserNameAsync(default!, CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ReturnNormalizedUserName_When_UserIsValid()
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
                NormalizedUserName = "TEST",
            };
            await context.SaveAsync(user);

            // Act
            var value = await userStore.GetNormalizedUserNameAsync(user, CancellationToken.None);

            // Assert
            Assert.Equal(user.NormalizedUserName, value);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToGetLoginsAndUserIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.GetLoginsAsync(default!, CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ReturnLogins_When_ListingThem()
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
                Email = "test@test.se",
            };
            await context.SaveAsync(user);

            var loginCount = 10;
            for (var index = 0; index < loginCount; index++)
            {
                var login = new DynamoDbUserLogin
                {
                    LoginProvider = $"test-{index}",
                    ProviderKey = Guid.NewGuid().ToString(),
                    UserId = user.Id,
                };
                await context.SaveAsync(login);
            }

            // Act
            var logins = await userStore.GetLoginsAsync(user, CancellationToken.None);

            // Assert
            var response = await database.Client.DescribeTableAsync(new DescribeTableRequest
            {
                TableName = Constants.DefaultUserLoginsTableName,
            });
            Assert.Equal(loginCount, response.Table.ItemCount);
            Assert.Equal(loginCount, logins.Count);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToGetRolesAndUserIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.GetRolesAsync(default!, CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ReturnRoles_When_ListingThem()
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
                Email = "test@test.se",
            };
            await context.SaveAsync(user);

            var loginCount = 10;
            for (var index = 0; index < loginCount; index++)
            {
                var login = new DynamoDbUserRole
                {
                    RoleName = $"test-{index}",
                    UserId = user.Id,
                };
                await context.SaveAsync(login);
            }

            // Act
            var logins = await userStore.GetRolesAsync(user, CancellationToken.None);

            // Assert
            var response = await database.Client.DescribeTableAsync(new DescribeTableRequest
            {
                TableName = Constants.DefaultUserRolesTableName,
            });
            Assert.Equal(loginCount, response.Table.ItemCount);
            Assert.Equal(loginCount, logins.Count);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToGetPasswordHashOnUserThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.GetPasswordHashAsync(default!, CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ReturnPasswordHash_When_UserIsValid()
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
                PasswordHash = "TEST",
            };
            await context.SaveAsync(user);

            // Act
            var value = await userStore.GetPasswordHashAsync(user, CancellationToken.None);

            // Assert
            Assert.Equal(user.PasswordHash, value);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToGetPhoneNumberOnUserThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.GetPhoneNumberAsync(default!, CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ReturnPhoneNumber_When_UserIsValid()
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
                PhoneNumber = "TEST",
            };
            await context.SaveAsync(user);

            // Act
            var value = await userStore.GetPhoneNumberAsync(user, CancellationToken.None);

            // Assert
            Assert.Equal(user.PhoneNumber, value);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToGetPhoneNumberConfirmedOnUserThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.GetPhoneNumberConfirmedAsync(default!, CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ReturnPhoneNumberConfirmed_When_UserIsValid()
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
                PhoneNumberConfirmed = true,
            };
            await context.SaveAsync(user);

            // Act
            var value = await userStore.GetPhoneNumberConfirmedAsync(user, CancellationToken.None);

            // Assert
            Assert.Equal(user.PhoneNumberConfirmed, value);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToGetSecurityStampOnUserThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.GetSecurityStampAsync(default!, CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ReturnSecurityStamp_When_UserIsValid()
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
                SecurityStamp = "TEST",
            };
            await context.SaveAsync(user);

            // Act
            var value = await userStore.GetSecurityStampAsync(user, CancellationToken.None);

            // Assert
            Assert.Equal(user.SecurityStamp, value);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToGetTwoFactorEnabledOnUserThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.GetTwoFactorEnabledAsync(default!, CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ReturnTwoFactorEnabled_When_UserIsValid()
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
                TwoFactorEnabled = true,
            };
            await context.SaveAsync(user);

            // Act
            var value = await userStore.GetTwoFactorEnabledAsync(user, CancellationToken.None);

            // Assert
            Assert.Equal(user.TwoFactorEnabled, value);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToGetUserIdOnUserThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.GetUserIdAsync(default!, CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ReturnUserId_When_UserIsValid()
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
                Id = "TEST",
            };
            await context.SaveAsync(user);

            // Act
            var value = await userStore.GetUserIdAsync(user, CancellationToken.None);

            // Assert
            Assert.Equal(user.Id, value);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToGetUserNameOnUserThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.GetUserNameAsync(default!, CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ReturnUserName_When_UserIsValid()
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
                UserName = "TEST",
            };
            await context.SaveAsync(user);

            // Act
            var value = await userStore.GetUserNameAsync(user, CancellationToken.None);

            // Assert
            Assert.Equal(user.UserName, value);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToGetHasPasswordOnUserThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.HasPasswordAsync(default!, CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ReturnTrue_When_UserHasPassword()
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
                PasswordHash = "TEST",
            };
            await context.SaveAsync(user);

            // Act
            var value = await userStore.HasPasswordAsync(user, CancellationToken.None);

            // Assert
            Assert.True(value);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToIncrementAccessFailedCountOnUserThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.IncrementAccessFailedCountAsync(default!, CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_IncrementAccessFailedCount_When_Requested()
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
                AccessFailedCount = 5,
            };
            await context.SaveAsync(user);

            // Act
            var value = await userStore.IncrementAccessFailedCountAsync(user, CancellationToken.None);

            // Assert
            Assert.Equal(6, value);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToCheckIfUserIsInRoleAndUserIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.IsInRoleAsync(default!, "test", CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Theory]
    [InlineData("test", true)]
    [InlineData("testing", false)]
    public async Task Should_ReturnExpected_When_CheckingIfUserIsInRole(string roleName, bool expectedResult)
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var context = new DynamoDBContext(database.Client);
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);
            var user = new DynamoDbUser();
            await context.SaveAsync(user);
            var userRoles = new DynamoDbUserRole
            {
                RoleName = "test",
                UserId = user.Id,
            };
            await context.SaveAsync(userRoles);

            // Act
            var value = await userStore.IsInRoleAsync(user, roleName, CancellationToken.None);

            // Assert
            Assert.Equal(expectedResult, value);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToSetEmailAndUserIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.SetEmailAsync(default!, "test@test.se", CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_SetEmail_When_UserIsValid()
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
                Email = "test@test.se",
            };
            await context.SaveAsync(user);

            // Act
            var email = "testing@test.se";
            await userStore.SetEmailAsync(user, email, CancellationToken.None);

            // Assert
            Assert.Equal(email, user.Email);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToSetEmailConfirmedAndUserIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.SetEmailConfirmedAsync(default!, true, CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_SetEmailConfirmed_When_UserIsValid()
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
                EmailConfirmed = true,
            };
            await context.SaveAsync(user);

            // Act
            var emailConfirmed = true;
            await userStore.SetEmailConfirmedAsync(
                user, emailConfirmed, CancellationToken.None);

            // Assert
            Assert.Equal(emailConfirmed, user.EmailConfirmed);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToSetLockoutEnabledAndUserIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.SetLockoutEnabledAsync(default!, true, CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_SetLockoutEnabled_When_UserIsValid()
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
                LockoutEnabled = true,
            };
            await context.SaveAsync(user);

            // Act
            var emailConfirmed = true;
            await userStore.SetLockoutEnabledAsync(
                user, emailConfirmed, CancellationToken.None);

            // Assert
            Assert.Equal(emailConfirmed, user.LockoutEnabled);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToSetLockoutEndDateAndUserIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.SetLockoutEndDateAsync(default!, DateTimeOffset.Now, CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_SetLockoutEndDate_When_UserIsValid()
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
                LockoutEnd = default,
            };
            await context.SaveAsync(user);

            // Act
            var lockoutEnd = DateTimeOffset.Now;
            await userStore.SetLockoutEndDateAsync(
                user, lockoutEnd, CancellationToken.None);

            // Assert
            Assert.Equal(lockoutEnd.UtcDateTime, user.LockoutEnd);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToSetNormalizedEmailAndUserIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.SetNormalizedEmailAsync(default!, "TEST@TEST.SE", CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_SetNormalizedEmail_When_UserIsValid()
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
                NormalizedEmail = "TEST@TEST.SE",
            };
            await context.SaveAsync(user);

            // Act
            var normalizedEmail = "TESTING@TEST.SE";
            await userStore.SetNormalizedEmailAsync(
                user, normalizedEmail, CancellationToken.None);

            // Assert
            Assert.Equal(normalizedEmail, user.NormalizedEmail);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToSetNormalizedUserNameAndUserIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.SetNormalizedUserNameAsync(default!, "TEST", CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_SetNormalizedUserName_When_UserIsValid()
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
                NormalizedUserName = "TEST",
            };
            await context.SaveAsync(user);

            // Act
            var normalizedUserName = "TESTING";
            await userStore.SetNormalizedUserNameAsync(
                user, normalizedUserName, CancellationToken.None);

            // Assert
            Assert.Equal(normalizedUserName, user.NormalizedUserName);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToSetPasswordHashAndUserIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.SetPasswordHashAsync(default!, "Secret", CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_SetPasswordHash_When_UserIsValid()
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
                PasswordHash = "Secret",
            };
            await context.SaveAsync(user);

            // Act
            var passwordHash = "Even-More-Secret";
            await userStore.SetPasswordHashAsync(
                user, passwordHash, CancellationToken.None);

            // Assert
            Assert.Equal(passwordHash, user.PasswordHash);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToSetPhoneNumberAndUserIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.SetPhoneNumberAsync(default!, "1111111", CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_SetPhoneNumber_When_UserIsValid()
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
                PhoneNumber = "1111111",
            };
            await context.SaveAsync(user);

            // Act
            var phoneNumber = "2222222";
            await userStore.SetPhoneNumberAsync(
                user, phoneNumber, CancellationToken.None);

            // Assert
            Assert.Equal(phoneNumber, user.PhoneNumber);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToSetPhoneNumberConfirmedAndUserIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.SetPhoneNumberConfirmedAsync(default!, true, CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_SetPhoneNumberConfirmed_When_UserIsValid()
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
                PhoneNumberConfirmed = false,
            };
            await context.SaveAsync(user);

            // Act
            var phoneNumberConfirmed = true;
            await userStore.SetPhoneNumberConfirmedAsync(
                user, phoneNumberConfirmed, CancellationToken.None);

            // Assert
            Assert.Equal(phoneNumberConfirmed, user.PhoneNumberConfirmed);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToSetTwoFactorEnabledAndUserIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.SetTwoFactorEnabledAsync(default!, true, CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_SetTwoFactorEnabled_When_UserIsValid()
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
                TwoFactorEnabled = false,
            };
            await context.SaveAsync(user);

            // Act
            var phoneNumberConfirmed = true;
            await userStore.SetTwoFactorEnabledAsync(
                user, phoneNumberConfirmed, CancellationToken.None);

            // Assert
            Assert.Equal(phoneNumberConfirmed, user.TwoFactorEnabled);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToSetSecurityStampAndUserIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.SetSecurityStampAsync(default!, "some-string", CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_SetSecurityStamp_When_UserIsValid()
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
                SecurityStamp = "some-string",
            };
            await context.SaveAsync(user);

            // Act
            var securityStamp = "some-other-string";
            await userStore.SetSecurityStampAsync(
                user, securityStamp, CancellationToken.None);

            // Assert
            Assert.Equal(securityStamp, user.SecurityStamp);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToSetUserNameAndUserIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.SetUserNameAsync(default!, "some-user", CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_SetUserName_When_UserIsValid()
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
                UserName = "some-user",
            };
            await context.SaveAsync(user);

            // Act
            var userName = "some-other-user";
            await userStore.SetUserNameAsync(
                user, userName, CancellationToken.None);

            // Assert
            Assert.Equal(userName, user.UserName);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToGetClaimsAndUserIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.GetClaimsAsync(default!, CancellationToken.None));
            Assert.Equal("user", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ReturnClaims_When_ListingThem()
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
                Email = "test@test.se",
            };
            await context.SaveAsync(user);

            var claimsCount = 10;
            for (var index = 0; index < claimsCount; index++)
            {
                var login = new DynamoDbUserClaim
                {
                    ClaimType = $"Test{index}",
                    ClaimValue = $"Test{index}",
                    UserId = user.Id,
                };
                await context.SaveAsync(login);
            }

            // Act
            var claims = await userStore.GetClaimsAsync(user, CancellationToken.None);

            // Assert
            Assert.Equal(claimsCount, claims.Count);
        }
    }

    [Fact]
    public async Task Should_ThrowException_When_TryingToGetUsersInRoleThatIsNull()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await userStore.GetUsersInRoleAsync(default!, CancellationToken.None));
            Assert.Equal("roleName", exception.ParamName);
        }
    }

    [Fact]
    public async Task Should_ReturnUsers_When_ListingThemByRoleName()
    {
        using (var database = DynamoDbLocalServerUtils.CreateDatabase())
        {
            // Arrange
            var context = new DynamoDBContext(database.Client);
            var options = TestUtils.GetOptions(new() { Database = database.Client });
            var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
            await DynamoDbSetup.EnsureInitializedAsync(options);

            var roleName = "test-role";
            var userCount = 10;
            for (var index = 0; index < userCount; index++)
            {
                var user = new DynamoDbUser
                {
                    Email = "test@test.se",
                };
                await context.SaveAsync(user);
                var login = new DynamoDbUserRole
                {
                    RoleName = roleName,
                    UserId = user.Id,
                };
                await context.SaveAsync(login);
            }

            // Act
            var users = await userStore.GetUsersInRoleAsync(roleName, CancellationToken.None);

            // Assert
            Assert.Equal(userCount, users.Count);
        }
    }
}