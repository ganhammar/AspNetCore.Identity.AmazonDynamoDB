using Amazon.DynamoDBv2.Model;
using Xunit;

namespace AspNetCore.Identity.AmazonDynamoDB.Tests;

// The attributes are written the way the SDK's object persistence model writes them, so that
// items written by either version of the stores read the same
public class AttributeValuesTests
{
  [Fact]
  public void Should_LeaveAttributeOut_When_StringIsNull()
  {
    var item = new Dictionary<string, AttributeValue>();

    item.Set("Name", default(string));

    Assert.Empty(item);
    Assert.Null(item.GetString("Name"));
  }

  [Theory]
  [InlineData(true, "1")]
  [InlineData(false, "0")]
  public void Should_StoreBooleanAsNumber(bool value, string expected)
  {
    var item = new Dictionary<string, AttributeValue>();

    item.Set("Flag", value);

    Assert.Equal(expected, item["Flag"].N);
    Assert.Equal(value, item.GetBoolean("Flag"));
  }

  [Fact]
  public void Should_ReadBoolean_When_StoredAsBoolean()
  {
    var item = new Dictionary<string, AttributeValue>
    {
      ["Flag"] = new AttributeValue { BOOL = true },
    };

    Assert.True(item.GetBoolean("Flag"));
  }

  [Fact]
  public void Should_RoundTripDateTimeOffset_WithItsOffset()
  {
    var item = new Dictionary<string, AttributeValue>();
    var date = new DateTimeOffset(2026, 1, 2, 3, 4, 5, 678, TimeSpan.FromHours(2));

    item.Set("Date", date);

    Assert.Equal("2026-01-02T03:04:05.6780000+02:00", item["Date"].S);
    Assert.Equal(date, item.GetDateTimeOffset("Date"));
    Assert.Null(item.GetDateTimeOffset("Missing"));
  }

  [Fact]
  public void Should_StoreStringSet_When_ValuesExist_AndLeaveItOut_When_Empty()
  {
    var item = new Dictionary<string, AttributeValue>();

    item.SetStringSet("Values", ["a", "b"]);
    item.SetStringSet("Empty", []);
    item.SetStringSet("Null", default);

    Assert.Equal(["a", "b"], item["Values"].SS);
    Assert.False(item.ContainsKey("Empty"));
    Assert.False(item.ContainsKey("Null"));
    Assert.Equal(["a", "b"], item.GetStringList("Values"));
  }

  [Fact]
  public void Should_StoreStringList_WithOrderAndEmptyLists()
  {
    var item = new Dictionary<string, AttributeValue>();

    item.SetStringList("Values", ["b", "a"]);
    item.SetStringList("Empty", []);
    item.SetStringList("Null", default);

    Assert.Equal(["b", "a"], item["Values"].L.Select(x => x.S));
    Assert.True(item["Empty"].IsLSet);
    Assert.Empty(item["Empty"].L);
    Assert.False(item.ContainsKey("Null"));
    Assert.Equal(["b", "a"], item.GetStringList("Values"));
    Assert.Empty(item.GetStringList("Empty")!);
    Assert.Null(item.GetStringList("Null"));
  }

  [Fact]
  public void Should_StoreStringMap_IncludingEmptyMaps()
  {
    var item = new Dictionary<string, AttributeValue>();

    item.SetStringMap("Values", new Dictionary<string, string> { ["sv"] = "Hej" });
    item.SetStringMap("Empty", new Dictionary<string, string>());
    item.SetStringMap("Null", default);

    Assert.Equal("Hej", item["Values"].M["sv"].S);
    Assert.True(item["Empty"].IsMSet);
    Assert.False(item.ContainsKey("Null"));
    Assert.Equal("Hej", item.GetStringMap("Values")!["sv"]);
    Assert.Empty(item.GetStringMap("Empty")!);
    Assert.Null(item.GetStringMap("Null"));
  }

  [Fact]
  public void Should_StoreStringSetMap_AndLeaveOutEmptyEntries()
  {
    var item = new Dictionary<string, AttributeValue>();

    item.SetStringSetMap("Claims", new Dictionary<string, List<string>>
    {
      ["perm"] = ["read", "write"],
      ["none"] = [],
    });

    Assert.Equal(["read", "write"], item["Claims"].M["perm"].SS);
    Assert.False(item["Claims"].M.ContainsKey("none"));
    Assert.Equal(["read", "write"], item.GetStringSetMap("Claims")!["perm"]);
    Assert.Null(item.GetStringSetMap("Missing"));
  }

  [Fact]
  public void Should_ReadNumbers_AndDefaultToZero_When_Missing()
  {
    var item = new Dictionary<string, AttributeValue>();

    item.Set("Count", 7L);

    Assert.Equal("7", item["Count"].N);
    Assert.Equal(7, item.GetInt32("Count"));
    Assert.Equal(7L, item.GetInt64("Count"));
    Assert.Equal(7u, item.GetUInt32("Count"));
    Assert.Equal(0, item.GetInt32("Missing"));
  }
}
