using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Runtime;

namespace AspNetCore.Identity.AmazonDynamoDB.Tests;

public class DatabaseFixture : IDisposable
{
  public static readonly string TableName = Guid.NewGuid().ToString();
  public static readonly AmazonDynamoDBClient Client = new(
    new BasicAWSCredentials("test", "test"),
    new AmazonDynamoDBConfig
    {
      ServiceURL = "http://localhost:8000",
    });
  private bool _disposed;

  public DatabaseFixture()
  {
    // The tests seed and verify items through the SDK's object persistence model, whose models
    // name the default table; the alias points it at the test table
    AWSConfigsDynamoDB.Context.TableAliases["identity"] = TableName;
    CreateTable().GetAwaiter().GetResult();
  }

  private static async Task CreateTable()
  {
    await AspNetCoreIdentityDynamoDbSetup.EnsureInitializedAsync(TestUtils.GetOptions(new()
    {
      Database = Client,
      DefaultTableName = TableName,
    }));
  }

  public void Dispose()
  {
    if (_disposed)
    {
      return;
    }

    Client.DeleteTableAsync(TableName).GetAwaiter().GetResult();
    Client.Dispose();
    _disposed = true;
  }
}
