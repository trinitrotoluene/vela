using Convergence.Client;

namespace Vela.Contracts.Entities;

[ConvergenceEntity("BuildingNicknameState")]
public partial struct ConvergeBuildingNicknameState
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    // The game caps nicknames at 35 characters; 128 bytes leaves room for multi-byte UTF-8.
    [Field(1, MaxLength = 128)] public string Nickname { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }
}
