using System.Security.Claims;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using AspNetCore.Identity.AmazonDynamoDB;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// A user with a role, a claim and an external login goes through the stores and comes back out,
// against DynamoDB Local at DYNAMODB_ENDPOINT (default http://localhost:8000)
var endpoint = Environment.GetEnvironmentVariable("DYNAMODB_ENDPOINT") ?? "http://localhost:8000";
var tableName = $"native-aot-{Guid.NewGuid()}";
using var client = new AmazonDynamoDBClient(
  new BasicAWSCredentials("test", "test"),
  new AmazonDynamoDBConfig { ServiceURL = endpoint });

var services = new ServiceCollection();
services.AddLogging(logging => logging.AddConsole());
services.AddSingleton<IAmazonDynamoDB>(client);
services
  .AddIdentityCore<CustomUser>()
  .AddRoles<DynamoDbRole>()
  .AddDynamoDbStores<CustomUser, DynamoDbRole>()
  .SetDefaultTableName(tableName);

await using var provider = services.BuildServiceProvider();
await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(provider);

try
{
  var users = provider.GetRequiredService<UserManager<CustomUser>>();
  var roles = provider.GetRequiredService<RoleManager<DynamoDbRole>>();

  Ensure((await roles.CreateAsync(new DynamoDbRole { Name = "Admin" })).Succeeded, "create role");

  var user = new CustomUser
  {
    UserName = "anna",
    Email = "anna@example.com",
    ProfilePictureUrl = "https://example.com/anna.png",
  };
  Ensure((await users.CreateAsync(user)).Succeeded, "create user");
  Ensure((await users.AddToRoleAsync(user, "Admin")).Succeeded, "add to role");
  Ensure((await users.AddClaimAsync(user, new Claim("plan", "pro"))).Succeeded, "add claim");
  Ensure((await users.AddLoginAsync(user, new UserLoginInfo("GitHub", "42", "GitHub"))).Succeeded, "add login");

  var found = await users.FindByLoginAsync("GitHub", "42")
    ?? throw new Exception("The user was not found by its login");
  Ensure(found.Email == user.Email, "email");
  Ensure(found.ProfilePictureUrl == user.ProfilePictureUrl, "custom attribute");
  Ensure(await users.IsInRoleAsync(found, "Admin"), "role");
  Ensure((await users.GetClaimsAsync(found)).Any(x => x.Type == "plan" && x.Value == "pro"), "claim");

  Console.WriteLine("Native AOT: the stores round-tripped a user with a role, a claim, a login and a custom attribute");
}
finally
{
  await client.DeleteTableAsync(new DeleteTableRequest { TableName = tableName });
}

static void Ensure(bool condition, string what)
{
  if (!condition)
  {
    throw new Exception($"Failed: {what}");
  }
}

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
