using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class ChatMessageStateMapper : MappedDbEntityBase<ChatMessageState, BitcraftChatMessage>
  {
    public override string TopicName => "bitcraft.chat.message";

    public override BitcraftChatMessage Map(
      ChatMessageState entity
    ) => new(
        Id: entity.EntityId.ToString(),
        ChannelId: entity.ChannelId,
        SenderId: entity.OwnerEntityId.ToString(),
        SenderUsername: entity.Username,
        Content: entity.Text
      );
  }
}