namespace Vela.Events
{
  // From claim_member_state, keyed by the membership's entity id.
  [ConvergeDb]
  public record BitcraftClaimMemberState(
    string Id,
    string ClaimEntityId,
    string PlayerEntityId,
    bool InventoryPermission,
    bool BuildPermission,
    bool OfficerPermission,
    bool CoOwnerPermission
  ) : BitcraftEventBase(Id);
}
