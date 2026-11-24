# Vela.Fake

Stands in for Vela's gateways so consumers (Nexus, Botrealis, Belp, BitAuth) can be smoke-tested
against a ConvergeDB without the game. It publishes made-up rows in Vela's contract
(`Vela.Contracts`), the way `ConvergeDbWriter` does:

- one connection per region, named `idc-N` with source id N;
- a snapshot between `EpochBegin` and `EpochEnd` on every connect, so rows left out of it are
  retracted by the server;
- live changes after that, flushed in frames;
- `MarketTradeEvent` and `BarterStallInventoryEvent` asserted against the region's shared event
  entity, and chat messages asserted then retracted later.

The rows hang together (inventories belong to players and buildings, buildings to claims, orders
to stalls) but carry no game meaning. Fidelity is low on purpose: the point is volume and
lifecycle (bootstrap, churn, reconnects), not realistic values.

## Running

```sh
# A ConvergeDB to publish to
convergence --in-memory

dotnet run --project src/Vela.Fake -- --regions 1,2 --rate 200 --restart-every 300
```

`--help`, or any bad option, prints every option. The main ones:

| Option | Default | |
|---|---|---|
| `--regions` | `1` | regions to publish, each its own source |
| `--scale` | `10000` | inventories and buildings per region; other kinds are sized from it |
| `--rate` | `200` | live changes per second per region |
| `--restart-every` | off | seconds between reconnects, each publishing a fresh snapshot |
| `--restart-drop` | `0.05` | share of inventories, orders, listings and crafts replaced by new rows in each new snapshot |

Production is far larger (millions of `LocationState` and `InventoryState` rows), but Vela's
filtered subscriptions keep what reaches ConvergeDB small today; 10,000 is about that. Raise
`--scale` to see how a consumer copes with more.

Every `--report-every` seconds (and on Ctrl+C) each region logs its row counts by kind. These
should match the server's, with one catch: the admin API's `entity_count`
(`/api/v1/kinds`) includes deleted rows until tombstone retention (300 s) expires them. To compare
right away, run the server with `CONVERGEDB_TOMBSTONE_RETENTION_SECS=2`.
