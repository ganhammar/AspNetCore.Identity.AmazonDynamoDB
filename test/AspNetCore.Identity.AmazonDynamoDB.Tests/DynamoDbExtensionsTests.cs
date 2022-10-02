using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AspNetCore.Identity.AmazonDynamoDB.Tests;

public class DynamoDbExtensionsTests
{
    [Fact]
    public void Should_ThrowException_When_CallingUseDynamoDbAndBuilderIsNull()
    {
        // Arrange, Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            IdentityBuilderExtensions.AddDynamoDbStores(null!));

        Assert.Equal("builder", exception.ParamName);
    }
}