using Xunit;

public class SubscriptionBatchTests
{
    [Fact]
    public void OwnerLocations_ShareTheInventoryBatch()
    {
        // Same batch as inventory_state, so the epoch covers both and retracts owners that are gone.
        // Small table first: reads inventory_state and probes location_state by primary key.
        // Written the other way round, the server would read all of location_state.
        Assert.StartsWith("SELECT l.* FROM inventory_state i JOIN location_state l ", EventGatewayService.OwnerLocationsQuery);
        var batches = EventGatewayService.BuildSubscriptionBatches(0);
        var withQuery = Assert.Single(batches, b => b.Contains(EventGatewayService.OwnerLocationsQuery));
        Assert.Contains("SELECT * FROM inventory_state", withQuery);
    }

    [Fact]
    public void LocationJoins_ReadTheSmallTableFirst()
    {
        // Every location_state join starts from the small table and probes location_state by its
        // primary key. FROM location_state would read the whole table on every apply.
        var queries = EventGatewayService.BuildSubscriptionBatches(0).SelectMany(b => b)
            .Where(q => q.Contains("location_state")).ToList();
        Assert.Equal(4, queries.Count);
        Assert.All(queries, q => Assert.DoesNotContain("FROM location_state", q));
    }
}
