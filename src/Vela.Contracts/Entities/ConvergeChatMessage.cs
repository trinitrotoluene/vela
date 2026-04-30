using Convergence.Client;

namespace Vela.Contracts.Entities;

[ConvergenceEntity("ChatMessage", EventStream = true)]
public partial struct ConvergeChatMessage
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    [Field(1)] public int ChannelId { get; set; }
    [Field(2)] public ulong SenderId { get; set; }
    [Field(3, MaxLength = 256)] public string SenderUsername { get; set; }
    [Field(4, MaxLength = 4096)] public string Content { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }
}
