using Amazon.DynamoDBv2.DataModel;

namespace AspNetCore.Identity.AmazonDynamoDB;

/// <summary>
/// A WebAuthn passkey credential registered to a user. Binary WebAuthn values
/// (credential id, public key, attestation object and client data JSON) are
/// stored as unpadded base64url strings.
/// </summary>
[DynamoDBTable(Constants.DefaultTableName)]
public class DynamoDbUserPasskey
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
}
