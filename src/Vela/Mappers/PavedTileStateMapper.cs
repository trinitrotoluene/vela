using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class PavedTileStateMapper : MappedDbEntityBase<PavedTileState, BitcraftPavedTileState>
  {
    public override BitcraftPavedTileState Map(
      PavedTileState entity
    ) => new(
        Id: entity.EntityId.ToString(),
        TileTypeId: entity.TileTypeId,
        RelatedEntityId: entity.RelatedEntityId.ToString()
      );
  }
}
