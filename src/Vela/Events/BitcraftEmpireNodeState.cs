namespace Vela.Events;

[Storage(StorageTarget.Cache)]
public record BitcraftEmpireNodeState(
  string Id,
  string EmpireId,
  bool IsActive,
  ulong ChunkIndex,
  uint Dimension,
  int LocationX,
  int LocationZ,
  int Upkeep,
  int Energy
) : BitcraftEventBase(Id);