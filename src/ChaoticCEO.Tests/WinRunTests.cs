using ChaoticCEO;

namespace ChaoticCEO.Tests;

[Collection(nameof(AutopilotState))]
public class WinRunTests
{
    private const string Hair = "ba:itemname_haircareproduct";
    private const string Salon = "ba:street_fourthavenue 28";

    [Fact]
    public void WinRun_day_runs_autopilot_then_records_win_when_crossed()
    {
        using var dir = new TempLog();
        Autopilot.Enabled = true;
        Autopilot.DryRun = false;
        Win.ClearApproved();
        var api = GameApi.Live();
        var bar = TopBar.Line(40, 500m, TopBar.WinNetWorth, 10m, 1, true, 1);
        var health = Health.Section(null, null);

        var day = WinRun.Day(
            dir.Path,
            40,
            netWorth: TopBar.WinNetWorth,
            api,
            ClearWorld(),
            LiveGuard(),
            heal => HealFate.Done,
            PlaybookStep.None(),
            Needs.Hold(),
            bar,
            health);

        Assert.True(day.Loop.Ran);
        Assert.StartsWith(
            "win.reached version=" + LoadLog.Version
                + " day=40 netWorth=" + TopBar.WinNetWorth.ToString(System.Globalization.CultureInfo.InvariantCulture),
            day.Reached);
        Assert.True(Win.AlreadyLogged(LoadLog.CeoLogPath(dir.Path)));
        Assert.True(Autopilot.Enabled);
    }

    [Fact]
    public void WinRun_run_logs_win_once_keeps_autopilot_on_and_advances_telemetry_after()
    {
        using var dir = new TempLog();
        Autopilot.Enabled = true;
        Autopilot.DryRun = false;
        Win.ClearApproved();
        var api = GameApi.Live();
        var frames = new[]
        {
            Frame(38, TopBar.WinNetWorth - 1m, 100m, -10m),
            Frame(39, TopBar.WinNetWorth, 110m, 5m),
            Frame(40, TopBar.WinNetWorth + 1m, 120m, 8m),
            Frame(41, TopBar.WinNetWorth + 2m, 130m, 9m),
        };

        var run = WinRun.Run(
            dir.Path,
            frames,
            api,
            ClearWorld(),
            LiveGuard(),
            heal => HealFate.Done);

        Assert.StartsWith(
            "win.reached version=" + LoadLog.Version
                + " day=39 netWorth=" + TopBar.WinNetWorth.ToString(System.Globalization.CultureInfo.InvariantCulture),
            run.Reached);
        Assert.Equal(39, run.WinDay);
        Assert.Equal(1, CountToken(File.ReadAllText(LoadLog.CeoLogPath(dir.Path)), "win.reached "));
        Assert.True(run.AutopilotStillOn);
        Assert.True(Autopilot.Enabled);
        Assert.Equal(new[] { 38, 39, 40, 41 }, run.TelemetryDays);
        Assert.True(run.AdvancedAfterWin);
        Assert.Equal(4, run.Days.Count);
        Assert.All(run.Days, day => Assert.True(day.Loop.Ran));
    }

    [Fact]
    public void WinRun_run_skips_win_line_when_never_crossing()
    {
        using var dir = new TempLog();
        Autopilot.Enabled = true;
        Win.ClearApproved();
        var run = WinRun.Run(
            dir.Path,
            new[] { Frame(10, 20_000m, 50m, 1m), Frame(11, 21_000m, 51m, 1m) },
            GameApi.Live(),
            ClearWorld(),
            LiveGuard(),
            heal => HealFate.Done);

        Assert.Equal("", run.Reached);
        Assert.Equal(0, run.WinDay);
        Assert.False(run.AdvancedAfterWin);
        Assert.Equal(new[] { 10, 11 }, run.TelemetryDays);
        Assert.DoesNotContain("win.reached ", File.ReadAllText(LoadLog.CeoLogPath(dir.Path)));
    }

    [Fact]
    public void WinRun_run_empty_or_invalid_is_blank()
    {
        Autopilot.Enabled = true;
        Win.ClearApproved();
        Assert.Equal("", WinRun.Run(null!, new[] { Frame(1, 1m, 1m, 0m) }, GameApi.Live(), ClearWorld(), LiveGuard(), null).Reached);
        Assert.Equal("", WinRun.Run("", new[] { Frame(1, 1m, 1m, 0m) }, GameApi.Live(), ClearWorld(), LiveGuard(), null).Reached);
        Assert.Equal("", WinRun.Run(" ", Array.Empty<WinRunFrame>(), GameApi.Live(), ClearWorld(), LiveGuard(), null).Reached);

        using var dir = new TempLog();
        Assert.Equal("", WinRun.Run(dir.Path, null, GameApi.Live(), ClearWorld(), LiveGuard(), null).Reached);
        Assert.Equal("", WinRun.Run(dir.Path, Array.Empty<WinRunFrame>(), GameApi.Live(), ClearWorld(), LiveGuard(), null).Reached);
        Assert.False(File.Exists(LoadLog.CeoLogPath(dir.Path)));
    }

    [Fact]
    public void Open_stage_one_candidates_new_save_day_loop_suppresses_open_while_pending()
    {
        // Harvest: days 2–8 kept autopilot.action kind=open while rent never finished a site.
        var offers = Starter.StageOne();
        var owned = new Dictionary<string, int> { ["specialty"] = 1 };
        var pending = Open.StageOneCandidates(offers, owned, pendingOpen: "ba:businesstype_giftshop");
        var ready = Open.StageOneCandidates(offers, owned, pendingOpen: "");

        Assert.Empty(pending);
        Assert.False(Open.Decide(1, pending, truckLoadFraction: 0m).Chosen);
        Assert.True(Open.Decide(1, ready, truckLoadFraction: 0m).Chosen);
        Assert.Equal("ba:businesstype_giftshop", Open.Decide(1, ready, truckLoadFraction: 0m).Type);
    }

    [Fact]
    public void Open_stage_one_candidates_new_save_day_loop_carries_owned_family_count()
    {
        // Harvest: StarterOpens stamped ownedOfFamily=0 every day even with a live gift shop.
        var rows = Open.StageOneCandidates(
            Starter.StageOne(),
            new Dictionary<string, int> { ["specialty"] = 1, ["food"] = 0 },
            pendingOpen: "");

        var gift = Assert.Single(rows, row => row.Type == "ba:businesstype_giftshop");
        var food = Assert.Single(rows, row => row.Type == "ba:businesstype_fastfoodrestaurant");
        Assert.Equal(1, gift.OwnedOfFamily);
        Assert.Equal("specialty", gift.Family);
        Assert.Equal(0, food.OwnedOfFamily);
        Assert.Equal("food", food.Family);
    }

    [Fact]
    public void WinRun_next_new_save_day_loop_picks_where_after_pending_open()
    {
        // Harvest: playbook stayed on open because Where was always None after the open pick.
        WinRun.ClearPending();
        var sites = new[]
        {
            new SiteCandidate("ba:street_seventhavenue", 30, familyOnStreet: 0, traffic: 20m, dailyRent: 10m, cost: 5_000m),
        };
        var openFirst = WinRun.Next(
            stage: 1,
            opens: Open.StageOneCandidates(Starter.StageOne(), new Dictionary<string, int>(), ""),
            pendingOpen: "",
            sites,
            cash: 20_000m,
            reserve: 100m,
            money: MoneyPick.None(),
            run: RunPick.None());
        Assert.Equal("open", openFirst.Kind);
        Assert.Equal("ba:businesstype_giftshop", openFirst.Detail);

        WinRun.NotePending(openFirst.Detail);
        var whereNext = WinRun.Next(
            stage: 1,
            opens: Open.StageOneCandidates(
                Starter.StageOne(),
                new Dictionary<string, int> { ["specialty"] = 1 },
                WinRun.PendingOpen),
            pendingOpen: WinRun.PendingOpen,
            sites,
            cash: 20_000m,
            reserve: 100m,
            money: MoneyPick.None(),
            run: RunPick.None());

        Assert.Equal("where", whereNext.Kind);
        Assert.Equal("ba:street_seventhavenue 30", whereNext.Detail);
        WinRun.ClearPending();
    }

    [Fact]
    public void WinRun_next_blocks_open_while_owned_store_fails_readiness_gate()
    {
        // Harvest: open kept firing while Store #1 was closed / out of paper bags / flat customers.
        WinRun.ClearPending();
        var unhealthy = new[]
        {
            new StoreHealth(open: true, stocked: false, staffed: true, selling: false, profitable: false, "ba:street_seventhavenue 30"),
        };
        var step = WinRun.Next(
            stage: 1,
            opens: Open.StageOneCandidates(Starter.StageOne(), new Dictionary<string, int> { ["specialty"] = 1 }, ""),
            pendingOpen: "",
            sites: Array.Empty<SiteCandidate>(),
            cash: 20_000m,
            reserve: 100m,
            money: new MoneyPick("loan", 500m, 40, true),
            run: RunPick.None(),
            ownedHealth: unhealthy);

        Assert.NotEqual("open", step.Kind);
        Assert.Equal("money", step.Kind);
        WinRun.ClearPending();
    }

    [Fact]
    public void WinRun_next_new_save_day_loop_clears_pending_when_where_not_chosen_and_suppresses_open()
    {
        // Harvest: days 3–8 blank playbook — PendingOpen set, Where none, money/run starved, open never re-armed same tick.
        WinRun.ClearPending();
        WinRun.NotePending("ba:businesstype_giftshop");
        var money = new MoneyPick("loan", 500m, 40, true);
        var step = WinRun.Next(
            stage: 1,
            opens: Open.StageOneCandidates(
                Starter.StageOne(),
                new Dictionary<string, int> { ["specialty"] = 1 },
                WinRun.PendingOpen),
            pendingOpen: WinRun.PendingOpen,
            sites: Array.Empty<SiteCandidate>(),
            cash: 20_000m,
            reserve: 100m,
            money: money,
            run: RunPick.None());

        Assert.Equal("", WinRun.PendingOpen);
        Assert.True(step.Chosen);
        Assert.Equal("money", step.Kind);
        Assert.Equal("loan 500", step.Detail);
        Assert.NotEqual("open", step.Kind);

        var rearmed = WinRun.Next(
            stage: 1,
            opens: Open.StageOneCandidates(Starter.StageOne(), new Dictionary<string, int> { ["specialty"] = 1 }, ""),
            pendingOpen: WinRun.PendingOpen,
            sites: Array.Empty<SiteCandidate>(),
            cash: 20_000m,
            reserve: 100m,
            money: MoneyPick.None(),
            run: RunPick.None());
        Assert.Equal("open", rearmed.Kind);
        WinRun.ClearPending();
    }

    [Fact]
    public void WinRun_day_notes_pending_open_and_clears_on_where()
    {
        using var dir = new TempLog();
        Autopilot.Enabled = true;
        Autopilot.DryRun = false;
        Win.ClearApproved();
        WinRun.ClearPending();
        var api = GameApi.Live();
        var bar = TopBar.Line(12, 100m, 200m, 1m, 1, true, 1);

        var openDay = WinRun.Day(
            dir.Path,
            12,
            netWorth: 200m,
            api,
            ClearWorld(),
            LiveGuard(),
            heal => HealFate.Done,
            new PlaybookStep("open", "ba:businesstype_giftshop", true),
            Needs.Hold(),
            bar,
            Health.Section(null, null));
        Assert.True(openDay.Loop.Ran);
        Assert.Equal("ba:businesstype_giftshop", WinRun.PendingOpen);

        var whereDay = WinRun.Day(
            dir.Path,
            13,
            netWorth: 200m,
            api,
            ClearWorld(),
            LiveGuard(),
            heal => HealFate.Done,
            new PlaybookStep("where", "ba:street_seventhavenue 30", true),
            Needs.Hold(),
            TopBar.Line(13, 100m, 200m, 1m, 1, true, 1),
            Health.Section(null, null));
        Assert.True(whereDay.Loop.Ran);
        Assert.Equal("", WinRun.PendingOpen);
    }

    [Fact]
    public void Open_stage_one_candidates_blank_offers_are_empty()
    {
        Assert.Empty(Open.StageOneCandidates(null, null, ""));
        Assert.Empty(Open.StageOneCandidates(Array.Empty<StarterOffer>(), null, ""));
        Assert.Empty(Open.StageOneCandidates(Starter.StageOne(), null, "ba:businesstype_giftshop"));
    }

    [Fact]
    public void WinRun_autopilot_win_run_pending_clear_then_money_executes_action_bridge()
    {
        // Chain: win → autopilot → win-run (pending unlock → money bridge)
        using var dir = new TempLog();
        Autopilot.Enabled = true;
        Autopilot.DryRun = false;
        Win.ClearApproved();
        WinRun.ClearPending();
        WinRun.NotePending("ba:businesstype_giftshop");
        var api = GameApi.Live();
        var money = new MoneyPick("loan", 500m, 40, true);
        var step = WinRun.Next(
            stage: 1,
            opens: Open.StageOneCandidates(Starter.StageOne(), new Dictionary<string, int> { ["specialty"] = 1 }, WinRun.PendingOpen),
            pendingOpen: WinRun.PendingOpen,
            sites: Array.Empty<SiteCandidate>(),
            cash: 20_000m,
            reserve: 100m,
            money: money,
            run: RunPick.None());
        Assert.Equal("", WinRun.PendingOpen);
        Assert.Equal("money", step.Kind);

        var bridged = false;
        var day = WinRun.Day(
            dir.Path,
            9,
            netWorth: 14_000m,
            api,
            ClearWorld(),
            LiveGuard(),
            heal => HealFate.Done,
            step,
            Needs.Hold(),
            TopBar.Line(9, 14_000m, 14_000m, -106m, 1, true, 1),
            Health.Section(null, null),
            timeBridge: skip => true,
            actionBridge: (chosen, call) =>
            {
                bridged = chosen.Kind == "money" && call == api.Call("loan");
                return bridged;
            });

        Assert.True(day.Loop.Ran);
        Assert.True(bridged);
        Assert.Contains("autopilot.action ", day.Loop.Action);
        Assert.Contains("kind=money", day.Loop.Action);
        Assert.StartsWith("time.advanced ", day.Loop.Advanced);
        WinRun.ClearPending();
    }

    [Fact]
    public void WinRun_day_skips_win_record_when_autopilot_cleared()
    {
        using var dir = new TempLog();
        Autopilot.Enabled = true;
        Controls.Pause();
        Win.ClearApproved();

        var day = WinRun.Day(
            dir.Path,
            40,
            netWorth: TopBar.WinNetWorth,
            GameApi.Live(),
            ClearWorld(),
            LiveGuard(),
            heal => HealFate.Done,
            new PlaybookStep("money", "loan 2000", true),
            Needs.Hold(),
            TopBar.Line(40, 1m, 2m, 0m, 1, false, 0),
            Health.Section(null, null));

        Assert.False(day.Loop.Ran);
        Assert.Equal("", day.Reached);
        Assert.DoesNotContain("win.reached ", File.ReadAllText(LoadLog.CeoLogPath(dir.Path)));
    }

    private static WinRunFrame Frame(int day, decimal netWorth, decimal cash, decimal profit)
    {
        return new WinRunFrame(
            day,
            netWorth,
            TopBar.Line(day, cash, netWorth, profit, 1, true, 1),
            Health.Section(null, null),
            PlaybookStep.None(),
            Needs.Hold());
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

    private static int CountToken(string text, string token)
    {
        var count = 0;
        var at = 0;
        while ((at = text.IndexOf(token, at, StringComparison.Ordinal)) >= 0)
        {
            count++;
            at += token.Length;
        }

        return count;
    }

    private sealed class TempLog : IDisposable
    {
        public TempLog()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ceo-winrun-" + Guid.NewGuid().ToString("N"));
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
