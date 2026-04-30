using Convergence.Client;

namespace Vela.Contracts.Entities;

[ConvergenceEntity("EmpireState")]
public partial struct ConvergeEmpireState
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    [Field(1, MaxLength = 256)] public string Name { get; set; }
    [Field(2)] public int ShardTreasury { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }
}
