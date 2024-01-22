using Amazon.DynamoDBv2.DataModel;
using Microsoft.AspNetCore.Identity;

namespace AspNetCore.Identity.AmazonDynamoDB;

[DynamoDBTable(Constants.DefaultTableName)]
public class DynamoDbRole : IdentityRole
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
}
