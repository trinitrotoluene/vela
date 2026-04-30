using Convergence.Client;

namespace Vela.Contracts.Entities;

[ConvergenceEntity("EmpireNodeState")]
public partial struct ConvergeEmpireNodeState
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    [Field(1)] public ulong EmpireId { get; set; }
    [Field(2)] public bool IsActive { get; set; }
    [Field(3)] public ulong ChunkIndex { get; set; }
    [Field(4)] public uint Dimension { get; set; }
    [Field(5)] public int LocationX { get; set; }
    [Field(6)] public int LocationZ { get; set; }
    [Field(7)] public int Upkeep { get; set; }
    [Field(8)] public int Energy { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }
}
