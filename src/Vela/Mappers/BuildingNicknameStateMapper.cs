using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class BuildingNicknameStateMapper : MappedDbEntityBase<BuildingNicknameState, BitcraftBuildingNicknameState>
  {
    public override BitcraftBuildingNicknameState Map(BuildingNicknameState entity) =>
        new(
          Id: entity.EntityId.ToString(),
          Nickname: entity.Nickname
        );
  }
}
