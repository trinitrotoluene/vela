using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class CargoItemDescMapper : MappedDbEntityBase<CargoDesc, BitcraftCargoItem>
  {
    public override BitcraftCargoItem Map(CargoDesc entity) =>
        new(
          Id: entity.Id.ToString(),
          Name: entity.Name,
          Description: entity.Description,
          Volume: entity.Volume,
          Tier: entity.Tier,
          Rarity: (BitcraftItemRarity)entity.Rarity,
          Tag: entity.Tag,
          NotPickupable: entity.NotPickupable,
          BlocksPath: entity.BlocksPath
          );
  }
}