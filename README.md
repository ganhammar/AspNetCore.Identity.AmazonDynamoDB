# AspNetCore.Identity.AmazonDynamoDB

![Build Status](https://github.com/ganhammar/AspNetCore.Identity.AmazonDynamoDB/actions/workflows/ci-cd.yml/badge.svg) [![codecov](https://codecov.io/gh/ganhammar/AspNetCore.Identity.AmazonDynamoDB/branch/main/graph/badge.svg?token=S4M1VCX8J6)](https://codecov.io/gh/ganhammar/AspNetCore.Identity.AmazonDynamoDB) [![NuGet](https://img.shields.io/nuget/v/AspNetCore.Identity.AmazonDynamoDB)](https://www.nuget.org/packages/AspNetCore.Identity.AmazonDynamoDB)

An [ASP.NET Core Identity 6.0](https://github.com/dotnet/aspnetcore/tree/main/src/Identity) provider for [DynamoDB](https://aws.amazon.com/dynamodb/).

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
        options.UsersTableName = "CustomIdentityUserTable"; // Default is identity.users
    });
```

Finally you need to ensure that tables and indexes has been added:

```c#
DynamoDbSetup.EnsureInitialized(serviceProvider);
```

Or asynchronously:

```c#
await DynamoDbSetup.EnsureInitializedAsync(serviceProvider);
```

## Tests

In order to run the tests, you need to have DynamoDB running locally on `localhost:8000`. This can easily be done using [Docker](https://www.docker.com/) and the following command:

```
docker run -p 8000:8000 amazon/dynamodb-local
```