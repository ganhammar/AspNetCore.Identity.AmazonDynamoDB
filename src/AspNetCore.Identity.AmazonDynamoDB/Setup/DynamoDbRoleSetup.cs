using System.Net;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Util;

namespace AspNetCore.Identity.AmazonDynamoDB;

public static class DynamoDbRoleSetup
{
  public static Task EnsureInitializedAsync(
    DynamoDbOptions options,
    IAmazonDynamoDB? database = default,
    CancellationToken cancellationToken = default)
  {
    var dynamoDb = database ?? options.Database;

    ArgumentNullException.ThrowIfNull(options);
    ArgumentNullException.ThrowIfNull(dynamoDb);

    if (options.RolesTableName != Constants.DefaultRolesTableName)
    {
      AWSConfigsDynamoDB.Context.AddAlias(new TableAlias(
        options.RolesTableName, Constants.DefaultRolesTableName));
    }

    return SetupTable(options, dynamoDb, cancellationToken);
  }

  private static async Task SetupTable(
    DynamoDbOptions options,
    IAmazonDynamoDB database,
    CancellationToken cancellationToken)
  {
    var roleGlobalSecondaryIndexes = new List<GlobalSecondaryIndex>
    {
      new GlobalSecondaryIndex
      {
        IndexName = "NormalizedName-index",
        KeySchema = new List<KeySchemaElement>
        {
          new KeySchemaElement("NormalizedName", KeyType.HASH),
        },
        ProvisionedThroughput = options.ProvisionedThroughput,
        Projection = new Projection
        {
          ProjectionType = ProjectionType.ALL,
        },
      },
    };

    var tableNames = await database.ListTablesAsync(cancellationToken);

    if (!tableNames.TableNames.Contains(options.RolesTableName))
    {
      await CreateRolesTableAsync(
        options,
        database,
        roleGlobalSecondaryIndexes,
        cancellationToken);
    }
    else
    {
      await DynamoDbUtils.UpdateSecondaryIndexes(
        database,
        options.RolesTableName,
        roleGlobalSecondaryIndexes,
        cancellationToken);
    }
  }

  private static async Task CreateRolesTableAsync(
    DynamoDbOptions options,
    IAmazonDynamoDB database,
    List<GlobalSecondaryIndex>? globalSecondaryIndexes,
    CancellationToken cancellationToken)
  {
    var response = await database.CreateTableAsync(new CreateTableRequest
    {
      TableName = options.RolesTableName,
      ProvisionedThroughput = options.ProvisionedThroughput,
      BillingMode = options.BillingMode,
      KeySchema = new List<KeySchemaElement>
      {
        new KeySchemaElement
        {
          AttributeName = "Id",
          KeyType = KeyType.HASH,
        },
      },
      AttributeDefinitions = new List<AttributeDefinition>
      {
        new AttributeDefinition
        {
          AttributeName = "Id",
          AttributeType = ScalarAttributeType.S,
        },
        new AttributeDefinition
        {
          AttributeName = "NormalizedName",
          AttributeType = ScalarAttributeType.S,
        },
      },
      GlobalSecondaryIndexes = globalSecondaryIndexes,
    }, cancellationToken);

    if (response.HttpStatusCode != HttpStatusCode.OK)
    {
      throw new Exception($"Couldn't create table {options.RolesTableName}");
    }

    await DynamoDbUtils.WaitForActiveTableAsync(
      database,
      options.RolesTableName,
      cancellationToken);
  }
}
