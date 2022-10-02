using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AspNetCore.Identity.AmazonDynamoDB;

public static class AspNetCoreIdentityDynamoDbSetup
{
    public static void EnsureInitialized(IServiceProvider services)
    {
        EnsureInitialized(services.GetRequiredService<IOptionsMonitor<DynamoDbOptions>>());
    }

    public static async Task EnsureInitializedAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(
            services.GetRequiredService<IOptionsMonitor<DynamoDbOptions>>(),
            cancellationToken);
    }

    public static async Task EnsureInitializedAsync(
        IOptionsMonitor<DynamoDbOptions> options,
        CancellationToken cancellationToken = default)
    {
        var promises = new[]
        {
            DynamoDbUserSetup.EnsureInitializedAsync(
                options.CurrentValue),
            DynamoDbRoleSetup.EnsureInitializedAsync(
                options.CurrentValue),
        };

        await Task.WhenAll(promises);
    }

    public static void EnsureInitialized(IOptionsMonitor<DynamoDbOptions> options)
    {
        EnsureInitializedAsync(options).GetAwaiter().GetResult();
    }
}