using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class AutopilotTests
{
    private const string Hair = "ba:itemname_haircareproduct";
    private const string Salon = "ba:street_fourthavenue 28";

    [Fact]
    public void Autopilot_day_runs_audit_stage_action_time_and_telemetry_when_enabled()
    {
        using var dir = new TempLog();
        var api = GameApi.Live();
        Autopilot.Enabled = true;
        Autopilot.DryRun = false;
        var world = PlantedStore();
        var guard = new AuditGuard(true, false, 1000m, 10m, 1, LoadLog.PinnedBuild);
        var step = new PlaybookStep("money", "loan 2000", true);
        var needs = Needs.Hold();
        var bar = TopBar.Line(1016, 1000m, 2500000m, 7m, 1, true, 1);
        var health = "Stores supplied unconnected=0  Slots filled emptySlots=0  Plans present missingPlans=0  Last heal healed=1 action=connect";

        var day = Autopilot.Day(
            dir.Path,
            1016,
            api,
            world,
            guard,
            heal => HealFate.Done,
            step,
            needs,
            bar,
            health);

        Assert.True(day.Ran);
        Assert.Equal(
            "autopilot.day version=" + LoadLog.Version + " day=1016 ran=true",
            day.Marker);
        Assert.Contains(day.Report.Lines, line => line.StartsWith("audit.summary "));
        Assert.Contains(day.Report.Lines, line => line.StartsWith("audit.healed ") && line.Contains("action=connect"));
        Assert.Equal(
            "autopilot.action version=" + LoadLog.Version
                + " day=1016 kind=money detail=loan 2000 call=" + api.Call("loan"),
            day.Action);
        Assert.Equal(api.Call("loan"), Autopilot.Call(step, api));
        Assert.Equal(
            "time.advanced version=" + LoadLog.Version
                + " day=1016 span=1d call=" + api.Call("time advance"),
            day.Advanced);
        Assert.Contains("telemetry.written version=" + LoadLog.Version + " day=1016", day.Telemetry);
        Assert.Contains("autopilot=on", day.Telemetry);

        var log = File.ReadAllText(LoadLog.CeoLogPath(dir.Path));
        Assert.Contains(day.Marker, log);
        Assert.Contains(day.Action, log);
        Assert.Contains(day.Advanced, log);
        Assert.Contains(day.Telemetry, log);
        Assert.True(log.IndexOf(day.Marker, StringComparison.Ordinal) < log.IndexOf("audit.summary ", StringComparison.Ordinal));
        Assert.True(log.IndexOf("audit.summary ", StringComparison.Ordinal) < log.IndexOf(day.Action, StringComparison.Ordinal));
        Assert.True(log.IndexOf(day.Action, StringComparison.Ordinal) < log.IndexOf(day.Advanced, StringComparison.Ordinal));
        Assert.True(log.IndexOf(day.Advanced, StringComparison.Ordinal) < log.IndexOf(day.Telemetry, StringComparison.Ordinal));

        var row = File.ReadAllLines(Telemetry.CsvPath(dir.Path))[1].Split(',');
        Assert.Equal("1016", row[0]);
        Assert.Equal("1000", row[1]);
        Assert.Equal("2500000", row[2]);
        Assert.Equal("7", row[3]);
        Assert.Equal("1", row[4]);
        Assert.Equal("connect", row[14]);
    }

    [Fact]
    public void Autopilot_day_skips_when_the_flag_is_cleared()
    {
        using var dir = new TempLog();
        var api = GameApi.Live();
        Autopilot.Enabled = true;
        Controls.Pause();
        Assert.False(Autopilot.Enabled);

        var called = false;
        var day = Autopilot.Day(
            dir.Path,
            1016,
            api,
            PlantedStore(),
            new AuditGuard(true, false, 1000m, 10m, 1, LoadLog.PinnedBuild),
            heal =>
            {
                called = true;
                return HealFate.Done;
            },
            new PlaybookStep("money", "loan 2000", true),
            Needs.Hold(),
            TopBar.Line(1016, 1000m, 20m, 7m, 1, false, 1),
            Health.Section(null, null));

        Assert.False(day.Ran);
        Assert.False(called);
        Assert.Equal(
            "autopilot.day version=" + LoadLog.Version + " day=1016 ran=false",
            day.Marker);
        Assert.Equal("", day.Action);
        Assert.Equal("", day.Advanced);
        Assert.Equal("", day.Telemetry);
        Assert.Empty(day.Report.Lines);

        var log = File.ReadAllText(LoadLog.CeoLogPath(dir.Path));
        Assert.Equal(day.Marker + Environment.NewLine, log);
        Assert.DoesNotContain("audit.healed ", log);
        Assert.DoesNotContain("time.advanced ", log);
        Assert.DoesNotContain("telemetry.written ", log);
        Assert.False(File.Exists(Telemetry.CsvPath(dir.Path)));
    }

    [Fact]
    public void Autopilot_call_maps_stage_kinds_to_recorded_calls()
    {
        var api = GameApi.Live();

        Assert.Equal("loan", Autopilot.CallKey(new PlaybookStep("money", "loan 2000", true)));
        Assert.Equal(api.Call("loan"), Autopilot.Call(new PlaybookStep("money", "loan 2000", true), api));
        Assert.Equal(api.Call("close"), Autopilot.Call(new PlaybookStep("close", "shutdown", true), api));
        Assert.Equal(api.Call("rent"), Autopilot.Call(new PlaybookStep("open", "ba:businesstype_giftshop", true), api));
        Assert.Equal(api.Call("rent"), Autopilot.Call(new PlaybookStep("where", "ba:street_low 9", true), api));
        Assert.Equal(api.Call("clone"), Autopilot.Call(new PlaybookStep("layout", "GiftShopWholesalers", true), api));
        Assert.Equal(api.Call("price"), Autopilot.Call(new PlaybookStep("prices", "ba:itemname_cheapgift 21", true), api));
        Assert.Equal(api.Call("delivery"), Autopilot.Call(new PlaybookStep("supply", "warehouse W12", true), api));
        Assert.Equal(api.Call("truck buy"), Autopilot.Call(new PlaybookStep("trucks", "W12 1", true), api));
        Assert.Equal(api.Call("purchasing plan"), Autopilot.Call(new PlaybookStep("warehouses", "home specialty W3", true), api));
        Assert.Equal(api.Call("plan assign"), Autopilot.Call(new PlaybookStep("managers", "pricing hire", true), api));
        Assert.Equal(api.Call("buy"), Autopilot.Call(new PlaybookStep("realestate", "headquarters ba:street_hq 1", true), api));
        Assert.Equal(api.Call("sleep"), Autopilot.Call(new PlaybookStep("run", "sleep", true), api));
        Assert.Equal(api.Call("food"), Autopilot.Call(new PlaybookStep("run", "eat", true), api));
        Assert.Equal("", Autopilot.Call(PlaybookStep.None(), api));
        Assert.Equal("", Autopilot.Call(null, api));
        Assert.Equal("", Autopilot.CallKey(null));
    }

    [Fact]
    public void Autopilot_day_skips_stage_action_when_the_step_is_none()
    {
        using var dir = new TempLog();
        Autopilot.Enabled = true;
        Autopilot.DryRun = false;
        var api = GameApi.Live();
        var day = Autopilot.Day(
            dir.Path,
            40,
            api,
            ClearWorld(),
            new AuditGuard(true, false, 1000m, 10m, 1, LoadLog.PinnedBuild),
            heal => HealFate.Done,
            PlaybookStep.None(),
            Needs.Hold(),
            TopBar.Line(40, 10m, 20m, 1m, 1, true, 1),
            Health.Section(null, null));

        Assert.True(day.Ran);
        Assert.Equal("", day.Action);
        Assert.StartsWith("time.advanced ", day.Advanced);
        Assert.DoesNotContain("autopilot.action ", File.ReadAllText(LoadLog.CeoLogPath(dir.Path)));
    }

    [Fact]
    public void Autopilot_day_empty_or_invalid_is_a_blank_skip()
    {
        Autopilot.Enabled = true;
        Assert.False(Autopilot.Day(null!, 1, GameApi.Live(), ClearWorld(), LiveGuard(), null, null, null, null, null).Ran);
        Assert.False(Autopilot.Day("", 1, GameApi.Live(), ClearWorld(), LiveGuard(), null, null, null, null, null).Ran);
        Assert.False(Autopilot.Day(" ", 1, GameApi.Live(), ClearWorld(), LiveGuard(), null, null, null, null, null).Ran);

        using var dir = new TempLog();
        Assert.False(Autopilot.Day(dir.Path, 0, GameApi.Live(), ClearWorld(), LiveGuard(), null, null, null, null, null).Ran);
        Assert.False(Autopilot.Day(dir.Path, -1, GameApi.Live(), ClearWorld(), LiveGuard(), null, null, null, null, null).Ran);
        Assert.False(File.Exists(LoadLog.CeoLogPath(dir.Path)));
    }

    [Fact]
    public void Autopilot_enabled_flag_wins_over_a_guard_that_still_says_on()
    {
        // Winner: Autopilot.Enabled. F10 clears the flag; a planted guard.Enabled=true does not run the day.
        using var dir = new TempLog();
        Autopilot.Enabled = false;
        var called = false;
        var day = Autopilot.Day(
            dir.Path,
            1016,
            GameApi.Live(),
            PlantedStore(),
            new AuditGuard(enabled: true, dryRun: false, cash: 1000m, dailyCost: 10m, truckCost: 1, build: LoadLog.PinnedBuild),
            heal =>
            {
                called = true;
                return HealFate.Done;
            },
            new PlaybookStep("money", "loan 2000", true),
            Needs.Hold(),
            TopBar.Line(1016, 1m, 2m, 3m, 1, false, 0),
            Health.Section(null, null));

        Assert.False(day.Ran);
        Assert.False(called);
        Assert.Contains("ran=false", day.Marker);
    }

    private static AuditWorld PlantedStore()
    {
        return new AuditWorld(
            Array.Empty<DeliveryStop>(),
            new[] { new WarehouseHold("W11", "ba:street_twelfthstreet", 1, Hair, 40) },
            new[] { new GoodsNeed(Salon, Hair) },
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

    private static AuditWorld ClearWorld()
    {
        return new AuditWorld(
            Array.Empty<DeliveryStop>(),
            Array.Empty<WarehouseHold>(),
            Array.Empty<GoodsNeed>(),
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

    private static AuditGuard LiveGuard()
    {
        return new AuditGuard(true, false, 1000m, 10m, 1, LoadLog.PinnedBuild);
    }

    private sealed class TempLog : IDisposable
    {
        public TempLog()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ceo-autopilot-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
            }
        }
    }
}
