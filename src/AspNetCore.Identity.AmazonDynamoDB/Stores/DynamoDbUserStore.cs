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
        IUserLoginStore<TUserEntity>
    where TUserEntity : DynamoDbUser, new()
{
    private IAmazonDynamoDB _client;
    private IDynamoDBContext _context;
    private IOptionsMonitor<DynamoDbOptions> _optionsMonitor;
    private DynamoDbOptions _options => _optionsMonitor.CurrentValue;

    public DynamoDbUserStore(IOptionsMonitor<DynamoDbOptions> optionsMonitor)
    {
        ArgumentNullException.ThrowIfNull(optionsMonitor);

        _optionsMonitor = optionsMonitor;

        ArgumentNullException.ThrowIfNull(_options.Database);

        _client = _options.Database;
        _context = new DynamoDBContext(_client);
    }

    public async Task AddClaimsAsync(TUserEntity user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (claims?.Any() != true)
        {
            return;
        }

        var batch = _context.CreateBatchWrite<DynamoDbUserClaim>();

        foreach (var claim in claims)
        {
            batch.AddPutItem(new()
            {
                ClaimType = claim.Type,
                ClaimValue = claim.Value,
                UserId = user.Id,
            });
        }

        await batch.ExecuteAsync(cancellationToken);
    }

    public async Task AddLoginAsync(TUserEntity user, UserLoginInfo login, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(login);

        await _context.SaveAsync(new DynamoDbUserLogin
        {
            LoginProvider = login.LoginProvider,
            ProviderDisplayName = login.ProviderDisplayName,
            ProviderKey = login.ProviderKey,
            UserId = user.Id,
        }, cancellationToken);
    }

    public async Task AddToRoleAsync(TUserEntity user, string roleName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(roleName);

        await _context.SaveAsync(new DynamoDbUserRole
        {
            RoleName = roleName,
            UserId = user.Id,
        }, cancellationToken);
    }

    public async Task<IdentityResult> CreateAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        cancellationToken.ThrowIfCancellationRequested();

        await _context.SaveAsync(user, cancellationToken);

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

    public async Task<IList<Claim>> GetClaimsAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

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
        var claims = await search.GetRemainingAsync(cancellationToken);

        return claims
            .Where(x => x.ClaimType != default && x.ClaimValue != default)
            .Select(x => new Claim(x.ClaimType!, x.ClaimValue!))
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

    public async Task<IList<UserLoginInfo>> GetLoginsAsync(
        TUserEntity user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

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
        var logins = await search.GetRemainingAsync(cancellationToken);

        return logins
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

    public async Task<IList<string>> GetRolesAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

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
        var roles = await search.GetRemainingAsync(cancellationToken);

        return roles
            .Where(x => x.RoleName != default)
            .Select(x => x.RoleName!)
            .ToList();
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

    public Task<IList<TUserEntity>> GetUsersForClaimAsync(Claim claim, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
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

    public Task RemoveClaimsAsync(TUserEntity user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task RemoveFromRoleAsync(TUserEntity user, string roleName, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task RemoveLoginAsync(TUserEntity user, string loginProvider, string providerKey, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task ReplaceClaimAsync(TUserEntity user, Claim claim, Claim newClaim, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task ResetAccessFailedCountAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
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

    public Task<IdentityResult> UpdateAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
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