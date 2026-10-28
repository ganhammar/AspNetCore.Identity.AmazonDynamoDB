using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;

namespace AspNetCore.Identity.AmazonDynamoDB;

[DynamoDBTable(Constants.DefaultTableName)]
public class DynamoDbUserClaim : IDynamoDbItem
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
    get => $"CLAIM#{ClaimType}-{ClaimValue}";
    set { }
  }
  public string? ClaimType { get; set; }
  public string? UserId { get; set; }
  public string? ClaimValue { get; set; }

  void IDynamoDbItem.Write(Dictionary<string, AttributeValue> item)
  {
    item.Set("PartitionKey", PartitionKey);
    item.Set("SortKey", SortKey);
    item.Set("ClaimType", ClaimType);
    item.Set("UserId", UserId);
    item.Set("ClaimValue", ClaimValue);
  }

  void IDynamoDbItem.Read(Dictionary<string, AttributeValue> item)
  {
    ClaimType = item.GetString("ClaimType");
    UserId = item.GetString("UserId");
    ClaimValue = item.GetString("ClaimValue");
  }
}
