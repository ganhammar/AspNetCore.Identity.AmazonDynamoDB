using System.Net;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace AspNetCore.Identity.AmazonDynamoDB;

public static class DynamoDbTableSetup
{
  public static Task EnsureInitializedAsync(
    DynamoDbOptions options,
    IAmazonDynamoDB? database = default,
    CancellationToken cancellationToken = default)
  {
    var dynamoDb = database ?? options.Database;

    ArgumentNullException.ThrowIfNull(options);
    ArgumentNullException.ThrowIfNull(dynamoDb);

    EnsureAliasCreated(options);

    return SetupTable(options, dynamoDb, cancellationToken);
  }

  public static void EnsureAliasCreated(DynamoDbOptions options)
  {
    if (options.DefaultTableName != Constants.DefaultTableName)
    {
      if (AWSConfigsDynamoDB.Context.TableAliases.ContainsKey(Constants.DefaultTableName))
      {
        AWSConfigsDynamoDB.Context.TableAliases.Remove(Constants.DefaultTableName);
      }

      AWSConfigsDynamoDB.Context.TableAliases
        .Add(Constants.DefaultTableName, options.DefaultTableName);
    }
  }

  private static async Task SetupTable(
    DynamoDbOptions options,
    IAmazonDynamoDB database,
    CancellationToken cancellationToken)
  {
    var provisionedThroughput = options.BillingMode != BillingMode.PAY_PER_REQUEST
      ? options.ProvisionedThroughput : default;
    var globalSecondaryIndexes = GetGlobalSecondaryIndexes(provisionedThroughput);

    var tableNames = await database.ListTablesAsync(cancellationToken);
    if (tableNames.TableNames.Contains(options.DefaultTableName))
    {
      await AddMissingGlobalSecondaryIndexes(
        options, database, globalSecondaryIndexes, cancellationToken);
      return;
    }

    var response = await database.CreateTableAsync(new CreateTableRequest
    {
      TableName = options.DefaultTableName,
      ProvisionedThroughput = provisionedThroughput,
      BillingMode = options.BillingMode,
      GlobalSecondaryIndexes = globalSecondaryIndexes,
      KeySchema = new List<KeySchemaElement>
      {
        new("PartitionKey", KeyType.HASH),
        new("SortKey", KeyType.RANGE),
      },
      AttributeDefinitions = AttributeDefinitions,
    }, cancellationToken);

    if (response.HttpStatusCode != HttpStatusCode.OK)
    {
      throw new Exception($"Couldn't create table {options.DefaultTableName}");
    }

    await DynamoDbUtils.WaitForActiveTableAsync(
      database,
      options.DefaultTableName,
      cancellationToken);
  }

  private static async Task AddMissingGlobalSecondaryIndexes(
    DynamoDbOptions options,
    IAmazonDynamoDB database,
    List<GlobalSecondaryIndex> globalSecondaryIndexes,
    CancellationToken cancellationToken)
  {
    var table = await database.DescribeTableAsync(options.DefaultTableName, cancellationToken);
    var existingIndexNames = (table.Table.GlobalSecondaryIndexes ?? new())
      .Select(x => x.IndexName)
      .ToHashSet();

    // DynamoDB accepts one new global secondary index per UpdateTable request
    foreach (var index in globalSecondaryIndexes.Where(x => existingIndexNames.Contains(x.IndexName) == false))
    {
      var keyAttributeNames = index.KeySchema.Select(x => x.AttributeName).ToHashSet();
      var response = await database.UpdateTableAsync(new UpdateTableRequest
      {
        TableName = options.DefaultTableName,
        AttributeDefinitions = AttributeDefinitions
          .Where(x => keyAttributeNames.Contains(x.AttributeName))
          .ToList(),
        GlobalSecondaryIndexUpdates = new()
        {
          new()
          {
            Create = new()
            {
              IndexName = index.IndexName,
              KeySchema = index.KeySchema,
              Projection = index.Projection,
              ProvisionedThroughput = index.ProvisionedThroughput,
            },
          },
        },
      }, cancellationToken);

      if (response.HttpStatusCode != HttpStatusCode.OK)
      {
        throw new Exception($"Couldn't create index {index.IndexName} on table {options.DefaultTableName}");
      }

      await DynamoDbUtils.WaitForActiveTableAsync(
        database,
        options.DefaultTableName,
        cancellationToken);
    }
  }

  private static List<AttributeDefinition> AttributeDefinitions => new()
  {
    new("PartitionKey", ScalarAttributeType.S),
    new("SortKey", ScalarAttributeType.S),
    // User Attributes
    new("NormalizedEmail", ScalarAttributeType.S),
    new("NormalizedUserName", ScalarAttributeType.S),
    new("ClaimType", ScalarAttributeType.S),
    new("ClaimValue", ScalarAttributeType.S),
    new("RoleName", ScalarAttributeType.S),
    new("LoginProvider", ScalarAttributeType.S),
    new("ProviderKey", ScalarAttributeType.S),
    new("CredentialId", ScalarAttributeType.S),
    // Role Attributes
    new("NormalizedName", ScalarAttributeType.S),
  };

  private static List<GlobalSecondaryIndex> GetGlobalSecondaryIndexes(
    ProvisionedThroughput? provisionedThroughput) => new()
  {
    // User Indexes
    new()
    {
      IndexName = "NormalizedEmail-index",
      KeySchema = new List<KeySchemaElement>
      {
        new KeySchemaElement("NormalizedEmail", KeyType.HASH),
      },
      ProvisionedThroughput = provisionedThroughput,
      Projection = new Projection
      {
        ProjectionType = ProjectionType.ALL,
      },
    },
    new()
    {
      IndexName = "NormalizedUserName-index",
      KeySchema = new List<KeySchemaElement>
      {
        new KeySchemaElement("NormalizedUserName", KeyType.HASH),
      },
      ProvisionedThroughput = provisionedThroughput,
      Projection = new Projection
      {
        ProjectionType = ProjectionType.ALL,
      },
    },
    new()
    {
      IndexName = "ClaimType-ClaimValue-index",
      KeySchema = new List<KeySchemaElement>
      {
        new KeySchemaElement("ClaimType", KeyType.HASH),
        new KeySchemaElement("ClaimValue", KeyType.RANGE),
      },
      ProvisionedThroughput = provisionedThroughput,
      Projection = new Projection
      {
        ProjectionType = ProjectionType.ALL,
      },
    },
    new()
    {
      IndexName = "RoleName-index",
      KeySchema = new List<KeySchemaElement>
      {
        new KeySchemaElement("RoleName", KeyType.HASH),
      },
      ProvisionedThroughput = provisionedThroughput,
      Projection = new Projection
      {
        ProjectionType = ProjectionType.ALL,
      },
    },
    new()
    {
      IndexName = "LoginProvider-ProviderKey-index",
      KeySchema = new List<KeySchemaElement>
      {
        new KeySchemaElement("LoginProvider", KeyType.HASH),
        new KeySchemaElement("ProviderKey", KeyType.RANGE),
      },
      ProvisionedThroughput = provisionedThroughput,
      Projection = new Projection
      {
        ProjectionType = ProjectionType.ALL,
      },
    },
    new()
    {
      IndexName = "CredentialId-index",
      KeySchema = new List<KeySchemaElement>
      {
        new KeySchemaElement("CredentialId", KeyType.HASH),
      },
      ProvisionedThroughput = provisionedThroughput,
      Projection = new Projection
      {
        ProjectionType = ProjectionType.ALL,
      },
    },
    // Role Indexes
    new()
    {
      IndexName = "NormalizedName-index",
      KeySchema = new List<KeySchemaElement>
      {
        new("NormalizedName", KeyType.HASH),
      },
      ProvisionedThroughput = provisionedThroughput,
      Projection = new()
      {
        ProjectionType = ProjectionType.ALL,
      },
    },
  };
}
