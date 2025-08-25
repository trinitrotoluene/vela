using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class ClaimStateMapper : MappedDbEntityBase<ClaimState, BitcraftClaimState>
  {
    public override string TopicName => "bitcraft.claim.state";

    public override BitcraftClaimState Map(
      ClaimState entity
    ) => new(
        Id: entity.EntityId.ToString(),
        OwnerPlayerId: entity.OwnerPlayerEntityId.ToString(),
        OwnerBuildingId: entity.OwnerPlayerEntityId.ToString(),
        Name: entity.Name,
        IsNeutral: entity.Neutral
      );
  }
}