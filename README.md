# AspNetCore.Identity.AmazonDynamoDB

![Build Status](https://github.com/ganhammar/AspNetCore.Identity.AmazonDynamoDB/actions/workflows/ci-cd.yml/badge.svg) [![codecov](https://codecov.io/gh/ganhammar/AspNetCore.Identity.AmazonDynamoDB/branch/main/graph/badge.svg?token=S4M1VCX8J6)](https://codecov.io/gh/ganhammar/AspNetCore.Identity.AmazonDynamoDB) [![NuGet](https://img.shields.io/nuget/v/AspNetCore.Identity.AmazonDynamoDB)](https://www.nuget.org/packages/AspNetCore.Identity.AmazonDynamoDB)

An [ASP.NET Core Identity](https://github.com/dotnet/aspnetcore/tree/main/src/Identity) provider for [DynamoDB](https://aws.amazon.com/dynamodb/), targeting .NET 8, 9 and 10. On .NET 10 it also stores passkey (WebAuthn) credentials through `IUserPasskeyStore<TUser>`.

## Getting Started

You can install the latest version via [Nuget](https://www.nuget.org/packages/AspNetCore.Identity.AmazonDynamoDB):

```
> dotnet add package AspNetCore.Identity.AmazonDynamoDB
```

Then you use the stores by calling `AddDynamoDbStores` on `IdentityBuilder`:

```c#
services
    .AddIdentityCore<DynamoDbUser>()
    .AddRoles<DynamoDbRole>()
    .AddDynamoDbStores()
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

## Adding Attributes

To add custom attributes to the user or role model, you would need to create a new class that extends the `DynamoDbUser` or `DynamoDbRole` and adds the needed additional attributes.

```c#
public class CustomUser : DynamoDbUser
{
    public string? ProfilePictureUrl { get; set; }
}
```

Then you need to use your new classes when adding the DynamoDB stores:

```c#
services
    .AddIdentityCore<CustomUser>()
    .AddRoles<CustomRole>()
    .AddDynamoDbStores();
```
