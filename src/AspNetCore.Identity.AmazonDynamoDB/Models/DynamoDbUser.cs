using Amazon.DynamoDBv2.DataModel;
using Microsoft.AspNetCore.Identity;

namespace AspNetCore.Identity.AmazonDynamoDB;

[DynamoDBTable(Constants.DefaultTableName)]
public class DynamoDbUser : IdentityUser
{
  [DynamoDBHashKey]
  public string PartitionKey
  {
    get => $"USER#{Id}";
    set { }
  }
  [DynamoDBRangeKey]
  public string? SortKey
  {
    get => $"#USER#{Id}";
    set { }
  }
  [DynamoDBProperty(typeof(DateTimeOffsetConverter))]
  public override DateTimeOffset? LockoutEnd { get; set; }
  [DynamoDBIgnore]
  public Dictionary<string, List<string>> Claims { get; set; } = new();
  [DynamoDBIgnore]
  public List<DynamoDbUserLogin> Logins { get; set; } = new();
  [DynamoDBIgnore]
  public List<string> Roles { get; set; } = new();
  [DynamoDBIgnore]
  public List<IdentityUserToken<string>> Tokens { get; set; }
    = new List<IdentityUserToken<string>>();
}
