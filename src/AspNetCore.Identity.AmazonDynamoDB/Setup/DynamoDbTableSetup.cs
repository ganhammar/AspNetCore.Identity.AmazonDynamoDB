using System.Net;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Util;

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

    if (options.DefaultTableName != Constants.DefaultTableName &&
      AWSConfigsDynamoDB.Context.TableAliases.Any(x => x.Key == Constants.DefaultTableName) == false)
    {
      AWSConfigsDynamoDB.Context.AddAlias(new TableAlias(
        Constants.DefaultTableName, options.DefaultTableName));
    }

    return SetupTable(options, dynamoDb, cancellationToken);
  }

  private static async Task SetupTable(
    DynamoDbOptions options,
    IAmazonDynamoDB database,
    CancellationToken cancellationToken)
  {
    var tableNames = await database.ListTablesAsync(cancellationToken);
    if (tableNames.TableNames.Contains(options.DefaultTableName))
    {
      return;
    }

    var provisionedThroughput = options.BillingMode != BillingMode.PAY_PER_REQUEST
      ? options.ProvisionedThroughput : default;
    var response = await database.CreateTableAsync(new CreateTableRequest
    {
      TableName = options.DefaultTableName,
      ProvisionedThroughput = provisionedThroughput,
      BillingMode = options.BillingMode,
      GlobalSecondaryIndexes = new()
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
          IndexName = "UserId-index",
          KeySchema = new List<KeySchemaElement>
          {
            new KeySchemaElement("UserId", KeyType.HASH),
            new KeySchemaElement("SortKey", KeyType.RANGE),
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
      },
      KeySchema = new List<KeySchemaElement>
      {
        new("PartitionKey", KeyType.HASH),
        new("SortKey", KeyType.RANGE),
      },
      AttributeDefinitions = new List<AttributeDefinition>
      {
        new("PartitionKey", ScalarAttributeType.S),
        new("SortKey", ScalarAttributeType.S),
        // User Attributes
        new("NormalizedEmail", ScalarAttributeType.S),
        new("NormalizedUserName", ScalarAttributeType.S),
        new("UserId", ScalarAttributeType.S),
        new("ClaimType", ScalarAttributeType.S),
        new("ClaimValue", ScalarAttributeType.S),
        new("RoleName", ScalarAttributeType.S),
        new("LoginProvider", ScalarAttributeType.S),
        new("ProviderKey", ScalarAttributeType.S),
        // Role Attributes
        new("NormalizedName", ScalarAttributeType.S),
      },
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
}
