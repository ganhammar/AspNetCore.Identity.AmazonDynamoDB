using System.Globalization;
using Amazon.DynamoDBv2.Model;

namespace AspNetCore.Identity.AmazonDynamoDB;

/// <summary>
/// Reads and writes the attributes of a DynamoDB item in the representation the AWS SDK object
/// persistence model uses, so that items written by either read the same: booleans are the
/// numbers 1 and 0, dates are ISO 8601 strings, lists of strings are string sets, and a null
/// value is an absent attribute. Derived models use these in their
/// <c>WriteAttributes</c> and <c>ReadAttributes</c> overrides.
/// </summary>
public static class AttributeValues
{
  /// <summary>Sets a string attribute; a null value leaves the attribute out.</summary>
  public static void Set(this Dictionary<string, AttributeValue> item, string name, string? value)
  {
    ArgumentNullException.ThrowIfNull(item);

    if (value is not null)
    {
      item[name] = new AttributeValue { S = value };
    }
  }

  /// <summary>Sets a boolean attribute as the number 1 or 0.</summary>
  public static void Set(this Dictionary<string, AttributeValue> item, string name, bool value)
  {
    ArgumentNullException.ThrowIfNull(item);

    item[name] = new AttributeValue { N = value ? "1" : "0" };
  }

  /// <summary>Sets a numeric attribute.</summary>
  public static void Set(this Dictionary<string, AttributeValue> item, string name, long value)
  {
    ArgumentNullException.ThrowIfNull(item);

    item[name] = new AttributeValue { N = value.ToString(CultureInfo.InvariantCulture) };
  }

  /// <summary>Sets a date attribute as a round-trip ISO 8601 string; a null value leaves the attribute out.</summary>
  public static void Set(this Dictionary<string, AttributeValue> item, string name, DateTimeOffset? value)
  {
    ArgumentNullException.ThrowIfNull(item);

    if (value is { } date)
    {
      item[name] = new AttributeValue { S = date.ToString("o", CultureInfo.InvariantCulture) };
    }
  }

  /// <summary>Sets a string set attribute; a null or empty collection leaves the attribute out, as DynamoDB has no empty sets.</summary>
  public static void SetStringSet(this Dictionary<string, AttributeValue> item, string name, IEnumerable<string>? values)
  {
    ArgumentNullException.ThrowIfNull(item);

    var list = values?.ToList();

    if (list is { Count: > 0 })
    {
      item[name] = new AttributeValue { SS = list };
    }
  }

  /// <summary>Sets a list attribute of strings, which keeps order and may be empty; a null collection leaves the attribute out.</summary>
  public static void SetStringList(this Dictionary<string, AttributeValue> item, string name, IEnumerable<string>? values)
  {
    ArgumentNullException.ThrowIfNull(item);

    if (values is not null)
    {
      item[name] = new AttributeValue { L = values.Select(x => new AttributeValue { S = x }).ToList(), IsLSet = true };
    }
  }

  /// <summary>Sets a map attribute of strings, which may be empty; a null dictionary leaves the attribute out.</summary>
  public static void SetStringMap(this Dictionary<string, AttributeValue> item, string name, IDictionary<string, string>? values)
  {
    ArgumentNullException.ThrowIfNull(item);

    if (values is not null)
    {
      item[name] = new AttributeValue
      {
        M = values.ToDictionary(x => x.Key, x => new AttributeValue { S = x.Value }),
        IsMSet = true,
      };
    }
  }

  /// <summary>
  /// Sets a map attribute of string sets, which may be empty; a null dictionary leaves the attribute
  /// out, and an entry with no values is left out of the map.
  /// </summary>
  public static void SetStringSetMap(this Dictionary<string, AttributeValue> item, string name, IDictionary<string, List<string>>? values)
  {
    ArgumentNullException.ThrowIfNull(item);

    if (values is not null)
    {
      item[name] = new AttributeValue
      {
        M = values
          .Where(x => x.Value is { Count: > 0 })
          .ToDictionary(x => x.Key, x => new AttributeValue { SS = x.Value.ToList() }),
        IsMSet = true,
      };
    }
  }

  /// <summary>The string attribute, or null when absent.</summary>
  public static string? GetString(this Dictionary<string, AttributeValue> item, string name)
  {
    ArgumentNullException.ThrowIfNull(item);

    return item.TryGetValue(name, out var value) ? value.S : default;
  }

  /// <summary>The boolean attribute, stored as a number or a boolean, or false when absent.</summary>
  public static bool GetBoolean(this Dictionary<string, AttributeValue> item, string name)
  {
    ArgumentNullException.ThrowIfNull(item);

    if (item.TryGetValue(name, out var value) == false)
    {
      return false;
    }

    if (value.IsBOOLSet)
    {
      return value.BOOL == true;
    }

    return value.N is { } number && number != "0";
  }

  /// <summary>The numeric attribute, or 0 when absent.</summary>
  public static long GetInt64(this Dictionary<string, AttributeValue> item, string name)
  {
    ArgumentNullException.ThrowIfNull(item);

    return item.TryGetValue(name, out var value) && value.N is { } number
      ? long.Parse(number, NumberStyles.Float, CultureInfo.InvariantCulture)
      : 0;
  }

  /// <summary>The numeric attribute, or 0 when absent.</summary>
  public static int GetInt32(this Dictionary<string, AttributeValue> item, string name)
    => (int)item.GetInt64(name);

  /// <summary>The numeric attribute, or 0 when absent.</summary>
  public static uint GetUInt32(this Dictionary<string, AttributeValue> item, string name)
    => (uint)item.GetInt64(name);

  /// <summary>The date attribute, or null when absent.</summary>
  public static DateTimeOffset? GetDateTimeOffset(this Dictionary<string, AttributeValue> item, string name)
  {
    ArgumentNullException.ThrowIfNull(item);

    return item.TryGetValue(name, out var value) && value.S is { } date
      ? DateTimeOffset.Parse(date, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
      : default(DateTimeOffset?);
  }

  /// <summary>The strings of a list or string set attribute, or null when absent.</summary>
  public static List<string>? GetStringList(this Dictionary<string, AttributeValue> item, string name)
  {
    ArgumentNullException.ThrowIfNull(item);

    if (item.TryGetValue(name, out var value) == false)
    {
      return default;
    }

    if (value.IsLSet)
    {
      return value.L.Select(x => x.S).Where(x => x is not null).ToList()!;
    }

    return value.SS is { } set ? [.. set] : default;
  }

  /// <summary>The strings of a map attribute, or null when absent.</summary>
  public static Dictionary<string, string>? GetStringMap(this Dictionary<string, AttributeValue> item, string name)
  {
    ArgumentNullException.ThrowIfNull(item);

    return item.TryGetValue(name, out var value) && value.IsMSet
      ? value.M.Where(x => x.Value.S is not null).ToDictionary(x => x.Key, x => x.Value.S)
      : default;
  }

  /// <summary>The string sets of a map attribute, or null when absent.</summary>
  public static Dictionary<string, List<string>>? GetStringSetMap(this Dictionary<string, AttributeValue> item, string name)
  {
    ArgumentNullException.ThrowIfNull(item);

    if (item.TryGetValue(name, out var value) == false || value.IsMSet == false)
    {
      return default;
    }

    return value.M.ToDictionary(
      x => x.Key,
      x => x.Value.IsLSet
        ? x.Value.L.Select(y => y.S).Where(y => y is not null).ToList()!
        : x.Value.SS?.ToList() ?? new List<string>());
  }
}
