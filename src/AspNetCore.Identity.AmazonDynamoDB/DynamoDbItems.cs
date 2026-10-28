using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace AspNetCore.Identity.AmazonDynamoDB;

/// <summary>A model the stores read and write as one DynamoDB item.</summary>
internal interface IDynamoDbItem
{
  string PartitionKey { get; }
  string? SortKey { get; }
  void Write(Dictionary<string, AttributeValue> item);
  void Read(Dictionary<string, AttributeValue> item);
}

/// <summary>The low-level DynamoDB operations the stores use, over <see cref="IDynamoDbItem"/> models.</summary>
internal static class DynamoDbItems
{
  private const int MaxBatchGetSize = 100;
  private const int MaxBatchGetRetries = 8;

  public static Dictionary<string, AttributeValue> ToItem<T>(T model)
    where T : IDynamoDbItem
  {
    var item = new Dictionary<string, AttributeValue>();
    model.Write(item);
    return item;
  }

  public static T FromItem<T>(Dictionary<string, AttributeValue> item)
    where T : IDynamoDbItem, new()
  {
    var model = new T();
    model.Read(item);
    return model;
  }

  public static Dictionary<string, AttributeValue> Key(string partitionKey, string? sortKey) => new()
  {
    { "PartitionKey", new AttributeValue { S = partitionKey } },
    { "SortKey", new AttributeValue { S = sortKey } },
  };

  public static Dictionary<string, AttributeValue> Key<T>(T model)
    where T : IDynamoDbItem
    => Key(model.PartitionKey, model.SortKey);

  public static WriteRequest Put<T>(T model)
    where T : IDynamoDbItem
    => new(new PutRequest(ToItem(model)));

  public static WriteRequest Delete<T>(T model)
    where T : IDynamoDbItem
    => new(new DeleteRequest(Key(model)));

  /// <summary>Puts the item in a transaction, when the given condition holds.</summary>
  public static TransactWriteItem TransactPut<T>(
    string tableName, T model, string? condition = default, Dictionary<string, AttributeValue>? values = default, Dictionary<string, string>? names = default)
    where T : IDynamoDbItem
    => new()
    {
      Put = new Put
      {
        TableName = tableName,
        Item = ToItem(model),
        ConditionExpression = condition,
        ExpressionAttributeValues = values,
        ExpressionAttributeNames = names,
      },
    };

  /// <summary>Deletes the item in a transaction, when the given condition holds.</summary>
  public static TransactWriteItem TransactDelete(
    string tableName, string partitionKey, string? sortKey, string? condition = default, Dictionary<string, AttributeValue>? values = default)
    => new()
    {
      Delete = new Delete
      {
        TableName = tableName,
        Key = Key(partitionKey, sortKey),
        ConditionExpression = condition,
        ExpressionAttributeValues = values,
      },
    };

  private const int MaxTransactionConflictRetries = 6;

  /// <summary>
  /// Writes the items as one transaction, so that either all or none of them are written. A
  /// transaction that another transaction on the same items cancelled is tried again; one that a
  /// condition cancelled is not, and throws its <see cref="TransactionCanceledException"/>.
  /// </summary>
  public static async Task TransactWriteAsync(IAmazonDynamoDB client, List<TransactWriteItem> items, CancellationToken cancellationToken)
  {
    for (var attempt = 0; ; attempt++)
    {
      try
      {
        await client.TransactWriteItemsAsync(new TransactWriteItemsRequest { TransactItems = items }, cancellationToken);
        return;
      }
      catch (TransactionCanceledException exception)
        when (attempt < MaxTransactionConflictRetries && IsConflictOnly(exception))
      {
        await Task.Delay(TimeSpan.FromMilliseconds(25 * Math.Pow(2, attempt) + Random.Shared.Next(25)), cancellationToken);
      }
    }
  }

  private static bool IsConflictOnly(TransactionCanceledException exception)
  {
    var reasons = exception.CancellationReasons ?? new();

    return reasons.Any(x => x.Code == "TransactionConflict")
      && reasons.All(x => x.Code is "TransactionConflict" or "None" or null);
  }

  /// <summary>The positions of the items whose condition failed, in a cancelled transaction.</summary>
  public static IReadOnlyList<int> FailedConditions(TransactionCanceledException exception)
    => (exception.CancellationReasons ?? new())
      .Select((reason, index) => (reason, index))
      .Where(x => x.reason.Code == "ConditionalCheckFailed")
      .Select(x => x.index)
      .ToList();

  /// <summary>Reads a partition, or the part of it whose sort keys start with the prefix.</summary>
  public static Task<List<T>> QueryPartitionAsync<T>(
    IAmazonDynamoDB client,
    string tableName,
    string partitionKey,
    string? sortKeyPrefix,
    CancellationToken cancellationToken,
    bool consistentRead = false)
    where T : IDynamoDbItem, new()
  {
    var request = new QueryRequest
    {
      TableName = tableName,
      KeyConditionExpression = sortKeyPrefix is null
        ? "PartitionKey = :partitionKey"
        : "PartitionKey = :partitionKey and begins_with(SortKey, :sortKey)",
      ExpressionAttributeValues = new()
      {
        { ":partitionKey", new AttributeValue { S = partitionKey } },
      },
      ConsistentRead = consistentRead,
    };

    if (sortKeyPrefix is not null)
    {
      request.ExpressionAttributeValues.Add(":sortKey", new AttributeValue { S = sortKeyPrefix });
    }

    return QueryAsync<T>(client, request, cancellationToken);
  }

  // Writes the item only when it exists and still carries the concurrency value it was loaded with
  public static Task PutIfUnchangedAsync(
    IAmazonDynamoDB client,
    string tableName,
    Dictionary<string, AttributeValue> item,
    string concurrencyAttribute,
    string? expected,
    CancellationToken cancellationToken)
    => client.PutItemAsync(new PutItemRequest
    {
      TableName = tableName,
      Item = item,
      ConditionExpression = expected is null
        ? "attribute_exists(PartitionKey) and attribute_not_exists(#concurrency)"
        : "attribute_exists(PartitionKey) and #concurrency = :concurrency",
      ExpressionAttributeNames = new()
      {
        { "#concurrency", concurrencyAttribute },
      },
      ExpressionAttributeValues = expected is null
        ? default
        : new()
        {
          { ":concurrency", new AttributeValue { S = expected } },
        },
    }, cancellationToken);

  public static async Task<T?> GetAsync<T>(
    IAmazonDynamoDB client,
    string tableName,
    string partitionKey,
    string? sortKey,
    CancellationToken cancellationToken,
    bool consistentRead = false)
    where T : class, IDynamoDbItem, new()
  {
    var response = await client.GetItemAsync(new GetItemRequest
    {
      TableName = tableName,
      Key = Key(partitionKey, sortKey),
      ConsistentRead = consistentRead,
    }, cancellationToken);

    return response.Item is { Count: > 0 } item ? FromItem<T>(item) : default;
  }

  // Reads every page of the query
  public static async Task<List<T>> QueryAsync<T>(
    IAmazonDynamoDB client,
    QueryRequest request,
    CancellationToken cancellationToken)
    where T : IDynamoDbItem, new()
  {
    var items = new List<T>();

    do
    {
      var response = await client.QueryAsync(request, cancellationToken);
      items.AddRange((response.Items ?? new()).Select(FromItem<T>));
      request.ExclusiveStartKey = response.LastEvaluatedKey;
    } while (request.ExclusiveStartKey?.Count > 0);

    return items;
  }

  // Reads the first matching item only, the query has no filter so the first page holds it
  public static async Task<T?> QueryFirstAsync<T>(
    IAmazonDynamoDB client,
    QueryRequest request,
    CancellationToken cancellationToken)
    where T : class, IDynamoDbItem, new()
  {
    request.Limit = 1;
    var response = await client.QueryAsync(request, cancellationToken);

    return response.Items is { Count: > 0 } items ? FromItem<T>(items[0]) : default;
  }

  // BatchGetItem accepts at most 100 keys and can leave some of them unprocessed
  public static async Task<List<T>> BatchGetAsync<T>(
    IAmazonDynamoDB client,
    string tableName,
    IEnumerable<Dictionary<string, AttributeValue>> keys,
    CancellationToken cancellationToken)
    where T : IDynamoDbItem, new()
  {
    var items = new List<T>();

    foreach (var chunk in keys.Chunk(MaxBatchGetSize))
    {
      var unprocessed = new KeysAndAttributes { Keys = chunk.ToList() };

      for (var attempt = 0; unprocessed.Keys.Count > 0; attempt++)
      {
        if (attempt > MaxBatchGetRetries)
        {
          throw new Exception($"Couldn't read {unprocessed.Keys.Count} items from table {tableName}");
        }

        if (attempt > 0)
        {
          await Task.Delay(TimeSpan.FromMilliseconds(50 * Math.Pow(2, attempt - 1)), cancellationToken);
        }

        var response = await client.BatchGetItemAsync(new BatchGetItemRequest
        {
          RequestItems = new()
          {
            { tableName, unprocessed },
          },
        }, cancellationToken);

        items.AddRange((response.Responses?.GetValueOrDefault(tableName) ?? new()).Select(FromItem<T>));
        unprocessed = response.UnprocessedKeys?.GetValueOrDefault(tableName) ?? new KeysAndAttributes { Keys = new() };
        unprocessed.Keys ??= new();
      }
    }

    return items;
  }
}
