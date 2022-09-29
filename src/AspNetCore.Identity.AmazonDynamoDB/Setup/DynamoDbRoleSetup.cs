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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Database);

        if (options.RolesTableName != Constants.DefaultRolesTableName)
        {
            AWSConfigsDynamoDB.Context.AddAlias(new TableAlias(
                options.RolesTableName, Constants.DefaultRolesTableName));
        }

        return SetupTable(options, cancellationToken);
    }

    private static async Task SetupTable(
        DynamoDbOptions options,
        CancellationToken cancellationToken)
    {
        var roleGlobalSecondaryIndexes = new List<GlobalSecondaryIndex>
        {
            new GlobalSecondaryIndex
            {
                IndexName = "Name-index",
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement("Name", KeyType.HASH),
                },
                ProvisionedThroughput = options.ProvisionedThroughput,
                Projection = new Projection
                {
                    ProjectionType = ProjectionType.ALL,
                },
            },
        };

        var tableNames = await options.Database!.ListTablesAsync(cancellationToken);

        if (!tableNames.TableNames.Contains(options.RolesTableName))
        {
            await CreateRolesTableAsync(
                options,
                roleGlobalSecondaryIndexes,
                cancellationToken);
        }
        else
        {
            await DynamoDbUtils.UpdateSecondaryIndexes(
                options.Database,
                options.RolesTableName,
                roleGlobalSecondaryIndexes,
                cancellationToken);
        }
    }

    private static async Task CreateRolesTableAsync(
        DynamoDbOptions options,
        List<GlobalSecondaryIndex>? globalSecondaryIndexes,
        CancellationToken cancellationToken)
    {
        var response = await options.Database!.CreateTableAsync(new CreateTableRequest
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
                    AttributeName = "Name",
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
            options.Database,
            options.RolesTableName,
            cancellationToken);
    }
}