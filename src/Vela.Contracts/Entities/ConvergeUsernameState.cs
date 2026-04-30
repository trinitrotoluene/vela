using Convergence.Client;

namespace Vela.Contracts.Entities;

[ConvergenceEntity("UsernameState")]
public partial struct ConvergeUsernameState
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    [Field(1, MaxLength = 256)] public string Username { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }
}
