using System.Security.Claims;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AspNetCore.Identity.AmazonDynamoDB;

public class DynamoDbRoleStore<TRoleEntity> : IRoleStore<TRoleEntity>,
    IRoleClaimStore<TRoleEntity>
  where TRoleEntity : DynamoDbRole, new()
{
  private static readonly IdentityErrorDescriber Errors = new();
  private readonly IAmazonDynamoDB _client;
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

  // The role and the lookup of its normalized name are written together, so a name that another
  // role holds fails the whole write
  public async Task<IdentityResult> CreateAsync(TRoleEntity role, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(role);

    var items = new List<TransactWriteItem>
    {
      DynamoDbItems.TransactPut(_tableName, role, "attribute_not_exists(PartitionKey)"),
    };

    if (role.NormalizedName is { } normalizedName)
    {
      items.Add(DynamoDbItems.TransactPut(
        _tableName, new LookupItem(Lookups.RoleName(normalizedName), role.Id, role.Name), "attribute_not_exists(PartitionKey)"));
    }

    try
    {
      await DynamoDbItems.TransactWriteAsync(_client, items, cancellationToken);
    }
    catch (TransactionCanceledException exception)
    {
      return IdentityResult.Failed(DynamoDbItems.FailedConditions(exception).Contains(1)
        ? Errors.DuplicateRoleName(role.Name ?? role.NormalizedName!)
        : new IdentityError { Code = "DuplicateRoleId", Description = $"A role with the id {role.Id} already exists" });
    }

    return IdentityResult.Success;
  }

  public async Task<IdentityResult> DeleteAsync(TRoleEntity role, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(role);

    // The stored role names the lookup to remove, which a renamed one in memory would not
    var stored = await FindByIdAsync(role.Id, cancellationToken);
    var items = new List<TransactWriteItem>
    {
      DynamoDbItems.TransactDelete(_tableName, role.PartitionKey, role.SortKey),
    };

    if (stored?.NormalizedName is { } normalizedName)
    {
      items.Add(DeleteLookup(Lookups.RoleName(normalizedName), role.Id));
    }

    await DynamoDbItems.TransactWriteAsync(_client, items, cancellationToken);

    return IdentityResult.Success;
  }

  public async Task<TRoleEntity?> FindByIdAsync(string roleId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(roleId);

    var role = new DynamoDbRole
    {
      Id = roleId,
    };
    return await DynamoDbItems.GetAsync<TRoleEntity>(
      _client, _tableName, role.PartitionKey, role.SortKey, cancellationToken, consistentRead: true);
  }

  public async Task<TRoleEntity?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(normalizedRoleName);

    var lookup = await DynamoDbItems.GetAsync<LookupItem>(
      _client, _tableName, Lookups.RoleName(normalizedRoleName), Lookups.SortKey, cancellationToken, consistentRead: true);

    return lookup is null ? default! : (await FindByIdAsync(lookup.Id, cancellationToken))!; // Hide compiler warning until Identity handles nullable (v7)
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

  // The role is written only when it still has the concurrency stamp it was loaded with, and its
  // name lookup moves with a renamed role in the same transaction
  public async Task<IdentityResult> UpdateAsync(TRoleEntity role, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(role);

    var stored = await FindByIdAsync(role.Id, cancellationToken);

    if (stored is null)
    {
      return IdentityResult.Failed(Errors.ConcurrencyFailure());
    }

    var concurrencyStamp = role.ConcurrencyStamp;
    role.ConcurrencyStamp = Guid.NewGuid().ToString();

    var items = new List<TransactWriteItem>
    {
      DynamoDbItems.TransactPut(
        _tableName,
        role,
        concurrencyStamp is null
          ? "attribute_exists(PartitionKey) and attribute_not_exists(ConcurrencyStamp)"
          : "attribute_exists(PartitionKey) and ConcurrencyStamp = :concurrencyStamp",
        concurrencyStamp is null ? default : new() { { ":concurrencyStamp", new AttributeValue { S = concurrencyStamp } } }),
    };

    if (stored.NormalizedName is { } previousName && previousName != role.NormalizedName)
    {
      items.Add(DeleteLookup(Lookups.RoleName(previousName), role.Id));
    }

    var lookupIndex = -1;
    if (role.NormalizedName is { } normalizedName)
    {
      lookupIndex = items.Count;
      items.Add(DynamoDbItems.TransactPut(
        _tableName,
        new LookupItem(Lookups.RoleName(normalizedName), role.Id, role.Name),
        "attribute_not_exists(PartitionKey) or Id = :id",
        new() { { ":id", new AttributeValue { S = role.Id } } }));
    }

    try
    {
      await DynamoDbItems.TransactWriteAsync(_client, items, cancellationToken);
    }
    catch (TransactionCanceledException exception)
    {
      role.ConcurrencyStamp = concurrencyStamp;
      var failed = DynamoDbItems.FailedConditions(exception);

      return IdentityResult.Failed(failed.Contains(lookupIndex) && !failed.Contains(0)
        ? Errors.DuplicateRoleName(role.Name ?? role.NormalizedName!)
        : Errors.ConcurrencyFailure());
    }

    return IdentityResult.Success;
  }

  // Removes a lookup that points at the role, or is already gone
  private TransactWriteItem DeleteLookup(string partitionKey, string roleId) => DynamoDbItems.TransactDelete(
    _tableName,
    partitionKey,
    Lookups.SortKey,
    "attribute_not_exists(PartitionKey) or Id = :id",
    new() { { ":id", new AttributeValue { S = roleId } } });

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
