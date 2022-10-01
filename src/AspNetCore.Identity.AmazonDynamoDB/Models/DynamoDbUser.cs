using Amazon.DynamoDBv2.DataModel;
using Microsoft.AspNetCore.Identity;

namespace AspNetCore.Identity.AmazonDynamoDB;

[DynamoDBTable(Constants.DefaultUsersTableName)]
public class DynamoDbUser : IdentityUser
{
    public new DateTime? LockoutEnd { get; set; }
    [DynamoDBIgnore]
    public Dictionary<string, List<string>> Claims { get; set; } = new();
    [DynamoDBIgnore]
    public List<DynamoDbUserLogin> Logins { get; set; } = new();
    [DynamoDBIgnore]
    public List<string> Roles { get; set; } = new();
}