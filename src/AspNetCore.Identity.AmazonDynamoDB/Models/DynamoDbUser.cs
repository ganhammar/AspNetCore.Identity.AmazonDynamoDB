using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Microsoft.AspNetCore.Identity;

namespace AspNetCore.Identity.AmazonDynamoDB;

[DynamoDBTable(Constants.DefaultTableName)]
public class DynamoDbUser : IdentityUser, IDynamoDbItem
{
  [DynamoDBHashKey]
  public string PartitionKey
  {
    get => $"USER#{Id}";
    set { }
  }
  [DynamoDBRangeKey]
  public string? SortKey
  {
    get => $"#USER#{Id}";
    set { }
  }
  [DynamoDBProperty(typeof(DateTimeOffsetConverter))]
  public override DateTimeOffset? LockoutEnd { get; set; }
  [DynamoDBIgnore]
  public Dictionary<string, List<string>>? Claims { get; set; }
  [DynamoDBIgnore]
  public List<DynamoDbUserLogin>? Logins { get; set; }
  [DynamoDBIgnore]
  public List<string>? Roles { get; set; }
  [DynamoDBIgnore]
  public List<IdentityUserToken<string>>? Tokens { get; set; }
  [DynamoDBIgnore]
  public List<DynamoDbUserPasskey>? Passkeys { get; set; }

  /// <summary>
  /// Writes the user's attributes to the item the store saves. A derived type with properties of
  /// its own overrides this, calls the base implementation and adds them, using the
  /// <see cref="AttributeValues"/> extension methods.
  /// </summary>
  protected virtual void WriteAttributes(Dictionary<string, AttributeValue> item)
  {
    ArgumentNullException.ThrowIfNull(item);

    item.Set("PartitionKey", PartitionKey);
    item.Set("SortKey", SortKey);
    item.Set("Id", Id);
    item.Set("UserName", UserName);
    item.Set("NormalizedUserName", NormalizedUserName);
    item.Set("Email", Email);
    item.Set("NormalizedEmail", NormalizedEmail);
    item.Set("EmailConfirmed", EmailConfirmed);
    item.Set("PasswordHash", PasswordHash);
    item.Set("SecurityStamp", SecurityStamp);
    item.Set("ConcurrencyStamp", ConcurrencyStamp);
    item.Set("PhoneNumber", PhoneNumber);
    item.Set("PhoneNumberConfirmed", PhoneNumberConfirmed);
    item.Set("TwoFactorEnabled", TwoFactorEnabled);
    item.Set("LockoutEnd", LockoutEnd);
    item.Set("LockoutEnabled", LockoutEnabled);
    item.Set("AccessFailedCount", (long)AccessFailedCount);
  }

  /// <summary>
  /// Reads the user's attributes from the item the store loaded. A derived type with properties of
  /// its own overrides this, calls the base implementation and reads them, using the
  /// <see cref="AttributeValues"/> extension methods.
  /// </summary>
  protected virtual void ReadAttributes(Dictionary<string, AttributeValue> item)
  {
    ArgumentNullException.ThrowIfNull(item);

    Id = item.GetString("Id") ?? Id;
    UserName = item.GetString("UserName");
    NormalizedUserName = item.GetString("NormalizedUserName");
    Email = item.GetString("Email");
    NormalizedEmail = item.GetString("NormalizedEmail");
    EmailConfirmed = item.GetBoolean("EmailConfirmed");
    PasswordHash = item.GetString("PasswordHash");
    SecurityStamp = item.GetString("SecurityStamp") ?? SecurityStamp;
    ConcurrencyStamp = item.GetString("ConcurrencyStamp") ?? ConcurrencyStamp;
    PhoneNumber = item.GetString("PhoneNumber");
    PhoneNumberConfirmed = item.GetBoolean("PhoneNumberConfirmed");
    TwoFactorEnabled = item.GetBoolean("TwoFactorEnabled");
    LockoutEnd = item.GetDateTimeOffset("LockoutEnd");
    LockoutEnabled = item.GetBoolean("LockoutEnabled");
    AccessFailedCount = item.GetInt32("AccessFailedCount");
  }

  void IDynamoDbItem.Write(Dictionary<string, AttributeValue> item) => WriteAttributes(item);

  void IDynamoDbItem.Read(Dictionary<string, AttributeValue> item) => ReadAttributes(item);
}
