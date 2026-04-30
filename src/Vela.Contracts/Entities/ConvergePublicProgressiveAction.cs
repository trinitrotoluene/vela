using Convergence.Client;

namespace Vela.Contracts.Entities;

[ConvergenceEntity("PublicProgressiveAction")]
public partial struct ConvergePublicProgressiveAction
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    [Field(1)] public ulong BuildingEntityId { get; set; }
    [Field(2)] public ulong OwnerEntityId { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }
}
