using Convergence.Client;

namespace Vela.Contracts.Entities;

[ConvergenceEntity("ClosedListingState", EventStream = true)]
public partial struct ConvergeClosedListingState
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    [Field(1)] public ulong OwnerId { get; set; }
    [Field(2)] public ulong ClaimId { get; set; }
    [Field(3)] public ConvergeItemStack ItemStack { get; set; }
    [Field(4, MaxLength = 128)] public string Timestamp { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }
}
