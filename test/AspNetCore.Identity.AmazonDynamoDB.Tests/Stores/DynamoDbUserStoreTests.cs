using System.Security.Claims;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace AspNetCore.Identity.AmazonDynamoDB.Tests;

[Collection(Constants.DatabaseCollection)]
public class DynamoDbUserStoreTests
{
  [Fact]
  public void Should_ThrowArgumentNullException_When_OptionsIsNotSet()
  {
    // Arrange, Act & Assert
    var exception = Assert.Throws<ArgumentNullException>(() =>
      new DynamoDbUserStore<DynamoDbUser>(null!));

    Assert.Equal("optionsMonitor", exception.ParamName);
  }

  [Fact]
  public void Should_ThrowArgumentNullException_When_DatabaseIsNotSet()
  {
    // Arrange, Act & Assert
    var exception = Assert.Throws<ArgumentNullException>(() =>
      new DynamoDbUserStore<DynamoDbUser>(TestUtils.GetOptions(new())));

    Assert.Equal("Database", exception.ParamName);
  }

  [Fact]
  public async Task Should_GetDatabaseFromServiceProvider_When_DatabaseIsNullInOptions()
  {
    // Arrange
    var options = TestUtils.GetOptions(new());
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options, DatabaseFixture.Client);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options, DatabaseFixture.Client);

    // Act
    var user = new DynamoDbUser();
    await userStore.CreateAsync(user, CancellationToken.None);

    // Assert
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var dbUser = await context.LoadAsync<DynamoDbUser>(user.PartitionKey, user.SortKey);
    Assert.NotNull(dbUser);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToCreateUserThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.CreateAsync(default!, CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_CreateUser_When_UserIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser
    {
      UserName = Guid.NewGuid().ToString(),
    };

    // Act
    await userStore.CreateAsync(user, CancellationToken.None);

    // Assert
    var databaseUser = await context.LoadAsync<DynamoDbUser>(user.PartitionKey, user.SortKey);
    Assert.NotNull(databaseUser);
    Assert.Equal(user.UserName, databaseUser.UserName);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToAddClaimsToAUserThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.AddClaimsAsync(default!, new List<Claim>(), CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_DoNothing_When_ClaimsIsEmpty()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser();

    // Act
    await userStore.AddClaimsAsync(user, new List<Claim>(), CancellationToken.None);

    // Assert
    Assert.Null(user.Claims);
  }

  [Fact]
  public async Task Should_AddClaims_When_ClaimsIsNotEmpty()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser();

    // Act
    await userStore.AddClaimsAsync(user, new List<Claim>
    {
      new(ClaimTypes.Country, "se"),
      new(ClaimTypes.Email, "test@test.se"),
    }, CancellationToken.None);

    // Assert
    Assert.Equal(2, user.Claims!.Count);
  }

  [Fact]
  public async Task Should_AddToExistingClaims_When_ClaimAlreadyExists()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var userId = Guid.NewGuid().ToString();
    await userStore.CreateAsync(new DynamoDbUser
    {
      Id = userId,
      Claims = new Dictionary<string, List<string>>
      {
        { ClaimTypes.Country, new List<string> { "us" } },
      },
    }, CancellationToken.None);
    var user = await userStore.FindByIdAsync(userId, CancellationToken.None);

    // Act
    await userStore.AddClaimsAsync(user!, new List<Claim>
    {
      new Claim(ClaimTypes.Country, "se"),
    }, CancellationToken.None);

    // Assert
    Assert.Single(user!.Claims!);
    Assert.Equal(2, user.Claims!.Where(x => x.Key == ClaimTypes.Country).SelectMany(x => x.Value).Count());
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToAddLoginToAUserThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.AddLoginAsync(default!, new("test", "test", "test"), CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToAddLoginToAUserAndTheLoginIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.AddLoginAsync(new(), default!, CancellationToken.None));
    Assert.Equal("login", exception.ParamName);
  }

  [Fact]
  public async Task Should_AddLogin_When_ParametersIsCorrect()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser();

    // Act
    await userStore.AddLoginAsync(
      user, new("test", "test", "test"), CancellationToken.None);

    // Assert
    Assert.Single(user.Logins!);
  }

  [Fact]
  public async Task Should_AddSecondLoginProvider_When_UserExists()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser();
    var login = new DynamoDbUserLogin
    {
      LoginProvider = "first",
      ProviderKey = "first",
      UserId = user.Id,
    };
    await context.SaveAsync(login);

    // Act
    await userStore.AddLoginAsync(
      user, new("second", "second", "second"), CancellationToken.None);

    // Assert
    Assert.Equal(2, user.Logins!.Count);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToAddRoleToAUserThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.AddToRoleAsync(default!, "test", CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToAddRoleToAUserAndTheRoleIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.AddToRoleAsync(new(), default!, CancellationToken.None));
    Assert.Equal("roleName", exception.ParamName);
  }

  [Fact]
  public async Task Should_AddRole_When_ParametersIsCorrect()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser();

    // Act
    await userStore.AddToRoleAsync(
      user, "test", CancellationToken.None);

    // Assert
    Assert.Single(user.Roles!);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToDeleteUserThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.DeleteAsync(default!, CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_DeleteUser_When_ParametersIsCorrect()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser
    {
      Email = "test@test.se",
    };
    await context.SaveAsync(user);

    // Act
    await userStore.DeleteAsync(user, CancellationToken.None);

    // Assert
    var dbUser = await context.LoadAsync<DynamoDbUser>(user.PartitionKey, user.SortKey);
    Assert.Null(dbUser);
  }

  [Fact]
  public async Task Should_DeleteUserWithClaims_When_ParametersIsCorrect()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser
    {
      Email = "test@test.se",
    };
    await context.SaveAsync(user);

    var claims = new List<Claim>();
    for (var i = 0; i < 5; i++)
    {
      var type = $"Claim{i}";
      var value = $"{i}";
      claims.Add(new Claim(type, value));
      var claim = new DynamoDbUserClaim
      {
        ClaimType = type,
        ClaimValue = value,
        UserId = user.Id,
      };
      await context.SaveAsync(claim);
    }

    // Act
    await userStore.DeleteAsync(user, CancellationToken.None);

    // Assert
    var query = await DatabaseFixture.Client.QueryAsync(new QueryRequest()
    {
      ProjectionExpression = "PartitionKey",
      TableName = options.CurrentValue.DefaultTableName,
      KeyConditionExpression = "PartitionKey = :partitionKey",
      ExpressionAttributeValues = new()
      {
        { ":partitionKey", new(user.PartitionKey) },
      },
    });
    Assert.Empty(query.Items);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToFindByEmailThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.FindByEmailAsync(default!, CancellationToken.None));
    Assert.Equal("normalizedEmail", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnDefault_When_FindingByEmailAndUserDoesntExist()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act
    var user = await userStore.FindByEmailAsync("doesnt@exi.st", CancellationToken.None);

    // Assert
    Assert.Null(user);
  }

  [Fact]
  public async Task Should_ReturnUser_When_FindingByEmail()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser
    {
      Email = "test-unique-email@testuniqum.se",
      NormalizedEmail = "TEST-UNIQUE-EMAIL@TESTUNIQUM.SE",
    };
    await context.SaveAsync(user);

    // Act
    var foundUser = await userStore.FindByEmailAsync(user.NormalizedEmail, CancellationToken.None);

    // Assert
    Assert.Equal(user.Id, foundUser!.Id);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToFindUserByIdThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.FindByIdAsync(default!, CancellationToken.None));
    Assert.Equal("userId", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnDefault_When_FindingByIdAndUserDoesntExist()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act
    var user = await userStore.FindByIdAsync(Guid.NewGuid().ToString(), CancellationToken.None);

    // Assert
    Assert.Null(user);
  }

  [Fact]
  public async Task Should_ReturnUser_When_FindingById()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser
    {
      Email = "test@test.se",
    };
    await context.SaveAsync(user);

    // Act
    var foundUser = await userStore.FindByIdAsync(user.Id, CancellationToken.None);

    // Assert
    Assert.Equal(user.Email, foundUser!.Email);
  }

  [Fact]
  public async Task Should_ReturnCustomerUser_When_FindingById()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<CustomUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new CustomUser
    {
      Email = "test@test.se",
      ProfilePictureUrl = "https://test.se/my-beautiful-profile-picture.png",
    };
    await context.SaveAsync(user);

    // Act
    var foundUser = await userStore.FindByIdAsync(user.Id, CancellationToken.None);

    // Assert
    Assert.Equal(user.Email, foundUser!.Email);
    Assert.Equal(user.ProfilePictureUrl, foundUser!.ProfilePictureUrl);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToFindByLoginAndLoginProviderIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.FindByLoginAsync(default!, "test", CancellationToken.None));
    Assert.Equal("loginProvider", exception.ParamName);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToFindByLoginAndProviderKeyIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.FindByLoginAsync("test", default!, CancellationToken.None));
    Assert.Equal("providerKey", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnDefault_When_LoginDoesntExist()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act
    var user = await userStore.FindByLoginAsync(
      "test", Guid.NewGuid().ToString(), CancellationToken.None);

    // Assert
    Assert.Null(user);
  }

  [Fact]
  public async Task Should_ReturnUser_When_FindingByLogin()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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
    Assert.Equal(user.Id, foundUser!.Id);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToFindByNameThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.FindByNameAsync(default!, CancellationToken.None));
    Assert.Equal("normalizedUserName", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnDefault_When_FindingByNameAndUserDoesntExist()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act
    var user = await userStore.FindByNameAsync("doesnt@exi.st", CancellationToken.None);

    // Assert
    Assert.Null(user);
  }

  [Fact]
  public async Task Should_ReturnUser_When_FindingByName()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser
    {
      UserName = "test-unique-name",
      NormalizedUserName = "TEST-UNIQUE-NAME",
    };
    await context.SaveAsync(user);

    // Act
    var foundUser = await userStore.FindByNameAsync(user.NormalizedUserName, CancellationToken.None);

    // Assert
    Assert.Equal(user.Id, foundUser!.Id);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToGetAccessFailedCountOnUserThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.GetAccessFailedCountAsync(default!, CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnAccessFailedCount_When_UserIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToGetEmailOnUserThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.GetEmailAsync(default!, CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnEmail_When_UserIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToGetEmailConfirmedOnUserThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.GetEmailConfirmedAsync(default!, CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnEmailConfirmed_When_UserIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToGetLockoutEnabledOnUserThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.GetLockoutEnabledAsync(default!, CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnLockoutEnabled_When_UserIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToGetLockoutEndOnUserThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.GetLockoutEndDateAsync(default!, CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnLockoutEnd_When_UserIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToGetNormalizedEmailOnUserThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.GetNormalizedEmailAsync(default!, CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnNormalizedEmail_When_UserIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToGetNormalizedUserNameOnUserThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.GetNormalizedUserNameAsync(default!, CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnNormalizedUserName_When_UserIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToGetLoginsAndUserIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.GetLoginsAsync(default!, CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnLogins_When_ListingThem()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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
    Assert.Equal(loginCount, logins.Count);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToGetRolesAndUserIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.GetRolesAsync(default!, CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnRoles_When_ListingThem()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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
    Assert.Equal(loginCount, logins.Count);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToGetPasswordHashOnUserThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.GetPasswordHashAsync(default!, CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnPasswordHash_When_UserIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToGetPhoneNumberOnUserThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.GetPhoneNumberAsync(default!, CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnPhoneNumber_When_UserIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToGetPhoneNumberConfirmedOnUserThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.GetPhoneNumberConfirmedAsync(default!, CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnPhoneNumberConfirmed_When_UserIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToGetSecurityStampOnUserThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.GetSecurityStampAsync(default!, CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnSecurityStamp_When_UserIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToGetTwoFactorEnabledOnUserThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.GetTwoFactorEnabledAsync(default!, CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnTwoFactorEnabled_When_UserIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToGetUserIdOnUserThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.GetUserIdAsync(default!, CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnUserId_When_UserIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToGetUserNameOnUserThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.GetUserNameAsync(default!, CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnUserName_When_UserIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToGetHasPasswordOnUserThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.HasPasswordAsync(default!, CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnTrue_When_UserHasPassword()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToIncrementAccessFailedCountOnUserThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.IncrementAccessFailedCountAsync(default!, CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_IncrementAccessFailedCount_When_Requested()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToCheckIfUserIsInRoleAndUserIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.IsInRoleAsync(default!, "test", CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Theory]
  [InlineData("test", true)]
  [InlineData("testing", false)]
  public async Task Should_ReturnExpected_When_CheckingIfUserIsInRole(string roleName, bool expectedResult)
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToSetEmailAndUserIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.SetEmailAsync(default!, "test@test.se", CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_SetEmail_When_UserIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToSetEmailConfirmedAndUserIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.SetEmailConfirmedAsync(default!, true, CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_SetEmailConfirmed_When_UserIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToSetLockoutEnabledAndUserIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.SetLockoutEnabledAsync(default!, true, CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_SetLockoutEnabled_When_UserIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToSetLockoutEndDateAndUserIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.SetLockoutEndDateAsync(default!, DateTimeOffset.Now, CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_SetLockoutEndDate_When_UserIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToSetNormalizedEmailAndUserIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.SetNormalizedEmailAsync(default!, "TEST@TEST.SE", CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_SetNormalizedEmail_When_UserIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToSetNormalizedUserNameAndUserIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.SetNormalizedUserNameAsync(default!, "TEST", CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_SetNormalizedUserName_When_UserIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToSetPasswordHashAndUserIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.SetPasswordHashAsync(default!, "Secret", CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_SetPasswordHash_When_UserIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToSetPhoneNumberAndUserIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.SetPhoneNumberAsync(default!, "1111111", CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_SetPhoneNumber_When_UserIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToSetPhoneNumberConfirmedAndUserIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.SetPhoneNumberConfirmedAsync(default!, true, CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_SetPhoneNumberConfirmed_When_UserIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToSetTwoFactorEnabledAndUserIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.SetTwoFactorEnabledAsync(default!, true, CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_SetTwoFactorEnabled_When_UserIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToSetSecurityStampAndUserIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.SetSecurityStampAsync(default!, "some-string", CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_SetSecurityStamp_When_UserIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToSetUserNameAndUserIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.SetUserNameAsync(default!, "some-user", CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_SetUserName_When_UserIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToGetClaimsAndUserIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.GetClaimsAsync(default!, CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnClaims_When_ListingThem()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
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

  [Fact]
  public async Task Should_ThrowException_When_TryingToGetUsersInRoleThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.GetUsersInRoleAsync(default!, CancellationToken.None));
    Assert.Equal("roleName", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnUsers_When_ListingThemByRoleName()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

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

  [Fact]
  public async Task Should_ThrowException_When_TryingToResetAccessFailedCountOnUserThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.ResetAccessFailedCountAsync(default!, CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_ResetAccessFailedCount_When_Requested()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser
    {
      AccessFailedCount = 5,
    };
    await context.SaveAsync(user);

    // Act
    await userStore.ResetAccessFailedCountAsync(user, CancellationToken.None);

    // Assert
    Assert.Equal(0, user.AccessFailedCount);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToRemoveClaimsAndUserIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.RemoveClaimsAsync(default!, new List<Claim>(), CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToRemoveClaimsAndClaimsIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.RemoveClaimsAsync(new(), default!, CancellationToken.None));
    Assert.Equal("claims", exception.ParamName);
  }

  [Fact]
  public async Task Should_RemoveClaims_When_RequestIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser();
    await context.SaveAsync(user);

    var claims = new List<Claim>();
    for (var i = 0; i < 5; i++)
    {
      var type = $"Claim{i}";
      var value = $"{i}";
      claims.Add(new Claim(type, value));
      var claim = new DynamoDbUserClaim
      {
        ClaimType = type,
        ClaimValue = value,
        UserId = user.Id,
      };
      await context.SaveAsync(claim);
    }

    // Act
    await userStore.RemoveClaimsAsync(user, claims, CancellationToken.None);

    // Assert
    Assert.Empty(user.Claims!);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingRemoveFromRoleOnUserThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.RemoveFromRoleAsync(default!, "test", CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingRemoveUserFromRoleThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.RemoveFromRoleAsync(new(), default!, CancellationToken.None));
    Assert.Equal("roleName", exception.ParamName);
  }

  [Fact]
  public async Task Should_RemoveUserFromRole_When_RequestIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser();
    await context.SaveAsync(user);
    var roleName = "test";
    var userRole = new DynamoDbUserRole
    {
      RoleName = roleName,
      UserId = user.Id,
    };
    await context.SaveAsync(userRole);

    // Act
    await userStore.RemoveFromRoleAsync(user, roleName, CancellationToken.None);

    // Assert
    Assert.Empty(user.Roles!);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingRemoveFromLoginOnUserThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.RemoveLoginAsync(default!, "test", "test", CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingRemoveLoginAndLoginProviderIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.RemoveLoginAsync(new(), default!, "test", CancellationToken.None));
    Assert.Equal("loginProvider", exception.ParamName);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingRemoveLoginAndProviderKeyIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.RemoveLoginAsync(new(), "test", default!, CancellationToken.None));
    Assert.Equal("providerKey", exception.ParamName);
  }

  [Fact]
  public async Task Should_RemoveLoginFromUser_When_RequestIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser();
    await context.SaveAsync(user);
    var loginProvider = "test";
    var providerKey = Guid.NewGuid().ToString();
    var userRole = new DynamoDbUserLogin
    {
      LoginProvider = loginProvider,
      ProviderKey = providerKey,
      UserId = user.Id,
    };
    await context.SaveAsync(userRole);

    // Act
    await userStore.RemoveLoginAsync(user, loginProvider, providerKey, CancellationToken.None);

    // Assert
    Assert.Empty(user.Logins!);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToGetUsersForClaimThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.GetUsersForClaimAsync(default!, CancellationToken.None));
    Assert.Equal("claim", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnUsers_When_ListingThemByClaim()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    var claimType = "test-claim-type";
    var claimValue = "test-claim-value";
    var userCount = 10;
    for (var index = 0; index < userCount; index++)
    {
      var user = new DynamoDbUser();
      await context.SaveAsync(user);
      var login = new DynamoDbUserClaim
      {
        ClaimType = claimType,
        ClaimValue = claimValue,
        UserId = user.Id,
      };
      await context.SaveAsync(login);
    }

    // Act
    var users = await userStore.GetUsersForClaimAsync(new Claim(claimType, claimValue), CancellationToken.None);

    // Assert
    Assert.Equal(userCount, users.Count);
  }

  [Theory]
  [InlineData(false, true, true, "user")]
  [InlineData(true, false, true, "claim")]
  [InlineData(true, true, false, "newClaim")]
  public async Task Should_ThrowException_When_ParameterIsNull(
    bool userHasValue, bool currentClaimHasValue, bool newClaimHasValue, string expectedParamName)
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.ReplaceClaimAsync(
        userHasValue ? new() : default!,
        currentClaimHasValue ? new("t", "t") : default!,
        newClaimHasValue ? new("t", "t") : default!,
        CancellationToken.None));
    Assert.Equal(expectedParamName, exception.ParamName);
  }

  [Fact]
  public async Task Should_ReplaceClaim_When_RequestIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    var user = new DynamoDbUser();
    await context.SaveAsync(user);

    var currentClaim = new Claim("current", "current");
    var newClaim = new Claim("claim", "claim");
    await context.SaveAsync(new DynamoDbUserClaim
    {
      UserId = user.Id,
      ClaimType = currentClaim.Type,
      ClaimValue = currentClaim.Value,
    });

    // Act
    await userStore.ReplaceClaimAsync(user, currentClaim, newClaim, CancellationToken.None);

    // Assert
    var claims = await userStore.GetClaimsAsync(user, CancellationToken.None);
    Assert.DoesNotContain(claims, x => x.Type == currentClaim.Type && x.Value == currentClaim.Value);
    Assert.Contains(claims, x => x.Type == newClaim.Type && x.Value == newClaim.Value);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToUpdateUserThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.UpdateAsync(default!, CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToUpdateUserThatDoesntExist()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act
    var result = await userStore.UpdateAsync(new(), CancellationToken.None);

    // Assert
    Assert.False(result.Succeeded);
    Assert.Contains(result.Errors, x => x.Code == "ConcurrencyFailure");
  }

  [Fact]
  public async Task Should_ThrowException_When_ConcurrencyStampHasChanged()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser();
    await userStore.CreateAsync(user, CancellationToken.None);

    // Act
    user.ConcurrencyStamp = Guid.NewGuid().ToString();
    var result = await userStore.UpdateAsync(user, CancellationToken.None);

    // Assert
    Assert.False(result.Succeeded);
    Assert.Contains(result.Errors, x => x.Code == "ConcurrencyFailure");
  }

  [Fact]
  public async Task Should_UpdateUser_When_UserIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser();
    await userStore.CreateAsync(user, CancellationToken.None);

    // Act
    user.UserName = "testing-to-update";
    await userStore.UpdateAsync(user, CancellationToken.None);

    // Assert
    var databaseUser = await context.LoadAsync<DynamoDbUser>(user.PartitionKey, user.SortKey);
    Assert.NotNull(databaseUser);
    Assert.Equal(databaseUser.UserName, user.UserName);
  }

  [Fact]
  public async Task Should_RemoveOldClaims_When_Updating()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser
    {
      Claims = new()
      {
        { "test", new() { "test" } },
      }
    };
    await userStore.CreateAsync(user, CancellationToken.None);

    // Act
    await userStore.RemoveClaimsAsync(
      user, new List<Claim> { new("test", "test") }, CancellationToken.None);
    await userStore.UpdateAsync(user, CancellationToken.None);

    // Assert
    var claims = await userStore.GetClaimsAsync(user, CancellationToken.None);
    Assert.Empty(claims);
  }

  [Fact]
  public async Task Should_NotRemoveClaims_When_Updating()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser
    {
      Claims = new()
      {
        { "test", new() { "test" } },
      }
    };
    await userStore.CreateAsync(user, CancellationToken.None);

    // Act
    await userStore.UpdateAsync(user, CancellationToken.None);

    // Assert
    var claims = await userStore.GetClaimsAsync(user, CancellationToken.None);
    Assert.Single(claims);
  }

  [Fact]
  public async Task Should_RemoveOldRoles_When_Updating()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser
    {
      Roles = new() { "test" },
    };
    await userStore.CreateAsync(user, CancellationToken.None);

    // Act
    await userStore.RemoveFromRoleAsync(
      user, "test", CancellationToken.None);
    await userStore.UpdateAsync(user, CancellationToken.None);

    // Assert
    var roles = await userStore.GetRolesAsync(user, CancellationToken.None);
    Assert.Empty(roles);
  }

  [Fact]
  public async Task Should_NotRemoveRoles_When_Updating()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser
    {
      Roles = new() { "test" },
    };
    await userStore.CreateAsync(user, CancellationToken.None);

    // Act
    await userStore.UpdateAsync(user, CancellationToken.None);

    // Assert
    var roles = await userStore.GetRolesAsync(user, CancellationToken.None);
    Assert.Single(roles);
  }

  [Fact]
  public async Task Should_RemoveOldLogins_When_Updating()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser
    {
      Logins = new()
      {
        new()
        {
          LoginProvider = "test",
          ProviderKey = "test",
        },
      },
    };
    await userStore.CreateAsync(user, CancellationToken.None);

    // Act
    await userStore.RemoveLoginAsync(
      user, "test", "test", CancellationToken.None);
    await userStore.UpdateAsync(user, CancellationToken.None);

    // Assert
    var logins = await userStore.GetLoginsAsync(user, CancellationToken.None);
    Assert.Empty(logins);
  }

  [Fact]
  public async Task Should_NotRemoveLogins_When_Updating()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser
    {
      Logins = new()
      {
        new()
        {
          LoginProvider = "test",
          ProviderKey = "test",
        },
      },
    };
    await userStore.CreateAsync(user, CancellationToken.None);

    // Act
    await userStore.UpdateAsync(user, CancellationToken.None);

    // Assert
    var logins = await userStore.GetLoginsAsync(user, CancellationToken.None);
    Assert.Single(logins);
  }

  [Fact]
  public async Task Should_SaveClaims_When_OneKeyHasMultipleValues()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser
    {
      Claims = new()
      {
        { "test", new() { "test", "test2" } },
      },
    };

    // Act
    await userStore.CreateAsync(user, CancellationToken.None);

    // Assert
    var claims = await userStore.GetClaimsAsync(user, CancellationToken.None);
    Assert.Equal(2, claims.Count);
  }

  [Fact]
  public async Task Should_SaveTokens_When_CreatingUser()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var loginProvider = "TestProvider";
    var name = "MyTestThing";
    var value = "ItsAsEasyAs123";
    var user = new DynamoDbUser
    {
      Tokens = new List<IdentityUserToken<string>>
      {
        new()
        {
          LoginProvider = loginProvider,
          Name = name,
          Value = value,
        },
      },
    };

    // Act
    await userStore.CreateAsync(user, CancellationToken.None);

    // Assert
    var token = await userStore.GetTokenAsync(
      user, loginProvider, name, CancellationToken.None);
    Assert.Equal(value, token);
  }

  [Fact]
  public async Task Should_RemoveTokens_When_UpdatingUserAndTokensHasBeenRemoved()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var loginProvider = "TestProvider";
    var name = "MyTestThing";
    var user = new DynamoDbUser
    {
      Tokens = new List<IdentityUserToken<string>>
      {
        new()
        {
          LoginProvider = loginProvider,
          Name = name,
          Value = "ItsAsEasyAs123",
        },
      },
    };
    await userStore.CreateAsync(user, CancellationToken.None);

    // Act
    await userStore.RemoveTokenAsync(user, loginProvider, name, CancellationToken.None);
    await userStore.UpdateAsync(user, CancellationToken.None);

    // Assert
    var token = await userStore.GetTokenAsync(
      user, loginProvider, name, CancellationToken.None);
    Assert.Null(token);
  }

  [Fact]
  public async Task Should_AddToken_When_UpdatingUserAndTokenHasBeenAdded()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var originalLoginProvider = "TestProvider";
    var originalName = "MyTestThing";
    var user = new DynamoDbUser
    {
      Tokens = new List<IdentityUserToken<string>>
      {
        new()
        {
          LoginProvider = originalLoginProvider,
          Name = originalName,
          Value = "ItsAsEasyAs123",
        },
      },
    };
    await userStore.CreateAsync(user, CancellationToken.None);

    // Act
    var newLoginProvider = "NewTestProvider";
    var newName = "NewTestThing";
    await userStore.SetTokenAsync(user, newLoginProvider, newName, "ItsNotAsEasyAs123", CancellationToken.None);
    await userStore.UpdateAsync(user, CancellationToken.None);

    // Assert
    var originalToken = await userStore.GetTokenAsync(
      user, originalLoginProvider, originalName, CancellationToken.None);
    var newToken = await userStore.GetTokenAsync(
      user, newLoginProvider, newName, CancellationToken.None);
    Assert.NotNull(originalToken);
    Assert.NotNull(newToken);
  }

  [Fact]
  public async Task Should_UpdateExistingToken_When_ItAlreadyExists()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var loginProvider = "TestProvider";
    var name = "MyTestThing";
    var user = new DynamoDbUser
    {
      Tokens = new List<IdentityUserToken<string>>
      {
        new()
        {
          LoginProvider = loginProvider,
          Name = name,
          Value = "ItsAsEasyAs123",
        },
      },
    };
    await userStore.CreateAsync(user, CancellationToken.None);

    // Act
    var value = "SomeNewerValue";
    await userStore.SetTokenAsync(user, loginProvider, name, value, CancellationToken.None);
    await userStore.UpdateAsync(user, CancellationToken.None);

    // Assert
    var token = await userStore.GetTokenAsync(user, loginProvider, name, CancellationToken.None);
    Assert.Equal(value, token);
  }

  [Fact]
  public async Task Should_AddAuthenticatorKey_When_UpdatingUser()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser();
    await userStore.CreateAsync(user, CancellationToken.None);

    // Act
    var key = "Test";
    await userStore.SetAuthenticatorKeyAsync(user, key, CancellationToken.None);
    await userStore.UpdateAsync(user, CancellationToken.None);

    // Assert
    var token = await userStore.GetAuthenticatorKeyAsync(user, CancellationToken.None);
    Assert.Equal(key, token);
  }

  [Fact]
  public async Task Should_ReturnAuthenticatorKey_When_OneHasBeenSet()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser();
    await userStore.CreateAsync(user, CancellationToken.None);
    var key = "Test";
    await userStore.SetAuthenticatorKeyAsync(user, key, CancellationToken.None);

    // Act
    var userKey = await userStore.GetAuthenticatorKeyAsync(user, CancellationToken.None);

    // Assert
    Assert.Equal(key, userKey);
  }

  [Theory]
  [InlineData(false, true, true, "user")]
  [InlineData(true, false, true, "loginProvider")]
  [InlineData(true, true, false, "name")]
  public async Task Should_ThrowException_When_GetTokenParameterIsNull(
    bool userHasValue, bool loginProviderHasValue, bool nameHasValue, string expectedParamName)
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.GetTokenAsync(
        userHasValue ? new() : default!,
        loginProviderHasValue ? "test" : default!,
        nameHasValue ? "test" : default!,
        CancellationToken.None));
    Assert.Equal(expectedParamName, exception.ParamName);
  }

  [Theory]
  [InlineData(false, true, true, "user")]
  [InlineData(true, false, true, "loginProvider")]
  [InlineData(true, true, false, "name")]
  public async Task Should_ThrowException_When_RemoveTokenParameterIsNull(
    bool userHasValue, bool loginProviderHasValue, bool nameHasValue, string expectedParamName)
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.RemoveTokenAsync(
        userHasValue ? new() : default!,
        loginProviderHasValue ? "test" : default!,
        nameHasValue ? "test" : default!,
        CancellationToken.None));
    Assert.Equal(expectedParamName, exception.ParamName);
  }

  [Theory]
  [InlineData(false, true, "user")]
  [InlineData(true, false, "code")]
  public async Task Should_ThrowException_When_RedeemCodeParameterIsNull(
    bool userHasValue, bool code, string expectedParamName)
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.RedeemCodeAsync(
        userHasValue ? new() : default!,
        code ? "test" : default!,
        CancellationToken.None));
    Assert.Equal(expectedParamName, exception.ParamName);
  }

  [Fact]
  public async Task Should_CountCodes_When_TokenHasValue()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser();
    await userStore.CreateAsync(user, CancellationToken.None);

    // Act
    await userStore.ReplaceCodesAsync(user, new[] { "1", "2", "3" }, CancellationToken.None);
    var count = await userStore.CountCodesAsync(user, CancellationToken.None);

    // Assert
    Assert.Equal(3, count);
  }

  [Fact]
  public async Task Should_ReturnZero_When_ThereIsNoCodes()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser();
    await userStore.CreateAsync(user, CancellationToken.None);

    // Act
    var count = await userStore.CountCodesAsync(user, CancellationToken.None);

    // Assert
    Assert.Equal(0, count);
  }

  [Fact]
  public async Task Should_ThrowException_When_ReplacingCodesAndUserIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.ReplaceCodesAsync(default!, new[] { "1" }, CancellationToken.None));

    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_ThrowException_When_CountingCodesAndUserIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.CountCodesAsync(default!, CancellationToken.None));

    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnTrue_When_RedeemingExistingCode()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser();
    await userStore.CreateAsync(user, CancellationToken.None);

    // Act
    await userStore.ReplaceCodesAsync(user, new[] { "not-it", "the-code", "not-this-either" }, CancellationToken.None);
    var result = await userStore.RedeemCodeAsync(user, "the-code", CancellationToken.None);

    // Assert
    Assert.True(result);
    var count = await userStore.CountCodesAsync(user, CancellationToken.None);
    Assert.Equal(2, count);
  }

  [Fact]
  public async Task Should_ReturnFalse_When_RedeemingNonExistingCode()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser();
    await userStore.CreateAsync(user, CancellationToken.None);

    // Act
    await userStore.ReplaceCodesAsync(user, new[] { "not-it", "yeah-no-not-it", "not-this-either" }, CancellationToken.None);
    var result = await userStore.RedeemCodeAsync(user, "the-code", CancellationToken.None);

    // Assert
    Assert.False(result);
  }

  [Fact]
  public async Task Should_ReturnFalse_When_ThereIsNoExistingCodes()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser();
    await userStore.CreateAsync(user, CancellationToken.None);

    // Act
    var result = await userStore.RedeemCodeAsync(user, "the-code", CancellationToken.None);

    // Assert
    Assert.False(result);
  }

  [Fact]
  public async Task Should_NotRemoveAuthenticator_When_AddingClaims()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser();
    await userStore.CreateAsync(user, CancellationToken.None);
    await userStore.SetAuthenticatorKeyAsync(user, "test", CancellationToken.None);
    await userStore.UpdateAsync(user, CancellationToken.None);

    // Act
    user = await userStore.FindByIdAsync(user.Id, CancellationToken.None);
    await userStore.AddClaimsAsync(user!, new[] { new Claim("test", "test") }, CancellationToken.None);
    await userStore.UpdateAsync(user!, CancellationToken.None);

    // Assert
    user = await userStore.FindByIdAsync(user!.Id, CancellationToken.None);
    var key = await userStore.GetAuthenticatorKeyAsync(user!, CancellationToken.None);
    Assert.Equal("test", key);
  }

  [Fact]
  public async Task Should_KeepOneAuthenticator_When_RemovingTheOtherAndAddingClaim()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var userStore = new DynamoDbUserStore<DynamoDbUser>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var user = new DynamoDbUser();
    await userStore.CreateAsync(user, CancellationToken.None);
    await userStore.SetTokenAsync(user, "Authenticator", "first", "test", CancellationToken.None);
    await userStore.SetTokenAsync(user, "Authenticator", "second", "test", CancellationToken.None);
    await userStore.UpdateAsync(user, CancellationToken.None);

    // Act
    user = await userStore.FindByIdAsync(user.Id, CancellationToken.None);
    await userStore.AddClaimsAsync(user!, new[] { new Claim("test", "test") }, CancellationToken.None);
    await userStore.RemoveTokenAsync(user!, "Authenticator", "first", CancellationToken.None);
    await userStore.UpdateAsync(user!, CancellationToken.None);

    // Assert
    user = await userStore.FindByIdAsync(user!.Id, CancellationToken.None);
    var first = await userStore.GetTokenAsync(user!, "Authenticator", "first", CancellationToken.None);
    var second = await userStore.GetTokenAsync(user!, "Authenticator", "second", CancellationToken.None);
    var claims = await userStore.GetClaimsAsync(user!, CancellationToken.None);
    Assert.Null(first);
    Assert.NotNull(second);
    Assert.Single(claims);
  }
}
