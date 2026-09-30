using System.Security.Claims;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
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
  private readonly IAmazonDynamoDB _client;
  private readonly IDynamoDBContext _context;
  private readonly IOptionsMonitor<DynamoDbOptions> _optionsMonitor;
  private string _tableName =>
    _optionsMonitor.CurrentValue.DefaultTableName ?? Constants.DefaultTableName;
  private const string InternalLoginProvider = "[AspNetUserStore]";
  private const string AuthenticatorKeyTokenName = "AuthenticatorKey";
  private const string RecoveryCodeTokenName = "RecoveryCodes";
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
    _context = new DynamoDBContextBuilder()
      .WithDynamoDBClient(() => _client)
      .Build();
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

  public async Task<IdentityResult> CreateAsync(TUserEntity user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    cancellationToken.ThrowIfCancellationRequested();

    await _context.SaveAsync(user, GetSaveConfig(), cancellationToken);
    await SaveClaims(user, cancellationToken);
    await SaveLogins(user, cancellationToken);
    await SaveRoles(user, cancellationToken);
    await SaveTokens(user, cancellationToken);
    await SavePasskeys(user, cancellationToken);

    return IdentityResult.Success;
  }

  public async Task<IdentityResult> DeleteAsync(TUserEntity user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    var keys = new List<Dictionary<string, AttributeValue>>();
    Dictionary<string, AttributeValue>? exclusiveStartKey = default;

    do
    {
      var query = await _client.QueryAsync(new()
      {
        ProjectionExpression = "PartitionKey, SortKey",
        TableName = _tableName,
        KeyConditionExpression = "PartitionKey = :partitionKey",
        ExpressionAttributeValues = new()
        {
          { ":partitionKey", new(user.PartitionKey) },
        },
        ExclusiveStartKey = exclusiveStartKey,
        ConsistentRead = true,
      }, cancellationToken);

      keys.AddRange(query.Items ?? new());
      exclusiveStartKey = query.LastEvaluatedKey;
    } while (exclusiveStartKey?.Count > 0);

    // Delete the user item last, so that a delete that fails halfway can be retried
    var requests = keys
      .OrderBy(x => x["SortKey"].S == user.SortKey)
      .Select(x => new WriteRequest(new DeleteRequest(x)));

    await DynamoDbUtils.BatchWriteAsync(_client, _tableName, requests, cancellationToken);

    return IdentityResult.Success;
  }

  public async Task<TUserEntity?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(normalizedEmail);

#pragma warning disable CS0618 // Type or member is obsolete - Using DynamoDBOperationConfig is necessary for dynamic table name override via OverrideTableName
    var search = _context.FromQueryAsync<TUserEntity>(new QueryOperationConfig
    {
      IndexName = "NormalizedEmail-index",
      KeyExpression = new Expression
      {
        ExpressionStatement = "NormalizedEmail = :email",
        ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
        {
          { ":email", normalizedEmail },
        },
      },
      Limit = 1
    }, GetOperationConfig());
#pragma warning restore CS0618
    var users = await search.GetRemainingAsync(cancellationToken);
    return users.FirstOrDefault();
  }

  public async Task<TUserEntity?> FindByIdAsync(string userId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(userId);

    var user = new DynamoDbUser
    {
      Id = userId,
    };
    return await _context.LoadAsync<TUserEntity>(user.PartitionKey, user.SortKey, GetLoadConfig(), cancellationToken);
  }

  public async Task<TUserEntity?> FindByLoginAsync(
    string loginProvider, string providerKey, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(loginProvider);
    ArgumentNullException.ThrowIfNull(providerKey);

#pragma warning disable CS0618 // Type or member is obsolete - Using DynamoDBOperationConfig is necessary for dynamic table name override via OverrideTableName
    var search = _context.FromQueryAsync<DynamoDbUserLogin>(new QueryOperationConfig
    {
      IndexName = "LoginProvider-ProviderKey-index",
      KeyExpression = new Expression
      {
        ExpressionStatement = "LoginProvider = :loginProvider AND ProviderKey = :providerKey",
        ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
        {
          { ":loginProvider", loginProvider },
          { ":providerKey", providerKey },
        },
      },
      Limit = 1
    }, GetOperationConfig());
#pragma warning restore CS0618
    var logins = await search.GetNextSetAsync(cancellationToken);

    if (logins.Any() == false || logins.First().UserId == default)
    {
      return default;
    }

    var user = new DynamoDbUser
    {
      Id = logins.First().UserId!,
    };
    return await _context.LoadAsync<TUserEntity>(
      user.PartitionKey, user.SortKey, GetLoadConfig(), cancellationToken);
  }

  public async Task<TUserEntity?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(normalizedUserName);

#pragma warning disable CS0618 // Type or member is obsolete - Using DynamoDBOperationConfig is necessary for dynamic table name override via OverrideTableName
    var search = _context.FromQueryAsync<TUserEntity>(new QueryOperationConfig
    {
      IndexName = "NormalizedUserName-index",
      KeyExpression = new Expression
      {
        ExpressionStatement = "NormalizedUserName = :normalizedUserName",
        ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
        {
          { ":normalizedUserName", normalizedUserName },
        },
      },
      Limit = 1
    }, GetOperationConfig());
#pragma warning restore CS0618
    var users = await search.GetRemainingAsync(cancellationToken);
    return users.FirstOrDefault();
  }

  public Task<int> GetAccessFailedCountAsync(TUserEntity user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    return Task.FromResult(user.AccessFailedCount);
  }

  private async Task<List<DynamoDbUserClaim>> GetRawClaims(TUserEntity user, CancellationToken cancellationToken)
  {
#pragma warning disable CS0618 // Type or member is obsolete - Using DynamoDBOperationConfig is necessary for dynamic table name override via OverrideTableName
    var search = _context.FromQueryAsync<DynamoDbUserClaim>(new QueryOperationConfig
    {
      KeyExpression = new Expression
      {
        ExpressionStatement = "PartitionKey = :partitionKey and begins_with(SortKey, :sortKey)",
        ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
        {
          { ":partitionKey", user.PartitionKey },
          { ":sortKey", "CLAIM#" },
        },
      },
    }, GetOperationConfig());
#pragma warning restore CS0618
    return await search.GetRemainingAsync(cancellationToken);
  }

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

  public async Task<List<DynamoDbUserLogin>> GetRawLogins(TUserEntity user, CancellationToken cancellationToken)
  {
#pragma warning disable CS0618 // Type or member is obsolete - Using DynamoDBOperationConfig is necessary for dynamic table name override via OverrideTableName
    var search = _context.FromQueryAsync<DynamoDbUserLogin>(new QueryOperationConfig
    {
      KeyExpression = new Expression
      {
        ExpressionStatement = "PartitionKey = :partitionKey and begins_with(SortKey, :sortKey)",
        ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
        {
          { ":partitionKey", user.PartitionKey },
          { ":sortKey", "LOGIN#" },
        },
      },
    }, GetOperationConfig());
#pragma warning restore CS0618
    return await search.GetRemainingAsync(cancellationToken);
  }

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

  public async Task<List<DynamoDbUserRole>> GetRawRoles(TUserEntity user, CancellationToken cancellationToken)
  {
#pragma warning disable CS0618 // Type or member is obsolete - Using DynamoDBOperationConfig is necessary for dynamic table name override via OverrideTableName
    var search = _context.FromQueryAsync<DynamoDbUserRole>(new QueryOperationConfig
    {
      KeyExpression = new Expression
      {
        ExpressionStatement = "PartitionKey = :partitionKey and begins_with(SortKey, :sortKey)",
        ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
        {
          { ":partitionKey", $"USER#{user.Id}" },
          { ":sortKey", "ROLE#" },
        },
      },
    }, GetOperationConfig());
#pragma warning restore CS0618
    return await search.GetRemainingAsync(cancellationToken);
  }

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

  private async Task<string> GetRoleName(string normalizedRoleName, CancellationToken cancellationToken)
  {
    var response = await _client.QueryAsync(new QueryRequest
    {
      TableName = _tableName,
      IndexName = "NormalizedName-index",
      KeyConditionExpression = "NormalizedName = :normalizedName",
      ProjectionExpression = "#name",
      ExpressionAttributeNames = new()
      {
        { "#name", "Name" },
      },
      ExpressionAttributeValues = new()
      {
        { ":normalizedName", new(normalizedRoleName) },
      },
      Limit = 1,
    }, cancellationToken);

    var roleName = response.Items?.FirstOrDefault()?.GetValueOrDefault("Name")?.S;
    return string.IsNullOrEmpty(roleName) ? normalizedRoleName : roleName;
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

#pragma warning disable CS0618 // Type or member is obsolete - Using DynamoDBOperationConfig is necessary for dynamic table name override via OverrideTableName
    var search = _context.FromQueryAsync<DynamoDbUserClaim>(new QueryOperationConfig
    {
      IndexName = "ClaimType-ClaimValue-index",
      KeyExpression = new Expression
      {
        ExpressionStatement = "ClaimType = :claimType and ClaimValue = :claimValue",
        ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
        {
          { ":claimType", claim.Type },
          { ":claimValue", claim.Value },
        },
      },
    }, GetOperationConfig());
#pragma warning restore CS0618
    var userClaims = await search.GetRemainingAsync(cancellationToken);

    var batch = _context.CreateBatchGet<TUserEntity>(GetBatchGetConfig());
    foreach (var userId in userClaims.Where(x => x.UserId != default).Select(x => x.UserId).Distinct())
    {
      var user = new DynamoDbUser
      {
        Id = userId!,
      };
      batch.AddKey(user.PartitionKey, user.SortKey);
    }

    await batch.ExecuteAsync(cancellationToken);

    return batch.Results;
  }

  public async Task<IList<TUserEntity>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(roleName);

#pragma warning disable CS0618 // Type or member is obsolete - Using DynamoDBOperationConfig is necessary for dynamic table name override via OverrideTableName
    var search = _context.FromQueryAsync<DynamoDbUserRole>(new QueryOperationConfig
    {
      IndexName = "RoleName-index",
      KeyExpression = new Expression
      {
        ExpressionStatement = "RoleName = :roleName",
        ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
        {
          { ":roleName", roleName },
        },
      },
    }, GetOperationConfig());
#pragma warning restore CS0618
    var userRoles = await search.GetRemainingAsync(cancellationToken);

    var batch = _context.CreateBatchGet<TUserEntity>(GetBatchGetConfig());
    foreach (var userId in userRoles.Where(x => x.UserId != default).Select(x => x.UserId).Distinct())
    {
      var user = new DynamoDbUser
      {
        Id = userId!,
      };
      batch.AddKey(user.PartitionKey, user.SortKey);
    }

    await batch.ExecuteAsync(cancellationToken);

    return batch.Results;
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
#pragma warning disable CS0618 // Type or member is obsolete - Using DynamoDBOperationConfig is necessary for dynamic table name override via OverrideTableName
    var search = _context.FromQueryAsync<DynamoDbUserRole>(new QueryOperationConfig
    {
      KeyExpression = new Expression
      {
        ExpressionStatement = "PartitionKey = :partitionKey and SortKey = :sortKey",
        ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
        {
          { ":partitionKey", userRole.PartitionKey },
          { ":sortKey", userRole.SortKey },
        },
      },
      Limit = 1,
    }, GetOperationConfig());
#pragma warning restore CS0618
    var roles = await search.GetRemainingAsync(cancellationToken);

    return roles.Any();
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

  public async Task<IdentityResult> UpdateAsync(TUserEntity user, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(user);

    var concurrencyStamp = user.ConcurrencyStamp;
    user.ConcurrencyStamp = Guid.NewGuid().ToString();

    try
    {
      // Ensure no one else has updated the user since it was loaded
      await _context.SaveAsync(user, GetSaveConfig(concurrencyStamp), cancellationToken);
    }
    catch (ConditionalCheckFailedException)
    {
      user.ConcurrencyStamp = concurrencyStamp;

      return IdentityResult.Failed(new IdentityError
      {
        Code = "ConcurrencyFailure",
        Description = "ConcurrencyStamp mismatch",
      });
    }
    await SaveClaims(user, cancellationToken);
    await SaveLogins(user, cancellationToken);
    await SaveRoles(user, cancellationToken);
    await SaveTokens(user, cancellationToken);
    await SavePasskeys(user, cancellationToken);

    return IdentityResult.Success;
  }

  private static List<KeyValuePair<string, string>> FlattenClaims(TUserEntity user) => user.Claims
      ?.SelectMany(x => x.Value.Select(y => new KeyValuePair<string, string>(x.Key, y)))
      .ToList() ?? new();

  public async Task RemoveDeletedClaims(TUserEntity user, CancellationToken cancellationToken)
  {
    if (user.Claims == default)
    {
      return;
    }

    var persistedClaims = await GetRawClaims(user, cancellationToken);
    var newClaims = DynamoDbUserStore<TUserEntity>.FlattenClaims(user).Select(x => new DynamoDbUserClaim
    {
      ClaimType = x.Key,
      ClaimValue = x.Value,
      UserId = user.Id,
    });

    var toBeDeleted = persistedClaims.Except(newClaims);

    if (toBeDeleted.Any())
    {
      var batch = _context.CreateBatchWrite<DynamoDbUserClaim>(GetBatchWriteConfig());

      foreach (var claim in toBeDeleted)
      {
        batch.AddDeleteItem(claim);
      }

      await batch.ExecuteAsync(cancellationToken);
    }
  }

  public async Task SaveClaims(TUserEntity user, CancellationToken cancellationToken)
  {
    await RemoveDeletedClaims(user, cancellationToken);

    if (user.Claims == default)
    {
      return;
    }

    var batch = _context.CreateBatchWrite<DynamoDbUserClaim>(GetBatchWriteConfig());
    var flattenClaims = DynamoDbUserStore<TUserEntity>.FlattenClaims(user);

    foreach (var claim in flattenClaims)
    {
      batch.AddPutItem(new()
      {
        ClaimType = claim.Key,
        ClaimValue = claim.Value,
        UserId = user.Id,
      });
    }

    await batch.ExecuteAsync(cancellationToken);
  }

  public async Task RemoveDeletedLogins(TUserEntity user, CancellationToken cancellationToken)
  {
    if (user.Logins == default)
    {
      return;
    }

    var persistedLogins = await GetRawLogins(user, cancellationToken);
    var newLogins = user.Logins;

    var toBeDeleted = persistedLogins.Except(newLogins);

    if (toBeDeleted.Any())
    {
      var batch = _context.CreateBatchWrite<DynamoDbUserLogin>(GetBatchWriteConfig());

      foreach (var login in toBeDeleted)
      {
        batch.AddDeleteItem(login);
      }

      await batch.ExecuteAsync(cancellationToken);
    }
  }

  public async Task SaveLogins(TUserEntity user, CancellationToken cancellationToken)
  {
    await RemoveDeletedLogins(user, cancellationToken);

    if (user.Logins == default)
    {
      return;
    }

    var batch = _context.CreateBatchWrite<DynamoDbUserLogin>(GetBatchWriteConfig());

    foreach (var login in user.Logins!)
    {
      login.UserId = user.Id;
      batch.AddPutItem(login);
    }

    await batch.ExecuteAsync(cancellationToken);
  }

  public async Task RemoveDeletedRoles(TUserEntity user, CancellationToken cancellationToken)
  {
    if (user.Roles == default)
    {
      return;
    }

    var persistedRoles = await GetRawRoles(user, cancellationToken);
    var newRoles = user.Roles.Select(x => new DynamoDbUserRole
    {
      RoleName = x,
      UserId = user.Id,
    });

    var toBeDeleted = persistedRoles.Except(newRoles);

    if (toBeDeleted.Any())
    {
      var batch = _context.CreateBatchWrite<DynamoDbUserRole>(GetBatchWriteConfig());

      foreach (var role in toBeDeleted)
      {
        batch.AddDeleteItem(role);
      }

      await batch.ExecuteAsync(cancellationToken);
    }
  }

  public async Task SaveRoles(TUserEntity user, CancellationToken cancellationToken)
  {
    await RemoveDeletedRoles(user, cancellationToken);

    if (user.Roles == default)
    {
      return;
    }

    var batch = _context.CreateBatchWrite<DynamoDbUserRole>(GetBatchWriteConfig());

    foreach (var role in user.Roles!)
    {
      batch.AddPutItem(new()
      {
        RoleName = role,
        UserId = user.Id,
      });
    }

    await batch.ExecuteAsync(cancellationToken);
  }

  public async Task RemoveDeletedTokens(TUserEntity user, CancellationToken cancellationToken)
  {
    if (user.Tokens == default)
    {
      return;
    }

    var persistedTokens = await GetRawTokens(user, cancellationToken);
    var newTokens = user.Tokens.Select(x => new DynamoDbUserToken
    {
      UserId = user.Id,
      LoginProvider = x.LoginProvider,
      Name = x.Name,
      Value = x.Value,
    });

    var toBeDeleted = persistedTokens.Except(newTokens);

    if (toBeDeleted.Any())
    {
      var batch = _context.CreateBatchWrite<DynamoDbUserToken>(GetBatchWriteConfig());

      foreach (var token in toBeDeleted)
      {
        batch.AddDeleteItem(token);
      }

      await batch.ExecuteAsync(cancellationToken);
    }
  }

  public async Task SaveTokens(TUserEntity user, CancellationToken cancellationToken)
  {
    await RemoveDeletedTokens(user, cancellationToken);

    if (user.Tokens == default)
    {
      return;
    }

    var batch = _context.CreateBatchWrite<DynamoDbUserToken>(GetBatchWriteConfig());

    foreach (var token in user.Tokens!)
    {
      batch.AddPutItem(new()
      {
        UserId = user.Id,
        LoginProvider = token.LoginProvider,
        Name = token.Name,
        Value = token.Value,
      });
    }

    await batch.ExecuteAsync(cancellationToken);
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

  private async Task<List<DynamoDbUserToken>> GetRawTokens(TUserEntity user, CancellationToken cancellationToken)
  {
#pragma warning disable CS0618 // Type or member is obsolete - Using DynamoDBOperationConfig is necessary for dynamic table name override via OverrideTableName
    var search = _context.FromQueryAsync<DynamoDbUserToken>(new QueryOperationConfig
    {
      KeyExpression = new Expression
      {
        ExpressionStatement = "PartitionKey = :partitionKey and begins_with(SortKey, :sortKey)",
        ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
        {
          { ":partitionKey", user.PartitionKey },
          { ":sortKey", "TOKEN#" },
        },
      },
    }, GetOperationConfig());
#pragma warning restore CS0618
    return await search.GetRemainingAsync(cancellationToken);
  }

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

#pragma warning disable CS0618 // Type or member is obsolete - Using DynamoDBOperationConfig is necessary for dynamic table name override via OverrideTableName
    var search = _context.FromQueryAsync<DynamoDbUserPasskey>(new QueryOperationConfig
    {
      IndexName = "CredentialId-index",
      KeyExpression = new Expression
      {
        ExpressionStatement = "CredentialId = :credentialId",
        ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
        {
          { ":credentialId", Base64UrlEncoding.Encode(credentialId) },
        },
      },
      Limit = 1
    }, GetOperationConfig());
#pragma warning restore CS0618
    var passkeys = await search.GetNextSetAsync(cancellationToken);

    if (passkeys.Any() == false || passkeys.First().UserId == default)
    {
      return default;
    }

    var user = new DynamoDbUser
    {
      Id = passkeys.First().UserId!,
    };
    return await _context.LoadAsync<TUserEntity>(
      user.PartitionKey, user.SortKey, GetLoadConfig(), cancellationToken);
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

  private async Task<List<DynamoDbUserPasskey>> GetRawPasskeys(TUserEntity user, CancellationToken cancellationToken)
  {
#pragma warning disable CS0618 // Type or member is obsolete - Using DynamoDBOperationConfig is necessary for dynamic table name override via OverrideTableName
    var search = _context.FromQueryAsync<DynamoDbUserPasskey>(new QueryOperationConfig
    {
      KeyExpression = new Expression
      {
        ExpressionStatement = "PartitionKey = :partitionKey and begins_with(SortKey, :sortKey)",
        ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
        {
          { ":partitionKey", user.PartitionKey },
          { ":sortKey", PasskeySortKeyPrefix },
        },
      },
    }, GetOperationConfig());
#pragma warning restore CS0618
    return await search.GetRemainingAsync(cancellationToken);
  }

  public async Task RemoveDeletedPasskeys(TUserEntity user, CancellationToken cancellationToken)
  {
    if (user.Passkeys == default)
    {
      return;
    }

    var persistedPasskeys = await GetRawPasskeys(user, cancellationToken);
    var currentSortKeys = user.Passkeys.Select(x => x.SortKey).ToHashSet();

    var toBeDeleted = persistedPasskeys
      .Where(x => currentSortKeys.Contains(x.SortKey) == false)
      .ToList();

    if (toBeDeleted.Count > 0)
    {
      var batch = _context.CreateBatchWrite<DynamoDbUserPasskey>(GetBatchWriteConfig());

      foreach (var passkey in toBeDeleted)
      {
        batch.AddDeleteItem(passkey);
      }

      await batch.ExecuteAsync(cancellationToken);
    }
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
    var batch = _context.CreateBatchWrite<DynamoDbUserPasskey>(GetBatchWriteConfig());

    foreach (var passkey in persistedPasskeys.Values.Where(x => currentSortKeys.Contains(x.SortKey) == false))
    {
      batch.AddDeleteItem(passkey);
    }

    // Passkeys carry the attestation object and client data, so only write the ones that changed
    foreach (var passkey in user.Passkeys)
    {
      passkey.UserId = user.Id;

      if (persistedPasskeys.TryGetValue(passkey.SortKey, out var persistedPasskey) == false
        || IsEqual(persistedPasskey, passkey) == false)
      {
        batch.AddPutItem(passkey);
      }
    }

    await batch.ExecuteAsync(cancellationToken);
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

  private DynamoDBOperationConfig GetOperationConfig() => new()
  {
    OverrideTableName = _tableName,
  };

  private SaveConfig GetSaveConfig() => new()
  {
    OverrideTableName = _tableName,
  };

  private SaveConfig GetSaveConfig(string? concurrencyStamp)
  {
    // Only save when the user exists and still has the concurrency stamp it was loaded with
    var condition = new ContextExpression();

    if (concurrencyStamp == default)
    {
      condition.SetFilter<TUserEntity>(x => ContextExpression.AttributeExists(x.PartitionKey)
        && ContextExpression.AttributeNotExists(x.ConcurrencyStamp));
    }
    else
    {
      condition.SetFilter<TUserEntity>(x => ContextExpression.AttributeExists(x.PartitionKey)
        && x.ConcurrencyStamp == concurrencyStamp);
    }

    return new()
    {
      OverrideTableName = _tableName,
      ConditionalExpression = condition,
    };
  }

  private LoadConfig GetLoadConfig() => new()
  {
    OverrideTableName = _tableName,
    // Updates check the concurrency stamp, so a stale user would fail to update
    ConsistentRead = true,
  };

  private BatchGetConfig GetBatchGetConfig() => new()
  {
    OverrideTableName = _tableName,
  };

  private BatchWriteConfig GetBatchWriteConfig() => new()
  {
    OverrideTableName = _tableName,
  };

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
