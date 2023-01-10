using Amazon.DynamoDBv2;
using AspNetCore.Identity.AmazonDynamoDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace OpenIddict.AmazonDynamoDB.IntegrationTests;

public class Main
{
  [Fact]
  public async Task Should_CreateTable_When_CallingSetup()
  {
    // Arrange
    var tableName = Guid.NewGuid().ToString();
    var collection = new ServiceCollection()
      .AddIdentityCore<DynamoDbUser>()
      .AddRoles<DynamoDbRole>()
      .AddDynamoDbStores();
    var client = new AmazonDynamoDBClient();

    var mock = new Mock<IOptionsMonitor<DynamoDbOptions>>();
    mock.Setup(x => x.CurrentValue).Returns(new DynamoDbOptions
    {
      DefaultTableName = tableName,
      Database = client,
    });

    // Act
    AspNetCoreIdentityDynamoDbSetup.EnsureInitialized(mock.Object);

    // Assert
    var tableNames = await client.ListTablesAsync();
    Assert.Contains(tableName, tableNames.TableNames);

    await client.DeleteTableAsync(tableName);
  }
}
