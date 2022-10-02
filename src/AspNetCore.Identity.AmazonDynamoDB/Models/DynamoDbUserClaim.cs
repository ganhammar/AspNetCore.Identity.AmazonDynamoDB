using Amazon.DynamoDBv2.DataModel;

namespace AspNetCore.Identity.AmazonDynamoDB;

[DynamoDBTable(Constants.DefaultUserClaimsTableName)]
public class DynamoDbUserClaim
{
    [DynamoDBHashKey]
    public string Id
    { 
        get => $"{UserId}#{ClaimType}#{ClaimValue}";
        set { }
    }
    public string? ClaimType { get; set; }
    public string? UserId { get; set; }
    public string? ClaimValue { get; set; }
}