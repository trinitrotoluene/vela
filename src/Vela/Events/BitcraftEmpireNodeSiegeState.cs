namespace Vela.Events;

public record BitcraftEmpireNodeSiegeState(
  string Id,
  string EmpireId,
  bool IsActive,
  /// This is the BitcraftEmpireNodeState.Id
  string BuildingEntityId,
  int Energy
) : BitcraftEventBase(Id);