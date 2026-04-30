using Convergence.Client;

namespace Vela.Contracts.Entities;

[ConvergenceEntity("AuctionListingState", EventStream = true)]
public partial struct ConvergeAuctionListingState
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    [Field(1)] public ulong OwnerId { get; set; }
    [Field(2)] public ulong ClaimId { get; set; }
    [Field(3)] public int Price { get; set; }
    [Field(4)] public int Quantity { get; set; }
    [Field(5)] public int StoredCoins { get; set; }
    [Field(6)] public int ItemId { get; set; }
    [Field(7)] public bool IsCargoItem { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }
}
