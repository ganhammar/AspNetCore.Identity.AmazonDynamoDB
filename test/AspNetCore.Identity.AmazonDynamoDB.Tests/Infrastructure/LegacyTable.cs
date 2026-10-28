using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace AspNetCore.Identity.AmazonDynamoDB.Tests;

// Creates a table as version 2 did, with the global secondary indexes its stores queried
public static class LegacyTable
{
  public static readonly string[] IndexNames = DynamoDbTableSetup.LegacyIndexNames;

  public static async Task<string> CreateAsync(IAmazonDynamoDB client)
  {
    var tableName = $"legacy-{Guid.NewGuid()}";

    await client.CreateTableAsync(new CreateTableRequest
    {
      TableName = tableName,
      BillingMode = BillingMode.PAY_PER_REQUEST,
      KeySchema = [new("PartitionKey", KeyType.HASH), new("SortKey", KeyType.RANGE)],
      AttributeDefinitions =
      [
        new("PartitionKey", ScalarAttributeType.S),
        new("SortKey", ScalarAttributeType.S),
        new("NormalizedEmail", ScalarAttributeType.S),
        new("NormalizedUserName", ScalarAttributeType.S),
        new("ClaimType", ScalarAttributeType.S),
        new("ClaimValue", ScalarAttributeType.S),
        new("RoleName", ScalarAttributeType.S),
        new("LoginProvider", ScalarAttributeType.S),
        new("ProviderKey", ScalarAttributeType.S),
        new("CredentialId", ScalarAttributeType.S),
        new("NormalizedName", ScalarAttributeType.S),
      ],
      GlobalSecondaryIndexes =
      [
        Index("NormalizedEmail-index", "NormalizedEmail"),
        Index("NormalizedUserName-index", "NormalizedUserName"),
        Index("ClaimType-ClaimValue-index", "ClaimType", "ClaimValue"),
        Index("RoleName-index", "RoleName"),
        Index("LoginProvider-ProviderKey-index", "LoginProvider", "ProviderKey"),
        Index("CredentialId-index", "CredentialId"),
        Index("NormalizedName-index", "NormalizedName"),
      ],
    });

    for (var attempt = 0; attempt < 60; attempt++)
    {
      var table = await client.DescribeTableAsync(tableName);
      if (Equals(table.Table.TableStatus, TableStatus.ACTIVE)
        && table.Table.GlobalSecondaryIndexes.TrueForAll(x => Equals(x.IndexStatus, IndexStatus.ACTIVE)))
      {
        return tableName;
      }

      await Task.Delay(500);
    }

    throw new TimeoutException($"Table {tableName} did not become active");
  }

  public static async Task<int> CountItemsAsync(IAmazonDynamoDB client, string tableName)
  {
    var count = 0;
    Dictionary<string, AttributeValue>? exclusiveStartKey = default;

    do
    {
      var response = await client.ScanAsync(new ScanRequest
      {
        TableName = tableName,
        ExclusiveStartKey = exclusiveStartKey,
        ConsistentRead = true,
        Select = Select.COUNT,
      });
      count += response.Count ?? 0;
      exclusiveStartKey = response.LastEvaluatedKey;
    } while (exclusiveStartKey?.Count > 0);

    return count;
  }

  private static GlobalSecondaryIndex Index(string name, string hashKey, string? rangeKey = default) => new()
  {
    IndexName = name,
    KeySchema = rangeKey is null
      ? [new(hashKey, KeyType.HASH)]
      : [new(hashKey, KeyType.HASH), new(rangeKey, KeyType.RANGE)],
    Projection = new Projection { ProjectionType = ProjectionType.ALL },
  };
}
