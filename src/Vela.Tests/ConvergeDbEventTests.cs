using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SpacetimeDB;
using SpacetimeDB.Types;
using Vela.Contracts.Entities;
using Vela.Entities;
using Vela.Events;
using Vela.Mappers;
using Vela.Services.Contracts;
using Vela.Services.Impl;
using Xunit;
using ContractReason = Vela.Contracts.Entities.BarterStallInventoryChangeReason;
using SpacetimeReason = SpacetimeDB.Types.BarterStallInventoryChangeReason;

namespace Vela.Tests;

// Mapping and conversion of the SpacetimeDB event tables that are emitted as ConvergeDB event kinds.
public class ConvergeDbEventTests
{
    [Fact]
    public void EventRecords_AreClassifiedAsEventOnly()
    {
        Assert.Contains(typeof(BitcraftMarketTradeEvent), BitcraftEventBase.ConvergeDbEventTypes);
        Assert.Contains(typeof(BitcraftBarterStallInventoryEvent), BitcraftEventBase.ConvergeDbEventTypes);
        Assert.DoesNotContain(typeof(BitcraftMarketTradeEvent), BitcraftEventBase.ConvergeDbTypes);
        Assert.DoesNotContain(typeof(BitcraftBarterStallInventoryEvent), BitcraftEventBase.ConvergeDbTypes);
    }

    [Fact]
    public void Subscriber_DispatcherCoverage_IsComplete()
    {
        // The constructor throws if any [ConvergeDb]/[ConvergeDbEvent] type is mis-wired - the same
        // check that would otherwise only fire at gateway startup.
        using var services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        _ = new EventSubscriberService(
            NullLogger<EventGatewayService>.Instance,
            Options.Create(new BitcraftServiceOptions { Uri = "", Module = "test-0", AuthToken = "" }),
            services.GetRequiredService<IMeterFactory>(),
            new StubMetricHelpers(),
            new ConvergeDbWriter(NullLogger<ConvergeDbWriter>.Instance, new StubFatalRestart(), null!),
            new StubDescriptorWriter());
    }

    [Fact]
    public void MarketTrade_MapsAndConverts()
    {
        var row = new MarketTradeEvent(
            ClaimEntityId: 1,
            ListingEntityId: 2,
            ListingType: MarketOrderType.BuyOrder,
            BuyerEntityId: 3,
            SellerEntityId: 4,
            ItemId: 5,
            ItemType: ItemType.Cargo,
            Quantity: 6,
            UnitPrice: 7,
            ListingRemainingQuantity: 8,
            Timestamp: new Timestamp(1_700_000_000_000_000));

        var converged = ConvergeDbConverters.ToConverge(new MarketTradeEventMapper().Map(row), "bitcraft-live-3");

        Assert.Equal("bitcraft-live-3", converged.Module);
        Assert.Equal(1UL, converged.ClaimId);
        Assert.Equal(2UL, converged.ListingId);
        Assert.True(converged.IsBuyOrder);
        Assert.Equal(3UL, converged.BuyerId);
        Assert.Equal(4UL, converged.SellerId);
        Assert.Equal(5, converged.ItemId);
        Assert.True(converged.IsCargo);
        Assert.Equal(6, converged.Quantity);
        Assert.Equal(7, converged.UnitPrice);
        Assert.Equal(8, converged.ListingRemainingQuantity);
        Assert.Equal(1_700_000_000_000_000, converged.TimestampMicros);
    }

    [Fact]
    public void BarterStall_WithTrade_MapsAndConverts()
    {
        var row = BarterRow(tradeOrderId: 40, tradeAmount: 2);

        var converged = ConvergeDbConverters.ToConverge(new BarterStallInventoryEventMapper().Map(row), "m");

        Assert.Equal(10UL, converged.ShopId);
        Assert.Equal(20UL, converged.ClaimId);
        Assert.Equal(30UL, converged.ActorId);
        Assert.Equal(ContractReason.Sale, converged.Reason);
        Assert.True(converged.HasTrade);
        Assert.Equal(40UL, converged.TradeOrderId);
        Assert.Equal(2, converged.TradeAmount);
        var added = Assert.Single(converged.AddedItems);
        Assert.Equal((100, 1, false), (added.ItemId, added.Quantity, added.IsCargo));
        var removed = Assert.Single(converged.RemovedItems);
        Assert.Equal((200, 3, true), (removed.ItemId, removed.Quantity, removed.IsCargo));
        Assert.Equal(50, converged.TreasuryCoinsAdded);
        Assert.Equal(60, converged.TreasuryCoinsRemoved);
        Assert.Equal(42, converged.TimestampMicros);
    }

    [Fact]
    public void BarterStall_WithoutTrade_ZeroesTradeFields()
    {
        var converged = ConvergeDbConverters.ToConverge(
            new BarterStallInventoryEventMapper().Map(BarterRow(tradeOrderId: null, tradeAmount: null)), "m");

        Assert.False(converged.HasTrade);
        Assert.Equal(0UL, converged.TradeOrderId);
        Assert.Equal(0, converged.TradeAmount);
    }

    [Fact]
    public void BarterStall_UnknownReason_FallsBackToUnknown()
    {
        var mapped = new BarterStallInventoryEventMapper().Map(BarterRow(tradeOrderId: null, tradeAmount: null))
            with { Reason = "SomethingNew" };

        Assert.Equal(ContractReason.Unknown, ConvergeDbConverters.ToConverge(mapped, "m").Reason);
    }

    [Fact]
    public void BarterStall_EveryReason_HasAContractValue()
    {
        // A new SpacetimeDB reason must be added to the contract enum, or it silently becomes Unknown.
        foreach (var name in Enum.GetNames<SpacetimeReason>())
            Assert.True(Enum.TryParse<ContractReason>(name, out _), $"Contract enum is missing reason '{name}'");
    }

    private static BarterStallInventoryEvent BarterRow(ulong? tradeOrderId, int? tradeAmount) => new(
        ShopEntityId: 10,
        ClaimEntityId: 20,
        ActorEntityId: 30,
        Reason: SpacetimeReason.Sale,
        TradeOrderEntityId: tradeOrderId,
        TradeAmount: tradeAmount,
        AddedItems: [new SpacetimeDB.Types.ItemStack(100, 1, ItemType.Item, null)],
        RemovedItems: [new SpacetimeDB.Types.ItemStack(200, 3, ItemType.Cargo, 9)],
        TreasuryCoinsAdded: 50,
        TreasuryCoinsRemoved: 60,
        Timestamp: new Timestamp(42));

    private sealed class StubMetricHelpers : IMetricHelpers
    {
        public string ServiceName => "test";
    }

    private sealed class StubFatalRestart : IFatalRestart
    {
        public void Trigger(string reason) { }
    }

    private sealed class StubDescriptorWriter : IDescriptorDbWriter
    {
        public Task PopulateAsync(Type entityType, IReadOnlyList<BitcraftEventBase> entities) => Task.CompletedTask;
        public void EnqueueUpsert<T>(T entity) where T : BitcraftEventBase { }
        public void EnqueueDelete<T>(T entity) where T : BitcraftEventBase { }
    }
}
