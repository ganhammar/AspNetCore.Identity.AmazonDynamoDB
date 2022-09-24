using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace AspNetCore.Identity.AmazonDynamoDB;

public class DynamoDbRoleStore<TRoleEntity> : IRoleStore<TRoleEntity>,
        IRoleClaimStore<TRoleEntity>
    where TRoleEntity : DynamoDbRole, new()
{
    public Task AddClaimAsync(TRoleEntity role, Claim claim, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IdentityResult> CreateAsync(TRoleEntity role, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<IdentityResult> DeleteAsync(TRoleEntity role, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Task<TRoleEntity> FindByIdAsync(string roleId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<TRoleEntity> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
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
}