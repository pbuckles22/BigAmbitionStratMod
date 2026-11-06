using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class HqPlanTests
{
    private const string Hair = "ba:itemname_haircareproduct";
    private const string Soda = "ba:itemname_sodacan";
    private const string Salon = "ba:street_fourthavenue 28";
    private const string Planned = "ba:street_side 9";

    [Fact]
    public void HqPlans_a_missing_pricing_plan_is_created_and_an_existing_one_is_left()
    {
        var api = GameApi.Live();
        var seats = Filled();

        var created = HqPlans.Assign(api, pricingPlan: false, seats, Array.Empty<HiredHand>());
        var left = HqPlans.Assign(api, pricingPlan: true, seats, Array.Empty<HiredHand>());

        var move = Assert.Single(created);
        Assert.Equal("create", move.Kind);
        Assert.Equal("", move.Employee);
        Assert.Equal(
            "UI.Smartphone.Apps.BizMan.PricingManagers.PricingManagersPlanList.AddPlan",
            move.Call);
        Assert.Empty(left);
    }

    [Fact]
    public void HqPlans_an_open_role_is_assigned_and_a_filled_role_is_left()
    {
        var api = GameApi.Live();
        var seats = new[]
        {
            new HqSeat("logistics", filled: true, employee: "kept-l"),
            new HqSeat("logistics", filled: false),
            new HqSeat("hr", filled: false),
            new HqSeat("purchasing", filled: true, employee: "kept-p"),
            new HqSeat("headhunter", filled: true, employee: "kept-n"),
        };
        var hired = new[]
        {
            new HiredHand("lm-spare", HqPlans.LogisticsSkill),
            new HiredHand("hr-2", HqPlans.HrSkill),
            new HiredHand("hr-1", HqPlans.HrSkill),
        };

        var moves = HqPlans.Assign(api, pricingPlan: true, seats, hired);

        var assigned = Assert.Single(moves);
        Assert.Equal("assign", assigned.Kind);
        Assert.Equal("hr", assigned.Role);
        Assert.Equal("hr-1", assigned.Employee);
        Assert.Equal("Buildings.Office.Headquarters.HrManagerPlan.AssignEmployee", assigned.Call);
    }

    [Fact]
    public void HqPlans_one_person_per_open_role_and_a_missing_skill_stays_open()
    {
        var api = GameApi.Live();
        var seats = new[] { new HqSeat("hr", filled: true, employee: "both") };
        var hired = new[]
        {
            new HiredHand("lm-b", HqPlans.LogisticsSkill),
            new HiredHand("lm-a", HqPlans.LogisticsSkill),
            new HiredHand("both", HqPlans.HrSkill),
            new HiredHand("both", HqPlans.LogisticsSkill),
            new HiredHand("hunt-c", HqPlans.HeadhunterSkill, candidate: true),
            new HiredHand("", HqPlans.PurchasingSkill),
        };

        var moves = HqPlans.Assign(api, pricingPlan: false, seats, hired);

        Assert.Equal("create", moves[0].Kind);
        Assert.Equal(
            "UI.Smartphone.Apps.BizMan.PricingManagers.PricingManagersPlanList.AddPlan",
            moves[0].Call);
        var assigned = Assert.Single(moves, move => move.Kind == "assign");
        Assert.Equal("logistics", assigned.Role);
        Assert.Equal("lm-a", assigned.Employee);
        Assert.Equal("Buildings.Office.Headquarters.HrManagerPlan.AssignEmployee", assigned.Call);
        Assert.DoesNotContain(moves, move => move.Role is "hr" or "purchasing" or "headhunter");
        Assert.DoesNotContain(moves, move => move.Employee == "both");
    }

    [Fact]
    public void HqPlans_a_missing_call_creates_nothing_and_assigns_nobody()
    {
        var moves = HqPlans.Assign(
            null,
            pricingPlan: false,
            Array.Empty<HqSeat>(),
            new[] { new HiredHand("lm-1", HqPlans.LogisticsSkill) });

        Assert.Empty(moves);
    }

    [Fact]
    public void HqPlans_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_locks_add_and_assign()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign
        using var dir = new TempHq();
        var save = new object();
        var world = Sample();
        var ran = Run(dir.Path, save, world);
        var connected = new ConnectWatch().Tick(
            dir.Path,
            save,
            1016,
            ran.Api,
            ran.Stops,
            ran.Holds,
            world.Needs,
            world.Sales);
        var plans = new PlanWatch();
        var seats = new[]
        {
            new HqSeat("hr", filled: true, employee: "kept-h"),
            new HqSeat("purchasing", filled: true, employee: "kept-p"),
            new HqSeat("headhunter", filled: true, employee: "kept-n"),
        };
        var hired = new[] { new HiredHand("lm-1", HqPlans.LogisticsSkill) };
        var written = plans.Tick(dir.Path, save, 1016, ran.Api, pricingPlan: false, seats, hired);
        var again = plans.Tick(dir.Path, save, 1017, ran.Api, pricingPlan: false, seats, hired);

        Assert.Equal(Needs.Full, ran.Refill.Energy);
        Assert.Contains("day=1016", ran.Rolled);
        Assert.Equal(
            "UI.Smartphone.Apps.BizMan.PricingManagers.PricingManagersPlanList.AddPlan",
            ran.Api.Call("pricing plan create"));
        Assert.Equal("Buildings.Office.Headquarters.HrManagerPlan.AssignEmployee", ran.Api.Call("plan assign"));
        var link = Assert.Single(connected, line => line.Contains(" store=" + Salon + " "));
        Assert.Equal(ran.Api.Call("delivery"), Field(link, "add"));
        Assert.Equal(
            new[]
            {
                "plan.created version=" + LoadLog.Version
                    + " day=1016 create=" + ran.Api.Call("pricing plan create"),
                "plan.assigned version=" + LoadLog.Version
                    + " day=1016 role=logistics employee=lm-1 assign=" + ran.Api.Call("plan assign"),
            },
            written);
        var log = File.ReadAllLines(LoadLog.CeoLogPath(dir.Path));
        Assert.Equal(ran.Loaded, log[0]);
        var connectAt = Array.IndexOf(log, link);
        var createAt = Array.IndexOf(log, written[0]);
        var assignAt = Array.IndexOf(log, written[1]);
        Assert.True(connectAt > Array.IndexOf(log, ran.SlotLine));
        Assert.True(createAt > connectAt && assignAt > createAt);
        Assert.Empty(again);
        log = File.ReadAllLines(LoadLog.CeoLogPath(dir.Path));
        Assert.Single(log, line => line.StartsWith("plan.created "));
        Assert.Single(log, line => line.StartsWith("plan.assigned "));
    }

    [Fact]
    public void HqPlans_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_leaves_a_staffed_headquarters()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → staffed
        using var dir = new TempHq();
        var save = new object();
        var world = Sample();
        var ran = Run(dir.Path, save, world);
        var connect = new ConnectWatch();
        var connected = connect.Tick(
            dir.Path,
            save,
            1016,
            ran.Api,
            ran.Stops,
            ran.Holds,
            world.Needs,
            world.Sales);
        RouteDump.WriteRoutes(dir.Path, world.Stops);
        var againConnect = connect.Tick(
            dir.Path,
            save,
            1017,
            ran.Api,
            ran.Stops,
            ran.Holds,
            world.Needs,
            world.Sales);
        var watch = new PlanWatch();
        var written = watch.Tick(dir.Path, save, 1016, ran.Api, pricingPlan: true, Filled(), Array.Empty<HiredHand>());
        var again = watch.Tick(dir.Path, save, 1017, ran.Api, pricingPlan: true, Filled(), Array.Empty<HiredHand>());

        Assert.NotEmpty(connected);
        Assert.Empty(againConnect);
        Assert.Equal(
            new[] { "plan.summary version=" + LoadLog.Version + " day=1016 created=0 assigned=0" },
            written);
        Assert.Empty(again);
        var log = File.ReadAllLines(LoadLog.CeoLogPath(dir.Path));
        Assert.DoesNotContain(log, line => line.StartsWith("plan.created ") || line.StartsWith("plan.assigned "));
        Assert.Single(log, line => line.StartsWith("plan.summary "));
        var connectAt = Array.FindIndex(log, line => line.StartsWith("store.connected "));
        var summaryAt = Array.IndexOf(log, written[0]);
        Assert.True(connectAt >= 0 && summaryAt > connectAt);
    }

    private static HqSeat[] Filled()
    {
        return new[]
        {
            new HqSeat("logistics", filled: true, employee: "kept-l"),
            new HqSeat("hr", filled: true, employee: "kept-h"),
            new HqSeat("purchasing", filled: true, employee: "kept-p"),
            new HqSeat("headhunter", filled: true, employee: "kept-n"),
        };
    }

    private static World Sample()
    {
        return new World(
            new[] { new DeliveryStop("W1", Planned, Soda, null, "") },
            new[]
            {
                new WarehouseHold("W11", "ba:street_twelfthstreet", 1, Hair, 40),
                new WarehouseHold("W1", "ba:street_fourthstreet", 46, Soda, 45),
            },
            new[]
            {
                new GoodsNeed(Salon, Hair),
                new GoodsNeed(Planned, Soda),
            },
            Array.Empty<DailyUnits>());
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

    private static string Field(string line, string name)
    {
        var token = name + "=";
        var start = line.IndexOf(token, StringComparison.Ordinal);
        if (start < 0)
        {
            return "";
        }

        start += token.Length;
        var end = line.IndexOf(' ', start);
        return end < 0 ? line.Substring(start) : line.Substring(start, end - start);
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

    private sealed class TempHq : IDisposable
    {
        public TempHq()
        {
            Path = Directory.CreateTempSubdirectory("chaotic-hq").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
