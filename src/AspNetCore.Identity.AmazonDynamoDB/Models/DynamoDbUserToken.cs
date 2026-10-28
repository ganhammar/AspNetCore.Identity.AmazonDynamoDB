using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;

namespace AspNetCore.Identity.AmazonDynamoDB;

[DynamoDBTable(Constants.DefaultTableName)]
public class DynamoDbUserToken : IDynamoDbItem
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
    get => $"TOKEN#{LoginProvider}-{Name}";
    set { }
  }
  public virtual string? UserId { get; set; }
  public virtual string? LoginProvider { get; set; }
  public virtual string? Name { get; set; }
  public virtual string? Value { get; set; }

  void IDynamoDbItem.Write(Dictionary<string, AttributeValue> item)
  {
    item.Set("PartitionKey", PartitionKey);
    item.Set("SortKey", SortKey);
    item.Set("UserId", UserId);
    item.Set("LoginProvider", LoginProvider);
    item.Set("Name", Name);
    item.Set("Value", Value);
  }

  void IDynamoDbItem.Read(Dictionary<string, AttributeValue> item)
  {
    UserId = item.GetString("UserId");
    LoginProvider = item.GetString("LoginProvider");
    Name = item.GetString("Name");
    Value = item.GetString("Value");
  }
}
