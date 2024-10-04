using Microsoft.Extensions.Options;

namespace AspNetCore.Identity.AmazonDynamoDB;

public class DynamoDbOptionsMonitor
{
  private readonly IOptionsMonitor<DynamoDbOptions> _optionsMonitor;

  public DynamoDbOptionsMonitor(IOptionsMonitor<DynamoDbOptions> optionsMonitor)
  {
    ArgumentNullException.ThrowIfNull(optionsMonitor);

    _optionsMonitor = optionsMonitor;

    if (_optionsMonitor.CurrentValue.DefaultTableName is not null)
    {
      DynamoDbTableSetup.EnsureAliasCreated(_optionsMonitor.CurrentValue);
    }

    _optionsMonitor.OnChange((options, _) =>
    {
      DynamoDbTableSetup.EnsureAliasCreated(options);
    });
  }
}
