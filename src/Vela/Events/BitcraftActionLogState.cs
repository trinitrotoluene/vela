using System.Text.Json.Serialization;

namespace Vela.Events;

public record BitcraftActionLogState(
  string Id,
  string SubjectEntityId,
  string SubjectName,
  BitcraftActionLogSubjectType SubjectType,
  string ObjectEntityId,
  BitcraftActionLogStateAction Action
) : BitcraftEventBase(Id);


public enum BitcraftActionLogSubjectType
{
  Player = 0,
}

[JsonDerivedType(typeof(DepositItemStateAction), nameof(DepositItemStateAction))]
[JsonDerivedType(typeof(WithdrawItemStateAction), nameof(WithdrawItemStateAction))]
[JsonDerivedType(typeof(UnknownItemStateAction), nameof(UnknownItemStateAction))]
public abstract record BitcraftActionLogStateAction(
);

public record DepositItemStateAction(
  string ItemId,
  int Quantity
) : BitcraftActionLogStateAction();

public record WithdrawItemStateAction(
  string ItemId,
  int Quantity
) : BitcraftActionLogStateAction();

public record UnknownItemStateAction() : BitcraftActionLogStateAction();