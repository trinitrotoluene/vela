using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class BarterStallInventoryEventMapper : MappedDbEntityBase<BarterStallInventoryEvent, BitcraftBarterStallInventoryEvent>
  {
    public override BitcraftBarterStallInventoryEvent Map(
      BarterStallInventoryEvent entity
    ) => new(
        Id: entity.ShopEntityId.ToString(),
        ClaimId: entity.ClaimEntityId.ToString(),
        ActorId: entity.ActorEntityId.ToString(),
        Reason: entity.Reason.ToString(),
        TradeOrderId: entity.TradeOrderEntityId?.ToString(),
        TradeAmount: entity.TradeAmount,
        AddedItems: [.. entity.AddedItems.Select(MapItemStack)],
        RemovedItems: [.. entity.RemovedItems.Select(MapItemStack)],
        TreasuryCoinsAdded: entity.TreasuryCoinsAdded,
        TreasuryCoinsRemoved: entity.TreasuryCoinsRemoved,
        TimestampMicros: entity.Timestamp.MicrosecondsSinceUnixEpoch
      );

    private static BitcraftItemStack MapItemStack(SpacetimeDB.Types.ItemStack stack) => new(
      ItemId: stack.ItemId,
      Quantity: stack.Quantity,
      IsCargo: stack.ItemType == ItemType.Cargo
    );
  }
}
