using Amazon.DynamoDBv2.DataModel;

namespace AspNetCore.Identity.AmazonDynamoDB;

[DynamoDBTable(Constants.DefaultUserTokensTableName)]
public class DynamoDbUserToken
{
    [DynamoDBHashKey]
    public string Id
    { 
        get => $"{UserId}#{LoginProvider}#{Name}";
        set { }
    }
    public virtual string? UserId { get; set; }
    public virtual string? LoginProvider { get; set; }
    public virtual string? Name { get; set; }
    public virtual string? Value { get; set; }
}