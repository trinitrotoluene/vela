using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpacetimeDB.Types;
using Vela.Data;
using Vela.Entities;
using Vela.Events;
using Vela.Mappers;
using Xunit;
using SdkItemStack = SpacetimeDB.Types.ItemStack;
using SdkItemListPossibility = SpacetimeDB.Types.ItemListPossibility;
using SdkToolRequirement = SpacetimeDB.Types.ToolRequirement;

namespace Vela.Tests;

// Mapping of descriptor tables into the Postgres records the recipe graph is built from.
// Item and cargo ids overlap (e.g. 1 is both Hex Coin and Sagi Bird), so every stack must carry its type.
public class DescriptorMapperTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new ServiceCollection().AddJsonSerializer().BuildServiceProvider().GetRequiredService<JsonSerializerOptions>();

    [Fact]
    public void CraftingRecipe_KeepsItemTypeOnStacks()
    {
        var row = new CraftingRecipeDesc
        {
            Id = 10,
            Name = "Harvest {0}",
            TimeRequirement = 1.5f,
            StaminaRequirement = 2.5f,
            RequiredClaimTechId = 7,
            RequiredKnowledges = [100],
            BlockingKnowledges = [200],
            ToolRequirements = [new SdkToolRequirement { ToolType = 3, Level = 2, Power = 4 }],
            ConsumedItemStacks = [new InputItemStack { ItemId = 1, Quantity = 1, ItemType = ItemType.Cargo, ConsumptionChance = 1 }],
            CraftedItemStacks = [new SdkItemStack { ItemId = 1, Quantity = 5, ItemType = ItemType.Item }],
        };

        var mapped = new CraftingRecipeDescMapper().Map(row);

        Assert.Equal(BitcraftItemType.Cargo, Assert.Single(mapped.ConsumedItemStacks).ItemType);
        Assert.Equal(BitcraftItemType.Item, Assert.Single(mapped.ProducedItemStacks).ItemType);
        Assert.Equal(4, Assert.Single(mapped.ToolRequirements).Power);
        Assert.Equal(1.5f, mapped.TimeRequirement);
        Assert.Equal(2.5f, mapped.StaminaRequirement);
        Assert.Equal(7, mapped.RequiredClaimTechId);
        Assert.Equal([100], mapped.RequiredKnowledges);
        Assert.Equal([200], mapped.BlockingKnowledges);

        // jsonb columns are written with the app's serializer options.
        Assert.Contains("\"ItemType\":\"cargo\"", JsonSerializer.Serialize(mapped.ConsumedItemStacks, JsonOptions));
        Assert.Contains("\"ItemType\":\"item\"", JsonSerializer.Serialize(mapped.ProducedItemStacks, JsonOptions));
    }

    [Fact]
    public void ItemList_KeepsItemTypeOnEntries()
    {
        var row = new ItemListDesc
        {
            Id = 20,
            Name = "Pack",
            Possibilities =
            [
                new SdkItemListPossibility
                {
                    Probability = 1,
                    Items =
                    [
                        new SdkItemStack { ItemId = 1010001, Quantity = 1, ItemType = ItemType.Cargo },
                        new SdkItemStack { ItemId = 1010001, Quantity = 2, ItemType = ItemType.Item },
                    ],
                },
            ],
        };

        var items = Assert.Single(new ItemListDescMapper().Map(row).Possibilities).Items;

        Assert.Equal([BitcraftItemType.Cargo, BitcraftItemType.Item], items.Select(x => x.ItemType));
    }

    [Fact]
    public void ExtractionRecipe_KeepsItemTypeAndDropsEmptyYields()
    {
        var row = new ExtractionRecipeDesc
        {
            Id = 40,
            ResourceId = 50,
            VerbPhrase = "Chop",
            RequiredKnowledges = [100],
            ConsumedItemStacks = [new InputItemStack { ItemId = 1, Quantity = 1, ItemType = ItemType.Item, ConsumptionChance = 1 }],
            ExtractedItemStacks =
            [
                new ProbabilisticItemStack { ItemStack = new SdkItemStack { ItemId = 1, Quantity = 2, ItemType = ItemType.Cargo }, Probability = 0.25f },
                new ProbabilisticItemStack { ItemStack = null, Probability = 0.5f },
            ],
        };

        var mapped = new ExtractionRecipeDescMapper().Map(row);

        Assert.Equal(BitcraftItemType.Item, Assert.Single(mapped.ConsumedItemStacks).ItemType);
        var extracted = Assert.Single(mapped.ExtractedItemStacks);
        Assert.Equal("1", extracted.ItemId);
        Assert.Equal(BitcraftItemType.Cargo, extracted.ItemType);
        Assert.Equal(0.25f, extracted.Probability);
        Assert.Equal(50, mapped.ResourceId);
        Assert.Equal([100], mapped.RequiredKnowledges);
    }

    [Fact]
    public void ResourceDesc_KeepsItemTypeOnYield()
    {
        var row = new ResourceDesc
        {
            Id = 50,
            Name = "Rough Tree",
            Description = "",
            Tag = "Tree",
            Rarity = Rarity.Common,
            OnDestroyYield = [new SdkItemStack { ItemId = 1010001, Quantity = 1, ItemType = ItemType.Cargo }],
        };

        var mapped = new ResourceDescMapper().Map(row);

        Assert.Equal(BitcraftItemType.Cargo, Assert.Single(mapped.OnDestroyYield).ItemType);
        Assert.Equal(BitcraftItemRarity.Common, mapped.Rarity);
        Assert.Equal("Tree", mapped.Tag);
    }

    [Fact]
    public void BuildingDesc_KeepsFunctions()
    {
        var row = new BuildingDesc
        {
            Id = 30,
            Name = "Workbench",
            Description = "",
            Functions = [new BuildingFunction { FunctionType = 5, Level = 2, AllowedItemIdPerSlot = [] }],
        };

        var function = Assert.Single(new BuildingDescMapper().Map(row).Functions);

        Assert.Equal(5, function.FunctionType);
        Assert.Equal(2, function.Level);
    }

    [Fact]
    public void ActionLog_CargoDeposit_ConvertsToIsCargo()
    {
        var row = new ActionLogState
        {
            Id = 1,
            ObjectEntityId = 2,
            SubjectEntityId = 3,
            SubjectName = "player",
            Data = new ActionLogData.DepositItem(new SdkItemStack { ItemId = 3000, Quantity = 1, ItemType = ItemType.Cargo }),
        };

        var converged = ConvergeDbConverters.ToConverge(new ActionLogStateMapper().Map(row), "bitcraft-live-3");

        Assert.Equal(3000UL, converged.ActionItemId);
        Assert.True(converged.ActionIsCargo);
    }

    [Fact]
    public void PostgresTypes_AreAllInDbModel()
    {
        // DescriptorDbWriter.BuildDefinition throws at populate time for any [Postgres] type missing
        // from the model; building the model needs no live connection.
        var options = new DbContextOptionsBuilder<VelaDbContext>()
            .UseNpgsql("Host=localhost")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var db = new VelaDbContext(options, JsonOptions);

        foreach (var type in new[] { typeof(BitcraftSkillDesc), typeof(BitcraftToolTypeDesc), typeof(BitcraftBuildingTypeDesc), typeof(BitcraftToolDesc),
            typeof(BitcraftExtractionRecipe), typeof(BitcraftSecondaryKnowledgeDesc), typeof(BitcraftResourceDesc) })
        {
            Assert.Contains(type, BitcraftEventBase.PostgresTypes);
            Assert.NotNull(db.Model.FindEntityType(type));
        }
    }
}
