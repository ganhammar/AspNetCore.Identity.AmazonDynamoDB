using Amazon.DynamoDBv2.DataModel;

namespace AspNetCore.Identity.AmazonDynamoDB;

[DynamoDBTable(Constants.DefaultTableName)]
public class DynamoDbUserRole
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
    get => $"ROLE#{RoleName}";
    private set { }
  }
  public string? UserId { get; set; }
  public string? RoleName { get; set; }
}
