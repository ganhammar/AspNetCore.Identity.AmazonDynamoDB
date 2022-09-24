using Microsoft.Extensions.Options;

namespace AspNetCore.Identity.AmazonDynamoDB;

public static class DynamoDbSetup
{
    public static async Task EnsureInitializedAsync(
        IOptionsMonitor<DynamoDbOptions> options,
        CancellationToken cancellationToken = default)
    {
        var promises = new[]
        {
            DynamoDbUserSetup.EnsureInitializedAsync(
                options.CurrentValue),
        };

        await Task.WhenAll(promises);
    }

    public static void EnsureInitialized(IOptionsMonitor<DynamoDbOptions> options)
    {
        EnsureInitializedAsync(options).GetAwaiter().GetResult();
    }
}