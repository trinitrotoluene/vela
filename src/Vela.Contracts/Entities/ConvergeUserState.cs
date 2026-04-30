using Convergence.Client;

namespace Vela.Contracts.Entities;

[ConvergenceEntity("UserState")]
public partial struct ConvergeUserState
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    [Field(1)] public ulong UserEntityId { get; set; }
    [Field(2)] public bool CanSignIn { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }
}
