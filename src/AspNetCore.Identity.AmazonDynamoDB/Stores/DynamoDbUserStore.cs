using System.Security.Claims;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AspNetCore.Identity.AmazonDynamoDB;

public class DynamoDbUserStore<TUserEntity> : IUserStore<TUserEntity>,
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
        IProtectedUserStore<TUserEntity>
    where TUserEntity : DynamoDbUser, new()
{
    private IAmazonDynamoDB _client;
    private IDynamoDBContext _context;
    private const string InternalLoginProvider = "[AspNetUserStore]";
    private const string AuthenticatorKeyTokenName = "AuthenticatorKey";
    private const string RecoveryCodeTokenName = "RecoveryCodes";

    public DynamoDbUserStore(
        IOptionsMonitor<DynamoDbOptions> optionsMonitor,
        IAmazonDynamoDB? database = default)
    {
        ArgumentNullException.ThrowIfNull(optionsMonitor);

        var options = optionsMonitor.CurrentValue;

        if (options.Database == default && database == default)
        {
            throw new ArgumentNullException(nameof(options.Database));
        }

        _client = database ?? options.Database!;
        _context = new DynamoDBContext(_client);
    }

    public Task AddClaimsAsync(TUserEntity user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (claims?.Any() != true)
        {
            return Task.CompletedTask;
        }

        foreach (var claim in claims)
        {
            if (user.Claims.ContainsKey(claim.Type))
            {
                user.Claims[claim.Type].Add(claim.Value);
            }
            else
            {
                user.Claims.Add(claim.Type, new() { claim.Value });
            }
        }

        return Task.CompletedTask;
    }

    public Task AddLoginAsync(TUserEntity user, UserLoginInfo login, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(login);

        user.Logins.Add(new DynamoDbUserLogin
        {
            LoginProvider = login.LoginProvider,
            ProviderKey = login.ProviderKey,
            ProviderDisplayName = login.ProviderDisplayName,
            UserId = user.Id,
        });

        return Task.CompletedTask;
    }

    public Task AddToRoleAsync(TUserEntity user, string roleName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(roleName);

        if (user.Roles.Contains(roleName) == false)
        {
            user.Roles.Add(roleName);
        }

        return Task.CompletedTask;
    }

    public async Task<IdentityResult> CreateAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        cancellationToken.ThrowIfCancellationRequested();

        await _context.SaveAsync(user, cancellationToken);
        await SaveClaims(user, cancellationToken);
        await SaveLogins(user, cancellationToken);
        await SaveRoles(user, cancellationToken);
        await SaveTokens(user, cancellationToken);

        return IdentityResult.Success;
    }

    // TODO: Cleanup resources connected to the user (roles, logins, claims)
    public async Task<IdentityResult> DeleteAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        await _context.DeleteAsync(user, cancellationToken);

        return IdentityResult.Success;
    }

    public async Task<TUserEntity> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(normalizedEmail);

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
        });
        var users = await search.GetRemainingAsync(cancellationToken);
        return users?.FirstOrDefault()!; // Hide compiler warning until Identity handles nullable (v7)
    }

    public async Task<TUserEntity> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await _context.LoadAsync<TUserEntity>(userId, cancellationToken);
    }

    public async Task<TUserEntity> FindByLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(loginProvider);
        ArgumentNullException.ThrowIfNull(providerKey);

        var login = await _context.LoadAsync<DynamoDbUserLogin>(loginProvider, providerKey, cancellationToken);

        if (login == default)
        {
            return default!; // Hide compiler warning until Identity handles nullable (v7)
        }

        return await _context.LoadAsync<TUserEntity>(login.UserId, cancellationToken);
    }

    public async Task<TUserEntity> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(normalizedUserName);

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
        });
        var users = await search.GetRemainingAsync(cancellationToken);
        return users?.FirstOrDefault()!; // Hide compiler warning until Identity handles nullable (v7)
    }

    public Task<int> GetAccessFailedCountAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        return Task.FromResult(user.AccessFailedCount);
    }

    private async Task<List<DynamoDbUserClaim>> GetRawClaims(TUserEntity user, CancellationToken cancellationToken)
    {
        var search = _context.FromQueryAsync<DynamoDbUserClaim>(new QueryOperationConfig
        {
            IndexName = "UserId-index",
            KeyExpression = new Expression
            {
                ExpressionStatement = "UserId = :userId",
                ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
                {
                    { ":userId", user.Id },
                },
            },
        });
        return await search.GetRemainingAsync(cancellationToken);
    }

    private Dictionary<string, List<string>> ToDictionary(List<DynamoDbUserClaim> claims) => claims
        .GroupBy(x => x.ClaimType)
        .ToDictionary(x => x.Key!, x => x.Select(y => y.ClaimValue!).ToList());

    public async Task<IList<Claim>> GetClaimsAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        
        if (user.Claims.Any() == false)
        {
            var claims = await GetRawClaims(user, cancellationToken);
            user.Claims = ToDictionary(claims);
        }

        return FlattenClaims(user)
            .Select(x => new Claim(x.Key, x.Value))
            .ToList();
    }

    public Task<string> GetEmailAsync(TUserEntity user, CancellationToken cancellationToken)
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

        return Task.FromResult((DateTimeOffset?)user.LockoutEnd);
    }

    public async Task<List<DynamoDbUserLogin>> GetRawLogins(TUserEntity user, CancellationToken cancellationToken)
    {
        var search = _context.FromQueryAsync<DynamoDbUserLogin>(new QueryOperationConfig
        {
            IndexName = "UserId-index",
            KeyExpression = new Expression
            {
                ExpressionStatement = "UserId = :userId",
                ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
                {
                    { ":userId", user.Id },
                },
            },
        });
        return await search.GetRemainingAsync(cancellationToken);
    }

    public async Task<IList<UserLoginInfo>> GetLoginsAsync(
        TUserEntity user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user.Logins.Any() == false)
        {
            user.Logins = await GetRawLogins(user, cancellationToken);
        }

        return user.Logins
            .Select(x => new UserLoginInfo(
                x.LoginProvider, x.ProviderKey, x.ProviderDisplayName))
            .ToList();
    }

    public Task<string> GetNormalizedEmailAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        return Task.FromResult(user.NormalizedEmail);
    }

    public Task<string> GetNormalizedUserNameAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        return Task.FromResult(user.NormalizedUserName);
    }

    public Task<string> GetPasswordHashAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        return Task.FromResult(user.PasswordHash);
    }

    public Task<string> GetPhoneNumberAsync(TUserEntity user, CancellationToken cancellationToken)
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
        var search = _context.FromQueryAsync<DynamoDbUserRole>(new QueryOperationConfig
        {
            KeyExpression = new Expression
            {
                ExpressionStatement = "UserId = :userId",
                ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
                {
                    { ":userId", user.Id },
                },
            },
        });
        return await search.GetRemainingAsync(cancellationToken);
    }

    public async Task<IList<string>> GetRolesAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user.Roles.Any() == false)
        {
            var roles = await GetRawRoles(user, cancellationToken);
            user.Roles = roles.Select(x => x.RoleName!).ToList();
        }

        return user.Roles;
    }

    public Task<string> GetSecurityStampAsync(TUserEntity user, CancellationToken cancellationToken)
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

    public Task<string> GetUserNameAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        return Task.FromResult(user.UserName);
    }

    public async Task<IList<TUserEntity>> GetUsersForClaimAsync(Claim claim, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);

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
        });
        var userClaims = await search.GetRemainingAsync(cancellationToken);

        var batch = _context.CreateBatchGet<TUserEntity>();
        foreach (var userId in userClaims.Select(x => x.UserId).Distinct())
        {
            batch.AddKey(userId);
        }

        await batch.ExecuteAsync(cancellationToken);

        return batch.Results;
    }

    public async Task<IList<TUserEntity>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(roleName);

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
        });
        var userRoles = await search.GetRemainingAsync(cancellationToken);

        var batch = _context.CreateBatchGet<TUserEntity>();
        foreach (var userId in userRoles.Select(x => x.UserId).Distinct())
        {
            batch.AddKey(userId);
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

        var search = _context.FromQueryAsync<DynamoDbUserRole>(new QueryOperationConfig
        {
            KeyExpression = new Expression
            {
                ExpressionStatement = "UserId = :userId and RoleName = :roleName",
                ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
                {
                    { ":userId", user.Id },
                    { ":roleName", roleName },
                },
            },
            Limit = 1,
        });
        var roles = await search.GetRemainingAsync(cancellationToken);

        return roles.Any();
    }

    public async Task RemoveClaimsAsync(TUserEntity user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(claims);

        user.Claims = user.Claims ?? ToDictionary(await GetRawClaims(user, cancellationToken));

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
        await AddClaimsAsync(user, new List<Claim> { newClaim }, cancellationToken);
    }

    public Task ResetAccessFailedCountAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.AccessFailedCount = 0;
        return Task.FromResult(user.AccessFailedCount);
    }

    public Task SetEmailAsync(TUserEntity user, string email, CancellationToken cancellationToken)
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

    public Task SetNormalizedEmailAsync(TUserEntity user, string normalizedEmail, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.NormalizedEmail = normalizedEmail;
        return Task.CompletedTask;
    }

    public Task SetNormalizedUserNameAsync(TUserEntity user, string normalizedName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.NormalizedUserName = normalizedName;
        return Task.CompletedTask;
    }

    public Task SetPasswordHashAsync(TUserEntity user, string passwordHash, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.PasswordHash = passwordHash;
        return Task.CompletedTask;
    }

    public Task SetPhoneNumberAsync(TUserEntity user, string phoneNumber, CancellationToken cancellationToken)
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

    public Task SetUserNameAsync(TUserEntity user, string userName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.UserName = userName;
        return Task.CompletedTask;
    }

    public async Task<IdentityResult> UpdateAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        // Ensure no one else is updating
        var databaseApplication = await _context.LoadAsync<TUserEntity>(user.Id, cancellationToken);
        if (databaseApplication == default || databaseApplication.ConcurrencyStamp != user.ConcurrencyStamp)
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "ConcurrencyFailure",
                Description = "ConcurrencyStamp mismatch",
            });
        }

        user.ConcurrencyStamp = Guid.NewGuid().ToString();

        await _context.SaveAsync(user, cancellationToken);
        await SaveClaims(user, cancellationToken);
        await SaveLogins(user, cancellationToken);
        await SaveRoles(user, cancellationToken);
        await SaveTokens(user, cancellationToken);

        return IdentityResult.Success;
    }

    private List<KeyValuePair<string, string>> FlattenClaims(TUserEntity user) => user.Claims
        .SelectMany(x => x.Value.Select(y => new KeyValuePair<string, string>(x.Key, y)))
        .ToList();

    public async Task RemoveDeletedClaims(TUserEntity user, CancellationToken cancellationToken)
    {
        var persistedClaims = await GetRawClaims(user, cancellationToken);
        var newClaims = FlattenClaims(user).Select(x => new DynamoDbUserClaim
        {
            ClaimType = x.Key,
            ClaimValue = x.Value,
            UserId = user.Id,
        });

        var toBeDeleted = persistedClaims.Except(newClaims);

        if (toBeDeleted.Any())
        {
            var batch = _context.CreateBatchWrite<DynamoDbUserClaim>();
            
            foreach (var claim in toBeDeleted)
            {
                batch.AddDeleteItem(claim);
            }

            await batch.ExecuteAsync();
        }
    }

    public async Task SaveClaims(TUserEntity user, CancellationToken cancellationToken)
    {
        await RemoveDeletedClaims(user, cancellationToken);

        if (user.Claims.Any() == false)
        {
            return;
        }

        var batch = _context.CreateBatchWrite<DynamoDbUserClaim>();
        var flattenClaims = FlattenClaims(user);

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
        var persistedLogins = await GetRawLogins(user, cancellationToken);
        var newLogins = user.Logins;

        var toBeDeleted = persistedLogins.Except(newLogins);

        if (toBeDeleted.Any())
        {
            var batch = _context.CreateBatchWrite<DynamoDbUserLogin>();

            foreach (var login in toBeDeleted)
            {
                batch.AddDeleteItem(login);
            }

            await batch.ExecuteAsync();
        }
    }

    public async Task SaveLogins(TUserEntity user, CancellationToken cancellationToken)
    {
        await RemoveDeletedLogins(user, cancellationToken);

        if (user.Logins.Any() == false)
        {
            return;
        }

        var batch = _context.CreateBatchWrite<DynamoDbUserLogin>();

        foreach (var login in user.Logins)
        {
            login.UserId = user.Id;
            batch.AddPutItem(login);
        }

        await batch.ExecuteAsync(cancellationToken);
    }

    public async Task RemoveDeletedRoles(TUserEntity user, CancellationToken cancellationToken)
    {
        var persistedRoles = await GetRawRoles(user, cancellationToken);
        var newRoles = user.Roles.Select(x => new DynamoDbUserRole
        {
            RoleName = x,
            UserId = user.Id,
        });

        var toBeDeleted = persistedRoles.Except(newRoles);

        if (toBeDeleted.Any())
        {
            var batch = _context.CreateBatchWrite<DynamoDbUserRole>();

            foreach (var role in toBeDeleted)
            {
                batch.AddDeleteItem(role);
            }

            await batch.ExecuteAsync();
        }
    }

    public async Task SaveRoles(TUserEntity user, CancellationToken cancellationToken)
    {
        await RemoveDeletedRoles(user, cancellationToken);

        if (user.Roles.Any() == false)
        {
            return;
        }

        var batch = _context.CreateBatchWrite<DynamoDbUserRole>();

        foreach (var role in user.Roles)
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
            var batch = _context.CreateBatchWrite<DynamoDbUserToken>();

            foreach (var token in toBeDeleted)
            {
                batch.AddDeleteItem(token);
            }

            await batch.ExecuteAsync();
        }
    }

    public async Task SaveTokens(TUserEntity user, CancellationToken cancellationToken)
    {
        await RemoveDeletedTokens(user, cancellationToken);

        if (user.Tokens.Any() == false)
        {
            return;
        }

        var batch = _context.CreateBatchWrite<DynamoDbUserToken>();

        foreach (var token in user.Tokens)
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

    public Task<string> GetAuthenticatorKeyAsync(TUserEntity user, CancellationToken cancellationToken)
        => GetTokenAsync(user, InternalLoginProvider, AuthenticatorKeyTokenName, cancellationToken);

    public async Task RemoveTokenAsync(TUserEntity user, string loginProvider, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(loginProvider);
        ArgumentNullException.ThrowIfNull(name);

        var entry = await FindTokenAsync(user, loginProvider, name, cancellationToken);
        if (entry != null)
        {
            user.Tokens.RemoveAll(x => x.LoginProvider == entry.LoginProvider && x.Name == entry.Name);
        }
    }

    public async Task<string> GetTokenAsync(TUserEntity user, string loginProvider, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(loginProvider);
        ArgumentNullException.ThrowIfNull(name);

        var token = await FindTokenAsync(user, loginProvider, name, cancellationToken);
        return token?.Value ?? string.Empty;
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

    public async Task SetTokenAsync(TUserEntity user, string loginProvider, string name, string value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var token = await FindTokenAsync(user, loginProvider, name, cancellationToken);
        if (token == null)
        {
            user.Tokens.Add(new IdentityUserToken<string>
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

            var idx = user.Tokens.FindIndex(x => x.LoginProvider == token.LoginProvider && x.Name == token.Name);
            user.Tokens[idx] = token;
        }
    }

    private async Task<List<DynamoDbUserToken>> GetRawTokens(TUserEntity user, CancellationToken cancellationToken)
    {
        var search = _context.FromQueryAsync<DynamoDbUserToken>(new QueryOperationConfig
        {
            IndexName = "UserId-index",
            KeyExpression = new Expression
            {
                ExpressionStatement = "UserId = :userId",
                ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
                {
                    { ":userId", user.Id },
                },
            },
        });
        return await search.GetRemainingAsync(cancellationToken);
    }

    private async Task<IdentityUserToken<string>?> FindTokenAsync(TUserEntity user, string loginProvider, string name, CancellationToken cancellationToken)
    {
        if (user.Tokens.Any() == false)
        {
            var tokens = await GetRawTokens(user, cancellationToken);
            user.Tokens = tokens
                .Select(x => new IdentityUserToken<string>
                {
                    LoginProvider = x.LoginProvider,
                    Name = x.Name,
                    UserId = x.UserId!,
                    Value = x.Value,
                })
                .ToList();
        }

        return user.Tokens.FirstOrDefault(x => x.LoginProvider == loginProvider && x.Name == name);
    }

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