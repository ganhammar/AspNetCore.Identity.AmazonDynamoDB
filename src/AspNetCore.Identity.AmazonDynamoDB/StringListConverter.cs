using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;

namespace AspNetCore.Identity.AmazonDynamoDB;

/// <summary>
/// Stores a list of strings as a DynamoDB list (L) so that order and empty lists
/// are preserved, which a string set (SS) does not do.
/// </summary>
public class StringListConverter : IPropertyConverter
{
  public DynamoDBEntry ToEntry(object value)
  {
    if (value is not IEnumerable<string> values)
    {
      return new Primitive { Value = null };
    }

    var list = new DynamoDBList();
    foreach (var item in values)
    {
      list.Add(new Primitive(item));
    }

    return list;
  }

  public object? FromEntry(DynamoDBEntry entry)
  {
    return entry is not DynamoDBList list ? null : list.Entries.Select(x => x.AsString()).ToList();
  }
}
