using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

namespace AspNetCore.Identity.AmazonDynamoDB.Tests;

// Forwards every call to a real client and records the requests that are sent
public class RecordingDynamoDbClient : DispatchProxy
{
  private IAmazonDynamoDB _client = default!;

  public ConcurrentQueue<AmazonWebServiceRequest> Requests { get; } = new();

  public static (IAmazonDynamoDB Client, RecordingDynamoDbClient Recorder) Create(IAmazonDynamoDB client)
  {
    var proxy = Create<IAmazonDynamoDB, RecordingDynamoDbClient>();
    var recorder = (RecordingDynamoDbClient)(object)proxy;
    recorder._client = client;
    return (proxy, recorder);
  }

  protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
  {
    foreach (var request in args?.OfType<AmazonWebServiceRequest>() ?? [])
    {
      // The SDK clears batch requests once they have been sent, so keep a copy of what was sent
      Requests.Enqueue(request is BatchWriteItemRequest batchWriteItemRequest
        ? new BatchWriteItemRequest
        {
          RequestItems = batchWriteItemRequest.RequestItems.ToDictionary(x => x.Key, x => x.Value.ToList()),
        }
        : request);
    }

    try
    {
      return targetMethod!.Invoke(_client, args);
    }
    catch (TargetInvocationException exception) when (exception.InnerException != default)
    {
      ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
      throw;
    }
  }
}
