using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class WhereTests
{
    [Fact]
    public void Where_names_cluster_min()
    {
        Assert.Equal(2, Where.ClusterMin);
    }

    [Fact]
    public void Where_decide_a_cluster_street_beats_a_higher_traffic_ratio()
    {
        var pick = Where.Decide(
            new[]
            {
                new SiteCandidate("ba:street_lonely", 9, familyOnStreet: 1, traffic: 100m, dailyRent: 10m, cost: 100m),
                new SiteCandidate("ba:street_cluster", 3, familyOnStreet: Where.ClusterMin, traffic: 20m, dailyRent: 10m, cost: 100m),
                new SiteCandidate("ba:street_cluster", 4, familyOnStreet: Where.ClusterMin, traffic: 15m, dailyRent: 10m, cost: 100m),
            },
            cash: 1000m,
            reserve: 100m);

        Assert.True(pick.Chosen);
        Assert.Equal("ba:street_cluster", pick.Street);
        Assert.Equal(3, pick.Number);
    }

    [Fact]
    public void Where_decide_without_a_cluster_picks_the_larger_traffic_per_rent()
    {
        var pick = Where.Decide(
            new[]
            {
                new SiteCandidate("ba:street_a", 1, familyOnStreet: 0, traffic: 40m, dailyRent: 10m, cost: 50m),
                new SiteCandidate("ba:street_b", 2, familyOnStreet: 1, traffic: 90m, dailyRent: 30m, cost: 50m),
                new SiteCandidate("ba:street_c", 3, familyOnStreet: 0, traffic: 50m, dailyRent: 10m, cost: 50m),
            },
            cash: 1000m,
            reserve: 100m);

        Assert.True(pick.Chosen);
        Assert.Equal("ba:street_c", pick.Street);
        Assert.Equal(3, pick.Number);
    }

    [Fact]
    public void Where_decide_a_tie_keeps_the_later_building()
    {
        var pick = Where.Decide(
            new[]
            {
                new SiteCandidate("ba:street_a", 1, familyOnStreet: 0, traffic: 50m, dailyRent: 10m, cost: 50m),
                new SiteCandidate("ba:street_b", 2, familyOnStreet: 0, traffic: 100m, dailyRent: 20m, cost: 50m),
            },
            cash: 1000m,
            reserve: 100m);

        Assert.True(pick.Chosen);
        Assert.Equal("ba:street_b", pick.Street);
        Assert.Equal(2, pick.Number);
    }

    [Fact]
    public void Where_decide_a_building_over_the_cash_reserve_is_not_a_choice()
    {
        var pick = Where.Decide(
            new[]
            {
                new SiteCandidate("ba:street_rich", 1, familyOnStreet: Where.ClusterMin, traffic: 100m, dailyRent: 10m, cost: 950m),
                new SiteCandidate("ba:street_ok", 2, familyOnStreet: 0, traffic: 20m, dailyRent: 10m, cost: 100m),
            },
            cash: 1000m,
            reserve: 100m);
        var none = Where.Decide(
            new[]
            {
                new SiteCandidate("ba:street_rich", 1, familyOnStreet: 0, traffic: 100m, dailyRent: 10m, cost: 950m),
            },
            cash: 1000m,
            reserve: 100m);

        Assert.True(pick.Chosen);
        Assert.Equal("ba:street_ok", pick.Street);
        Assert.Equal(2, pick.Number);
        Assert.False(none.Chosen);
        Assert.Equal("", none.Street);
        Assert.Equal(0, none.Number);
    }

    [Fact]
    public void Where_decide_skips_blank_zero_rent_and_empty_lists()
    {
        var pick = Where.Decide(
            new[]
            {
                new SiteCandidate("", 1, familyOnStreet: 0, traffic: 100m, dailyRent: 10m, cost: 10m),
                new SiteCandidate("ba:street_a", 0, familyOnStreet: 0, traffic: 100m, dailyRent: 10m, cost: 10m),
                new SiteCandidate("ba:street_b", 2, familyOnStreet: 0, traffic: 100m, dailyRent: 0m, cost: 10m),
                new SiteCandidate("ba:street_c", 3, familyOnStreet: 0, traffic: 40m, dailyRent: 10m, cost: 10m),
            },
            cash: 1000m,
            reserve: 100m);
        var empty = Where.Decide(Array.Empty<SiteCandidate>(), 1000m, 100m);
        var missing = Where.Decide(null, 1000m, 100m);

        Assert.True(pick.Chosen);
        Assert.Equal("ba:street_c", pick.Street);
        Assert.Equal(3, pick.Number);
        Assert.False(empty.Chosen);
        Assert.False(missing.Chosen);
    }

    [Fact]
    public void Where_decide_a_cost_equal_to_cash_minus_reserve_still_wins()
    {
        var pick = Where.Decide(
            new[]
            {
                new SiteCandidate("ba:street_edge", 5, familyOnStreet: 0, traffic: 10m, dailyRent: 10m, cost: 900m),
            },
            cash: 1000m,
            reserve: 100m);

        Assert.True(pick.Chosen);
        Assert.Equal("ba:street_edge", pick.Street);
        Assert.Equal(5, pick.Number);
    }
}
