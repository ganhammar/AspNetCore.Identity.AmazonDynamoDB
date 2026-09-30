using System.Collections.Concurrent;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

namespace AspNetCore.Identity.AmazonDynamoDB.Tests;

// A client for DynamoDB Local that records the requests that are sent
public class RecordingDynamoDbClient : AmazonDynamoDBClient
{
  public ConcurrentQueue<AmazonWebServiceRequest> Requests { get; } = new();

  public RecordingDynamoDbClient()
    : base(new BasicAWSCredentials("test", "test"), new AmazonDynamoDBConfig
    {
      ServiceURL = "http://localhost:8000",
    })
  {
    BeforeRequestEvent += (_, args) =>
    {
      if (args is not WebServiceRequestEventArgs { Request: { } request })
      {
        return;
      }

      // The SDK clears batch requests once they have been sent, so keep a copy of what was sent
      Requests.Enqueue(request is BatchWriteItemRequest batchWriteItemRequest
        ? new BatchWriteItemRequest
        {
          RequestItems = batchWriteItemRequest.RequestItems.ToDictionary(x => x.Key, x => x.Value.ToList()),
        }
        : request);
    };
  }
}
