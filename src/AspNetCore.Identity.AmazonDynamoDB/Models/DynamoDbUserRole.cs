using Amazon.DynamoDBv2.DataModel;

namespace AspNetCore.Identity.AmazonDynamoDB;

[DynamoDBTable(Constants.DefaultUserRolesTableName)]
public class DynamoDbRoleUser
{
    [DynamoDBHashKey]
    public string? UserId { get; set; }
    [DynamoDBRangeKey]
    public string? RoleName { get; set; }
}