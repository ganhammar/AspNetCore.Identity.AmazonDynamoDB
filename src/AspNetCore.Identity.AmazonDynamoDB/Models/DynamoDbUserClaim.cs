using Amazon.DynamoDBv2.DataModel;

namespace AspNetCore.Identity.AmazonDynamoDB;

[DynamoDBTable(Constants.DefaultUserClaimsTableName)]
public class DynamoDbUserClaim
{
    [DynamoDBHashKey]
    public string? ClaimType { get; set; }
    [DynamoDBRangeKey]
    public string? UserId { get; set; }
    public string? ClaimValue { get; set; }
}