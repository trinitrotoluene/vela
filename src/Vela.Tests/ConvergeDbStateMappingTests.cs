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
}
