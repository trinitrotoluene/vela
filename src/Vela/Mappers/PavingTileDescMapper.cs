using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class PavingTileDescMapper : MappedDbEntityBase<PavingTileDesc, BitcraftPavingTileDesc>
  {
    public override BitcraftPavingTileDesc Map(
      PavingTileDesc entity
    ) => new(
        Id: entity.Id.ToString(),
        Name: entity.Name,
        Description: entity.Description,
        Tier: entity.Tier,
        PavingDuration: entity.PavingDuration,
        PrefabAddress: entity.PrefabAddress,
        IconAddress: entity.IconAddress,
        InputCargoId: entity.InputCargoId,
        InputCargoDiscoveryScore: entity.InputCargoDiscoveryScore,
        FullDiscoveryScore: entity.FullDiscoveryScore,
        RequiredKnowledges: [.. entity.RequiredKnowledges],
        DiscoveryTriggers: [.. entity.DiscoveryTriggers]
      );
  }
}
