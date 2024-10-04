using Microsoft.Extensions.Options;

namespace AspNetCore.Identity.AmazonDynamoDB.Tests;

public class TestDynamoDbOptionsMonitor : IOptionsMonitor<DynamoDbOptions>
{
  private DynamoDbOptions _options;
  private readonly List<Action<DynamoDbOptions, string>> _listeners = new();

  public TestDynamoDbOptionsMonitor(DynamoDbOptions initialValue)
  {
    _options = initialValue;
  }

  public DynamoDbOptions CurrentValue => _options;
  public DynamoDbOptions Get(string? name) => _options;

  public IDisposable OnChange(Action<DynamoDbOptions, string> listener)
  {
    _listeners.Add(listener);
    return new ChangeToken(() => _listeners.Remove(listener));
  }

  public void UpdateOptions(DynamoDbOptions newOptions)
  {
    _options = newOptions;
    foreach (var listener in _listeners)
    {
      listener.Invoke(newOptions, string.Empty);
    }
  }

  private class ChangeToken : IDisposable
  {
    private readonly Action _disposeAction;

    public ChangeToken(Action disposeAction)
    {
      _disposeAction = disposeAction;
    }

    public void Dispose()
    {
      _disposeAction();
    }
  }
}
