using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class RestockTests
{
    private const string Hair = "ba:itemname_haircareproduct";
    private const string Soda = "ba:itemname_sodacan";
    private const string Phone = "ba:itemname_smartphone1";
    private const string Gift = "ba:itemname_cheapgift";
    private const string Salon = "ba:street_fourthavenue 28";
    private const string Planned = "ba:street_side 9";
    private const string GiftShop = "ba:street_fifthavenue 36";

    [Fact]
    public void Restock_a_short_warehouse_is_ordered_and_a_covered_or_ordered_one_is_left()
    {
        var api = GameApi.Live();
        var levels = new[]
        {
            new WarehouseLevel("W1", "food", onHand: 10, daily: 4, ordered: false),
            new WarehouseLevel("W2", "food", onHand: 28, daily: 4, ordered: false),
            new WarehouseLevel("W3", "food", onHand: 1, daily: 4, ordered: true),
        };

        var orders = Restock.Order(api, levels);

        var ordered = Assert.Single(orders);
        Assert.Equal("W1", ordered.Warehouse);
        Assert.Equal("food", ordered.Family);
        Assert.Equal(18, ordered.Amount);
        Assert.Equal(
            "UI.Smartphone.Apps.BizMan.PurchasingAgent.PurchasingAgentProductCellView.ChangeAssignedWarehouse",
            ordered.Assign);
        Assert.Equal(
            "UI.Smartphone.Apps.BizMan.PurchasingAgent.PurchasingAgentPlanUI.StartOrder",
            ordered.Start);
    }

    [Fact]
    public void Restock_a_warehouse_with_no_sales_is_left_and_a_delivering_empty_one_is_ordered()
    {
        var orders = Restock.Order(
            GameApi.Live(),
            new[]
            {
                new WarehouseLevel("W12", "food", onHand: 0, daily: 0, ordered: false),
                new WarehouseLevel("W1", "food", onHand: 0, daily: 4, ordered: false),
            });

        var ordered = Assert.Single(orders);
        Assert.Equal("W1", ordered.Warehouse);
        Assert.Equal(28, ordered.Amount);
    }

    [Fact]
    public void Restock_a_new_home_orders_from_wholesaler_burn_and_a_home_with_no_burn_is_left()
    {
        var burn = Restock.FamilyBurn(
            "specialty",
            "W12",
            Array.Empty<DeliveryStop>(),
            new[] { new DailyUnits(GiftShop, Gift, 1015, 6) });
        var none = Restock.FamilyBurn(
            "specialty",
            "W12",
            Array.Empty<DeliveryStop>(),
            Array.Empty<DailyUnits>());

        Assert.Equal(6, burn);
        Assert.Equal(0, none);
        var ordered = Assert.Single(Restock.Order(
            GameApi.Live(),
            new[] { new WarehouseLevel("W12", "specialty", onHand: 0, daily: burn, ordered: false) }));
        Assert.Equal(42, ordered.Amount);
        Assert.Empty(Restock.Order(
            GameApi.Live(),
            new[] { new WarehouseLevel("W12", "specialty", onHand: 0, daily: none, ordered: false) }));
    }

    [Fact]
    public void Restock_no_warehouse_sorts_nothing()
    {
        var none = Restock.Arrange(null);
        var empty = Restock.Arrange(Array.Empty<WarehouseHold>());

        Assert.Empty(none.Homes);
        Assert.Empty(none.Drains);
        Assert.Empty(empty.Homes);
        Assert.Empty(empty.Drains);
    }

    [Fact]
    public void Restock_one_mixed_warehouse_keeps_the_majority_and_does_not_drain()
    {
        var arranged = Restock.Arrange(new[]
        {
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, Soda, 10),
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, Phone, 3),
        });

        var home = Assert.Single(arranged.Homes);
        Assert.Equal("W1", home.Warehouse);
        Assert.Equal("food", home.Family);
        Assert.Empty(arranged.Drains);
    }

    [Fact]
    public void Restock_a_second_warehouse_receives_the_other_family()
    {
        var arranged = Restock.Arrange(new[]
        {
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, Soda, 10),
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, Phone, 3),
            new WarehouseHold("W2", "ba:street_eighthstreet", 12, "ba:itemname_palletshelf", 40),
        });

        Assert.Equal("food", arranged.Homes.Single(level => level.Warehouse == "W1").Family);
        Assert.Equal("durables", arranged.Homes.Single(level => level.Warehouse == "W2").Family);
        var drain = Assert.Single(arranged.Drains);
        Assert.Equal("durables", drain.Family);
        Assert.Equal("W1", drain.From);
        Assert.Equal("W2", drain.To);
        Assert.Equal(3, drain.Amount);
    }

    [Fact]
    public void Restock_an_empty_warehouse_with_nothing_to_receive_stays_empty()
    {
        var arranged = Restock.Arrange(new[]
        {
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, Soda, 45),
            new WarehouseHold("W12", "ba:street_twelfthstreet", 6, "ba:itemname_palletshelf", 99),
            new WarehouseHold("W14", "ba:street_fifthavenue", 55, "ba:itemname_ceilinglampwarehouse", 35),
        });

        Assert.Equal("food", arranged.Homes.Single(level => level.Warehouse == "W1").Family);
        Assert.Equal("", arranged.Homes.Single(level => level.Warehouse == "W12").Family);
        Assert.Equal("", arranged.Homes.Single(level => level.Warehouse == "W14").Family);
        Assert.Empty(arranged.Drains);
    }

    [Fact]
    public void Restock_mixed_stock_drains_to_the_family_warehouse_and_a_shelfless_building_stays()
    {
        var arranged = Restock.Arrange(new[]
        {
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, Soda, 232),
            new WarehouseHold("W11", "ba:street_twelfthstreet", 1, Hair, 15),
            new WarehouseHold("W9", "ba:street_fifthavenue", 35, Phone, 510),
            new WarehouseHold("W9", "ba:street_fifthavenue", 35, "ba:itemname_novel", 226),
            new WarehouseHold("W12", "ba:street_twelfthstreet", 6, "ba:itemname_palletshelf", 99),
            new WarehouseHold("W13", "ba:street_twelfthstreet", 5, "ba:itemname_palletshelf", 95),
            new WarehouseHold("W14", "ba:street_fifthavenue", 55, "ba:itemname_ceilinglampwarehouse", 35),
        });

        Assert.Equal("durables", arranged.Homes.Single(level => level.Warehouse == "W9").Family);
        Assert.Equal("specialty", arranged.Homes.Single(level => level.Warehouse == "W12").Family);
        Assert.Equal("", arranged.Homes.Single(level => level.Warehouse == "W13").Family);
        Assert.Equal("", arranged.Homes.Single(level => level.Warehouse == "W14").Family);
        var drain = Assert.Single(arranged.Drains);
        Assert.Equal("specialty", drain.Family);
        Assert.Equal("W9", drain.From);
        Assert.Equal("W12", drain.To);
        Assert.Equal(226, drain.Amount);
    }

    [Fact]
    public void Restock_placed_shelves_make_an_empty_warehouse_the_specialty_home()
    {
        var arranged = Restock.Arrange(
            new[]
            {
                new WarehouseHold("W9", "ba:street_fifthavenue", 35, Phone, 510),
                new WarehouseHold("W9", "ba:street_fifthavenue", 35, "ba:itemname_novel", 226),
            },
            new Dictionary<string, int> { ["W12"] = 99, ["W14"] = 0 });

        Assert.Equal("specialty", arranged.Homes.Single(level => level.Warehouse == "W12").Family);
        Assert.DoesNotContain(arranged.Homes, level => level.Warehouse == "W14");
        var drain = Assert.Single(arranged.Drains);
        Assert.Equal("W9", drain.From);
        Assert.Equal("W12", drain.To);
        Assert.Equal("specialty", drain.Family);
    }

    [Fact]
    public void Restock_unconnected_family_burn_is_ordered_on_the_smaller_home_only()
    {
        var burned = Restock.WithBurn(
            new[]
            {
                new WarehouseLevel("W7", "food", onHand: 1089, daily: 0, ordered: false),
                new WarehouseLevel("W4", "food", onHand: 173, daily: 0, ordered: false),
                new WarehouseLevel("W12", "specialty", onHand: 0, daily: 0, ordered: false, shelves: 99),
            },
            Array.Empty<DeliveryStop>(),
            new[]
            {
                new DailyUnits(Planned, Soda, 1015, 4),
                new DailyUnits(GiftShop, Gift, 1015, 6),
            },
            null);

        Assert.Equal(0, burned.Single(level => level.Warehouse == "W7").Daily);
        Assert.Equal(4, burned.Single(level => level.Warehouse == "W4").Daily);
        Assert.Equal(6, burned.Single(level => level.Warehouse == "W12").Daily);
        var ordered = Restock.Order(GameApi.Live(), burned);
        Assert.Equal(new[] { "W12" }, ordered.Select(order => order.Warehouse).ToArray());
        Assert.Equal(42, Assert.Single(ordered).Amount);
    }

    [Fact]
    public void Restock_a_home_with_an_order_and_under_seven_days_claims_the_next_empty_shelved_warehouse()
    {
        var homes = new[]
        {
            new WarehouseLevel("W9", "durables", onHand: 10, daily: 4, ordered: true, shelves: 100),
            new WarehouseLevel("W12", "", onHand: 0, daily: 0, ordered: false, shelves: 99),
            new WarehouseLevel("W13", "", onHand: 0, daily: 0, ordered: false, shelves: 95),
            new WarehouseLevel("W14", "", onHand: 0, daily: 0, ordered: false, shelves: 0),
        };

        var plan = Restock.Overflow(homes, null);

        var claim = Assert.Single(plan.Claims);
        Assert.Equal("durables", claim.Family);
        Assert.Equal("W9", claim.Home);
        Assert.Equal("W12", claim.Second);
        Assert.Equal("short", claim.Reason);
        Assert.Equal("durables", plan.Homes.Single(level => level.Warehouse == "W12").Family);
        Assert.Equal("", plan.Homes.Single(level => level.Warehouse == "W13").Family);
        Assert.Equal("", plan.Homes.Single(level => level.Warehouse == "W14").Family);
    }

    [Fact]
    public void Restock_a_home_under_seven_days_with_no_order_does_not_claim_a_second_warehouse()
    {
        var plan = Restock.Overflow(
            new[]
            {
                new WarehouseLevel("W9", "durables", onHand: 10, daily: 4, ordered: false, shelves: 100),
                new WarehouseLevel("W12", "", onHand: 0, daily: 0, ordered: false, shelves: 99),
            },
            null);

        Assert.Empty(plan.Claims);
        Assert.Equal("", plan.Homes.Single(level => level.Warehouse == "W12").Family);
    }

    [Fact]
    public void Restock_busy_trucks_claim_a_second_warehouse_and_no_empty_shelf_claims_nothing()
    {
        var busy = Restock.Overflow(
            new[]
            {
                new WarehouseLevel("W9", "durables", onHand: 40, daily: 4, ordered: false, shelves: 100),
                new WarehouseLevel("W12", "", onHand: 0, daily: 0, ordered: false, shelves: 99),
            },
            new Dictionary<string, bool> { ["durables"] = true });
        var none = Restock.Overflow(
            new[]
            {
                new WarehouseLevel("W9", "durables", onHand: 10, daily: 4, ordered: true, shelves: 100),
                new WarehouseLevel("W14", "", onHand: 0, daily: 0, ordered: false, shelves: 0),
            },
            null);

        var claim = Assert.Single(busy.Claims);
        Assert.Equal("trucks", claim.Reason);
        Assert.Equal("W12", claim.Second);
        Assert.Empty(none.Claims);
    }

    [Fact]
    public void Restock_a_missing_call_orders_nothing()
    {
        var orders = Restock.Order(
            null,
            new[] { new WarehouseLevel("W12", "food", onHand: 0, daily: 4, ordered: false) });

        Assert.Empty(orders);
    }

    [Fact]
    public void Restock_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_locks_assign_and_start()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock
        using var dir = new TempRestock();
        var save = new object();
        var ran = Run(dir.Path, save);
        var connected = new ConnectWatch().Tick(
            dir.Path, save, 1016, ran.Api, ran.Stops, ran.Holds, SampleNeeds(), Array.Empty<DailyUnits>());
        var plans = new PlanWatch().Tick(
            dir.Path,
            save,
            1016,
            ran.Api,
            pricingPlan: true,
            new[]
            {
                new HqSeat("logistics", filled: true, employee: "kept-l"),
                new HqSeat("hr", filled: true, employee: "kept-h"),
                new HqSeat("purchasing", filled: true, employee: "kept-p"),
                new HqSeat("headhunter", filled: true, employee: "kept-n"),
            },
            Array.Empty<HiredHand>());
        var restock = new RestockWatch();
        var written = restock.Tick(
            dir.Path,
            save,
            1016,
            ran.Api,
            new[] { new WarehouseLevel("W12", "food", onHand: 0, daily: 4, ordered: false) });
        var again = restock.Tick(
            dir.Path,
            save,
            1017,
            ran.Api,
            new[] { new WarehouseLevel("W12", "food", onHand: 0, daily: 4, ordered: false) });

        Assert.Equal(Needs.Full, ran.Refill.Energy);
        Assert.Contains("day=1016", ran.Rolled);
        Assert.Equal(
            "UI.Smartphone.Apps.BizMan.PurchasingAgent.PurchasingAgentProductCellView.ChangeAssignedWarehouse",
            ran.Api.Call("purchasing plan"));
        Assert.Equal(
            "UI.Smartphone.Apps.BizMan.PurchasingAgent.PurchasingAgentPlanUI.StartOrder",
            ran.Api.Call("importer order"));
        Assert.NotEmpty(connected);
        Assert.Equal(
            new[] { "plan.summary version=" + LoadLog.Version + " day=1016 created=0 assigned=0" },
            plans);
        var line = Assert.Single(written);
        Assert.Equal(
            "restock.ordered version=" + LoadLog.Version
                + " day=1016 warehouse=W12 family=food amount=28 assign=" + ran.Api.Call("purchasing plan")
                + " start=" + ran.Api.Call("importer order"),
            line);
        Assert.Empty(again);
        var log = File.ReadAllLines(LoadLog.CeoLogPath(dir.Path));
        Assert.True(Array.IndexOf(log, line) > Array.IndexOf(log, plans[0]));
        Assert.Single(log, row => row.StartsWith("restock.ordered "));
    }

    [Fact]
    public void Restock_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_leaves_a_covered_warehouse()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → covered
        using var dir = new TempRestock();
        var save = new object();
        var ran = Run(dir.Path, save);
        new ConnectWatch().Tick(
            dir.Path, save, 1016, ran.Api, ran.Stops, ran.Holds, SampleNeeds(), Array.Empty<DailyUnits>());
        new PlanWatch().Tick(
            dir.Path,
            save,
            1016,
            ran.Api,
            pricingPlan: true,
            new[] { new HqSeat("logistics", filled: true, employee: "kept-l") },
            Array.Empty<HiredHand>());
        var watch = new RestockWatch();
        var written = watch.Tick(
            dir.Path,
            save,
            1016,
            ran.Api,
            new[] { new WarehouseLevel("W1", "food", onHand: 40, daily: 4, ordered: false) });
        var again = watch.Tick(
            dir.Path,
            save,
            1017,
            ran.Api,
            new[] { new WarehouseLevel("W1", "food", onHand: 40, daily: 4, ordered: false) });

        Assert.Equal(
            new[] { "restock.summary version=" + LoadLog.Version + " day=1016 ordered=0" },
            written);
        Assert.Empty(again);
        var log = File.ReadAllLines(LoadLog.CeoLogPath(dir.Path));
        Assert.DoesNotContain(log, row => row.StartsWith("restock.ordered "));
        Assert.Single(log, row => row.StartsWith("restock.summary "));
    }

    [Fact]
    public void Restock_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_locks_drain_and_order()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order
        using var dir = new TempRestock();
        var save = new object();
        var ran = Run(dir.Path, save);
        new ConnectWatch().Tick(
            dir.Path, save, 1016, ran.Api, ran.Stops, ran.Holds, SampleNeeds(), Array.Empty<DailyUnits>());
        new PlanWatch().Tick(
            dir.Path,
            save,
            1016,
            ran.Api,
            pricingPlan: true,
            new[] { new HqSeat("logistics", filled: true, employee: "kept-l") },
            Array.Empty<HiredHand>());
        var holds = ran.Holds.Concat(new[]
        {
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, Phone, 3),
            new WarehouseHold("W12", "ba:street_twelfthstreet", 6, "ba:itemname_palletshelf", 99),
            new WarehouseHold("W13", "ba:street_twelfthstreet", 5, "ba:itemname_palletshelf", 95),
        }).ToArray();
        var arranged = Restock.Arrange(holds);
        var levels = arranged.Homes.Select(level => level.Warehouse == "W12"
            ? new WarehouseLevel(level.Warehouse, level.Family, 0, 6, false, level.Mixed, level.Shelves)
            : level.Warehouse == "W1" && level.Family == "food"
                ? new WarehouseLevel(level.Warehouse, level.Family, 10, 4, true, level.Mixed, level.Shelves)
                : level).ToArray();
        var overflow = Restock.Overflow(levels, null);
        var written = new RestockWatch().Tick(
            dir.Path, save, 1016, ran.Api, overflow.Homes, arranged.Drains, overflow.Claims);

        Assert.Equal(
            new[]
            {
                "warehouse.drain version=" + LoadLog.Version
                    + " day=1016 family=durables from=W1 to=W12 amount=3 assign=" + ran.Api.Call("purchasing plan"),
                "warehouse.overflow version=" + LoadLog.Version
                    + " day=1016 family=food home=W1 second=W13 reason=short",
                "restock.ordered version=" + LoadLog.Version
                    + " day=1016 warehouse=W12 family=durables amount=42 assign=" + ran.Api.Call("purchasing plan")
                    + " start=" + ran.Api.Call("importer order"),
                "restock.ordered version=" + LoadLog.Version
                    + " day=1016 warehouse=W13 family=food amount=28 assign=" + ran.Api.Call("purchasing plan")
                    + " start=" + ran.Api.Call("importer order"),
            },
            written);
    }

    private static GoodsNeed[] SampleNeeds()
    {
        return new[]
        {
            new GoodsNeed(Salon, Hair),
            new GoodsNeed(Planned, Soda),
        };
    }

    private static Ran Run(string path, object save)
    {
        var loaded = LoadLog.StartSession(path);
        var days = new DayWatch();
        var needs = new NeedsWatch();
        var stops = new[] { new DeliveryStop("W1", Planned, Soda, null, "") };
        var holds = new[]
        {
            new WarehouseHold("W11", "ba:street_twelfthstreet", 1, Hair, 40),
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, Soda, 45),
        };
        var statements = new[]
        {
            new FinancialDay(1015, new[] { new StatementRow("ba:street_fourthavenue", 28, 10f, 1f, 1f, 1f, 0f, 0f, 7f) }),
        };
        needs.Tick(path, save, 1015, new PlayerNeeds(37f, 12f, 64f, false, false));
        days.Tick(path, 1015, statements, stops, holds);
        var refill = needs.Tick(path, save, 1016, new PlayerNeeds(100f, 97f, 100f, true, true));
        var rolled = days.Tick(path, 1016, statements, stops, holds);
        var api = GameApi.Read(
            new[] { new PanelLine("red", "Low", "ba:street_fourthstreet", 46, "Close Low.") },
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        new SlotWatch().Tick(
            path,
            save,
            1016,
            api,
            new[] { new GarageSlot("W7", 1, filled: false) },
            Array.Empty<string>());
        return new Ran(
            loaded,
            refill ?? throw new InvalidOperationException("needs refill missing"),
            rolled ?? "",
            api,
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

    private sealed class Ran
    {
        public Ran(
            string loaded,
            PlayerNeeds refill,
            string rolled,
            ApiNote api,
            IReadOnlyList<DeliveryStop> stops,
            IReadOnlyList<WarehouseHold> holds)
        {
            Loaded = loaded;
            Refill = refill;
            Rolled = rolled;
            Api = api;
            Stops = stops;
            Holds = holds;
        }

        public string Loaded { get; }

        public PlayerNeeds Refill { get; }

        public string Rolled { get; }

        public ApiNote Api { get; }

        public IReadOnlyList<DeliveryStop> Stops { get; }

        public IReadOnlyList<WarehouseHold> Holds { get; }
    }

    private sealed class TempRestock : IDisposable
    {
        public TempRestock()
        {
            Path = Directory.CreateTempSubdirectory("chaotic-restock").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
