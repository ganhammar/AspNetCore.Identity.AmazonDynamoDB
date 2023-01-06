using Amazon.DynamoDBv2;

namespace AspNetCore.Identity.AmazonDynamoDB.Tests;

public class DatabaseFixture : IDisposable
{
  public static readonly string TableName = Guid.NewGuid().ToString();
  public static readonly AmazonDynamoDBClient Client = new();
  private bool _disposed;

  public DatabaseFixture()
  {
    CreateTable().GetAwaiter().GetResult();
    var tables = Client.ListTablesAsync().GetAwaiter().GetResult();
  }

  private async Task CreateTable()
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
