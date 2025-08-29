using Vela.Events;

public record BitcraftProgressiveAction(
  string Id,
  string RecipeId,
  int CraftCount,
  int Progress
) : BitcraftEventBase(Id);