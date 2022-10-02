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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Database);

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

        return SetupTable(options, cancellationToken);
    }

    private static async Task SetupTable(
        DynamoDbOptions options,
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

        var tableNames = await options.Database!.ListTablesAsync(cancellationToken);

        if (!tableNames.TableNames.Contains(options.UsersTableName))
        {
            await CreateUsersTableAsync(
                options,
                userGlobalSecondaryIndexes,
                cancellationToken);
        }
        else
        {
            await DynamoDbUtils.UpdateSecondaryIndexes(
                options.Database,
                options.UsersTableName,
                userGlobalSecondaryIndexes,
                cancellationToken);
        }

        if (!tableNames.TableNames.Contains(options.UserClaimsTableName))
        {
            await CreateUserClaimsTableAsync(
                options,
                userClaimGlobalSecondaryIndexes,
                cancellationToken);
        }
        else
        {
            await DynamoDbUtils.UpdateSecondaryIndexes(
                options.Database,
                options.UserClaimsTableName,
                userClaimGlobalSecondaryIndexes,
                cancellationToken);
        }

        if (!tableNames.TableNames.Contains(options.UserLoginsTableName))
        {
            await CreateUserLoginsTableAsync(
                options,
                userLoginGlobalSecondaryIndexes,
                cancellationToken);
        }
        else
        {
            await DynamoDbUtils.UpdateSecondaryIndexes(
                options.Database,
                options.UserLoginsTableName,
                userLoginGlobalSecondaryIndexes,
                cancellationToken);
        }

        if (!tableNames.TableNames.Contains(options.UserRolesTableName))
        {
            await CreateUserRolesTableAsync(
                options,
                userRolesGlobalSecondaryIndexes,
                cancellationToken);
        }
        else
        {
            await DynamoDbUtils.UpdateSecondaryIndexes(
                options.Database,
                options.UserRolesTableName,
                userRolesGlobalSecondaryIndexes,
                cancellationToken);
        }
    }

    private static async Task CreateUsersTableAsync(
        DynamoDbOptions options,
        List<GlobalSecondaryIndex>? globalSecondaryIndexes,
        CancellationToken cancellationToken)
    {
        var response = await options.Database!.CreateTableAsync(new CreateTableRequest
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
            options.Database,
            options.UsersTableName,
            cancellationToken);
    }

    private static async Task CreateUserClaimsTableAsync(
        DynamoDbOptions options,
        List<GlobalSecondaryIndex>? globalSecondaryIndexes,
        CancellationToken cancellationToken)
    {
        var response = await options.Database!.CreateTableAsync(new CreateTableRequest
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
            options.Database,
            options.UserClaimsTableName,
            cancellationToken);
    }

    private static async Task CreateUserLoginsTableAsync(
        DynamoDbOptions options,
        List<GlobalSecondaryIndex>? globalSecondaryIndexes,
        CancellationToken cancellationToken)
    {
        var response = await options.Database!.CreateTableAsync(new CreateTableRequest
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
            options.Database,
            options.UserLoginsTableName,
            cancellationToken);
    }

    private static async Task CreateUserRolesTableAsync(
        DynamoDbOptions options,
        List<GlobalSecondaryIndex>? globalSecondaryIndexes,
        CancellationToken cancellationToken)
    {
        var response = await options.Database!.CreateTableAsync(new CreateTableRequest
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
            options.Database,
            options.UserRolesTableName,
            cancellationToken);
    }
}