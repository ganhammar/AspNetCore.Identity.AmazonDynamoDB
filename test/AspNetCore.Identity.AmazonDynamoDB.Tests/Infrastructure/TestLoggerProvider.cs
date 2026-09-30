using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace AspNetCore.Identity.AmazonDynamoDB.Tests;

public class TestLoggerProvider : ILoggerProvider
{
  public ConcurrentQueue<(LogLevel Level, string Message)> Entries { get; } = new();

  public ILogger CreateLogger(string categoryName) => new TestLogger(Entries);

  public void Dispose()
  {
  }

  private class TestLogger : ILogger
  {
    private readonly ConcurrentQueue<(LogLevel Level, string Message)> _entries;

    public TestLogger(ConcurrentQueue<(LogLevel Level, string Message)> entries)
    {
      _entries = entries;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => default;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
      LogLevel logLevel,
      EventId eventId,
      TState state,
      Exception? exception,
      Func<TState, Exception?, string> formatter)
      => _entries.Enqueue((logLevel, formatter(state, exception)));
  }
}
