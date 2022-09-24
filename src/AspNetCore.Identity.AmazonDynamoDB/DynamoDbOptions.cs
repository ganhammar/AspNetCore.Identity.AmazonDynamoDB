using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace AspNetCore.Identity.AmazonDynamoDB;

public class DynamoDbOptions
{
    public string UsersTableName { get; set; } = Constants.DefaultUsersTableName;
    public string UserClaimsTableName { get; set; } = Constants.DefaultUserClaimsTableName;
    public string UserLoginsTableName { get; set; } = Constants.DefaultUserLoginsTableName;
    public IAmazonDynamoDB? Database { get; set; }
    public ProvisionedThroughput ProvisionedThroughput { get; set; } = new ProvisionedThroughput
    {
        ReadCapacityUnits = 1,
        WriteCapacityUnits = 1,
    };
    public BillingMode BillingMode { get; set; } = BillingMode.PAY_PER_REQUEST;
}