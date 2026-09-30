#if NET10_0_OR_GREATER
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Amazon.DynamoDBv2.Model;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AspNetCore.Identity.AmazonDynamoDB.Tests;

[Collection(Constants.DatabaseCollection)]
public class DynamoDbUserStorePasskeyTests
{
  [Fact]
  public async Task Should_ThrowException_When_AddingPasskeyToAUserThatIsNull()
  {
    // Arrange
    var userStore = await CreateUserStore();

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.AddOrUpdatePasskeyAsync(default!, CreatePasskey(), CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_ThrowException_When_AddingPasskeyThatIsNull()
  {
    // Arrange
    var userStore = await CreateUserStore();

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.AddOrUpdatePasskeyAsync(new(), default!, CancellationToken.None));
    Assert.Equal("passkey", exception.ParamName);
  }

  [Fact]
  public async Task Should_ThrowException_When_GettingPasskeysForAUserThatIsNull()
  {
    // Arrange
    var userStore = await CreateUserStore();

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.GetPasskeysAsync(default!, CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_ThrowException_When_FindingByPasskeyIdThatIsNull()
  {
    // Arrange
    var userStore = await CreateUserStore();

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.FindByPasskeyIdAsync(default!, CancellationToken.None));
    Assert.Equal("credentialId", exception.ParamName);
  }

  [Fact]
  public async Task Should_ThrowException_When_FindingPasskeyWithCredentialIdThatIsNull()
  {
    // Arrange
    var userStore = await CreateUserStore();

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.FindPasskeyAsync(new(), default!, CancellationToken.None));
    Assert.Equal("credentialId", exception.ParamName);
  }

  [Fact]
  public async Task Should_ThrowException_When_RemovingPasskeyFromAUserThatIsNull()
  {
    // Arrange
    var userStore = await CreateUserStore();

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
      await userStore.RemovePasskeyAsync(default!, [1, 2, 3], CancellationToken.None));
    Assert.Equal("user", exception.ParamName);
  }

  [Fact]
  public async Task Should_AddPasskey_When_ParametersAreCorrect()
  {
    // Arrange
    var userStore = await CreateUserStore();
    var user = new DynamoDbUser();

    // Act
    await userStore.AddOrUpdatePasskeyAsync(user, CreatePasskey(), CancellationToken.None);

    // Assert
    Assert.Single(user.Passkeys!);
  }

  [Fact]
  public async Task Should_PersistAllPasskeyFields_When_CreatingUser()
  {
    // Arrange
    var userStore = await CreateUserStore();
    var user = new DynamoDbUser();
    var passkey = CreatePasskey();
    await userStore.AddOrUpdatePasskeyAsync(user, passkey, CancellationToken.None);

    // Act
    await userStore.CreateAsync(user, CancellationToken.None);

    // Assert
    var persistedUser = await userStore.FindByIdAsync(user.Id, CancellationToken.None);
    var passkeys = await userStore.GetPasskeysAsync(persistedUser!, CancellationToken.None);
    var persisted = Assert.Single(passkeys);
    AssertEqual(passkey, persisted);
  }

  public static TheoryData<string[]?> TransportCases => new()
  {
    null,
    Array.Empty<string>(),
    new[] { "internal", "hybrid", "usb" },
  };

  [Theory]
  [MemberData(nameof(TransportCases))]
  public async Task Should_RoundTripTransports_When_Persisted(string[]? transports)
  {
    // Arrange
    var userStore = await CreateUserStore();
    var user = new DynamoDbUser();
    var passkey = CreatePasskey(default, 0, transports);
    await userStore.AddOrUpdatePasskeyAsync(user, passkey, CancellationToken.None);

    // Act
    await userStore.CreateAsync(user, CancellationToken.None);

    // Assert
    var persistedUser = await userStore.FindByIdAsync(user.Id, CancellationToken.None);
    var persisted = await userStore.FindPasskeyAsync(
      persistedUser!, passkey.CredentialId, CancellationToken.None);
    Assert.Equal(transports, persisted!.Transports);
  }

  [Fact]
  public async Task Should_RoundTripCredentialId_When_ItContainsBase64SpecialCharacters()
  {
    // Arrange
    var userStore = await CreateUserStore();
    var user = new DynamoDbUser();
    // Encodes to "+/+/" in standard base64 and needs padding when a byte is appended
    byte[] credentialId = [0xfb, 0xff, 0xbf, 0xfe];
    await userStore.AddOrUpdatePasskeyAsync(
      user, CreatePasskey(credentialId), CancellationToken.None);

    // Act
    await userStore.CreateAsync(user, CancellationToken.None);

    // Assert
    var foundUser = await userStore.FindByPasskeyIdAsync(credentialId, CancellationToken.None);
    Assert.Equal(user.Id, foundUser!.Id);
    var passkey = await userStore.FindPasskeyAsync(foundUser, credentialId, CancellationToken.None);
    Assert.Equal(credentialId, passkey!.CredentialId);
  }

  [Fact]
  public async Task Should_ReturnUser_When_FindingByPasskeyId()
  {
    // Arrange
    var userStore = await CreateUserStore();
    var user = new DynamoDbUser
    {
      Email = "passkey@test.se",
    };
    var passkey = CreatePasskey();
    await userStore.AddOrUpdatePasskeyAsync(user, passkey, CancellationToken.None);
    await userStore.CreateAsync(user, CancellationToken.None);

    // Act
    var foundUser = await userStore.FindByPasskeyIdAsync(passkey.CredentialId, CancellationToken.None);

    // Assert
    Assert.Equal(user.Id, foundUser!.Id);
    Assert.Equal(user.Email, foundUser.Email);
  }

  [Fact]
  public async Task Should_ReturnDefault_When_PasskeyIdDoesntExist()
  {
    // Arrange
    var userStore = await CreateUserStore();

    // Act
    var foundUser = await userStore.FindByPasskeyIdAsync(
      RandomNumberGenerator.GetBytes(32), CancellationToken.None);

    // Assert
    Assert.Null(foundUser);
  }

  [Fact]
  public async Task Should_ReturnPasskey_When_FindingPasskeyOfUser()
  {
    // Arrange
    var userStore = await CreateUserStore();
    var user = new DynamoDbUser();
    var passkey = CreatePasskey();
    await userStore.AddOrUpdatePasskeyAsync(user, CreatePasskey(), CancellationToken.None);
    await userStore.AddOrUpdatePasskeyAsync(user, passkey, CancellationToken.None);
    await userStore.CreateAsync(user, CancellationToken.None);
    var persistedUser = await userStore.FindByIdAsync(user.Id, CancellationToken.None);

    // Act
    var found = await userStore.FindPasskeyAsync(
      persistedUser!, passkey.CredentialId, CancellationToken.None);

    // Assert
    AssertEqual(passkey, found!);
  }

  [Fact]
  public async Task Should_ReturnDefault_When_FindingPasskeyThatBelongsToAnotherUser()
  {
    // Arrange
    var userStore = await CreateUserStore();
    var owner = new DynamoDbUser();
    var passkey = CreatePasskey();
    await userStore.AddOrUpdatePasskeyAsync(owner, passkey, CancellationToken.None);
    await userStore.CreateAsync(owner, CancellationToken.None);
    var otherUser = new DynamoDbUser();
    await userStore.CreateAsync(otherUser, CancellationToken.None);

    // Act
    var found = await userStore.FindPasskeyAsync(
      otherUser, passkey.CredentialId, CancellationToken.None);

    // Assert
    Assert.Null(found);
  }

  [Fact]
  public async Task Should_ReturnAllPasskeys_When_UserHasMultiple()
  {
    // Arrange
    var userStore = await CreateUserStore();
    var user = new DynamoDbUser();
    await userStore.AddOrUpdatePasskeyAsync(user, CreatePasskey(), CancellationToken.None);
    await userStore.AddOrUpdatePasskeyAsync(user, CreatePasskey(), CancellationToken.None);
    await userStore.AddOrUpdatePasskeyAsync(user, CreatePasskey(), CancellationToken.None);
    await userStore.CreateAsync(user, CancellationToken.None);
    var persistedUser = await userStore.FindByIdAsync(user.Id, CancellationToken.None);

    // Act
    var passkeys = await userStore.GetPasskeysAsync(persistedUser!, CancellationToken.None);

    // Assert
    Assert.Equal(3, passkeys.Count);
  }

  [Fact]
  public async Task Should_UpdateMutableFields_When_PasskeyAlreadyExists()
  {
    // Arrange
    var userStore = await CreateUserStore();
    var user = new DynamoDbUser();
    var passkey = CreatePasskey(signCount: 1);
    await userStore.AddOrUpdatePasskeyAsync(user, passkey, CancellationToken.None);
    await userStore.CreateAsync(user, CancellationToken.None);
    var persistedUser = await userStore.FindByIdAsync(user.Id, CancellationToken.None);
    var updated = new UserPasskeyInfo(
      passkey.CredentialId,
      RandomNumberGenerator.GetBytes(77),
      passkey.CreatedAt.AddDays(1),
      signCount: 42,
      passkey.Transports,
      isUserVerified: false,
      passkey.IsBackupEligible,
      isBackedUp: true,
      passkey.AttestationObject,
      passkey.ClientDataJson)
    {
      Name = "Renamed passkey",
    };

    // Act
    await userStore.AddOrUpdatePasskeyAsync(persistedUser!, updated, CancellationToken.None);
    await userStore.UpdateAsync(persistedUser!, CancellationToken.None);

    // Assert
    var reloadedUser = await userStore.FindByIdAsync(user.Id, CancellationToken.None);
    var passkeys = await userStore.GetPasskeysAsync(reloadedUser!, CancellationToken.None);
    var persisted = Assert.Single(passkeys);
    Assert.Equal(42u, persisted.SignCount);
    Assert.Equal("Renamed passkey", persisted.Name);
    Assert.True(persisted.IsBackedUp);
    Assert.False(persisted.IsUserVerified);
    Assert.Equal(passkey.PublicKey, persisted.PublicKey);
    Assert.Equal(passkey.CreatedAt, persisted.CreatedAt);
  }

  [Fact]
  public async Task Should_RemovePasskey_When_Updating()
  {
    // Arrange
    var userStore = await CreateUserStore();
    var user = new DynamoDbUser();
    var removed = CreatePasskey();
    var kept = CreatePasskey();
    await userStore.AddOrUpdatePasskeyAsync(user, removed, CancellationToken.None);
    await userStore.AddOrUpdatePasskeyAsync(user, kept, CancellationToken.None);
    await userStore.CreateAsync(user, CancellationToken.None);
    var persistedUser = await userStore.FindByIdAsync(user.Id, CancellationToken.None);

    // Act
    await userStore.RemovePasskeyAsync(persistedUser!, removed.CredentialId, CancellationToken.None);
    await userStore.UpdateAsync(persistedUser!, CancellationToken.None);

    // Assert
    var reloadedUser = await userStore.FindByIdAsync(user.Id, CancellationToken.None);
    var passkeys = await userStore.GetPasskeysAsync(reloadedUser!, CancellationToken.None);
    var remaining = Assert.Single(passkeys);
    Assert.Equal(kept.CredentialId, remaining.CredentialId);
    Assert.Null(await userStore.FindByPasskeyIdAsync(removed.CredentialId, CancellationToken.None));
  }

  [Fact]
  public async Task Should_NotRemovePasskeys_When_Updating()
  {
    // Arrange
    var userStore = await CreateUserStore();
    var user = new DynamoDbUser();
    await userStore.AddOrUpdatePasskeyAsync(user, CreatePasskey(), CancellationToken.None);
    await userStore.CreateAsync(user, CancellationToken.None);

    // Act
    await userStore.UpdateAsync(user, CancellationToken.None);

    // Assert
    var reloadedUser = await userStore.FindByIdAsync(user.Id, CancellationToken.None);
    var passkeys = await userStore.GetPasskeysAsync(reloadedUser!, CancellationToken.None);
    Assert.Single(passkeys);
  }

  [Fact]
  public async Task Should_RemovePasskeys_When_DeletingUser()
  {
    // Arrange
    var userStore = await CreateUserStore();
    var user = new DynamoDbUser();
    var passkey = CreatePasskey();
    await userStore.AddOrUpdatePasskeyAsync(user, passkey, CancellationToken.None);
    await userStore.CreateAsync(user, CancellationToken.None);

    // Act
    await userStore.DeleteAsync(user, CancellationToken.None);

    // Assert
    Assert.Null(await userStore.FindByPasskeyIdAsync(passkey.CredentialId, CancellationToken.None));
  }

  [Fact]
  public async Task Should_SupportPasskeys_When_UsedThroughUserManager()
  {
    // Arrange
    var services = new ServiceCollection();
    services.AddSingleton<Amazon.DynamoDBv2.IAmazonDynamoDB>(DatabaseFixture.Client);
    services
      .AddIdentityCore<DynamoDbUser>()
      .AddDynamoDbStores()
      .SetDefaultTableName(DatabaseFixture.TableName);
    var serviceProvider = services.BuildServiceProvider();
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(serviceProvider);
    var userManager = serviceProvider.GetRequiredService<UserManager<DynamoDbUser>>();
    var user = new DynamoDbUser
    {
      UserName = $"passkey-{Guid.NewGuid()}",
    };
    await userManager.CreateAsync(user);
    var passkey = CreatePasskey(signCount: 1);

    // Act
    var addResult = await userManager.AddOrUpdatePasskeyAsync(user, passkey);
    passkey.SignCount = 2;
    var updateResult = await userManager.AddOrUpdatePasskeyAsync(user, passkey);

    // Assert
    Assert.True(userManager.SupportsUserPasskey);
    Assert.True(addResult.Succeeded);
    Assert.True(updateResult.Succeeded);
    var foundUser = await userManager.FindByPasskeyIdAsync(passkey.CredentialId);
    Assert.Equal(user.Id, foundUser!.Id);
    var persisted = await userManager.GetPasskeyAsync(foundUser, passkey.CredentialId);
    Assert.Equal(2u, persisted!.SignCount);

    var removeResult = await userManager.RemovePasskeyAsync(foundUser, passkey.CredentialId);
    Assert.True(removeResult.Succeeded);
    Assert.Null(await userManager.FindByPasskeyIdAsync(passkey.CredentialId));
  }

  [Fact]
  public async Task Should_OnlyWriteChangedPasskey_When_Updating()
  {
    // Arrange
    var (userStore, recorder) = await CreateRecordingUserStore();
    var passkeys = new[] { CreatePasskey(), CreatePasskey(), CreatePasskey() };
    var user = await CreateUserWithPasskeys(userStore, passkeys);
    var passkey = (await userStore.FindPasskeyAsync(user, passkeys[1].CredentialId, CancellationToken.None))!;
    passkey.SignCount = 5;
    await userStore.AddOrUpdatePasskeyAsync(user, passkey, CancellationToken.None);
    recorder.Requests.Clear();

    // Act
    var result = await userStore.UpdateAsync(user, CancellationToken.None);

    // Assert
    Assert.True(result.Succeeded);
    var write = Assert.Single(GetPasskeyWrites(recorder));
    Assert.Equal(GetSortKey(passkeys[1]), write.PutRequest.Item["SortKey"].S);
    Assert.Empty(recorder.Requests.OfType<GetItemRequest>());
    var persisted = await userStore.FindPasskeyAsync(
      new DynamoDbUser { Id = user.Id }, passkeys[1].CredentialId, CancellationToken.None);
    Assert.Equal(5u, persisted!.SignCount);
  }

  [Fact]
  public async Task Should_OnlyDeleteRemovedPasskey_When_Updating()
  {
    // Arrange
    var (userStore, recorder) = await CreateRecordingUserStore();
    var passkeys = new[] { CreatePasskey(), CreatePasskey(), CreatePasskey() };
    var user = await CreateUserWithPasskeys(userStore, passkeys);
    await userStore.RemovePasskeyAsync(user, passkeys[0].CredentialId, CancellationToken.None);
    recorder.Requests.Clear();

    // Act
    await userStore.UpdateAsync(user, CancellationToken.None);

    // Assert
    var write = Assert.Single(GetPasskeyWrites(recorder));
    Assert.Equal(GetSortKey(passkeys[0]), write.DeleteRequest.Key["SortKey"].S);
    Assert.Equal(2, (await userStore.GetPasskeysAsync(new DynamoDbUser { Id = user.Id }, CancellationToken.None)).Count);
  }

  [Fact]
  public async Task Should_NotWritePasskeys_When_NothingChanged()
  {
    // Arrange
    var (userStore, recorder) = await CreateRecordingUserStore();
    var user = await CreateUserWithPasskeys(userStore, [CreatePasskey(), CreatePasskey(transports: null)]);
    await userStore.GetPasskeysAsync(user, CancellationToken.None);
    recorder.Requests.Clear();

    // Act
    await userStore.UpdateAsync(user, CancellationToken.None);

    // Assert
    Assert.Empty(GetPasskeyWrites(recorder));
  }

  [Fact]
  public async Task Should_UseConsistentRead_When_FindingUserByPasskeyId()
  {
    // Arrange
    var (userStore, recorder) = await CreateRecordingUserStore();
    var passkey = CreatePasskey();
    var user = await CreateUserWithPasskeys(userStore, [passkey]);
    recorder.Requests.Clear();

    // Act
    var found = await userStore.FindByPasskeyIdAsync(passkey.CredentialId, CancellationToken.None);

    // Assert
    Assert.Equal(user.Id, found!.Id);
    Assert.True(Assert.Single(recorder.Requests.OfType<GetItemRequest>()).ConsistentRead);
  }

  private static async Task<(DynamoDbUserStore<DynamoDbUser>, RecordingDynamoDbClient)> CreateRecordingUserStore()
  {
    var recorder = new RecordingDynamoDbClient();
    var options = TestUtils.GetOptions(new() { Database = recorder });
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    return (new DynamoDbUserStore<DynamoDbUser>(options), recorder);
  }

  private static async Task<DynamoDbUser> CreateUserWithPasskeys(
    DynamoDbUserStore<DynamoDbUser> userStore, IEnumerable<UserPasskeyInfo> passkeys)
  {
    var user = new DynamoDbUser();
    foreach (var passkey in passkeys)
    {
      await userStore.AddOrUpdatePasskeyAsync(user, passkey, CancellationToken.None);
    }
    await userStore.CreateAsync(user, CancellationToken.None);
    return (await userStore.FindByIdAsync(user.Id, CancellationToken.None))!;
  }

  private static List<WriteRequest> GetPasskeyWrites(RecordingDynamoDbClient recorder) => recorder.Requests
    .OfType<BatchWriteItemRequest>()
    .SelectMany(x => x.RequestItems.Values.SelectMany(y => y))
    .Where(x => (x.PutRequest?.Item ?? x.DeleteRequest?.Key)?["SortKey"].S.StartsWith("PASSKEY#") == true)
    .ToList();

  private static string GetSortKey(UserPasskeyInfo passkey)
    => $"PASSKEY#{Base64Url.EncodeToString(passkey.CredentialId)}";

  private static async Task<DynamoDbUserStore<DynamoDbUser>> CreateUserStore()
  {
    var options = TestUtils.GetOptions(new() { Database = DatabaseFixture.Client });
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(options);
    return new DynamoDbUserStore<DynamoDbUser>(options);
  }

  private static UserPasskeyInfo CreatePasskey(byte[]? credentialId = default, uint signCount = 0)
    => CreatePasskey(credentialId, signCount, ["internal", "hybrid"]);

  private static UserPasskeyInfo CreatePasskey(string[]? transports)
    => CreatePasskey(default, 0, transports);

  private static UserPasskeyInfo CreatePasskey(
    byte[]? credentialId,
    uint signCount,
    string[]? transports) => new(
      credentialId ?? RandomNumberGenerator.GetBytes(32),
      RandomNumberGenerator.GetBytes(77),
      DateTimeOffset.UtcNow,
      signCount,
      transports,
      isUserVerified: true,
      isBackupEligible: true,
      isBackedUp: false,
      RandomNumberGenerator.GetBytes(300),
      Encoding.UTF8.GetBytes("""{"type":"webauthn.create","challenge":"abc"}"""))
    {
      Name = "Test passkey",
    };

  private static void AssertEqual(UserPasskeyInfo expected, UserPasskeyInfo actual)
  {
    Assert.Equal(expected.CredentialId, actual.CredentialId);
    Assert.Equal(expected.PublicKey, actual.PublicKey);
    Assert.Equal(expected.Name, actual.Name);
    Assert.Equal(expected.CreatedAt, actual.CreatedAt);
    Assert.Equal(expected.SignCount, actual.SignCount);
    Assert.Equal(expected.Transports, actual.Transports);
    Assert.Equal(expected.IsUserVerified, actual.IsUserVerified);
    Assert.Equal(expected.IsBackupEligible, actual.IsBackupEligible);
    Assert.Equal(expected.IsBackedUp, actual.IsBackedUp);
    Assert.Equal(expected.AttestationObject, actual.AttestationObject);
    Assert.Equal(expected.ClientDataJson, actual.ClientDataJson);
  }
}
#endif
