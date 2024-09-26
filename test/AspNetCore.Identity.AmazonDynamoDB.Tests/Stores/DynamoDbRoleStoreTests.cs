using Amazon.DynamoDBv2.DataModel;
using Xunit;

namespace AspNetCore.Identity.AmazonDynamoDB.Tests;

[Collection(Constants.DatabaseCollection)]
public class DynamoDbRoleStoreTests
{
  [Fact]
  public void Should_ThrowArgumentNullException_When_OptionsIsNotSet()
  {
    // Arrange, Act & Assert
    var exception = Assert.Throws<ArgumentNullException>(() =>
        new DynamoDbRoleStore<DynamoDbRole>(null!));

    Assert.Equal("optionsMonitor", exception.ParamName);
  }

  [Fact]
  public void Should_ThrowArgumentNullException_When_DatabaseIsNotSet()
  {
    // Arrange, Act & Assert
    var exception = Assert.Throws<ArgumentNullException>(() =>
        new DynamoDbRoleStore<DynamoDbRole>(TestUtils.GetOptions(new())));

    Assert.Equal("database", exception.ParamName);
  }

  [Fact]
  public async Task Should_GetDatabaseFromServiceProvider_When_DatabaseIsNullInOptions()
  {
    // Arrange
    var options = TestUtils.GetOptions(new());
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options, DatabaseFixture.Client);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options, DatabaseFixture.Client);

    // Act
    var role = new DynamoDbRole();
    await roleStore.CreateAsync(role, CancellationToken.None);

    // Assert
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var dbRole = await context.LoadAsync<DynamoDbRole>(role.PartitionKey, role.SortKey);
    Assert.NotNull(dbRole);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToCreateRoleThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        await roleStore.CreateAsync(default!, CancellationToken.None));
    Assert.Equal("role", exception.ParamName);
  }

  [Fact]
  public async Task Should_CreateRole_When_RoleIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var role = new DynamoDbRole
    {
      Name = Guid.NewGuid().ToString(),
    };

    // Act
    await roleStore.CreateAsync(role, CancellationToken.None);

    // Assert
    var databaseRole = await context.LoadAsync<DynamoDbRole>(role.PartitionKey, role.SortKey);
    Assert.NotNull(databaseRole);
    Assert.Equal(role.Name, databaseRole.Name);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToDeleteRoleThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        await roleStore.DeleteAsync(default!, CancellationToken.None));
    Assert.Equal("role", exception.ParamName);
  }

  [Fact]
  public async Task Should_DeleteRole_When_ParametersIsCorrect()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var role = new DynamoDbRole
    {
      Name = "test",
    };
    await context.SaveAsync(role);

    // Act
    await roleStore.DeleteAsync(role, CancellationToken.None);

    // Assert
    var dbRole = await context.LoadAsync<DynamoDbRole>(role.PartitionKey, role.SortKey);
    Assert.Null(dbRole);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToAddClaimToRoleThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        await roleStore.AddClaimAsync(default!, new("t", "t"), CancellationToken.None));
    Assert.Equal("role", exception.ParamName);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToAddClaimToRoleAndTheClaimIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        await roleStore.AddClaimAsync(new(), default!, CancellationToken.None));
    Assert.Equal("claim", exception.ParamName);
  }

  [Fact]
  public async Task Should_AddClaim_When_RequestIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var role = new DynamoDbRole();
    await context.SaveAsync(role);

    var claimType = "test";
    var claimValue = "test";

    // Act
    await roleStore.AddClaimAsync(
      role, new(claimType, claimValue), CancellationToken.None);

    // Assert
    Assert.Contains(role.Claims, x => x.Key == claimType && x.Value.Contains(claimValue));
  }

  [Fact]
  public async Task Should_AddValueToClaim_When_ClaimAlreadyExists()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var claimType = "test";
    var role = new DynamoDbRole();
    role.Claims.Add(claimType, new() { "testicles" });
    await context.SaveAsync(role);

    var claimValue = "test";

    // Act
    await roleStore.AddClaimAsync(
      role, new(claimType, claimValue), CancellationToken.None);

    // Assert
    Assert.Contains(role.Claims, x => x.Key == claimType && x.Value.Contains(claimValue));
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToFindRoleByIdThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        await roleStore.FindByIdAsync(default!, CancellationToken.None));
    Assert.Equal("roleId", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnDefault_When_FindingByIdAndRoleDoesntExist()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act
    var role = await roleStore.FindByIdAsync(Guid.NewGuid().ToString(), CancellationToken.None);

    // Assert
    Assert.Null(role);
  }

  [Fact]
  public async Task Should_ReturnRole_When_FindingById()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var role = new DynamoDbRole
    {
      Name = "test@test.se",
    };
    await context.SaveAsync(role);

    // Act
    var foundRole = await roleStore.FindByIdAsync(role.Id, CancellationToken.None);

    // Assert
    Assert.Equal(role.Name, foundRole!.Name);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToFindByNameThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        await roleStore.FindByNameAsync(default!, CancellationToken.None));
    Assert.Equal("normalizedRoleName", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnDefault_When_FindingByNameAndRoleDoesntExist()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act
    var role = await roleStore.FindByNameAsync("doesnt@exi.st", CancellationToken.None);

    // Assert
    Assert.Null(role);
  }

  [Fact]
  public async Task Should_ReturnRole_When_FindingByName()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var role = new DynamoDbRole
    {
      Name = "test",
      NormalizedName = "TEST",
    };
    await context.SaveAsync(role);

    // Act
    var foundRole = await roleStore.FindByNameAsync(role.NormalizedName, CancellationToken.None);

    // Assert
    Assert.Equal(role.Id, foundRole!.Id);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToGetClaimsForRoleThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        await roleStore.GetClaimsAsync(default!, CancellationToken.None));
    Assert.Equal("role", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnClaims_When_RoleIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var role = new DynamoDbRole();
    role.Claims.Add("test", new() { "test" });
    role.Claims.Add("test2", new() { "test2" });
    role.Claims.Add("test3", new() { "test3" });
    await context.SaveAsync(role);

    // Act
    var claims = await roleStore.GetClaimsAsync(role, CancellationToken.None);

    // Assert
    Assert.Equal(role.Claims.Count, claims.Count);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToGetNormalizedRoleNameForRoleThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        await roleStore.GetNormalizedRoleNameAsync(default!, CancellationToken.None));
    Assert.Equal("role", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnNormalizedRoleName_When_RoleIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var role = new DynamoDbRole
    {
      NormalizedName = "TEST",
    };
    await context.SaveAsync(role);

    // Act
    var name = await roleStore.GetNormalizedRoleNameAsync(role, CancellationToken.None);

    // Assert
    Assert.Equal(role.NormalizedName, name);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToGetIdForRoleThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        await roleStore.GetRoleIdAsync(default!, CancellationToken.None));
    Assert.Equal("role", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnId_When_RoleIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var role = new DynamoDbRole
    {
      Id = Guid.NewGuid().ToString(),
    };
    await context.SaveAsync(role);

    // Act
    var id = await roleStore.GetRoleIdAsync(role, CancellationToken.None);

    // Assert
    Assert.Equal(role.Id, id);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToGetNameForRoleThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        await roleStore.GetRoleNameAsync(default!, CancellationToken.None));
    Assert.Equal("role", exception.ParamName);
  }

  [Fact]
  public async Task Should_ReturnName_When_RoleIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var role = new DynamoDbRole
    {
      Name = "test",
    };
    await context.SaveAsync(role);

    // Act
    var id = await roleStore.GetRoleNameAsync(role, CancellationToken.None);

    // Assert
    Assert.Equal(role.Name, id);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToRemoveClaimOnRoleThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        await roleStore.RemoveClaimAsync(default!, new("t", "t"), CancellationToken.None));
    Assert.Equal("role", exception.ParamName);
  }

  [Fact]
  public async Task Should_RemoveClaim_When_RoleIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var role = new DynamoDbRole();
    role.Claims.Add("test", new() { "test" });
    await context.SaveAsync(role);

    // Act
    await roleStore.RemoveClaimAsync(
        role, new("test", "test"), CancellationToken.None);

    // Assert
    Assert.Empty(role.Claims);
  }

  [Fact]
  public async Task Should_NotRemoveClaim_When_ClaimValueDoesntExist()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var role = new DynamoDbRole();
    role.Claims.Add("test", new() { "test" });
    await context.SaveAsync(role);

    // Act
    await roleStore.RemoveClaimAsync(
        role, new("test", "testicles"), CancellationToken.None);

    // Assert
    Assert.NotEmpty(role.Claims);
  }

  [Fact]
  public async Task Should_KeepClaim_When_ClaimHasMultipleValues()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var role = new DynamoDbRole();
    role.Claims.Add("test", new() { "test", "testicles" });
    await context.SaveAsync(role);

    // Act
    await roleStore.RemoveClaimAsync(
        role, new("test", "testicles"), CancellationToken.None);

    // Assert
    Assert.NotEmpty(role.Claims);
    Assert.Contains(role.Claims, x => x.Key == "test");
    Assert.DoesNotContain(role.Claims, x => x.Key == "testicles");
  }

  [Theory]
  [InlineData(false, true, "role")]
  [InlineData(true, false, "normalizedName")]
  public async Task Should_ThrowException_When_SettingNormalizedNameAndParameterIsNull(
      bool roleHasValue, bool normalizedNameHasValue, string expectedParamName)
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        await roleStore.SetNormalizedRoleNameAsync(
            roleHasValue ? new() : default!,
            normalizedNameHasValue ? "test" : default!,
            CancellationToken.None));
    Assert.Equal(expectedParamName, exception.ParamName);
  }

  [Fact]
  public async Task Should_SetNormalizedName_When_RoleIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var role = new DynamoDbRole
    {
      NormalizedName = "TEST",
    };
    await context.SaveAsync(role);

    // Act
    await roleStore.SetNormalizedRoleNameAsync(role, "TESTICLES", CancellationToken.None);

    // Assert
    Assert.Equal("TESTICLES", role.NormalizedName);
  }

  [Theory]
  [InlineData(false, true, "role")]
  [InlineData(true, false, "roleName")]
  public async Task Should_ThrowException_When_SettingNameAndParameterIsNull(
      bool roleHasValue, bool roleNameHasValue, string expectedParamName)
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await roleStore.SetRoleNameAsync(
        roleHasValue ? new() : default!,
        roleNameHasValue ? "test" : default!,
        CancellationToken.None));
    Assert.Equal(expectedParamName, exception.ParamName);
  }

  [Fact]
  public async Task Should_SetName_When_RoleIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var role = new DynamoDbRole
    {
      Name = "test",
    };
    await context.SaveAsync(role);

    // Act
    await roleStore.SetRoleNameAsync(role, "testicles", CancellationToken.None);

    // Assert
    Assert.Equal("testicles", role.Name);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToUpdateRoleThatIsNull()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        await roleStore.UpdateAsync(default!, CancellationToken.None));
    Assert.Equal("role", exception.ParamName);
  }

  [Fact]
  public async Task Should_ThrowException_When_TryingToUpdateRoleThatDoesntExist()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);

    // Act
    var result = await roleStore.UpdateAsync(new(), CancellationToken.None);

    // Assert
    Assert.False(result.Succeeded);
    Assert.Contains(result.Errors, x => x.Code == "ConcurrencyFailure");
  }

  [Fact]
  public async Task Should_ThrowException_When_ConcurrencyStampHasChanged()
  {
    // Arrange
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var role = new DynamoDbRole();
    await roleStore.CreateAsync(role, CancellationToken.None);

    // Act
    role.ConcurrencyStamp = Guid.NewGuid().ToString();
    var result = await roleStore.UpdateAsync(role, CancellationToken.None);

    // Assert
    Assert.False(result.Succeeded);
    Assert.Contains(result.Errors, x => x.Code == "ConcurrencyFailure");
  }

  [Fact]
  public async Task Should_UpdateRole_When_RoleIsValid()
  {
    // Arrange
    var context = new DynamoDBContext(DatabaseFixture.Client);
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    var roleStore = new DynamoDbRoleStore<DynamoDbRole>(options);
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    var role = new DynamoDbRole();
    await roleStore.CreateAsync(role, CancellationToken.None);

    // Act
    role.Name = "testing-to-update";
    await roleStore.UpdateAsync(role, CancellationToken.None);

    // Assert
    var databaseRole = await context.LoadAsync<DynamoDbRole>(role.PartitionKey, role.SortKey);
    Assert.NotNull(databaseRole);
    Assert.Equal(databaseRole.Name, role.Name);
  }
}
