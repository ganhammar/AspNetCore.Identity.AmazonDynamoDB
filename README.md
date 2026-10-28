# AspNetCore.Identity.AmazonDynamoDB

![Build Status](https://github.com/ganhammar/AspNetCore.Identity.AmazonDynamoDB/actions/workflows/ci-cd.yml/badge.svg) [![codecov](https://codecov.io/gh/ganhammar/AspNetCore.Identity.AmazonDynamoDB/branch/main/graph/badge.svg?token=S4M1VCX8J6)](https://codecov.io/gh/ganhammar/AspNetCore.Identity.AmazonDynamoDB) [![NuGet](https://img.shields.io/nuget/v/AspNetCore.Identity.AmazonDynamoDB)](https://www.nuget.org/packages/AspNetCore.Identity.AmazonDynamoDB)

An [ASP.NET Core Identity](https://github.com/dotnet/aspnetcore/tree/main/src/Identity) provider for [DynamoDB](https://aws.amazon.com/dynamodb/), targeting .NET 8, 9 and 10. On .NET 10 it also stores passkey (WebAuthn) credentials through `IUserPasskeyStore<TUser>`.

## Getting Started

You can install the latest version via [Nuget](https://www.nuget.org/packages/AspNetCore.Identity.AmazonDynamoDB):

```
> dotnet add package AspNetCore.Identity.AmazonDynamoDB
```

Then you use the stores by calling `AddDynamoDbStores` on `IdentityBuilder`, with the user type and, when roles are used, the role type:

```c#
services
    .AddIdentityCore<DynamoDbUser>()
    .AddRoles<DynamoDbRole>()
    .AddDynamoDbStores<DynamoDbUser, DynamoDbRole>()
    .Configure(options =>
    {
        options.BillingMode = BillingMode.PROVISIONED; // Default is BillingMode.PAY_PER_REQUEST
        options.ProvisionedThroughput = new ProvisionedThroughput
        {
            ReadCapacityUnits = 5, // Default is 1
            WriteCapacityUnits = 5, // Default is 1
        };
        options.DefaultTableName = "my-custom-identity-table-name"; // Default is identity
        options.ResolveRoleNames = true; // Default is false, see Role Names below
    });
```

Finally, you need to ensure that tables and indexes have been added:

```c#
AspNetCoreIdentityDynamoDbSetup.EnsureInitialized(serviceProvider);
```

Or asynchronously:

```c#
await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(serviceProvider);
```

The table is created if it doesn't exist. If it already exists, any global secondary index that is missing from it (such as `CredentialId-index`, used to look up users by passkey) is added and the call waits until the index is active. New indexes use provisioned throughput from the options when the existing table is provisioned, and on-demand capacity otherwise. It's safe to call from several instances at the same time.

The call needs the `dynamodb:DescribeTable` permission on the table, plus `dynamodb:CreateTable` to create it and `dynamodb:UpdateTable` to add a missing index. Without `dynamodb:DescribeTable` it falls back to `dynamodb:ListTables` and only checks that the table exists. If an index can't be added because `dynamodb:UpdateTable` is missing, a warning is logged instead of failing.

The `IServiceProvider` overloads log progress and warnings through the registered `ILoggerFactory`.

## Role Names

Users store the normalized names of their roles, so by default `UserManager.GetRolesAsync` returns `ADMIN` for a role named `Admin`, and that is also the value of the role claims in the user's cookie. `ClaimsPrincipal.IsInRole` and `[Authorize(Roles = "...")]` compare role names case sensitively, so they need to use the normalized name.

Set `ResolveRoleNames` (or call `ResolveRoleNames()` on the builder) to return the names of the roles instead, as the Entity Framework Core stores do. Each role is then looked up by its normalized name when the user's roles are read, and roles that can't be found are returned with their normalized name.

## Tests

To run the tests, you need to have DynamoDB running locally on `localhost:8000`. This can easily be done using [Docker](https://www.docker.com/) and the following command:

```
docker run -p 8000:8000 amazon/dynamodb-local
```

## Table Layout

Everything lives in one table with a `PartitionKey` and a `SortKey` and no secondary indexes. A user's items share its partition (`USER#{id}`): the user itself, its claims, logins, roles, tokens and passkeys. Values that are looked up directly have a lookup item of their own, which is one strongly consistent read: `USERNAME#{normalized}`, `LOGIN#{provider}#{key}`, `PASSKEY#{credentialId}` and `ROLENAME#{normalized}` point at the id they belong to, and the partitions `EMAIL#{normalized}`, `CLAIM#{type}#{value}` and `USERROLE#{name}` list the users that hold them. A user and its username lookup are written in one transaction, as are a role and its name lookup and each login or passkey with its lookup, so a username, role name, login or passkey that another user holds fails the write with Identity's `DuplicateUserName`, `DuplicateRoleName`, `LoginAlreadyAssociated` or a `DuplicatePasskey` error. Emails are not unique, as Identity leaves that to `RequireUniqueEmail`; `FindByEmailAsync` returns one of the users with the email.

## Upgrading from 2.x

Version 2 found users and roles through seven global secondary indexes. Version 3 reads and writes the same items in the same format, so no data changes, but it needs the lookup items described above, which an existing table does not have. Upgrade like this:

1. With version 3 installed, run `AspNetCoreIdentityDynamoDbSetup.MigrateAsync(serviceProvider)` (or `DynamoDbTableSetup.MigrateAsync(options, client)`) against the table. It scans the table once and writes the lookup items for every user, role, claim, login, role membership and passkey. It is safe to run while version 2 instances are serving traffic, and safe to run again. The result says how many items it wrote lookups for.
2. Deploy version 3. `EnsureInitialized` logs a warning as long as the legacy indexes exist, and nothing else; it never scans or changes an existing table.
3. Run `MigrateAsync` once more, to cover anything a version 2 instance wrote during the rollout.
4. Run `AspNetCoreIdentityDynamoDbSetup.RemoveLegacyIndexesAsync(serviceProvider)` to drop the seven indexes, one at a time as DynamoDB requires. Until then the indexes are kept up to date by DynamoDB and cost storage and write capacity, but nothing reads them. This needs `dynamodb:UpdateTable`; without it a warning is logged and the indexes stay.

What changes for an application:

- Lookups by name, email, login or passkey are two strongly consistent reads instead of one eventually consistent index query, so a user is found right after it was created.
- Creating or updating a user is a transaction of the user item and its lookups, which DynamoDB bills at twice the write units of a plain write. Duplicate usernames, role names, logins and passkeys fail with the Identity errors above instead of relying on the managers' checks.
- Register the stores with `AddDynamoDbStores<TUser>()` or `AddDynamoDbStores<TUser, TRole>()`; the non-generic overload still works but needs dynamic code, so it is not for trimmed or Native AOT applications.
- Custom properties on a `DynamoDbUser` or `DynamoDbRole` subclass are stored only when the subclass overrides `WriteAttributes` and `ReadAttributes`, see Adding Attributes.
- `DynamoDbTableSetup.EnsureAliasCreated` and `DynamoDbOptionsMonitor` are gone: the stores read the table name from the options on every call and no longer use the SDK's object persistence model.

## Native AOT

The package is trim and Native AOT compatible: the stores read and write items through the low-level DynamoDB client, with no reflection. Register the stores with the generic `AddDynamoDbStores<TUser>()` or `AddDynamoDbStores<TUser, TRole>()`; the non-generic `AddDynamoDbStores()` builds the store types from the builder's user and role types at runtime and is marked as requiring dynamic code.

## Adding Attributes

To add custom attributes to the user or role model, create a class that extends `DynamoDbUser` or `DynamoDbRole`, add the properties, and override `WriteAttributes` and `ReadAttributes` to store them. The `AttributeValues` extension methods convert strings, booleans, numbers, dates, lists and maps the way the store does.

```c#
public class CustomUser : DynamoDbUser
{
    public string? ProfilePictureUrl { get; set; }

    protected override void WriteAttributes(Dictionary<string, AttributeValue> item)
    {
        base.WriteAttributes(item);
        item.Set("ProfilePictureUrl", ProfilePictureUrl);
    }

    protected override void ReadAttributes(Dictionary<string, AttributeValue> item)
    {
        base.ReadAttributes(item);
        ProfilePictureUrl = item.GetString("ProfilePictureUrl");
    }
}
```

Then use your classes when adding the DynamoDB stores:

```c#
services
    .AddIdentityCore<CustomUser>()
    .AddRoles<CustomRole>()
    .AddDynamoDbStores<CustomUser, CustomRole>();
```
