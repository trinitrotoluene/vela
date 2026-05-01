using Vela.Events;

[ConvergeDb]
public record BitcraftProgressiveAction(
  string Id,
  string RecipeId,
  int CraftCount,
  int Progress
) : BitcraftEventBase(Id);