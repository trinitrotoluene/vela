using SpacetimeDB;
using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class UserStateMapper : MappedDbEntityBase<UserState, BitcraftUserState>
  {
    public override BitcraftUserState Map(UserState entity) =>
        new(
          Id: entity.Identity.ToString(),
          UserEntityId: entity.EntityId.ToString(),
          CanSignIn: entity.CanSignIn
        );
  }
}