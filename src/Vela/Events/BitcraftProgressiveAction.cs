using Vela.Events;

[Storage(StorageTarget.Cache)]
public record BitcraftProgressiveAction(
  string Id,
  string RecipeId,
  int CraftCount,
  int Progress
) : BitcraftEventBase(Id);