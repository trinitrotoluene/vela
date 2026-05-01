namespace Vela.Events
{
  [Postgres]
  public record BitcraftPavingTileDesc(
    string Id,
    string Name,
    string Description,
    int Tier,
    float PavingDuration,
    string PrefabAddress,
    string IconAddress,
    int InputCargoId,
    int InputCargoDiscoveryScore,
    int FullDiscoveryScore,
    int[] RequiredKnowledges,
    int[] DiscoveryTriggers
  ) : BitcraftEventBase(Id);
}
