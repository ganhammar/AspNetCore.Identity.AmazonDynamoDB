namespace AspNetCore.Identity.AmazonDynamoDB.Tests;

public class CustomUser : DynamoDbUser
{
  public string? ProfilePictureUrl { get; set; }
}
