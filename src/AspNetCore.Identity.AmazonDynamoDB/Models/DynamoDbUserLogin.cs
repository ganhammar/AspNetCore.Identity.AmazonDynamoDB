using Amazon.DynamoDBv2.DataModel;

namespace AspNetCore.Identity.AmazonDynamoDB;

[DynamoDBTable(Constants.DefaultUserLoginsTableName)]
public class DynamoDbUserLogin
{
    [DynamoDBHashKey]
    public string? LoginProvider { get; set; }
    [DynamoDBRangeKey]
    public string? ProviderKey { get; set; }
    public string? ProviderDisplayName { get; set; }
    public string? UserId { get; set; }
}