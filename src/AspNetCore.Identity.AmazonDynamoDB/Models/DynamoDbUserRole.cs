using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;

namespace AspNetCore.Identity.AmazonDynamoDB;

[DynamoDBTable(Constants.DefaultTableName)]
public class DynamoDbUserRole : IDynamoDbItem
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
    get => $"ROLE#{RoleName}";
    set { }
  }
  public string? UserId { get; set; }
  public string? RoleName { get; set; }

  void IDynamoDbItem.Write(Dictionary<string, AttributeValue> item)
  {
    item.Set("PartitionKey", PartitionKey);
    item.Set("SortKey", SortKey);
    item.Set("UserId", UserId);
    item.Set("RoleName", RoleName);
  }

  void IDynamoDbItem.Read(Dictionary<string, AttributeValue> item)
  {
    UserId = item.GetString("UserId");
    RoleName = item.GetString("RoleName");
  }
}
