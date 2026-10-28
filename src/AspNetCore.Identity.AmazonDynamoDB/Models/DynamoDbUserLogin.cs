using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;

namespace AspNetCore.Identity.AmazonDynamoDB;

[DynamoDBTable(Constants.DefaultTableName)]
public class DynamoDbUserLogin : IDynamoDbItem
{
  [DynamoDBHashKey]
  public string PartitionKey
  {
    get => $"USER#{UserId}";
    set { }
  }
  [DynamoDBRangeKey]
  public string SortKey
  {
    get => $"LOGIN#{LoginProvider}-{ProviderKey}";
    set { }
  }
  public string? LoginProvider { get; set; }
  public string? ProviderKey { get; set; }
  public string? ProviderDisplayName { get; set; }
  public string? UserId { get; set; }

  void IDynamoDbItem.Write(Dictionary<string, AttributeValue> item)
  {
    item.Set("PartitionKey", PartitionKey);
    item.Set("SortKey", SortKey);
    item.Set("LoginProvider", LoginProvider);
    item.Set("ProviderKey", ProviderKey);
    item.Set("ProviderDisplayName", ProviderDisplayName);
    item.Set("UserId", UserId);
  }

  void IDynamoDbItem.Read(Dictionary<string, AttributeValue> item)
  {
    LoginProvider = item.GetString("LoginProvider");
    ProviderKey = item.GetString("ProviderKey");
    ProviderDisplayName = item.GetString("ProviderDisplayName");
    UserId = item.GetString("UserId");
  }
}
