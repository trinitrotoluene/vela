using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class UserModerationStateMapper : MappedDbEntityBase<UserModerationState, BitcraftUserModerationState>
  {
    public override string TopicName => "bitcraft.moderation.user";

    public override BitcraftUserModerationState Map(UserModerationState entity) =>
        new(
          CreatedByEntityId: entity.CreatedByEntityId.ToString(),
          TargetEntityId: entity.TargetEntityId.ToString(),
          UserModerationPolicy: (BitcraftUserModerationPolicy)entity.UserModerationPolicy,
          CreatedAt: entity.CreatedTime.ToStd(),
          ExpiresAt: entity.ExpirationTime.ToStd()
        );
  }
}