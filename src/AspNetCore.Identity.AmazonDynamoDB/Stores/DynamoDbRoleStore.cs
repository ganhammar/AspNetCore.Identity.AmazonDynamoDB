using System.Security.Claims;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AspNetCore.Identity.AmazonDynamoDB;

public class DynamoDbRoleStore<TRoleEntity> : IRoleStore<TRoleEntity>,
        IRoleClaimStore<TRoleEntity>
    where TRoleEntity : DynamoDbRole, new()
{
    private IAmazonDynamoDB _client;
    private IDynamoDBContext _context;
    private IOptionsMonitor<DynamoDbOptions> _optionsMonitor;
    private DynamoDbOptions _options => _optionsMonitor.CurrentValue;

    public DynamoDbRoleStore(IOptionsMonitor<DynamoDbOptions> optionsMonitor)
    {
        ArgumentNullException.ThrowIfNull(optionsMonitor);

        _optionsMonitor = optionsMonitor;

        ArgumentNullException.ThrowIfNull(_options.Database);

        _client = _options.Database;
        _context = new DynamoDBContext(_client);
    }

    public Task AddClaimAsync(TRoleEntity role, Claim claim, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(claim);

        role.Claims.Add(claim.Type, claim.Value);

        return Task.CompletedTask;
    }

    public async Task<IdentityResult> CreateAsync(TRoleEntity role, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(role);

        await _context.SaveAsync(role, cancellationToken);

        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(TRoleEntity role, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(role);

        await _context.DeleteAsync(role, cancellationToken);

        return IdentityResult.Success;
    }

    public async Task<TRoleEntity> FindByIdAsync(string roleId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(roleId);

        return await _context.LoadAsync<TRoleEntity>(roleId, cancellationToken);
    }

    public async Task<TRoleEntity> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(normalizedRoleName);

        var search = _context.FromQueryAsync<TRoleEntity>(new QueryOperationConfig
        {
            IndexName = "NormalizedName-index",
            KeyExpression = new Expression
            {
                ExpressionStatement = "NormalizedName = :normalizedRoleName",
                ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
                {
                    { ":normalizedRoleName", normalizedRoleName },
                },
            },
            Limit = 1
        });
        var roles = await search.GetRemainingAsync(cancellationToken);
        return roles?.FirstOrDefault()!; // Hide compiler warning until Identity handles nullable (v7)
    }

    public Task<IList<Claim>> GetClaimsAsync(TRoleEntity role, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetNormalizedRoleNameAsync(TRoleEntity role, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetRoleIdAsync(TRoleEntity role, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetRoleNameAsync(TRoleEntity role, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task RemoveClaimAsync(TRoleEntity role, Claim claim, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task SetNormalizedRoleNameAsync(TRoleEntity role, string normalizedName, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task SetRoleNameAsync(TRoleEntity role, string roleName, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<IdentityResult> UpdateAsync(TRoleEntity role, CancellationToken cancellationToken)
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

    ~DynamoDbRoleStore()
    {
        Dispose(false);
    }
}