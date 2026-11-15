using Convergence.Client;

namespace Vela.Contracts.Entities;

// A barter order on a stall: each trade gives the buyer OfferItems in return for RequiredItems,
// up to RemainingStock times. Only orders on barter stalls are published.
[ConvergenceEntity("TradeOrderState")]
public partial struct ConvergeTradeOrderState
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    // The stall's building entity id.
    [Field(1)] public ulong ShopId { get; set; }
    [Field(2)] public int RemainingStock { get; set; }
    [Field(3, MaxCount = 64)] public ConvergeItemStack[] OfferItems { get; set; }
    [Field(4, MaxCount = 64)] public ConvergeItemStack[] RequiredItems { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }
}
