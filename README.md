# AspNetCore.Identity.AmazonDynamoDB

![Build Status](https://github.com/ganhammar/AspNetCore.Identity.AmazonDynamoDB/actions/workflows/ci-cd.yml/badge.svg) [![codecov](https://codecov.io/gh/ganhammar/AspNetCore.Identity.AmazonDynamoDB/branch/main/graph/badge.svg?token=S4M1VCX8J6)](https://codecov.io/gh/ganhammar/AspNetCore.Identity.AmazonDynamoDB) [![NuGet](https://img.shields.io/nuget/v/AspNetCore.Identity.AmazonDynamoDB)](https://www.nuget.org/packages/AspNetCore.Identity.AmazonDynamoDB)

An [ASP.NET Core Identity 9.0](https://github.com/dotnet/aspnetcore/tree/main/src/Identity) provider for [DynamoDB](https://aws.amazon.com/dynamodb/).

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
