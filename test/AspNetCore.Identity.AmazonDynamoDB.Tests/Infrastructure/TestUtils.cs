using Microsoft.Extensions.Options;
using Moq;

namespace AspNetCore.Identity.AmazonDynamoDB.Tests;

public static class TestUtils
{
  public static IOptionsMonitor<DynamoDbOptions> GetOptions(DynamoDbOptions options)
  {
    var mock = new Mock<IOptionsMonitor<DynamoDbOptions>>();
    mock.Setup(x => x.CurrentValue).Returns(options);
    return mock.Object;
  }
}
