using System.Diagnostics;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AspNetCore.Identity.AmazonDynamoDB.UpgradeTests;

// Runs the seeder built against version 2 to fill a table, then migrates it with this version:
// EnsureInitialized (whose warning is kept), MigrateAsync twice and RemoveLegacyIndexesAsync
public sealed class LegacyTableFixture : IAsyncLifetime
{
  public string TableName { get; } = $"upgrade-{Guid.NewGuid()}";

  public AmazonDynamoDBClient Client { get; } = new(
    new BasicAWSCredentials("test", "test"),
    new AmazonDynamoDBConfig { ServiceURL = Environment.GetEnvironmentVariable("DYNAMODB_ENDPOINT") ?? "http://localhost:8000" });

  public Seed Seed { get; private set; } = default!;

  public MigrationResult FirstMigration { get; private set; } = default!;

  public MigrationResult SecondMigration { get; private set; } = default!;

  public List<(LogLevel Level, string Message)> SetupLog { get; } = [];

  public int LegacyIndexCountBeforeRemoval { get; private set; }

  public ServiceProvider Services { get; private set; } = default!;

  public async Task InitializeAsync()
  {
    Seed = await RunSeeder();

    var services = new ServiceCollection();
    services.AddLogging(logging => logging.AddProvider(new ListLoggerProvider(SetupLog)));
    services.AddSingleton<IAmazonDynamoDB>(Client);
    services.AddDataProtection();
    services
      .AddIdentityCore<DynamoDbUser>(options => options.User.RequireUniqueEmail = false)
      .AddRoles<DynamoDbRole>()
      .AddDynamoDbStores<DynamoDbUser, DynamoDbRole>()
      .SetDefaultTableName(TableName)
      .ResolveRoleNames();
    Services = services.BuildServiceProvider();

    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(Services);
    FirstMigration = await AspNetCoreIdentityDynamoDbSetup.MigrateAsync(Services);
    SecondMigration = await AspNetCoreIdentityDynamoDbSetup.MigrateAsync(Services);
    LegacyIndexCountBeforeRemoval = (await Client.DescribeTableAsync(TableName)).Table.GlobalSecondaryIndexes?.Count ?? 0;
    await AspNetCoreIdentityDynamoDbSetup.RemoveLegacyIndexesAsync(Services);
  }

  public async Task DisposeAsync()
  {
    try
    {
      await Client.DeleteTableAsync(TableName);
    }
    catch (Amazon.DynamoDBv2.Model.ResourceNotFoundException)
    {
      // The seeder failed before creating the table
    }

    if (Services is not null)
    {
      await Services.DisposeAsync();
    }

    Client.Dispose();
  }

  private async Task<Seed> RunSeeder()
  {
    var seederProject = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LegacySeeder"));
    var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
    var startInfo = new ProcessStartInfo(dotnet)
    {
      ArgumentList = { "run", "--project", seederProject, "--", TableName },
      RedirectStandardOutput = true,
      RedirectStandardError = true,
    };
    using var process = Process.Start(startInfo) ?? throw new Exception("Could not start the seeder");
    var output = await process.StandardOutput.ReadToEndAsync();
    var error = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();

    if (process.ExitCode != 0)
    {
      throw new Exception($"The seeder failed with exit code {process.ExitCode}:{Environment.NewLine}{output}{Environment.NewLine}{error}");
    }

    var json = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Last(x => x.TrimStart().StartsWith('{'));
    return JsonSerializer.Deserialize<Seed>(json) ?? throw new Exception("The seeder printed no seed");
  }

  private sealed class ListLoggerProvider(List<(LogLevel Level, string Message)> entries) : ILoggerProvider
  {
    public ILogger CreateLogger(string categoryName) => new ListLogger(entries);

    public void Dispose()
    {
    }

    private sealed class ListLogger(List<(LogLevel Level, string Message)> entries) : ILogger
    {
      public IDisposable? BeginScope<TState>(TState state) where TState : notnull => default;

      public bool IsEnabled(LogLevel logLevel) => true;

      public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
      {
        lock (entries)
        {
          entries.Add((logLevel, formatter(state, exception)));
        }
      }
    }
  }
}

public sealed class Seed
{
  public List<SeedRole> Roles { get; set; } = [];
  public List<SeedUser> Users { get; set; } = [];
}

public sealed record SeedRole(string Id, string Name);

public sealed record SeedClaim(string Type, string Value);

public sealed record SeedUser(
  string Id, string UserName, string Email, string Password, string Role, List<SeedClaim> Claims,
  string LoginProvider, string ProviderKey, string PasskeyCredentialId, string TokenValue);
