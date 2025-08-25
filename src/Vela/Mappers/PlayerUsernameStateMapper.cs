using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class PlayerUsernameStateMapper : MappedDbEntityBase<PlayerUsernameState, BitcraftUsernameState>
  {
    public override string TopicName => "bitcraft.player.username";

    public override BitcraftUsernameState Map(PlayerUsernameState entity) =>
        new(
          Id: entity.EntityId.ToString(),
          Username: entity.Username
        );
  }
}