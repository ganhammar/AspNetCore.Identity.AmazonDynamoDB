using System.Net;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Util;

namespace AspNetCore.Identity.AmazonDynamoDB;

public static class DynamoDbUserSetup
{
  public static Task EnsureInitializedAsync(
      DynamoDbOptions options,
      IAmazonDynamoDB? database = default,
      CancellationToken cancellationToken = default)
  {
    var dynamoDb = database ?? options.Database;

    ArgumentNullException.ThrowIfNull(options);
    ArgumentNullException.ThrowIfNull(dynamoDb);

    if (options.UsersTableName != Constants.DefaultUsersTableName)
    {
      AWSConfigsDynamoDB.Context.AddAlias(new TableAlias(
          options.UsersTableName, Constants.DefaultUsersTableName));
    }

    if (options.UserClaimsTableName != Constants.DefaultUserClaimsTableName)
    {
      AWSConfigsDynamoDB.Context.AddAlias(new TableAlias(
          options.UserClaimsTableName, Constants.DefaultUserClaimsTableName));
    }

    if (options.UserLoginsTableName != Constants.DefaultUserLoginsTableName)
    {
      AWSConfigsDynamoDB.Context.AddAlias(new TableAlias(
          options.UserLoginsTableName, Constants.DefaultUserLoginsTableName));
    }

    if (options.UserRolesTableName != Constants.DefaultUserRolesTableName)
    {
      AWSConfigsDynamoDB.Context.AddAlias(new TableAlias(
          options.UserRolesTableName, Constants.DefaultUserRolesTableName));
    }

    if (options.UserTokensTableName != Constants.DefaultUserTokensTableName)
    {
      AWSConfigsDynamoDB.Context.AddAlias(new TableAlias(
          options.UserTokensTableName, Constants.DefaultUserTokensTableName));
    }

    return SetupTable(options, dynamoDb, cancellationToken);
  }

  private static async Task SetupTable(
      DynamoDbOptions options,
      IAmazonDynamoDB database,
      CancellationToken cancellationToken)
  {
    var userGlobalSecondaryIndexes = new List<GlobalSecondaryIndex>
        {
            new GlobalSecondaryIndex
            {
                IndexName = "NormalizedEmail-index",
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement("NormalizedEmail", KeyType.HASH),
                },
                ProvisionedThroughput = options.ProvisionedThroughput,
                Projection = new Projection
                {
                    ProjectionType = ProjectionType.ALL,
                },
            },
            new GlobalSecondaryIndex
            {
                IndexName = "NormalizedUserName-index",
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement("NormalizedUserName", KeyType.HASH),
                },
                ProvisionedThroughput = options.ProvisionedThroughput,
                Projection = new Projection
                {
                    ProjectionType = ProjectionType.ALL,
                },
            },
        };
    var userClaimGlobalSecondaryIndexes = new List<GlobalSecondaryIndex>
        {
            new GlobalSecondaryIndex
            {
                IndexName = "UserId-index",
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement("UserId", KeyType.HASH),
                },
                ProvisionedThroughput = options.ProvisionedThroughput,
                Projection = new Projection
                {
                    ProjectionType = ProjectionType.ALL,
                },
            },
            new GlobalSecondaryIndex
            {
                IndexName = "ClaimType-ClaimValue-index",
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement("ClaimType", KeyType.HASH),
                    new KeySchemaElement("ClaimValue", KeyType.RANGE),
                },
                ProvisionedThroughput = options.ProvisionedThroughput,
                Projection = new Projection
                {
                    ProjectionType = ProjectionType.ALL,
                },
            },
        };
    var userLoginGlobalSecondaryIndexes = new List<GlobalSecondaryIndex>
        {
            new GlobalSecondaryIndex
            {
                IndexName = "UserId-index",
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement("UserId", KeyType.HASH),
                },
                ProvisionedThroughput = options.ProvisionedThroughput,
                Projection = new Projection
                {
                    ProjectionType = ProjectionType.ALL,
                },
            },
        };
    var userRolesGlobalSecondaryIndexes = new List<GlobalSecondaryIndex>
        {
            new GlobalSecondaryIndex
            {
                IndexName = "RoleName-index",
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement("RoleName", KeyType.HASH),
                },
                ProvisionedThroughput = options.ProvisionedThroughput,
                Projection = new Projection
                {
                    ProjectionType = ProjectionType.ALL,
                },
            },
        };
    var userTokensGlobalSecondaryIndexes = new List<GlobalSecondaryIndex>
        {
            new GlobalSecondaryIndex
            {
                IndexName = "UserId-index",
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement("UserId", KeyType.HASH),
                },
                ProvisionedThroughput = options.ProvisionedThroughput,
                Projection = new Projection
                {
                    ProjectionType = ProjectionType.ALL,
                },
            },
        };

    var tableNames = await database.ListTablesAsync(cancellationToken);

    if (!tableNames.TableNames.Contains(options.UsersTableName))
    {
      await CreateUsersTableAsync(
          options,
          database,
          userGlobalSecondaryIndexes,
          cancellationToken);
    }
    else
    {
      await DynamoDbUtils.UpdateSecondaryIndexes(
          database,
          options.UsersTableName,
          userGlobalSecondaryIndexes,
          cancellationToken);
    }

    if (!tableNames.TableNames.Contains(options.UserClaimsTableName))
    {
      await CreateUserClaimsTableAsync(
          options,
          database,
          userClaimGlobalSecondaryIndexes,
          cancellationToken);
    }
    else
    {
      await DynamoDbUtils.UpdateSecondaryIndexes(
          database,
          options.UserClaimsTableName,
          userClaimGlobalSecondaryIndexes,
          cancellationToken);
    }

    if (!tableNames.TableNames.Contains(options.UserLoginsTableName))
    {
      await CreateUserLoginsTableAsync(
          options,
          database,
          userLoginGlobalSecondaryIndexes,
          cancellationToken);
    }
    else
    {
      await DynamoDbUtils.UpdateSecondaryIndexes(
          database,
          options.UserLoginsTableName,
          userLoginGlobalSecondaryIndexes,
          cancellationToken);
    }

    if (!tableNames.TableNames.Contains(options.UserRolesTableName))
    {
      await CreateUserRolesTableAsync(
          options,
          database,
          userRolesGlobalSecondaryIndexes,
          cancellationToken);
    }
    else
    {
      await DynamoDbUtils.UpdateSecondaryIndexes(
          database,
          options.UserRolesTableName,
          userRolesGlobalSecondaryIndexes,
          cancellationToken);
    }

    if (!tableNames.TableNames.Contains(options.UserTokensTableName))
    {
      await CreateUserTokensTableAsync(
          options,
          database,
          userTokensGlobalSecondaryIndexes,
          cancellationToken);
    }
    else
    {
      await DynamoDbUtils.UpdateSecondaryIndexes(
          database,
          options.UserTokensTableName,
          userTokensGlobalSecondaryIndexes,
          cancellationToken);
    }
  }

  private static async Task CreateUsersTableAsync(
      DynamoDbOptions options,
      IAmazonDynamoDB database,
      List<GlobalSecondaryIndex>? globalSecondaryIndexes,
      CancellationToken cancellationToken)
  {
    var response = await database.CreateTableAsync(new CreateTableRequest
    {
      TableName = options.UsersTableName,
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
                    AttributeName = "NormalizedEmail",
                    AttributeType = ScalarAttributeType.S,
                },
                new AttributeDefinition
                {
                    AttributeName = "NormalizedUserName",
                    AttributeType = ScalarAttributeType.S,
                },
            },
      GlobalSecondaryIndexes = globalSecondaryIndexes,
    }, cancellationToken);

    if (response.HttpStatusCode != HttpStatusCode.OK)
    {
      throw new Exception($"Couldn't create table {options.UsersTableName}");
    }

    await DynamoDbUtils.WaitForActiveTableAsync(
        database,
        options.UsersTableName,
        cancellationToken);
  }

  private static async Task CreateUserClaimsTableAsync(
      DynamoDbOptions options,
      IAmazonDynamoDB database,
      List<GlobalSecondaryIndex>? globalSecondaryIndexes,
      CancellationToken cancellationToken)
  {
    var response = await database.CreateTableAsync(new CreateTableRequest
    {
      TableName = options.UserClaimsTableName,
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
                    AttributeName = "ClaimType",
                    AttributeType = ScalarAttributeType.S,
                },
                new AttributeDefinition
                {
                    AttributeName = "UserId",
                    AttributeType = ScalarAttributeType.S,
                },
                new AttributeDefinition
                {
                    AttributeName = "ClaimValue",
                    AttributeType = ScalarAttributeType.S,
                },
            },
      GlobalSecondaryIndexes = globalSecondaryIndexes,
    }, cancellationToken);

    if (response.HttpStatusCode != HttpStatusCode.OK)
    {
      throw new Exception($"Couldn't create table {options.UserClaimsTableName}");
    }

    await DynamoDbUtils.WaitForActiveTableAsync(
        database,
        options.UserClaimsTableName,
        cancellationToken);
  }

  private static async Task CreateUserLoginsTableAsync(
      DynamoDbOptions options,
      IAmazonDynamoDB database,
      List<GlobalSecondaryIndex>? globalSecondaryIndexes,
      CancellationToken cancellationToken)
  {
    var response = await database.CreateTableAsync(new CreateTableRequest
    {
      TableName = options.UserLoginsTableName,
      ProvisionedThroughput = options.ProvisionedThroughput,
      BillingMode = options.BillingMode,
      KeySchema = new List<KeySchemaElement>
            {
                new KeySchemaElement
                {
                    AttributeName = "LoginProvider",
                    KeyType = KeyType.HASH,
                },
                new KeySchemaElement
                {
                    AttributeName = "ProviderKey",
                    KeyType = KeyType.RANGE,
                },
            },
      AttributeDefinitions = new List<AttributeDefinition>
            {
                new AttributeDefinition
                {
                    AttributeName = "LoginProvider",
                    AttributeType = ScalarAttributeType.S,
                },
                new AttributeDefinition
                {
                    AttributeName = "ProviderKey",
                    AttributeType = ScalarAttributeType.S,
                },
                new AttributeDefinition
                {
                    AttributeName = "UserId",
                    AttributeType = ScalarAttributeType.S,
                },
            },
      GlobalSecondaryIndexes = globalSecondaryIndexes,
    }, cancellationToken);

    if (response.HttpStatusCode != HttpStatusCode.OK)
    {
      throw new Exception($"Couldn't create table {options.UserLoginsTableName}");
    }

    await DynamoDbUtils.WaitForActiveTableAsync(
        database,
        options.UserLoginsTableName,
        cancellationToken);
  }

  private static async Task CreateUserRolesTableAsync(
      DynamoDbOptions options,
      IAmazonDynamoDB database,
      List<GlobalSecondaryIndex>? globalSecondaryIndexes,
      CancellationToken cancellationToken)
  {
    var response = await database.CreateTableAsync(new CreateTableRequest
    {
      TableName = options.UserRolesTableName,
      ProvisionedThroughput = options.ProvisionedThroughput,
      BillingMode = options.BillingMode,
      KeySchema = new List<KeySchemaElement>
            {
                new KeySchemaElement
                {
                    AttributeName = "UserId",
                    KeyType = KeyType.HASH,
                },
                new KeySchemaElement
                {
                    AttributeName = "RoleName",
                    KeyType = KeyType.RANGE,
                },
            },
      AttributeDefinitions = new List<AttributeDefinition>
            {
                new AttributeDefinition
                {
                    AttributeName = "UserId",
                    AttributeType = ScalarAttributeType.S,
                },
                new AttributeDefinition
                {
                    AttributeName = "RoleName",
                    AttributeType = ScalarAttributeType.S,
                },
            },
      GlobalSecondaryIndexes = globalSecondaryIndexes,
    }, cancellationToken);

    if (response.HttpStatusCode != HttpStatusCode.OK)
    {
      throw new Exception($"Couldn't create table {options.UserRolesTableName}");
    }

    await DynamoDbUtils.WaitForActiveTableAsync(
        database,
        options.UserRolesTableName,
        cancellationToken);
  }

  private static async Task CreateUserTokensTableAsync(
      DynamoDbOptions options,
      IAmazonDynamoDB database,
      List<GlobalSecondaryIndex>? globalSecondaryIndexes,
      CancellationToken cancellationToken)
  {
    var response = await database.CreateTableAsync(new CreateTableRequest
    {
      TableName = options.UserTokensTableName,
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
                    AttributeName = "UserId",
                    AttributeType = ScalarAttributeType.S,
                },
            },
      GlobalSecondaryIndexes = globalSecondaryIndexes,
    }, cancellationToken);

    if (response.HttpStatusCode != HttpStatusCode.OK)
    {
      throw new Exception($"Couldn't create table {options.UserTokensTableName}");
    }

    await DynamoDbUtils.WaitForActiveTableAsync(
        database,
        options.UserTokensTableName,
        cancellationToken);
  }
}
