using Xunit;

namespace AspNetCore.Identity.AmazonDynamoDB.Tests;

[CollectionDefinition(Constants.DatabaseCollection)]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
}
