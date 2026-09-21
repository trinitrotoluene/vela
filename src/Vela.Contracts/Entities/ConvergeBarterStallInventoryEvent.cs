using Convergence.Client;

namespace Vela.Contracts.Entities;

public enum BarterStallInventoryChangeReason : byte
{
    Unknown = 0,
    Sale = 1,
}

// Event-only kind: every write targets a single per-module entity, so consume it via
// SubscribeEventsAsync (assert events only) rather than the converged state.
[ConvergenceEntity("BarterStallInventoryEvent", EventStream = true)]
public partial struct ConvergeBarterStallInventoryEvent : IConvergeEvent<ConvergeBarterStallInventoryEvent>
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    [Field(1)] public ulong ShopId { get; set; }
    [Field(2)] public ulong ClaimId { get; set; }
    [Field(3)] public ulong ActorId { get; set; }
    // ConvergeDB fields can't be enums; the enum is stored as its byte value.
    [Field(4)] public byte ReasonCode { get; set; }
    // TradeOrderId/TradeAmount are only meaningful when HasTrade is set (0 otherwise).
    [Field(5)] public bool HasTrade { get; set; }
    [Field(6)] public ulong TradeOrderId { get; set; }
    [Field(7)] public int TradeAmount { get; set; }
    [Field(8, MaxCount = 64)] public ConvergeItemStack[] AddedItems { get; set; }
    [Field(9, MaxCount = 64)] public ConvergeItemStack[] RemovedItems { get; set; }
    [Field(10)] public int TreasuryCoinsAdded { get; set; }
    [Field(11)] public int TreasuryCoinsRemoved { get; set; }
    [Field(12)] public long TimestampMicros { get; set; }

    public BarterStallInventoryChangeReason Reason
    {
        readonly get => (BarterStallInventoryChangeReason)ReasonCode;
        set => ReasonCode = (byte)value;
    }

    public ReadOnlyMemory<byte> EntityId { get; init; }

    public ConvergeBarterStallInventoryEvent WithEntityId(ReadOnlyMemory<byte> entityId) => this with { EntityId = entityId };
}
