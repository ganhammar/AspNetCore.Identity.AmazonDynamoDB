using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;

namespace AspNetCore.Identity.AmazonDynamoDB;

public class DateTimeOffsetConverter : IPropertyConverter
{
  public DynamoDBEntry ToEntry(object value)
  {
    var date = value as DateTimeOffset?;
    return new Primitive { Value = date?.ToString("o") };
  }

  public object? FromEntry(DynamoDBEntry entry)
  {
    return entry is not Primitive primitive ? (DateTimeOffset?)null : DateTimeOffset.Parse(primitive.Value.ToString()!);
  }
}
