using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class PlayerHousingStateMapper : MappedDbEntityBase<PlayerHousingState, BitcraftPlayerHousingState>
  {
    public override BitcraftPlayerHousingState Map(PlayerHousingState entity) =>
        new(
          Id: entity.EntityId.ToString(),
          EntranceBuildingId: entity.EntranceBuildingEntityId.ToString(),
          NetworkId: entity.NetworkEntityId.ToString()
        );
  }
}
