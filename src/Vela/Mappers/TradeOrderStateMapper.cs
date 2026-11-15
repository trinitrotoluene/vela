using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class TradeOrderStateMapper : MappedDbEntityBase<TradeOrderState, BitcraftTradeOrderState>
  {
    // OfferCargoId/RequiredCargoId are obsolete in the game (cargo now travels in the item stacks).
    public override BitcraftTradeOrderState Map(TradeOrderState entity) =>
        new(
          Id: entity.EntityId.ToString(),
          ShopId: entity.ShopEntityId.ToString(),
          RemainingStock: entity.RemainingStock,
          OfferItems: [.. entity.OfferItems.Select(MapItemStack)],
          RequiredItems: [.. entity.RequiredItems.Select(MapItemStack)]
        );

    private static BitcraftItemStack MapItemStack(SpacetimeDB.Types.ItemStack stack) => new(
      ItemId: stack.ItemId,
      Quantity: stack.Quantity,
      IsCargo: stack.ItemType == ItemType.Cargo
    );
  }
}
