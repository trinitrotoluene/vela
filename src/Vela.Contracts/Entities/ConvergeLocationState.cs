using Convergence.Client;

namespace Vela.Contracts.Entities;

[ConvergenceEntity("LocationState", DiskBacked = true)]
public partial struct ConvergeLocationState
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    [Field(1)] public int X { get; set; }
    [Field(2)] public int Z { get; set; }
    [Field(3)] public uint Dimension { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }
}
