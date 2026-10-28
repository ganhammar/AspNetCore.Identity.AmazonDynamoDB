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
    await DynamoDbTableSetup.EnsureInitializedAsync(
      Options(services),
      services.GetService<IAmazonDynamoDB>(),
      Logger(services),
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

  /// <summary>Writes the lookup items a table created by version 2 lacks; see <see cref="DynamoDbTableSetup.MigrateAsync(DynamoDbOptions, IAmazonDynamoDB?, CancellationToken)"/>.</summary>
  public static Task<MigrationResult> MigrateAsync(
    IServiceProvider services,
    CancellationToken cancellationToken = default)
    => DynamoDbTableSetup.MigrateAsync(
      Options(services),
      services.GetService<IAmazonDynamoDB>(),
      Logger(services),
      cancellationToken);

  public static Task<MigrationResult> MigrateAsync(
    IOptionsMonitor<DynamoDbOptions> options,
    IAmazonDynamoDB? database = default,
    CancellationToken cancellationToken = default)
    => DynamoDbTableSetup.MigrateAsync(options.CurrentValue, database, cancellationToken);

  /// <summary>Removes the global secondary indexes of version 2; see <see cref="DynamoDbTableSetup.RemoveLegacyIndexesAsync(DynamoDbOptions, IAmazonDynamoDB?, CancellationToken)"/>.</summary>
  public static Task RemoveLegacyIndexesAsync(
    IServiceProvider services,
    CancellationToken cancellationToken = default)
    => DynamoDbTableSetup.RemoveLegacyIndexesAsync(
      Options(services),
      services.GetService<IAmazonDynamoDB>(),
      Logger(services),
      cancellationToken);

  public static Task RemoveLegacyIndexesAsync(
    IOptionsMonitor<DynamoDbOptions> options,
    IAmazonDynamoDB? database = default,
    CancellationToken cancellationToken = default)
    => DynamoDbTableSetup.RemoveLegacyIndexesAsync(options.CurrentValue, database, cancellationToken);

  private static DynamoDbOptions Options(IServiceProvider services)
    => services.GetRequiredService<IOptionsMonitor<DynamoDbOptions>>().CurrentValue;

  private static ILogger Logger(IServiceProvider services)
    => services.GetService<ILoggerFactory>()?.CreateLogger(typeof(AspNetCoreIdentityDynamoDbSetup)) ?? NullLogger.Instance;
}
