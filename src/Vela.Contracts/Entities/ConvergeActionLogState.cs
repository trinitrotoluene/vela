using Convergence.Client;

namespace Vela.Contracts.Entities;

[ConvergenceEntity("ActionLogState", EventStream = true)]
public partial struct ConvergeActionLogState
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    [Field(1)] public ulong SubjectEntityId { get; set; }
    [Field(2, MaxLength = 256)] public string SubjectName { get; set; }
    [Field(3)] public byte SubjectType { get; set; }
    [Field(4)] public ulong ObjectEntityId { get; set; }
    // Discriminator: 0 = Deposit, 1 = Withdraw, 2 = Unknown
    [Field(5)] public byte ActionType { get; set; }
    [Field(6)] public ulong ActionItemId { get; set; }
    [Field(7)] public int ActionQuantity { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }
}
