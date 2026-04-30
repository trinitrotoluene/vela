using Convergence.Client;

namespace Vela.Contracts.Entities;

[ConvergenceEntity("ClaimLocalState")]
public partial struct ConvergeClaimLocalState
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    [Field(1)] public int Supplies { get; set; }
    [Field(2)] public uint Treasury { get; set; }
    [Field(3)] public int BuildingDescriptionId { get; set; }
    [Field(4)] public bool HasLocation { get; set; }
    [Field(5)] public ConvergeLocation Location { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }
}
