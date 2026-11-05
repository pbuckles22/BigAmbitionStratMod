using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class StoreTests
{
    private const string Hair = "ba:itemname_haircareproduct";
    private const string Soda = "ba:itemname_sodacan";
    private const string Gift = "ba:itemname_cheapgift";
    private const string Flower = "ba:itemname_cheapflower";
    private const string Salon = "ba:street_fourthavenue 28";
    private const string Planned = "ba:street_side 9";
    private const string Fifth = "ba:street_fifthavenue 36";
    private const string Wholesaler = "ba:street_firststreet 37";

    [Fact]
    public void Stores_an_unplanned_store_is_connected_and_a_planned_store_is_left()
    {
        var api = GameApi.Live();
        var stops = new[] { Plan("W1", Planned, Soda) };
        var holds = new[]
        {
            Hold("W11", "ba:street_twelfthstreet", 1, Hair, 40),
            Hold("W1", "ba:street_fourthstreet", 46, Soda, 45),
        };
        var needs = new[]
        {
            new GoodsNeed(Salon, Hair),
            new GoodsNeed(Planned, Soda),
        };

        var links = StoreConnect.Connect(api, stops, holds, needs, Array.Empty<DailyUnits>());

        var connected = Assert.Single(links);
        Assert.Equal(Salon, connected.Store);
        Assert.Equal("W11", connected.Warehouse);
        Assert.Equal(Hair, connected.Item);
        Assert.Equal("UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination", connected.Add);
        Assert.Equal("", connected.Drop);
        var routes = StoreConnect.Apply(stops, links);
        Assert.Contains(routes, stop => stop.Destination == Salon && stop.Warehouse == "W11" && stop.Item == Hair && stop.Day == null);
        Assert.Contains(routes, stop => stop.Destination == Planned && stop.Warehouse == "W1" && stop.Item == Soda && stop.Day == null);
        Assert.Equal(2, routes.Count);
    }

    [Fact]
    public void Stores_a_held_contract_is_dropped_and_an_unheld_one_stays()
    {
        var api = GameApi.Live();
        var stops = new[]
        {
            Contract(Wholesaler, Fifth, Gift),
            Contract(Wholesaler, Fifth, Flower),
        };
        var holds = new[] { Hold("W2", "ba:street_gift", 2, Gift, 8) };
        var needs = new[]
        {
            new GoodsNeed(Fifth, Gift),
            new GoodsNeed(Fifth, Flower),
        };
        var sales = new[] { new DailyUnits(Fifth, Gift, 1014, 4) };

        var links = StoreConnect.Connect(api, stops, holds, needs, sales);

        var dropped = Assert.Single(links);
        Assert.Equal(Fifth, dropped.Store);
        Assert.Equal("W2", dropped.Warehouse);
        Assert.Equal(Gift, dropped.Item);
        Assert.Equal("Entities.DeliveryContract.Remove", dropped.Drop);
        Assert.Equal("UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination", dropped.Add);
        Assert.DoesNotContain(links, link => link.Item == Flower);
        var routes = StoreConnect.Apply(stops, links);
        Assert.DoesNotContain(routes, stop => stop.Item == Gift && stop.Day != null);
        Assert.Contains(routes, stop => stop.Destination == Fifth && stop.Warehouse == "W2" && stop.Item == Gift && stop.Day == null);
        var kept = Assert.Single(routes, stop => stop.Item == Flower);
        Assert.Equal(Wholesaler, kept.Warehouse);
        Assert.Equal(Fifth, kept.Destination);
        Assert.Equal(1016, kept.Day);
    }

    [Fact]
    public void Stores_a_contract_stays_when_the_hub_holds_less_than_one_day()
    {
        var api = GameApi.Live();
        var stops = new[] { Contract(Wholesaler, Fifth, Gift) };
        var holds = new[] { Hold("W2", "ba:street_gift", 2, Gift, 3) };
        var needs = new[] { new GoodsNeed(Fifth, Gift) };
        var sales = new[] { new DailyUnits(Fifth, Gift, 1014, 4) };

        var links = StoreConnect.Connect(api, stops, holds, needs, sales);

        Assert.Empty(links);
        var routes = StoreConnect.Apply(stops, links);
        var kept = Assert.Single(routes);
        Assert.Equal(Wholesaler, kept.Warehouse);
        Assert.Equal(Fifth, kept.Destination);
        Assert.Equal(Gift, kept.Item);
        Assert.Equal(1016, kept.Day);
        Assert.Equal(new[] { Fifth }, Supply.List(routes, holds, needs, sales).Wholesaler);
    }

    [Fact]
    public void Stores_two_stores_join_the_single_family_hub_and_a_mixed_warehouse_is_left()
    {
        var api = GameApi.Live();
        var other = "ba:street_sixthavenue 12";
        var holds = new[]
        {
            Hold("W6", "ba:street_fifthavenue", 43, Hair, 40),
            Hold("W6", "ba:street_fifthavenue", 43, Soda, 8),
            Hold("W11", "ba:street_twelfthstreet", 1, Hair, 15),
        };
        var needs = new[]
        {
            new GoodsNeed(Salon, Hair),
            new GoodsNeed(other, Hair),
        };

        var links = StoreConnect.Connect(api, Array.Empty<DeliveryStop>(), holds, needs, Array.Empty<DailyUnits>());

        Assert.Equal(2, links.Count);
        Assert.All(links, link =>
        {
            Assert.Equal("W11", link.Warehouse);
            Assert.Equal(Hair, link.Item);
            Assert.Equal("UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination", link.Add);
            Assert.Equal("", link.Drop);
        });
        Assert.DoesNotContain(links, link => link.Warehouse == "W6");
    }

    [Fact]
    public void Stores_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_locks_add_and_remove()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect
        using var dir = new TempStores();
        var save = new object();
        var world = Sample();
        var ran = Run(dir.Path, save, world);
        var written = new ConnectWatch().Tick(
            dir.Path,
            save,
            1016,
            ran.Api,
            ran.Stops,
            ran.Holds,
            world.Needs,
            world.Sales);

        Assert.Equal(Needs.Full, ran.Refill.Energy);
        Assert.Contains("day=1016", ran.Rolled);
        Assert.Equal(
            "UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination",
            ran.Api.Call("delivery"));
        Assert.Equal("Entities.DeliveryContract.Remove", ran.Api.Call("wholesaler contract drop"));
        var connected = Assert.Single(written, line => line.Contains(" store=" + Salon + " "));
        Assert.Equal(
            "store.connected version=" + LoadLog.Version
                + " day=1016 store=" + Salon
                + " warehouse=W11 item=" + Hair
                + " add=" + ran.Api.Call("delivery")
                + " drop=",
            connected);
        var dropped = Assert.Single(written, line => line.Contains(" item=" + Gift + " "));
        Assert.Equal(
            "store.connected version=" + LoadLog.Version
                + " day=1016 store=" + Fifth
                + " warehouse=W2 item=" + Gift
                + " add=" + ran.Api.Call("delivery")
                + " drop=" + ran.Api.Call("wholesaler contract drop"),
            dropped);
        Assert.DoesNotContain(written, line => line.Contains(" store=" + Planned + " "));
        Assert.DoesNotContain(written, line => line.Contains(" item=" + Flower + " "));
        var log = File.ReadAllLines(LoadLog.CeoLogPath(dir.Path));
        Assert.Equal(ran.Loaded, log[0]);
        var slotAt = Array.IndexOf(log, ran.SlotLine);
        var connectAt = Array.IndexOf(log, dropped);
        Assert.True(slotAt >= 0 && connectAt > slotAt);
    }

    [Fact]
    public void Stores_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_writes_the_stop_and_keeps_the_unheld_contract()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → routes
        using var dir = new TempStores();
        var save = new object();
        var world = Sample();
        var ran = Run(dir.Path, save, world);
        var warehouses = File.ReadAllText(RouteDump.WarehousesPath(dir.Path));
        var watch = new ConnectWatch();
        var written = watch.Tick(dir.Path, save, 1016, ran.Api, ran.Stops, ran.Holds, world.Needs, world.Sales);
        RouteDump.WriteRoutes(dir.Path, world.Stops);
        var again = watch.Tick(dir.Path, save, 1017, ran.Api, ran.Stops, ran.Holds, world.Needs, world.Sales);
        var routes = Supply.Stops(RouteDump.RoutesPath(dir.Path));
        var list = Supply.List(routes, ran.Holds, world.Needs, world.Sales);

        Assert.NotEmpty(written);
        Assert.Empty(again);
        Assert.Equal(warehouses, File.ReadAllText(RouteDump.WarehousesPath(dir.Path)));
        Assert.Contains(routes, stop => stop.Destination == Salon && stop.Warehouse == "W11" && stop.Item == Hair && stop.Day == null);
        Assert.Contains(routes, stop => stop.Destination == Planned && stop.Warehouse == "W1" && stop.Item == Soda && stop.Day == null);
        Assert.Contains(routes, stop => stop.Destination == Fifth && stop.Warehouse == "W2" && stop.Item == Gift && stop.Day == null);
        Assert.DoesNotContain(routes, stop => stop.Item == Gift && stop.Day != null);
        var flower = Assert.Single(routes, stop => stop.Item == Flower);
        Assert.Equal(Wholesaler, flower.Warehouse);
        Assert.Equal(1016, flower.Day);
        Assert.DoesNotContain(list.Unsupplied, gap => gap.Store == Salon && gap.Reason == "noplan");
        Assert.Equal(new[] { Fifth }, list.Wholesaler);
        Assert.Equal("short", Assert.Single(list.Unsupplied, gap => gap.Item == Gift).Reason);
        var log = File.ReadAllLines(LoadLog.CeoLogPath(dir.Path));
        Assert.Single(log, line => line.StartsWith("store.connected ") && line.Contains(" item=" + Gift + " "));
    }

    private static DeliveryStop Plan(string warehouse, string destination, string item)
    {
        return new DeliveryStop(warehouse, destination, item, null, "");
    }

    private static DeliveryStop Contract(string warehouse, string destination, string item)
    {
        return new DeliveryStop(warehouse, destination, item, 1016, "");
    }

    private static WarehouseHold Hold(string warehouse, string street, int number, string item, int amount)
    {
        return new WarehouseHold(warehouse, street, number, item, amount);
    }

    private static World Sample()
    {
        return new World(
            new[]
            {
                Plan("W1", Planned, Soda),
                Contract(Wholesaler, Fifth, Gift),
                Contract(Wholesaler, Fifth, Flower),
            },
            new[]
            {
                Hold("W11", "ba:street_twelfthstreet", 1, Hair, 40),
                Hold("W1", "ba:street_fourthstreet", 46, Soda, 45),
                Hold("W2", "ba:street_gift", 2, Gift, 8),
            },
            new[]
            {
                new GoodsNeed(Salon, Hair),
                new GoodsNeed(Planned, Soda),
                new GoodsNeed(Fifth, Gift),
                new GoodsNeed(Fifth, Flower),
            },
            new[]
            {
                new DailyUnits(Salon, Hair, 1014, 4),
                new DailyUnits(Fifth, Gift, 1014, 4),
            });
    }

    private static Ran Run(string path, object save, World world)
    {
        var loaded = LoadLog.StartSession(path);
        var days = new DayWatch();
        var needs = new NeedsWatch();
        var statements = new[]
        {
            new FinancialDay(1015, new[] { new StatementRow("ba:street_fourthavenue", 28, 10f, 1f, 1f, 1f, 0f, 0f, 7f) }),
        };
        needs.Tick(path, save, 1015, new PlayerNeeds(37f, 12f, 64f, false, false));
        days.Tick(path, 1015, statements, world.Stops, world.Holds);
        var refill = needs.Tick(path, save, 1016, new PlayerNeeds(100f, 97f, 100f, true, true));
        var rolled = days.Tick(path, 1016, statements, world.Stops, world.Holds);
        var api = GameApi.Read(
            new[] { new PanelLine("red", "Low", "ba:street_fourthstreet", 46, "Close Low.") },
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var slots = new SlotWatch().Tick(
            path,
            save,
            1016,
            api,
            new[]
            {
                new GarageSlot("W7", 1, filled: false),
                new GarageSlot("W1", 1, filled: true, driver: "driver-kept"),
            },
            Array.Empty<string>());
        return new Ran(
            loaded,
            refill ?? throw new InvalidOperationException("needs refill missing"),
            rolled ?? "",
            api,
            Assert.Single(slots),
            Supply.Stops(RouteDump.RoutesPath(path)),
            Supply.Stock(RouteDump.WarehousesPath(path)));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "script", "game_build.py")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("repo root not found");
    }

    private sealed class World
    {
        public World(
            IReadOnlyList<DeliveryStop> stops,
            IReadOnlyList<WarehouseHold> holds,
            IReadOnlyList<GoodsNeed> needs,
            IReadOnlyList<DailyUnits> sales)
        {
            Stops = stops;
            Holds = holds;
            Needs = needs;
            Sales = sales;
        }

        public IReadOnlyList<DeliveryStop> Stops { get; }

        public IReadOnlyList<WarehouseHold> Holds { get; }

        public IReadOnlyList<GoodsNeed> Needs { get; }

        public IReadOnlyList<DailyUnits> Sales { get; }
    }

    private sealed class Ran
    {
        public Ran(
            string loaded,
            PlayerNeeds refill,
            string rolled,
            ApiNote api,
            string slotLine,
            IReadOnlyList<DeliveryStop> stops,
            IReadOnlyList<WarehouseHold> holds)
        {
            Loaded = loaded;
            Refill = refill;
            Rolled = rolled;
            Api = api;
            SlotLine = slotLine;
            Stops = stops;
            Holds = holds;
        }

        public string Loaded { get; }

        public PlayerNeeds Refill { get; }

        public string Rolled { get; }

        public ApiNote Api { get; }

        public string SlotLine { get; }

        public IReadOnlyList<DeliveryStop> Stops { get; }

        public IReadOnlyList<WarehouseHold> Holds { get; }
    }

    private sealed class TempStores : IDisposable
    {
        public TempStores()
        {
            Path = Directory.CreateTempSubdirectory("chaotic-stores").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
