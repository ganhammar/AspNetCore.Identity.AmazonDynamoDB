using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AspNetCore.Identity.AmazonDynamoDB.Tests;

// Guards against referencing ASP.NET Core packages from a newer major version than the
// target framework, which replaces shared framework assemblies and breaks Data Protection
[Collection(Constants.DatabaseCollection)]
public class FrameworkCompatibilityTests
{
  private const string Password = "Passw0rd!";

  [Theory]
  [InlineData("Microsoft.Extensions.Identity.Core")]
  [InlineData("Microsoft.Extensions.Identity.Stores")]
  [InlineData("Microsoft.AspNetCore.Cryptography.Internal")]
  [InlineData("Microsoft.AspNetCore.Cryptography.KeyDerivation")]
  public void Should_LoadAssemblyMatchingTheRuntime_When_ReferencingStores(string assemblyName)
  {
    // Act
    var assembly = Assembly.Load(assemblyName);

    // Assert
    Assert.Equal(Environment.Version.Major, assembly.GetName().Version!.Major);
  }

  [Fact]
  public void Should_UnprotectData_When_ReferencingStores()
  {
    // Arrange
    var protector = new EphemeralDataProtectionProvider().CreateProtector("test");

    // Act
    var payload = protector.Unprotect(protector.Protect("payload"));

    // Assert
    Assert.Equal("payload", payload);
  }

  [Fact]
  public async Task Should_AuthenticateWithCookie_When_SignedInWithPassword()
  {
    // Arrange
    await using var app = await StartApplication();
    var userName = await CreateUser(app, roleName: "Admin");
    using var client = CreateClient(app);

    // Act
    var signInResponse = await client.PostAsync($"/sign-in?userName={userName}", default);
    var claimsResponse = await client.GetAsync("/claims");

    // Assert
    Assert.Equal(HttpStatusCode.OK, signInResponse.StatusCode);
    Assert.Equal(HttpStatusCode.OK, claimsResponse.StatusCode);
    var claims = await claimsResponse.Content.ReadFromJsonAsync<Dictionary<string, string[]>>();
    Assert.Equal([userName], claims![ClaimTypes.Name]);
    Assert.Equal(["ADMIN"], claims[ClaimTypes.Role]);
  }

  [Theory]
  [InlineData(false, "ADMIN", false)]
  [InlineData(true, "Admin", true)]
  public async Task Should_AuthorizeByRoleName_When_RoleNamesAreResolved(
    bool resolveRoleNames, string expectedRoleClaim, bool expectedIsInRole)
  {
    // Arrange
    await using var app = await StartApplication(options => options.ResolveRoleNames = resolveRoleNames);
    var userName = await CreateUser(app, roleName: "Admin");
    using var client = CreateClient(app);
    await client.PostAsync($"/sign-in?userName={userName}", default);

    // Act
    var claims = await client.GetFromJsonAsync<Dictionary<string, string[]>>("/claims");
    var isInRole = await client.GetFromJsonAsync<bool>("/is-in-role?role=Admin");

    // Assert
    Assert.Equal([expectedRoleClaim], claims![ClaimTypes.Role]);
    Assert.Equal(expectedIsInRole, isInRole);
  }

  [Fact]
  public async Task Should_NotAuthenticate_When_PasswordIsWrong()
  {
    // Arrange
    await using var app = await StartApplication();
    var userName = await CreateUser(app);
    using var client = CreateClient(app);

    // Act
    var signInResponse = await client.PostAsync($"/sign-in?userName={userName}&password=wrong", default);
    var claimsResponse = await client.GetAsync("/claims");

    // Assert
    Assert.Equal(HttpStatusCode.Unauthorized, signInResponse.StatusCode);
    // .NET 10 no longer redirects API endpoints to the login page
    Assert.Contains(claimsResponse.StatusCode, new[] { HttpStatusCode.Redirect, HttpStatusCode.Unauthorized });
  }

  private static async Task<WebApplication> StartApplication(Action<DynamoDbOptions>? configure = default)
  {
    var builder = WebApplication.CreateBuilder();
    builder.Logging.ClearProviders();
    builder.WebHost.UseUrls("http://127.0.0.1:0");
    builder.Services
      .AddIdentity<DynamoDbUser, DynamoDbRole>()
      .AddDefaultTokenProviders()
      .AddDynamoDbStores()
      .UseDatabase(DatabaseFixture.Client)
      .SetDefaultTableName(DatabaseFixture.TableName)
      .Configure(options => configure?.Invoke(options));
    builder.Services.AddAuthorization();

    var app = builder.Build();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapPost("/sign-in", async (SignInManager<DynamoDbUser> signInManager, string userName, string? password) =>
    {
      var result = await signInManager.PasswordSignInAsync(userName, password ?? Password, false, false);
      return result.Succeeded ? Results.Ok() : Results.Unauthorized();
    });
    app.MapGet("/claims", (ClaimsPrincipal user) => user.Claims
      .GroupBy(x => x.Type)
      .ToDictionary(x => x.Key, x => x.Select(y => y.Value).ToArray()))
      .RequireAuthorization();
    app.MapGet("/is-in-role", (ClaimsPrincipal user, string role) => user.IsInRole(role))
      .RequireAuthorization();

    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(app.Services);
    await app.StartAsync();

    return app;
  }

  private static async Task<string> CreateUser(WebApplication app, string? roleName = default)
  {
    using var scope = app.Services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<DynamoDbUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<DynamoDbRole>>();
    var userName = $"user-{Guid.NewGuid()}";
    var user = new DynamoDbUser { UserName = userName };
    Assert.True((await userManager.CreateAsync(user, Password)).Succeeded);

    if (roleName != default)
    {
      if (await roleManager.FindByNameAsync(roleName) == default)
      {
        Assert.True((await roleManager.CreateAsync(new DynamoDbRole { Name = roleName })).Succeeded);
      }

      Assert.True((await userManager.AddToRoleAsync(user, roleName)).Succeeded);
    }

    return userName;
  }

  private static HttpClient CreateClient(WebApplication app) => new(new HttpClientHandler
  {
    AllowAutoRedirect = false,
    CookieContainer = new(),
  })
  {
    BaseAddress = new Uri(app.Urls.First()),
  };
}
