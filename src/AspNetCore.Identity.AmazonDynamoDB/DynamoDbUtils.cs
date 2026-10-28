using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;

namespace AspNetCore.Identity.AmazonDynamoDB;

internal class DynamoDbUtils
{
  private const int MaxBatchWriteSize = 25;
  private const int MaxBatchWriteRetries = 8;

  public static async Task WaitForActiveTableAsync(
    IAmazonDynamoDB client, string tableName, ILogger logger, CancellationToken cancellationToken = default)
  {
    bool active;
    do
    {
      var response = await client.DescribeTableAsync(new DescribeTableRequest
      {
        TableName = tableName,
      }, cancellationToken);

      active = Equals(response.Table.TableStatus, TableStatus.ACTIVE)
        && (response.Table.GlobalSecondaryIndexes ?? new())
          .TrueForAll(g => Equals(g.IndexStatus, IndexStatus.ACTIVE));

      if (!active)
      {
        logger.LogInformation("Waiting for table {TableName} to become active", tableName);

        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
      }
    } while (!active);
  }

  // BatchWriteItem accepts at most 25 requests, none of them for the same key, and can leave some
  // of them unprocessed. Of several requests for one key the last is written.
  public static async Task BatchWriteAsync(
    IAmazonDynamoDB client,
    string tableName,
    IEnumerable<WriteRequest> requests,
    CancellationToken cancellationToken = default)
  {
    var distinct = new Dictionary<(string, string?), WriteRequest>();

    foreach (var request in requests)
    {
      var key = request.PutRequest?.Item ?? request.DeleteRequest?.Key ?? throw new ArgumentException("A write request puts or deletes an item", nameof(requests));
      distinct[(key["PartitionKey"].S, key.GetValueOrDefault("SortKey")?.S)] = request;
    }

    foreach (var chunk in distinct.Values.Chunk(MaxBatchWriteSize))
    {
      var unprocessed = chunk.ToList();

      for (var attempt = 0; unprocessed.Count > 0; attempt++)
      {
        if (attempt > MaxBatchWriteRetries)
        {
          throw new Exception($"Couldn't write {unprocessed.Count} items to table {tableName}");
        }

        if (attempt > 0)
        {
          await Task.Delay(TimeSpan.FromMilliseconds(50 * Math.Pow(2, attempt - 1)), cancellationToken);
        }

        var response = await client.BatchWriteItemAsync(new BatchWriteItemRequest
        {
          RequestItems = new()
          {
            { tableName, unprocessed },
          },
        }, cancellationToken);

        unprocessed = response.UnprocessedItems?.GetValueOrDefault(tableName) ?? new();
      }
    }
  }
}
