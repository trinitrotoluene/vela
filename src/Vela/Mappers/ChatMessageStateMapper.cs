using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class ChatMessageStateMapper : MappedDbEntityBase<ChatMessageState, BitcraftChatMessage>
  {
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