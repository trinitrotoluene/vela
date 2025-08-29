using Vela.Events;

public record BitcraftLocationState(
  string Id,
  int X,
  int Z
) : BitcraftEventBase(Id);