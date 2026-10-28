using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;

namespace AspNetCore.Identity.AmazonDynamoDB;

/// <summary>
/// A WebAuthn passkey credential registered to a user. Binary WebAuthn values
/// (credential id, public key, attestation object and client data JSON) are
/// stored as unpadded base64url strings.
/// </summary>
[DynamoDBTable(Constants.DefaultTableName)]
public class DynamoDbUserPasskey : IDynamoDbItem
{
  [DynamoDBHashKey]
  public string PartitionKey
  {
    get => $"USER#{UserId}";
    set { }
  }
  [DynamoDBRangeKey]
  public string SortKey
  {
    get => $"PASSKEY#{CredentialId}";
    set { }
  }
  public string? UserId { get; set; }
  public string? CredentialId { get; set; }
  public string? PublicKey { get; set; }
  public string? Name { get; set; }
  [DynamoDBProperty(typeof(DateTimeOffsetConverter))]
  public DateTimeOffset CreatedAt { get; set; }
  public uint SignCount { get; set; }
  [DynamoDBProperty(typeof(StringListConverter))]
  public List<string>? Transports { get; set; }
  public bool IsUserVerified { get; set; }
  public bool IsBackupEligible { get; set; }
  public bool IsBackedUp { get; set; }
  public string? AttestationObject { get; set; }
  public string? ClientDataJson { get; set; }

  void IDynamoDbItem.Write(Dictionary<string, AttributeValue> item)
  {
    item.Set("PartitionKey", PartitionKey);
    item.Set("SortKey", SortKey);
    item.Set("UserId", UserId);
    item.Set("CredentialId", CredentialId);
    item.Set("PublicKey", PublicKey);
    item.Set("Name", Name);
    item.Set("CreatedAt", CreatedAt);
    item.Set("SignCount", SignCount);
    item.SetStringList("Transports", Transports);
    item.Set("IsUserVerified", IsUserVerified);
    item.Set("IsBackupEligible", IsBackupEligible);
    item.Set("IsBackedUp", IsBackedUp);
    item.Set("AttestationObject", AttestationObject);
    item.Set("ClientDataJson", ClientDataJson);
  }

  void IDynamoDbItem.Read(Dictionary<string, AttributeValue> item)
  {
    UserId = item.GetString("UserId");
    CredentialId = item.GetString("CredentialId");
    PublicKey = item.GetString("PublicKey");
    Name = item.GetString("Name");
    CreatedAt = item.GetDateTimeOffset("CreatedAt") ?? default;
    SignCount = item.GetUInt32("SignCount");
    Transports = item.GetStringList("Transports");
    IsUserVerified = item.GetBoolean("IsUserVerified");
    IsBackupEligible = item.GetBoolean("IsBackupEligible");
    IsBackedUp = item.GetBoolean("IsBackedUp");
    AttestationObject = item.GetString("AttestationObject");
    ClientDataJson = item.GetString("ClientDataJson");
  }
}
