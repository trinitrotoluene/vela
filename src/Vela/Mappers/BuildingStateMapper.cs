using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class BuildingStateMapper : MappedDbEntityBase<BuildingState, BitcraftBuildingState>
  {
    public override string TopicName => "bitcraft.building.state";

    public override BitcraftBuildingState Map(
      BuildingState entity
    ) => new(
        Id: entity.EntityId.ToString(),
        ClaimEntityId: entity.ClaimEntityId.ToString()
      );
  }
}