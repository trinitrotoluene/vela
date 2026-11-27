using System.Diagnostics;
using Convergence.Client;
using Vela.Contracts.Entities;

namespace Vela.Fake;

/// <summary>
/// One region's gateway: a connection named <c>idc-N</c> with source id N, a made-up world whose
/// references resolve (claims own buildings, buildings have locations, stalls have orders), and a
/// stream of changes to it.
/// </summary>
public sealed class Region
{
    private readonly int _n;
    private readonly FakeOptions _o;
    private readonly string _module;
    private readonly Random _rng;
    private readonly ReadOnlyMemory<byte> _eventEntityId;
    private ulong _nextId = 1;

    // Every kind Vela registers, in its order. Kinds left empty are registered all the same.
    private readonly Table<ConvergeBuildingState> _buildings = new();
    private readonly Table<ConvergeBuildingNicknameState> _nicknames = new();
    private readonly Table<ConvergeClaimState> _claims = new();
    private readonly Table<ConvergeClaimLocalState> _claimLocals = new();
    private readonly Table<ConvergeClaimTechState> _claimTechs = new();
    private readonly Table<ConvergeEmpireState> _empires = new();
    private readonly Table<ConvergeEmpireNodeState> _empireNodes = new();
    private readonly Table<ConvergeEmpireNodeSiegeState> _sieges = new();
    private readonly Table<ConvergeUserState> _users = new();
    private readonly Table<ConvergeUsernameState> _usernames = new();
    private readonly Table<ConvergeLocationState> _locations = new();
    private readonly Table<ConvergeProgressiveAction> _crafts = new();
    private readonly Table<ConvergePublicProgressiveAction> _publicCrafts = new();
    private readonly Table<ConvergeChatMessage> _chat = new();
    private readonly Table<ConvergeActionLogState> _actionLog = new();
    private readonly Table<ConvergeAuctionListingState> _auctions = new();
    private readonly Table<ConvergeClosedListingState> _closedListings = new();
    private readonly Table<ConvergeInventoryState> _inventories = new();
    private readonly Table<ConvergeBarterStallState> _stalls = new();
    private readonly Table<ConvergeTradeOrderState> _orders = new();
    private readonly Table<ConvergeDimensionDescriptionState> _dimensions = new();
    private readonly Table<ConvergeDimensionNetworkState> _networks = new();
    private readonly Table<ConvergePlayerHousingState> _housing = new();
    private readonly Table<ConvergeMarketTradeEvent> _marketTrades = new();
    private readonly Table<ConvergeBarterStallInventoryEvent> _stallEvents = new();
    private readonly Table<ConvergeEquipmentState> _equipment = new();
    private readonly Table<ConvergeEquipmentPresetState> _presets = new();
    private readonly Table<ConvergeDeployableState> _deployables = new();
    private readonly Table<ConvergeClaimMemberState> _claimMembers = new();
    private readonly ITable[] _tables;

    // Players, claims and buildings are never removed, so other rows can point at them.
    private readonly List<ulong> _players = new();
    private readonly List<ulong> _claimIds = new();
    private readonly List<ulong> _buildingIds = new();
    private readonly List<ulong> _stallIds = new();

    private long _changes;
    // Sizes the churning tables hover around, so long runs don't drift.
    private int _inventoryTarget, _orderTarget, _auctionTarget;

    public Region(int n, FakeOptions options)
    {
        _n = n;
        _o = options;
        _module = $"idc-{n}";
        _rng = new Random(options.Seed * 1000 + n);
        _eventEntityId = EntityId.FromULong((ulong)n);
        _tables =
        [
            _buildings, _nicknames, _claims, _claimLocals, _claimTechs, _empires, _empireNodes, _sieges,
            _users, _usernames, _locations, _crafts, _publicCrafts, _chat, _actionLog, _auctions,
            _closedListings, _inventories, _stalls, _orders, _dimensions, _networks, _housing,
            _marketTrades, _stallEvents, _equipment, _presets, _deployables, _claimMembers,
        ];
    }

    public async Task RunAsync(CancellationToken ct)
    {
        Generate();
        _inventoryTarget = _inventories.Count;
        _orderTarget = _orders.Count;
        _auctionTarget = _auctions.Count;
        Log($"generated {Summary()}");
        bool restarted = false;
        while (!ct.IsCancellationRequested)
        {
            if (restarted) DropSome();
            restarted = false;
            try
            {
                await using var client = await ConvergenceClient.ConnectAsync(new ConvergenceOptions
                {
                    Host = _o.Host,
                    Port = _o.Port,
                    Name = _module,
                    SourceId = (byte)_n,
                }, ct);
                foreach (var table in _tables) await table.RegisterAsync(client, ct);
                var writer = new Writer(client, ct);
                await SnapshotAsync(writer, ct);
                await ChurnAsync(writer, ct);
                restarted = true;
            }
            catch (Exception ex)
            {
                if (ct.IsCancellationRequested) break;
                Log($"connection failed, reconnecting in 2 s: {ex.Message}");
                try { await Task.Delay(2000, ct); } catch (OperationCanceledException) { }
            }
        }
        Log($"stopped: {Summary()}");
    }

    private async Task SnapshotAsync(Writer writer, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        await writer.Client.EpochBeginAsync(ct);
        foreach (var table in _tables) await table.AssertAllAsync(writer);
        await writer.FlushAsync();
        await writer.Client.EpochEndAsync(ct);
        Log($"snapshot published in {sw.ElapsedMilliseconds} ms: {Summary()}");
    }

    private async Task ChurnAsync(Writer writer, CancellationToken ct)
    {
        var started = Stopwatch.StartNew();
        var restartAt = _o.RestartEverySeconds > 0 ? TimeSpan.FromSeconds(_o.RestartEverySeconds) : TimeSpan.MaxValue;
        var reportEvery = TimeSpan.FromSeconds(_o.ReportEverySeconds);
        var nextReport = reportEvery;
        double owed = 0;
        var last = started.Elapsed;
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        while (await timer.WaitForNextTickAsync(ct))
        {
            var now = started.Elapsed;
            owed += (now - last).TotalSeconds * _o.Rate;
            last = now;
            for (; owed >= 1; owed--)
            {
                ct.ThrowIfCancellationRequested();
                await ChangeAsync(writer);
            }
            await writer.FlushAsync();

            if (now >= nextReport)
            {
                Log($"{_changes} changes so far: {Summary()}");
                nextReport += reportEvery;
            }
            if (now >= restartAt)
            {
                Log("restarting, as Vela does after a crash or deploy");
                return;
            }
        }
    }

    // ---- The world ----

    private ulong NewId() => ((ulong)_n << 48) | _nextId++;
    private T Pick<T>(List<T> list) => list[_rng.Next(list.Count)];
    private int Coord() => _rng.Next(1_000, 38_000);

    private void Generate()
    {
        int s = _o.Scale;

        for (int i = 0; i < s / 10; i++)
        {
            ulong p = NewId();
            _players.Add(p);
            var identity = new byte[32];
            BitConverter.TryWriteBytes(identity, p);
            _users.Set(p, new ConvergeUserState { EntityId = identity, Module = _module, UserEntityId = p, CanSignIn = true });
            _usernames.Set(p, new ConvergeUsernameState { EntityId = EntityId.FromULong(p), Module = _module, Username = $"player{_n}_{i}" });
            _locations.Set(p, Location(p, Coord(), Coord()));
            _equipment.Set(p, new ConvergeEquipmentState { EntityId = EntityId.FromULong(p), Module = _module, Slots = EquipmentSlots() });
            // Up to three presets, the first active; each holds its own gear.
            for (int k = 0, presets = _rng.Next(4); k < presets; k++)
            {
                ulong preset = NewId();
                _presets.Set(preset, new ConvergeEquipmentPresetState
                {
                    EntityId = EntityId.FromULong(preset), Module = _module, PlayerEntityId = p, Index = k + 1,
                    Active = k == 0, Slots = EquipmentSlots(),
                });
            }
        }

        int claims = Math.Max(1, s / 100);
        for (int i = 0; i < claims; i++) _claimIds.Add(NewId());

        for (int i = 0; i < s; i++)
        {
            ulong b = NewId();
            _buildingIds.Add(b);
            // The first building of each claim is its town hall.
            ulong claim = i < claims ? _claimIds[i] : Pick(_claimIds);
            _buildings.Set(b, new ConvergeBuildingState
            {
                EntityId = EntityId.FromULong(b), Module = _module, ClaimEntityId = claim,
                BuildingDescriptionId = i < claims ? 405 : _rng.Next(1, 400),
            });
            _locations.Set(b, Location(b, Coord(), Coord()));
        }

        for (int i = 0; i < claims; i++)
        {
            ulong c = _claimIds[i];
            ulong townHall = _buildingIds[i];
            var at = _locations[townHall];
            ulong owner = Pick(_players);
            _claims.Set(c, new ConvergeClaimState
            {
                EntityId = EntityId.FromULong(c), Module = _module, OwnerPlayerId = owner,
                OwnerBuildingId = townHall, Name = $"Claim {_n}-{i}", IsNeutral = false,
            });
            // The owner is the claim's one member. A membership id of its own, outside NewId's
            // range, so the ids handed out after it don't move.
            ulong membership = c | (1UL << 47);
            _claimMembers.Set(membership, new ConvergeClaimMemberState
            {
                EntityId = EntityId.FromULong(membership), Module = _module, ClaimEntityId = c,
                PlayerEntityId = owner,
            });
            _claimLocals.Set(c, new ConvergeClaimLocalState
            {
                EntityId = EntityId.FromULong(c), Module = _module, Supplies = _rng.Next(10_000),
                Treasury = (uint)_rng.Next(100_000), BuildingDescriptionId = 405, HasLocation = true,
                Location = new ConvergeLocation { X = at.X, Z = at.Z, Dimension = at.Dimension },
            });
            _claimTechs.Set(c, new ConvergeClaimTechState
            {
                EntityId = EntityId.FromULong(c), Module = _module,
                Learned = Enumerable.Range(1, _rng.Next(1, 40)).ToArray(), Researching = _rng.Next(40, 60),
            });
        }

        for (int i = 0; i < s; i++) NewInventory(out _);

        // Deployables belong to players and own an inventory of their own.
        for (int i = 0; i < s / 25; i++)
        {
            ulong d = NewId();
            _deployables.Set(d, new ConvergeDeployableState
            {
                EntityId = EntityId.FromULong(d), Module = _module, OwnerId = Pick(_players),
                ClaimEntityId = _rng.Next(4) == 0 ? Pick(_claimIds) : 0, Hidden = _rng.Next(3) == 0,
                DeployableDescriptionId = _rng.Next(1, 20),
            });
            ulong inv = NewId();
            var pockets = Pockets();
            _inventories.Set(inv, new ConvergeInventoryState
            {
                EntityId = EntityId.FromULong(inv), Module = _module, Pockets = pockets,
                OwnerEntityId = d, PlayerOwnerEntityId = 0, InventoryIndex = 0, CargoIndex = pockets.Length - 1,
            });
        }

        for (int i = 0; i < s / 50; i++)
        {
            ulong b = Pick(_buildingIds);
            if (_stalls.Contains(b)) continue;
            _stallIds.Add(b);
            _stalls.Set(b, new ConvergeBarterStallState { EntityId = EntityId.FromULong(b), Module = _module });
            if (i % 2 == 0)
                _nicknames.Set(b, new ConvergeBuildingNicknameState { EntityId = EntityId.FromULong(b), Module = _module, Nickname = $"Stall {_n}-{i}" });
            for (int k = _rng.Next(1, 6); k > 0; k--) NewOrder(out _);
        }

        for (int i = 0; i < s / 20; i++) NewAuction(out _);
        for (int i = 0; i < s / 50; i++) NewClosedListing(out _);
        for (int i = 0; i < s / 20; i++) NewCraft(out _, out _);

        for (int i = 0; i < Math.Max(1, s / 200); i++)
        {
            ulong net = NewId();
            ulong building = Pick(_buildingIds);
            _networks.Set(net, new ConvergeDimensionNetworkState
            {
                EntityId = EntityId.FromULong(net), Module = _module, BuildingId = building,
                ClaimEntityId = _buildings[building].ClaimEntityId,
            });
            ulong dim = NewId();
            _dimensions.Set(dim, new ConvergeDimensionDescriptionState
            {
                EntityId = EntityId.FromULong(dim), Module = _module, DimensionNetworkId = net, DimensionId = (uint)(1000 + i),
            });
            ulong owner = Pick(_players);
            _housing.Set(owner, new ConvergePlayerHousingState
            {
                EntityId = EntityId.FromULong(owner), Module = _module, EntranceBuildingId = building, NetworkId = net,
            });
        }

        for (int i = 0; i < 5; i++)
        {
            ulong e = NewId();
            _empires.Set(e, new ConvergeEmpireState { EntityId = EntityId.FromULong(e), Module = _module, Name = $"Empire {_n}-{i}", ShardTreasury = _rng.Next(50_000) });
            for (int k = 0; k < 4; k++)
            {
                ulong node = NewId();
                _empireNodes.Set(node, new ConvergeEmpireNodeState
                {
                    EntityId = EntityId.FromULong(node), Module = _module, EmpireId = e, IsActive = true,
                    ChunkIndex = (ulong)_rng.Next(100_000), Dimension = 1, LocationX = Coord(), LocationZ = Coord(),
                    Upkeep = _rng.Next(100), Energy = _rng.Next(10_000),
                });
            }
        }
    }

    private ConvergeLocationState Location(ulong id, int x, int z) =>
        new() { EntityId = EntityId.FromULong(id), Module = _module, X = x, Z = z, Dimension = 1 };

    private ConvergeInventoryPocket[] Pockets()
    {
        var pockets = new ConvergeInventoryPocket[_rng.Next(8, 33)];
        for (int i = 0; i < pockets.Length; i++)
        {
            // Empty pockets are common in game.
            if (_rng.Next(3) == 0) continue;
            pockets[i] = new ConvergeInventoryPocket { ItemId = (ulong)_rng.Next(1, 3_000), Quantity = _rng.Next(1, 500), ItemType = (byte)_rng.Next(2) };
        }
        return pockets;
    }

    // Equipment slots hold one item each, and are often empty.
    private ConvergeInventoryPocket[] EquipmentSlots()
    {
        var slots = new ConvergeInventoryPocket[12];
        for (int i = 0; i < slots.Length; i++)
        {
            if (_rng.Next(2) == 0) continue;
            slots[i] = new ConvergeInventoryPocket { ItemId = (ulong)_rng.Next(1, 3_000), Quantity = 1, ItemType = 1 };
        }
        return slots;
    }

    private ConvergeInventoryState NewInventory(out ulong id)
    {
        id = NewId();
        // Building storage, or a player's own inventory.
        bool player = _rng.Next(5) < 2;
        ulong owner = player ? Pick(_players) : Pick(_buildingIds);
        var pockets = Pockets();
        var inv = new ConvergeInventoryState
        {
            EntityId = EntityId.FromULong(id), Module = _module, Pockets = pockets,
            OwnerEntityId = owner, PlayerOwnerEntityId = player ? owner : 0, InventoryIndex = _rng.Next(4),
            CargoIndex = pockets.Length - 1,
        };
        _inventories.Set(id, inv);
        return inv;
    }

    private ConvergeItemStack[] Stacks() =>
        Enumerable.Range(0, _rng.Next(1, 4))
            .Select(_ => new ConvergeItemStack { ItemId = _rng.Next(1, 3_000), Quantity = _rng.Next(1, 100), IsCargo = _rng.Next(10) == 0 })
            .ToArray();

    private ConvergeTradeOrderState NewOrder(out ulong id)
    {
        id = NewId();
        var order = new ConvergeTradeOrderState
        {
            EntityId = EntityId.FromULong(id), Module = _module, ShopId = Pick(_stallIds),
            RemainingStock = _rng.Next(1, 50), OfferItems = Stacks(), RequiredItems = Stacks(),
        };
        _orders.Set(id, order);
        return order;
    }

    private ConvergeAuctionListingState NewAuction(out ulong id)
    {
        id = NewId();
        var listing = new ConvergeAuctionListingState
        {
            EntityId = EntityId.FromULong(id), Module = _module, OwnerId = Pick(_players), ClaimId = Pick(_claimIds),
            Price = _rng.Next(1, 1_000), Quantity = _rng.Next(1, 200), StoredCoins = 0,
            ItemId = _rng.Next(1, 3_000), IsCargoItem = _rng.Next(10) == 0,
        };
        _auctions.Set(id, listing);
        return listing;
    }

    private ConvergeClosedListingState NewClosedListing(out ulong id)
    {
        id = NewId();
        var closed = new ConvergeClosedListingState
        {
            EntityId = EntityId.FromULong(id), Module = _module, OwnerId = Pick(_players), ClaimId = Pick(_claimIds),
            ItemStack = new ConvergeItemStack { ItemId = _rng.Next(1, 3_000), Quantity = _rng.Next(1, 100) },
            Timestamp = DateTimeOffset.UtcNow.ToString("O"),
        };
        _closedListings.Set(id, closed);
        return closed;
    }

    private void NewCraft(out ConvergeProgressiveAction craft, out ConvergePublicProgressiveAction shared)
    {
        ulong id = NewId();
        craft = new ConvergeProgressiveAction
        {
            EntityId = EntityId.FromULong(id), Module = _module, RecipeId = _rng.Next(1, 2_000),
            CraftCount = _rng.Next(1, 20), Progress = 0,
        };
        shared = new ConvergePublicProgressiveAction
        {
            EntityId = EntityId.FromULong(id), Module = _module, BuildingEntityId = Pick(_buildingIds), OwnerEntityId = Pick(_players),
        };
        _crafts.Set(id, craft);
        _publicCrafts.Set(id, shared);
    }

    // While a gateway is down, some rows go and others appear: the next snapshot leaves the first
    // out, so its epoch retracts them, and adds as many new ones.
    private void DropSome()
    {
        int dropped = 0;
        foreach (var table in new ITable[] { _inventories, _orders, _auctions, _crafts })
        {
            int n = (int)(table.Count * _o.RestartDrop);
            for (int i = 0; i < n; i++)
            {
                bool ok = table switch
                {
                    Table<ConvergeInventoryState> t => t.TryPick(_rng, out var k) && t.Drop(k, out _),
                    Table<ConvergeTradeOrderState> t => t.TryPick(_rng, out var k) && t.Drop(k, out _),
                    Table<ConvergeAuctionListingState> t => t.TryPick(_rng, out var k) && t.Drop(k, out _),
                    Table<ConvergeProgressiveAction> t => t.TryPick(_rng, out var k) && t.Drop(k, out _) && _publicCrafts.Drop(k, out _),
                    _ => false,
                };
                if (!ok) continue;
                dropped++;
                switch (table)
                {
                    case Table<ConvergeInventoryState>: NewInventory(out _); break;
                    case Table<ConvergeTradeOrderState>: NewOrder(out _); break;
                    case Table<ConvergeAuctionListingState>: NewAuction(out _); break;
                    default: NewCraft(out _, out _); break;
                }
            }
        }
        Log($"replaced {dropped} rows in the next snapshot");
    }

    // ---- Live changes ----

    private async ValueTask ChangeAsync(Writer w)
    {
        _changes++;
        int roll = _rng.Next(100);
        ulong key;
        switch (roll)
        {
            case < 40 when _inventories.TryPick(_rng, out key):
                await _inventories.PutAsync(w, key, _inventories[key] with { Pockets = Pockets() });
                break;
            case < 52 when _inventories.Count >= _inventoryTarget && _inventories.TryPick(_rng, out key):
                await _inventories.RemoveAsync(w, key);
                break;
            case < 52:
            {
                var inv = NewInventory(out key);
                await _inventories.PutAsync(w, key, inv);
                break;
            }
            case < 62:
            {
                // Players move; buildings stay put, so claims keep their town hall's location.
                key = Pick(_players);
                var at = _locations[key];
                await _locations.PutAsync(w, key, at with { X = at.X + _rng.Next(-3, 4), Z = at.Z + _rng.Next(-3, 4) });
                break;
            }
            case < 70 when _crafts.TryPick(_rng, out key):
            {
                var craft = _crafts[key];
                int progress = craft.Progress + _rng.Next(10, 60);
                if (progress >= craft.CraftCount * 100)
                {
                    // Drop both before publishing, so a failed write can't leave one without the other.
                    _crafts.Drop(key, out var done);
                    _publicCrafts.Drop(key, out var doneShared);
                    await _crafts.RetractAsync(w, done);
                    await _publicCrafts.RetractAsync(w, doneShared);
                    NewCraft(out var next, out var shared);
                    var id = EntityId.ToULong(next.EntityId);
                    await _crafts.PutAsync(w, id, next);
                    await _publicCrafts.PutAsync(w, id, shared);
                }
                else
                {
                    await _crafts.PutAsync(w, key, craft with { Progress = progress });
                }
                break;
            }
            case < 75:
                await ChangeOrderAsync(w);
                break;
            case < 80:
                await ChangeAuctionAsync(w);
                break;
            case < 84:
            {
                ulong c = Pick(_claimIds);
                var local = _claimLocals[c];
                await _claimLocals.PutAsync(w, c, local with { Supplies = Math.Max(0, local.Supplies + _rng.Next(-50, 51)) });
                break;
            }
            case < 87:
                await ChatAsync(w);
                break;
            case < 88:
            {
                // Players change gear now and then.
                key = Pick(_players);
                await _equipment.PutAsync(w, key, _equipment[key] with { Slots = EquipmentSlots() });
                break;
            }
            case < 92:
                await _marketTrades.EmitAsync(w, new ConvergeMarketTradeEvent
                {
                    Module = _module, ClaimId = Pick(_claimIds), ListingId = NewId(), IsBuyOrder = _rng.Next(2) == 0,
                    BuyerId = Pick(_players), SellerId = Pick(_players), ItemId = _rng.Next(1, 3_000), IsCargo = false,
                    Quantity = _rng.Next(1, 50), UnitPrice = _rng.Next(1, 500), ListingRemainingQuantity = _rng.Next(0, 50),
                    TimestampMicros = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000,
                }.WithEntityId(_eventEntityId));
                break;
            case < 96 when _stallIds.Count > 0:
            {
                ulong shop = Pick(_stallIds);
                await _stallEvents.EmitAsync(w, new ConvergeBarterStallInventoryEvent
                {
                    Module = _module, ShopId = shop, ClaimId = _buildings[shop].ClaimEntityId, ActorId = Pick(_players),
                    ReasonCode = (byte)_rng.Next(4), HasTrade = false, AddedItems = Stacks(), RemovedItems = [],
                    TimestampMicros = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000,
                }.WithEntityId(_eventEntityId));
                break;
            }
            default:
            {
                var closed = NewClosedListing(out key);
                await _closedListings.PutAsync(w, key, closed);
                if (_closedListings.Count > _o.Scale / 50 && _closedListings.TryPick(_rng, out var old))
                    await _closedListings.RemoveAsync(w, old);
                break;
            }
        }
    }

    private async ValueTask ChangeOrderAsync(Writer w)
    {
        bool trade = _rng.Next(3) == 0;
        if (!trade && _orders.Count < _orderTarget && _stallIds.Count > 0)
        {
            var order = NewOrder(out var id);
            await _orders.PutAsync(w, id, order);
        }
        else if (_orders.TryPick(_rng, out var key))
        {
            var order = _orders[key];
            if (trade && order.RemainingStock > 1)
                await _orders.PutAsync(w, key, order with { RemainingStock = order.RemainingStock - 1 });
            else
                await _orders.RemoveAsync(w, key);
        }
    }

    private async ValueTask ChangeAuctionAsync(Writer w)
    {
        bool trade = _rng.Next(3) == 0;
        if (!trade && _auctions.Count < _auctionTarget)
        {
            var listing = NewAuction(out var id);
            await _auctions.PutAsync(w, id, listing);
        }
        else if (_auctions.TryPick(_rng, out var key))
        {
            var listing = _auctions[key];
            if (trade && listing.Quantity > 1)
                await _auctions.PutAsync(w, key, listing with { Quantity = listing.Quantity - 1, StoredCoins = listing.StoredCoins + listing.Price });
            else
                await _auctions.RemoveAsync(w, key);
        }
    }

    // Vela keeps chat messages as rows for a while, then retracts them.
    private async ValueTask ChatAsync(Writer w)
    {
        ulong id = NewId();
        ulong sender = Pick(_players);
        await _chat.PutAsync(w, id, new ConvergeChatMessage
        {
            EntityId = EntityId.FromULong(id), Module = _module, ChannelId = _rng.Next(1, 5), SenderId = sender,
            SenderUsername = _usernames[sender].Username, Content = $"message {id & 0xFFFFFFFF}",
        });
        if (_chat.Count > 200 && _chat.TryPick(_rng, out var old)) await _chat.RemoveAsync(w, old);
    }

    private string Summary() =>
        string.Join(' ', _tables.Where(t => t.Count > 0).Select(t => $"{t.Name}={t.Count}"));

    private void Log(string message) => Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss} [{_module}] {message}");
}
