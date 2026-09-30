using Amazon.DynamoDBv2;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AspNetCore.Identity.AmazonDynamoDB;

public static class AspNetCoreIdentityDynamoDbSetup
{
  public static void EnsureInitialized(IServiceProvider services)
  {
    EnsureInitializedAsync(services)
      .GetAwaiter().GetResult();
  }

  public static async Task EnsureInitializedAsync(
    IServiceProvider services,
    CancellationToken cancellationToken = default)
  {
    var database = services.GetService<IAmazonDynamoDB>();
    var logger = services.GetService<ILoggerFactory>()?.CreateLogger(typeof(AspNetCoreIdentityDynamoDbSetup))
      ?? NullLogger.Instance;

    await DynamoDbTableSetup.EnsureInitializedAsync(
      services.GetRequiredService<IOptionsMonitor<DynamoDbOptions>>().CurrentValue,
      database,
      logger,
      cancellationToken);
  }

  public static async Task EnsureInitializedAsync(
    IOptionsMonitor<DynamoDbOptions> options,
    IAmazonDynamoDB? database = default,
    CancellationToken cancellationToken = default)
  {
    await DynamoDbTableSetup.EnsureInitializedAsync(
      options.CurrentValue, database, cancellationToken);
  }

  public static void EnsureInitialized(
    IOptionsMonitor<DynamoDbOptions> options,
    IAmazonDynamoDB? database = default)
  {
    EnsureInitializedAsync(options, database)
      .GetAwaiter().GetResult();
  }
}
