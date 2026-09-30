namespace AspNetCore.Identity.AmazonDynamoDB;

internal static class Base64UrlEncoding
{
  public static string Encode(byte[] value) => Convert.ToBase64String(value)
    .TrimEnd('=')
    .Replace('+', '-')
    .Replace('/', '_');

  public static byte[] Decode(string value)
  {
    var base64 = value
      .Replace('-', '+')
      .Replace('_', '/');

    return Convert.FromBase64String((base64.Length % 4) switch
    {
      2 => base64 + "==",
      3 => base64 + "=",
      _ => base64,
    });
  }
}
