using System.Security.Claims;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
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

        await _context.SaveAsync(new DynamoDbRoleUser
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

    public Task<IdentityResult> DeleteAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Task<TUserEntity> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<TUserEntity> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<TUserEntity> FindByLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<TUserEntity> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<int> GetAccessFailedCountAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<IList<Claim>> GetClaimsAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetEmailAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<bool> GetEmailConfirmedAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<bool> GetLockoutEnabledAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<IList<UserLoginInfo>> GetLoginsAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetNormalizedEmailAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetNormalizedUserNameAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetPasswordHashAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetPhoneNumberAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<bool> GetPhoneNumberConfirmedAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<IList<string>> GetRolesAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetSecurityStampAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<bool> GetTwoFactorEnabledAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<string> GeTUserEntityIdAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<string> GeTUserEntityNameAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetUserIdAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetUserNameAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<IList<TUserEntity>> GetUsersForClaimAsync(Claim claim, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<IList<TUserEntity>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<bool> HasPasswordAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<int> IncrementAccessFailedCountAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<bool> IsInRoleAsync(TUserEntity user, string roleName, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }

    public Task SetEmailConfirmedAsync(TUserEntity user, bool confirmed, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task SetLockoutEnabledAsync(TUserEntity user, bool enabled, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task SetLockoutEndDateAsync(TUserEntity user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task SetNormalizedEmailAsync(TUserEntity user, string normalizedEmail, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task SetNormalizedUserNameAsync(TUserEntity user, string normalizedName, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task SetPasswordHashAsync(TUserEntity user, string passwordHash, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task SetPhoneNumberAsync(TUserEntity user, string phoneNumber, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task SetPhoneNumberConfirmedAsync(TUserEntity user, bool confirmed, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task SetSecurityStampAsync(TUserEntity user, string stamp, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task SetTwoFactorEnabledAsync(TUserEntity user, bool enabled, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task SeTUserEntityNameAsync(TUserEntity user, string userName, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task SetUserNameAsync(TUserEntity user, string userName, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<IdentityResult> UpdateAsync(TUserEntity user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}