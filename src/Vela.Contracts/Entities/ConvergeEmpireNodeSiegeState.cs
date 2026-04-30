using Convergence.Client;

namespace Vela.Contracts.Entities;

[ConvergenceEntity("EmpireNodeSiegeState")]
public partial struct ConvergeEmpireNodeSiegeState
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    [Field(1)] public ulong EmpireId { get; set; }
    [Field(2)] public bool IsActive { get; set; }
    [Field(3)] public ulong BuildingEntityId { get; set; }
    [Field(4)] public int Energy { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }
}
