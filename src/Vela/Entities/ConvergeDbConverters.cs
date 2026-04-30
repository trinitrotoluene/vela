using Convergence.Client;
using Vela.Contracts.Entities;
using Vela.Events;

namespace Vela.Entities;

public static class ConvergeDbConverters
{
    private static ReadOnlyMemory<byte> NumericId(string id) => EntityId.FromULong(ulong.Parse(id));
    private static ReadOnlyMemory<byte> IdentityId(string hexId) => Convert.FromHexString(hexId);

    // --- Dynamic state entities (module-scoped) ---

    public static ConvergeBuildingState ToConverge(BitcraftBuildingState e, string module) => new()
    {
        EntityId = NumericId(e.Id),
        Module = module,
        ClaimEntityId = ulong.Parse(e.ClaimEntityId),
    };

    public static ConvergeLocationState ToConverge(BitcraftLocationState e, string module) => new()
    {
        EntityId = NumericId(e.Id),
        Module = module,
        X = e.X,
        Z = e.Z,
        Dimension = e.Dimension,
    };

    public static ConvergeEmpireNodeState ToConverge(BitcraftEmpireNodeState e, string module) => new()
    {
        EntityId = NumericId(e.Id),
        Module = module,
        EmpireId = ulong.Parse(e.EmpireId),
        IsActive = e.IsActive,
        ChunkIndex = e.ChunkIndex,
        Dimension = e.Dimension,
        LocationX = e.LocationX,
        LocationZ = e.LocationZ,
        Upkeep = e.Upkeep,
        Energy = e.Energy,
    };

    public static ConvergeEmpireNodeSiegeState ToConverge(BitcraftEmpireNodeSiegeState e, string module) => new()
    {
        EntityId = NumericId(e.Id),
        Module = module,
        EmpireId = ulong.Parse(e.EmpireId),
        IsActive = e.IsActive,
        BuildingEntityId = ulong.Parse(e.BuildingEntityId),
        Energy = e.Energy,
    };

    public static ConvergeUserState ToConverge(BitcraftUserState e, string module) => new()
    {
        EntityId = IdentityId(e.Id),
        Module = module,
        UserEntityId = ulong.Parse(e.UserEntityId),
        CanSignIn = e.CanSignIn,
    };

    public static ConvergeProgressiveAction ToConverge(BitcraftProgressiveAction e, string module) => new()
    {
        EntityId = NumericId(e.Id),
        Module = module,
        RecipeId = int.Parse(e.RecipeId),
        CraftCount = e.CraftCount,
        Progress = e.Progress,
    };

    public static ConvergePublicProgressiveAction ToConverge(BitcraftPublicProgressiveAction e, string module) => new()
    {
        EntityId = NumericId(e.Id),
        Module = module,
        BuildingEntityId = ulong.Parse(e.BuildingEntityId),
        OwnerEntityId = ulong.Parse(e.OwnerEntityId),
    };

    public static ConvergePavedTileState ToConverge(BitcraftPavedTileState e, string module) => new()
    {
        EntityId = NumericId(e.Id),
        Module = module,
        TileTypeId = e.TileTypeId,
        RelatedEntityId = ulong.Parse(e.RelatedEntityId),
    };

    public static ConvergeClosedListingState ToConverge(BitcraftClosedListingState e, string module) => new()
    {
        EntityId = NumericId(e.Id),
        Module = module,
        OwnerId = ulong.Parse(e.OwnerId),
        ClaimId = ulong.Parse(e.ClaimId),
        ItemStack = new ConvergeItemStack
        {
            ItemId = e.ItemStack.ItemId,
            Quantity = e.ItemStack.Quantity,
            IsCargo = e.ItemStack.IsCargo,
        },
        Timestamp = e.Timestamp,
    };

    // --- Event stream entities (module-scoped) ---

    public static ConvergeChatMessage ToConverge(BitcraftChatMessage e, string module) => new()
    {
        EntityId = NumericId(e.Id),
        Module = module,
        ChannelId = e.ChannelId,
        SenderId = ulong.Parse(e.SenderId),
        SenderUsername = e.SenderUsername,
        Content = e.Content,
    };

    public static ConvergeActionLogState ToConverge(BitcraftActionLogState e, string module) => new()
    {
        EntityId = NumericId(e.Id),
        Module = module,
        SubjectEntityId = ulong.Parse(e.SubjectEntityId),
        SubjectName = e.SubjectName,
        SubjectType = (byte)e.SubjectType,
        ObjectEntityId = ulong.Parse(e.ObjectEntityId),
        ActionType = e.Action switch
        {
            DepositItemStateAction => 0,
            WithdrawItemStateAction => 1,
            _ => 2,
        },
        ActionItemId = e.Action switch
        {
            DepositItemStateAction d => ulong.Parse(d.ItemId),
            WithdrawItemStateAction w => ulong.Parse(w.ItemId),
            _ => 0,
        },
        ActionQuantity = e.Action switch
        {
            DepositItemStateAction d => d.Quantity,
            WithdrawItemStateAction w => w.Quantity,
            _ => 0,
        },
    };

    public static ConvergeInventoryState ToConverge(BitcraftInventoryState e, string module) => new()
    {
        EntityId = NumericId(e.Id),
        Module = module,
        Pockets = e.Pockets.Select(p => new ConvergeInventoryPocket
        {
            ItemId = p.ItemId is not null ? ulong.Parse(p.ItemId) : 0,
            Quantity = p.Quantity ?? 0,
            ItemType = p.ItemType switch
            {
                "Item" => 1,
                "Cargo" => 2,
                _ => 0,
            },
        }).ToArray(),
    };

    // --- Global entities ---

    public static ConvergeClaimState ToConverge(BitcraftClaimState e, string module) => new()
    {
        EntityId = NumericId(e.Id),
        Module = module,
        OwnerPlayerId = ulong.Parse(e.OwnerPlayerId),
        OwnerBuildingId = ulong.Parse(e.OwnerBuildingId),
        Name = e.Name,
        IsNeutral = e.IsNeutral,
    };

    public static ConvergeClaimLocalState ToConverge(BitcraftClaimLocalState e, string module) => new()
    {
        EntityId = NumericId(e.Id),
        Module = module,
        Supplies = e.Supplies,
        Treasury = e.Treasury,
        BuildingDescriptionId = e.BuildingDescriptionId,
        HasLocation = e.Location is not null,
        Location = e.Location is { } loc
            ? new ConvergeLocation { X = loc.X, Z = loc.Z, Dimension = loc.Dimension }
            : default,
    };

    public static ConvergeClaimTechState ToConverge(BitcraftClaimTechState e, string module) => new()
    {
        EntityId = NumericId(e.Id),
        Module = module,
        Learned = e.Learned,
        Researching = e.Researching,
    };

    public static ConvergeEmpireState ToConverge(BitcraftEmpireState e, string module) => new()
    {
        EntityId = NumericId(e.Id),
        Module = module,
        Name = e.Name,
        ShardTreasury = e.ShardTreasury,
    };

    public static ConvergeUsernameState ToConverge(BitcraftUsernameState e, string module) => new()
    {
        EntityId = NumericId(e.Id),
        Module = module,
        Username = e.Username,
    };

    public static ConvergeAuctionListingState ToConverge(BitcraftAuctionListingState e, string module) => new()
    {
        EntityId = NumericId(e.Id),
        Module = module,
        OwnerId = ulong.Parse(e.OwnerId),
        ClaimId = ulong.Parse(e.ClaimId),
        Price = e.Price,
        Quantity = e.Quantity,
        StoredCoins = e.StoredCoins,
        ItemId = e.ItemId,
        IsCargoItem = e.IsCargoItem,
    };
}

