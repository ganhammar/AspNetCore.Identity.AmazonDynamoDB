using Amazon.DynamoDBv2.DataModel;

namespace AspNetCore.Identity.AmazonDynamoDB;

[DynamoDBTable(Constants.DefaultTableName)]
public class DynamoDbUserToken
{
  [DynamoDBHashKey]
  public string PartitionKey
  {
    get => $"USER#{UserId}";
    private set { }
  }
  [DynamoDBRangeKey]
  public string SortKey
  {
    get => $"TOKEN#{LoginProvider}-{Name}";
    private set { }
  }
  public virtual string? UserId { get; set; }
  public virtual string? LoginProvider { get; set; }
  public virtual string? Name { get; set; }
  public virtual string? Value { get; set; }
}
