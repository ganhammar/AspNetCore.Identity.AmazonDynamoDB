using Amazon.DynamoDBv2.DataModel;

namespace AspNetCore.Identity.AmazonDynamoDB;

[DynamoDBTable(Constants.DefaultTableName)]
public class DynamoDbUserLogin
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
}
