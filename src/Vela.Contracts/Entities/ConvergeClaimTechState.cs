using Convergence.Client;

namespace Vela.Contracts.Entities;

[ConvergenceEntity("ClaimTechState")]
public partial struct ConvergeClaimTechState
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    [Field(1, MaxCount = 256)] public int[] Learned { get; set; }
    [Field(2)] public int Researching { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }
}
