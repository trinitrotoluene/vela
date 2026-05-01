namespace Vela.Events
{
  [ConvergeDb]
  public record BitcraftChatMessage(
    string Id,
    int ChannelId,
    string SenderId,
    string SenderUsername,
    string Content
  ) : BitcraftEventBase(Id);
}