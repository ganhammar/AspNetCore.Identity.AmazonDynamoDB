using Amazon.DynamoDBv2.DataModel;
using Microsoft.AspNetCore.Identity;

namespace AspNetCore.Identity.AmazonDynamoDB;

[DynamoDBTable(Constants.DefaultRolesTableName)]
public class DynamoDbRole : IdentityRole
{
    public Dictionary<string, string> Claims { get; set; }
        = new Dictionary<string, string>();
}