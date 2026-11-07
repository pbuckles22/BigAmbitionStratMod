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
