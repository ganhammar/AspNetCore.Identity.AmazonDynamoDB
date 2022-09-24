using Amazon.DynamoDBv2.DataModel;
using Microsoft.AspNetCore.Identity;

namespace AspNetCore.Identity.AmazonDynamoDB;

[DynamoDBTable(Constants.DefaultUsersTableName)]
public class DynamoDbUser : IdentityUser
{
}