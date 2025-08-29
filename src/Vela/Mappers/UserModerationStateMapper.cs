using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class UserModerationStateMapper : MappedDbEntityBase<UserModerationState, BitcraftUserModerationState>
  {
    public override BitcraftUserModerationState Map(UserModerationState entity) =>
        new(
          Id: entity.EntityId.ToString(),
          CreatedByEntityId: entity.CreatedByEntityId.ToString(),
          TargetEntityId: entity.TargetEntityId.ToString(),
          UserModerationPolicy: (BitcraftUserModerationPolicy)entity.UserModerationPolicy,
          CreatedAt: entity.CreatedTime.ToStd(),
          ExpiresAt: entity.ExpirationTime.ToStd()
        );
  }
}