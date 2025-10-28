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
  private readonly IAmazonDynamoDB _client;
  private readonly IDynamoDBContext _context;
  private readonly IOptionsMonitor<DynamoDbOptions> _optionsMonitor;
  private string _tableName =>
    _optionsMonitor.CurrentValue.DefaultTableName ?? Constants.DefaultTableName;

  public DynamoDbRoleStore(
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

    await _context.SaveAsync(role, GetSaveConfig(), cancellationToken);

    return IdentityResult.Success;
  }

  public async Task<IdentityResult> DeleteAsync(TRoleEntity role, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(role);

    await _context.DeleteAsync(role, GetDeleteConfig(), cancellationToken);

    return IdentityResult.Success;
  }

  public async Task<TRoleEntity?> FindByIdAsync(string roleId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(roleId);

    var role = new DynamoDbRole
    {
      Id = roleId,
    };
    return await _context.LoadAsync<TRoleEntity>(role.PartitionKey, role.SortKey, GetLoadConfig(), cancellationToken);
  }

  public async Task<TRoleEntity?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(normalizedRoleName);

#pragma warning disable CS0618 // Type or member is obsolete - Using DynamoDBOperationConfig is necessary for dynamic table name override via OverrideTableName
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
      Limit = 1,
    }, GetOperationConfig());
#pragma warning restore CS0618
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

  public Task<string?> GetNormalizedRoleNameAsync(TRoleEntity role, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(role);

    return Task.FromResult(role.NormalizedName);
  }

  public Task<string> GetRoleIdAsync(TRoleEntity role, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(role);

    return Task.FromResult(role.Id);
  }

  public Task<string?> GetRoleNameAsync(TRoleEntity role, CancellationToken cancellationToken)
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

  public Task SetNormalizedRoleNameAsync(TRoleEntity role, string? normalizedName, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(role);
    ArgumentNullException.ThrowIfNull(normalizedName);

    role.NormalizedName = normalizedName;

    return Task.CompletedTask;
  }

  public Task SetRoleNameAsync(TRoleEntity role, string? roleName, CancellationToken cancellationToken)
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
    var databaseApplication = await _context.LoadAsync<TRoleEntity>(
      role.PartitionKey, role.SortKey, GetLoadConfig(), cancellationToken);
    if (databaseApplication == default || databaseApplication.ConcurrencyStamp != role.ConcurrencyStamp)
    {
      return IdentityResult.Failed(new IdentityError
      {
        Code = "ConcurrencyFailure",
        Description = "ConcurrencyStamp mismatch",
      });
    }

    role.ConcurrencyStamp = Guid.NewGuid().ToString();

    await _context.SaveAsync(role, GetSaveConfig(), cancellationToken);

    return IdentityResult.Success;
  }

  private DynamoDBOperationConfig GetOperationConfig() => new()
  {
    OverrideTableName = _tableName,
  };

  private SaveConfig GetSaveConfig() => new()
  {
    OverrideTableName = _tableName,
  };

  private LoadConfig GetLoadConfig() => new()
  {
    OverrideTableName = _tableName,
  };

  private DeleteConfig GetDeleteConfig() => new()
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

  ~DynamoDbRoleStore()
  {
    Dispose(false);
  }
}
