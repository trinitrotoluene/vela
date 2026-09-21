using Convergence.Client;

namespace Vela.Contracts.Entities;

// Event-only kind: every write targets a single per-module entity, so consume it via
// SubscribeEventsAsync (assert events only) rather than the converged state.
[ConvergenceEntity("MarketTradeEvent", EventStream = true)]
public partial struct ConvergeMarketTradeEvent : IConvergeEvent<ConvergeMarketTradeEvent>
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    [Field(1)] public ulong ClaimId { get; set; }
    [Field(2)] public ulong ListingId { get; set; }
    [Field(3)] public bool IsBuyOrder { get; set; }
    [Field(4)] public ulong BuyerId { get; set; }
    [Field(5)] public ulong SellerId { get; set; }
    [Field(6)] public int ItemId { get; set; }
    [Field(7)] public bool IsCargo { get; set; }
    [Field(8)] public int Quantity { get; set; }
    [Field(9)] public int UnitPrice { get; set; }
    [Field(10)] public int ListingRemainingQuantity { get; set; }
    [Field(11)] public long TimestampMicros { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }

    public ConvergeMarketTradeEvent WithEntityId(ReadOnlyMemory<byte> entityId) => this with { EntityId = entityId };
}
