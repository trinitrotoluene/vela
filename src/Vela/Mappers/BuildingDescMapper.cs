using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class BuildingDescMapper : MappedDbEntityBase<BuildingDesc, BitcraftBuildingDesc>
  {
    public override string TopicName => "bitcraft.building.desc";

    public override BitcraftBuildingDesc Map(
      BuildingDesc entity
    ) => new(
        Id: entity.Id,
        Name: entity.Name,
        Description: entity.Description
      );
  }
}