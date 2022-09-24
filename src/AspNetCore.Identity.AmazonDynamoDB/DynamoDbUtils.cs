using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;

namespace AspNetCore.Identity.AmazonDynamoDB;

public class DynamoDbUtils
{
    public static async Task WaitForActiveTableAsync(
        IAmazonDynamoDB client, string tableName, CancellationToken cancellationToken = default)
    {
        bool active;
        do
        {
            active = true;
            var response = await client.DescribeTableAsync(new DescribeTableRequest
            {
                TableName = tableName,
            }, cancellationToken);

            if (!Equals(response.Table.TableStatus, TableStatus.ACTIVE) ||
                !response.Table.GlobalSecondaryIndexes.TrueForAll(g => Equals(g.IndexStatus, IndexStatus.ACTIVE)))
            {
                active = false;
            }

            if (!active)
            {
                Console.WriteLine($"Waiting for table {tableName} to become active...");

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        } while (!active);
    }

    public static async Task UpdateSecondaryIndexes(
        IAmazonDynamoDB client,
        string tableName,
        List<GlobalSecondaryIndex> globalSecondaryIndexes,
        CancellationToken cancellationToken = default)
    {
        var response = await client.DescribeTableAsync(new DescribeTableRequest { TableName = tableName }, cancellationToken);
        var table = response.Table;

        var indexesToAdd = globalSecondaryIndexes
            .Where(g => !table.GlobalSecondaryIndexes
                .Exists(gd => gd.IndexName.Equals(g.IndexName)));
        var indexUpdates = indexesToAdd
            .Select(index => new GlobalSecondaryIndexUpdate
            {
                Create = new CreateGlobalSecondaryIndexAction
                {
                    IndexName = index.IndexName,
                    KeySchema = index.KeySchema,
                    ProvisionedThroughput = index.ProvisionedThroughput,
                    Projection = index.Projection
                }
            })
            .ToList();

        if (indexUpdates.Count > 0)
        {
            await UpdateTableAsync(client, tableName, indexUpdates, cancellationToken);
        }
    }

    private static async Task UpdateTableAsync(
        IAmazonDynamoDB client,
        string tableName,
        List<GlobalSecondaryIndexUpdate> indexUpdates,
        CancellationToken cancellationToken)
    {
        await client.UpdateTableAsync(new UpdateTableRequest
        {
            TableName = tableName,
            GlobalSecondaryIndexUpdates = indexUpdates
        }, cancellationToken);

        await WaitForActiveTableAsync(client, tableName, cancellationToken);
    }
}
