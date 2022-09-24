using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace AspNetCore.Identity.AmazonDynamoDB.Tests;

public static class DynamoDbTestUtils
{
    public static async Task TruncateTable(string tableName, IAmazonDynamoDB client)
    {
        var (numberOfItems, keys) = await GetKeyDefinitions(tableName, client);

        if (numberOfItems == 0)
        {
            return;
        }

        var allItems = new List<Dictionary<string, AttributeValue>>();
        Dictionary<string, AttributeValue>? exclusiveStartKey = default;

        while (exclusiveStartKey == default || exclusiveStartKey.Count > 0)
        {
            var data = await client.ScanAsync(new ScanRequest
            {
                TableName = tableName,
                AttributesToGet = keys.Select(x => x.AttributeName).ToList(),
                ExclusiveStartKey = exclusiveStartKey,
            });
            allItems.AddRange(data.Items);
            exclusiveStartKey = data.LastEvaluatedKey;
        }

        if (allItems.Any() == false)
        {
            return;
        }

        var writeRequests = allItems
            .Select(x => new WriteRequest
            {
                DeleteRequest = new DeleteRequest
                {
                    Key = x,
                },
            })
            .ToList();

        var batches = ToChunks(writeRequests, 25);

        foreach (var batch in batches)
        {
            var request = new BatchWriteItemRequest
            {
                RequestItems = new Dictionary<string, List<WriteRequest>>
                {
                    { tableName, batch.ToList() },
                },
            };

            await client.BatchWriteItemAsync(request);
        }
    }

    private static async Task<(long, IEnumerable<KeyDefinition>)> GetKeyDefinitions(string tableName, IAmazonDynamoDB client)
    {
        var tableDefinition = await client.DescribeTableAsync(new DescribeTableRequest
        {
            TableName = tableName,
        });

        return (tableDefinition.Table.ItemCount, tableDefinition.Table.KeySchema.Select(x => new KeyDefinition
        {
            AttributeName = x.AttributeName,
            AttributeType = tableDefinition.Table.AttributeDefinitions
                .First(y => y.AttributeName == x.AttributeName)
                .AttributeType,
            KeyType = x.KeyType,
        }));
    }

    private static IEnumerable<IEnumerable<T>> ToChunks<T>(List<T> fullList, int batchSize)
    {
        var total = 0;

        while (total < fullList.Count)
        {
            yield return fullList.Skip(total).Take(batchSize);
            total += batchSize;
        }
    }

    public class KeyDefinition
    {
        public string? AttributeName { get; set; }
        public ScalarAttributeType? AttributeType { get; set; }
        public KeyType? KeyType { get; set; }
    }
}