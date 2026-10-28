using System.Security.Claims;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AspNetCore.Identity.AmazonDynamoDB;

public class DynamoDbUserStore<TUserEntity> :
    IUserStore<TUserEntity>,
    IUserRoleStore<TUserEntity>,
    IUserEmailStore<TUserEntity>,
    IUserPasswordStore<TUserEntity>,
    IUserPhoneNumberStore<TUserEntity>,
    IUserLockoutStore<TUserEntity>,
    IUserClaimStore<TUserEntity>,
    IUserSecurityStampStore<TUserEntity>,
    IUserTwoFactorStore<TUserEntity>,
    IUserLoginStore<TUserEntity>,
    IUserAuthenticatorKeyStore<TUserEntity>,
    IUserAuthenticationTokenStore<TUserEntity>,
    IUserTwoFactorRecoveryCodeStore<TUserEntity>,
#if NET10_0_OR_GREATER
    IUserPasskeyStore<TUserEntity>,
#endif
    IProtectedUserStore<TUserEntity>
  where TUserEntity : DynamoDbUser, new()
{
  private static readonly IdentityErrorDescriber Errors = new();
  private readonly IAmazonDynamoDB _client;
  private readonly IOptionsMonitor<DynamoDbOptions> _optionsMonitor;
  private string _tableName =>
    _optionsMonitor.CurrentValue.DefaultTableName ?? Constants.DefaultTableName;
  private const string InternalLoginProvider = "[AspNetUserStore]";
  private const string AuthenticatorKeyTokenName = "AuthenticatorKey";
  private const string RecoveryCodeTokenName = "RecoveryCodes";
  private const string ClaimSortKeyPrefix = "CLAIM#";
  private const string LoginSortKeyPrefix = "LOGIN#";
  private const string RoleSortKeyPrefix = "ROLE#";
  private const string TokenSortKeyPrefix = "TOKEN#";
  private const string PasskeySortKeyPrefix = "PASSKEY#";

  public DynamoDbUserStore(
    IOptionsMonitor<DynamoDbOptions> optionsMonitor,
    IAmazonDynamoDB? database = default)
  {
    ArgumentNullException.ThrowIfNull(optionsMonitor);

    var options = optionsMonitor.CurrentValue;

    if (options.Database == default && database == default)
    {
      throw new ArgumentNullException(nameof(database));
    }

    _client = database ?? options.Database!;
    _optionsMonitor = optionsMonitor;
  }

  public async Task AddClaimsAsync(TUserEntity user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    if (claims.Any() == false)
    {
      return;
    }

    if (user.Claims == default)
    {
      var rawClaims = await GetRawClaims(user, cancellationToken);
      user.Claims = DynamoDbUserStore<TUserEntity>.ToDictionary(rawClaims);
    }

    DynamoDbUserStore<TUserEntity>.AddClaims(user, claims);
  }

  private static void AddClaims(TUserEntity user, IEnumerable<Claim> claims)
  {
    user.Claims ??= new();

    foreach (var claim in claims)
    {
      if (user.Claims.TryGetValue(claim.Type, out var value))
      {
        value.Add(claim.Value);
      }
      else
      {
        user.Claims.Add(claim.Type, new() { claim.Value });
      }
    }
  }

  public async Task AddLoginAsync(TUserEntity user, UserLoginInfo login, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);
    ArgumentNullException.ThrowIfNull(login);

    user.Logins = await GetRawLogins(user, cancellationToken);

    user.Logins.Add(new DynamoDbUserLogin
    {
      LoginProvider = login.LoginProvider,
      ProviderKey = login.ProviderKey,
      ProviderDisplayName = login.ProviderDisplayName,
      UserId = user.Id,
    });
  }

  public async Task AddToRoleAsync(TUserEntity user, string roleName, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);
    ArgumentNullException.ThrowIfNull(roleName);

    user.Roles ??= (await GetRawRoles(user, cancellationToken))
      .Select(x => x.RoleName!)
      .ToList();

    if (user.Roles.Contains(roleName) == false)
    {
      user.Roles.Add(roleName);
    }
  }

  /// <summary>The lookup and membership items a user's own attributes give: its username and its email.</summary>
  internal static IEnumerable<IDynamoDbItem> LookupsOf(DynamoDbUser user)
  {
    if (user.NormalizedUserName is { } userName)
    {
      yield return new LookupItem(Lookups.UserName(userName), user.Id);
    }

    if (user.NormalizedEmail is { } email)
    {
      yield return new MembershipItem(Lookups.Email(email), user.Id);
    }
  }

  // The user and the lookup of its username are written together, so a username that another
  // user holds fails the whole write. Claims, logins, roles, tokens and passkeys follow, each
  // login and passkey together with its own lookup.
  public async Task<IdentityResult> CreateAsync(TUserEntity user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    cancellationToken.ThrowIfCancellationRequested();

    var items = new List<TransactWriteItem>
    {
      DynamoDbItems.TransactPut(_tableName, user, "attribute_not_exists(PartitionKey)"),
    };
    var userNameIndex = -1;

    if (user.NormalizedUserName is { } userName)
    {
      userNameIndex = items.Count;
      items.Add(DynamoDbItems.TransactPut(
        _tableName, new LookupItem(Lookups.UserName(userName), user.Id), "attribute_not_exists(PartitionKey)"));
    }

    if (user.NormalizedEmail is { } email)
    {
      items.Add(DynamoDbItems.TransactPut(_tableName, new MembershipItem(Lookups.Email(email), user.Id)));
    }

    try
    {
      await DynamoDbItems.TransactWriteAsync(_client, items, cancellationToken);
    }
    catch (TransactionCanceledException exception)
    {
      return IdentityResult.Failed(DynamoDbItems.FailedConditions(exception).Contains(userNameIndex)
        ? Errors.DuplicateUserName(user.UserName ?? user.NormalizedUserName!)
        : new IdentityError { Code = "DuplicateUserId", Description = $"A user with the id {user.Id} already exists" });
    }

    return await SaveRelatedItems(user, cancellationToken);
  }

  private async Task<IdentityResult> SaveRelatedItems(TUserEntity user, CancellationToken cancellationToken)
  {
    try
    {
      await SaveClaims(user, cancellationToken);
      await SaveLogins(user, cancellationToken);
      await SaveRoles(user, cancellationToken);
      await SaveTokens(user, cancellationToken);
      await SavePasskeys(user, cancellationToken);
    }
    catch (LookupConflictException exception)
    {
      return IdentityResult.Failed(exception.Error);
    }

    return IdentityResult.Success;
  }

  // Removes everything in the user's partition and the lookups that point into it, the user
  // item last so that a delete that fails halfway can be retried
  public async Task<IdentityResult> DeleteAsync(TUserEntity user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    var items = new List<Dictionary<string, AttributeValue>>();
    Dictionary<string, AttributeValue>? exclusiveStartKey = default;

    do
    {
      var query = await _client.QueryAsync(new()
      {
        TableName = _tableName,
        KeyConditionExpression = "PartitionKey = :partitionKey",
        ExpressionAttributeValues = new()
        {
          { ":partitionKey", new(user.PartitionKey) },
        },
        ExclusiveStartKey = exclusiveStartKey,
        ConsistentRead = true,
      }, cancellationToken);

      items.AddRange(query.Items ?? new());
      exclusiveStartKey = query.LastEvaluatedKey;
    } while (exclusiveStartKey?.Count > 0);

    var requests = new List<WriteRequest>();

    foreach (var item in items.OrderBy(x => x["SortKey"].S == user.SortKey))
    {
      requests.AddRange(LookupsOf(item).Select(x => new WriteRequest(new DeleteRequest(DynamoDbItems.Key(x)))));
      requests.Add(new WriteRequest(new DeleteRequest(DynamoDbItems.Key(item["PartitionKey"].S, item["SortKey"].S))));
    }

    await DynamoDbUtils.BatchWriteAsync(_client, _tableName, requests, cancellationToken);

    return IdentityResult.Success;
  }

  // The lookup and membership items of one item in a user's partition
  private static IEnumerable<IDynamoDbItem> LookupsOf(Dictionary<string, AttributeValue> item)
  {
    var sortKey = item.GetString("SortKey") ?? "";

    if (sortKey.StartsWith("#USER#", StringComparison.Ordinal))
    {
      return LookupsOf(DynamoDbItems.FromItem<DynamoDbUser>(item));
    }

    if (sortKey.StartsWith(ClaimSortKeyPrefix, StringComparison.Ordinal))
    {
      return LookupsOf(DynamoDbItems.FromItem<DynamoDbUserClaim>(item));
    }

    if (sortKey.StartsWith(LoginSortKeyPrefix, StringComparison.Ordinal))
    {
      return LookupsOf(DynamoDbItems.FromItem<DynamoDbUserLogin>(item));
    }

    if (sortKey.StartsWith(RoleSortKeyPrefix, StringComparison.Ordinal))
    {
      return LookupsOf(DynamoDbItems.FromItem<DynamoDbUserRole>(item));
    }

    if (sortKey.StartsWith(PasskeySortKeyPrefix, StringComparison.Ordinal))
    {
      return LookupsOf(DynamoDbItems.FromItem<DynamoDbUserPasskey>(item));
    }

    return [];
  }

  private static IEnumerable<IDynamoDbItem> LookupsOf(DynamoDbUserClaim claim)
  {
    if (claim is { UserId: not null, ClaimType: not null, ClaimValue: not null })
    {
      yield return new MembershipItem(Lookups.ClaimMembers(claim.ClaimType, claim.ClaimValue), claim.UserId);
    }
  }

  private static IEnumerable<IDynamoDbItem> LookupsOf(DynamoDbUserLogin login)
  {
    if (login is { UserId: not null, LoginProvider: not null, ProviderKey: not null })
    {
      yield return new LookupItem(Lookups.Login(login.LoginProvider, login.ProviderKey), login.UserId);
    }
  }

  private static IEnumerable<IDynamoDbItem> LookupsOf(DynamoDbUserRole role)
  {
    if (role is { UserId: not null, RoleName: not null })
    {
      yield return new MembershipItem(Lookups.RoleMembers(role.RoleName), role.UserId);
    }
  }

  private static IEnumerable<IDynamoDbItem> LookupsOf(DynamoDbUserPasskey passkey)
  {
    if (passkey is { UserId: not null, CredentialId: not null })
    {
      yield return new LookupItem(Lookups.Passkey(passkey.CredentialId), passkey.UserId);
    }
  }

  private Task<TUserEntity?> GetUser(string userId, CancellationToken cancellationToken)
  {
    var user = new DynamoDbUser
    {
      Id = userId,
    };
    return DynamoDbItems.GetAsync<TUserEntity>(_client, _tableName, user.PartitionKey, user.SortKey, cancellationToken, consistentRead: true);
  }

  // The user a lookup item points at, when the value is taken
  private async Task<TUserEntity?> FindByLookup(string partitionKey, CancellationToken cancellationToken)
  {
    var lookup = await DynamoDbItems.GetAsync<LookupItem>(
      _client, _tableName, partitionKey, Lookups.SortKey, cancellationToken, consistentRead: true);

    return lookup is null ? default : await GetUser(lookup.Id, cancellationToken);
  }

  // Users may share an email, so the first of them is returned, as the index returned one of them
  public async Task<TUserEntity?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(normalizedEmail);

    var member = await DynamoDbItems.QueryFirstAsync<MembershipItem>(_client, new QueryRequest
    {
      TableName = _tableName,
      KeyConditionExpression = "PartitionKey = :partitionKey",
      ExpressionAttributeValues = new()
      {
        { ":partitionKey", new AttributeValue { S = Lookups.Email(normalizedEmail) } },
      },
      ConsistentRead = true,
    }, cancellationToken);

    return member is null ? default : await GetUser(member.UserId, cancellationToken);
  }

  public async Task<TUserEntity?> FindByIdAsync(string userId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(userId);

    return await GetUser(userId, cancellationToken);
  }

  public async Task<TUserEntity?> FindByLoginAsync(
    string loginProvider, string providerKey, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(loginProvider);
    ArgumentNullException.ThrowIfNull(providerKey);

    return await FindByLookup(Lookups.Login(loginProvider, providerKey), cancellationToken);
  }

  public async Task<TUserEntity?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(normalizedUserName);

    return await FindByLookup(Lookups.UserName(normalizedUserName), cancellationToken);
  }

  public Task<int> GetAccessFailedCountAsync(TUserEntity user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    return Task.FromResult(user.AccessFailedCount);
  }

  // The items of one kind in the user's partition: claims, logins, roles, tokens or passkeys
  private Task<List<T>> GetRawItems<T>(TUserEntity user, string sortKeyPrefix, CancellationToken cancellationToken)
    where T : IDynamoDbItem, new()
    => DynamoDbItems.QueryPartitionAsync<T>(_client, _tableName, user.PartitionKey, sortKeyPrefix, cancellationToken, consistentRead: true);

  private Task<List<DynamoDbUserClaim>> GetRawClaims(TUserEntity user, CancellationToken cancellationToken)
    => GetRawItems<DynamoDbUserClaim>(user, ClaimSortKeyPrefix, cancellationToken);

  private static Dictionary<string, List<string>> ToDictionary(List<DynamoDbUserClaim> claims) => claims
    .GroupBy(x => x.ClaimType)
    .ToDictionary(x => x.Key!, x => x.Select(y => y.ClaimValue!).ToList());

  public async Task<IList<Claim>> GetClaimsAsync(TUserEntity user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    if (user.Claims == default)
    {
      var claims = await GetRawClaims(user, cancellationToken);
      user.Claims = DynamoDbUserStore<TUserEntity>.ToDictionary(claims);
    }

    return DynamoDbUserStore<TUserEntity>.FlattenClaims(user)
      .Select(x => new Claim(x.Key, x.Value))
      .ToList();
  }

  public Task<string?> GetEmailAsync(TUserEntity user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    return Task.FromResult(user.Email);
  }

  public Task<bool> GetEmailConfirmedAsync(TUserEntity user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    return Task.FromResult(user.EmailConfirmed);
  }

  public Task<bool> GetLockoutEnabledAsync(TUserEntity user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    return Task.FromResult(user.LockoutEnabled);
  }

  public Task<DateTimeOffset?> GetLockoutEndDateAsync(TUserEntity user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    return Task.FromResult(user.LockoutEnd);
  }

  public Task<List<DynamoDbUserLogin>> GetRawLogins(TUserEntity user, CancellationToken cancellationToken)
    => GetRawItems<DynamoDbUserLogin>(user, LoginSortKeyPrefix, cancellationToken);

  public async Task<IList<UserLoginInfo>> GetLoginsAsync(
    TUserEntity user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    if (user.Logins == default)
    {
      user.Logins = await GetRawLogins(user, cancellationToken);
    }

    return user.Logins
      .Where(x => x.LoginProvider != default)
      .Where(x => x.ProviderKey != default)
      .Select(x => new UserLoginInfo(
        x.LoginProvider!, x.ProviderKey!, x.ProviderDisplayName))
      .ToList();
  }

  public Task<string?> GetNormalizedEmailAsync(TUserEntity user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    return Task.FromResult(user.NormalizedEmail);
  }

  public Task<string?> GetNormalizedUserNameAsync(TUserEntity user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    return Task.FromResult(user.NormalizedUserName);
  }

  public Task<string?> GetPasswordHashAsync(TUserEntity user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    return Task.FromResult(user.PasswordHash);
  }

  public Task<string?> GetPhoneNumberAsync(TUserEntity user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    return Task.FromResult(user.PhoneNumber);
  }

  public Task<bool> GetPhoneNumberConfirmedAsync(TUserEntity user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    return Task.FromResult(user.PhoneNumberConfirmed);
  }

  public Task<List<DynamoDbUserRole>> GetRawRoles(TUserEntity user, CancellationToken cancellationToken)
    => GetRawItems<DynamoDbUserRole>(user, RoleSortKeyPrefix, cancellationToken);

  public async Task<IList<string>> GetRolesAsync(TUserEntity user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    if (user.Roles == default)
    {
      var roles = await GetRawRoles(user, cancellationToken);
      user.Roles = roles.Select(x => x.RoleName!).ToList();
    }

    if (_optionsMonitor.CurrentValue.ResolveRoleNames == false)
    {
      return user.Roles;
    }

    // The user keeps the normalized names, since that is what the other role methods are given
    var roleNames = await Task.WhenAll(user.Roles.Select(x => GetRoleName(x, cancellationToken)));
    return roleNames.ToList();
  }

  // The role's name is kept on its name lookup, so resolving it is one read
  private async Task<string> GetRoleName(string normalizedRoleName, CancellationToken cancellationToken)
  {
    var lookup = await DynamoDbItems.GetAsync<LookupItem>(
      _client, _tableName, Lookups.RoleName(normalizedRoleName), Lookups.SortKey, cancellationToken, consistentRead: true);

    return string.IsNullOrEmpty(lookup?.Name) ? normalizedRoleName : lookup.Name;
  }

  public Task<string?> GetSecurityStampAsync(TUserEntity user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    return Task.FromResult(user.SecurityStamp);
  }

  public Task<bool> GetTwoFactorEnabledAsync(TUserEntity user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    return Task.FromResult(user.TwoFactorEnabled);
  }

  public Task<string> GetUserIdAsync(TUserEntity user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    return Task.FromResult(user.Id);
  }

  public Task<string?> GetUserNameAsync(TUserEntity user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    return Task.FromResult(user.UserName);
  }

  public async Task<IList<TUserEntity>> GetUsersForClaimAsync(Claim claim, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(claim);

    return await GetMembers(Lookups.ClaimMembers(claim.Type, claim.Value), cancellationToken);
  }

  public async Task<IList<TUserEntity>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(roleName);

    return await GetMembers(Lookups.RoleMembers(roleName), cancellationToken);
  }

  // The users listed in a membership partition
  private async Task<IList<TUserEntity>> GetMembers(string partitionKey, CancellationToken cancellationToken)
  {
    var members = await DynamoDbItems.QueryPartitionAsync<MembershipItem>(
      _client, _tableName, partitionKey, default, cancellationToken, consistentRead: true);
    var keys = members
      .Select(x => x.UserId)
      .Distinct()
      .Select(x => new DynamoDbUser { Id = x })
      .Select(DynamoDbItems.Key);

    return await DynamoDbItems.BatchGetAsync<TUserEntity>(_client, _tableName, keys, cancellationToken);
  }

  public Task<bool> HasPasswordAsync(TUserEntity user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    return Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));
  }

  public Task<int> IncrementAccessFailedCountAsync(TUserEntity user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    user.AccessFailedCount++;

    return Task.FromResult(user.AccessFailedCount);
  }

  public async Task<bool> IsInRoleAsync(TUserEntity user, string roleName, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    var userRole = new DynamoDbUserRole
    {
      UserId = user.Id,
      RoleName = roleName,
    };
    var role = await DynamoDbItems.GetAsync<DynamoDbUserRole>(
      _client, _tableName, userRole.PartitionKey, userRole.SortKey, cancellationToken, consistentRead: true);

    return role != default;
  }

  public async Task RemoveClaimsAsync(TUserEntity user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);
    ArgumentNullException.ThrowIfNull(claims);

    user.Claims ??= DynamoDbUserStore<TUserEntity>.ToDictionary(await GetRawClaims(user, cancellationToken));

    foreach (var claim in claims)
    {
      if (user.Claims.ContainsKey(claim.Type))
      {
        user.Claims[claim.Type].Remove(claim.Value);

        if (user.Claims[claim.Type].Count == 0)
        {
          user.Claims.Remove(claim.Type);
        }
      }
    }
  }

  public async Task RemoveFromRoleAsync(TUserEntity user, string roleName, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);
    ArgumentNullException.ThrowIfNull(roleName);

    var roles = user.Roles ?? await GetRolesAsync(user, cancellationToken);
    user.Roles = roles.Except(new List<string> { roleName }).ToList();
  }

  public async Task RemoveLoginAsync(TUserEntity user, string loginProvider, string providerKey, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);
    ArgumentNullException.ThrowIfNull(loginProvider);
    ArgumentNullException.ThrowIfNull(providerKey);

    var logins = user.Logins ?? await GetRawLogins(user, cancellationToken);
    user.Logins = logins.Except(logins
      .Where(x => x.LoginProvider == loginProvider)
      .Where(x => x.ProviderKey == providerKey))
      .ToList();
  }

  public async Task ReplaceClaimAsync(TUserEntity user, Claim claim, Claim newClaim, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);
    ArgumentNullException.ThrowIfNull(claim);
    ArgumentNullException.ThrowIfNull(newClaim);

    await RemoveClaimsAsync(user, new List<Claim> { claim }, cancellationToken);
    DynamoDbUserStore<TUserEntity>.AddClaims(user, new List<Claim> { newClaim });
  }

  public Task ResetAccessFailedCountAsync(TUserEntity user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);
    user.AccessFailedCount = 0;
    return Task.FromResult(user.AccessFailedCount);
  }

  public Task SetEmailAsync(TUserEntity user, string? email, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);
    user.Email = email;
    return Task.CompletedTask;
  }

  public Task SetEmailConfirmedAsync(TUserEntity user, bool confirmed, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);
    user.EmailConfirmed = confirmed;
    return Task.CompletedTask;
  }

  public Task SetLockoutEnabledAsync(TUserEntity user, bool enabled, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);
    user.LockoutEnabled = enabled;
    return Task.CompletedTask;
  }

  public Task SetLockoutEndDateAsync(TUserEntity user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);
    user.LockoutEnd = lockoutEnd.HasValue ? lockoutEnd.Value.UtcDateTime : default;
    return Task.CompletedTask;
  }

  public Task SetNormalizedEmailAsync(TUserEntity user, string? normalizedEmail, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);
    user.NormalizedEmail = normalizedEmail;
    return Task.CompletedTask;
  }

  public Task SetNormalizedUserNameAsync(TUserEntity user, string? normalizedName, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);
    user.NormalizedUserName = normalizedName;
    return Task.CompletedTask;
  }

  public Task SetPasswordHashAsync(TUserEntity user, string? passwordHash, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);
    user.PasswordHash = passwordHash;
    return Task.CompletedTask;
  }

  public Task SetPhoneNumberAsync(TUserEntity user, string? phoneNumber, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);
    user.PhoneNumber = phoneNumber;
    return Task.CompletedTask;
  }

  public Task SetPhoneNumberConfirmedAsync(TUserEntity user, bool confirmed, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);
    user.PhoneNumberConfirmed = confirmed;
    return Task.CompletedTask;
  }

  public Task SetSecurityStampAsync(TUserEntity user, string stamp, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);
    user.SecurityStamp = stamp;
    return Task.CompletedTask;
  }

  public Task SetTwoFactorEnabledAsync(TUserEntity user, bool enabled, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);
    user.TwoFactorEnabled = enabled;
    return Task.CompletedTask;
  }

  public Task SetUserNameAsync(TUserEntity user, string? userName, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);
    user.UserName = userName;
    return Task.CompletedTask;
  }

  // The user is written only when it still has the concurrency stamp it was loaded with, and the
  // lookups of its username and email move with changed values in the same transaction
  public async Task<IdentityResult> UpdateAsync(TUserEntity user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    var stored = await GetUser(user.Id, cancellationToken);

    if (stored is null)
    {
      return IdentityResult.Failed(Errors.ConcurrencyFailure());
    }

    var concurrencyStamp = user.ConcurrencyStamp;
    user.ConcurrencyStamp = Guid.NewGuid().ToString();

    var items = new List<TransactWriteItem>
    {
      DynamoDbItems.TransactPut(
        _tableName,
        user,
        concurrencyStamp is null
          ? "attribute_exists(PartitionKey) and attribute_not_exists(ConcurrencyStamp)"
          : "attribute_exists(PartitionKey) and ConcurrencyStamp = :concurrencyStamp",
        concurrencyStamp is null ? default : new() { { ":concurrencyStamp", new AttributeValue { S = concurrencyStamp } } }),
    };
    var userNameIndex = -1;

    if (stored.NormalizedUserName != user.NormalizedUserName)
    {
      if (stored.NormalizedUserName is { } previousUserName)
      {
        items.Add(DeleteLookup(Lookups.UserName(previousUserName), user.Id));
      }

      if (user.NormalizedUserName is { } userName)
      {
        userNameIndex = items.Count;
        items.Add(PutLookup(new LookupItem(Lookups.UserName(userName), user.Id), user.Id));
      }
    }

    if (stored.NormalizedEmail != user.NormalizedEmail)
    {
      if (stored.NormalizedEmail is { } previousEmail)
      {
        items.Add(DynamoDbItems.TransactDelete(_tableName, Lookups.Email(previousEmail), Lookups.Member(user.Id)));
      }

      if (user.NormalizedEmail is { } email)
      {
        items.Add(DynamoDbItems.TransactPut(_tableName, new MembershipItem(Lookups.Email(email), user.Id)));
      }
    }

    try
    {
      await DynamoDbItems.TransactWriteAsync(_client, items, cancellationToken);
    }
    catch (TransactionCanceledException exception)
    {
      user.ConcurrencyStamp = concurrencyStamp;
      var failed = DynamoDbItems.FailedConditions(exception);

      return IdentityResult.Failed(failed.Contains(userNameIndex) && !failed.Contains(0)
        ? Errors.DuplicateUserName(user.UserName ?? user.NormalizedUserName!)
        : Errors.ConcurrencyFailure());
    }

    return await SaveRelatedItems(user, cancellationToken);
  }

  // Writes a lookup that is free or already points at the user
  private TransactWriteItem PutLookup(LookupItem lookup, string userId) => DynamoDbItems.TransactPut(
    _tableName,
    lookup,
    "attribute_not_exists(PartitionKey) or Id = :id",
    new() { { ":id", new AttributeValue { S = userId } } });

  // Removes a lookup that points at the user, or is already gone
  private TransactWriteItem DeleteLookup(string partitionKey, string userId) => DynamoDbItems.TransactDelete(
    _tableName,
    partitionKey,
    Lookups.SortKey,
    "attribute_not_exists(PartitionKey) or Id = :id",
    new() { { ":id", new AttributeValue { S = userId } } });

  private static List<KeyValuePair<string, string>> FlattenClaims(TUserEntity user) => user.Claims
      ?.SelectMany(x => x.Value.Select(y => new KeyValuePair<string, string>(x.Key, y)))
      .ToList() ?? new();

  // Deletes the persisted items of one kind that the user no longer holds, with their lookups
  private async Task RemoveDeletedItems<T>(
    IEnumerable<T> persisted, IEnumerable<T> current, Func<T, IEnumerable<IDynamoDbItem>> lookupsOf, CancellationToken cancellationToken)
    where T : IDynamoDbItem
  {
    var currentSortKeys = current.Select(x => x.SortKey).ToHashSet();
    var toBeDeleted = persisted
      .Where(x => currentSortKeys.Contains(x.SortKey) == false)
      .SelectMany(x => lookupsOf(x).Append(x))
      .Select(DynamoDbItems.Delete);

    await DynamoDbUtils.BatchWriteAsync(_client, _tableName, toBeDeleted, cancellationToken);
  }

  // Writes items of one kind with their membership items, which need no condition
  private Task PutItems<T>(IEnumerable<T> items, Func<T, IEnumerable<IDynamoDbItem>> lookupsOf, CancellationToken cancellationToken)
    where T : IDynamoDbItem
    => DynamoDbUtils.BatchWriteAsync(
      _client, _tableName, items.SelectMany(x => lookupsOf(x).Append(x)).Select(DynamoDbItems.Put), cancellationToken);

  // Writes an item together with its lookup, which must be free or already point at the user,
  // so that a login or passkey can belong to one user only
  private async Task PutWithLookups<T>(T item, Func<T, IEnumerable<IDynamoDbItem>> lookupsOf, string userId, IdentityError conflict, CancellationToken cancellationToken)
    where T : IDynamoDbItem
  {
    var items = new List<TransactWriteItem> { DynamoDbItems.TransactPut(_tableName, item) };
    items.AddRange(lookupsOf(item).Select(x => PutLookup((LookupItem)x, userId)));

    try
    {
      await DynamoDbItems.TransactWriteAsync(_client, items, cancellationToken);
    }
    catch (TransactionCanceledException exception) when (DynamoDbItems.FailedConditions(exception).Count > 0)
    {
      throw new LookupConflictException(conflict);
    }
  }

  private static IEnumerable<DynamoDbUserClaim> CurrentClaims(TUserEntity user) => DynamoDbUserStore<TUserEntity>
    .FlattenClaims(user)
    .Select(x => new DynamoDbUserClaim
    {
      ClaimType = x.Key,
      ClaimValue = x.Value,
      UserId = user.Id,
    });

  public async Task RemoveDeletedClaims(TUserEntity user, CancellationToken cancellationToken)
  {
    if (user.Claims == default)
    {
      return;
    }

    var persistedClaims = await GetRawClaims(user, cancellationToken);
    await RemoveDeletedItems(persistedClaims, CurrentClaims(user), LookupsOf, cancellationToken);
  }

  public async Task SaveClaims(TUserEntity user, CancellationToken cancellationToken)
  {
    await RemoveDeletedClaims(user, cancellationToken);

    if (user.Claims == default)
    {
      return;
    }

    await PutItems(CurrentClaims(user), LookupsOf, cancellationToken);
  }

  public async Task RemoveDeletedLogins(TUserEntity user, CancellationToken cancellationToken)
  {
    if (user.Logins == default)
    {
      return;
    }

    var persistedLogins = await GetRawLogins(user, cancellationToken);
    await RemoveDeletedItems(persistedLogins, user.Logins, LookupsOf, cancellationToken);
  }

  public async Task SaveLogins(TUserEntity user, CancellationToken cancellationToken)
  {
    await RemoveDeletedLogins(user, cancellationToken);

    if (user.Logins == default)
    {
      return;
    }

    foreach (var login in user.Logins)
    {
      login.UserId = user.Id;
      await PutWithLookups(login, LookupsOf, user.Id, Errors.LoginAlreadyAssociated(), cancellationToken);
    }
  }

  private static IEnumerable<DynamoDbUserRole> CurrentRoles(TUserEntity user) => (user.Roles ?? new())
    .Select(x => new DynamoDbUserRole
    {
      RoleName = x,
      UserId = user.Id,
    });

  public async Task RemoveDeletedRoles(TUserEntity user, CancellationToken cancellationToken)
  {
    if (user.Roles == default)
    {
      return;
    }

    var persistedRoles = await GetRawRoles(user, cancellationToken);
    await RemoveDeletedItems(persistedRoles, CurrentRoles(user), LookupsOf, cancellationToken);
  }

  public async Task SaveRoles(TUserEntity user, CancellationToken cancellationToken)
  {
    await RemoveDeletedRoles(user, cancellationToken);

    if (user.Roles == default)
    {
      return;
    }

    await PutItems(CurrentRoles(user), LookupsOf, cancellationToken);
  }

  private static IEnumerable<DynamoDbUserToken> CurrentTokens(TUserEntity user) => (user.Tokens ?? new())
    .Select(x => new DynamoDbUserToken
    {
      UserId = user.Id,
      LoginProvider = x.LoginProvider,
      Name = x.Name,
      Value = x.Value,
    });

  public async Task RemoveDeletedTokens(TUserEntity user, CancellationToken cancellationToken)
  {
    if (user.Tokens == default)
    {
      return;
    }

    var persistedTokens = await GetRawTokens(user, cancellationToken);
    await RemoveDeletedItems(persistedTokens, CurrentTokens(user), _ => [], cancellationToken);
  }

  public async Task SaveTokens(TUserEntity user, CancellationToken cancellationToken)
  {
    await RemoveDeletedTokens(user, cancellationToken);

    if (user.Tokens == default)
    {
      return;
    }

    await PutItems(CurrentTokens(user), _ => [], cancellationToken);
  }

  public Task SetAuthenticatorKeyAsync(TUserEntity user, string key, CancellationToken cancellationToken)
    => SetTokenAsync(user, InternalLoginProvider, AuthenticatorKeyTokenName, key, cancellationToken);

  public Task<string?> GetAuthenticatorKeyAsync(TUserEntity user, CancellationToken cancellationToken)
    => GetTokenAsync(user, InternalLoginProvider, AuthenticatorKeyTokenName, cancellationToken);

  public async Task RemoveTokenAsync(TUserEntity user, string loginProvider, string name, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);
    ArgumentNullException.ThrowIfNull(loginProvider);
    ArgumentNullException.ThrowIfNull(name);

    var entry = await FindTokenAsync(user, loginProvider, name, cancellationToken);
    if (entry != null)
    {
      user.Tokens?.RemoveAll(x => x.LoginProvider == entry.LoginProvider && x.Name == entry.Name);
    }
  }

  public async Task<string?> GetTokenAsync(TUserEntity user, string loginProvider, string name, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);
    ArgumentNullException.ThrowIfNull(loginProvider);
    ArgumentNullException.ThrowIfNull(name);

    var token = await FindTokenAsync(user, loginProvider, name, cancellationToken);
    return token?.Value;
  }

  public Task ReplaceCodesAsync(TUserEntity user, IEnumerable<string> recoveryCodes, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    var mergedCodes = string.Join(";", recoveryCodes);
    return SetTokenAsync(user, InternalLoginProvider, RecoveryCodeTokenName, mergedCodes, cancellationToken);
  }

  public async Task<bool> RedeemCodeAsync(TUserEntity user, string code, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);
    ArgumentNullException.ThrowIfNull(code);

    var mergedCodes = await GetTokenAsync(user, InternalLoginProvider, RecoveryCodeTokenName, cancellationToken) ?? "";
    var splitCodes = mergedCodes.Split(';');
    if (splitCodes.Contains(code))
    {
      var updatedCodes = new List<string>(splitCodes.Where(s => s != code));
      await ReplaceCodesAsync(user, updatedCodes, cancellationToken);

      return true;
    }

    return false;
  }

  public async Task<int> CountCodesAsync(TUserEntity user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    var mergedCodes = await GetTokenAsync(user, InternalLoginProvider, RecoveryCodeTokenName, cancellationToken) ?? "";
    if (mergedCodes.Length > 0)
    {
      return mergedCodes.Split(';').Length;
    }
    return 0;
  }

  public async Task SetTokenAsync(TUserEntity user, string loginProvider, string name, string? value, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(user);

    var token = await FindTokenAsync(user, loginProvider, name, cancellationToken);
    if (token == null)
    {
      user.Tokens?.Add(new IdentityUserToken<string>
      {
        UserId = user.Id.ToString(),
        LoginProvider = loginProvider,
        Name = name,
        Value = value
      });
    }
    else
    {
      token.Value = value;

      var idx = user.Tokens?.FindIndex(x => x.LoginProvider == token.LoginProvider && x.Name == token.Name);

      if (!idx.HasValue || user.Tokens == default)
      {
        return;
      }

      user.Tokens[idx.Value] = token;
    }
  }

  private Task<List<DynamoDbUserToken>> GetRawTokens(TUserEntity user, CancellationToken cancellationToken)
    => GetRawItems<DynamoDbUserToken>(user, TokenSortKeyPrefix, cancellationToken);

  private async Task<IdentityUserToken<string>?> FindTokenAsync(TUserEntity user, string loginProvider, string name, CancellationToken cancellationToken)
  {
    if (user.Tokens == default)
    {
      var tokens = await GetRawTokens(user, cancellationToken);

      user.Tokens = [.. tokens
        .Where(x => x.LoginProvider != default)
        .Where(x => x.Name != default)
        .Select(x => new IdentityUserToken<string>
        {
          LoginProvider = x.LoginProvider!,
          Name = x.Name!,
          UserId = x.UserId!,
          Value = x.Value,
        })];
    }

    return user.Tokens!.FirstOrDefault(x => x.LoginProvider == loginProvider && x.Name == name);
  }

#if NET10_0_OR_GREATER
  public async Task AddOrUpdatePasskeyAsync(TUserEntity user, UserPasskeyInfo passkey, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);
    ArgumentNullException.ThrowIfNull(passkey);

    user.Passkeys ??= await GetRawPasskeys(user, cancellationToken);

    var credentialId = Base64UrlEncoding.Encode(passkey.CredentialId);
    var existing = user.Passkeys.FirstOrDefault(x => x.CredentialId == credentialId);

    if (existing == default)
    {
      user.Passkeys.Add(ToDynamoDbUserPasskey(user, passkey));
      return;
    }

    existing.Name = passkey.Name;
    existing.SignCount = passkey.SignCount;
    existing.IsBackedUp = passkey.IsBackedUp;
    existing.IsUserVerified = passkey.IsUserVerified;
  }

  public async Task<IList<UserPasskeyInfo>> GetPasskeysAsync(TUserEntity user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    user.Passkeys ??= await GetRawPasskeys(user, cancellationToken);

    return user.Passkeys
      .Select(ToUserPasskeyInfo)
      .ToList();
  }

  public async Task<TUserEntity?> FindByPasskeyIdAsync(byte[] credentialId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(credentialId);

    return await FindByLookup(Lookups.Passkey(Base64UrlEncoding.Encode(credentialId)), cancellationToken);
  }

  public async Task<UserPasskeyInfo?> FindPasskeyAsync(TUserEntity user, byte[] credentialId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);
    ArgumentNullException.ThrowIfNull(credentialId);

    user.Passkeys ??= await GetRawPasskeys(user, cancellationToken);

    var encodedCredentialId = Base64UrlEncoding.Encode(credentialId);
    var passkey = user.Passkeys.FirstOrDefault(x => x.CredentialId == encodedCredentialId);

    return passkey == default ? default : ToUserPasskeyInfo(passkey);
  }

  public async Task RemovePasskeyAsync(TUserEntity user, byte[] credentialId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);
    ArgumentNullException.ThrowIfNull(credentialId);

    user.Passkeys ??= await GetRawPasskeys(user, cancellationToken);

    var encodedCredentialId = Base64UrlEncoding.Encode(credentialId);
    user.Passkeys.RemoveAll(x => x.CredentialId == encodedCredentialId);
  }

  private static DynamoDbUserPasskey ToDynamoDbUserPasskey(TUserEntity user, UserPasskeyInfo passkey) => new()
  {
    UserId = user.Id,
    CredentialId = Base64UrlEncoding.Encode(passkey.CredentialId),
    PublicKey = Base64UrlEncoding.Encode(passkey.PublicKey),
    Name = passkey.Name,
    CreatedAt = passkey.CreatedAt,
    SignCount = passkey.SignCount,
    Transports = passkey.Transports?.ToList(),
    IsUserVerified = passkey.IsUserVerified,
    IsBackupEligible = passkey.IsBackupEligible,
    IsBackedUp = passkey.IsBackedUp,
    AttestationObject = Base64UrlEncoding.Encode(passkey.AttestationObject),
    ClientDataJson = Base64UrlEncoding.Encode(passkey.ClientDataJson),
  };

  private static UserPasskeyInfo ToUserPasskeyInfo(DynamoDbUserPasskey passkey) => new(
    Base64UrlEncoding.Decode(passkey.CredentialId!),
    Base64UrlEncoding.Decode(passkey.PublicKey!),
    passkey.CreatedAt,
    passkey.SignCount,
    passkey.Transports?.ToArray(),
    passkey.IsUserVerified,
    passkey.IsBackupEligible,
    passkey.IsBackedUp,
    Base64UrlEncoding.Decode(passkey.AttestationObject!),
    Base64UrlEncoding.Decode(passkey.ClientDataJson!))
  {
    Name = passkey.Name,
  };
#endif

  private Task<List<DynamoDbUserPasskey>> GetRawPasskeys(TUserEntity user, CancellationToken cancellationToken)
    => GetRawItems<DynamoDbUserPasskey>(user, PasskeySortKeyPrefix, cancellationToken);

  public async Task RemoveDeletedPasskeys(TUserEntity user, CancellationToken cancellationToken)
  {
    if (user.Passkeys == default)
    {
      return;
    }

    var persistedPasskeys = await GetRawPasskeys(user, cancellationToken);
    await RemoveDeletedItems(persistedPasskeys, user.Passkeys, LookupsOf, cancellationToken);
  }

  public async Task SavePasskeys(TUserEntity user, CancellationToken cancellationToken)
  {
    if (user.Passkeys == default)
    {
      return;
    }

    var persistedPasskeys = (await GetRawPasskeys(user, cancellationToken))
      .ToDictionary(x => x.SortKey);
    var currentSortKeys = user.Passkeys.Select(x => x.SortKey).ToHashSet();
    var toBeDeleted = persistedPasskeys.Values
      .Where(x => currentSortKeys.Contains(x.SortKey) == false)
      .SelectMany(x => LookupsOf(x).Append(x))
      .Select(DynamoDbItems.Delete);

    await DynamoDbUtils.BatchWriteAsync(_client, _tableName, toBeDeleted, cancellationToken);

    // Passkeys carry the attestation object and client data, so only write the ones that changed
    foreach (var passkey in user.Passkeys)
    {
      passkey.UserId = user.Id;

      if (persistedPasskeys.TryGetValue(passkey.SortKey, out var persistedPasskey) == false
        || IsEqual(persistedPasskey, passkey) == false)
      {
        await PutWithLookups(
          passkey,
          LookupsOf,
          user.Id,
          new IdentityError { Code = "DuplicatePasskey", Description = "The passkey is registered to another user" },
          cancellationToken);
      }
    }
  }

  private static bool IsEqual(DynamoDbUserPasskey left, DynamoDbUserPasskey right) =>
    left.UserId == right.UserId
    && left.CredentialId == right.CredentialId
    && left.PublicKey == right.PublicKey
    && left.Name == right.Name
    && left.CreatedAt == right.CreatedAt
    && left.SignCount == right.SignCount
    && (left.Transports == right.Transports
      || (left.Transports != default && right.Transports != default && left.Transports.SequenceEqual(right.Transports)))
    && left.IsUserVerified == right.IsUserVerified
    && left.IsBackupEligible == right.IsBackupEligible
    && left.IsBackedUp == right.IsBackedUp
    && left.AttestationObject == right.AttestationObject
    && left.ClientDataJson == right.ClientDataJson;

  protected virtual void Dispose(bool disposing)
  {
    if (disposing)
    {
    }
  }

  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  ~DynamoDbUserStore()
  {
    Dispose(false);
  }
}

/// <summary>A login or passkey that another user holds, which fails the write with the given Identity error.</summary>
internal sealed class LookupConflictException(IdentityError error) : Exception(error.Description)
{
  public IdentityError Error { get; } = error;
}
