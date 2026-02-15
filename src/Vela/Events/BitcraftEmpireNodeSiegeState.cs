namespace Vela.Events;

[Storage(StorageTarget.Cache)]
public record BitcraftEmpireNodeSiegeState(
  string Id,
  string EmpireId,
  bool IsActive,
  /// This is the BitcraftEmpireNodeState.Id
  string BuildingEntityId,
  int Energy
) : BitcraftEventBase(Id);