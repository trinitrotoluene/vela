using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class ClaimTechStateMapper : MappedDbEntityBase<ClaimTechState, BitcraftClaimTechState>
  {
    public override BitcraftClaimTechState Map(
      ClaimTechState entity
    ) => new(
        Id: entity.EntityId.ToString(),
        Learned: [.. entity.Learned],
        Researching: entity.Researching
      );
  }
}
