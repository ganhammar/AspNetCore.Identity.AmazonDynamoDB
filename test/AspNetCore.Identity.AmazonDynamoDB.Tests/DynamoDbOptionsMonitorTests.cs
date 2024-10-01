using Amazon;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace AspNetCore.Identity.AmazonDynamoDB.Tests;

public class DynamoDbOptionsMonitorTests
{
  private class TestOptionsMonitor<T> : IOptionsMonitor<T>
  {
    private T _currentValue;
    private readonly List<Action<T, string>> _listeners = new();

    public TestOptionsMonitor(T currentValue)
    {
      _currentValue = currentValue;
    }

    public T CurrentValue => _currentValue;

    public T Get(string? name) => _currentValue;

    public IDisposable OnChange(Action<T, string> listener)
    {
      _listeners.Add(listener);
      return new Mock<IDisposable>().Object;
    }

    public void Set(T value)
    {
      _currentValue = value;
      foreach (var listener in _listeners)
      {
        listener(value, string.Empty);
      }
    }
  }

  [Fact]
  public void EnsureAliasCreated_IsCalled_WhenOptionsChange()
  {
    // Arrange
    var initialOptions = new DynamoDbOptions { DefaultTableName = "identity" };
    var optionsMonitor = new TestOptionsMonitor<DynamoDbOptions>(initialOptions);
    var _ = new DynamoDbOptionsMonitor(optionsMonitor);

    // Act
    var newOptions = new DynamoDbOptions { DefaultTableName = "CustomTableName" };
    optionsMonitor.Set(newOptions);

    // Assert
    Assert.True(AWSConfigsDynamoDB.Context.TableAliases.ContainsKey("identity"));
    Assert.Equal("CustomTableName", AWSConfigsDynamoDB.Context.TableAliases["identity"]);
  }
}
