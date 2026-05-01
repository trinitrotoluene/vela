using Vela.Events;

[ConvergeDb]
public record BitcraftLocationState(
  string Id,
  int X,
  int Z,
  uint Dimension
) : BitcraftEventBase(Id);