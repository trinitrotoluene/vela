using Convergence.Client;

namespace Vela.Contracts.Entities;

[ConvergenceStruct]
public partial struct ConvergeLocation
{
    [Field(0)] public int X { get; set; }
    [Field(1)] public int Z { get; set; }
    [Field(2)] public uint Dimension { get; set; }
}

[ConvergenceStruct]
public partial struct ConvergeInventoryPocket
{
    [Field(0)] public ulong ItemId { get; set; }
    [Field(1)] public int Quantity { get; set; }
    [Field(2)] public byte ItemType { get; set; }
}

[ConvergenceStruct]
public partial struct ConvergeItemStack
{
    [Field(0)] public int ItemId { get; set; }
    [Field(1)] public int Quantity { get; set; }
    [Field(2)] public bool IsCargo { get; set; }
}
