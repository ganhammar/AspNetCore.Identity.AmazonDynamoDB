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
}