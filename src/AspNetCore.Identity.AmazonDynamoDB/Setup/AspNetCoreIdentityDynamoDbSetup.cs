using Amazon.DynamoDBv2;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AspNetCore.Identity.AmazonDynamoDB;

public static class AspNetCoreIdentityDynamoDbSetup
{
  public static void EnsureInitialized(IServiceProvider services)
  {
    var database = services.GetService<IAmazonDynamoDB>();

    EnsureInitialized(
        services.GetRequiredService<IOptionsMonitor<DynamoDbOptions>>(),
        database);
  }

  public static async Task EnsureInitializedAsync(
      IServiceProvider services,
      CancellationToken cancellationToken = default)
  {
    var database = services.GetService<IAmazonDynamoDB>();

    await EnsureInitializedAsync(
        services.GetRequiredService<IOptionsMonitor<DynamoDbOptions>>(),
        database,
        cancellationToken);
  }

  public static async Task EnsureInitializedAsync(
      IOptionsMonitor<DynamoDbOptions> options,
      IAmazonDynamoDB? database = default,
      CancellationToken cancellationToken = default)
  {
    var promises = new[]
    {
            DynamoDbUserSetup.EnsureInitializedAsync(
                options.CurrentValue, database),
            DynamoDbRoleSetup.EnsureInitializedAsync(
                options.CurrentValue, database),
        };

    await Task.WhenAll(promises);
  }

  public static void EnsureInitialized(
      IOptionsMonitor<DynamoDbOptions> options,
      IAmazonDynamoDB? database = default)
  {
    EnsureInitializedAsync(options, database).GetAwaiter().GetResult();
  }
}
