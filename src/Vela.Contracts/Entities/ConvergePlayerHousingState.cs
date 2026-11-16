using Convergence.Client;

namespace Vela.Contracts.Entities;

// A player's housing, keyed by the owning player's entity id.
[ConvergenceEntity("PlayerHousingState")]
public partial struct ConvergePlayerHousingState
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    // The building in the world the housing is entered from; its claim is the housing's claim.
    [Field(1)] public ulong EntranceBuildingId { get; set; }
    [Field(2)] public ulong NetworkId { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }
}
