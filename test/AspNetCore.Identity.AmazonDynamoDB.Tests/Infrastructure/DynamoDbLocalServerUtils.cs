using Amazon.DynamoDBv2;
using Amazon.Runtime;

namespace AspNetCore.Identity.AmazonDynamoDB.Tests;

internal static class DynamoDbLocalServerUtils
{
    public static DisposableDatabase CreateDatabase() => new DisposableDatabase();

    public class DisposableDatabase : IDisposable
    {
        private bool _disposed;

        public DisposableDatabase()
        {
            Client = new AmazonDynamoDBClient(
                new BasicAWSCredentials("test", "test"),
                new AmazonDynamoDBConfig
                {
                    ServiceURL = "http://localhost:8000"
                });
        }

        public IAmazonDynamoDB Client { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            var tables = Client.ListTablesAsync().GetAwaiter().GetResult();
            foreach (var tableName in tables.TableNames)
            {
                DynamoDbTestUtils.TruncateTable(tableName, Client).GetAwaiter().GetResult();
            }

            Client.Dispose();
            _disposed = true;
        }
    }
}