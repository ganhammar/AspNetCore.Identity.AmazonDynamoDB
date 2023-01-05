using Amazon.DynamoDBv2;

namespace AspNetCore.Identity.AmazonDynamoDB;

internal class DynamoDbUtils
{
  public static async Task WaitForActiveTableAsync(
    IAmazonDynamoDB client, string tableName, CancellationToken cancellationToken = default)
  {
    bool active;
    do
    {
      active = true;
      var response = await client.DescribeTableAsync(tableName, cancellationToken);

      if (!Equals(response.Table.TableStatus, TableStatus.ACTIVE) ||
        !response.Table.GlobalSecondaryIndexes.TrueForAll(g => Equals(g.IndexStatus, IndexStatus.ACTIVE)))
      {
        active = false;
      }

      if (!active)
      {
        Console.WriteLine($"Waiting for table {tableName} to become active...");

        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
      }
    } while (!active);
  }
}
