using Convergence.Client;
using SpacetimeDB.Types;
using Vela.Entities;
using Vela.Mappers;
using Xunit;

namespace Vela.Tests;

// Mapping and conversion of SpacetimeDB state tables into ConvergeDB kinds.
public class ConvergeDbStateMappingTests
{
    [Fact]
    public void Inventory_IsKeyedByItsOwnEntityId()
    {
        // A barter stall can keep items and cargo in separate inventories under one owner; both
        // must survive as separate entities rather than overwrite each other.
        InventoryState Inventory(ulong entityId, int index) => new()
        {
            EntityId = entityId,
            OwnerEntityId = 500,
            PlayerOwnerEntityId = 0,
            InventoryIndex = index,
            CargoIndex = index == 0 ? 1 : 0,
            Pockets = [new Pocket { Contents = new ItemStack { ItemId = 1, Quantity = 3, ItemType = ItemType.Cargo } }],
        };

        var items = ConvergeDbConverters.ToConverge(new InventoryStateMapper().Map(Inventory(10, 0)), "bitcraft-live-3");
        var cargo = ConvergeDbConverters.ToConverge(new InventoryStateMapper().Map(Inventory(11, 1)), "bitcraft-live-3");

        Assert.Equal(10UL, EntityId.ToULong(items.EntityId));
        Assert.Equal(11UL, EntityId.ToULong(cargo.EntityId));
        Assert.Equal(500UL, items.OwnerEntityId);
        Assert.Equal(500UL, cargo.OwnerEntityId);
        Assert.Equal(0, items.InventoryIndex);
        Assert.Equal(1, cargo.InventoryIndex);
        Assert.Equal(1, items.CargoIndex);
        Assert.Equal(0, cargo.CargoIndex);
        Assert.Equal(2, Assert.Single(cargo.Pockets).ItemType);
    }

    [Fact]
    public void Inventory_BankPersonal_KeepsPlayerOwner()
    {
        var row = new InventoryState
        {
            EntityId = 20,
            OwnerEntityId = 600,
            PlayerOwnerEntityId = 42,
            InventoryIndex = 0,
            Pockets = [],
        };

        var converged = ConvergeDbConverters.ToConverge(new InventoryStateMapper().Map(row), "bitcraft-live-3");

        Assert.Equal(600UL, converged.OwnerEntityId);
        Assert.Equal(42UL, converged.PlayerOwnerEntityId);
    }

    [Fact]
    public void Building_KeepsDescriptionId()
    {
        var row = new BuildingState { EntityId = 7, ClaimEntityId = 8, BuildingDescriptionId = 405 };

        var converged = ConvergeDbConverters.ToConverge(new BuildingStateMapper().Map(row), "bitcraft-live-3");

        Assert.Equal(8UL, converged.ClaimEntityId);
        Assert.Equal(405, converged.BuildingDescriptionId);
    }

    [Fact]
    public void BuildingNickname_MapsAndConverts()
    {
        var row = new BuildingNicknameState { EntityId = 7, Nickname = "Town Stall" };

        var converged = ConvergeDbConverters.ToConverge(new BuildingNicknameStateMapper().Map(row), "bitcraft-live-3");

        Assert.Equal(7UL, EntityId.ToULong(converged.EntityId));
        Assert.Equal("bitcraft-live-3", converged.Module);
        Assert.Equal("Town Stall", converged.Nickname);
    }

    [Fact]
    public void BarterStall_IsKeyedByItsBuilding()
    {
        var converged = ConvergeDbConverters.ToConverge(
            new BarterStallStateMapper().Map(new BarterStallState { EntityId = 42, MarketModeEnabled = false }), "bitcraft-live-3");

        Assert.Equal(42UL, EntityId.ToULong(converged.EntityId));
        Assert.Equal("bitcraft-live-3", converged.Module);
    }

    [Fact]
    public void TradeOrder_MapsStallAndBothSidesOfTheTrade()
    {
        var row = new TradeOrderState
        {
            EntityId = 9,
            ShopEntityId = 42,
            RemainingStock = 3,
            OfferItems = [new ItemStack { ItemId = 100, Quantity = 5, ItemType = ItemType.Item }],
            OfferCargoId = [],
            RequiredItems =
            [
                new ItemStack { ItemId = 1, Quantity = 20, ItemType = ItemType.Item },
                new ItemStack { ItemId = 7, Quantity = 1, ItemType = ItemType.Cargo },
            ],
            RequiredCargoId = [],
            TravelerTradeOrderId = null,
        };

        var converged = ConvergeDbConverters.ToConverge(new TradeOrderStateMapper().Map(row), "bitcraft-live-3");

        Assert.Equal(9UL, EntityId.ToULong(converged.EntityId));
        Assert.Equal(42UL, converged.ShopId);
        Assert.Equal(3, converged.RemainingStock);
        var offer = Assert.Single(converged.OfferItems);
        Assert.Equal((100, 5, false), (offer.ItemId, offer.Quantity, offer.IsCargo));
        Assert.Equal(2, converged.RequiredItems.Length);
        Assert.Equal((7, 1, true), (converged.RequiredItems[1].ItemId, converged.RequiredItems[1].Quantity, converged.RequiredItems[1].IsCargo));
    }

    [Fact]
    public void Interiors_MapDimensionToNetworkToBuildingOrHousing()
    {
        var dimension = ConvergeDbConverters.ToConverge(new DimensionDescriptionStateMapper().Map(new DimensionDescriptionState
        {
            EntityId = 1, DimensionNetworkEntityId = 2, DimensionId = 3, DimensionType = DimensionType.Unknown,
        }), "bitcraft-live-3");
        var network = ConvergeDbConverters.ToConverge(new DimensionNetworkStateMapper().Map(new DimensionNetworkState
        {
            EntityId = 2, BuildingId = 4, ClaimEntityId = 5,
        }), "bitcraft-live-3");
        var housing = ConvergeDbConverters.ToConverge(new PlayerHousingStateMapper().Map(new PlayerHousingState
        {
            EntityId = 6, EntranceBuildingEntityId = 7, NetworkEntityId = 2,
        }), "bitcraft-live-3");

        Assert.Equal((1UL, 2UL, 3U), (EntityId.ToULong(dimension.EntityId), dimension.DimensionNetworkId, dimension.DimensionId));
        Assert.Equal((2UL, 4UL, 5UL), (EntityId.ToULong(network.EntityId), network.BuildingId, network.ClaimEntityId));
        Assert.Equal((6UL, 7UL, 2UL), (EntityId.ToULong(housing.EntityId), housing.EntranceBuildingId, housing.NetworkId));
    }

    [Fact]
    public void Equipment_KeepsEverySlotInPlace()
    {
        // Slots line up with the game's, so an empty one stays as an empty pocket rather than
        // shifting the rest.
        List<EquipmentSlot> Slots() =>
        [
            new EquipmentSlot { Item = null, Primary = EquipmentSlotType.HeadClothing },
            new EquipmentSlot { Item = new ItemStack { ItemId = 7, Quantity = 1, ItemType = ItemType.Item }, Primary = EquipmentSlotType.TorsoClothing },
        ];

        var worn = ConvergeDbConverters.ToConverge(
            new EquipmentStateMapper().Map(new EquipmentState { EntityId = 42, EquipmentSlots = Slots() }), "bitcraft-live-3");
        var preset = ConvergeDbConverters.ToConverge(new EquipmentPresetStateMapper().Map(new EquipmentPresetState
        {
            EntityId = 9, PlayerEntityId = 42, Index = 2, Active = true, EquipmentSlots = Slots(),
        }), "bitcraft-live-3");

        Assert.Equal(42UL, EntityId.ToULong(worn.EntityId));
        Assert.Equal((9UL, 42UL, 2, true), (EntityId.ToULong(preset.EntityId), preset.PlayerEntityId, preset.Index, preset.Active));
        foreach (var slots in new[] { worn.Slots, preset.Slots })
        {
            Assert.Equal(2, slots.Length);
            Assert.Equal(0, slots[0].ItemType);
            Assert.Equal((7UL, 1, (byte)1), (slots[1].ItemId, slots[1].Quantity, slots[1].ItemType));
        }
    }

    [Fact]
    public void Deployable_IsKeyedByItselfAndKeepsItsOwner()
    {
        // Keyed by the deployable, which is what its inventory's OwnerEntityId points at.
        var converged = ConvergeDbConverters.ToConverge(new DeployableStateMapper().Map(new DeployableStateV2
        {
            EntityId = 5, OwnerId = 42, ClaimEntityId = 8, DeployableDescriptionId = 3, Nickname = "Boat", Hidden = true,
        }), "bitcraft-live-3");

        Assert.Equal((5UL, 42UL, 8UL, true, 3),
            (EntityId.ToULong(converged.EntityId), converged.OwnerId, converged.ClaimEntityId, converged.Hidden, converged.DeployableDescriptionId));
    }

    [Fact]
    public void ClaimMember_KeepsIdsAndPermissionsApart()
    {
        // The ids are both ulongs and the permissions all bools, so a swap would compile and quietly
        // put players in the wrong claims or give them the wrong rights.
        var converged = ConvergeDbConverters.ToConverge(new ClaimMemberStateMapper().Map(new ClaimMemberState
        {
            EntityId = 5, ClaimEntityId = 8, PlayerEntityId = 42, UserName = "Ann",
            InventoryPermission = true, BuildPermission = false, OfficerPermission = false, CoOwnerPermission = true,
        }), "bitcraft-live-3");

        Assert.Equal((5UL, 8UL, 42UL, true, false, false, true),
            (EntityId.ToULong(converged.EntityId), converged.ClaimEntityId, converged.PlayerEntityId,
             converged.InventoryPermission, converged.BuildPermission, converged.OfficerPermission,
             converged.CoOwnerPermission));
    }
}
