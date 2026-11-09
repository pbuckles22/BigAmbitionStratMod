using System.Globalization;
using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class AuditTests
{
    private const string Hair = "ba:itemname_haircareproduct";
    private const string Soda = "ba:itemname_sodacan";
    private const string Phone = "ba:itemname_smartphone1";
    private const string Salon = "ba:street_fourthavenue 28";
    private const string Planned = "ba:street_side 9";

    [Fact]
    public void Audit_summary_names_the_strategy_counts()
    {
        var api = GameApi.Live();
        var unconnected = Audit.Unconnected(
            Array.Empty<DeliveryStop>(),
            new[] { Hold("W11", "ba:street_twelfthstreet", 1, Hair, 40) },
            new[] { new GoodsNeed(Salon, Hair) },
            Array.Empty<DailyUnits>());
        var emptySlots = Audit.EmptySlots(new[]
        {
            new GarageSlot("W7", 1, filled: false),
            new GarageSlot("W8", 1, filled: false),
            new GarageSlot("W9", 1, filled: true),
        });
        var dry = Audit.Dry(api, new[]
        {
            new WarehouseLevel("W1", "food", onHand: 0, daily: 4, ordered: false),
            new WarehouseLevel("W2", "food", onHand: 1, daily: 4, ordered: false),
            new WarehouseLevel("W3", "food", onHand: 2, daily: 4, ordered: false),
            new WarehouseLevel("W4", "food", onHand: 40, daily: 4, ordered: false),
        });
        var mixed = Audit.Mixed(new[]
        {
            Hold("W6", "ba:street_fifthavenue", 43, Hair, 40),
            Hold("W6", "ba:street_fifthavenue", 43, Soda, 8),
        });
        var unstaffed = Audit.Unstaffed(
            new[] { Salon, Planned, "ba:street_a 1", "ba:street_b 2" },
            new[] { Planned });
        var missing = Audit.MissingPlans(
            api,
            pricingPlan: false,
            new[]
            {
                new HqSeat("logistics", filled: true, employee: "kept-l"),
                new HqSeat("hr", filled: true, employee: "kept-h"),
                new HqSeat("purchasing", filled: true, employee: "kept-p"),
                new HqSeat("headhunter", filled: true, employee: "kept-n"),
            },
            Array.Empty<HiredHand>());
        var cash = Audit.CashBelowReserve(10m, 10m);
        var pin = Audit.PinMismatch("1", LoadLog.PinnedBuild);
        var clear = Audit.Unconnected(
            new[] { new DeliveryStop("W1", Planned, Soda, null, "") },
            new[] { Hold("W1", "ba:street_fourthstreet", 46, Soda, 45) },
            new[] { new GoodsNeed(Planned, Soda) },
            Array.Empty<DailyUnits>());

        Assert.Equal(1, unconnected);
        Assert.Equal(2, emptySlots);
        Assert.Equal(3, dry);
        Assert.Equal(1, mixed);
        Assert.Equal(3, unstaffed);
        Assert.Equal(1, missing);
        Assert.Equal(1, cash);
        Assert.Equal(1, pin);
        Assert.Equal(0, clear);
        Assert.Equal(0, Audit.CashBelowReserve(100m, 10m));
        Assert.Equal(0, Audit.PinMismatch(LoadLog.PinnedBuild, LoadLog.PinnedBuild));
        Assert.Equal(0, Audit.PinMismatch("", LoadLog.PinnedBuild));
        Assert.Equal(
            "audit.summary version=" + LoadLog.Version
                + " day=1016 unconnected=1 emptySlots=2 dry=3 mixed=1 unstaffed=3 missingPlans=1 cashBelowReserve=1 pinMismatch=1 healed=0",
            Audit.Summary(1016, new AuditCounts(1, 2, 3, 1, 3, 1, 1, 1), 0));
    }

    [Fact]
    public void Audit_a_planted_unconnected_store_is_the_heal()
    {
        using var dir = new TempAudit();
        var api = GameApi.Live();
        var calls = new List<string>();
        var report = Audit.Check(
            dir.Path,
            1016,
            api,
            PlantedStore(),
            LiveGuard(),
            heal =>
            {
                calls.Add(heal.Kind + " " + heal.Call);
                return HealFate.Done;
            });

        var heal = Assert.Single(report.Lines, line => line.StartsWith("audit.healed "));
        Assert.Equal(
            "audit.healed version=" + LoadLog.Version
                + " day=1016 action=connect store=" + Salon
                + " warehouse=W11 item=" + Hair
                + " call=" + api.Call("delivery")
                + " dry=false",
            heal);
        Assert.Equal(new[] { "connect " + api.Call("delivery") }, calls);
        var summary = Assert.Single(report.Lines, line => line.StartsWith("audit.summary "));
        Assert.Contains("unconnected=0", summary);
        Assert.Contains("healed=1", summary);
        var log = File.ReadAllLines(LoadLog.CeoLogPath(dir.Path));
        Assert.True(Array.IndexOf(log, heal) < Array.IndexOf(log, summary));
        var shot = File.ReadAllText(SupplyNight.SnapshotPath(dir.Path));
        Assert.Contains("snapshot.taken version=" + LoadLog.Version, shot);
        Assert.DoesNotContain(Salon, shot);
        Assert.Equal(0, Audit.Unconnected(report.Stops, PlantedHolds(), PlantedNeeds(), Array.Empty<DailyUnits>()));
    }

    [Fact]
    public void Audit_a_dry_run_logs_the_heal_and_does_not_call_the_bridge()
    {
        using var dir = new TempAudit();
        var called = false;
        var report = Audit.Check(
            dir.Path,
            1016,
            GameApi.Live(),
            PlantedStore(),
            new AuditGuard(enabled: true, dryRun: true, cash: 1000m, dailyCost: 10m, truckCost: 1, build: LoadLog.PinnedBuild),
            heal =>
            {
                called = true;
                return HealFate.Done;
            });

        Assert.False(called);
        var heal = Assert.Single(report.Lines, line => line.StartsWith("audit.healed "));
        Assert.EndsWith(" dry=true", heal);
        Assert.Contains(" store=" + Salon + " ", heal);
        var summary = Assert.Single(report.Lines, line => line.StartsWith("audit.summary "));
        Assert.Contains("unconnected=0", summary);
        Assert.Contains("healed=1", summary);
    }

    [Fact]
    public void Audit_heal_cap_leaves_the_eleventh_store()
    {
        using var dir = new TempAudit();
        var needs = new List<GoodsNeed>();
        for (var index = 1; index <= 11; index++)
        {
            needs.Add(new GoodsNeed("ba:street_side " + index.ToString(), Hair));
        }

        var calls = 0;
        var report = Audit.Check(
            dir.Path,
            1016,
            GameApi.Live(),
            new AuditWorld(
                Array.Empty<DeliveryStop>(),
                PlantedHolds(),
                needs,
                Array.Empty<DailyUnits>(),
                Array.Empty<GarageSlot>(),
                Array.Empty<string>(),
                Array.Empty<WarehouseLevel>(),
                pricingPlan: true,
                Array.Empty<HqSeat>(),
                Array.Empty<HiredHand>(),
                Array.Empty<string>(),
                Array.Empty<string>()),
            LiveGuard(),
            heal =>
            {
                calls++;
                return HealFate.Done;
            });

        Assert.Equal(Audit.HealCap, calls);
        Assert.Equal(Audit.HealCap, report.Lines.Count(line => line.StartsWith("audit.healed ")));
        var summary = Assert.Single(report.Lines, line => line.StartsWith("audit.summary "));
        Assert.Contains("unconnected=1", summary);
        Assert.Contains("healed=" + Audit.HealCap.ToString(), summary);
        Assert.DoesNotContain(report.Lines, line => line.Contains(" store=ba:street_side 11 "));
    }

    [Fact]
    public void Audit_a_clothing_store_fills_the_heal_cap_before_the_next_store()
    {
        var clothing = new[]
        {
            "ba:itemname_moderncheapfemaleclothing",
            "ba:itemname_classicexpensivefemaleclothing",
            "ba:itemname_modernexpensivemaleclothing",
            "ba:itemname_classiccheapfemaleclothing",
            "ba:itemname_moderncheapmaleclothing",
            "ba:itemname_modernexpensivefemaleclothing",
            "ba:itemname_classiccheapmaleclothing",
            "ba:itemname_classicexpensivemaleclothing",
            "ba:itemname_headphones01",
            "ba:itemname_smartphone1",
        };
        var next = "ba:itemname_earbuds01";
        var holds = new List<WarehouseHold>();
        var needs = new List<GoodsNeed>();
        foreach (var item in clothing)
        {
            holds.Add(Hold("W9", "ba:street_firstavenue", 38, item, 40));
            needs.Add(new GoodsNeed("ba:street_firstavenue 38", item));
        }

        holds.Add(Hold("W9", "ba:street_firstavenue", 38, next, 40));
        needs.Add(new GoodsNeed("ba:street_firstavenue 5", next));
        using var dir = new TempAudit();
        var report = Audit.Check(
            dir.Path,
            1015,
            GameApi.Live(),
            new AuditWorld(
                Array.Empty<DeliveryStop>(),
                holds,
                needs,
                Array.Empty<DailyUnits>(),
                Array.Empty<GarageSlot>(),
                Array.Empty<string>(),
                Array.Empty<WarehouseLevel>(),
                pricingPlan: true,
                Array.Empty<HqSeat>(),
                Array.Empty<HiredHand>(),
                Array.Empty<string>(),
                Array.Empty<string>()),
            LiveGuard(),
            heal => HealFate.Done);

        Assert.Equal(Audit.HealCap, report.Lines.Count(line => line.StartsWith("audit.healed ")));
        Assert.DoesNotContain(report.Lines, line => line.Contains(" store=ba:street_firstavenue 5 "));
        var summary = Assert.Single(report.Lines, line => line.StartsWith("audit.summary "));
        Assert.Contains("unconnected=1", summary);
        Assert.Contains("healed=" + Audit.HealCap.ToString(), summary);
    }

    [Fact]
    public void Audit_an_empty_slot_is_bought_when_the_spend_fits_and_left_when_it_does_not()
    {
        using var dir = new TempAudit();
        var api = GameApi.Live();
        var bought = Audit.Check(
            dir.Path,
            1016,
            api,
            SlotWorld(),
            new AuditGuard(enabled: true, dryRun: false, cash: 1000m, dailyCost: 10m, truckCost: 1, build: LoadLog.PinnedBuild),
            heal => HealFate.Done);
        var skipped = Audit.Check(
            dir.Path,
            1017,
            api,
            SlotWorld(),
            new AuditGuard(enabled: true, dryRun: false, cash: 100m, dailyCost: 10m, truckCost: 100, build: LoadLog.PinnedBuild),
            heal => HealFate.Done);

        var heal = Assert.Single(bought.Lines, line => line.StartsWith("audit.healed "));
        Assert.Equal(
            "audit.healed version=" + LoadLog.Version
                + " day=1016 action=slot warehouse=W7 slot=1 order=" + api.Call("truck buy")
                + " assign=" + api.Call("slot assign")
                + " driver= dry=false",
            heal);
        Assert.Contains("emptySlots=0", Assert.Single(bought.Lines, line => line.StartsWith("audit.summary ")));
        Assert.DoesNotContain(skipped.Lines, line => line.StartsWith("audit.healed "));
        Assert.Contains("emptySlots=1", Assert.Single(skipped.Lines, line => line.StartsWith("audit.summary ")));
        Assert.Contains("healed=0", Assert.Single(skipped.Lines, line => line.StartsWith("audit.summary ")));
    }

    [Fact]
    public void Audit_three_failures_pause_the_autopilot()
    {
        using var dir = new TempAudit();
        var needs = new List<GoodsNeed>();
        for (var index = 1; index <= 4; index++)
        {
            needs.Add(new GoodsNeed("ba:street_side " + index.ToString(), Hair));
        }

        var calls = 0;
        var report = Audit.Check(
            dir.Path,
            1016,
            GameApi.Live(),
            new AuditWorld(
                Array.Empty<DeliveryStop>(),
                PlantedHolds(),
                needs,
                Array.Empty<DailyUnits>(),
                Array.Empty<GarageSlot>(),
                Array.Empty<string>(),
                Array.Empty<WarehouseLevel>(),
                pricingPlan: true,
                Array.Empty<HqSeat>(),
                Array.Empty<HiredHand>(),
                Array.Empty<string>(),
                Array.Empty<string>()),
            LiveGuard(),
            heal =>
            {
                calls++;
                return HealFate.Failed;
            });

        Assert.Equal(Audit.HealStrikeMax, calls);
        Assert.DoesNotContain(report.Lines, line => line.StartsWith("audit.healed "));
        Assert.Contains(
            "autopilot.paused version=" + LoadLog.Version + " day=1016 reason=failures",
            report.Lines);
        var summary = Assert.Single(report.Lines, line => line.StartsWith("audit.summary "));
        Assert.Contains("unconnected=4", summary);
        Assert.Contains("healed=0", summary);
    }

    [Fact]
    public void Audit_f10_logs_the_counts_and_does_not_heal()
    {
        using var dir = new TempAudit();
        var called = false;
        var report = Audit.Check(
            dir.Path,
            1016,
            GameApi.Live(),
            PlantedStore(),
            new AuditGuard(enabled: false, dryRun: false, cash: 1000m, dailyCost: 10m, truckCost: 1, build: LoadLog.PinnedBuild),
            heal =>
            {
                called = true;
                return HealFate.Done;
            });

        Assert.False(called);
        Assert.DoesNotContain(report.Lines, line => line.StartsWith("audit.healed "));
        Assert.False(File.Exists(SupplyNight.SnapshotPath(dir.Path)));
        var summary = Assert.Single(report.Lines);
        Assert.Contains("unconnected=1", summary);
        Assert.Contains("healed=0", summary);
    }

    [Fact]
    public void Audit_a_pin_mismatch_halts_and_does_not_heal()
    {
        using var dir = new TempAudit();
        var called = false;
        var report = Audit.Check(
            dir.Path,
            1016,
            GameApi.Live(),
            PlantedStore(),
            new AuditGuard(enabled: true, dryRun: false, cash: 1000m, dailyCost: 10m, truckCost: 1, build: "1"),
            heal =>
            {
                called = true;
                return HealFate.Done;
            });

        Assert.False(called);
        Assert.Contains(
            "autopilot.halted version=" + LoadLog.Version + " day=1016 reason=pin",
            report.Lines);
        var summary = Assert.Single(report.Lines, line => line.StartsWith("audit.summary "));
        Assert.Contains("pinMismatch=1", summary);
        Assert.Contains("unconnected=1", summary);
        Assert.Contains("healed=0", summary);
    }

    [Fact]
    public void Audit_a_store_wins_when_the_truck_does_not_fit_the_day_spend()
    {
        using var dir = new TempAudit();
        var api = GameApi.Live();
        var calls = new List<string>();
        var report = Audit.Check(
            dir.Path,
            1016,
            api,
            new AuditWorld(
                Array.Empty<DeliveryStop>(),
                PlantedHolds(),
                PlantedNeeds(),
                Array.Empty<DailyUnits>(),
                new[] { new GarageSlot("W7", 1, filled: false) },
                Array.Empty<string>(),
                Array.Empty<WarehouseLevel>(),
                pricingPlan: true,
                Array.Empty<HqSeat>(),
                Array.Empty<HiredHand>(),
                Array.Empty<string>(),
                Array.Empty<string>()),
            new AuditGuard(enabled: true, dryRun: false, cash: 100m, dailyCost: 10m, truckCost: 100, build: LoadLog.PinnedBuild),
            heal =>
            {
                calls.Add(heal.Kind);
                return HealFate.Done;
            });

        Assert.Equal(new[] { "connect" }, calls);
        Assert.Contains(report.Lines, line => line.Contains("action=connect") && line.Contains(" store=" + Salon + " "));
        Assert.DoesNotContain(report.Lines, line => line.Contains("action=slot"));
        var summary = Assert.Single(report.Lines, line => line.StartsWith("audit.summary "));
        Assert.Contains("unconnected=0", summary);
        Assert.Contains("emptySlots=1", summary);
        Assert.Contains("healed=1", summary);
    }

    [Fact]
    public void Audit_a_connect_without_a_save_is_not_the_heal()
    {
        using var dir = new TempAudit();
        var report = Audit.Check(
            dir.Path,
            1016,
            GameApi.Live(),
            PlantedStore(),
            LiveGuard(),
            heal => AuditBridge.Live(heal) ? HealFate.Done : HealFate.Failed);

        Assert.DoesNotContain(report.Lines, line => line.StartsWith("audit.healed "));
        var summary = Assert.Single(report.Lines, line => line.StartsWith("audit.summary "));
        Assert.Contains("unconnected=1", summary);
        Assert.Contains("healed=0", summary);
        Assert.DoesNotContain(report.Lines, line => line.StartsWith("autopilot.paused "));
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_heals_the_unconnected_store()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var calls = new List<string>();
        var report = Audit.Check(
            dir.Path,
            1016,
            prefix.Api,
            prefix.World,
            LiveGuard(),
            heal =>
            {
                calls.Add(heal.Call);
                return HealFate.Done;
            });

        Assert.Equal(Needs.Full, prefix.Refill.Energy);
        Assert.Contains("day=1016", prefix.Rolled);
        Assert.Contains(prefix.Api.Call("delivery"), calls);
        var connected = Assert.Single(prefix.Connected, line => line.Contains(" store=" + Salon + " "));
        var heal = Assert.Single(report.Lines, line => line.StartsWith("audit.healed ") && line.Contains(" store=" + Salon + " "));
        Assert.Contains("action=connect", heal);
        Assert.Contains("call=" + prefix.Api.Call("delivery"), heal);
        Assert.Contains("dry=false", heal);
        Assert.Contains(Salon, connected);
        var summary = Assert.Single(report.Lines, line => line.StartsWith("audit.summary "));
        Assert.Contains("unconnected=0", summary);
        foreach (var drain in prefix.Drains)
        {
            Assert.Contains(
                report.Lines,
                line => line.StartsWith("audit.healed ")
                    && line.Contains("action=drain")
                    && line.Contains(" family=" + drain.Family + " ")
                    && line.Contains(" from=" + drain.From + " "));
        }

        var log = File.ReadAllLines(LoadLog.CeoLogPath(dir.Path));
        var orderAt = -1;
        for (var index = 0; index < log.Length; index++)
        {
            if (log[index].StartsWith("restock.ordered ", StringComparison.Ordinal))
            {
                orderAt = index;
            }
        }

        Assert.True(orderAt >= 0);
        Assert.True(Array.IndexOf(log, heal) > orderAt);
        var shot = File.ReadAllText(SupplyNight.SnapshotPath(dir.Path));
        Assert.Contains("snapshot.taken version=" + LoadLog.Version, shot);
        Assert.DoesNotContain(Salon, shot);
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_leaves_the_healed_store_connected()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → clear
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var first = Audit.Check(dir.Path, 1016, prefix.Api, prefix.World, LiveGuard(), heal => HealFate.Done);
        var world = prefix.World.WithStops(first.Stops);
        var again = Audit.Check(dir.Path, 1017, prefix.Api, world, LiveGuard(), heal => HealFate.Done);

        Assert.Equal(0, Audit.Unconnected(first.Stops, prefix.World.Holds, prefix.World.Needs, prefix.World.Sales));
        Assert.DoesNotContain(again.Lines, line => line.StartsWith("audit.healed ") && line.Contains(" store=" + Salon + " "));
        var summary = Assert.Single(again.Lines, line => line.StartsWith("audit.summary "));
        Assert.Contains("unconnected=0", summary);
        var log = File.ReadAllLines(LoadLog.CeoLogPath(dir.Path));
        Assert.Single(log, line => line.StartsWith("audit.healed ") && line.Contains(" store=" + Salon + " "));
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_names_the_run()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var report = Audit.Check(dir.Path, 1016, prefix.Api, prefix.World, guard, heal => HealFate.Done);
        var stage = Stage.Read(TopBar.Day(prefix.Rolled), guard.Cash, prefix.World.Needs, report.Stops, prefix.World.Holds, prefix.World.Slots);
        var line = TopBar.From(
            prefix.Rolled,
            File.ReadAllText(StatementDump.CsvPath(dir.Path)),
            guard.Cash,
            0m,
            stage,
            guard.Enabled,
            TopBar.Clock(paused: false, rate: null));

        Assert.Contains("day=1016", prefix.Rolled);
        Assert.Contains("audit.summary ", string.Join("\n", report.Lines));
        Assert.Equal(1, stage);
        Assert.Equal(
            "Day 1016  Cash 1000  Net worth 0 / 100000000  Profit 7  Stage 1  Autopilot on  Speed 0",
            line);
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_stays_the_first_line()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → window
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var report = Audit.Check(dir.Path, 1016, prefix.Api, prefix.World, guard, heal => HealFate.Done);
        var stage = Stage.Read(TopBar.Day(prefix.Rolled), guard.Cash, prefix.World.Needs, report.Stops, prefix.World.Holds, prefix.World.Slots);
        var line = TopBar.From(
            prefix.Rolled,
            File.ReadAllText(StatementDump.CsvPath(dir.Path)),
            guard.Cash,
            0m,
            stage,
            guard.Enabled,
            TopBar.Clock(paused: false, rate: null));
        var summary = Assert.Single(report.Lines, row => row.StartsWith("audit.summary "));
        var frame = TopBar.Frame(line, new[] { summary });
        var shown = TopBar.Record(dir.Path, line);
        var again = TopBar.Record(dir.Path, line);

        Assert.Equal(line, frame[0]);
        Assert.Equal(summary, frame[1]);
        Assert.Equal(shown, again);
        Assert.StartsWith("bar.shown version=" + LoadLog.Version + " Day 1016", shown);
        var log = File.ReadAllLines(LoadLog.CeoLogPath(dir.Path));
        Assert.Single(log, row => row.StartsWith("bar.shown "));
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_says_off_when_the_guard_is_off()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → off
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = new AuditGuard(enabled: false, dryRun: false, cash: 1000m, dailyCost: 10m, truckCost: 1, build: LoadLog.PinnedBuild);
        var report = Audit.Check(dir.Path, 1016, prefix.Api, prefix.World, guard, heal => HealFate.Done);
        var stage = Stage.Read(TopBar.Day(prefix.Rolled), guard.Cash, prefix.World.Needs, report.Stops, prefix.World.Holds, prefix.World.Slots);
        var line = TopBar.From(
            prefix.Rolled,
            File.ReadAllText(StatementDump.CsvPath(dir.Path)),
            guard.Cash,
            0m,
            stage,
            guard.Enabled,
            TopBar.Clock(paused: false, rate: null));
        var summary = Assert.Single(report.Lines, row => row.StartsWith("audit.summary "));

        Assert.Contains("healed=0", summary);
        Assert.Contains("Autopilot off", line);
        Assert.DoesNotContain("Autopilot on", line);
        Assert.Contains("Day 1016", line);
        Assert.Contains("Profit 7", line);
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_locks_the_summary_counts()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var report = Audit.Check(dir.Path, 1016, prefix.Api, prefix.World, guard, heal => HealFate.Done);
        var stage = Stage.Read(TopBar.Day(prefix.Rolled), guard.Cash, prefix.World.Needs, report.Stops, prefix.World.Holds, prefix.World.Slots);
        var bar = TopBar.From(
            prefix.Rolled,
            File.ReadAllText(StatementDump.CsvPath(dir.Path)),
            guard.Cash,
            0m,
            stage,
            guard.Enabled,
            TopBar.Clock(paused: false, rate: null));
        var summary = Assert.Single(report.Lines, row => row.StartsWith("audit.summary "));
        var health = Health.Latest(dir.Path);

        Assert.Contains("Day 1016", bar);
        Assert.Contains("audit.summary ", summary);
        Assert.Contains("Stores supplied unconnected=" + Token(summary, "unconnected"), health);
        Assert.Contains("Slots filled emptySlots=" + Token(summary, "emptySlots"), health);
        Assert.Contains("Plans present missingPlans=" + Token(summary, "missingPlans"), health);
        Assert.Contains("Last heal healed=" + Token(summary, "healed"), health);
        if (Token(summary, "healed") == "0")
        {
            Assert.Contains("Last heal healed=0 none", health);
        }
        else
        {
            var last = report.Lines.Last(row => row.StartsWith("audit.healed "));
            Assert.Contains(Tail(last), health);
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_quiet_day_drops_the_older_heal()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → quiet
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var first = Audit.Check(dir.Path, 1016, prefix.Api, prefix.World, guard, heal => HealFate.Done);
        var off = new AuditGuard(enabled: false, dryRun: false, cash: 1000m, dailyCost: 10m, truckCost: 1, build: LoadLog.PinnedBuild);
        var second = Audit.Check(dir.Path, 1017, prefix.Api, prefix.World.WithStops(first.Stops), off, heal => HealFate.Done);
        var stage = Stage.Read(TopBar.Day(prefix.Rolled), guard.Cash, prefix.World.Needs, first.Stops, prefix.World.Holds, prefix.World.Slots);
        var bar = TopBar.From(
            prefix.Rolled,
            File.ReadAllText(StatementDump.CsvPath(dir.Path)),
            guard.Cash,
            0m,
            stage,
            guard.Enabled,
            TopBar.Clock(paused: false, rate: null));
        var summary = Assert.Single(second.Lines, row => row.StartsWith("audit.summary "));
        var health = Health.Latest(dir.Path);
        var frame = TopBar.Frame(bar, new[] { health });
        var shown = Health.Record(dir.Path, health);
        var again = Health.Record(dir.Path, health);

        Assert.Equal(bar, frame[0]);
        Assert.Equal(health, frame[1]);
        Assert.StartsWith("Stores supplied ", frame[1]);
        Assert.Contains("healed=0", summary);
        Assert.Contains("Stores supplied unconnected=" + Token(summary, "unconnected"), health);
        Assert.Contains("Slots filled emptySlots=" + Token(summary, "emptySlots"), health);
        Assert.Contains("Plans present missingPlans=" + Token(summary, "missingPlans"), health);
        Assert.Equal(
            "Last heal healed=0 none",
            health.Substring(health.LastIndexOf("Last heal ", StringComparison.Ordinal)));
        Assert.DoesNotContain("action=", health);
        Assert.Equal(shown, again);
        Assert.Equal(health, Health.Latest(dir.Path));
        Assert.StartsWith("health.shown version=" + LoadLog.Version + " Stores supplied ", shown);
        var log = File.ReadAllLines(LoadLog.CeoLogPath(dir.Path));
        Assert.Single(log, row => row.StartsWith("health.shown "));
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_locks_the_toggle_f10_and_execute_all()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var report = Audit.Check(dir.Path, 1016, prefix.Api, prefix.World, guard, heal => HealFate.Done);
        var stage = Stage.Read(TopBar.Day(prefix.Rolled), guard.Cash, prefix.World.Needs, report.Stops, prefix.World.Holds, prefix.World.Slots);
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var csv = File.ReadAllText(StatementDump.CsvPath(dir.Path));
            var bar = TopBar.From(
                prefix.Rolled,
                csv,
                guard.Cash,
                0m,
                stage,
                Autopilot.Enabled,
                TopBar.Clock(paused: false, rate: null));
            var summary = Assert.Single(report.Lines, row => row.StartsWith("audit.summary "));
            var health = Health.Latest(dir.Path);
            var row = Controls.Row(Autopilot.Enabled, Autopilot.DryRun);
            var frame = TopBar.Frame(bar, new[] { health, row });
            var shown = Controls.Record(dir.Path, row);
            var again = Controls.Record(dir.Path, row);
            var toggled = Controls.Toggle();
            var offBar = TopBar.From(
                prefix.Rolled,
                csv,
                guard.Cash,
                0m,
                stage,
                toggled,
                TopBar.Clock(paused: false, rate: null));
            var restored = Controls.Toggle();
            var paused = Controls.Pause();
            var pausedBar = TopBar.From(
                prefix.Rolled,
                csv,
                guard.Cash,
                0m,
                stage,
                paused,
                TopBar.Clock(paused: false, rate: null));

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Contains("audit.summary ", summary);
            Assert.Contains("Stores supplied ", health);
            Assert.Equal(bar, frame[0]);
            Assert.Equal(health, frame[1]);
            Assert.Equal(row, frame[2]);
            Assert.Contains("Autopilot on", bar);
            Assert.False(toggled);
            Assert.Contains("Autopilot off", offBar);
            Assert.DoesNotContain("Autopilot on", offBar);
            Assert.True(restored);
            Assert.False(paused);
            Assert.False(Autopilot.Enabled);
            Assert.Contains("Autopilot off", pausedBar);
            Assert.DoesNotContain("Autopilot on", pausedBar);
            Assert.Equal("Execute all", Controls.ExecuteAll);
            Assert.Equal("7.12", Controls.ExecuteAllStory);
            Assert.EndsWith("Execute all", row);
            Assert.Equal("action=execute-all story=7.12", Controls.PressAll());
            Assert.Equal(shown, again);
            Assert.StartsWith("controls.shown version=" + LoadLog.Version + " Autopilot on", shown);
            Assert.Contains("Execute all", shown);
            var log = File.ReadAllLines(LoadLog.CeoLogPath(dir.Path));
            Assert.Single(log, line => line.StartsWith("controls.shown "));
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_dry_run_leaves_the_bar_on()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → dry
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var report = Audit.Check(dir.Path, 1016, prefix.Api, prefix.World, guard, heal => HealFate.Done);
        var stage = Stage.Read(TopBar.Day(prefix.Rolled), guard.Cash, prefix.World.Needs, report.Stops, prefix.World.Holds, prefix.World.Slots);
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var csv = File.ReadAllText(StatementDump.CsvPath(dir.Path));
            var health = Health.Latest(dir.Path);
            var dry = Controls.Dry();
            var bar = TopBar.From(
                prefix.Rolled,
                csv,
                guard.Cash,
                0m,
                stage,
                Autopilot.Enabled,
                TopBar.Clock(paused: false, rate: null));
            var row = Controls.Row(Autopilot.Enabled, dry);
            var frame = TopBar.Frame(bar, new[] { health, row });

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Contains(report.Lines, line => line.StartsWith("audit.summary "));
            Assert.Contains("Stores supplied ", health);
            Assert.True(dry);
            Assert.True(Autopilot.DryRun);
            Assert.True(Autopilot.Enabled);
            Assert.Contains("Autopilot on", bar);
            Assert.DoesNotContain("Autopilot off", bar);
            Assert.Contains("Dry run on", row);
            Assert.EndsWith("Execute all", row);
            Assert.Equal(bar, frame[0]);
            Assert.Equal(health, frame[1]);
            Assert.Equal(row, frame[2]);
            Assert.Equal("Dry run on", Controls.Buttons(Autopilot.Enabled, Autopilot.DryRun)[1]);
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_locks_three_days_as_three_rows()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var first = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var second = WatchDay(dir.Path, ref world, guard, prefix.Api, first.Rolled, 1017, 20m, 1016);
            var third = WatchDay(dir.Path, ref world, guard, prefix.Api, second.Rolled, 1018, 30m, 1017);
            var lines = File.ReadAllLines(Telemetry.CsvPath(dir.Path));
            var rows = new[] { lines[1].Split(','), lines[2].Split(','), lines[3].Split(',') };
            var shots = new[] { first, second, third };

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Equal(4, lines.Length);
            Assert.Equal(new[] { "1016", "1017", "1018" }, rows.Select(cells => cells[0]).ToArray());
            Assert.Equal(new[] { "10", "20", "30" }, rows.Select(cells => cells[2]).ToArray());
            for (var index = 0; index < shots.Length; index++)
            {
                Assert.Equal(Between(shots[index].Bar, "Day "), rows[index][0]);
                Assert.Equal(Between(shots[index].Bar, "Cash "), rows[index][1]);
                Assert.Equal(Net(shots[index].Bar), rows[index][2]);
                Assert.Equal(Between(shots[index].Bar, "Profit "), rows[index][3]);
                Assert.Equal(Between(shots[index].Bar, "Stage "), rows[index][4]);
                Assert.Equal(Token(shots[index].Health, "unconnected"), rows[index][5]);
                Assert.Equal(Token(shots[index].Health, "emptySlots"), rows[index][6]);
                Assert.Equal(Token(shots[index].Health, "missingPlans"), rows[index][10]);
                Assert.Equal(Token(shots[index].Health, "healed"), rows[index][13]);
                var summary = shots[index].Report.Lines.Last(line => line.StartsWith("audit.summary ", StringComparison.Ordinal));
                Assert.Equal(Token(summary, "dry"), rows[index][7]);
                Assert.Equal(Token(summary, "pinMismatch"), rows[index][12]);
                Assert.Equal(JoinedActions(shots[index].Report.Lines), rows[index][14]);
                Assert.Contains("day=" + rows[index][0], shots[index].Written);
                Assert.Contains("rows=" + (index + 1).ToString(), shots[index].Written);
                Assert.Contains("autopilot=on", shots[index].Written);
                Assert.Contains("dry=off", shots[index].Written);
            }

            Assert.Equal(3, File.ReadAllLines(LoadLog.CeoLogPath(dir.Path)).Count(line => line.StartsWith("telemetry.written ")));
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_same_day_stays_one_row()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → same day
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var first = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var second = WatchDay(dir.Path, ref world, guard, prefix.Api, first.Rolled, 1017, 20m, 1016);
            var third = WatchDay(dir.Path, ref world, guard, prefix.Api, second.Rolled, 1018, 30m, 1017);
            var kept = File.ReadAllLines(Telemetry.CsvPath(dir.Path))[1];
            var againBar = TopBar.Line(
                int.Parse(Between(third.Bar, "Day "), CultureInfo.InvariantCulture),
                decimal.Parse(Between(third.Bar, "Cash "), CultureInfo.InvariantCulture),
                99m,
                decimal.Parse(Between(third.Bar, "Profit "), CultureInfo.InvariantCulture),
                int.Parse(Between(third.Bar, "Stage "), CultureInfo.InvariantCulture),
                Autopilot.Enabled,
                TopBar.Clock(paused: false, rate: null));
            var again = Telemetry.Record(dir.Path, againBar, third.Health, third.Controls);
            var lines = File.ReadAllLines(Telemetry.CsvPath(dir.Path));

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Equal("", again);
            Assert.Equal(4, lines.Length);
            Assert.Equal(kept, lines[1]);
            Assert.Equal("99", lines[3].Split(',')[2]);
            Assert.Equal("1018", lines[3].Split(',')[0]);
            Assert.Equal(JoinedActions(first.Report.Lines), lines[1].Split(',')[14]);
            Assert.Equal(3, File.ReadAllLines(LoadLog.CeoLogPath(dir.Path)).Count(line => line.StartsWith("telemetry.written ")));
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_locks_the_cheaper_lot()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var chosen = Rent.Choose(
                new[]
                {
                    new RentLot("ba:street_high", 2, 80m),
                    new RentLot("ba:street_low", 9, 40m),
                },
                guard.Cash,
                anyRented: false,
                prefix.Api);
            var written = Rent.Record(dir.Path, chosen, TopBar.Day(prefix.Rolled));
            var again = Rent.Record(dir.Path, chosen, TopBar.Day(prefix.Rolled));

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Contains("telemetry.written ", shot.Written);
            Assert.True(chosen.Rented);
            Assert.Equal("ba:street_low", chosen.Street);
            Assert.Equal(9, chosen.Number);
            Assert.Equal("Buildings.BuildingCommands.Command_RentBuilding", prefix.Api.Call("rent"));
            Assert.Equal(prefix.Api.Call("rent"), chosen.Call);
            Assert.Equal(
                "building.rented version=" + LoadLog.Version
                    + " day=1016 street=ba:street_low number=9 call=" + prefix.Api.Call("rent"),
                written);
            Assert.Equal(written, again);
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.Path)).Count(line => line.StartsWith("building.rented ")));
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_skips_a_save_that_already_rents()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → kept
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var chosen = Rent.Choose(
                new[] { new RentLot("ba:street_low", 9, 40m) },
                guard.Cash,
                anyRented: true,
                prefix.Api);
            var written = Rent.Record(dir.Path, chosen, TopBar.Day(shot.Rolled));

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Contains("telemetry.written ", shot.Written);
            Assert.False(chosen.Rented);
            Assert.Equal("", chosen.Street);
            Assert.Equal(0, chosen.Number);
            Assert.Equal("", written);
            Assert.DoesNotContain(
                File.ReadAllLines(LoadLog.CeoLogPath(dir.Path)),
                line => line.StartsWith("building.rented "));
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_layout_locks_the_gift_shop()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → layout
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var rented = Rent.Choose(
                new[]
                {
                    new RentLot("ba:street_high", 2, 80m),
                    new RentLot("ba:street_low", 9, 40m),
                },
                guard.Cash,
                anyRented: false,
                prefix.Api);
            var rentLine = Rent.Record(dir.Path, rented, TopBar.Day(prefix.Rolled));
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, prefix.Api);
            var written = Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));
            var again = Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Contains("telemetry.written ", shot.Written);
            Assert.Contains("building.rented ", rentLine);
            Assert.Equal("ba:street_low", rented.Street);
            Assert.Equal(9, rented.Number);
            Assert.True(chosen.Loaded);
            Assert.Equal("ba:businesstype_giftshop", chosen.BusinessType);
            Assert.Equal("GiftShopWholesalers", chosen.Layout);
            Assert.Equal("BusinessLayoutSets.BusinessLayoutSetHelper.LoadBusinessLayoutSet", prefix.Api.Call("clone"));
            Assert.Equal(prefix.Api.Call("clone"), chosen.Call);
            Assert.Equal(
                "layout.loaded version=" + LoadLog.Version
                    + " day=1016 street=ba:street_low number=9 type=ba:businesstype_giftshop layout=GiftShopWholesalers open=8-16 call="
                    + prefix.Api.Call("clone"),
                written);
            Assert.Equal(written, again);
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.Path)).Count(line => line.StartsWith("layout.loaded ")));
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_layout_skips_a_store_that_is_already_open()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → layout → kept
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var rented = Rent.Choose(
                new[] { new RentLot("ba:street_low", 9, 40m) },
                guard.Cash,
                anyRented: true,
                prefix.Api);
            var rentLine = Rent.Record(dir.Path, rented, TopBar.Day(shot.Rolled));
            var chosen = Starter.Choose(
                new[] { new StarterOffer("ba:businesstype_giftshop", "GiftShopWholesalers", 40m) },
                alreadyOpen: true,
                prefix.Api);
            var written = Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Contains("telemetry.written ", shot.Written);
            Assert.Equal("", rentLine);
            Assert.False(rented.Rented);
            Assert.False(chosen.Loaded);
            Assert.Equal("", chosen.BusinessType);
            Assert.Equal("", written);
            Assert.DoesNotContain(
                File.ReadAllLines(LoadLog.CeoLogPath(dir.Path)),
                line => line.StartsWith("layout.loaded "));
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_layout_ops_names_the_cleaner_and_the_guard()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → layout → ops
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var rented = Rent.Choose(
                new[]
                {
                    new RentLot("ba:street_high", 2, 80m),
                    new RentLot("ba:street_low", 9, 40m),
                },
                guard.Cash,
                anyRented: false,
                prefix.Api);
            var rentLine = Rent.Record(dir.Path, rented, TopBar.Day(prefix.Rolled));
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, prefix.Api);
            var layoutLine = Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));
            var assigned = StoreOps.Assign(chosen, StoreOps.Opening(), prefix.Api);
            var written = StoreOps.Record(dir.Path, assigned, rented, TopBar.Day(shot.Rolled));
            var again = StoreOps.Record(dir.Path, assigned, rented, TopBar.Day(shot.Rolled));

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Contains("telemetry.written ", shot.Written);
            Assert.Contains("building.rented ", rentLine);
            Assert.Contains("layout.loaded ", layoutLine);
            Assert.Equal("ba:street_low", rented.Street);
            Assert.Equal(9, rented.Number);
            Assert.Equal("GiftShopWholesalers", chosen.Layout);
            Assert.True(assigned.Assigned);
            Assert.Equal("Cleaner", assigned.Cleaner);
            Assert.Equal("Guard", assigned.Guard);
            Assert.False(assigned.Uniforms);
            Assert.Equal("CleaningStationController.AssignEmployee", prefix.Api.Call("cleaner"));
            Assert.Equal(prefix.Api.Call("cleaner"), assigned.CleanerCall);
            Assert.Equal(prefix.Api.Call("guard"), assigned.GuardCall);
            Assert.Equal("", assigned.UniformsCall);
            Assert.Equal(
                "ops.assigned version=" + LoadLog.Version
                    + " day=1016 street=ba:street_low number=9 cleaner=Cleaner guard=Guard uniforms=false"
                    + " cleanerCall=" + prefix.Api.Call("cleaner")
                    + " guardCall=" + prefix.Api.Call("guard")
                    + " uniformsCall=",
                written);
            Assert.Equal(written, again);
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.Path)).Count(line => line.StartsWith("ops.assigned ")));
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_layout_ops_capacity_locks_the_update()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → layout → ops → capacity
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var rented = Rent.Choose(
                new[]
                {
                    new RentLot("ba:street_high", 2, 80m),
                    new RentLot("ba:street_low", 9, 40m),
                },
                guard.Cash,
                anyRented: false,
                prefix.Api);
            var rentLine = Rent.Record(dir.Path, rented, TopBar.Day(prefix.Rolled));
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, prefix.Api);
            var layoutLine = Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));
            var assigned = StoreOps.Assign(chosen, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), prefix.Api);
            var opsLine = StoreOps.Record(dir.Path, assigned, rented, TopBar.Day(shot.Rolled));
            var matched = Capacity.Match(rented, chosen, alreadyMatched: false, prefix.Api);
            var capacityLine = Capacity.Record(dir.Path, matched, TopBar.Day(shot.Rolled));

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Contains("telemetry.written ", shot.Written);
            Assert.Contains("building.rented ", rentLine);
            Assert.Contains("layout.loaded ", layoutLine);
            Assert.Contains("ops.assigned ", opsLine);
            Assert.True(assigned.Assigned);
            Assert.Equal("Cleaner", assigned.Cleaner);
            Assert.Equal("", assigned.Guard);
            Assert.True(matched.Matched);
            Assert.Equal(prefix.Api.Call("capacity fixtures"), matched.Call);
            Assert.Equal("Helpers.BusinessHelper.UpdateCustomerCapacity", matched.Call);
            Assert.Equal(rented.Street, matched.Street);
            Assert.Equal(rented.Number, matched.Number);
            Assert.Equal(
                "capacity.matched version=" + LoadLog.Version
                    + " day=1016 street=" + rented.Street
                    + " number=" + rented.Number.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " call=" + matched.Call,
                capacityLine);
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.Path)).Count(line => line.StartsWith("capacity.matched ")));
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_layout_ops_capacity_skips_a_second_match()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → layout → ops → capacity → kept
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var rented = Rent.Choose(
                new[] { new RentLot("ba:street_low", 9, 40m) },
                guard.Cash,
                anyRented: false,
                prefix.Api);
            var rentLine = Rent.Record(dir.Path, rented, TopBar.Day(prefix.Rolled));
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, prefix.Api);
            var layoutLine = Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));
            var assigned = StoreOps.Assign(chosen, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), prefix.Api);
            var opsLine = StoreOps.Record(dir.Path, assigned, rented, TopBar.Day(shot.Rolled));
            var first = Capacity.Match(rented, chosen, alreadyMatched: false, prefix.Api);
            var firstLine = Capacity.Record(dir.Path, first, TopBar.Day(shot.Rolled));
            var again = Capacity.Match(rented, chosen, alreadyMatched: true, prefix.Api);
            var written = Capacity.Record(dir.Path, again, TopBar.Day(shot.Rolled));

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Contains("telemetry.written ", shot.Written);
            Assert.Contains("building.rented ", rentLine);
            Assert.Contains("layout.loaded ", layoutLine);
            Assert.Contains("ops.assigned ", opsLine);
            Assert.True(first.Matched);
            Assert.Contains("capacity.matched ", firstLine);
            Assert.False(again.Matched);
            Assert.Equal("", again.Call);
            Assert.Equal("", written);
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.Path)).Count(line => line.StartsWith("capacity.matched ")));
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_layout_ops_a_gift_shop_without_a_guard_locker_keeps_the_guard_off()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → layout → ops → wholesaler
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var rented = Rent.Choose(
                new[]
                {
                    new RentLot("ba:street_high", 2, 80m),
                    new RentLot("ba:street_low", 9, 40m),
                },
                guard.Cash,
                anyRented: false,
                prefix.Api);
            var rentLine = Rent.Record(dir.Path, rented, TopBar.Day(prefix.Rolled));
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, prefix.Api);
            var layoutLine = Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));
            var assigned = StoreOps.Assign(chosen, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), prefix.Api);
            var written = StoreOps.Record(dir.Path, assigned, rented, TopBar.Day(shot.Rolled));

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Contains("telemetry.written ", shot.Written);
            Assert.Contains("building.rented ", rentLine);
            Assert.Contains("layout.loaded ", layoutLine);
            Assert.Equal("GiftShopWholesalers", chosen.Layout);
            Assert.Equal("ba:street_low", rented.Street);
            Assert.Equal(9, rented.Number);
            Assert.True(assigned.Assigned);
            Assert.Equal("Cleaner", assigned.Cleaner);
            Assert.Equal("", assigned.Guard);
            Assert.False(assigned.Uniforms);
            Assert.Contains(
                "ops.assigned version=" + LoadLog.Version
                    + " day=1016 street=ba:street_low number=9 cleaner=Cleaner guard= uniforms=false",
                written);
            Assert.DoesNotContain("guard=Guard", written);
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_layout_ops_a_hire_without_a_person_spawns_one()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → layout → ops → person
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var rented = Rent.Choose(
                new[]
                {
                    new RentLot("ba:street_high", 2, 80m),
                    new RentLot("ba:street_low", 9, 40m),
                },
                guard.Cash,
                anyRented: false,
                prefix.Api);
            var rentLine = Rent.Record(dir.Path, rented, TopBar.Day(prefix.Rolled));
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, prefix.Api);
            var layoutLine = Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));
            var assigned = StoreOps.Assign(chosen, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), prefix.Api);
            var source = StoreOps.BodySource(assigned, stationHasPerson: false, recordHasPerson: false);
            var outcome = StoreOps.Outcome(assigned, source);
            var written = StoreOps.Record(dir.Path, assigned, rented, TopBar.Day(shot.Rolled));

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Contains("telemetry.written ", shot.Written);
            Assert.Contains("building.rented ", rentLine);
            Assert.Contains("layout.loaded ", layoutLine);
            Assert.Equal("GiftShopWholesalers", chosen.Layout);
            Assert.Equal("ba:street_low", rented.Street);
            Assert.Equal(9, rented.Number);
            Assert.Equal("Cleaner", assigned.Cleaner);
            Assert.Equal("", assigned.Guard);
            Assert.False(assigned.Uniforms);
            Assert.Equal("spawn", source);
            Assert.Equal("assigned", outcome);
            Assert.Equal(StoreOps.SpawnPrefab, "Characters/HumanDefinitionLow");
            Assert.Contains(
                "ops.assigned version=" + LoadLog.Version
                    + " day=1016 street=ba:street_low number=9 cleaner=Cleaner guard= uniforms=false",
                written);
            Assert.DoesNotContain("ops.missed", written);
            Assert.DoesNotContain("guard=Guard", written);
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_layout_ops_an_unstarted_station_uses_the_cleaner_type()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → layout → ops → person → type
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var rented = Rent.Choose(
                new[]
                {
                    new RentLot("ba:street_high", 2, 80m),
                    new RentLot("ba:street_low", 9, 40m),
                },
                guard.Cash,
                anyRented: false,
                prefix.Api);
            var rentLine = Rent.Record(dir.Path, rented, TopBar.Day(prefix.Rolled));
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, prefix.Api);
            var layoutLine = Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));
            var assigned = StoreOps.Assign(chosen, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), prefix.Api);
            var source = StoreOps.BodySource(assigned, stationHasPerson: false, recordHasPerson: false);
            var component = StoreOps.UnstartedType(assigned.Cleaner);
            var written = StoreOps.Record(dir.Path, assigned, rented, TopBar.Day(shot.Rolled));

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Contains("telemetry.written ", shot.Written);
            Assert.Contains("building.rented ", rentLine);
            Assert.Contains("layout.loaded ", layoutLine);
            Assert.Equal("GiftShopWholesalers", chosen.Layout);
            Assert.Equal("ba:street_low", rented.Street);
            Assert.Equal(9, rented.Number);
            Assert.Equal("spawn", source);
            Assert.Equal("CleanerEmployee", component);
            Assert.Equal("", StoreOps.UnstartedType(assigned.Guard));
            Assert.Contains(
                "ops.assigned version=" + LoadLog.Version
                    + " day=1016 street=ba:street_low number=9 cleaner=Cleaner guard= uniforms=false",
                written);
            Assert.DoesNotContain("reason=exception", written);
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_layout_ops_a_uniform_miss_keeps_the_cleaner_line()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → layout → ops → person → type → cleaner
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var rented = Rent.Choose(
                new[] { new RentLot("ba:street_low", 9, 40m) },
                guard.Cash,
                anyRented: false,
                prefix.Api);
            Rent.Record(dir.Path, rented, TopBar.Day(prefix.Rolled));
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, prefix.Api);
            Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));
            var assigned = StoreOps.Assign(chosen, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), prefix.Api);
            var source = StoreOps.BodySource(assigned, stationHasPerson: false, recordHasPerson: false);
            var cleaner = StoreOps.CleanerLine(
                dir.Path,
                rented,
                TopBar.Day(shot.Rolled),
                new StoreOpsCleaner("emp1", "Cleaner", "item7", true, true, StoreOps.UnstartedType(assigned.Cleaner)));
            var reason = StoreOps.UniformReason(locker: true, saveReady: true, presetId: "", dressed: false);
            var missed = StoreOps.Miss(dir.Path, assigned, rented, TopBar.Day(shot.Rolled), reason);
            var log = File.ReadAllLines(LoadLog.CeoLogPath(dir.Path));

            Assert.Equal("spawn", source);
            Assert.EndsWith(" employee=emp1 name=Cleaner station=item7 shift=true person=true type=CleanerEmployee placed=true", cleaner);
            Assert.False(StoreOps.Retry(reason));
            Assert.EndsWith(" reason=nopreset", missed);
            Assert.True(
                Array.FindIndex(log, line => line.StartsWith("ops.cleaner ")) < Array.FindIndex(log, line => line.StartsWith("ops.missed ")));
            Assert.DoesNotContain(log, line => line.StartsWith("ops.assigned "));
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_layout_ops_a_new_save_seeds_the_default_uniform()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → layout → ops → person → type → uniforms → verified
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var rented = Rent.Choose(
                new[] { new RentLot("ba:street_low", 9, 40m) },
                guard.Cash,
                anyRented: false,
                prefix.Api);
            Rent.Record(dir.Path, rented, TopBar.Day(prefix.Rolled));
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, prefix.Api);
            Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));
            var assigned = StoreOps.Assign(
                chosen,
                new StoreOpsFacts(1, 100m, 2m, true, true),
                StoreFixtures.GiftShopWholesalers(),
                prefix.Api);
            var bySkill = new Dictionary<string, string>();
            var preset = StoreOps.UniformPreset(new[] { "default1" }, "giftdefault");
            var seeded = StoreOps.SeedUniforms(
                bySkill,
                StoreOps.UniformSkills(new[] { "ba:skill_customerservice" }, new[] { "ba:skill_cleaning" }, StoreOps.CleanerSkill),
                preset);
            var written = StoreOps.Record(dir.Path, assigned, rented, TopBar.Day(shot.Rolled));
            var verified = StoreOps.Verify(
                dir.Path,
                assigned,
                rented,
                TopBar.Day(shot.Rolled),
                shift: true,
                person: true,
                type: StoreOps.UnstartedType(assigned.Cleaner),
                uniform: StoreOps.Dressed(bySkill, StoreOps.CleanerSkill) ? bySkill[StoreOps.CleanerSkill] : "");

            Assert.True(assigned.Uniforms);
            Assert.Equal(new[] { "ba:skill_cleaning", "ba:skill_customerservice" }, seeded);
            Assert.Contains(" cleaner=Cleaner guard= uniforms=true ", written);
            Assert.DoesNotContain("nopreset", written);
            Assert.EndsWith(" type=CleanerEmployee uniform=default1 kept=true", verified);
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_layout_ops_verified_keeps_the_cleaner()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → layout → ops → person → type → verified
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var rented = Rent.Choose(
                new[]
                {
                    new RentLot("ba:street_high", 2, 80m),
                    new RentLot("ba:street_low", 9, 40m),
                },
                guard.Cash,
                anyRented: false,
                prefix.Api);
            var rentLine = Rent.Record(dir.Path, rented, TopBar.Day(prefix.Rolled));
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, prefix.Api);
            var layoutLine = Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));
            var assigned = StoreOps.Assign(chosen, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), prefix.Api);
            var source = StoreOps.BodySource(assigned, stationHasPerson: false, recordHasPerson: false);
            var written = StoreOps.Record(dir.Path, assigned, rented, TopBar.Day(shot.Rolled));
            var watch = new StoreOpsWatch();
            watch.Mark(save);
            watch.Arm(rented.Street, rented.Number, TopBar.Day(shot.Rolled), StoreOps.VerifyFrames);
            var frames = 0;
            while (!watch.VerifyDue())
            {
                frames++;
            }

            var verified = StoreOps.Verify(
                dir.Path,
                assigned,
                new BuildingRent(watch.VerifyStreet, watch.VerifyNumber, true, ""),
                watch.VerifyDay,
                shift: true,
                person: true,
                type: StoreOps.UnstartedType(assigned.Cleaner),
                uniform: "p1");
            var log = File.ReadAllLines(LoadLog.CeoLogPath(dir.Path));

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Contains("telemetry.written ", shot.Written);
            Assert.Contains("building.rented ", rentLine);
            Assert.Contains("layout.loaded ", layoutLine);
            Assert.Equal("GiftShopWholesalers", chosen.Layout);
            Assert.Equal("spawn", source);
            Assert.False(watch.Due(save));
            Assert.Equal(StoreOps.VerifyFrames - 1, frames);
            Assert.Contains("ops.assigned ", written);
            Assert.Equal(
                "ops.verified version=" + LoadLog.Version
                    + " day=1016 street=ba:street_low number=9 cleaner=Cleaner shift=true person=true type=CleanerEmployee uniform=p1 kept=true",
                verified);
            Assert.True(
                Array.FindIndex(log, line => line.StartsWith("ops.assigned ")) < Array.FindIndex(log, line => line.StartsWith("ops.verified ")));
            Assert.False(watch.VerifyDue());
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_layout_ops_an_exact_theft_line_keeps_the_guard_off()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → layout → ops → line
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var rented = Rent.Choose(
                new[]
                {
                    new RentLot("ba:street_high", 2, 80m),
                    new RentLot("ba:street_low", 9, 40m),
                },
                guard.Cash,
                anyRented: false,
                prefix.Api);
            var rentLine = Rent.Record(dir.Path, rented, TopBar.Day(prefix.Rolled));
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, prefix.Api);
            var layoutLine = Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));
            var assigned = StoreOps.Assign(
                chosen,
                new StoreOpsFacts(dirtSpots: 2, sales: 100m, theft: 1m, uniformsDemanded: false, canMeetUniforms: true),
                prefix.Api);
            var written = StoreOps.Record(dir.Path, assigned, rented, TopBar.Day(shot.Rolled));

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Contains("telemetry.written ", shot.Written);
            Assert.Contains("building.rented ", rentLine);
            Assert.Contains("layout.loaded ", layoutLine);
            Assert.Equal("ba:street_low", rented.Street);
            Assert.Equal(9, rented.Number);
            Assert.Equal("GiftShopWholesalers", chosen.Layout);
            Assert.True(assigned.Assigned);
            Assert.Equal("Cleaner", assigned.Cleaner);
            Assert.Equal("", assigned.Guard);
            Assert.False(assigned.Uniforms);
            Assert.Contains(
                "ops.assigned version=" + LoadLog.Version
                    + " day=1016 street=ba:street_low number=9 cleaner=Cleaner guard= uniforms=false",
                written);
            Assert.DoesNotContain("guard=Guard", written);
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_layout_ops_skips_a_store_that_is_already_open()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → layout → ops → kept
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var rented = Rent.Choose(
                new[] { new RentLot("ba:street_low", 9, 40m) },
                guard.Cash,
                anyRented: true,
                prefix.Api);
            var rentLine = Rent.Record(dir.Path, rented, TopBar.Day(shot.Rolled));
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: true, prefix.Api);
            var layoutLine = Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));
            var assigned = StoreOps.Assign(chosen, StoreOps.Opening(), prefix.Api);
            var written = StoreOps.Record(dir.Path, assigned, rented, TopBar.Day(shot.Rolled));

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Contains("telemetry.written ", shot.Written);
            Assert.Equal("", rentLine);
            Assert.Equal("", layoutLine);
            Assert.False(chosen.Loaded);
            Assert.False(assigned.Assigned);
            Assert.Equal("", assigned.Cleaner);
            Assert.Equal("", assigned.Guard);
            Assert.False(assigned.Uniforms);
            Assert.Equal("", written);
            Assert.DoesNotContain(
                File.ReadAllLines(LoadLog.CeoLogPath(dir.Path)),
                line => line.StartsWith("ops.assigned "));
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_layout_ops_wholesaler_locks_the_contract()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → layout → ops → wholesaler
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var rented = Rent.Choose(
                new[]
                {
                    new RentLot("ba:street_high", 2, 80m),
                    new RentLot("ba:street_low", 9, 40m),
                },
                guard.Cash,
                anyRented: false,
                prefix.Api);
            var rentLine = Rent.Record(dir.Path, rented, TopBar.Day(prefix.Rolled));
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, prefix.Api);
            var layoutLine = Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));
            var assigned = StoreOps.Assign(chosen, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), prefix.Api);
            var opsLine = StoreOps.Record(dir.Path, assigned, rented, TopBar.Day(shot.Rolled));
            var signed = Wholesaler.Sign(rented, chosen, Wholesaler.StageOne(), alreadySigned: false, prefix.Api);
            var written = Wholesaler.Record(dir.Path, signed, TopBar.Day(shot.Rolled));
            var again = Wholesaler.Record(dir.Path, signed, TopBar.Day(shot.Rolled));

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Contains("telemetry.written ", shot.Written);
            Assert.Contains("building.rented ", rentLine);
            Assert.Contains("layout.loaded ", layoutLine);
            Assert.Contains("ops.assigned ", opsLine);
            Assert.Equal("ba:street_low", rented.Street);
            Assert.Equal(9, rented.Number);
            Assert.Equal("GiftShopWholesalers", chosen.Layout);
            Assert.Equal("Cleaner", assigned.Cleaner);
            Assert.True(signed.Signed);
            Assert.Equal("ba:street_firststreet 37", signed.Wholesale);
            Assert.Equal("ba:street_low", signed.Street);
            Assert.Equal(9, signed.Number);
            Assert.Equal("Dialogs.WholesaleStoreManagerDialog.StartContract", prefix.Api.Call("wholesaler contract"));
            Assert.Equal(prefix.Api.Call("wholesaler contract"), signed.Call);
            Assert.Equal(
                "contract.signed version=" + LoadLog.Version
                    + " day=1016 street=ba:street_low number=9 wholesale=ba:street_firststreet 37 call="
                    + prefix.Api.Call("wholesaler contract"),
                written);
            Assert.Equal(written, again);
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.Path)).Count(line => line.StartsWith("contract.signed ")));
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_layout_ops_wholesaler_skips_a_store_that_already_has_a_contract()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → layout → ops → wholesaler → kept
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var rented = Rent.Choose(
                new[] { new RentLot("ba:street_low", 9, 40m) },
                guard.Cash,
                anyRented: false,
                prefix.Api);
            var rentLine = Rent.Record(dir.Path, rented, TopBar.Day(prefix.Rolled));
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, prefix.Api);
            var layoutLine = Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));
            var assigned = StoreOps.Assign(chosen, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), prefix.Api);
            var opsLine = StoreOps.Record(dir.Path, assigned, rented, TopBar.Day(shot.Rolled));
            var signed = Wholesaler.Sign(rented, chosen, Wholesaler.StageOne(), alreadySigned: true, prefix.Api);
            var written = Wholesaler.Record(dir.Path, signed, TopBar.Day(shot.Rolled));

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Contains("telemetry.written ", shot.Written);
            Assert.Contains("building.rented ", rentLine);
            Assert.Contains("layout.loaded ", layoutLine);
            Assert.Contains("ops.assigned ", opsLine);
            Assert.Equal("ba:street_low", rented.Street);
            Assert.Equal(9, rented.Number);
            Assert.False(signed.Signed);
            Assert.Equal("", signed.Wholesale);
            Assert.Equal("", signed.Call);
            Assert.Equal("", written);
            Assert.DoesNotContain(
                File.ReadAllLines(LoadLog.CeoLogPath(dir.Path)),
                line => line.StartsWith("contract.signed "));
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_layout_ops_wholesaler_hire_locks_the_cashier()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → layout → ops → wholesaler → hire
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var rented = Rent.Choose(
                new[]
                {
                    new RentLot("ba:street_high", 2, 80m),
                    new RentLot("ba:street_low", 9, 40m),
                },
                guard.Cash,
                anyRented: false,
                prefix.Api);
            var rentLine = Rent.Record(dir.Path, rented, TopBar.Day(prefix.Rolled));
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, prefix.Api);
            var layoutLine = Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));
            var assigned = StoreOps.Assign(chosen, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), prefix.Api);
            var opsLine = StoreOps.Record(dir.Path, assigned, rented, TopBar.Day(shot.Rolled));
            var signed = Wholesaler.Sign(rented, chosen, Wholesaler.StageOne(), alreadySigned: false, prefix.Api);
            var contractLine = Wholesaler.Record(dir.Path, signed, TopBar.Day(shot.Rolled));
            var hired = Cashier.Hire(rented, chosen, signed, alreadyHired: false, prefix.Api);
            var written = Cashier.Record(dir.Path, hired, TopBar.Day(shot.Rolled));

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Contains("telemetry.written ", shot.Written);
            Assert.Contains("building.rented ", rentLine);
            Assert.Contains("layout.loaded ", layoutLine);
            Assert.Contains("ops.assigned ", opsLine);
            Assert.Contains("contract.signed ", contractLine);
            Assert.True(signed.Signed);
            Assert.True(hired.Hired);
            Assert.Equal("Cashier", hired.Role);
            Assert.Equal(Cashier.Skill, hired.Skill);
            Assert.Equal("SelfServiceEmployee", hired.Type);
            Assert.Equal("ba:street_low", hired.Street);
            Assert.Equal(9, hired.Number);
            Assert.Equal(prefix.Api.Call("hire"), hired.Call);
            Assert.Equal(prefix.Api.Call("cashier"), hired.StationCall);
            Assert.Equal(
                "hire.cashier version=" + LoadLog.Version
                    + " day=1016 street=ba:street_low number=9 role=Cashier skill=" + Cashier.Skill
                    + " type=SelfServiceEmployee call=" + prefix.Api.Call("hire")
                    + " stationCall=" + prefix.Api.Call("cashier"),
                written);
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.Path)).Count(line => line.StartsWith("hire.cashier ")));
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_layout_ops_wholesaler_hire_loan_demand_tax_locks_the_money_steps()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → layout → ops → wholesaler → hire → loan → demand → tax
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var rented = Rent.Choose(
                new[] { new RentLot("ba:street_low", 9, 40m) },
                guard.Cash,
                anyRented: false,
                prefix.Api);
            var rentLine = Rent.Record(dir.Path, rented, TopBar.Day(prefix.Rolled));
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, prefix.Api);
            var layoutLine = Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));
            var assigned = StoreOps.Assign(chosen, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), prefix.Api);
            var opsLine = StoreOps.Record(dir.Path, assigned, rented, TopBar.Day(shot.Rolled));
            var signed = Wholesaler.Sign(rented, chosen, Wholesaler.StageOne(), alreadySigned: false, prefix.Api);
            var contractLine = Wholesaler.Record(dir.Path, signed, TopBar.Day(shot.Rolled));
            var hired = Cashier.Hire(rented, chosen, signed, alreadyHired: false, prefix.Api);
            var hireLine = Cashier.Record(dir.Path, hired, TopBar.Day(shot.Rolled));
            var taken = BankLoan.Take(
                projectedDailyProfit: 200m,
                offers: new[] { new LoanOffer(2000m, 100), new LoanOffer(3000m, 101) },
                alreadyBorrowed: false,
                prefix.Api);
            var loanLine = BankLoan.RecordTake(dir.Path, taken, TopBar.Day(shot.Rolled));
            var cleared = StaffDemand.Clear(
                unmetDays: StaffDemand.DemandDays,
                demandName: "ba:jobdemand_healthinsurance",
                employeeId: "emp-1",
                prefix.Api);
            var demandLine = StaffDemand.Record(dir.Path, cleared, TopBar.Day(shot.Rolled));
            var paid = TaxBill.Pay(currentDue: 55m, backDue: 12m, prefix.Api);
            var taxLine = TaxBill.Record(dir.Path, paid, TopBar.Day(shot.Rolled));

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Contains("telemetry.written ", shot.Written);
            Assert.Contains("building.rented ", rentLine);
            Assert.Contains("layout.loaded ", layoutLine);
            Assert.Contains("ops.assigned ", opsLine);
            Assert.Contains("contract.signed ", contractLine);
            Assert.Contains("hire.cashier ", hireLine);
            Assert.True(hired.Hired);
            Assert.True(taken.Taken);
            Assert.Equal(2000m, taken.Amount);
            Assert.Equal(prefix.Api.Call("loan"), taken.Call);
            Assert.Contains("loan.taken ", loanLine);
            Assert.True(cleared.Cleared);
            Assert.Equal(prefix.Api.Call("employee demand"), cleared.Call);
            Assert.Contains("demand.cleared ", demandLine);
            Assert.True(paid.Paid);
            Assert.Equal(prefix.Api.Call("taxes pay"), paid.CurrentCall);
            Assert.Equal(prefix.Api.Call("taxes back"), paid.BackCall);
            Assert.Contains("tax.paid ", taxLine);
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_layout_ops_wholesaler_hire_loan_demand_tax_time_locks_the_day_skip()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → layout → ops → wholesaler → hire → loan → demand → tax → time
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var rented = Rent.Choose(
                new[] { new RentLot("ba:street_low", 9, 40m) },
                guard.Cash,
                anyRented: false,
                prefix.Api);
            var rentLine = Rent.Record(dir.Path, rented, TopBar.Day(prefix.Rolled));
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, prefix.Api);
            var layoutLine = Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));
            var assigned = StoreOps.Assign(chosen, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), prefix.Api);
            var opsLine = StoreOps.Record(dir.Path, assigned, rented, TopBar.Day(shot.Rolled));
            var signed = Wholesaler.Sign(rented, chosen, Wholesaler.StageOne(), alreadySigned: false, prefix.Api);
            var contractLine = Wholesaler.Record(dir.Path, signed, TopBar.Day(shot.Rolled));
            var hired = Cashier.Hire(rented, chosen, signed, alreadyHired: false, prefix.Api);
            var hireLine = Cashier.Record(dir.Path, hired, TopBar.Day(shot.Rolled));
            var taken = BankLoan.Take(
                projectedDailyProfit: 200m,
                offers: new[] { new LoanOffer(2000m, 100) },
                alreadyBorrowed: false,
                prefix.Api);
            var loanLine = BankLoan.RecordTake(dir.Path, taken, TopBar.Day(shot.Rolled));
            var cleared = StaffDemand.Clear(
                unmetDays: StaffDemand.DemandDays,
                demandName: "ba:jobdemand_healthinsurance",
                employeeId: "emp-1",
                prefix.Api);
            var demandLine = StaffDemand.Record(dir.Path, cleared, TopBar.Day(shot.Rolled));
            var paid = TaxBill.Pay(currentDue: 55m, backDue: 12m, prefix.Api);
            var taxLine = TaxBill.Record(dir.Path, paid, TopBar.Day(shot.Rolled));
            var low = new PlayerNeeds(18f, 40f, 40f, true, true);
            var care = TimeAdvance.Care(low, prefix.Api);
            var careLine = TimeAdvance.RecordCare(dir.Path, care, TopBar.Day(shot.Rolled));
            var safe = Needs.Hold();
            var advanced = TimeAdvance.Skip(safe, alreadyAdvanced: false, prefix.Api);
            var timeLine = TimeAdvance.RecordSkip(dir.Path, advanced, TopBar.Day(shot.Rolled));

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Contains("telemetry.written ", shot.Written);
            Assert.Contains("building.rented ", rentLine);
            Assert.Contains("layout.loaded ", layoutLine);
            Assert.Contains("ops.assigned ", opsLine);
            Assert.Contains("contract.signed ", contractLine);
            Assert.Contains("hire.cashier ", hireLine);
            Assert.Contains("loan.taken ", loanLine);
            Assert.Contains("demand.cleared ", demandLine);
            Assert.Contains("tax.paid ", taxLine);
            Assert.True(care.Due);
            Assert.True(care.Slept);
            Assert.False(care.Ate);
            Assert.Equal(prefix.Api.Call("sleep"), care.SleepCall);
            Assert.Contains("time.cared ", careLine);
            Assert.True(advanced.Advanced);
            Assert.Equal(TimeAdvance.DaySpan, advanced.Span);
            Assert.Equal(prefix.Api.Call("time advance"), advanced.Call);
            Assert.Contains("time.advanced ", timeLine);
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.Path)).Count(line => line.StartsWith("time.advanced ")));
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_layout_ops_wholesaler_hire_loan_demand_tax_time_eats_then_advances()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → layout → ops → wholesaler → hire → loan → demand → tax → time → food
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var rented = Rent.Choose(
                new[] { new RentLot("ba:street_low", 9, 40m) },
                guard.Cash,
                anyRented: false,
                prefix.Api);
            Rent.Record(dir.Path, rented, TopBar.Day(prefix.Rolled));
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, prefix.Api);
            Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));
            var assigned = StoreOps.Assign(chosen, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), prefix.Api);
            StoreOps.Record(dir.Path, assigned, rented, TopBar.Day(shot.Rolled));
            var signed = Wholesaler.Sign(rented, chosen, Wholesaler.StageOne(), alreadySigned: false, prefix.Api);
            Wholesaler.Record(dir.Path, signed, TopBar.Day(shot.Rolled));
            var hired = Cashier.Hire(rented, chosen, signed, alreadyHired: false, prefix.Api);
            Cashier.Record(dir.Path, hired, TopBar.Day(shot.Rolled));
            var taken = BankLoan.Take(
                projectedDailyProfit: 200m,
                offers: new[] { new LoanOffer(2000m, 100) },
                alreadyBorrowed: false,
                prefix.Api);
            BankLoan.RecordTake(dir.Path, taken, TopBar.Day(shot.Rolled));
            var cleared = StaffDemand.Clear(
                unmetDays: StaffDemand.DemandDays,
                demandName: "ba:jobdemand_healthinsurance",
                employeeId: "emp-1",
                prefix.Api);
            StaffDemand.Record(dir.Path, cleared, TopBar.Day(shot.Rolled));
            var paid = TaxBill.Pay(currentDue: 10m, backDue: 0m, prefix.Api);
            TaxBill.Record(dir.Path, paid, TopBar.Day(shot.Rolled));
            var hungry = new PlayerNeeds(90f, 15f, 90f, true, true);
            var care = TimeAdvance.Care(hungry, prefix.Api);
            var careLine = TimeAdvance.RecordCare(dir.Path, care, TopBar.Day(shot.Rolled));
            var advanced = TimeAdvance.Skip(Needs.Hold(), alreadyAdvanced: false, prefix.Api);
            var timeLine = TimeAdvance.RecordSkip(dir.Path, advanced, TopBar.Day(shot.Rolled));

            Assert.Contains("telemetry.written ", shot.Written);
            Assert.True(hired.Hired);
            Assert.True(taken.Taken);
            Assert.True(care.Due);
            Assert.False(care.Slept);
            Assert.True(care.Ate);
            Assert.Equal(prefix.Api.Call("food"), care.FoodCall);
            Assert.Contains("time.cared ", careLine);
            Assert.Contains(" food=true ", careLine);
            Assert.True(advanced.Advanced);
            Assert.Contains("time.advanced ", timeLine);
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_layout_ops_wholesaler_hire_loan_demand_tax_time_skips_when_needs_are_low()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → layout → ops → wholesaler → hire → loan → demand → tax → time → blocked
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var rented = Rent.Choose(
                new[] { new RentLot("ba:street_low", 9, 40m) },
                guard.Cash,
                anyRented: false,
                prefix.Api);
            Rent.Record(dir.Path, rented, TopBar.Day(prefix.Rolled));
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, prefix.Api);
            Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));
            var assigned = StoreOps.Assign(chosen, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), prefix.Api);
            StoreOps.Record(dir.Path, assigned, rented, TopBar.Day(shot.Rolled));
            var signed = Wholesaler.Sign(rented, chosen, Wholesaler.StageOne(), alreadySigned: false, prefix.Api);
            Wholesaler.Record(dir.Path, signed, TopBar.Day(shot.Rolled));
            var hired = Cashier.Hire(rented, chosen, signed, alreadyHired: false, prefix.Api);
            Cashier.Record(dir.Path, hired, TopBar.Day(shot.Rolled));
            var taken = BankLoan.Take(
                projectedDailyProfit: 200m,
                offers: new[] { new LoanOffer(2000m, 100) },
                alreadyBorrowed: false,
                prefix.Api);
            BankLoan.RecordTake(dir.Path, taken, TopBar.Day(shot.Rolled));
            var cleared = StaffDemand.Clear(
                unmetDays: StaffDemand.DemandDays,
                demandName: "ba:jobdemand_healthinsurance",
                employeeId: "emp-1",
                prefix.Api);
            StaffDemand.Record(dir.Path, cleared, TopBar.Day(shot.Rolled));
            var paid = TaxBill.Pay(currentDue: 55m, backDue: 0m, prefix.Api);
            TaxBill.Record(dir.Path, paid, TopBar.Day(shot.Rolled));
            var low = new PlayerNeeds(100f, 100f, 15f, true, true);
            var care = TimeAdvance.Care(low, prefix.Api);
            var careLine = TimeAdvance.RecordCare(dir.Path, care, TopBar.Day(shot.Rolled));
            var blocked = TimeAdvance.Skip(low, alreadyAdvanced: false, prefix.Api);
            var timeLine = TimeAdvance.RecordSkip(dir.Path, blocked, TopBar.Day(shot.Rolled));

            Assert.Contains("telemetry.written ", shot.Written);
            Assert.True(hired.Hired);
            Assert.True(taken.Taken);
            Assert.True(cleared.Cleared);
            Assert.True(paid.Paid);
            Assert.True(care.Due);
            Assert.True(care.Slept);
            Assert.True(care.Ate);
            Assert.Contains("time.cared ", careLine);
            Assert.False(blocked.Advanced);
            Assert.Equal("", timeLine);
            Assert.DoesNotContain(
                File.ReadAllLines(LoadLog.CeoLogPath(dir.Path)),
                line => line.StartsWith("time.advanced "));
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_layout_ops_wholesaler_hire_loan_demand_tax_time_marketing_locks_the_campaign()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → layout → ops → wholesaler → hire → loan → demand → tax → time → marketing
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var rented = Rent.Choose(
                new[] { new RentLot("ba:street_low", 9, 40m) },
                guard.Cash,
                anyRented: false,
                prefix.Api);
            var rentLine = Rent.Record(dir.Path, rented, TopBar.Day(prefix.Rolled));
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, prefix.Api);
            var layoutLine = Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));
            var assigned = StoreOps.Assign(chosen, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), prefix.Api);
            var opsLine = StoreOps.Record(dir.Path, assigned, rented, TopBar.Day(shot.Rolled));
            var signed = Wholesaler.Sign(rented, chosen, Wholesaler.StageOne(), alreadySigned: false, prefix.Api);
            var contractLine = Wholesaler.Record(dir.Path, signed, TopBar.Day(shot.Rolled));
            var hired = Cashier.Hire(rented, chosen, signed, alreadyHired: false, prefix.Api);
            var hireLine = Cashier.Record(dir.Path, hired, TopBar.Day(shot.Rolled));
            var taken = BankLoan.Take(
                projectedDailyProfit: 200m,
                offers: new[] { new LoanOffer(2000m, 100) },
                alreadyBorrowed: false,
                prefix.Api);
            var loanLine = BankLoan.RecordTake(dir.Path, taken, TopBar.Day(shot.Rolled));
            var cleared = StaffDemand.Clear(
                unmetDays: StaffDemand.DemandDays,
                demandName: "ba:jobdemand_healthinsurance",
                employeeId: "emp-1",
                prefix.Api);
            var demandLine = StaffDemand.Record(dir.Path, cleared, TopBar.Day(shot.Rolled));
            var paid = TaxBill.Pay(currentDue: 55m, backDue: 12m, prefix.Api);
            var taxLine = TaxBill.Record(dir.Path, paid, TopBar.Day(shot.Rolled));
            var advanced = TimeAdvance.Skip(Needs.Hold(), alreadyAdvanced: false, prefix.Api);
            var timeLine = TimeAdvance.RecordSkip(dir.Path, advanced, TopBar.Day(shot.Rolled));
            var started = Marketing.Start(
                dailyCost: 100m,
                gain: 300m,
                alreadyActive: false,
                street: rented.Street,
                number: rented.Number,
                type: Marketing.StarterType,
                prefix.Api);
            var marketingLine = Marketing.RecordStart(dir.Path, started, TopBar.Day(shot.Rolled));

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Contains("telemetry.written ", shot.Written);
            Assert.Contains("building.rented ", rentLine);
            Assert.Contains("layout.loaded ", layoutLine);
            Assert.Contains("ops.assigned ", opsLine);
            Assert.Contains("contract.signed ", contractLine);
            Assert.Contains("hire.cashier ", hireLine);
            Assert.Contains("loan.taken ", loanLine);
            Assert.Contains("demand.cleared ", demandLine);
            Assert.Contains("tax.paid ", taxLine);
            Assert.True(advanced.Advanced);
            Assert.Equal(prefix.Api.Call("time advance"), advanced.Call);
            Assert.Contains("time.advanced ", timeLine);
            Assert.True(started.Started);
            Assert.Equal(prefix.Api.Call("marketing start"), started.Call);
            Assert.Equal(rented.Street, started.Street);
            Assert.Equal(rented.Number, started.Number);
            Assert.Contains("marketing.started ", marketingLine);
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.Path)).Count(line => line.StartsWith("marketing.started ")));
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_layout_ops_wholesaler_hire_loan_demand_tax_time_marketing_stops_then_declines_a_weak_takeover()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → layout → ops → wholesaler → hire → loan → demand → tax → time → marketing → stop → takeover
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var rented = Rent.Choose(
                new[] { new RentLot("ba:street_low", 9, 40m) },
                guard.Cash,
                anyRented: false,
                prefix.Api);
            Rent.Record(dir.Path, rented, TopBar.Day(prefix.Rolled));
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, prefix.Api);
            Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));
            var assigned = StoreOps.Assign(chosen, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), prefix.Api);
            StoreOps.Record(dir.Path, assigned, rented, TopBar.Day(shot.Rolled));
            var signed = Wholesaler.Sign(rented, chosen, Wholesaler.StageOne(), alreadySigned: false, prefix.Api);
            Wholesaler.Record(dir.Path, signed, TopBar.Day(shot.Rolled));
            var hired = Cashier.Hire(rented, chosen, signed, alreadyHired: false, prefix.Api);
            Cashier.Record(dir.Path, hired, TopBar.Day(shot.Rolled));
            var taken = BankLoan.Take(
                projectedDailyProfit: 200m,
                offers: new[] { new LoanOffer(2000m, 100) },
                alreadyBorrowed: false,
                prefix.Api);
            BankLoan.RecordTake(dir.Path, taken, TopBar.Day(shot.Rolled));
            var cleared = StaffDemand.Clear(
                unmetDays: StaffDemand.DemandDays,
                demandName: "ba:jobdemand_healthinsurance",
                employeeId: "emp-1",
                prefix.Api);
            StaffDemand.Record(dir.Path, cleared, TopBar.Day(shot.Rolled));
            var paid = TaxBill.Pay(currentDue: 10m, backDue: 0m, prefix.Api);
            TaxBill.Record(dir.Path, paid, TopBar.Day(shot.Rolled));
            var advanced = TimeAdvance.Skip(Needs.Hold(), alreadyAdvanced: false, prefix.Api);
            TimeAdvance.RecordSkip(dir.Path, advanced, TopBar.Day(shot.Rolled));
            var started = Marketing.Start(
                dailyCost: 100m,
                gain: 300m,
                alreadyActive: false,
                street: rented.Street,
                number: rented.Number,
                type: Marketing.StarterType,
                prefix.Api);
            var startLine = Marketing.RecordStart(dir.Path, started, TopBar.Day(shot.Rolled));
            var stopped = Marketing.Stop(
                alreadyActive: started.Started,
                street: rented.Street,
                number: rented.Number,
                prefix.Api);
            var stopLine = Marketing.RecordStop(dir.Path, stopped, TopBar.Day(shot.Rolled));
            var declined = RivalTakeover.Decline(
                offerPrice: 1000m,
                dailyProfit: 10m,
                street: rented.Street,
                number: rented.Number,
                alreadyDeclined: false,
                prefix.Api);
            var takeoverLine = RivalTakeover.Record(dir.Path, declined, TopBar.Day(shot.Rolled));

            Assert.Contains("telemetry.written ", shot.Written);
            Assert.True(hired.Hired);
            Assert.True(taken.Taken);
            Assert.True(advanced.Advanced);
            Assert.True(started.Started);
            Assert.Contains("marketing.started ", startLine);
            Assert.True(stopped.Stopped);
            Assert.Equal(prefix.Api.Call("marketing stop"), stopped.Call);
            Assert.Contains("marketing.stopped ", stopLine);
            Assert.True(declined.Declined);
            Assert.Equal(prefix.Api.Call("rival takeover decline"), declined.Call);
            Assert.Contains("takeover.declined ", takeoverLine);
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.Path)).Count(line => line.StartsWith("marketing.started ")));
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.Path)).Count(line => line.StartsWith("marketing.stopped ")));
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.Path)).Count(line => line.StartsWith("takeover.declined ")));
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_layout_ops_wholesaler_hire_skips_a_second_cashier()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → layout → ops → wholesaler → hire → kept
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var rented = Rent.Choose(
                new[] { new RentLot("ba:street_low", 9, 40m) },
                guard.Cash,
                anyRented: false,
                prefix.Api);
            var rentLine = Rent.Record(dir.Path, rented, TopBar.Day(prefix.Rolled));
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, prefix.Api);
            var layoutLine = Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));
            var assigned = StoreOps.Assign(chosen, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), prefix.Api);
            var opsLine = StoreOps.Record(dir.Path, assigned, rented, TopBar.Day(shot.Rolled));
            var signed = Wholesaler.Sign(rented, chosen, Wholesaler.StageOne(), alreadySigned: false, prefix.Api);
            var contractLine = Wholesaler.Record(dir.Path, signed, TopBar.Day(shot.Rolled));
            var hired = Cashier.Hire(rented, chosen, signed, alreadyHired: true, prefix.Api);
            var written = Cashier.Record(dir.Path, hired, TopBar.Day(shot.Rolled));

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Contains("telemetry.written ", shot.Written);
            Assert.Contains("building.rented ", rentLine);
            Assert.Contains("layout.loaded ", layoutLine);
            Assert.Contains("ops.assigned ", opsLine);
            Assert.Contains("contract.signed ", contractLine);
            Assert.False(hired.Hired);
            Assert.Equal("", hired.Call);
            Assert.Equal("", written);
            Assert.DoesNotContain(
                File.ReadAllLines(LoadLog.CeoLogPath(dir.Path)),
                line => line.StartsWith("hire.cashier "));
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_layout_ops_wholesaler_hire_skips_when_the_contract_is_missing()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → layout → ops → wholesaler → hire → unsigned
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var rented = Rent.Choose(
                new[] { new RentLot("ba:street_low", 9, 40m) },
                guard.Cash,
                anyRented: false,
                prefix.Api);
            var rentLine = Rent.Record(dir.Path, rented, TopBar.Day(prefix.Rolled));
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, prefix.Api);
            var layoutLine = Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));
            var assigned = StoreOps.Assign(chosen, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), prefix.Api);
            var opsLine = StoreOps.Record(dir.Path, assigned, rented, TopBar.Day(shot.Rolled));
            var signed = Wholesaler.Sign(rented, chosen, Wholesaler.StageOne(), alreadySigned: true, prefix.Api);
            var contractLine = Wholesaler.Record(dir.Path, signed, TopBar.Day(shot.Rolled));
            var hired = Cashier.Hire(rented, chosen, signed, alreadyHired: false, prefix.Api);
            var written = Cashier.Record(dir.Path, hired, TopBar.Day(shot.Rolled));

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Contains("telemetry.written ", shot.Written);
            Assert.Contains("building.rented ", rentLine);
            Assert.Contains("layout.loaded ", layoutLine);
            Assert.Contains("ops.assigned ", opsLine);
            Assert.Equal("", contractLine);
            Assert.False(signed.Signed);
            Assert.False(hired.Hired);
            Assert.Equal("", written);
            Assert.DoesNotContain(
                File.ReadAllLines(LoadLog.CeoLogPath(dir.Path)),
                line => line.StartsWith("hire.cashier "));
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_layout_ops_wholesaler_hire_loan_demand_tax_time_marketing_stop_takeover_stage_locks_decide()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → layout → ops → wholesaler → hire → loan → demand → tax → time → marketing → stop → takeover → stage
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var rented = Rent.Choose(
                new[] { new RentLot("ba:street_low", 9, 40m) },
                guard.Cash,
                anyRented: false,
                prefix.Api);
            Rent.Record(dir.Path, rented, TopBar.Day(prefix.Rolled));
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, prefix.Api);
            Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));
            var assigned = StoreOps.Assign(chosen, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), prefix.Api);
            StoreOps.Record(dir.Path, assigned, rented, TopBar.Day(shot.Rolled));
            var signed = Wholesaler.Sign(rented, chosen, Wholesaler.StageOne(), alreadySigned: false, prefix.Api);
            Wholesaler.Record(dir.Path, signed, TopBar.Day(shot.Rolled));
            var hired = Cashier.Hire(rented, chosen, signed, alreadyHired: false, prefix.Api);
            Cashier.Record(dir.Path, hired, TopBar.Day(shot.Rolled));
            var taken = BankLoan.Take(
                projectedDailyProfit: 200m,
                offers: new[] { new LoanOffer(2000m, 100) },
                alreadyBorrowed: false,
                prefix.Api);
            BankLoan.RecordTake(dir.Path, taken, TopBar.Day(shot.Rolled));
            var cleared = StaffDemand.Clear(
                unmetDays: StaffDemand.DemandDays,
                demandName: "ba:jobdemand_healthinsurance",
                employeeId: "emp-1",
                prefix.Api);
            StaffDemand.Record(dir.Path, cleared, TopBar.Day(shot.Rolled));
            var paid = TaxBill.Pay(currentDue: 55m, backDue: 12m, prefix.Api);
            TaxBill.Record(dir.Path, paid, TopBar.Day(shot.Rolled));
            var advanced = TimeAdvance.Skip(Needs.Hold(), alreadyAdvanced: false, prefix.Api);
            TimeAdvance.RecordSkip(dir.Path, advanced, TopBar.Day(shot.Rolled));
            var started = Marketing.Start(
                dailyCost: 100m,
                gain: 300m,
                alreadyActive: false,
                street: rented.Street,
                number: rented.Number,
                type: Marketing.StarterType,
                prefix.Api);
            Marketing.RecordStart(dir.Path, started, TopBar.Day(shot.Rolled));
            var stopped = Marketing.Stop(
                alreadyActive: started.Started,
                street: rented.Street,
                number: rented.Number,
                prefix.Api);
            Marketing.RecordStop(dir.Path, stopped, TopBar.Day(shot.Rolled));
            var declined = RivalTakeover.Decline(
                offerPrice: 1000m,
                dailyProfit: 10m,
                street: rented.Street,
                number: rented.Number,
                alreadyDeclined: false,
                prefix.Api);
            RivalTakeover.Record(dir.Path, declined, TopBar.Day(shot.Rolled));
            var stage = Stage.Read(
                TopBar.Day(shot.Rolled),
                guard.Cash,
                world.Needs,
                shot.Report.Stops,
                world.Holds,
                world.Slots);

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Contains("telemetry.written ", shot.Written);
            Assert.True(hired.Hired);
            Assert.True(taken.Taken);
            Assert.True(advanced.Advanced);
            Assert.True(started.Started);
            Assert.True(stopped.Stopped);
            Assert.True(declined.Declined);
            Assert.Equal(1, stage);
            Assert.Equal("1", Between(shot.Bar, "Stage "));
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_layout_ops_wholesaler_hire_loan_demand_tax_time_marketing_stop_takeover_stage_open_where_locks_the_playbook()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → layout → ops → wholesaler → hire → loan → demand → tax → time → marketing → stop → takeover → stage → open → where
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var rented = Rent.Choose(
                new[] { new RentLot("ba:street_low", 9, 40m) },
                guard.Cash,
                anyRented: false,
                prefix.Api);
            Rent.Record(dir.Path, rented, TopBar.Day(prefix.Rolled));
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, prefix.Api);
            Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));
            var assigned = StoreOps.Assign(chosen, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), prefix.Api);
            StoreOps.Record(dir.Path, assigned, rented, TopBar.Day(shot.Rolled));
            var signed = Wholesaler.Sign(rented, chosen, Wholesaler.StageOne(), alreadySigned: false, prefix.Api);
            Wholesaler.Record(dir.Path, signed, TopBar.Day(shot.Rolled));
            var hired = Cashier.Hire(rented, chosen, signed, alreadyHired: false, prefix.Api);
            Cashier.Record(dir.Path, hired, TopBar.Day(shot.Rolled));
            var taken = BankLoan.Take(
                projectedDailyProfit: 200m,
                offers: new[] { new LoanOffer(2000m, 100) },
                alreadyBorrowed: false,
                prefix.Api);
            BankLoan.RecordTake(dir.Path, taken, TopBar.Day(shot.Rolled));
            var cleared = StaffDemand.Clear(
                unmetDays: StaffDemand.DemandDays,
                demandName: "ba:jobdemand_healthinsurance",
                employeeId: "emp-1",
                prefix.Api);
            StaffDemand.Record(dir.Path, cleared, TopBar.Day(shot.Rolled));
            var paid = TaxBill.Pay(currentDue: 55m, backDue: 12m, prefix.Api);
            TaxBill.Record(dir.Path, paid, TopBar.Day(shot.Rolled));
            var advanced = TimeAdvance.Skip(Needs.Hold(), alreadyAdvanced: false, prefix.Api);
            TimeAdvance.RecordSkip(dir.Path, advanced, TopBar.Day(shot.Rolled));
            var started = Marketing.Start(
                dailyCost: 100m,
                gain: 300m,
                alreadyActive: false,
                street: rented.Street,
                number: rented.Number,
                type: Marketing.StarterType,
                prefix.Api);
            Marketing.RecordStart(dir.Path, started, TopBar.Day(shot.Rolled));
            var stopped = Marketing.Stop(
                alreadyActive: started.Started,
                street: rented.Street,
                number: rented.Number,
                prefix.Api);
            Marketing.RecordStop(dir.Path, stopped, TopBar.Day(shot.Rolled));
            var declined = RivalTakeover.Decline(
                offerPrice: 1000m,
                dailyProfit: 10m,
                street: rented.Street,
                number: rented.Number,
                alreadyDeclined: false,
                prefix.Api);
            RivalTakeover.Record(dir.Path, declined, TopBar.Day(shot.Rolled));
            var stage = Stage.Read(
                TopBar.Day(shot.Rolled),
                guard.Cash,
                world.Needs,
                shot.Report.Stops,
                world.Holds,
                world.Slots);
            var offers = new List<OpenCandidate>();
            foreach (var offer in Starter.StageOne())
            {
                offers.Add(new OpenCandidate(
                    offer.BusinessType,
                    "goods",
                    offer.DailyProfit,
                    wholesalerSells: true,
                    StoreFamily.For(offer.BusinessType) ?? "specialty",
                    ownedOfFamily: 0,
                    familyHasWarehouse: false));
            }

            offers.Add(new OpenCandidate(
                "ba:businesstype_lawfirm",
                "office",
                100m,
                wholesalerSells: false,
                "office",
                0,
                false));
            var open = Open.Decide(stage, offers, truckLoadFraction: 0m);
            var reserve = guard.DailyCost * Audit.ReserveDays;
            var site = Where.Decide(
                new[]
                {
                    new SiteCandidate("ba:street_lonely", 1, familyOnStreet: 0, traffic: 90m, dailyRent: 10m, cost: 50m),
                    new SiteCandidate(rented.Street, rented.Number, familyOnStreet: Where.ClusterMin, traffic: 20m, dailyRent: 40m, cost: rented.Number),
                    new SiteCandidate("ba:street_later", 8, familyOnStreet: Where.ClusterMin, traffic: 10m, dailyRent: 40m, cost: 40m),
                },
                guard.Cash,
                reserve);

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Equal(1, stage);
            Assert.True(open.Chosen);
            Assert.Equal(chosen.BusinessType, open.Type);
            Assert.Equal("ba:businesstype_giftshop", open.Type);
            Assert.True(site.Chosen);
            Assert.Equal(rented.Street, site.Street);
            Assert.Equal(rented.Number, site.Number);
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_stage_open_where_truck_busy_picks_service()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → stage → open → where
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = new AuditGuard(
            enabled: true,
            dryRun: false,
            cash: Stage.Stage2Cash,
            dailyCost: 10m,
            truckCost: 1,
            build: LoadLog.PinnedBuild);
        var needs = new[]
        {
            new GoodsNeed("ba:street_a 1", "ba:itemname_gift"),
            new GoodsNeed("ba:street_b 2", "ba:itemname_gift"),
            new GoodsNeed("ba:street_c 3", "ba:itemname_gift"),
        };
        var world = new AuditWorld(
            prefix.World.Stops,
            prefix.World.Holds,
            needs,
            prefix.World.Sales,
            prefix.World.Slots,
            prefix.World.Drivers,
            prefix.World.Levels,
            prefix.World.PricingPlan,
            prefix.World.Seats,
            prefix.World.Hired,
            prefix.World.Stores,
            prefix.World.Staffed);
        var report = Audit.Check(dir.Path, 1016, prefix.Api, world, guard, heal => HealFate.Done);
        var stage = Stage.Read(
            TopBar.Day(prefix.Rolled),
            guard.Cash,
            world.Needs,
            report.Stops,
            world.Holds,
            world.Slots);
        var bar = TopBar.From(
            prefix.Rolled,
            File.ReadAllText(StatementDump.CsvPath(dir.Path)),
            guard.Cash,
            0m,
            stage,
            guard.Enabled,
            TopBar.Clock(paused: false, rate: null));
        var open = Open.Decide(
            stage,
            new[]
            {
                new OpenCandidate(
                    "ba:businesstype_giftshop",
                    "goods",
                    90m,
                    wholesalerSells: true,
                    "specialty",
                    0,
                    true),
                new OpenCandidate(
                    "ba:businesstype_hairdresser",
                    "service",
                    40m,
                    wholesalerSells: false,
                    "service",
                    0,
                    false),
                new OpenCandidate(
                    "ba:businesstype_lawfirm",
                    "office",
                    55m,
                    wholesalerSells: false,
                    "office",
                    0,
                    false),
            },
            truckLoadFraction: Open.TruckBusy + 0.01m);
        var site = Where.Decide(
            new[]
            {
                new SiteCandidate("ba:street_service", 4, familyOnStreet: Where.ClusterMin, traffic: 12m, dailyRent: 20m, cost: 80m),
                new SiteCandidate("ba:street_quiet", 2, familyOnStreet: 0, traffic: 80m, dailyRent: 10m, cost: 80m),
            },
            guard.Cash,
            guard.DailyCost * Audit.ReserveDays);

        Assert.Contains("day=1016", prefix.Rolled);
        Assert.Equal(2, stage);
        Assert.Equal("2", Between(bar, "Stage "));
        Assert.True(open.Chosen);
        Assert.Equal("ba:businesstype_lawfirm", open.Type);
        Assert.True(site.Chosen);
        Assert.Equal("ba:street_service", site.Street);
        Assert.Equal(4, site.Number);
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_layout_ops_wholesaler_hire_loan_demand_tax_time_marketing_stop_takeover_stage_open_where_layout_locks_the_golden()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → layout → ops → wholesaler → hire → loan → demand → tax → time → marketing → stop → takeover → stage → open → where → layout
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var rented = Rent.Choose(
                new[] { new RentLot("ba:street_low", 9, 40m) },
                guard.Cash,
                anyRented: false,
                prefix.Api);
            Rent.Record(dir.Path, rented, TopBar.Day(prefix.Rolled));
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, prefix.Api);
            Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));
            var assigned = StoreOps.Assign(chosen, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), prefix.Api);
            StoreOps.Record(dir.Path, assigned, rented, TopBar.Day(shot.Rolled));
            var signed = Wholesaler.Sign(rented, chosen, Wholesaler.StageOne(), alreadySigned: false, prefix.Api);
            Wholesaler.Record(dir.Path, signed, TopBar.Day(shot.Rolled));
            var hired = Cashier.Hire(rented, chosen, signed, alreadyHired: false, prefix.Api);
            Cashier.Record(dir.Path, hired, TopBar.Day(shot.Rolled));
            var taken = BankLoan.Take(
                projectedDailyProfit: 200m,
                offers: new[] { new LoanOffer(2000m, 100) },
                alreadyBorrowed: false,
                prefix.Api);
            BankLoan.RecordTake(dir.Path, taken, TopBar.Day(shot.Rolled));
            var cleared = StaffDemand.Clear(
                unmetDays: StaffDemand.DemandDays,
                demandName: "ba:jobdemand_healthinsurance",
                employeeId: "emp-1",
                prefix.Api);
            StaffDemand.Record(dir.Path, cleared, TopBar.Day(shot.Rolled));
            var paid = TaxBill.Pay(currentDue: 55m, backDue: 12m, prefix.Api);
            TaxBill.Record(dir.Path, paid, TopBar.Day(shot.Rolled));
            var advanced = TimeAdvance.Skip(Needs.Hold(), alreadyAdvanced: false, prefix.Api);
            TimeAdvance.RecordSkip(dir.Path, advanced, TopBar.Day(shot.Rolled));
            var started = Marketing.Start(
                dailyCost: 100m,
                gain: 300m,
                alreadyActive: false,
                street: rented.Street,
                number: rented.Number,
                type: Marketing.StarterType,
                prefix.Api);
            Marketing.RecordStart(dir.Path, started, TopBar.Day(shot.Rolled));
            var stopped = Marketing.Stop(
                alreadyActive: started.Started,
                street: rented.Street,
                number: rented.Number,
                prefix.Api);
            Marketing.RecordStop(dir.Path, stopped, TopBar.Day(shot.Rolled));
            var declined = RivalTakeover.Decline(
                offerPrice: 1000m,
                dailyProfit: 10m,
                street: rented.Street,
                number: rented.Number,
                alreadyDeclined: false,
                prefix.Api);
            RivalTakeover.Record(dir.Path, declined, TopBar.Day(shot.Rolled));
            var stage = Stage.Read(
                TopBar.Day(shot.Rolled),
                guard.Cash,
                world.Needs,
                shot.Report.Stops,
                world.Holds,
                world.Slots);
            var offers = new List<OpenCandidate>();
            foreach (var offer in Starter.StageOne())
            {
                offers.Add(new OpenCandidate(
                    offer.BusinessType,
                    "goods",
                    offer.DailyProfit,
                    wholesalerSells: true,
                    StoreFamily.For(offer.BusinessType) ?? "specialty",
                    ownedOfFamily: 0,
                    familyHasWarehouse: false));
            }

            offers.Add(new OpenCandidate(
                "ba:businesstype_lawfirm",
                "office",
                100m,
                wholesalerSells: false,
                "office",
                0,
                false));
            var open = Open.Decide(stage, offers, truckLoadFraction: 0m);
            var reserve = guard.DailyCost * Audit.ReserveDays;
            var site = Where.Decide(
                new[]
                {
                    new SiteCandidate("ba:street_lonely", 1, familyOnStreet: 0, traffic: 90m, dailyRent: 10m, cost: 50m),
                    new SiteCandidate(rented.Street, rented.Number, familyOnStreet: Where.ClusterMin, traffic: 20m, dailyRent: 40m, cost: rented.Number),
                    new SiteCandidate("ba:street_later", 8, familyOnStreet: Where.ClusterMin, traffic: 10m, dailyRent: 40m, cost: 40m),
                },
                guard.Cash,
                reserve);
            var golden = Layout.Decide(
                stage,
                open.Type,
                "small",
                new[]
                {
                    new OwnedLayout(open.Type, "small", "GiftShopEmpire", 90),
                    new OwnedLayout(open.Type, "large", "GiftShopLarge", 99),
                },
                Starter.StageOne());

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Equal(1, stage);
            Assert.True(open.Chosen);
            Assert.Equal(chosen.BusinessType, open.Type);
            Assert.Equal("ba:businesstype_giftshop", open.Type);
            Assert.True(site.Chosen);
            Assert.Equal(rented.Street, site.Street);
            Assert.Equal(rented.Number, site.Number);
            Assert.True(golden.Chosen);
            Assert.Equal("GiftShopWholesalers", golden.Blueprint);
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_stage_open_where_layout_prices_locks_the_raise()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → stage → open → where → layout → prices
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = new AuditGuard(
            enabled: true,
            dryRun: false,
            cash: Stage.Stage2Cash,
            dailyCost: 10m,
            truckCost: 1,
            build: LoadLog.PinnedBuild);
        var needs = new[]
        {
            new GoodsNeed("ba:street_a 1", "ba:itemname_gift"),
            new GoodsNeed("ba:street_b 2", "ba:itemname_gift"),
            new GoodsNeed("ba:street_c 3", "ba:itemname_gift"),
        };
        var world = new AuditWorld(
            prefix.World.Stops,
            prefix.World.Holds,
            needs,
            prefix.World.Sales,
            prefix.World.Slots,
            prefix.World.Drivers,
            prefix.World.Levels,
            prefix.World.PricingPlan,
            prefix.World.Seats,
            prefix.World.Hired,
            prefix.World.Stores,
            prefix.World.Staffed);
        var report = Audit.Check(dir.Path, 1016, prefix.Api, world, guard, heal => HealFate.Done);
        var stage = Stage.Read(
            TopBar.Day(prefix.Rolled),
            guard.Cash,
            world.Needs,
            report.Stops,
            world.Holds,
            world.Slots);
        var open = Open.Decide(
            stage,
            new[]
            {
                new OpenCandidate(
                    "ba:businesstype_giftshop",
                    "goods",
                    90m,
                    wholesalerSells: true,
                    "specialty",
                    0,
                    true),
                new OpenCandidate(
                    "ba:businesstype_hairdresser",
                    "service",
                    40m,
                    wholesalerSells: false,
                    "service",
                    0,
                    false),
            },
            truckLoadFraction: 0m);
        var site = Where.Decide(
            new[]
            {
                new SiteCandidate("ba:street_gift", 5, familyOnStreet: Where.ClusterMin, traffic: 12m, dailyRent: 20m, cost: 80m),
            },
            guard.Cash,
            guard.DailyCost * Audit.ReserveDays);
        var golden = Layout.Decide(
            stage,
            open.Type,
            "small",
            new[]
            {
                new OwnedLayout(open.Type, "small", "GiftShopLive", 44),
                new OwnedLayout(open.Type, "small", "GiftShopBest", 70),
            },
            Starter.StageOne());
        var price = Prices.Decide(
            "ba:itemname_gift",
            currentPrice: 20m,
            defaultPrice: 10m,
            wholesale: 4m,
            priorUnits: 8,
            currentUnits: 9,
            satisfaction: Prices.PriceSatisfied,
            trials: new[]
            {
                new ItemSoldPerPriceEntry("ba:itemname_gift", 18f, 9),
            });

        Assert.Contains("day=1016", prefix.Rolled);
        Assert.Equal(2, stage);
        Assert.True(open.Chosen);
        Assert.Equal("ba:businesstype_giftshop", open.Type);
        Assert.True(site.Chosen);
        Assert.Equal("ba:street_gift", site.Street);
        Assert.Equal(5, site.Number);
        Assert.True(golden.Chosen);
        Assert.Equal("GiftShopBest", golden.Blueprint);
        Assert.True(price.Chosen);
        Assert.Equal(21m, price.Price);
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_stage_open_where_layout_prices_supply_locks_the_warehouse_source()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → stage → open → where → layout → prices → supply
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = new AuditGuard(
            enabled: true,
            dryRun: false,
            cash: Stage.Stage2Cash,
            dailyCost: 10m,
            truckCost: 1,
            build: LoadLog.PinnedBuild);
        var needs = new[]
        {
            new GoodsNeed("ba:street_a 1", "ba:itemname_cheapgift"),
            new GoodsNeed("ba:street_b 2", "ba:itemname_cheapgift"),
            new GoodsNeed("ba:street_c 3", "ba:itemname_cheapgift"),
        };
        var holds = new[]
        {
            new WarehouseHold("W12", "ba:street_twelfthstreet", 6, "ba:itemname_cheapgift", 40),
        };
        var world = new AuditWorld(
            prefix.World.Stops,
            holds,
            needs,
            prefix.World.Sales,
            prefix.World.Slots,
            prefix.World.Drivers,
            prefix.World.Levels,
            prefix.World.PricingPlan,
            prefix.World.Seats,
            prefix.World.Hired,
            prefix.World.Stores,
            prefix.World.Staffed);
        var report = Audit.Check(dir.Path, 1016, prefix.Api, world, guard, heal => HealFate.Done);
        var stage = Stage.Read(
            TopBar.Day(prefix.Rolled),
            guard.Cash,
            world.Needs,
            report.Stops,
            world.Holds,
            world.Slots);
        var open = Open.Decide(
            stage,
            new[]
            {
                new OpenCandidate(
                    "ba:businesstype_giftshop",
                    "goods",
                    90m,
                    wholesalerSells: true,
                    "specialty",
                    0,
                    true),
                new OpenCandidate(
                    "ba:businesstype_hairdresser",
                    "service",
                    40m,
                    wholesalerSells: false,
                    "service",
                    0,
                    false),
            },
            truckLoadFraction: 0m);
        var site = Where.Decide(
            new[]
            {
                new SiteCandidate("ba:street_gift", 5, familyOnStreet: Where.ClusterMin, traffic: 12m, dailyRent: 20m, cost: 80m),
            },
            guard.Cash,
            guard.DailyCost * Audit.ReserveDays);
        var golden = Layout.Decide(
            stage,
            open.Type,
            "small",
            new[]
            {
                new OwnedLayout(open.Type, "small", "GiftShopLive", 44),
                new OwnedLayout(open.Type, "small", "GiftShopBest", 70),
            },
            Starter.StageOne());
        var price = Prices.Decide(
            "ba:itemname_cheapgift",
            currentPrice: 20m,
            defaultPrice: 10m,
            wholesale: 4m,
            priorUnits: 8,
            currentUnits: 9,
            satisfaction: Prices.PriceSatisfied,
            trials: new[]
            {
                new ItemSoldPerPriceEntry("ba:itemname_cheapgift", 18f, 9),
            });
        var supply = Supply.Decide(
            stage,
            site.Street + " " + site.Number.ToString(CultureInfo.InvariantCulture),
            "ba:itemname_cheapgift",
            world.Holds,
            report.Stops);
        var truck = Trucks.Decide(
            new[]
            {
                new GarageSlot("W12", 1, filled: false),
                new GarageSlot("W12", 2, filled: false),
            },
            delivering: new[] { "W12" },
            cash: guard.Cash,
            reserve: guard.DailyCost * Audit.ReserveDays,
            truckCost: guard.TruckCost);
        var warehouse = Warehouses.Decide(
            "specialty",
            enteringFamily: false,
            hasStockWithoutHome: false,
            new[]
            {
                new WarehouseLevel("W12", "specialty", onHand: 40, daily: 4, ordered: false, shelves: 99),
                new WarehouseLevel("W13", "", onHand: 0, daily: 0, ordered: false, shelves: 95),
            },
            trucksBusy: true);

        Assert.Contains("day=1016", prefix.Rolled);
        Assert.Equal(2, stage);
        Assert.True(open.Chosen);
        Assert.Equal("ba:businesstype_giftshop", open.Type);
        Assert.True(site.Chosen);
        Assert.Equal("ba:street_gift", site.Street);
        Assert.Equal(5, site.Number);
        Assert.True(golden.Chosen);
        Assert.Equal("GiftShopBest", golden.Blueprint);
        Assert.True(price.Chosen);
        Assert.Equal(21m, price.Price);
        Assert.True(supply.Chosen);
        Assert.Equal("warehouse", supply.Source);
        Assert.Equal("W12", supply.Warehouse);
        Assert.True(supply.DropContract);
        Assert.True(truck.Chosen);
        Assert.Equal("W12", truck.Warehouse);
        Assert.Equal(1, truck.Slot);
        Assert.True(warehouse.Chosen);
        Assert.Equal("W13", warehouse.Warehouse);
        Assert.Equal("second", warehouse.Action);
        Assert.Equal("trucks", warehouse.Reason);
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_stage_open_where_layout_prices_supply_managers_locks_the_pricing_plan()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → stage → open → where → layout → prices → supply → managers
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = new AuditGuard(
            enabled: true,
            dryRun: false,
            cash: Stage.Stage2Cash,
            dailyCost: 10m,
            truckCost: 1,
            build: LoadLog.PinnedBuild);
        var needs = new[]
        {
            new GoodsNeed("ba:street_a 1", "ba:itemname_cheapgift"),
            new GoodsNeed("ba:street_b 2", "ba:itemname_cheapgift"),
            new GoodsNeed("ba:street_c 3", "ba:itemname_cheapgift"),
        };
        var holds = new[]
        {
            new WarehouseHold("W12", "ba:street_twelfthstreet", 6, "ba:itemname_cheapgift", 40),
        };
        var world = new AuditWorld(
            prefix.World.Stops,
            holds,
            needs,
            prefix.World.Sales,
            prefix.World.Slots,
            prefix.World.Drivers,
            prefix.World.Levels,
            prefix.World.PricingPlan,
            prefix.World.Seats,
            prefix.World.Hired,
            prefix.World.Stores,
            prefix.World.Staffed);
        var report = Audit.Check(dir.Path, 1016, prefix.Api, world, guard, heal => HealFate.Done);
        var stage = Stage.Read(
            TopBar.Day(prefix.Rolled),
            guard.Cash,
            world.Needs,
            report.Stops,
            world.Holds,
            world.Slots);
        var open = Open.Decide(
            stage,
            new[]
            {
                new OpenCandidate(
                    "ba:businesstype_giftshop",
                    "goods",
                    90m,
                    wholesalerSells: true,
                    "specialty",
                    0,
                    true),
            },
            truckLoadFraction: 0m);
        var site = Where.Decide(
            new[]
            {
                new SiteCandidate("ba:street_gift", 5, familyOnStreet: Where.ClusterMin, traffic: 12m, dailyRent: 20m, cost: 80m),
            },
            guard.Cash,
            guard.DailyCost * Audit.ReserveDays);
        var golden = Layout.Decide(
            stage,
            open.Type,
            "small",
            new[]
            {
                new OwnedLayout(open.Type, "small", "GiftShopLive", 44),
                new OwnedLayout(open.Type, "small", "GiftShopBest", 70),
            },
            Starter.StageOne());
        var price = Prices.Decide(
            "ba:itemname_cheapgift",
            currentPrice: 20m,
            defaultPrice: 10m,
            wholesale: 4m,
            priorUnits: 8,
            currentUnits: 9,
            satisfaction: Prices.PriceSatisfied,
            trials: new[]
            {
                new ItemSoldPerPriceEntry("ba:itemname_cheapgift", 18f, 9),
            });
        var supply = Supply.Decide(
            stage,
            site.Street + " " + site.Number.ToString(CultureInfo.InvariantCulture),
            "ba:itemname_cheapgift",
            world.Holds,
            report.Stops);
        var managers = Managers.Decide(
            stage,
            new[] { supply.Warehouse },
            employees: 10,
            openRoles: 2,
            seats: Array.Empty<ManagerSeat>(),
            pricingPlanExists: false);

        Assert.Contains("day=1016", prefix.Rolled);
        Assert.Equal(2, stage);
        Assert.True(open.Chosen);
        Assert.Equal("ba:businesstype_giftshop", open.Type);
        Assert.True(site.Chosen);
        Assert.Equal("ba:street_gift", site.Street);
        Assert.True(golden.Chosen);
        Assert.Equal("GiftShopBest", golden.Blueprint);
        Assert.True(price.Chosen);
        Assert.Equal(21m, price.Price);
        Assert.True(supply.Chosen);
        Assert.Equal("warehouse", supply.Source);
        Assert.Equal("W12", supply.Warehouse);
        Assert.True(managers.Chosen);
        Assert.Equal("pricing", managers.Role);
        Assert.Equal("plan", managers.Action);
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_stage_open_where_layout_prices_supply_managers_money_locks_the_day_spend_room()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → stage → open → where → layout → prices → supply → managers → money
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = new AuditGuard(
            enabled: true,
            dryRun: false,
            cash: Stage.Stage2Cash,
            dailyCost: 10m,
            truckCost: 1,
            build: LoadLog.PinnedBuild);
        var needs = new[]
        {
            new GoodsNeed("ba:street_a 1", "ba:itemname_cheapgift"),
            new GoodsNeed("ba:street_b 2", "ba:itemname_cheapgift"),
            new GoodsNeed("ba:street_c 3", "ba:itemname_cheapgift"),
        };
        var holds = new[]
        {
            new WarehouseHold("W12", "ba:street_twelfthstreet", 6, "ba:itemname_cheapgift", 40),
        };
        var world = new AuditWorld(
            prefix.World.Stops,
            holds,
            needs,
            prefix.World.Sales,
            prefix.World.Slots,
            prefix.World.Drivers,
            prefix.World.Levels,
            prefix.World.PricingPlan,
            prefix.World.Seats,
            prefix.World.Hired,
            prefix.World.Stores,
            prefix.World.Staffed);
        var report = Audit.Check(dir.Path, 1016, prefix.Api, world, guard, heal => HealFate.Done);
        var stage = Stage.Read(
            TopBar.Day(prefix.Rolled),
            guard.Cash,
            world.Needs,
            report.Stops,
            world.Holds,
            world.Slots);
        var open = Open.Decide(
            stage,
            new[]
            {
                new OpenCandidate(
                    "ba:businesstype_giftshop",
                    "goods",
                    90m,
                    wholesalerSells: true,
                    "specialty",
                    0,
                    true),
            },
            truckLoadFraction: 0m);
        var reserve = Money.Reserve(guard.DailyCost);
        var site = Where.Decide(
            new[]
            {
                new SiteCandidate("ba:street_gift", 5, familyOnStreet: Where.ClusterMin, traffic: 12m, dailyRent: 20m, cost: 80m),
            },
            guard.Cash,
            reserve);
        var golden = Layout.Decide(
            stage,
            open.Type,
            "small",
            new[]
            {
                new OwnedLayout(open.Type, "small", "GiftShopBest", 70),
            },
            Starter.StageOne());
        var price = Prices.Decide(
            "ba:itemname_cheapgift",
            currentPrice: 20m,
            defaultPrice: 10m,
            wholesale: 4m,
            priorUnits: 8,
            currentUnits: 9,
            satisfaction: Prices.PriceSatisfied,
            trials: new[]
            {
                new ItemSoldPerPriceEntry("ba:itemname_cheapgift", 18f, 9),
            });
        var supply = Supply.Decide(
            stage,
            site.Street + " " + site.Number.ToString(CultureInfo.InvariantCulture),
            "ba:itemname_cheapgift",
            world.Holds,
            report.Stops);
        var managers = Managers.Decide(
            stage,
            new[] { supply.Warehouse },
            employees: 10,
            openRoles: 2,
            seats: Array.Empty<ManagerSeat>(),
            pricingPlanExists: true);
        var room = Money.SpendRoom(guard.Cash, guard.DailyCost);
        var money = Money.Decide(
            stage,
            guard.Cash,
            guard.DailyCost,
            projectedDailyProfit: open.Type.Length,
            offers: new[] { new LoanOffer(2_000m, 80) },
            loan: null,
            bestPaybackRate: 0.1m);

        Assert.Contains("day=1016", prefix.Rolled);
        Assert.Equal(2, stage);
        Assert.True(supply.Chosen);
        Assert.Equal("W12", supply.Warehouse);
        Assert.True(managers.Chosen);
        Assert.Equal("logistics", managers.Role);
        Assert.Equal("W12", managers.Warehouse);
        Assert.Equal(70m, reserve);
        Assert.Equal(37_482.5m, room);
        Assert.False(money.Chosen);
        Assert.Equal("", money.Action);
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_stage_open_where_layout_prices_supply_managers_money_playbook_locks_the_open_step()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → stage → open → where → layout → prices → supply → managers → money → playbook
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = new AuditGuard(
            enabled: true,
            dryRun: false,
            cash: Stage.Stage2Cash,
            dailyCost: 10m,
            truckCost: 1,
            build: LoadLog.PinnedBuild);
        var needs = new[]
        {
            new GoodsNeed("ba:street_a 1", "ba:itemname_cheapgift"),
            new GoodsNeed("ba:street_b 2", "ba:itemname_cheapgift"),
            new GoodsNeed("ba:street_c 3", "ba:itemname_cheapgift"),
        };
        var holds = new[]
        {
            new WarehouseHold("W12", "ba:street_twelfthstreet", 6, "ba:itemname_cheapgift", 40),
        };
        var world = new AuditWorld(
            prefix.World.Stops,
            holds,
            needs,
            prefix.World.Sales,
            prefix.World.Slots,
            prefix.World.Drivers,
            prefix.World.Levels,
            prefix.World.PricingPlan,
            prefix.World.Seats,
            prefix.World.Hired,
            prefix.World.Stores,
            prefix.World.Staffed);
        var report = Audit.Check(dir.Path, 1016, prefix.Api, world, guard, heal => HealFate.Done);
        var stage = Stage.Read(
            TopBar.Day(prefix.Rolled),
            guard.Cash,
            world.Needs,
            report.Stops,
            world.Holds,
            world.Slots);
        var open = Open.Decide(
            stage,
            new[]
            {
                new OpenCandidate(
                    "ba:businesstype_giftshop",
                    "goods",
                    90m,
                    wholesalerSells: true,
                    "specialty",
                    0,
                    true),
            },
            truckLoadFraction: 0m);
        var reserve = Money.Reserve(guard.DailyCost);
        var site = Where.Decide(
            new[]
            {
                new SiteCandidate("ba:street_gift", 5, familyOnStreet: Where.ClusterMin, traffic: 12m, dailyRent: 20m, cost: 80m),
            },
            guard.Cash,
            reserve);
        var golden = Layout.Decide(
            stage,
            open.Type,
            "small",
            new[]
            {
                new OwnedLayout(open.Type, "small", "GiftShopBest", 70),
            },
            Starter.StageOne());
        var price = Prices.Decide(
            "ba:itemname_cheapgift",
            currentPrice: 20m,
            defaultPrice: 10m,
            wholesale: 4m,
            priorUnits: 8,
            currentUnits: 9,
            satisfaction: Prices.PriceSatisfied,
            trials: new[]
            {
                new ItemSoldPerPriceEntry("ba:itemname_cheapgift", 18f, 9),
            });
        var supply = Supply.Decide(
            stage,
            site.Street + " " + site.Number.ToString(CultureInfo.InvariantCulture),
            "ba:itemname_cheapgift",
            world.Holds,
            report.Stops);
        var managers = Managers.Decide(
            stage,
            new[] { supply.Warehouse },
            employees: 10,
            openRoles: 2,
            seats: Array.Empty<ManagerSeat>(),
            pricingPlanExists: false);
        var money = Money.Decide(
            stage,
            guard.Cash,
            guard.DailyCost,
            projectedDailyProfit: open.Type.Length,
            offers: new[] { new LoanOffer(2_000m, 80) },
            loan: null,
            bestPaybackRate: 0.1m);
        var step = Playbook.Decide(
            open,
            site,
            golden,
            price,
            supply,
            TruckPick.None(),
            WarehousePick.None(),
            managers,
            money,
            EstatePick.None(),
            ClosePick.None(),
            RunPick.None());
        var shown = Playbook.Record(dir.Path, step);

        Assert.Contains("day=1016", prefix.Rolled);
        Assert.Equal(2, stage);
        Assert.True(open.Chosen);
        Assert.True(managers.Chosen);
        Assert.Equal("pricing", managers.Role);
        Assert.False(money.Chosen);
        Assert.True(step.Chosen);
        Assert.Equal("open", step.Kind);
        Assert.Equal("ba:businesstype_giftshop", step.Detail);
        Assert.Equal("Next open ba:businesstype_giftshop", Playbook.Line(step));
        Assert.Equal(
            "playbook.shown version=" + LoadLog.Version + " kind=open detail=ba:businesstype_giftshop",
            shown);
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_stage_open_where_layout_prices_supply_managers_money_playbook_locks_the_stage_one_loan()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → stage → open → where → layout → prices → supply → managers → money → playbook
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var stage = Stage.Read(
                TopBar.Day(shot.Rolled),
                guard.Cash,
                world.Needs,
                world.Stops,
                world.Holds,
                world.Slots);
            var open = Open.Decide(stage, Array.Empty<OpenCandidate>(), truckLoadFraction: 0m);
            var reserve = Money.Reserve(guard.DailyCost);
            var site = Where.Decide(Array.Empty<SiteCandidate>(), guard.Cash, reserve);
            var golden = Layout.Decide(stage, "", "small", Array.Empty<OwnedLayout>(), Starter.StageOne());
            var price = Prices.Decide(
                "",
                currentPrice: 0m,
                defaultPrice: 10m,
                wholesale: 4m,
                priorUnits: 8,
                currentUnits: 8,
                satisfaction: 0m,
                trials: Array.Empty<ItemSoldPerPriceEntry>());
            var supply = Supply.Decide(stage, "", "ba:itemname_cheapgift", world.Holds, world.Stops);
            var managers = Managers.Decide(
                stage,
                Array.Empty<string>(),
                employees: 0,
                openRoles: 0,
                seats: Array.Empty<ManagerSeat>(),
                pricingPlanExists: true);
            var money = Money.Decide(
                stage,
                guard.Cash,
                guard.DailyCost,
                projectedDailyProfit: 200m,
                offers: new[]
                {
                    new LoanOffer(1_000m, 80),
                    new LoanOffer(2_000m, 100),
                },
                loan: null,
                bestPaybackRate: 0.1m);
            var step = Playbook.Decide(
                open,
                site,
                golden,
                price,
                supply,
                TruckPick.None(),
                WarehousePick.None(),
                managers,
                money,
                EstatePick.None(),
                ClosePick.None(),
                RunPick.None());

            Assert.Contains("day=1016", shot.Rolled);
            Assert.Equal(1, stage);
            Assert.False(open.Chosen);
            Assert.False(supply.Chosen);
            Assert.False(managers.Chosen);
            Assert.True(money.Chosen);
            Assert.Equal("loan", money.Action);
            Assert.Equal(2_000m, money.Amount);
            Assert.True(step.Chosen);
            Assert.Equal("money", step.Kind);
            Assert.Equal("loan 2000", step.Detail);
            Assert.Equal("Next money loan 2000", Playbook.Line(step));
        }
        finally
        {
            Autopilot.Enabled = false;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_stage_open_where_layout_prices_supply_managers_money_playbook_curves_locks_fitted_demand_and_cost()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → stage → open → where → layout → prices → supply → managers → money → playbook → curves
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = new AuditGuard(
            enabled: true,
            dryRun: false,
            cash: Stage.Stage2Cash,
            dailyCost: 10m,
            truckCost: 1,
            build: LoadLog.PinnedBuild);
        var needs = new[]
        {
            new GoodsNeed("ba:street_a 1", "ba:itemname_cheapgift"),
            new GoodsNeed("ba:street_b 2", "ba:itemname_cheapgift"),
            new GoodsNeed("ba:street_c 3", "ba:itemname_cheapgift"),
        };
        var holds = new[]
        {
            new WarehouseHold("W12", "ba:street_twelfthstreet", 6, "ba:itemname_cheapgift", 40),
        };
        var world = new AuditWorld(
            prefix.World.Stops,
            holds,
            needs,
            prefix.World.Sales,
            prefix.World.Slots,
            prefix.World.Drivers,
            prefix.World.Levels,
            prefix.World.PricingPlan,
            prefix.World.Seats,
            prefix.World.Hired,
            prefix.World.Stores,
            prefix.World.Staffed);
        var report = Audit.Check(dir.Path, 1016, prefix.Api, world, guard, heal => HealFate.Done);
        var stage = Stage.Read(
            TopBar.Day(prefix.Rolled),
            guard.Cash,
            world.Needs,
            report.Stops,
            world.Holds,
            world.Slots);
        var open = Open.Decide(
            stage,
            new[]
            {
                new OpenCandidate(
                    "ba:businesstype_giftshop",
                    "goods",
                    90m,
                    wholesalerSells: true,
                    "specialty",
                    0,
                    true),
            },
            truckLoadFraction: 0m);
        var reserve = Money.Reserve(guard.DailyCost);
        var site = Where.Decide(
            new[]
            {
                new SiteCandidate("ba:street_gift", 5, familyOnStreet: Where.ClusterMin, traffic: 12m, dailyRent: 20m, cost: 80m),
            },
            guard.Cash,
            reserve);
        var golden = Layout.Decide(
            stage,
            open.Type,
            "small",
            new[]
            {
                new OwnedLayout(open.Type, "small", "GiftShopBest", 70),
            },
            Starter.StageOne());
        var price = Prices.Decide(
            "ba:itemname_cheapgift",
            currentPrice: 20m,
            defaultPrice: 10m,
            wholesale: 4m,
            priorUnits: 8,
            currentUnits: 9,
            satisfaction: Prices.PriceSatisfied,
            trials: new[]
            {
                new ItemSoldPerPriceEntry("ba:itemname_cheapgift", 18f, 9),
            });
        var supply = Supply.Decide(
            stage,
            site.Street + " " + site.Number.ToString(CultureInfo.InvariantCulture),
            "ba:itemname_cheapgift",
            world.Holds,
            report.Stops);
        var managers = Managers.Decide(
            stage,
            new[] { supply.Warehouse },
            employees: 10,
            openRoles: 2,
            seats: Array.Empty<ManagerSeat>(),
            pricingPlanExists: false);
        var money = Money.Decide(
            stage,
            guard.Cash,
            guard.DailyCost,
            projectedDailyProfit: open.Type.Length,
            offers: new[] { new LoanOffer(2_000m, 80) },
            loan: null,
            bestPaybackRate: 0.1m);
        var step = Playbook.Decide(
            open,
            site,
            golden,
            price,
            supply,
            TruckPick.None(),
            WarehousePick.None(),
            managers,
            money,
            EstatePick.None(),
            ClosePick.None(),
            RunPick.None());
        var curves = Curves.Fit(
            new[]
            {
                new FinancialDay(
                    1015,
                    new[]
                    {
                        new StatementRow(site.Street, site.Number, 100f, 20f, 10f, 5f, 0f, 0f, 65f),
                    }),
                new FinancialDay(
                    1016,
                    new[]
                    {
                        new StatementRow(site.Street, site.Number, 200f, 40f, 10f, 5f, 0f, 0f, 145f),
                    }),
            });
        var projected = Curves.Project(curves[0]);

        Assert.Contains("day=1016", prefix.Rolled);
        Assert.Equal(2, stage);
        Assert.True(step.Chosen);
        Assert.Equal("open", step.Kind);
        Assert.Equal("ba:businesstype_giftshop", step.Detail);
        Assert.Single(curves);
        Assert.Equal(site.Street, curves[0].Street);
        Assert.Equal(site.Number, curves[0].Number);
        Assert.Equal(150m, curves[0].DailySales);
        Assert.Equal(0.2m, curves[0].GoodsPerSale);
        Assert.Equal(10m, curves[0].DailyWages);
        Assert.Equal(5m, curves[0].DailyRent);
        Assert.Equal(150f, projected.Sales);
        Assert.Equal(30f, projected.Goods);
        Assert.Equal(105f, projected.Profit);
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_stage_open_where_layout_prices_supply_managers_money_playbook_curves_day_step_locks_projected_rank()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → stage → open → where → layout → prices → supply → managers → money → playbook → curves → day-step
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = new AuditGuard(
            enabled: true,
            dryRun: false,
            cash: Stage.Stage2Cash,
            dailyCost: 10m,
            truckCost: 1,
            build: LoadLog.PinnedBuild);
        var needs = new[]
        {
            new GoodsNeed("ba:street_a 1", "ba:itemname_cheapgift"),
            new GoodsNeed("ba:street_b 2", "ba:itemname_cheapgift"),
            new GoodsNeed("ba:street_c 3", "ba:itemname_cheapgift"),
        };
        var holds = new[]
        {
            new WarehouseHold("W12", "ba:street_twelfthstreet", 6, "ba:itemname_cheapgift", 40),
        };
        var world = new AuditWorld(
            prefix.World.Stops,
            holds,
            needs,
            prefix.World.Sales,
            prefix.World.Slots,
            prefix.World.Drivers,
            prefix.World.Levels,
            prefix.World.PricingPlan,
            prefix.World.Seats,
            prefix.World.Hired,
            prefix.World.Stores,
            prefix.World.Staffed);
        var report = Audit.Check(dir.Path, 1016, prefix.Api, world, guard, heal => HealFate.Done);
        var stage = Stage.Read(
            TopBar.Day(prefix.Rolled),
            guard.Cash,
            world.Needs,
            report.Stops,
            world.Holds,
            world.Slots);
        var open = Open.Decide(
            stage,
            new[]
            {
                new OpenCandidate(
                    "ba:businesstype_giftshop",
                    "goods",
                    90m,
                    wholesalerSells: true,
                    "specialty",
                    0,
                    true),
            },
            truckLoadFraction: 0m);
        var reserve = Money.Reserve(guard.DailyCost);
        var site = Where.Decide(
            new[]
            {
                new SiteCandidate("ba:street_gift", 5, familyOnStreet: Where.ClusterMin, traffic: 12m, dailyRent: 20m, cost: 80m),
            },
            guard.Cash,
            reserve);
        var golden = Layout.Decide(
            stage,
            open.Type,
            "small",
            new[]
            {
                new OwnedLayout(open.Type, "small", "GiftShopBest", 70),
            },
            Starter.StageOne());
        var price = Prices.Decide(
            "ba:itemname_cheapgift",
            currentPrice: 20m,
            defaultPrice: 10m,
            wholesale: 4m,
            priorUnits: 8,
            currentUnits: 9,
            satisfaction: Prices.PriceSatisfied,
            trials: new[]
            {
                new ItemSoldPerPriceEntry("ba:itemname_cheapgift", 18f, 9),
            });
        var supply = Supply.Decide(
            stage,
            site.Street + " " + site.Number.ToString(CultureInfo.InvariantCulture),
            "ba:itemname_cheapgift",
            world.Holds,
            report.Stops);
        var managers = Managers.Decide(
            stage,
            new[] { supply.Warehouse },
            employees: 10,
            openRoles: 2,
            seats: Array.Empty<ManagerSeat>(),
            pricingPlanExists: false);
        var money = Money.Decide(
            stage,
            guard.Cash,
            guard.DailyCost,
            projectedDailyProfit: open.Type.Length,
            offers: new[] { new LoanOffer(2_000m, 80) },
            loan: null,
            bestPaybackRate: 0.1m);
        var step = Playbook.Decide(
            open,
            site,
            golden,
            price,
            supply,
            TruckPick.None(),
            WarehousePick.None(),
            managers,
            money,
            EstatePick.None(),
            ClosePick.None(),
            RunPick.None());
        var curves = Curves.Fit(
            new[]
            {
                new FinancialDay(
                    1015,
                    new[]
                    {
                        new StatementRow(site.Street, site.Number, 100f, 20f, 10f, 5f, 0f, 0f, 65f),
                        new StatementRow("ba:street_low", 1, 10f, 5f, 20f, 5f, 0f, 0f, -20f),
                    }),
                new FinancialDay(
                    1016,
                    new[]
                    {
                        new StatementRow(site.Street, site.Number, 200f, 40f, 10f, 5f, 0f, 0f, 145f),
                        new StatementRow("ba:street_low", 1, 10f, 5f, 20f, 5f, 0f, 0f, -20f),
                    }),
            });
        var projected = Curves.Project(curves[0]);
        var days = DayStep.Run(1016, 2, curves);
        var portfolio = new[]
        {
            new PortfolioBusiness("Gift", "specialty", site.Street, site.Number),
            new PortfolioBusiness("Low", "food", "ba:street_low", 1),
        };
        var ranked = DayStep.Rank(days, portfolio);
        var rows = DayStep.Portfolio(days, portfolio);

        Assert.Contains("day=1016", prefix.Rolled);
        Assert.Equal(2, stage);
        Assert.True(step.Chosen);
        Assert.Equal("open", step.Kind);
        Assert.Equal("ba:businesstype_giftshop", step.Detail);
        Assert.Equal(2, curves.Count);
        Assert.Equal(site.Street, curves[0].Street);
        Assert.Equal(150m, curves[0].DailySales);
        Assert.Equal(0.2m, curves[0].GoodsPerSale);
        Assert.Equal(150f, projected.Sales);
        Assert.Equal(30f, projected.Goods);
        Assert.Equal(105f, projected.Profit);
        Assert.Equal(new[] { 1016, 1017 }, days.Select(day => day.Day).ToArray());
        Assert.Equal(2, days[0].Rows.Count);
        Assert.Equal(150f, days[0].Rows[0].Sales);
        Assert.Equal(105f, days[0].Rows[0].Profit);
        Assert.Equal(new[] { "Gift", "Low" }, ranked.Select(row => row.Business).ToArray());
        Assert.Equal(105d, ranked[0].Profit);
        Assert.Equal(-20d, ranked[1].Profit);
        Assert.Equal(2, rows.Count);
        Assert.Equal("Gift", rows[0].Business);
        Assert.Equal("specialty", rows[0].Family);
        Assert.Equal(site.Street, rows[0].Street);
        Assert.Equal(site.Number, rows[0].Number);
        Assert.Equal(45d, rows[0].Tco);
        Assert.Equal("Low", rows[1].Business);
        Assert.Equal(30d, rows[1].Tco);
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_stage_open_where_layout_prices_supply_managers_money_playbook_curves_file_locks_ledger_columns()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → stage → open → where → layout → prices → supply → managers → money → playbook → curves
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var stage = Stage.Read(
                TopBar.Day(shot.Rolled),
                guard.Cash,
                world.Needs,
                world.Stops,
                world.Holds,
                world.Slots);
            var open = Open.Decide(stage, Array.Empty<OpenCandidate>(), truckLoadFraction: 0m);
            var reserve = Money.Reserve(guard.DailyCost);
            var site = Where.Decide(Array.Empty<SiteCandidate>(), guard.Cash, reserve);
            var golden = Layout.Decide(stage, "", "small", Array.Empty<OwnedLayout>(), Starter.StageOne());
            var price = Prices.Decide(
                "",
                currentPrice: 0m,
                defaultPrice: 10m,
                wholesale: 4m,
                priorUnits: 8,
                currentUnits: 8,
                satisfaction: 0m,
                trials: Array.Empty<ItemSoldPerPriceEntry>());
            var supply = Supply.Decide(stage, "", "ba:itemname_cheapgift", world.Holds, world.Stops);
            var managers = Managers.Decide(
                stage,
                Array.Empty<string>(),
                employees: 0,
                openRoles: 0,
                seats: Array.Empty<ManagerSeat>(),
                pricingPlanExists: true);
            var money = Money.Decide(
                stage,
                guard.Cash,
                guard.DailyCost,
                projectedDailyProfit: 200m,
                offers: new[]
                {
                    new LoanOffer(1_000m, 80),
                    new LoanOffer(2_000m, 100),
                },
                loan: null,
                bestPaybackRate: 0.1m);
            var step = Playbook.Decide(
                open,
                site,
                golden,
                price,
                supply,
                TruckPick.None(),
                WarehousePick.None(),
                managers,
                money,
                EstatePick.None(),
                ClosePick.None(),
                RunPick.None());
            var ledger = Path.Combine(dir.Path, "ledger-statements.csv");
            File.WriteAllText(
                ledger,
                "day,street,number,sales,goods,wages,rent,marketing,theft,profit\n"
                + "1015,ba:street_gift,5,80,16,8,4,1,0.5,50.5\n"
                + "1016,ba:street_gift,5,120,24,8,4,1,0.5,82.5\n");
            var curves = Curves.FitFile(ledger);
            var projected = Curves.Project(curves[0]);

            Assert.Contains("day=1016", shot.Rolled);
            Assert.True(step.Chosen);
            Assert.Equal("money", step.Kind);
            Assert.Single(curves);
            Assert.Equal(100m, curves[0].DailySales);
            Assert.Equal(0.2m, curves[0].GoodsPerSale);
            Assert.Equal(8m, curves[0].DailyWages);
            Assert.Equal(4m, curves[0].DailyRent);
            Assert.Equal(1m, curves[0].DailyMarketing);
            Assert.Equal(0.5m, curves[0].DailyTheft);
            Assert.Equal(100f, projected.Sales);
            Assert.Equal(20f, projected.Goods);
            Assert.Equal(66.5f, projected.Profit);
        }
        finally
        {
            Autopilot.Enabled = false;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_stage_open_where_layout_prices_supply_managers_money_playbook_curves_file_day_step_locks_portfolio_tco()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → stage → open → where → layout → prices → supply → managers → money → playbook → curves → day-step
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var stage = Stage.Read(
                TopBar.Day(shot.Rolled),
                guard.Cash,
                world.Needs,
                world.Stops,
                world.Holds,
                world.Slots);
            var open = Open.Decide(stage, Array.Empty<OpenCandidate>(), truckLoadFraction: 0m);
            var reserve = Money.Reserve(guard.DailyCost);
            var site = Where.Decide(Array.Empty<SiteCandidate>(), guard.Cash, reserve);
            var golden = Layout.Decide(stage, "", "small", Array.Empty<OwnedLayout>(), Starter.StageOne());
            var price = Prices.Decide(
                "",
                currentPrice: 0m,
                defaultPrice: 10m,
                wholesale: 4m,
                priorUnits: 8,
                currentUnits: 8,
                satisfaction: 0m,
                trials: Array.Empty<ItemSoldPerPriceEntry>());
            var supply = Supply.Decide(stage, "", "ba:itemname_cheapgift", world.Holds, world.Stops);
            var managers = Managers.Decide(
                stage,
                Array.Empty<string>(),
                employees: 0,
                openRoles: 0,
                seats: Array.Empty<ManagerSeat>(),
                pricingPlanExists: true);
            var money = Money.Decide(
                stage,
                guard.Cash,
                guard.DailyCost,
                projectedDailyProfit: 200m,
                offers: new[]
                {
                    new LoanOffer(1_000m, 80),
                    new LoanOffer(2_000m, 100),
                },
                loan: null,
                bestPaybackRate: 0.1m);
            var step = Playbook.Decide(
                open,
                site,
                golden,
                price,
                supply,
                TruckPick.None(),
                WarehousePick.None(),
                managers,
                money,
                EstatePick.None(),
                ClosePick.None(),
                RunPick.None());
            var ledger = Path.Combine(dir.Path, "ledger-statements.csv");
            File.WriteAllText(
                ledger,
                "day,street,number,sales,goods,wages,rent,marketing,theft,profit\n"
                + "1015,ba:street_gift,5,80,16,8,4,1,0.5,50.5\n"
                + "1016,ba:street_gift,5,120,24,8,4,1,0.5,82.5\n");
            var curves = Curves.FitFile(ledger);
            var projected = Curves.Project(curves[0]);
            var days = DayStep.Run(1016, 3, curves);
            var portfolio = new[]
            {
                new PortfolioBusiness("Ledger Gift", "specialty", "ba:street_gift", 5),
            };
            var rows = DayStep.Portfolio(days, portfolio);
            var ranked = DayStep.Rank(days, portfolio);

            Assert.Contains("day=1016", shot.Rolled);
            Assert.True(step.Chosen);
            Assert.Equal("money", step.Kind);
            Assert.Single(curves);
            Assert.Equal(100m, curves[0].DailySales);
            Assert.Equal(0.2m, curves[0].GoodsPerSale);
            Assert.Equal(8m, curves[0].DailyWages);
            Assert.Equal(4m, curves[0].DailyRent);
            Assert.Equal(1m, curves[0].DailyMarketing);
            Assert.Equal(0.5m, curves[0].DailyTheft);
            Assert.Equal(100f, projected.Sales);
            Assert.Equal(20f, projected.Goods);
            Assert.Equal(66.5f, projected.Profit);
            Assert.Equal(new[] { 1016, 1017, 1018 }, days.Select(day => day.Day).ToArray());
            Assert.All(days, day =>
            {
                Assert.Single(day.Rows);
                Assert.Equal(100f, day.Rows[0].Sales);
                Assert.Equal(66.5f, day.Rows[0].Profit);
            });
            Assert.Single(rows);
            Assert.Equal("Ledger Gift", rows[0].Business);
            Assert.Equal("specialty", rows[0].Family);
            Assert.Equal("ba:street_gift", rows[0].Street);
            Assert.Equal(5, rows[0].Number);
            Assert.Equal(33d, rows[0].Tco);
            Assert.Single(ranked);
            Assert.Equal("Ledger Gift", ranked[0].Business);
            Assert.Equal(66.5d, ranked[0].Profit);
        }
        finally
        {
            Autopilot.Enabled = false;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_stage_open_where_layout_prices_supply_managers_money_playbook_curves_day_step_calibrate_locks_within_tolerance()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → stage → open → where → layout → prices → supply → managers → money → playbook → curves → day-step → calibrate
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = new AuditGuard(
            enabled: true,
            dryRun: false,
            cash: Stage.Stage2Cash,
            dailyCost: 10m,
            truckCost: 1,
            build: LoadLog.PinnedBuild);
        var needs = new[]
        {
            new GoodsNeed("ba:street_a 1", "ba:itemname_cheapgift"),
            new GoodsNeed("ba:street_b 2", "ba:itemname_cheapgift"),
            new GoodsNeed("ba:street_c 3", "ba:itemname_cheapgift"),
        };
        var holds = new[]
        {
            new WarehouseHold("W12", "ba:street_twelfthstreet", 6, "ba:itemname_cheapgift", 40),
        };
        var world = new AuditWorld(
            prefix.World.Stops,
            holds,
            needs,
            prefix.World.Sales,
            prefix.World.Slots,
            prefix.World.Drivers,
            prefix.World.Levels,
            prefix.World.PricingPlan,
            prefix.World.Seats,
            prefix.World.Hired,
            prefix.World.Stores,
            prefix.World.Staffed);
        var report = Audit.Check(dir.Path, 1016, prefix.Api, world, guard, heal => HealFate.Done);
        var stage = Stage.Read(
            TopBar.Day(prefix.Rolled),
            guard.Cash,
            world.Needs,
            report.Stops,
            world.Holds,
            world.Slots);
        var open = Open.Decide(
            stage,
            new[]
            {
                new OpenCandidate(
                    "ba:businesstype_giftshop",
                    "goods",
                    90m,
                    wholesalerSells: true,
                    "specialty",
                    0,
                    true),
            },
            truckLoadFraction: 0m);
        var reserve = Money.Reserve(guard.DailyCost);
        var site = Where.Decide(
            new[]
            {
                new SiteCandidate("ba:street_gift", 5, familyOnStreet: Where.ClusterMin, traffic: 12m, dailyRent: 20m, cost: 80m),
            },
            guard.Cash,
            reserve);
        var golden = Layout.Decide(
            stage,
            open.Type,
            "small",
            new[]
            {
                new OwnedLayout(open.Type, "small", "GiftShopBest", 70),
            },
            Starter.StageOne());
        var price = Prices.Decide(
            "ba:itemname_cheapgift",
            currentPrice: 20m,
            defaultPrice: 10m,
            wholesale: 4m,
            priorUnits: 8,
            currentUnits: 9,
            satisfaction: Prices.PriceSatisfied,
            trials: new[]
            {
                new ItemSoldPerPriceEntry("ba:itemname_cheapgift", 18f, 9),
            });
        var supply = Supply.Decide(
            stage,
            site.Street + " " + site.Number.ToString(CultureInfo.InvariantCulture),
            "ba:itemname_cheapgift",
            world.Holds,
            report.Stops);
        var managers = Managers.Decide(
            stage,
            new[] { supply.Warehouse },
            employees: 10,
            openRoles: 2,
            seats: Array.Empty<ManagerSeat>(),
            pricingPlanExists: false);
        var money = Money.Decide(
            stage,
            guard.Cash,
            guard.DailyCost,
            projectedDailyProfit: open.Type.Length,
            offers: new[] { new LoanOffer(2_000m, 80) },
            loan: null,
            bestPaybackRate: 0.1m);
        var step = Playbook.Decide(
            open,
            site,
            golden,
            price,
            supply,
            TruckPick.None(),
            WarehousePick.None(),
            managers,
            money,
            EstatePick.None(),
            ClosePick.None(),
            RunPick.None());
        var ledger = new[]
        {
            new FinancialDay(
                Calibrate.StartDay,
                new[]
                {
                    new StatementRow(site.Street, site.Number, 100f, 20f, 10f, 5f, 0f, 0f, 65f),
                    new StatementRow("ba:street_low", 1, 10f, 5f, 20f, 5f, 0f, 0f, -20f),
                }),
            new FinancialDay(
                Calibrate.EndDay,
                new[]
                {
                    new StatementRow(site.Street, site.Number, 110f, 22f, 10f, 5f, 0f, 0f, 73f),
                    new StatementRow("ba:street_low", 1, 10f, 5f, 20f, 5f, 0f, 0f, -20f),
                }),
        };
        var curves = Curves.Fit(new[] { ledger[0] });
        var days = DayStep.Run(Calibrate.StartDay, Calibrate.EndDay - Calibrate.StartDay + 1, curves);
        var calibrated = Calibrate.Check(ledger);

        Assert.Contains("day=1016", prefix.Rolled);
        Assert.Equal(2, stage);
        Assert.True(step.Chosen);
        Assert.Equal("open", step.Kind);
        Assert.Equal(Calibrate.EndDay, days[^1].Day);
        Assert.Equal(2, days[^1].Rows.Count);
        Assert.Equal(65f, days[^1].Rows[0].Profit);
        Assert.Equal(-20f, days[^1].Rows[1].Profit);
        Assert.Equal(45m, calibrated.Projected);
        Assert.Equal(53m, calibrated.Actual);
        Assert.True(calibrated.Error <= Calibrate.Tolerance);
        Assert.True(calibrated.Within);
        Assert.Equal((decimal)days[^1].Rows.Sum(row => row.Profit), calibrated.Projected);
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_stage_open_where_layout_prices_supply_managers_money_playbook_curves_file_day_step_calibrate_locks_file_within()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → stage → open → where → layout → prices → supply → managers → money → playbook → curves → day-step → calibrate
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var stage = Stage.Read(
                TopBar.Day(shot.Rolled),
                guard.Cash,
                world.Needs,
                world.Stops,
                world.Holds,
                world.Slots);
            var open = Open.Decide(stage, Array.Empty<OpenCandidate>(), truckLoadFraction: 0m);
            var reserve = Money.Reserve(guard.DailyCost);
            var site = Where.Decide(Array.Empty<SiteCandidate>(), guard.Cash, reserve);
            var golden = Layout.Decide(stage, "", "small", Array.Empty<OwnedLayout>(), Starter.StageOne());
            var price = Prices.Decide(
                "",
                currentPrice: 0m,
                defaultPrice: 10m,
                wholesale: 4m,
                priorUnits: 8,
                currentUnits: 8,
                satisfaction: 0m,
                trials: Array.Empty<ItemSoldPerPriceEntry>());
            var supply = Supply.Decide(stage, "", "ba:itemname_cheapgift", world.Holds, world.Stops);
            var managers = Managers.Decide(
                stage,
                Array.Empty<string>(),
                employees: 0,
                openRoles: 0,
                seats: Array.Empty<ManagerSeat>(),
                pricingPlanExists: true);
            var money = Money.Decide(
                stage,
                guard.Cash,
                guard.DailyCost,
                projectedDailyProfit: 200m,
                offers: new[]
                {
                    new LoanOffer(1_000m, 80),
                    new LoanOffer(2_000m, 100),
                },
                loan: null,
                bestPaybackRate: 0.1m);
            var step = Playbook.Decide(
                open,
                site,
                golden,
                price,
                supply,
                TruckPick.None(),
                WarehousePick.None(),
                managers,
                money,
                EstatePick.None(),
                ClosePick.None(),
                RunPick.None());
            var ledger = Path.Combine(dir.Path, "calibrate-statements.csv");
            File.WriteAllText(
                ledger,
                "day,street,number,sales,goods,wages,rent,marketing,theft,profit\n"
                + "1000,ba:street_gift,5,100,20,10,5,0,0,65\n"
                + "1014,ba:street_gift,5,100,20,10,5,0,0,65\n");
            var curves = Curves.FitFile(ledger);
            var days = DayStep.Run(Calibrate.StartDay, Calibrate.EndDay - Calibrate.StartDay + 1, curves);
            var calibrated = Calibrate.CheckFile(ledger);

            Assert.Contains("day=1016", shot.Rolled);
            Assert.True(step.Chosen);
            Assert.Equal("money", step.Kind);
            Assert.Equal(Calibrate.EndDay, days[^1].Day);
            Assert.Equal(65f, days[^1].Rows[0].Profit);
            Assert.Equal(65m, calibrated.Projected);
            Assert.Equal(65m, calibrated.Actual);
            Assert.Equal(0m, calibrated.Error);
            Assert.True(calibrated.Within);
        }
        finally
        {
            Autopilot.Enabled = false;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_stage_open_where_layout_prices_supply_managers_money_playbook_curves_day_step_calibrate_noise_locks_net_worth_distribution()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → stage → open → where → layout → prices → supply → managers → money → playbook → curves → day-step → calibrate → noise
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = new AuditGuard(
            enabled: true,
            dryRun: false,
            cash: Stage.Stage2Cash,
            dailyCost: 10m,
            truckCost: 1,
            build: LoadLog.PinnedBuild);
        var needs = new[]
        {
            new GoodsNeed("ba:street_a 1", "ba:itemname_cheapgift"),
            new GoodsNeed("ba:street_b 2", "ba:itemname_cheapgift"),
            new GoodsNeed("ba:street_c 3", "ba:itemname_cheapgift"),
        };
        var holds = new[]
        {
            new WarehouseHold("W12", "ba:street_twelfthstreet", 6, "ba:itemname_cheapgift", 40),
        };
        var world = new AuditWorld(
            prefix.World.Stops,
            holds,
            needs,
            prefix.World.Sales,
            prefix.World.Slots,
            prefix.World.Drivers,
            prefix.World.Levels,
            prefix.World.PricingPlan,
            prefix.World.Seats,
            prefix.World.Hired,
            prefix.World.Stores,
            prefix.World.Staffed);
        var report = Audit.Check(dir.Path, 1016, prefix.Api, world, guard, heal => HealFate.Done);
        var stage = Stage.Read(
            TopBar.Day(prefix.Rolled),
            guard.Cash,
            world.Needs,
            report.Stops,
            world.Holds,
            world.Slots);
        var open = Open.Decide(
            stage,
            new[]
            {
                new OpenCandidate(
                    "ba:businesstype_giftshop",
                    "goods",
                    90m,
                    wholesalerSells: true,
                    "specialty",
                    0,
                    true),
            },
            truckLoadFraction: 0m);
        var reserve = Money.Reserve(guard.DailyCost);
        var site = Where.Decide(
            new[]
            {
                new SiteCandidate("ba:street_gift", 5, familyOnStreet: Where.ClusterMin, traffic: 12m, dailyRent: 20m, cost: 80m),
            },
            guard.Cash,
            reserve);
        var golden = Layout.Decide(
            stage,
            open.Type,
            "small",
            new[]
            {
                new OwnedLayout(open.Type, "small", "GiftShopBest", 70),
            },
            Starter.StageOne());
        var price = Prices.Decide(
            "ba:itemname_cheapgift",
            currentPrice: 20m,
            defaultPrice: 10m,
            wholesale: 4m,
            priorUnits: 8,
            currentUnits: 9,
            satisfaction: Prices.PriceSatisfied,
            trials: new[]
            {
                new ItemSoldPerPriceEntry("ba:itemname_cheapgift", 18f, 9),
            });
        var supply = Supply.Decide(
            stage,
            site.Street + " " + site.Number.ToString(CultureInfo.InvariantCulture),
            "ba:itemname_cheapgift",
            world.Holds,
            report.Stops);
        var managers = Managers.Decide(
            stage,
            new[] { supply.Warehouse },
            employees: 10,
            openRoles: 2,
            seats: Array.Empty<ManagerSeat>(),
            pricingPlanExists: false);
        var money = Money.Decide(
            stage,
            guard.Cash,
            guard.DailyCost,
            projectedDailyProfit: open.Type.Length,
            offers: new[] { new LoanOffer(2_000m, 80) },
            loan: null,
            bestPaybackRate: 0.1m);
        var step = Playbook.Decide(
            open,
            site,
            golden,
            price,
            supply,
            TruckPick.None(),
            WarehousePick.None(),
            managers,
            money,
            EstatePick.None(),
            ClosePick.None(),
            RunPick.None());
        var ledger = new[]
        {
            new FinancialDay(
                Calibrate.StartDay,
                new[]
                {
                    new StatementRow(site.Street, site.Number, 100f, 20f, 10f, 5f, 0f, 0f, 65f),
                    new StatementRow("ba:street_low", 1, 10f, 5f, 20f, 5f, 0f, 0f, -20f),
                }),
            new FinancialDay(
                Calibrate.EndDay,
                new[]
                {
                    new StatementRow(site.Street, site.Number, 110f, 22f, 10f, 5f, 0f, 0f, 73f),
                    new StatementRow("ba:street_low", 1, 10f, 5f, 20f, 5f, 0f, 0f, -20f),
                }),
        };
        var curves = Curves.Fit(new[] { ledger[0] });
        var days = DayStep.Run(Calibrate.StartDay, Calibrate.EndDay - Calibrate.StartDay + 1, curves);
        var calibrated = Calibrate.Check(ledger);
        var noise = Noise.Run(curves, startNetWorth: 1000m, seeds: new[] { 1, 2, 3 });

        Assert.Contains("day=1016", prefix.Rolled);
        Assert.Equal(2, stage);
        Assert.True(step.Chosen);
        Assert.Equal("open", step.Kind);
        Assert.Equal(Calibrate.EndDay, days[^1].Day);
        Assert.True(calibrated.Within);
        Assert.Equal((decimal)days[^1].Rows.Sum(row => row.Profit), calibrated.Projected);
        Assert.Equal(Noise.SimHorizonDays, noise.HorizonDays);
        Assert.Equal(3, noise.Trials);
        Assert.Equal(16519.7092619406m, decimal.Round(noise.Worst, 10));
        Assert.Equal(16679.8322483867m, decimal.Round(noise.Median, 10));
        Assert.Equal(16683.6276121568m, decimal.Round(noise.Best, 10));
        Assert.Equal(noise.Median, Noise.Trial(curves, 1000m, 3));
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_stage_open_where_layout_prices_supply_managers_money_playbook_curves_file_day_step_calibrate_noise_locks_file_distribution()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → stage → open → where → layout → prices → supply → managers → money → playbook → curves → day-step → calibrate → noise
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var stage = Stage.Read(
                TopBar.Day(shot.Rolled),
                guard.Cash,
                world.Needs,
                world.Stops,
                world.Holds,
                world.Slots);
            var open = Open.Decide(stage, Array.Empty<OpenCandidate>(), truckLoadFraction: 0m);
            var reserve = Money.Reserve(guard.DailyCost);
            var site = Where.Decide(Array.Empty<SiteCandidate>(), guard.Cash, reserve);
            var golden = Layout.Decide(stage, "", "small", Array.Empty<OwnedLayout>(), Starter.StageOne());
            var price = Prices.Decide(
                "",
                currentPrice: 0m,
                defaultPrice: 10m,
                wholesale: 4m,
                priorUnits: 8,
                currentUnits: 8,
                satisfaction: 0m,
                trials: Array.Empty<ItemSoldPerPriceEntry>());
            var supply = Supply.Decide(stage, "", "ba:itemname_cheapgift", world.Holds, world.Stops);
            var managers = Managers.Decide(
                stage,
                Array.Empty<string>(),
                employees: 0,
                openRoles: 0,
                seats: Array.Empty<ManagerSeat>(),
                pricingPlanExists: true);
            var money = Money.Decide(
                stage,
                guard.Cash,
                guard.DailyCost,
                projectedDailyProfit: 200m,
                offers: new[]
                {
                    new LoanOffer(1_000m, 80),
                    new LoanOffer(2_000m, 100),
                },
                loan: null,
                bestPaybackRate: 0.1m);
            var step = Playbook.Decide(
                open,
                site,
                golden,
                price,
                supply,
                TruckPick.None(),
                WarehousePick.None(),
                managers,
                money,
                EstatePick.None(),
                ClosePick.None(),
                RunPick.None());
            var ledger = Path.Combine(dir.Path, "noise-statements.csv");
            File.WriteAllText(
                ledger,
                "day,street,number,sales,goods,wages,rent,marketing,theft,profit\n"
                + "1000,ba:street_gift,5,100,20,10,5,0,0,65\n"
                + "1014,ba:street_gift,5,100,20,10,5,0,0,65\n");
            var curves = Curves.FitFile(ledger);
            var days = DayStep.Run(Calibrate.StartDay, Calibrate.EndDay - Calibrate.StartDay + 1, curves);
            var calibrated = Calibrate.CheckFile(ledger);
            var noise = Noise.Run(curves, startNetWorth: 500m, seeds: new[] { 5, 6, 7 });

            Assert.Contains("day=1016", shot.Rolled);
            Assert.True(step.Chosen);
            Assert.Equal("money", step.Kind);
            Assert.Equal(Calibrate.EndDay, days[^1].Day);
            Assert.True(calibrated.Within);
            Assert.Equal(Noise.SimHorizonDays, noise.HorizonDays);
            Assert.Equal(3, noise.Trials);
            Assert.Equal(23366.8279699935m, decimal.Round(noise.Worst, 10));
            Assert.Equal(23513.5405050029m, decimal.Round(noise.Median, 10));
            Assert.Equal(23520.1059412474m, decimal.Round(noise.Best, 10));
            Assert.Equal(noise.Worst, Noise.Trial(curves, 500m, 5));
        }
        finally
        {
            Autopilot.Enabled = false;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_stage_open_where_layout_prices_supply_managers_money_playbook_curves_day_step_calibrate_noise_policy_picks_close_over_keep()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → stage → open → where → layout → prices → supply → managers → money → playbook → curves → day-step → calibrate → noise → policy
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = new AuditGuard(
            enabled: true,
            dryRun: false,
            cash: Stage.Stage2Cash,
            dailyCost: 10m,
            truckCost: 1,
            build: LoadLog.PinnedBuild);
        var needs = new[]
        {
            new GoodsNeed("ba:street_a 1", "ba:itemname_cheapgift"),
            new GoodsNeed("ba:street_b 2", "ba:itemname_cheapgift"),
            new GoodsNeed("ba:street_c 3", "ba:itemname_cheapgift"),
        };
        var holds = new[]
        {
            new WarehouseHold("W12", "ba:street_twelfthstreet", 6, "ba:itemname_cheapgift", 40),
        };
        var world = new AuditWorld(
            prefix.World.Stops,
            holds,
            needs,
            prefix.World.Sales,
            prefix.World.Slots,
            prefix.World.Drivers,
            prefix.World.Levels,
            prefix.World.PricingPlan,
            prefix.World.Seats,
            prefix.World.Hired,
            prefix.World.Stores,
            prefix.World.Staffed);
        var report = Audit.Check(dir.Path, 1016, prefix.Api, world, guard, heal => HealFate.Done);
        var stage = Stage.Read(
            TopBar.Day(prefix.Rolled),
            guard.Cash,
            world.Needs,
            report.Stops,
            world.Holds,
            world.Slots);
        var open = Open.Decide(
            stage,
            new[]
            {
                new OpenCandidate(
                    "ba:businesstype_giftshop",
                    "goods",
                    90m,
                    wholesalerSells: true,
                    "specialty",
                    0,
                    true),
            },
            truckLoadFraction: 0m);
        var reserve = Money.Reserve(guard.DailyCost);
        var site = Where.Decide(
            new[]
            {
                new SiteCandidate("ba:street_gift", 5, familyOnStreet: Where.ClusterMin, traffic: 12m, dailyRent: 20m, cost: 80m),
            },
            guard.Cash,
            reserve);
        var golden = Layout.Decide(
            stage,
            open.Type,
            "small",
            new[]
            {
                new OwnedLayout(open.Type, "small", "GiftShopBest", 70),
            },
            Starter.StageOne());
        var price = Prices.Decide(
            "ba:itemname_cheapgift",
            currentPrice: 20m,
            defaultPrice: 10m,
            wholesale: 4m,
            priorUnits: 8,
            currentUnits: 9,
            satisfaction: Prices.PriceSatisfied,
            trials: new[]
            {
                new ItemSoldPerPriceEntry("ba:itemname_cheapgift", 18f, 9),
            });
        var supply = Supply.Decide(
            stage,
            site.Street + " " + site.Number.ToString(CultureInfo.InvariantCulture),
            "ba:itemname_cheapgift",
            world.Holds,
            report.Stops);
        var managers = Managers.Decide(
            stage,
            new[] { supply.Warehouse },
            employees: 10,
            openRoles: 2,
            seats: Array.Empty<ManagerSeat>(),
            pricingPlanExists: false);
        var money = Money.Decide(
            stage,
            guard.Cash,
            guard.DailyCost,
            projectedDailyProfit: open.Type.Length,
            offers: new[] { new LoanOffer(2_000m, 80) },
            loan: null,
            bestPaybackRate: 0.1m);
        var step = Playbook.Decide(
            open,
            site,
            golden,
            price,
            supply,
            TruckPick.None(),
            WarehousePick.None(),
            managers,
            money,
            EstatePick.None(),
            ClosePick.None(),
            RunPick.None());
        var ledger = new[]
        {
            new FinancialDay(
                Calibrate.StartDay,
                new[]
                {
                    new StatementRow(site.Street, site.Number, 100f, 20f, 10f, 5f, 0f, 0f, 65f),
                    new StatementRow("ba:street_low", 1, 10f, 5f, 20f, 5f, 0f, 0f, -20f),
                }),
            new FinancialDay(
                Calibrate.EndDay,
                new[]
                {
                    new StatementRow(site.Street, site.Number, 110f, 22f, 10f, 5f, 0f, 0f, 73f),
                    new StatementRow("ba:street_low", 1, 10f, 5f, 20f, 5f, 0f, 0f, -20f),
                }),
        };
        var curves = Curves.Fit(new[] { ledger[0] });
        var days = DayStep.Run(Calibrate.StartDay, Calibrate.EndDay - Calibrate.StartDay + 1, curves);
        var calibrated = Calibrate.Check(ledger);
        var seeds = new[] { 1, 2, 3 };
        var noise = Noise.Run(curves, startNetWorth: 1000m, seeds: seeds);
        var keep = new PolicyOption("keep", curves);
        var closeCurves = curves.Where(curve => curve.Street != "ba:street_low").ToList();
        var close = new PolicyOption("close", closeCurves);
        var pick = Policy.Compare(new[] { keep, close }, startNetWorth: 1000m, seeds: seeds);

        Assert.Contains("day=1016", prefix.Rolled);
        Assert.Equal(2, stage);
        Assert.True(step.Chosen);
        Assert.Equal("open", step.Kind);
        Assert.Equal(Calibrate.EndDay, days[^1].Day);
        Assert.True(calibrated.Within);
        Assert.Equal(Noise.SimHorizonDays, noise.HorizonDays);
        Assert.Equal(3, noise.Trials);
        Assert.Equal("close", pick.Name);
        Assert.True(pick.Acceptable);
        Assert.True(pick.Median > noise.Median);
        Assert.Equal(pick.Median, Noise.Run(closeCurves, 1000m, seeds).Median);
        Assert.Equal(pick.Worst, Noise.Run(closeCurves, 1000m, seeds).Worst);
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_stage_open_where_layout_prices_supply_managers_money_playbook_curves_file_day_step_calibrate_noise_policy_keeps_stable_over_gamble()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → stage → open → where → layout → prices → supply → managers → money → playbook → curves → day-step → calibrate → noise → policy
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var stage = Stage.Read(
                TopBar.Day(shot.Rolled),
                guard.Cash,
                world.Needs,
                world.Stops,
                world.Holds,
                world.Slots);
            var open = Open.Decide(stage, Array.Empty<OpenCandidate>(), truckLoadFraction: 0m);
            var reserve = Money.Reserve(guard.DailyCost);
            var site = Where.Decide(Array.Empty<SiteCandidate>(), guard.Cash, reserve);
            var golden = Layout.Decide(stage, "", "small", Array.Empty<OwnedLayout>(), Starter.StageOne());
            var price = Prices.Decide(
                "",
                currentPrice: 0m,
                defaultPrice: 10m,
                wholesale: 4m,
                priorUnits: 8,
                currentUnits: 8,
                satisfaction: 0m,
                trials: Array.Empty<ItemSoldPerPriceEntry>());
            var supply = Supply.Decide(stage, "", "ba:itemname_cheapgift", world.Holds, world.Stops);
            var managers = Managers.Decide(
                stage,
                Array.Empty<string>(),
                employees: 0,
                openRoles: 0,
                seats: Array.Empty<ManagerSeat>(),
                pricingPlanExists: true);
            var money = Money.Decide(
                stage,
                guard.Cash,
                guard.DailyCost,
                projectedDailyProfit: 200m,
                offers: new[]
                {
                    new LoanOffer(1_000m, 80),
                    new LoanOffer(2_000m, 100),
                },
                loan: null,
                bestPaybackRate: 0.1m);
            var step = Playbook.Decide(
                open,
                site,
                golden,
                price,
                supply,
                TruckPick.None(),
                WarehousePick.None(),
                managers,
                money,
                EstatePick.None(),
                ClosePick.None(),
                RunPick.None());
            var ledger = Path.Combine(dir.Path, "policy-statements.csv");
            File.WriteAllText(
                ledger,
                "day,street,number,sales,goods,wages,rent,marketing,theft,profit\n"
                + "1000,ba:street_gift,5,80,16,8,4,0,0,52\n"
                + "1014,ba:street_gift,5,80,16,8,4,0,0,52\n");
            var curves = Curves.FitFile(ledger);
            var days = DayStep.Run(Calibrate.StartDay, Calibrate.EndDay - Calibrate.StartDay + 1, curves);
            var calibrated = Calibrate.CheckFile(ledger);
            var seeds = new[] { 1, 2, 3, 4, 5 };
            var noise = Noise.Run(curves, startNetWorth: 1000m, seeds: seeds, horizonDays: 60);
            var keep = new PolicyOption("keep", curves);
            var gambleCurves = curves
                .Concat(new[]
                {
                    new StoreCurve("ba:street_risk", 2, 300m, 0.05m, 5m, 200m, 0m, 0m, 1),
                })
                .ToList();
            var gamble = new PolicyOption("gamble", gambleCurves);
            var pick = Policy.Compare(
                new[] { keep, gamble },
                startNetWorth: 1000m,
                seeds: seeds,
                horizonDays: 60);

            Assert.Contains("day=1016", shot.Rolled);
            Assert.True(step.Chosen);
            Assert.Equal("money", step.Kind);
            Assert.Equal(Calibrate.EndDay, days[^1].Day);
            Assert.True(calibrated.Within);
            Assert.Equal(60, noise.HorizonDays);
            Assert.True(Policy.Acceptable(noise.Worst, noise.Median));
            Assert.False(Policy.Acceptable(
                Noise.Run(gambleCurves, 1000m, seeds, 60).Worst,
                Noise.Run(gambleCurves, 1000m, seeds, 60).Median));
            Assert.Equal("keep", pick.Name);
            Assert.True(pick.Acceptable);
            Assert.Equal(noise.Median, pick.Median);
            Assert.Equal(noise.Worst, pick.Worst);
        }
        finally
        {
            Autopilot.Enabled = false;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_stage_open_where_layout_prices_supply_managers_money_playbook_curves_day_step_calibrate_noise_policy_stage_sim_reports_win_flags()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → stage → open → where → layout → prices → supply → managers → money → playbook → curves → day-step → calibrate → noise → policy → stage-sim
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = new AuditGuard(
            enabled: true,
            dryRun: false,
            cash: Stage.Stage2Cash,
            dailyCost: 10m,
            truckCost: 1,
            build: LoadLog.PinnedBuild);
        var needs = new[]
        {
            new GoodsNeed("ba:street_a 1", "ba:itemname_cheapgift"),
            new GoodsNeed("ba:street_b 2", "ba:itemname_cheapgift"),
            new GoodsNeed("ba:street_c 3", "ba:itemname_cheapgift"),
        };
        var holds = new[]
        {
            new WarehouseHold("W12", "ba:street_twelfthstreet", 6, "ba:itemname_cheapgift", 40),
        };
        var world = new AuditWorld(
            prefix.World.Stops,
            holds,
            needs,
            prefix.World.Sales,
            prefix.World.Slots,
            prefix.World.Drivers,
            prefix.World.Levels,
            prefix.World.PricingPlan,
            prefix.World.Seats,
            prefix.World.Hired,
            prefix.World.Stores,
            prefix.World.Staffed);
        var report = Audit.Check(dir.Path, 1016, prefix.Api, world, guard, heal => HealFate.Done);
        var stage = Stage.Read(
            TopBar.Day(prefix.Rolled),
            guard.Cash,
            world.Needs,
            report.Stops,
            world.Holds,
            world.Slots);
        var open = Open.Decide(
            stage,
            new[]
            {
                new OpenCandidate(
                    "ba:businesstype_giftshop",
                    "goods",
                    90m,
                    wholesalerSells: true,
                    "specialty",
                    0,
                    true),
            },
            truckLoadFraction: 0m);
        var reserve = Money.Reserve(guard.DailyCost);
        var site = Where.Decide(
            new[]
            {
                new SiteCandidate("ba:street_gift", 5, familyOnStreet: Where.ClusterMin, traffic: 12m, dailyRent: 20m, cost: 80m),
            },
            guard.Cash,
            reserve);
        var golden = Layout.Decide(
            stage,
            open.Type,
            "small",
            new[]
            {
                new OwnedLayout(open.Type, "small", "GiftShopBest", 70),
            },
            Starter.StageOne());
        var price = Prices.Decide(
            "ba:itemname_cheapgift",
            currentPrice: 20m,
            defaultPrice: 10m,
            wholesale: 4m,
            priorUnits: 8,
            currentUnits: 9,
            satisfaction: Prices.PriceSatisfied,
            trials: new[]
            {
                new ItemSoldPerPriceEntry("ba:itemname_cheapgift", 18f, 9),
            });
        var supply = Supply.Decide(
            stage,
            site.Street + " " + site.Number.ToString(CultureInfo.InvariantCulture),
            "ba:itemname_cheapgift",
            world.Holds,
            report.Stops);
        var managers = Managers.Decide(
            stage,
            new[] { supply.Warehouse },
            employees: 10,
            openRoles: 2,
            seats: Array.Empty<ManagerSeat>(),
            pricingPlanExists: false);
        var money = Money.Decide(
            stage,
            guard.Cash,
            guard.DailyCost,
            projectedDailyProfit: open.Type.Length,
            offers: new[] { new LoanOffer(2_000m, 80) },
            loan: null,
            bestPaybackRate: 0.1m);
        var step = Playbook.Decide(
            open,
            site,
            golden,
            price,
            supply,
            TruckPick.None(),
            WarehousePick.None(),
            managers,
            money,
            EstatePick.None(),
            ClosePick.None(),
            RunPick.None());
        var ledger = new[]
        {
            new FinancialDay(
                Calibrate.StartDay,
                new[]
                {
                    new StatementRow(site.Street, site.Number, 100f, 20f, 10f, 5f, 0f, 0f, 65f),
                    new StatementRow("ba:street_low", 1, 10f, 5f, 20f, 5f, 0f, 0f, -20f),
                }),
            new FinancialDay(
                Calibrate.EndDay,
                new[]
                {
                    new StatementRow(site.Street, site.Number, 110f, 22f, 10f, 5f, 0f, 0f, 73f),
                    new StatementRow("ba:street_low", 1, 10f, 5f, 20f, 5f, 0f, 0f, -20f),
                }),
        };
        var curves = Curves.Fit(new[] { ledger[0] });
        var days = DayStep.Run(Calibrate.StartDay, Calibrate.EndDay - Calibrate.StartDay + 1, curves);
        var calibrated = Calibrate.Check(ledger);
        var seeds = new[] { 1, 2, 3 };
        var noise = Noise.Run(curves, startNetWorth: 1000m, seeds: seeds);
        var keep = new PolicyOption("keep", curves);
        var closeCurves = curves.Where(curve => curve.Street != "ba:street_low").ToList();
        var close = new PolicyOption("close", closeCurves);
        var pick = Policy.Compare(new[] { keep, close }, startNetWorth: 1000m, seeds: seeds);
        var opens = new List<OpenCandidate>();
        var sites = new List<SiteCandidate>();
        for (var i = 1; i <= 6; i++)
        {
            opens.Add(new OpenCandidate(
                open.Type,
                "goods",
                400_000m,
                wholesalerSells: true,
                "specialty",
                0,
                true));
            sites.Add(new SiteCandidate(
                site.Street + i.ToString(CultureInfo.InvariantCulture),
                site.Number + i,
                familyOnStreet: Where.ClusterMin,
                traffic: 12m,
                dailyRent: 20m,
                cost: 80m));
        }

        var catalog = new StageSimCatalog(
            opens,
            sites,
            loans: new[] { new LoanOffer(100_000m, 50) },
            estates: Array.Empty<EstateCandidate>(),
            item: "ba:itemname_cheapgift",
            warehouseName: "W12",
            truckCost: 1m);
        var sim = StageSim.Run(
            startCash: guard.Cash,
            startNetWorth: guard.Cash,
            catalog,
            seeds,
            StageThresholds.Starting().With(stage2Cash: 50_000m, stage4Cash: 200_000m),
            horizonDays: 60);

        Assert.Contains("day=1016", prefix.Rolled);
        Assert.Equal(2, stage);
        Assert.True(step.Chosen);
        Assert.Equal("open", step.Kind);
        Assert.Equal(Calibrate.EndDay, days[^1].Day);
        Assert.True(calibrated.Within);
        Assert.Equal(Noise.SimHorizonDays, noise.HorizonDays);
        Assert.Equal("close", pick.Name);
        Assert.True(pick.Acceptable);
        Assert.Equal(60, sim.HorizonDays);
        Assert.Equal(3, sim.Trials);
        Assert.True(sim.WinAtMedian);
        Assert.Equal(sim.Median >= TopBar.WinNetWorth, sim.WinAtMedian);
        Assert.Equal(sim.Worst >= TopBar.WinNetWorth, sim.WinAtWorst);
        Assert.Equal(sim.Median, StageSim.Run(guard.Cash, guard.Cash, catalog, seeds, sim.Thresholds, 60).Median);
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_stage_open_where_layout_prices_supply_managers_money_playbook_curves_day_step_calibrate_noise_policy_stage_sim_win_logs_once()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → stage → open → where → layout → prices → supply → managers → money → playbook → curves → day-step → calibrate → noise → policy → stage-sim → win
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = new AuditGuard(
            enabled: true,
            dryRun: false,
            cash: Stage.Stage2Cash,
            dailyCost: 10m,
            truckCost: 1,
            build: LoadLog.PinnedBuild);
        var needs = new[]
        {
            new GoodsNeed("ba:street_a 1", "ba:itemname_cheapgift"),
            new GoodsNeed("ba:street_b 2", "ba:itemname_cheapgift"),
            new GoodsNeed("ba:street_c 3", "ba:itemname_cheapgift"),
        };
        var holds = new[]
        {
            new WarehouseHold("W12", "ba:street_twelfthstreet", 6, "ba:itemname_cheapgift", 40),
        };
        var world = new AuditWorld(
            prefix.World.Stops,
            holds,
            needs,
            prefix.World.Sales,
            prefix.World.Slots,
            prefix.World.Drivers,
            prefix.World.Levels,
            prefix.World.PricingPlan,
            prefix.World.Seats,
            prefix.World.Hired,
            prefix.World.Stores,
            prefix.World.Staffed);
        var report = Audit.Check(dir.Path, 1016, prefix.Api, world, guard, heal => HealFate.Done);
        var stage = Stage.Read(
            TopBar.Day(prefix.Rolled),
            guard.Cash,
            world.Needs,
            report.Stops,
            world.Holds,
            world.Slots);
        var open = Open.Decide(
            stage,
            new[]
            {
                new OpenCandidate(
                    "ba:businesstype_giftshop",
                    "goods",
                    90m,
                    wholesalerSells: true,
                    "specialty",
                    0,
                    true),
            },
            truckLoadFraction: 0m);
        var reserve = Money.Reserve(guard.DailyCost);
        var site = Where.Decide(
            new[]
            {
                new SiteCandidate("ba:street_gift", 5, familyOnStreet: Where.ClusterMin, traffic: 12m, dailyRent: 20m, cost: 80m),
            },
            guard.Cash,
            reserve);
        var golden = Layout.Decide(
            stage,
            open.Type,
            "small",
            new[]
            {
                new OwnedLayout(open.Type, "small", "GiftShopBest", 70),
            },
            Starter.StageOne());
        var price = Prices.Decide(
            "ba:itemname_cheapgift",
            currentPrice: 20m,
            defaultPrice: 10m,
            wholesale: 4m,
            priorUnits: 8,
            currentUnits: 9,
            satisfaction: Prices.PriceSatisfied,
            trials: new[]
            {
                new ItemSoldPerPriceEntry("ba:itemname_cheapgift", 18f, 9),
            });
        var supply = Supply.Decide(
            stage,
            site.Street + " " + site.Number.ToString(CultureInfo.InvariantCulture),
            "ba:itemname_cheapgift",
            world.Holds,
            report.Stops);
        var managers = Managers.Decide(
            stage,
            new[] { supply.Warehouse },
            employees: 10,
            openRoles: 2,
            seats: Array.Empty<ManagerSeat>(),
            pricingPlanExists: false);
        var money = Money.Decide(
            stage,
            guard.Cash,
            guard.DailyCost,
            projectedDailyProfit: open.Type.Length,
            offers: new[] { new LoanOffer(2_000m, 80) },
            loan: null,
            bestPaybackRate: 0.1m);
        var step = Playbook.Decide(
            open,
            site,
            golden,
            price,
            supply,
            TruckPick.None(),
            WarehousePick.None(),
            managers,
            money,
            EstatePick.None(),
            ClosePick.None(),
            RunPick.None());
        var ledger = new[]
        {
            new FinancialDay(
                Calibrate.StartDay,
                new[]
                {
                    new StatementRow(site.Street, site.Number, 100f, 20f, 10f, 5f, 0f, 0f, 65f),
                    new StatementRow("ba:street_low", 1, 10f, 5f, 20f, 5f, 0f, 0f, -20f),
                }),
            new FinancialDay(
                Calibrate.EndDay,
                new[]
                {
                    new StatementRow(site.Street, site.Number, 110f, 22f, 10f, 5f, 0f, 0f, 73f),
                    new StatementRow("ba:street_low", 1, 10f, 5f, 20f, 5f, 0f, 0f, -20f),
                }),
        };
        var curves = Curves.Fit(new[] { ledger[0] });
        var days = DayStep.Run(Calibrate.StartDay, Calibrate.EndDay - Calibrate.StartDay + 1, curves);
        var calibrated = Calibrate.Check(ledger);
        var seeds = new[] { 1, 2, 3 };
        var noise = Noise.Run(curves, startNetWorth: 1000m, seeds: seeds);
        var keep = new PolicyOption("keep", curves);
        var closeCurves = curves.Where(curve => curve.Street != "ba:street_low").ToList();
        var close = new PolicyOption("close", closeCurves);
        var pick = Policy.Compare(new[] { keep, close }, startNetWorth: 1000m, seeds: seeds);
        var opens = new List<OpenCandidate>();
        var sites = new List<SiteCandidate>();
        for (var i = 1; i <= 6; i++)
        {
            opens.Add(new OpenCandidate(
                open.Type,
                "goods",
                400_000m,
                wholesalerSells: true,
                "specialty",
                0,
                true));
            sites.Add(new SiteCandidate(
                site.Street + i.ToString(CultureInfo.InvariantCulture),
                site.Number + i,
                familyOnStreet: Where.ClusterMin,
                traffic: 12m,
                dailyRent: 20m,
                cost: 80m));
        }

        var catalog = new StageSimCatalog(
            opens,
            sites,
            loans: new[] { new LoanOffer(100_000m, 50) },
            estates: Array.Empty<EstateCandidate>(),
            item: "ba:itemname_cheapgift",
            warehouseName: "W12",
            truckCost: 1m);
        var sim = StageSim.Run(
            startCash: guard.Cash,
            startNetWorth: guard.Cash,
            catalog,
            seeds,
            StageThresholds.Starting().With(stage2Cash: 50_000m, stage4Cash: 200_000m),
            horizonDays: 60);
        Autopilot.Enabled = true;
        Win.ClearApproved();
        var logPath = LoadLog.CeoLogPath(dir.Path);
        var day = TopBar.Day(prefix.Rolled);
        var reached = Win.Record(logPath, day, sim.Median);
        var again = Win.Record(logPath, day + 1, sim.Median);

        Assert.Contains("day=1016", prefix.Rolled);
        Assert.Equal(2, stage);
        Assert.True(step.Chosen);
        Assert.Equal("open", step.Kind);
        Assert.Equal(Calibrate.EndDay, days[^1].Day);
        Assert.True(calibrated.Within);
        Assert.Equal("close", pick.Name);
        Assert.True(sim.WinAtMedian);
        Assert.True(sim.Median >= Win.Milestone);
        Assert.Equal(
            "win.reached version=" + LoadLog.Version
            + " day=" + day.ToString(CultureInfo.InvariantCulture)
            + " netWorth=" + sim.Median.ToString(CultureInfo.InvariantCulture),
            reached);
        Assert.Equal("", again);
        Assert.True(Win.AlreadyLogged(logPath));
        Assert.True(Autopilot.Enabled);
        Assert.Contains(reached, File.ReadAllText(logPath));
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_stage_open_where_layout_prices_supply_managers_money_playbook_curves_file_day_step_calibrate_noise_policy_stage_sim_win_tunes_climb_then_logs()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → stage → open → where → layout → prices → supply → managers → money → playbook → curves → day-step → calibrate → noise → policy → stage-sim → win
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var stage = Stage.Read(
                TopBar.Day(shot.Rolled),
                guard.Cash,
                world.Needs,
                world.Stops,
                world.Holds,
                world.Slots);
            var open = Open.Decide(stage, Array.Empty<OpenCandidate>(), truckLoadFraction: 0m);
            var reserve = Money.Reserve(guard.DailyCost);
            var site = Where.Decide(Array.Empty<SiteCandidate>(), guard.Cash, reserve);
            var golden = Layout.Decide(stage, "", "small", Array.Empty<OwnedLayout>(), Starter.StageOne());
            var price = Prices.Decide(
                "",
                currentPrice: 0m,
                defaultPrice: 10m,
                wholesale: 4m,
                priorUnits: 8,
                currentUnits: 8,
                satisfaction: 0m,
                trials: Array.Empty<ItemSoldPerPriceEntry>());
            var supply = Supply.Decide(stage, "", "ba:itemname_cheapgift", world.Holds, world.Stops);
            var managers = Managers.Decide(
                stage,
                Array.Empty<string>(),
                employees: 0,
                openRoles: 0,
                seats: Array.Empty<ManagerSeat>(),
                pricingPlanExists: true);
            var money = Money.Decide(
                stage,
                guard.Cash,
                guard.DailyCost,
                projectedDailyProfit: 200m,
                offers: new[]
                {
                    new LoanOffer(1_000m, 80),
                    new LoanOffer(2_000m, 100),
                },
                loan: null,
                bestPaybackRate: 0.1m);
            var step = Playbook.Decide(
                open,
                site,
                golden,
                price,
                supply,
                TruckPick.None(),
                WarehousePick.None(),
                managers,
                money,
                EstatePick.None(),
                ClosePick.None(),
                RunPick.None());
            var ledger = Path.Combine(dir.Path, "stage-sim-statements.csv");
            File.WriteAllText(
                ledger,
                "day,street,number,sales,goods,wages,rent,marketing,theft,profit\n"
                + "1000,ba:street_gift,5,80,16,8,4,0,0,52\n"
                + "1014,ba:street_gift,5,80,16,8,4,0,0,52\n");
            var curves = Curves.FitFile(ledger);
            var days = DayStep.Run(Calibrate.StartDay, Calibrate.EndDay - Calibrate.StartDay + 1, curves);
            var calibrated = Calibrate.CheckFile(ledger);
            var seeds = new[] { 1, 2, 3, 4, 5 };
            var noise = Noise.Run(curves, startNetWorth: 1000m, seeds: seeds, horizonDays: 60);
            var keep = new PolicyOption("keep", curves);
            var gambleCurves = curves
                .Concat(new[]
                {
                    new StoreCurve("ba:street_risk", 2, 300m, 0.05m, 5m, 200m, 0m, 0m, 1),
                })
                .ToList();
            var gamble = new PolicyOption("gamble", gambleCurves);
            var pick = Policy.Compare(
                new[] { keep, gamble },
                startNetWorth: 1000m,
                seeds: seeds,
                horizonDays: 60);
            var opens = new List<OpenCandidate>();
            var sites = new List<SiteCandidate>();
            for (var i = 1; i <= 8; i++)
            {
                opens.Add(new OpenCandidate(
                    "ba:businesstype_giftshop",
                    "goods",
                    200_000m,
                    wholesalerSells: true,
                    "specialty",
                    0,
                    false));
                sites.Add(new SiteCandidate(
                    "ba:street_climb" + i.ToString(CultureInfo.InvariantCulture),
                    i,
                    familyOnStreet: 0,
                    traffic: 20m,
                    dailyRent: 10m,
                    cost: 5_000m));
            }

            var catalog = new StageSimCatalog(
                opens,
                sites,
                loans: new[] { new LoanOffer(100_000m, 50) },
                estates: Array.Empty<EstateCandidate>(),
                item: "ba:itemname_cheapgift",
                warehouseName: "W1",
                truckCost: 1_000m);
            var tuned = StageSim.Tune(
                new[]
                {
                    new StageThresholdOption(
                        "tight",
                        StageThresholds.Starting().With(stage2Cash: 100_000_000m, stage4Cash: 200_000_000m)),
                    new StageThresholdOption(
                        "climb",
                        StageThresholds.Starting().With(stage2Cash: 30_000m, stage4Cash: 200_000m)),
                },
                startCash: 40_000m,
                startNetWorth: 40_000m,
                catalog,
                seeds,
                horizonDays: 80);

            Assert.Contains("day=1016", shot.Rolled);
            Assert.True(step.Chosen);
            Assert.Equal("money", step.Kind);
            Assert.Equal(Calibrate.EndDay, days[^1].Day);
            Assert.True(calibrated.Within);
            Assert.Equal(60, noise.HorizonDays);
            Assert.Equal("keep", pick.Name);
            Assert.Equal("climb", tuned.Name);
            Assert.True(tuned.WinAtMedian);
            Assert.Equal(30_000m, tuned.Thresholds.Stage2Cash);
            Assert.Equal(
                StageSim.Run(40_000m, 40_000m, catalog, seeds, tuned.Thresholds, 80).Median,
                tuned.Median);
            Win.ClearApproved();
            var logPath = LoadLog.CeoLogPath(dir.Path);
            var reached = Win.Record(logPath, TopBar.Day(shot.Rolled), tuned.Median);
            Assert.StartsWith("win.reached version=" + LoadLog.Version, reached);
            Assert.Equal("", Win.Record(logPath, TopBar.Day(shot.Rolled) + 1, tuned.Median));
            Assert.True(Autopilot.Enabled);
            Assert.True(Win.AlreadyLogged(logPath));
        }
        finally
        {
            Autopilot.Enabled = false;
            Autopilot.DryRun = false;
            Win.ClearApproved();
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_layout_ops_wholesaler_hire_loan_demand_tax_time_marketing_stop_takeover_stage_open_where_layout_prices_supply_locks_stage_one_wholesaler()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → layout → ops → wholesaler → hire → loan → demand → tax → time → marketing → stop → takeover → stage → open → where → layout → prices → supply
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var rented = Rent.Choose(
                new[] { new RentLot("ba:street_low", 9, 40m) },
                guard.Cash,
                anyRented: false,
                prefix.Api);
            Rent.Record(dir.Path, rented, TopBar.Day(prefix.Rolled));
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, prefix.Api);
            Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));
            var assigned = StoreOps.Assign(chosen, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), prefix.Api);
            StoreOps.Record(dir.Path, assigned, rented, TopBar.Day(shot.Rolled));
            var signed = Wholesaler.Sign(rented, chosen, Wholesaler.StageOne(), alreadySigned: false, prefix.Api);
            Wholesaler.Record(dir.Path, signed, TopBar.Day(shot.Rolled));
            var hired = Cashier.Hire(rented, chosen, signed, alreadyHired: false, prefix.Api);
            Cashier.Record(dir.Path, hired, TopBar.Day(shot.Rolled));
            var taken = BankLoan.Take(
                projectedDailyProfit: 200m,
                offers: new[] { new LoanOffer(2000m, 100) },
                alreadyBorrowed: false,
                prefix.Api);
            BankLoan.RecordTake(dir.Path, taken, TopBar.Day(shot.Rolled));
            var cleared = StaffDemand.Clear(
                unmetDays: StaffDemand.DemandDays,
                demandName: "ba:jobdemand_healthinsurance",
                employeeId: "emp-1",
                prefix.Api);
            StaffDemand.Record(dir.Path, cleared, TopBar.Day(shot.Rolled));
            var paid = TaxBill.Pay(currentDue: 55m, backDue: 12m, prefix.Api);
            TaxBill.Record(dir.Path, paid, TopBar.Day(shot.Rolled));
            var advanced = TimeAdvance.Skip(Needs.Hold(), alreadyAdvanced: false, prefix.Api);
            TimeAdvance.RecordSkip(dir.Path, advanced, TopBar.Day(shot.Rolled));
            var started = Marketing.Start(
                dailyCost: 100m,
                gain: 300m,
                alreadyActive: false,
                street: rented.Street,
                number: rented.Number,
                type: Marketing.StarterType,
                prefix.Api);
            Marketing.RecordStart(dir.Path, started, TopBar.Day(shot.Rolled));
            var stopped = Marketing.Stop(
                alreadyActive: started.Started,
                street: rented.Street,
                number: rented.Number,
                prefix.Api);
            Marketing.RecordStop(dir.Path, stopped, TopBar.Day(shot.Rolled));
            var declined = RivalTakeover.Decline(
                offerPrice: 1000m,
                dailyProfit: 10m,
                street: rented.Street,
                number: rented.Number,
                alreadyDeclined: false,
                prefix.Api);
            RivalTakeover.Record(dir.Path, declined, TopBar.Day(shot.Rolled));
            var stage = Stage.Read(
                TopBar.Day(shot.Rolled),
                guard.Cash,
                world.Needs,
                shot.Report.Stops,
                world.Holds,
                world.Slots);
            var offers = new List<OpenCandidate>();
            foreach (var offer in Starter.StageOne())
            {
                offers.Add(new OpenCandidate(
                    offer.BusinessType,
                    "goods",
                    offer.DailyProfit,
                    wholesalerSells: true,
                    StoreFamily.For(offer.BusinessType) ?? "specialty",
                    ownedOfFamily: 0,
                    familyHasWarehouse: false));
            }

            var open = Open.Decide(stage, offers, truckLoadFraction: 0m);
            var reserve = guard.DailyCost * Audit.ReserveDays;
            var site = Where.Decide(
                new[]
                {
                    new SiteCandidate(rented.Street, rented.Number, familyOnStreet: Where.ClusterMin, traffic: 20m, dailyRent: 40m, cost: rented.Number),
                },
                guard.Cash,
                reserve);
            var golden = Layout.Decide(
                stage,
                open.Type,
                "small",
                Array.Empty<OwnedLayout>(),
                Starter.StageOne());
            var price = Prices.Decide(
                "ba:itemname_cheapgift",
                currentPrice: 10m,
                defaultPrice: 10m,
                wholesale: 4m,
                priorUnits: 5,
                currentUnits: 3,
                satisfaction: 0.5m,
                trials: null);
            var supply = Supply.Decide(
                stage,
                site.Street + " " + site.Number.ToString(CultureInfo.InvariantCulture),
                "ba:itemname_cheapgift",
                world.Holds,
                shot.Report.Stops);

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Equal(1, stage);
            Assert.True(open.Chosen);
            Assert.Equal("ba:businesstype_giftshop", open.Type);
            Assert.True(site.Chosen);
            Assert.Equal(rented.Street, site.Street);
            Assert.Equal(rented.Number, site.Number);
            Assert.True(golden.Chosen);
            Assert.Equal("GiftShopWholesalers", golden.Blueprint);
            Assert.True(price.Chosen);
            Assert.Equal(10.5m, price.Price);
            Assert.True(supply.Chosen);
            Assert.Equal("wholesaler", supply.Source);
            Assert.Equal("", supply.Warehouse);
            Assert.False(supply.DropContract);
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_stage_locks_the_read_into_the_bar()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → stage
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = new AuditGuard(
            enabled: true,
            dryRun: false,
            cash: Stage.Stage4Cash,
            dailyCost: 10m,
            truckCost: 1,
            build: LoadLog.PinnedBuild);
        var world = prefix.World;
        var report = Audit.Check(dir.Path, 1016, prefix.Api, world, guard, heal => HealFate.Done);
        var stage = Stage.Read(
            TopBar.Day(prefix.Rolled),
            guard.Cash,
            world.Needs,
            report.Stops,
            world.Holds,
            world.Slots);
        var line = TopBar.From(
            prefix.Rolled,
            File.ReadAllText(StatementDump.CsvPath(dir.Path)),
            guard.Cash,
            0m,
            stage,
            guard.Enabled,
            TopBar.Clock(paused: false, rate: null));

        Assert.Contains("day=1016", prefix.Rolled);
        Assert.Contains("audit.summary ", string.Join("\n", report.Lines));
        Assert.Equal(1, stage);
        Assert.Equal("1", Between(line, "Stage "));
        Assert.Contains("Cash " + Stage.Stage4Cash.ToString(CultureInfo.InvariantCulture), line);
        Assert.True(Audit.EmptySlots(world.Slots) > 0);
    }

    [Fact]
    public void Audit_load_day_roll_needs_refill_recorded_call_slot_fill_store_connect_plan_assign_restock_sort_overflow_order_audit_bar_health_controls_telemetry_rent_layout_ops_capacity_stage_stays_bootstrap()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → store connect → plan assign → restock → sort → overflow → order → audit → bar → health → controls → telemetry → rent → layout → ops → capacity → stage
        using var dir = new TempAudit();
        var save = new object();
        var prefix = Prefix(dir.Path, save);
        var guard = LiveGuard();
        var world = prefix.World;
        Autopilot.Enabled = guard.Enabled;
        Autopilot.DryRun = guard.DryRun;
        try
        {
            var shot = WatchDay(dir.Path, ref world, guard, prefix.Api, prefix.Rolled, 1016, 10m, null);
            var rented = Rent.Choose(
                new[] { new RentLot("ba:street_low", 9, 40m) },
                guard.Cash,
                anyRented: false,
                prefix.Api);
            Rent.Record(dir.Path, rented, TopBar.Day(prefix.Rolled));
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, prefix.Api);
            Starter.Record(dir.Path, chosen, rented, TopBar.Day(shot.Rolled));
            var assigned = StoreOps.Assign(chosen, StoreOps.Opening(), StoreFixtures.GiftShopWholesalers(), prefix.Api);
            StoreOps.Record(dir.Path, assigned, rented, TopBar.Day(shot.Rolled));
            var matched = Capacity.Match(rented, chosen, alreadyMatched: false, prefix.Api);
            Capacity.Record(dir.Path, matched, TopBar.Day(shot.Rolled));
            var stage = Stage.Read(
                TopBar.Day(shot.Rolled),
                guard.Cash,
                world.Needs,
                shot.Report.Stops,
                world.Holds,
                world.Slots);

            Assert.Contains("day=1016", prefix.Rolled);
            Assert.Contains("telemetry.written ", shot.Written);
            Assert.True(matched.Matched);
            Assert.Equal(1, stage);
            Assert.Equal("1", Between(shot.Bar, "Stage "));
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    private static DayShot WatchDay(
        string path,
        ref AuditWorld world,
        AuditGuard guard,
        ApiNote api,
        string rolled,
        int day,
        decimal net,
        int? seen)
    {
        if (seen.HasValue)
        {
            rolled = DayRoll.Apply(
                path,
                seen,
                day,
                new[]
                {
                    new FinancialDay(1015, new[] { new StatementRow("ba:street_fourthavenue", 28, 10f, 1f, 1f, 1f, 0f, 0f, 7f) }),
                },
                world.Stops,
                world.Holds,
                world.Needs,
                Array.Empty<DailyUnits>()) ?? throw new InvalidOperationException("day roll missing");
        }

        var report = Audit.Check(path, day, api, world, guard, heal => HealFate.Done);
        var stage = Stage.Read(TopBar.Day(rolled), guard.Cash, world.Needs, report.Stops, world.Holds, world.Slots);
        var bar = TopBar.From(
            rolled,
            File.ReadAllText(StatementDump.CsvPath(path)),
            guard.Cash,
            net,
            stage,
            Autopilot.Enabled,
            TopBar.Clock(paused: false, rate: null));
        var health = Health.Latest(path);
        var controls = Controls.Row(Autopilot.Enabled, Autopilot.DryRun);
        var written = Telemetry.Record(path, bar, health, controls);
        world = world.WithStops(report.Stops);
        return new DayShot(written, bar, health, controls, report, rolled);
    }

    private static string Between(string text, string label)
    {
        var at = text.IndexOf(label, StringComparison.Ordinal);
        Assert.True(at >= 0, label);
        var rest = text.Substring(at + label.Length);
        var stop = rest.IndexOf("  ", StringComparison.Ordinal);
        return stop < 0 ? rest.Trim() : rest.Substring(0, stop).Trim();
    }

    private static string Net(string bar)
    {
        var worth = Between(bar, "Net worth ");
        var slash = worth.IndexOf(" / ", StringComparison.Ordinal);
        Assert.True(slash > 0);
        return worth.Substring(0, slash);
    }

    private static string JoinedActions(IReadOnlyList<string> lines)
    {
        var names = new List<string>();
        foreach (var line in lines)
        {
            if (!line.StartsWith("audit.healed ", StringComparison.Ordinal))
            {
                continue;
            }

            var at = line.IndexOf("action=", StringComparison.Ordinal);
            if (at < 0)
            {
                continue;
            }

            var rest = line.Substring(at + "action=".Length);
            var end = rest.IndexOf(' ');
            names.Add(end < 0 ? rest : rest.Substring(0, end));
        }

        return string.Join("|", names);
    }

    private sealed class DayShot
    {
        public DayShot(string written, string bar, string health, string controls, AuditReport report, string rolled)
        {
            Written = written;
            Bar = bar;
            Health = health;
            Controls = controls;
            Report = report;
            Rolled = rolled;
        }

        public string Written { get; }

        public string Bar { get; }

        public string Health { get; }

        public string Controls { get; }

        public AuditReport Report { get; }

        public string Rolled { get; }
    }

    private static string Token(string line, string name)
    {
        var token = name + "=";
        var at = line.IndexOf(token, StringComparison.Ordinal);
        Assert.True(at >= 0, token);
        var rest = line.Substring(at + token.Length);
        var end = 0;
        while (end < rest.Length && char.IsDigit(rest[end]))
        {
            end++;
        }

        Assert.True(end > 0, token);
        return rest.Substring(0, end);
    }

    private static string Tail(string healed)
    {
        var at = healed.IndexOf("action=", StringComparison.Ordinal);
        Assert.True(at >= 0);
        var tail = healed.Substring(at);
        var dry = tail.LastIndexOf(" dry=", StringComparison.Ordinal);
        if (dry >= 0)
        {
            tail = tail.Substring(0, dry);
        }

        var call = tail.LastIndexOf(" call=", StringComparison.Ordinal);
        if (call >= 0)
        {
            tail = tail.Substring(0, call);
        }

        return tail.Trim();
    }

    private static AuditWorld PlantedStore()
    {
        return new AuditWorld(
            Array.Empty<DeliveryStop>(),
            PlantedHolds(),
            PlantedNeeds(),
            Array.Empty<DailyUnits>(),
            Array.Empty<GarageSlot>(),
            Array.Empty<string>(),
            Array.Empty<WarehouseLevel>(),
            pricingPlan: true,
            Array.Empty<HqSeat>(),
            Array.Empty<HiredHand>(),
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static WarehouseHold[] PlantedHolds()
    {
        return new[] { Hold("W11", "ba:street_twelfthstreet", 1, Hair, 40) };
    }

    private static GoodsNeed[] PlantedNeeds()
    {
        return new[] { new GoodsNeed(Salon, Hair) };
    }

    private static AuditWorld SlotWorld()
    {
        return new AuditWorld(
            Array.Empty<DeliveryStop>(),
            Array.Empty<WarehouseHold>(),
            Array.Empty<GoodsNeed>(),
            Array.Empty<DailyUnits>(),
            new[] { new GarageSlot("W7", 1, filled: false) },
            Array.Empty<string>(),
            Array.Empty<WarehouseLevel>(),
            pricingPlan: true,
            Array.Empty<HqSeat>(),
            Array.Empty<HiredHand>(),
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static AuditGuard LiveGuard()
    {
        return new AuditGuard(enabled: true, dryRun: false, cash: 1000m, dailyCost: 10m, truckCost: 1, build: LoadLog.PinnedBuild);
    }

    private static WarehouseHold Hold(string warehouse, string street, int number, string item, int amount)
    {
        return new WarehouseHold(warehouse, street, number, item, amount);
    }

    private static PrefixRun Prefix(string path, object save)
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
        var sample = new[]
        {
            new GoodsNeed(Salon, Hair),
            new GoodsNeed(Planned, Soda),
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
        var routed = Supply.Stops(RouteDump.RoutesPath(path));
        var stock = Supply.Stock(RouteDump.WarehousesPath(path));
        var connected = new ConnectWatch().Tick(
            path, save, 1016, api, routed, stock, sample, Array.Empty<DailyUnits>());
        new PlanWatch().Tick(
            path,
            save,
            1016,
            api,
            pricingPlan: true,
            new[]
            {
                new HqSeat("logistics", filled: true, employee: "kept-l"),
                new HqSeat("hr", filled: true, employee: "kept-h"),
                new HqSeat("purchasing", filled: true, employee: "kept-p"),
                new HqSeat("headhunter", filled: true, employee: "kept-n"),
            },
            Array.Empty<HiredHand>());
        var arrangedHolds = stock.Concat(new[]
        {
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, Phone, 3),
            new WarehouseHold("W12", "ba:street_twelfthstreet", 6, "ba:itemname_palletshelf", 99),
            new WarehouseHold("W13", "ba:street_twelfthstreet", 5, "ba:itemname_palletshelf", 95),
        }).ToArray();
        var arranged = Restock.Arrange(arrangedHolds);
        var levels = arranged.Homes.Select(level => level.Warehouse == "W12"
            ? new WarehouseLevel(level.Warehouse, level.Family, 0, 6, false, level.Mixed, level.Shelves)
            : level.Warehouse == "W1" && level.Family == "food"
                ? new WarehouseLevel(level.Warehouse, level.Family, 10, 4, true, level.Mixed, level.Shelves)
                : level).ToArray();
        var overflow = Restock.Overflow(levels, null);
        new RestockWatch().Tick(path, save, 1016, api, overflow.Homes, arranged.Drains, overflow.Claims);
        var world = new AuditWorld(
            routed,
            arrangedHolds,
            sample,
            Array.Empty<DailyUnits>(),
            new[] { new GarageSlot("W7", 1, filled: false) },
            Array.Empty<string>(),
            overflow.Homes,
            pricingPlan: true,
            new[]
            {
                new HqSeat("logistics", filled: true, employee: "kept-l"),
                new HqSeat("hr", filled: true, employee: "kept-h"),
                new HqSeat("purchasing", filled: true, employee: "kept-p"),
                new HqSeat("headhunter", filled: true, employee: "kept-n"),
            },
            Array.Empty<HiredHand>(),
            Array.Empty<string>(),
            Array.Empty<string>());
        return new PrefixRun(
            loaded,
            refill ?? throw new InvalidOperationException("needs refill missing"),
            rolled ?? "",
            api,
            connected,
            arranged.Drains,
            world);
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

    private sealed class PrefixRun
    {
        public PrefixRun(
            string loaded,
            PlayerNeeds refill,
            string rolled,
            ApiNote api,
            IReadOnlyList<string> connected,
            IReadOnlyList<WarehouseDrain> drains,
            AuditWorld world)
        {
            Loaded = loaded;
            Refill = refill;
            Rolled = rolled;
            Api = api;
            Connected = connected;
            Drains = drains;
            World = world;
        }

        public string Loaded { get; }

        public PlayerNeeds Refill { get; }

        public string Rolled { get; }

        public ApiNote Api { get; }

        public IReadOnlyList<string> Connected { get; }

        public IReadOnlyList<WarehouseDrain> Drains { get; }

        public AuditWorld World { get; }
    }

    private sealed class TempAudit : IDisposable
    {
        public TempAudit()
        {
            Path = Directory.CreateTempSubdirectory("chaotic-audit").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
