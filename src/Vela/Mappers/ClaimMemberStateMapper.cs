using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class ClaimMemberStateMapper : MappedDbEntityBase<ClaimMemberState, BitcraftClaimMemberState>
  {
    public override BitcraftClaimMemberState Map(ClaimMemberState entity) =>
        new(
          Id: entity.EntityId.ToString(),
          ClaimEntityId: entity.ClaimEntityId.ToString(),
          PlayerEntityId: entity.PlayerEntityId.ToString()
        );
  }
}
