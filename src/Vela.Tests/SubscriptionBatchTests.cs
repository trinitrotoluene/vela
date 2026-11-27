using Xunit;

public class SubscriptionBatchTests
{
    [Fact]
    public void OwnerLocations_ReadInventoriesFirst()
    {
        // Small table first: reads inventory_state and probes location_state by primary key.
        // Written the other way round, the server would read all of location_state.
        Assert.StartsWith("SELECT l.* FROM inventory_state i JOIN location_state l ", EventGatewayService.OwnerLocationsQuery);
        Assert.Contains(EventGatewayService.OwnerLocationsQuery, EventGatewayService.BuildSubscriptionQueries(0));
    }

    [Fact]
    public void Queries_AreDistinct()
    {
        // Each is its own subscribe call, and a repeated query would be evaluated twice by the server.
        var queries = EventGatewayService.BuildSubscriptionQueries(0);
        Assert.Equal(queries.Length, queries.Distinct().Count());
    }
}
