using System.Text.Json.Serialization;

namespace Vela.Events
{
  [GlobalEntity]
  public record BitcraftUserModerationState(
    string Id,
    string CreatedByEntityId,
    string TargetEntityId,
    BitcraftUserModerationPolicy UserModerationPolicy,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt
  ) : BitcraftEventBase(Id);

  [JsonConverter(typeof(JsonStringEnumConverter))]
  public enum BitcraftUserModerationPolicy
  {
    PermanentBlockLogin = 0,
    TemporaryBlockLogin = 1,
    BlockChat = 2,
    BlockConstruct = 3,
  }
}