using Amazon.DynamoDBv2.DataModel;

namespace AspNetCore.Identity.AmazonDynamoDB;

[DynamoDBTable(Constants.DefaultTableName)]
public class DynamoDbUserClaim
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
    get => $"CLAIM#{ClaimType}-{ClaimValue}";
    set { }
  }
  public string? ClaimType { get; set; }
  public string? UserId { get; set; }
  public string? ClaimValue { get; set; }
}
