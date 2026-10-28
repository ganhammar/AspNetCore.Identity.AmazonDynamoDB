using System.Net;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AspNetCore.Identity.AmazonDynamoDB;

/// <summary>What <see cref="DynamoDbTableSetup.MigrateAsync(DynamoDbOptions, IAmazonDynamoDB?, CancellationToken)"/> wrote.</summary>
public sealed record MigrationResult(int Users, int Roles, int Claims, int Logins, int UserRoles, int Passkeys)
{
  public int Total => Users + Roles + Claims + Logins + UserRoles + Passkeys;
}

public static class DynamoDbTableSetup
{
  /// <summary>
  /// The global secondary indexes of tables created by version 2. Lookup items replace them, which
  /// <see cref="MigrateAsync(DynamoDbOptions, IAmazonDynamoDB?, CancellationToken)"/> writes for an
  /// existing table, and <see cref="RemoveLegacyIndexesAsync(DynamoDbOptions, IAmazonDynamoDB?, CancellationToken)"/> removes them.
  /// </summary>
  public static readonly string[] LegacyIndexNames =
  [
    "NormalizedEmail-index",
    "NormalizedUserName-index",
    "ClaimType-ClaimValue-index",
    "RoleName-index",
    "LoginProvider-ProviderKey-index",
    "CredentialId-index",
    "NormalizedName-index",
  ];

  public static Task EnsureInitializedAsync(
    DynamoDbOptions options,
    IAmazonDynamoDB? database = default,
    CancellationToken cancellationToken = default)
    => EnsureInitializedAsync(options, database, NullLogger.Instance, cancellationToken);

  internal static async Task EnsureInitializedAsync(
    DynamoDbOptions options,
    IAmazonDynamoDB? database,
    ILogger logger,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(options);

    var dynamoDb = database ?? options.Database;

    ArgumentNullException.ThrowIfNull(dynamoDb);

    TableDescription? table;

    try
    {
      table = await DescribeTable(dynamoDb, options.DefaultTableName, cancellationToken);
    }
    catch (AmazonDynamoDBException exception) when (IsAccessDenied(exception))
    {
      // Without dynamodb:DescribeTable, only make sure the table exists
      logger.LogWarning(
        exception,
        "Unable to describe table {TableName}, only verifying that it exists",
        options.DefaultTableName);

      if (await TableExists(dynamoDb, options.DefaultTableName, cancellationToken))
      {
        return;
      }

      table = default;
    }

    if (table == default)
    {
      if (await TryCreateTable(options, dynamoDb, logger, cancellationToken))
      {
        return;
      }

      // The table was created by someone else after it was described
      table = await DescribeTable(dynamoDb, options.DefaultTableName, cancellationToken)
        ?? throw new Exception($"Couldn't create table {options.DefaultTableName}");
    }

    if (Equals(table.TableStatus, TableStatus.ACTIVE) == false)
    {
      // The table is still being created or updated, possibly by another instance
      await DynamoDbUtils.WaitForActiveTableAsync(
        dynamoDb,
        options.DefaultTableName,
        logger,
        cancellationToken);
    }

    var legacyIndexes = LegacyIndexes(table);

    if (legacyIndexes.Count > 0)
    {
      logger.LogWarning(
        "Table {TableName} has the global secondary indexes of version 2 ({IndexNames}), which the stores no longer use. Run MigrateAsync, deploy, run it again, then RemoveLegacyIndexesAsync",
        options.DefaultTableName,
        string.Join(", ", legacyIndexes));
    }
  }

  /// <summary>
  /// Writes the lookup and membership items for every user, role, claim, login, role membership
  /// and passkey in an existing table, which version 2 found through global secondary indexes.
  /// Safe to run more than once, and while the table is in use.
  /// </summary>
  public static Task<MigrationResult> MigrateAsync(
    DynamoDbOptions options,
    IAmazonDynamoDB? database = default,
    CancellationToken cancellationToken = default)
    => MigrateAsync(options, database, NullLogger.Instance, cancellationToken);

  internal static async Task<MigrationResult> MigrateAsync(
    DynamoDbOptions options,
    IAmazonDynamoDB? database,
    ILogger logger,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(options);

    var dynamoDb = database ?? options.Database;

    ArgumentNullException.ThrowIfNull(dynamoDb);

    var tableName = options.DefaultTableName;
    var users = 0;
    var roles = 0;
    var claims = 0;
    var logins = 0;
    var userRoles = 0;
    var passkeys = 0;
    Dictionary<string, AttributeValue>? exclusiveStartKey = default;

    logger.LogInformation("Writing lookup items for the items in table {TableName}", tableName);

    do
    {
      var response = await dynamoDb.ScanAsync(new ScanRequest
      {
        TableName = tableName,
        ExclusiveStartKey = exclusiveStartKey,
        ConsistentRead = true,
      }, cancellationToken);

      var requests = new List<WriteRequest>();
      var lookups = new Dictionary<string, string>();

      // Version 2 enforced no uniqueness, so two users may share a username, or two roles a name,
      // in which case the lookup points at one of them and the other is reported
      void AddLookups(IEnumerable<IDynamoDbItem> items, string what)
      {
        foreach (var lookup in items)
        {
          if (lookup is LookupItem { } unique)
          {
            if (lookups.TryGetValue(unique.PartitionKey, out var holder) && holder != unique.Id)
            {
              logger.LogWarning(
                "{What} {Holder} and {Other} share the value of {LookupKey} in table {TableName}; the lookup points at the first",
                what, holder, unique.Id, unique.PartitionKey, tableName);
              continue;
            }

            lookups[unique.PartitionKey] = unique.Id;
          }

          requests.Add(DynamoDbItems.Put(lookup));
        }
      }

      foreach (var item in response.Items ?? new())
      {
        var partitionKey = item.GetString("PartitionKey") ?? "";
        var sortKey = item.GetString("SortKey") ?? "";

        if (partitionKey.StartsWith("USER#", StringComparison.Ordinal) && sortKey.StartsWith("#USER#", StringComparison.Ordinal))
        {
          var user = DynamoDbItems.FromItem<DynamoDbUser>(item);
          AddLookups(DynamoDbUserStore<DynamoDbUser>.LookupsOf(user), "Users");
          users++;
        }
        else if (partitionKey.StartsWith("USER#", StringComparison.Ordinal) && sortKey.StartsWith("CLAIM#", StringComparison.Ordinal))
        {
          var claim = DynamoDbItems.FromItem<DynamoDbUserClaim>(item);
          if (claim is { UserId: not null, ClaimType: not null, ClaimValue: not null })
          {
            requests.Add(DynamoDbItems.Put(new MembershipItem(Lookups.ClaimMembers(claim.ClaimType, claim.ClaimValue), claim.UserId)));
            claims++;
          }
        }
        else if (partitionKey.StartsWith("USER#", StringComparison.Ordinal) && sortKey.StartsWith("LOGIN#", StringComparison.Ordinal))
        {
          var login = DynamoDbItems.FromItem<DynamoDbUserLogin>(item);
          if (login is { UserId: not null, LoginProvider: not null, ProviderKey: not null })
          {
            AddLookups([new LookupItem(Lookups.Login(login.LoginProvider, login.ProviderKey), login.UserId)], "Users");
            logins++;
          }
        }
        else if (partitionKey.StartsWith("USER#", StringComparison.Ordinal) && sortKey.StartsWith("ROLE#", StringComparison.Ordinal))
        {
          var role = DynamoDbItems.FromItem<DynamoDbUserRole>(item);
          if (role is { UserId: not null, RoleName: not null })
          {
            requests.Add(DynamoDbItems.Put(new MembershipItem(Lookups.RoleMembers(role.RoleName), role.UserId)));
            userRoles++;
          }
        }
        else if (partitionKey.StartsWith("USER#", StringComparison.Ordinal) && sortKey.StartsWith("PASSKEY#", StringComparison.Ordinal))
        {
          var passkey = DynamoDbItems.FromItem<DynamoDbUserPasskey>(item);
          if (passkey is { UserId: not null, CredentialId: not null })
          {
            AddLookups([new LookupItem(Lookups.Passkey(passkey.CredentialId), passkey.UserId)], "Users");
            passkeys++;
          }
        }
        else if (partitionKey.StartsWith("ROLE#", StringComparison.Ordinal) && sortKey.StartsWith("#ROLE#", StringComparison.Ordinal))
        {
          var role = DynamoDbItems.FromItem<DynamoDbRole>(item);
          if (role.NormalizedName is { } normalizedName)
          {
            AddLookups([new LookupItem(Lookups.RoleName(normalizedName), role.Id, role.Name)], "Roles");
            roles++;
          }
        }
      }

      await DynamoDbUtils.BatchWriteAsync(dynamoDb, tableName, requests, cancellationToken);
      exclusiveStartKey = response.LastEvaluatedKey;
    } while (exclusiveStartKey?.Count > 0);

    var result = new MigrationResult(users, roles, claims, logins, userRoles, passkeys);

    logger.LogInformation(
      "Wrote lookup items in table {TableName} for {Users} users, {Roles} roles, {Claims} claims, {Logins} logins, {UserRoles} role memberships and {Passkeys} passkeys",
      tableName, users, roles, claims, logins, userRoles, passkeys);

    return result;
  }

  /// <summary>
  /// Removes the global secondary indexes of version 2 from an existing table, one at a time as
  /// DynamoDB requires, once every instance runs version 3 and
  /// <see cref="MigrateAsync(DynamoDbOptions, IAmazonDynamoDB?, CancellationToken)"/> has run.
  /// </summary>
  public static Task RemoveLegacyIndexesAsync(
    DynamoDbOptions options,
    IAmazonDynamoDB? database = default,
    CancellationToken cancellationToken = default)
    => RemoveLegacyIndexesAsync(options, database, NullLogger.Instance, cancellationToken);

  internal static async Task RemoveLegacyIndexesAsync(
    DynamoDbOptions options,
    IAmazonDynamoDB? database,
    ILogger logger,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(options);

    var dynamoDb = database ?? options.Database;

    ArgumentNullException.ThrowIfNull(dynamoDb);

    var table = await DescribeTable(dynamoDb, options.DefaultTableName, cancellationToken)
      ?? throw new Exception($"Table {options.DefaultTableName} does not exist");

    foreach (var indexName in LegacyIndexes(table))
    {
      // DynamoDB removes one index per UpdateTable request, and not while the table or another
      // index is being changed
      await DynamoDbUtils.WaitForActiveTableAsync(dynamoDb, options.DefaultTableName, logger, cancellationToken);

      logger.LogInformation(
        "Removing global secondary index {IndexName} from table {TableName}",
        indexName,
        options.DefaultTableName);

      try
      {
        await dynamoDb.UpdateTableAsync(new UpdateTableRequest
        {
          TableName = options.DefaultTableName,
          GlobalSecondaryIndexUpdates = new()
          {
            new()
            {
              Delete = new()
              {
                IndexName = indexName,
              },
            },
          },
        }, cancellationToken);
      }
      catch (AmazonDynamoDBException exception) when (IsAccessDenied(exception))
      {
        logger.LogWarning(
          exception,
          "Unable to remove global secondary index {IndexName} from table {TableName}, remove it manually or allow dynamodb:UpdateTable",
          indexName,
          options.DefaultTableName);
        return;
      }
      catch (AmazonDynamoDBException)
      {
        // Another instance might be removing the index at the same time
        var current = await DescribeTable(dynamoDb, options.DefaultTableName, cancellationToken);
        var index = current?.GlobalSecondaryIndexes?.Find(x => x.IndexName == indexName);

        if (index != default && Equals(index.IndexStatus, IndexStatus.DELETING) == false)
        {
          throw;
        }
      }
    }

    await DynamoDbUtils.WaitForActiveTableAsync(dynamoDb, options.DefaultTableName, logger, cancellationToken);
  }

  private static List<string> LegacyIndexes(TableDescription table) => (table.GlobalSecondaryIndexes ?? new())
    .Select(x => x.IndexName)
    .Where(LegacyIndexNames.Contains)
    .ToList();

  private static async Task<bool> TryCreateTable(
    DynamoDbOptions options,
    IAmazonDynamoDB database,
    ILogger logger,
    CancellationToken cancellationToken)
  {
    var provisionedThroughput = options.BillingMode != BillingMode.PAY_PER_REQUEST
      ? options.ProvisionedThroughput : default;

    logger.LogInformation("Creating table {TableName}", options.DefaultTableName);

    CreateTableResponse response;
    try
    {
      response = await database.CreateTableAsync(new CreateTableRequest
      {
        TableName = options.DefaultTableName,
        ProvisionedThroughput = provisionedThroughput,
        BillingMode = options.BillingMode,
        KeySchema = new List<KeySchemaElement>
        {
          new("PartitionKey", KeyType.HASH),
          new("SortKey", KeyType.RANGE),
        },
        AttributeDefinitions = new List<AttributeDefinition>
        {
          new("PartitionKey", ScalarAttributeType.S),
          new("SortKey", ScalarAttributeType.S),
        },
      }, cancellationToken);
    }
    catch (ResourceInUseException)
    {
      return false;
    }

    if (response.HttpStatusCode != HttpStatusCode.OK)
    {
      throw new Exception($"Couldn't create table {options.DefaultTableName}");
    }

    await DynamoDbUtils.WaitForActiveTableAsync(
      database,
      options.DefaultTableName,
      logger,
      cancellationToken);

    return true;
  }

  private static async Task<TableDescription?> DescribeTable(
    IAmazonDynamoDB database,
    string tableName,
    CancellationToken cancellationToken)
  {
    try
    {
      var response = await database.DescribeTableAsync(new DescribeTableRequest
      {
        TableName = tableName,
      }, cancellationToken);

      return response.Table;
    }
    catch (ResourceNotFoundException)
    {
      return default;
    }
  }

  private static async Task<bool> TableExists(
    IAmazonDynamoDB database,
    string tableName,
    CancellationToken cancellationToken)
  {
    string? lastEvaluatedTableName = default;

    do
    {
      var response = await database.ListTablesAsync(new ListTablesRequest
      {
        ExclusiveStartTableName = lastEvaluatedTableName,
      }, cancellationToken);

      if (response.TableNames?.Contains(tableName) == true)
      {
        return true;
      }

      lastEvaluatedTableName = response.LastEvaluatedTableName;
    } while (lastEvaluatedTableName != default);

    return false;
  }

  private static bool IsAccessDenied(AmazonDynamoDBException exception)
    => exception.ErrorCode == "AccessDeniedException";
}
