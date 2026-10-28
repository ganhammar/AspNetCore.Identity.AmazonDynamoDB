using Amazon.DynamoDBv2.Model;

namespace AspNetCore.Identity.AmazonDynamoDB.Tests;

public class CustomUser : DynamoDbUser
{
  public string? ProfilePictureUrl { get; set; }

  protected override void WriteAttributes(Dictionary<string, AttributeValue> item)
  {
    base.WriteAttributes(item);
    item.Set("ProfilePictureUrl", ProfilePictureUrl);
  }

  protected override void ReadAttributes(Dictionary<string, AttributeValue> item)
  {
    base.ReadAttributes(item);
    ProfilePictureUrl = item.GetString("ProfilePictureUrl");
  }
}
