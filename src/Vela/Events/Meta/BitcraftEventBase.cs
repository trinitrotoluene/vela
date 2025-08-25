namespace Vela.Events
{
  public abstract record BitcraftEventBase(string Id)
  {
    public static Type[] SchemaTypes = [
      typeof(BitcraftAuctionListingState),
      typeof(BitcraftBuildingDesc),
      typeof(BitcraftBuildingState),
      typeof(BitcraftChatMessage),
      typeof(BitcraftClaimState),
      typeof(BitcraftEmpireState),
      typeof(BitcraftItem),
      typeof(BitcraftItemList),
      typeof(BitcraftPublicProgressiveAction),
      typeof(BitcraftRecipe),
      typeof(BitcraftUserModerationState),
      typeof(BitcraftUsernameState)
    ];

    public static readonly string[] SchemaNames = [.. SchemaTypes.Select(x => x.Name)];
  };
}