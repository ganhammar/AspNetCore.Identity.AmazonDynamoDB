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

  public DynamoDbRoleStore(
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

  public Task AddClaimAsync(TRoleEntity role, Claim claim, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(role);
    ArgumentNullException.ThrowIfNull(claim);

    if (role.Claims.ContainsKey(claim.Type))
    {
      role.Claims[claim.Type].Add(claim.Value);
    }
    else
    {
      role.Claims.Add(claim.Type, new() { claim.Value });
    }

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
    ArgumentNullException.ThrowIfNull(role);

    return Task.FromResult(role.Claims
      .SelectMany(x => x.Value.Select(y => new Claim(x.Key, y)))
      .ToList() as IList<Claim>);
  }

  public Task<string> GetNormalizedRoleNameAsync(TRoleEntity role, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(role);

    return Task.FromResult(role.NormalizedName);
  }

  public Task<string> GetRoleIdAsync(TRoleEntity role, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(role);

    return Task.FromResult(role.Id);
  }

  public Task<string> GetRoleNameAsync(TRoleEntity role, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(role);

    return Task.FromResult(role.Name);
  }

  public Task RemoveClaimAsync(TRoleEntity role, Claim claim, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(role);
    ArgumentNullException.ThrowIfNull(claim);

    if (role.Claims.ContainsKey(claim.Type))
    {
      role.Claims[claim.Type].Remove(claim.Value);

      if (role.Claims[claim.Type].Count == 0)
      {
        role.Claims.Remove(claim.Type);
      }
    }

    return Task.CompletedTask;
  }

  public Task SetNormalizedRoleNameAsync(TRoleEntity role, string normalizedName, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(role);
    ArgumentNullException.ThrowIfNull(normalizedName);

    role.NormalizedName = normalizedName;

    return Task.CompletedTask;
  }

  public Task SetRoleNameAsync(TRoleEntity role, string roleName, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(role);
    ArgumentNullException.ThrowIfNull(roleName);

    role.Name = roleName;

    return Task.CompletedTask;
  }

  public async Task<IdentityResult> UpdateAsync(TRoleEntity role, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(role);

    // Ensure no one else is updating
    var databaseApplication = await _context.LoadAsync<TRoleEntity>(role.Id, cancellationToken);
    if (databaseApplication == default || databaseApplication.ConcurrencyStamp != role.ConcurrencyStamp)
    {
      return IdentityResult.Failed(new IdentityError
      {
        Code = "ConcurrencyFailure",
        Description = "ConcurrencyStamp mismatch",
      });
    }

    role.ConcurrencyStamp = Guid.NewGuid().ToString();

    await _context.SaveAsync(role, cancellationToken);

    return IdentityResult.Success;
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
