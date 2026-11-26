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
        var batches = EventGatewayService.BuildSubscriptionBatches(0, subscribeOwnerLocations: true);
        var withQuery = Assert.Single(batches, b => b.Contains(EventGatewayService.OwnerLocationsQuery));
        Assert.Contains("SELECT * FROM inventory_state", withQuery);
    }
}
