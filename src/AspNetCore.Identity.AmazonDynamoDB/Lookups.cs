using Amazon.DynamoDBv2.Model;

namespace AspNetCore.Identity.AmazonDynamoDB;

/// <summary>
/// The keys of the lookup items, which point from a value to the user or role it belongs to, and
/// of the membership items, which list the users that hold a claim or a role. They replace the
/// global secondary indexes: a lookup is one strongly consistent read, and a unique value is
/// enforced by writing its lookup item in the same transaction as the item it belongs to.
/// </summary>
internal static class Lookups
{
  /// <summary>The sort key of a lookup item, of which a value has at most one.</summary>
  public const string SortKey = "#LOOKUP";

  public static string UserName(string normalizedUserName) => $"USERNAME#{normalizedUserName}";

  /// <summary>Users may share an email, so the partition holds one membership item per user.</summary>
  public static string Email(string normalizedEmail) => $"EMAIL#{normalizedEmail}";

  public static string Login(string loginProvider, string providerKey) => $"LOGIN#{loginProvider}#{providerKey}";

  public static string Passkey(string credentialId) => $"PASSKEY#{credentialId}";

  public static string RoleName(string normalizedRoleName) => $"ROLENAME#{normalizedRoleName}";

  public static string ClaimMembers(string claimType, string claimValue) => $"CLAIM#{claimType}#{claimValue}";

  public static string RoleMembers(string roleName) => $"USERROLE#{roleName}";

  /// <summary>The sort key of a user's membership item.</summary>
  public static string Member(string userId) => $"USER#{userId}";
}

/// <summary>A pointer from a unique value to the id of the user or role it belongs to.</summary>
internal sealed class LookupItem : IDynamoDbItem
{
  public LookupItem() { }

  public LookupItem(string partitionKey, string id, string? name = default)
  {
    PartitionKey = partitionKey;
    Id = id;
    Name = name;
  }

  public string PartitionKey { get; set; } = "";

  public string? SortKey => Lookups.SortKey;

  /// <summary>The id of the user or role.</summary>
  public string Id { get; set; } = "";

  /// <summary>A role's name, so that resolving a role name needs no second read.</summary>
  public string? Name { get; set; }

  void IDynamoDbItem.Write(Dictionary<string, AttributeValue> item)
  {
    item.Set("PartitionKey", PartitionKey);
    item.Set("SortKey", SortKey);
    item.Set("Id", Id);
    item.Set("Name", Name);
  }

  void IDynamoDbItem.Read(Dictionary<string, AttributeValue> item)
  {
    PartitionKey = item.GetString("PartitionKey") ?? "";
    Id = item.GetString("Id") ?? "";
    Name = item.GetString("Name");
  }
}

/// <summary>One user in the partition of a claim value, a role name or an email.</summary>
internal sealed class MembershipItem : IDynamoDbItem
{
  public MembershipItem() { }

  public MembershipItem(string partitionKey, string userId)
  {
    PartitionKey = partitionKey;
    UserId = userId;
  }

  public string PartitionKey { get; set; } = "";

  public string? SortKey => Lookups.Member(UserId);

  public string UserId { get; set; } = "";

  void IDynamoDbItem.Write(Dictionary<string, AttributeValue> item)
  {
    item.Set("PartitionKey", PartitionKey);
    item.Set("SortKey", SortKey);
    item.Set("UserId", UserId);
  }

  void IDynamoDbItem.Read(Dictionary<string, AttributeValue> item)
  {
    PartitionKey = item.GetString("PartitionKey") ?? "";
    UserId = item.GetString("UserId") ?? "";
  }
}
