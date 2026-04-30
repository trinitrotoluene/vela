using Convergence.Client;

namespace Vela.Contracts.Entities;

[ConvergenceEntity("ProgressiveAction")]
public partial struct ConvergeProgressiveAction
{
    [Field(0, MaxLength = 128)] public string Module { get; set; }
    [Field(1)] public int RecipeId { get; set; }
    [Field(2)] public int CraftCount { get; set; }
    [Field(3)] public int Progress { get; set; }

    public ReadOnlyMemory<byte> EntityId { get; init; }
}
