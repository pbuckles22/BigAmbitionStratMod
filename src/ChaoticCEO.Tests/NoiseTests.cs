using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class NoiseTests
{
    [Fact]
    public void Noise_draw_same_seed_and_day_is_deterministic_and_stays_inside_amps()
    {
        var first = Noise.Draw(42, 0);
        var again = Noise.Draw(42, 0);
        var otherSeed = Noise.Draw(7, 0);
        var otherDay = Noise.Draw(42, 1);

        Assert.Equal(first.Demand, again.Demand);
        Assert.Equal(first.Rival, again.Rival);
        Assert.Equal(first.Hiring, again.Hiring);
        Assert.Equal(1.030943155288696289062500m, first.Demand);
        Assert.Equal(0.0461376011371612548828125m, first.Rival);
        Assert.Equal(1.049296998977661132812500m, first.Hiring);
        Assert.NotEqual(first.Demand, otherSeed.Demand);
        Assert.NotEqual(first.Demand, otherDay.Demand);
        Assert.InRange(first.Demand, 1m - Noise.DemandNoise, 1m + Noise.DemandNoise);
        Assert.InRange(first.Rival, 0m, Noise.RivalNoise);
        Assert.InRange(first.Hiring, 1m - Noise.HireNoise, 1m + Noise.HireNoise);
        Assert.Equal(0.10m, Noise.DemandNoise);
        Assert.Equal(0.05m, Noise.RivalNoise);
        Assert.Equal(0.10m, Noise.HireNoise);
        Assert.Equal(365, Noise.SimHorizonDays);
    }

    [Fact]
    public void Noise_project_scales_sales_by_demand_and_rival_and_wages_by_hiring()
    {
        var curve = new StoreCurve(
            "ba:street_a",
            1,
            dailySales: 100m,
            goodsPerSale: 0.2m,
            dailyWages: 10m,
            dailyRent: 5m,
            dailyMarketing: 1m,
            dailyTheft: 0.5m,
            sampleDays: 1);
        var draw = new NoiseDraw(demand: 1.1m, rival: 0.05m, hiring: 0.9m);

        var row = Noise.Project(curve, draw);

        Assert.Equal("ba:street_a", row.Street);
        Assert.Equal(1, row.Number);
        Assert.Equal(104.5f, row.Sales);
        Assert.Equal(20.9f, row.Goods);
        Assert.Equal(9f, row.Wages);
        Assert.Equal(5f, row.Rent);
        Assert.Equal(1f, row.Marketing);
        Assert.Equal(0.5f, row.Theft);
        Assert.Equal(68.1f, row.Profit);
    }

    [Fact]
    public void Noise_project_without_draw_matches_curves_project()
    {
        var curve = new StoreCurve(
            "ba:street_b",
            2,
            dailySales: 50m,
            goodsPerSale: 0.2m,
            dailyWages: 8m,
            dailyRent: 4m,
            dailyMarketing: 0m,
            dailyTheft: 0m,
            sampleDays: 1);

        var quiet = Noise.Project(curve, null);
        var baseline = Curves.Project(curve);

        Assert.Equal(baseline.Sales, quiet.Sales);
        Assert.Equal(baseline.Goods, quiet.Goods);
        Assert.Equal(baseline.Wages, quiet.Wages);
        Assert.Equal(baseline.Profit, quiet.Profit);
    }

    [Fact]
    public void Noise_trial_same_seed_locks_net_worth_after_horizon_days()
    {
        var curves = new[]
        {
            new StoreCurve("ba:street_c", 3, 100m, 0.2m, 10m, 5m, 0m, 0m, 1),
        };

        var first = Noise.Trial(curves, startNetWorth: 1000m, seed: 42, horizonDays: 3);
        var again = Noise.Trial(curves, startNetWorth: 1000m, seed: 42, horizonDays: 3);

        Assert.Equal(first, again);
        Assert.Equal(1197.5243332342828210812513134m, decimal.Round(first, 28));
    }

    [Fact]
    public void Noise_run_locks_net_worth_distribution_at_sim_horizon_days()
    {
        var curves = new[]
        {
            new StoreCurve("ba:street_d", 4, 100m, 0.2m, 10m, 5m, 0m, 0m, 1),
        };

        var result = Noise.Run(curves, startNetWorth: 1000m, seeds: new[] { 1, 2, 3 });

        Assert.Equal(Noise.SimHorizonDays, result.HorizonDays);
        Assert.Equal(3, result.Trials);
        Assert.Equal(3, result.NetWorths.Count);
        Assert.Equal(result.NetWorths[0], result.Worst);
        Assert.Equal(result.NetWorths[^1], result.Best);
        Assert.Equal(result.NetWorths[1], result.Median);
        Assert.Equal(23916.0683077659m, decimal.Round(result.Worst, 10));
        Assert.Equal(23992.3609662518m, decimal.Round(result.Median, 10));
        Assert.Equal(24030.7126866982m, decimal.Round(result.Best, 10));
        Assert.True(result.Worst < result.Median);
        Assert.True(result.Median < result.Best);
    }

    [Fact]
    public void Noise_run_empty_or_null_inputs_is_empty_distribution()
    {
        var curves = new[]
        {
            new StoreCurve("ba:street_e", 5, 10m, 0.2m, 1m, 1m, 0m, 0m, 1),
        };

        Assert.Equal(0, Noise.Run(null, 0m, new[] { 1 }).Trials);
        Assert.Equal(0, Noise.Run(Array.Empty<StoreCurve>(), 0m, new[] { 1 }).Trials);
        Assert.Equal(0, Noise.Run(curves, 0m, null).Trials);
        Assert.Equal(0, Noise.Run(curves, 0m, Array.Empty<int>()).Trials);
        Assert.Equal(100m, Noise.Trial(null, 100m, 1, 10));
        Assert.Equal(100m, Noise.Trial(Array.Empty<StoreCurve>(), 100m, 1, 10));
        Assert.Equal("", Noise.Project(null, new NoiseDraw(1m, 0m, 1m)).Street);
    }

    [Fact]
    public void Noise_project_hiring_noise_alone_moves_wages_and_keeps_sales()
    {
        var curve = new StoreCurve("ba:street_f", 6, 100m, 0.2m, 10m, 5m, 0m, 0m, 1);
        var baseline = Curves.Project(curve);
        var noisy = Noise.Project(curve, new NoiseDraw(demand: 1m, rival: 0m, hiring: 1.1m));

        Assert.Equal(65f, baseline.Profit);
        Assert.Equal(baseline.Sales, noisy.Sales);
        Assert.Equal(11f, noisy.Wages);
        Assert.Equal(64f, noisy.Profit);
    }
}
