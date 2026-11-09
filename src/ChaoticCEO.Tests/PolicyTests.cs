using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class PolicyTests
{
    [Fact]
    public void Policy_acceptable_locks_worst_tol_against_own_median()
    {
        Assert.Equal(0.98m, Policy.WorstTol);
        Assert.True(Policy.Acceptable(980m, 1000m));
        Assert.True(Policy.Acceptable(1000m, 1000m));
        Assert.False(Policy.Acceptable(979m, 1000m));
        Assert.True(Policy.Acceptable(-100m, -100m));
        Assert.False(Policy.Acceptable(-103m, -100m));
        Assert.True(Policy.Acceptable(0m, 0m));
        Assert.False(Policy.Acceptable(-1m, 0m));
    }

    [Fact]
    public void Policy_compare_picks_higher_median_when_worst_stays_acceptable()
    {
        var keep = new PolicyOption(
            "keep",
            new[]
            {
                new StoreCurve("ba:street_keep", 1, 100m, 0.2m, 10m, 5m, 0m, 0m, 1),
                new StoreCurve("ba:street_loss", 2, 10m, 0.2m, 40m, 5m, 0m, 0m, 1),
            });
        var close = new PolicyOption(
            "close",
            new[]
            {
                new StoreCurve("ba:street_keep", 1, 100m, 0.2m, 10m, 5m, 0m, 0m, 1),
            });
        var seeds = new[] { 1, 2, 3 };

        var keepNoise = Noise.Run(keep.Curves, startNetWorth: 1000m, seeds: seeds, horizonDays: 30);
        var closeNoise = Noise.Run(close.Curves, startNetWorth: 1000m, seeds: seeds, horizonDays: 30);
        var pick = Policy.Compare(new[] { keep, close }, startNetWorth: 1000m, seeds: seeds, horizonDays: 30);

        Assert.True(closeNoise.Median > keepNoise.Median);
        Assert.True(Policy.Acceptable(closeNoise.Worst, closeNoise.Median));
        Assert.Equal("close", pick.Name);
        Assert.True(pick.Acceptable);
        Assert.Equal(closeNoise.Median, pick.Median);
        Assert.Equal(closeNoise.Worst, pick.Worst);
        Assert.Equal(closeNoise.Best, pick.Best);
        Assert.Equal(Noise.SimHorizonDays, Policy.Compare(new[] { keep, close }, 1000m, seeds).Noise.HorizonDays);
    }

    [Fact]
    public void Policy_compare_keeps_stable_when_higher_median_breaks_worst_tol()
    {
        // Winner: keep. Gamble has the higher median but its worst/median fails WorstTol.
        var keep = new PolicyOption(
            "keep",
            new[]
            {
                new StoreCurve("ba:street_steady", 1, 80m, 0.2m, 8m, 4m, 0m, 0m, 1),
            });
        var gamble = new PolicyOption(
            "gamble",
            new[]
            {
                new StoreCurve("ba:street_steady", 1, 80m, 0.2m, 8m, 4m, 0m, 0m, 1),
                new StoreCurve("ba:street_risk", 2, 300m, 0.05m, 5m, 200m, 0m, 0m, 1),
            });
        var seeds = new[] { 1, 2, 3, 4, 5 };

        var keepNoise = Noise.Run(keep.Curves, startNetWorth: 1000m, seeds: seeds, horizonDays: 60);
        var gambleNoise = Noise.Run(gamble.Curves, startNetWorth: 1000m, seeds: seeds, horizonDays: 60);
        var pick = Policy.Compare(new[] { keep, gamble }, startNetWorth: 1000m, seeds: seeds, horizonDays: 60);

        Assert.True(gambleNoise.Median > keepNoise.Median);
        Assert.True(Policy.Acceptable(keepNoise.Worst, keepNoise.Median));
        Assert.False(Policy.Acceptable(gambleNoise.Worst, gambleNoise.Median));
        Assert.Equal("keep", pick.Name);
        Assert.True(pick.Acceptable);
        Assert.Equal(keepNoise.Median, pick.Median);
        Assert.Equal(keepNoise.Worst, pick.Worst);
    }

    [Fact]
    public void Policy_compare_median_tie_keeps_earlier()
    {
        // Winner: first. Same curves yield the same median; earlier name wins.
        var first = new PolicyOption(
            "first",
            new[] { new StoreCurve("ba:street_a", 1, 50m, 0.2m, 5m, 2m, 0m, 0m, 1) });
        var second = new PolicyOption(
            "second",
            new[] { new StoreCurve("ba:street_a", 1, 50m, 0.2m, 5m, 2m, 0m, 0m, 1) });

        var pick = Policy.Compare(new[] { first, second }, startNetWorth: 500m, seeds: new[] { 9, 10, 11 }, horizonDays: 10);

        Assert.Equal("first", pick.Name);
        Assert.Equal(pick.Median, Noise.Run(first.Curves, 500m, new[] { 9, 10, 11 }, 10).Median);
    }

    [Fact]
    public void Policy_compare_empty_or_null_is_none()
    {
        var one = new PolicyOption(
            "one",
            new[] { new StoreCurve("ba:street_z", 9, 10m, 0.2m, 1m, 1m, 0m, 0m, 1) });

        Assert.Equal("", Policy.Compare(null, 0m, new[] { 1 }).Name);
        Assert.False(Policy.Compare(null, 0m, new[] { 1 }).Acceptable);
        Assert.Equal(0, Policy.Compare(null, 0m, new[] { 1 }).Noise.Trials);
        Assert.Equal("", Policy.Compare(Array.Empty<PolicyOption>(), 0m, new[] { 1 }).Name);
        Assert.Equal("", Policy.Compare(new[] { one }, 0m, null).Name);
        Assert.Equal("", Policy.Compare(new[] { one }, 0m, Array.Empty<int>()).Name);
        Assert.Equal("", Policy.Compare(new[] { new PolicyOption("", Array.Empty<StoreCurve>()) }, 0m, new[] { 1 }).Name);
    }

    [Fact]
    public void Policy_compare_skips_blank_name_and_picks_next_acceptable()
    {
        var blank = new PolicyOption(
            "",
            new[] { new StoreCurve("ba:street_blank", 1, 100m, 0.2m, 10m, 5m, 0m, 0m, 1) });
        var named = new PolicyOption(
            "named",
            new[] { new StoreCurve("ba:street_named", 2, 120m, 0.2m, 10m, 5m, 0m, 0m, 1) });

        var pick = Policy.Compare(new[] { blank, named }, startNetWorth: 1000m, seeds: new[] { 2, 3, 4 }, horizonDays: 20);

        Assert.Equal("named", pick.Name);
        Assert.True(pick.Acceptable);
        Assert.Equal(
            Noise.Run(named.Curves, 1000m, new[] { 2, 3, 4 }, 20).Median,
            pick.Median);
    }
}
