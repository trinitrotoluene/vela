namespace Vela.Events
{
  [Storage(StorageTarget.Cache)]
  public record BitcraftChatMessage(
    string Id,
    int ChannelId,
    string SenderId,
    string SenderUsername,
    string Content
  ) : BitcraftEventBase(Id);
}