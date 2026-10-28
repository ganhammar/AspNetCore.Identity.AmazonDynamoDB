using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Microsoft.AspNetCore.Identity;

namespace AspNetCore.Identity.AmazonDynamoDB;

[DynamoDBTable(Constants.DefaultTableName)]
public class DynamoDbRole : IdentityRole, IDynamoDbItem
{
  [DynamoDBHashKey]
  public string PartitionKey
  {
    get => $"ROLE#{Id}";
    set { }
  }
  [DynamoDBRangeKey]
  public string? SortKey
  {
    get => $"#ROLE#{Id}";
    set { }
  }
  public Dictionary<string, List<string>> Claims { get; set; }
    = new Dictionary<string, List<string>>();

  /// <summary>
  /// Writes the role's attributes to the item the store saves. A derived type with properties of
  /// its own overrides this, calls the base implementation and adds them, using the
  /// <see cref="AttributeValues"/> extension methods.
  /// </summary>
  protected virtual void WriteAttributes(Dictionary<string, AttributeValue> item)
  {
    ArgumentNullException.ThrowIfNull(item);

    item.Set("PartitionKey", PartitionKey);
    item.Set("SortKey", SortKey);
    item.Set("Id", Id);
    item.Set("Name", Name);
    item.Set("NormalizedName", NormalizedName);
    item.Set("ConcurrencyStamp", ConcurrencyStamp);
    item.SetStringSetMap("Claims", Claims);
  }

  /// <summary>
  /// Reads the role's attributes from the item the store loaded. A derived type with properties of
  /// its own overrides this, calls the base implementation and reads them, using the
  /// <see cref="AttributeValues"/> extension methods.
  /// </summary>
  protected virtual void ReadAttributes(Dictionary<string, AttributeValue> item)
  {
    ArgumentNullException.ThrowIfNull(item);

    Id = item.GetString("Id") ?? Id;
    Name = item.GetString("Name");
    NormalizedName = item.GetString("NormalizedName");
    ConcurrencyStamp = item.GetString("ConcurrencyStamp") ?? ConcurrencyStamp;
    Claims = item.GetStringSetMap("Claims") ?? new Dictionary<string, List<string>>();
  }

  void IDynamoDbItem.Write(Dictionary<string, AttributeValue> item) => WriteAttributes(item);

  void IDynamoDbItem.Read(Dictionary<string, AttributeValue> item) => ReadAttributes(item);
}
