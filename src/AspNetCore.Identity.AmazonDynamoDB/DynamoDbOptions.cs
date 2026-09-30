using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace AspNetCore.Identity.AmazonDynamoDB;

public class DynamoDbOptions
{
  public string DefaultTableName { get; set; } = Constants.DefaultTableName;
  public IAmazonDynamoDB? Database { get; set; }
  public ProvisionedThroughput ProvisionedThroughput { get; set; } = new ProvisionedThroughput
  {
    ReadCapacityUnits = 1,
    WriteCapacityUnits = 1,
  };
  public BillingMode BillingMode { get; set; } = BillingMode.PAY_PER_REQUEST;
  /// <summary>
  /// Users store the normalized names of their roles. When enabled, <c>GetRolesAsync</c> returns
  /// the role names instead (as the Entity Framework Core stores do), so that role claims match
  /// the names used in <c>[Authorize(Roles = "...")]</c>. Each role is looked up once per call,
  /// and roles that can't be found are returned with their normalized name.
  /// </summary>
  public bool ResolveRoleNames { get; set; }
}
