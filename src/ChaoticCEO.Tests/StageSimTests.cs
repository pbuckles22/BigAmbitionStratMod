using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class StageSimTests
{
    [Fact]
    public void StageSim_starting_thresholds_match_named_production_consts()
    {
        var t = StageThresholds.Starting();

        Assert.Equal(TopBar.WinNetWorth, t.WinNetWorth);
        Assert.Equal(Noise.SimHorizonDays, t.SimHorizonDays);
        Assert.Equal(Calibrate.Tolerance, t.CalibrateTol);
        Assert.Equal(Noise.DemandNoise, t.DemandNoise);
        Assert.Equal(Noise.RivalNoise, t.RivalNoise);
        Assert.Equal(Noise.HireNoise, t.HireNoise);
        Assert.Equal(Policy.WorstTol, t.PolicyWorstTol);
        Assert.Equal(Stage.Stage2Cash, t.Stage2Cash);
        Assert.Equal(Stage.Stage2Stores, t.Stage2Stores);
        Assert.Equal(Stage.Stage3Supplied, t.Stage3Supplied);
        Assert.Equal(Stage.Stage4Cash, t.Stage4Cash);
        Assert.Equal(Open.TruckBusy, t.TruckBusy);
        Assert.Equal(Where.ClusterMin, t.ClusterMin);
        Assert.Equal(Prices.PriceStep, t.PriceStep);
        Assert.Equal(Prices.PriceSatisfied, t.PriceSatisfied);
        Assert.Equal(Supply.RefillDays, t.RefillDays);
        Assert.Equal(Managers.HrSpan, t.HrSpan);
        Assert.Equal(Managers.HuntSpan, t.HuntSpan);
        Assert.Equal(Money.ReserveDays, t.ReserveDays);
        Assert.Equal(Money.DaySpend, t.DaySpend);
        Assert.Equal(Money.LoanCover, t.LoanCover);
        Assert.Equal(RealEstate.PaybackMax, t.PaybackMax);
        Assert.Equal(Close.FixDays, t.FixDays);
        Assert.Equal(Close.RedDays, t.RedDays);
        Assert.Equal(TimeAdvance.NeedFloor, t.NeedFloor);
        Assert.Equal(StaffDemand.DemandDays, t.DemandDays);
        Assert.Equal(StoreOps.TheftMax, t.TheftMax);
        Assert.Equal(Marketing.MarketingCover, t.MarketingCover);
        Assert.Equal(Audit.HealCap, t.HealCap);
        Assert.Equal(Audit.HealStrikeMax, t.HealStrikeMax);
    }

    [Fact]
    public void Stage_decide_with_thresholds_matches_consts_when_starting()
    {
        var t = StageThresholds.Starting();

        Assert.Equal(
            Stage.Decide(10, Stage.Stage2Cash, Stage.Stage2Stores, 0, 0, 0, 0),
            Stage.Decide(10, Stage.Stage2Cash, Stage.Stage2Stores, 0, 0, 0, 0, t));
        Assert.Equal(
            Stage.Decide(20, 1000m, 5, Stage.Stage3Supplied, 1, 0, 1),
            Stage.Decide(20, 1000m, 5, Stage.Stage3Supplied, 1, 0, 1, t));
        Assert.Equal(
            Stage.Decide(30, Stage.Stage4Cash, 5, 5, 2, 0, 1),
            Stage.Decide(30, Stage.Stage4Cash, 5, 5, 2, 0, 1, t));
    }

    [Fact]
    public void Stage_decide_with_lower_stage2_cash_enters_two_sooner()
    {
        // Winner: tuned. Lower Stage2Cash opens stage 2 at cash the starting gate still rejects.
        var tuned = StageThresholds.Starting().With(stage2Cash: 50_000m);

        Assert.Equal(1, Stage.Decide(5, 50_000m, 3, 0, 0, 0, 0));
        Assert.Equal(2, Stage.Decide(5, 50_000m, 3, 0, 0, 0, 0, tuned));
    }

    [Fact]
    public void StageSim_trial_walks_stages_via_epic9_decides_over_horizon()
    {
        var catalog = ClimbCatalog(storeProfit: 80_000m);
        var trial = StageSim.Trial(
            startCash: 20_000m,
            startNetWorth: 20_000m,
            catalog,
            seed: 7,
            horizonDays: 120);

        Assert.True(trial.Stage >= 2);
        Assert.True(trial.GoodsStores >= Stage.Stage2Stores);
        Assert.True(trial.NetWorth > 20_000m);
        Assert.Equal(
            Stage.Decide(
                120,
                trial.Cash,
                trial.GoodsStores,
                trial.SuppliedStores,
                trial.Warehouses,
                trial.EmptySlots,
                trial.Plans),
            trial.Stage);
    }

    [Fact]
    public void StageSim_run_reports_win_at_median_and_worst_against_win_net_worth()
    {
        var winCatalog = ClimbCatalog(storeProfit: 500_000m);
        var loseCatalog = ClimbCatalog(storeProfit: 1m);
        var seeds = new[] { 1, 2, 3 };

        var win = StageSim.Run(startCash: 50_000m, startNetWorth: 50_000m, winCatalog, seeds, horizonDays: 90);
        var lose = StageSim.Run(startCash: 1_000m, startNetWorth: 1_000m, loseCatalog, seeds, horizonDays: 30);

        Assert.Equal(90, win.HorizonDays);
        Assert.Equal(3, win.Trials);
        Assert.Equal(win.NetWorths[0], win.Worst);
        Assert.Equal(win.NetWorths[1], win.Median);
        Assert.Equal(win.NetWorths[^1], win.Best);
        Assert.True(win.Median >= TopBar.WinNetWorth);
        Assert.True(win.WinAtMedian);
        Assert.Equal(win.Worst >= TopBar.WinNetWorth, win.WinAtWorst);
        Assert.False(lose.WinAtMedian);
        Assert.False(lose.WinAtWorst);
        Assert.True(lose.Median < TopBar.WinNetWorth);
        Assert.Equal(Noise.SimHorizonDays, StageSim.Run(50_000m, 50_000m, winCatalog, seeds).HorizonDays);
    }

    [Fact]
    public void StageSim_run_empty_or_null_is_empty_result()
    {
        var catalog = ClimbCatalog(10m);

        Assert.Equal(0, StageSim.Run(0m, 0m, null, new[] { 1 }).Trials);
        Assert.Equal(0, StageSim.Run(0m, 0m, catalog, null).Trials);
        Assert.Equal(0, StageSim.Run(0m, 0m, catalog, Array.Empty<int>()).Trials);
        Assert.False(StageSim.Run(0m, 0m, null, new[] { 1 }).WinAtMedian);
        Assert.Equal(0m, StageSim.Trial(100m, 100m, null, 1, horizonDays: 5).NetWorth - 100m);
    }

    [Fact]
    public void StageSim_tune_picks_thresholds_that_reach_win_at_median_with_acceptable_worst()
    {
        // Winner: climb. Tight stage gates stay below WinNetWorth; climb lowers Stage2Cash/Stage4Cash so the same catalog wins.
        var catalog = ClimbCatalog(storeProfit: 200_000m);
        var seeds = new[] { 2, 3, 4 };
        var tight = new StageThresholdOption(
            "tight",
            StageThresholds.Starting().With(stage2Cash: 100_000_000m, stage4Cash: 200_000_000m));
        var climb = new StageThresholdOption(
            "climb",
            StageThresholds.Starting().With(stage2Cash: 30_000m, stage4Cash: 200_000m));

        var tightRun = StageSim.Run(40_000m, 40_000m, catalog, seeds, tight.Thresholds, horizonDays: 90);
        var climbRun = StageSim.Run(40_000m, 40_000m, catalog, seeds, climb.Thresholds, horizonDays: 90);
        var pick = StageSim.Tune(
            new[] { tight, climb },
            startCash: 40_000m,
            startNetWorth: 40_000m,
            catalog,
            seeds,
            horizonDays: 90);

        Assert.False(tightRun.WinAtMedian);
        Assert.True(climbRun.WinAtMedian);
        Assert.True(Policy.Acceptable(climbRun.Worst, climbRun.Median));
        Assert.Equal("climb", pick.Name);
        Assert.True(pick.WinAtMedian);
        Assert.Equal(climbRun.Median, pick.Median);
        Assert.Equal(climbRun.Worst, pick.Worst);
        Assert.Equal(30_000m, pick.Thresholds.Stage2Cash);
        Assert.Equal(200_000m, pick.Thresholds.Stage4Cash);
    }

    [Fact]
    public void StageSim_tune_median_tie_keeps_earlier_name()
    {
        var catalog = ClimbCatalog(storeProfit: 50m);
        var first = new StageThresholdOption("first", StageThresholds.Starting());
        var second = new StageThresholdOption("second", StageThresholds.Starting());

        var pick = StageSim.Tune(
            new[] { first, second },
            startCash: 1_000m,
            startNetWorth: 1_000m,
            catalog,
            seeds: new[] { 9, 10, 11 },
            horizonDays: 10);

        Assert.Equal("first", pick.Name);
        Assert.Equal(
            StageSim.Run(1_000m, 1_000m, catalog, new[] { 9, 10, 11 }, first.Thresholds, 10).Median,
            pick.Median);
    }

    [Fact]
    public void StageSim_stage_one_caps_opens_at_stage2_stores_until_cash_gate()
    {
        // Winner: stage-1 cap. With Stage2Cash above what three stores earn, goods stay at Stage2Stores.
        var catalog = ClimbCatalog(storeProfit: 1_000m);
        var gates = StageThresholds.Starting().With(stage2Cash: 10_000_000m);
        var trial = StageSim.Trial(
            startCash: 20_000m,
            startNetWorth: 20_000m,
            catalog,
            seed: 3,
            thresholds: gates,
            horizonDays: 40);

        Assert.Equal(1, trial.Stage);
        Assert.Equal(Stage.Stage2Stores, trial.GoodsStores);
        Assert.Equal(gates.Stage2Stores, trial.GoodsStores);
    }

    [Fact]
    public void StageSim_tune_empty_or_null_is_none()
    {
        var catalog = ClimbCatalog(10m);
        var one = new StageThresholdOption("one", StageThresholds.Starting());

        Assert.Equal("", StageSim.Tune(null, 0m, 0m, catalog, new[] { 1 }).Name);
        Assert.Equal(0, StageSim.Tune(null, 0m, 0m, catalog, new[] { 1 }).Trials);
        Assert.Equal("", StageSim.Tune(Array.Empty<StageThresholdOption>(), 0m, 0m, catalog, new[] { 1 }).Name);
        Assert.Equal("", StageSim.Tune(new[] { one }, 0m, 0m, null, new[] { 1 }).Name);
        Assert.Equal("", StageSim.Tune(new[] { one }, 0m, 0m, catalog, null).Name);
        Assert.Equal("", StageSim.Tune(new[] { new StageThresholdOption("", StageThresholds.Starting()) }, 0m, 0m, catalog, new[] { 1 }).Name);
    }

    [Fact]
    public void StageSim_tune_skips_blank_name_and_unacceptable_worst()
    {
        var catalog = ClimbCatalog(storeProfit: 250_000m);
        var seeds = new[] { 1, 2, 3, 4, 5 };
        var blank = new StageThresholdOption("", StageThresholds.Starting().With(stage2Cash: 20_000m));
        var stable = new StageThresholdOption(
            "stable",
            StageThresholds.Starting().With(stage2Cash: 40_000m, stage4Cash: 250_000m));
        var wild = new StageThresholdOption(
            "wild",
            StageThresholds.Starting().With(stage2Cash: 10_000m, stage4Cash: 100_000m, demandNoise: 0.95m));

        var stableRun = StageSim.Run(50_000m, 50_000m, catalog, seeds, stable.Thresholds, 60);
        var wildRun = StageSim.Run(50_000m, 50_000m, catalog, seeds, wild.Thresholds, 60);
        var pick = StageSim.Tune(
            new[] { blank, wild, stable },
            50_000m,
            50_000m,
            catalog,
            seeds,
            horizonDays: 60);

        Assert.True(stableRun.WinAtMedian || stableRun.Median > 0m);
        if (wildRun.Median > stableRun.Median && !Policy.Acceptable(wildRun.Worst, wildRun.Median))
        {
            Assert.Equal("stable", pick.Name);
            Assert.True(Policy.Acceptable(pick.Worst, pick.Median));
        }
        else
        {
            Assert.True(pick.Name == "stable" || pick.Name == "wild");
            Assert.True(pick.Name.Length > 0);
        }
    }

    private static StageSimCatalog ClimbCatalog(decimal storeProfit)
    {
        var opens = new List<OpenCandidate>();
        var sites = new List<SiteCandidate>();
        for (var i = 1; i <= 8; i++)
        {
            opens.Add(new OpenCandidate(
                "ba:businesstype_giftshop",
                "goods",
                storeProfit,
                wholesalerSells: true,
                "specialty",
                ownedOfFamily: 0,
                familyHasWarehouse: false));
            sites.Add(new SiteCandidate(
                "ba:street_gift" + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                i,
                familyOnStreet: 0,
                traffic: 20m,
                dailyRent: 10m,
                cost: 5_000m));
        }

        return new StageSimCatalog(
            opens,
            sites,
            loans: new[] { new LoanOffer(100_000m, 50) },
            estates: new[]
            {
                new EstateCandidate("ba:street_hq", 1, "headquarters", 50_000m, 200m),
            },
            item: "ba:itemname_cheapgift",
            warehouseName: "W1",
            truckCost: 1_000m);
    }
}
