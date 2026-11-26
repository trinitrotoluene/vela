using Xunit;

public class SubscriptionBatchTests
{
    [Fact]
    public void OwnerLocations_AreOffByDefault()
    {
        Assert.False(new BitcraftServiceOptions { Uri = "", Module = "test-0", AuthToken = "" }.SubscribeOwnerLocations);
        var batches = EventGatewayService.BuildSubscriptionBatches(0, subscribeOwnerLocations: false);
        Assert.DoesNotContain(batches, b => b.Contains(EventGatewayService.OwnerLocationsQuery));
    }

    [Fact]
    public void OwnerLocations_ShareTheInventoryBatch()
    {
        // Same batch as inventory_state, so the epoch covers both and retracts owners that are gone.
        // Small table first: reads inventory_state and probes location_state by primary key.
        // Written the other way round, the server would read all of location_state.
        Assert.StartsWith("SELECT l.* FROM inventory_state i JOIN location_state l ", EventGatewayService.OwnerLocationsQuery);
        var batches = EventGatewayService.BuildSubscriptionBatches(0, subscribeOwnerLocations: true);
        var withQuery = Assert.Single(batches, b => b.Contains(EventGatewayService.OwnerLocationsQuery));
        Assert.Contains("SELECT * FROM inventory_state", withQuery);
    }
}
