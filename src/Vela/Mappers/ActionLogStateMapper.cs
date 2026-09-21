using System.Text.Json.Serialization;
using SpacetimeDB.Types;
using Vela.Events;

namespace Vela.Mappers
{
  public class ActionLogStateMapper : MappedDbEntityBase<ActionLogState, BitcraftActionLogState>
  {
    public override BitcraftActionLogState Map(
      ActionLogState entity
    ) => new(
        Id: entity.Id.ToString(),
        SubjectEntityId: entity.SubjectEntityId.ToString(),
        SubjectName: entity.SubjectName,
        SubjectType: (BitcraftActionLogSubjectType)entity.SubjectType,
        ObjectEntityId: entity.ObjectEntityId.ToString(),
        Action: entity.Data switch
        {
          ActionLogData.WithdrawItem w => new WithdrawItemStateAction(
            ItemId: w.WithdrawItem_.ItemId.ToString(),
            Quantity: w.WithdrawItem_.Quantity,
            ItemType: (BitcraftItemType)w.WithdrawItem_.ItemType
          ),
          ActionLogData.DepositItem d => new DepositItemStateAction(
            ItemId: d.DepositItem_.ItemId.ToString(),
            Quantity: d.DepositItem_.Quantity,
            ItemType: (BitcraftItemType)d.DepositItem_.ItemType
          ),
          _ => new UnknownItemStateAction()
        }
      );
  }
}