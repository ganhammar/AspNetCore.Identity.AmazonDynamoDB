using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using AspNetCore.Identity.AmazonDynamoDB;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

// Usage: LegacySeeder <table name>. Creates the table with version 2 and fills it through version 2's
// stores and Identity's managers, then prints what it wrote as JSON on the last line.
var tableName = args.Length > 0 ? args[0] : throw new ArgumentException("Pass the table name");
var endpoint = Environment.GetEnvironmentVariable("DYNAMODB_ENDPOINT") ?? "http://localhost:8000";
using var client = new AmazonDynamoDBClient(
  new BasicAWSCredentials("test", "test"),
  new AmazonDynamoDBConfig { ServiceURL = endpoint });

var services = new ServiceCollection();
services.AddLogging();
services.AddSingleton<IAmazonDynamoDB>(client);
services.AddDataProtection();
services
  .AddIdentityCore<DynamoDbUser>(options => options.User.RequireUniqueEmail = false)
  .AddRoles<DynamoDbRole>()
  .AddDynamoDbStores()
  .SetDefaultTableName(tableName);

await using var provider = services.BuildServiceProvider();
AspNetCoreIdentityDynamoDbSetup.EnsureInitialized(provider);

var users = provider.GetRequiredService<UserManager<DynamoDbUser>>();
var roles = provider.GetRequiredService<RoleManager<DynamoDbRole>>();
var seed = new Seed();

foreach (var roleName in new[] { "Admin", "Editor" })
{
  var role = new DynamoDbRole { Name = roleName };
  Ensure(await roles.CreateAsync(role), $"create role {roleName}");
  Ensure(await roles.AddClaimAsync(role, new("scope", roleName.ToLowerInvariant())), "add role claim");
  seed.Roles.Add(new(role.Id, roleName));
}

for (var i = 0; i < 12; i++)
{
  var user = new DynamoDbUser
  {
    UserName = $"user-{i}",
    Email = i < 10 ? $"user-{i}@example.com" : "shared@example.com",
    EmailConfirmed = i % 2 == 0,
    PhoneNumber = $"+4670000000{i:D2}",
    LockoutEnabled = true,
  };
  Ensure(await users.CreateAsync(user, $"Passw0rd!{i}"), $"create user {i}");
  Ensure(await users.AddToRoleAsync(user, i % 3 == 0 ? "Admin" : "Editor"), "add to role");
  Ensure(await users.AddClaimsAsync(user, [new("plan", i % 2 == 0 ? "pro" : "free"), new("seat", i.ToString())]), "add claims");
  Ensure(await users.AddLoginAsync(user, new UserLoginInfo("GitHub", $"gh-{i}", "GitHub")), "add login");
  Ensure(await users.SetAuthenticationTokenAsync(user, "Refresh", "token", $"value-{i}"), "set token");
  Ensure(await users.ResetAuthenticatorKeyAsync(user), "set authenticator key");

  var credentialId = Guid.NewGuid().ToByteArray();
  Ensure(await users.AddOrUpdatePasskeyAsync(user, new UserPasskeyInfo(
    credentialId, [1, 2, 3], DateTimeOffset.UtcNow, 0, ["internal"], true, true, false, [4, 5], [6, 7])
  { Name = $"key-{i}" }), "add passkey");

  seed.Users.Add(new(user.Id, user.UserName, user.Email, $"Passw0rd!{i}", i % 3 == 0 ? "Admin" : "Editor",
    [new("plan", i % 2 == 0 ? "pro" : "free"), new("seat", i.ToString())], "GitHub", $"gh-{i}", Convert.ToBase64String(credentialId), $"value-{i}"));
}

Console.WriteLine(JsonSerializer.Serialize(seed));

static void Ensure(IdentityResult result, string what)
{
  if (!result.Succeeded)
  {
    throw new Exception($"Failed to {what}: {string.Join(", ", result.Errors.Select(x => x.Description))}");
  }
}

public sealed class Seed
{
  public List<SeedRole> Roles { get; } = [];
  public List<SeedUser> Users { get; } = [];
}

public sealed record SeedRole(string Id, string Name);

public sealed record SeedClaim(string Type, string Value);

public sealed record SeedUser(
  string Id, string UserName, string Email, string Password, string Role, List<SeedClaim> Claims,
  string LoginProvider, string ProviderKey, string PasskeyCredentialId, string TokenValue);
