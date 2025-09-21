using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class ClaimLocalStateMapper : MappedDbEntityBase<ClaimLocalState, BitcraftClaimLocalState>
  {
    public override BitcraftClaimLocalState Map(
      ClaimLocalState entity
    ) => new(
        Id: entity.EntityId.ToString(),
        Supplies: entity.Supplies,
        Treasury: entity.Treasury,
        Location: entity.Location != null
          ? new BitcraftLocation(entity.Location.X, entity.Location.Z, entity.Location.Dimension)
          : null
      );
  }
}