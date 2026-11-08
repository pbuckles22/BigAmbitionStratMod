using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class RealEstateDecideTests
{
    [Fact]
    public void RealEstate_names_payback_max()
    {
        Assert.Equal(365, RealEstate.PaybackMax);
    }

    [Fact]
    public void RealEstate_decide_stage_four_buys_by_shortest_payback()
    {
        var pick = RealEstate.Decide(
            4,
            new[]
            {
                Building("ba:street_long", 1, "retail", price: 3_650m, dailyRent: 10m),
                Building("ba:street_short", 2, "retail", price: 1_000m, dailyRent: 10m),
                Building("ba:street_mid", 3, "retail", price: 2_000m, dailyRent: 10m),
            });

        Assert.True(pick.Chosen);
        Assert.Equal("ba:street_short", pick.Street);
        Assert.Equal(2, pick.Number);
        Assert.Equal("retail", pick.Kind);
        Assert.Equal(100m, pick.Days);
    }

    [Fact]
    public void RealEstate_decide_headquarters_beat_warehouses_and_retail()
    {
        var pick = RealEstate.Decide(
            4,
            new[]
            {
                Building("ba:street_shop", 1, "retail", price: 100m, dailyRent: 10m),
                Building("ba:street_hub", 2, "warehouse", price: 200m, dailyRent: 10m),
                Building("ba:street_hq", 3, "headquarters", price: 900m, dailyRent: 10m),
            });

        Assert.True(pick.Chosen);
        Assert.Equal("ba:street_hq", pick.Street);
        Assert.Equal(3, pick.Number);
        Assert.Equal("headquarters", pick.Kind);
        Assert.Equal(90m, pick.Days);
    }

    [Fact]
    public void RealEstate_decide_warehouses_beat_retail_when_no_headquarters()
    {
        var pick = RealEstate.Decide(
            4,
            new[]
            {
                Building("ba:street_shop", 1, "retail", price: 100m, dailyRent: 10m),
                Building("ba:street_hub", 2, "warehouse", price: 500m, dailyRent: 10m),
            });

        Assert.True(pick.Chosen);
        Assert.Equal("ba:street_hub", pick.Street);
        Assert.Equal("warehouse", pick.Kind);
    }

    [Fact]
    public void RealEstate_decide_a_building_over_payback_max_is_out()
    {
        var pick = RealEstate.Decide(
            4,
            new[]
            {
                Building("ba:street_slow", 1, "retail", price: 3_660m, dailyRent: 10m),
            });

        Assert.False(pick.Chosen);
        Assert.Equal("", pick.Street);
        Assert.Equal(0m, pick.Days);
    }

    [Fact]
    public void RealEstate_decide_a_tie_keeps_the_later_building()
    {
        var pick = RealEstate.Decide(
            4,
            new[]
            {
                Building("ba:street_first", 1, "retail", price: 1_000m, dailyRent: 10m),
                Building("ba:street_later", 2, "retail", price: 1_000m, dailyRent: 10m),
            });

        Assert.True(pick.Chosen);
        Assert.Equal("ba:street_later", pick.Street);
        Assert.Equal(2, pick.Number);
    }

    [Fact]
    public void RealEstate_decide_before_stage_four_buys_nothing()
    {
        var pick = RealEstate.Decide(
            3,
            new[]
            {
                Building("ba:street_hq", 1, "headquarters", price: 100m, dailyRent: 10m),
            });

        Assert.False(pick.Chosen);
        Assert.Equal("", pick.Street);
    }

    [Fact]
    public void RealEstate_decide_skips_blank_kind_and_zero_rent()
    {
        var blank = RealEstate.Decide(
            4,
            new[]
            {
                Building("ba:street_x", 1, "", price: 100m, dailyRent: 10m),
                Building("ba:street_y", 2, "retail", price: 100m, dailyRent: 0m),
            });
        var missing = RealEstate.Decide(4, null);

        Assert.False(blank.Chosen);
        Assert.False(missing.Chosen);
    }

    [Fact]
    public void RealEstate_decide_shortest_payback_wins_inside_the_same_kind()
    {
        var pick = RealEstate.Decide(
            4,
            new[]
            {
                Building("ba:street_slow_hq", 1, "headquarters", price: 2_000m, dailyRent: 10m),
                Building("ba:street_fast_hq", 2, "headquarters", price: 500m, dailyRent: 10m),
                Building("ba:street_shop", 3, "retail", price: 100m, dailyRent: 10m),
            });

        Assert.True(pick.Chosen);
        Assert.Equal("ba:street_fast_hq", pick.Street);
        Assert.Equal(2, pick.Number);
        Assert.Equal(50m, pick.Days);
    }

    private static EstateCandidate Building(
        string street,
        int number,
        string kind,
        decimal price,
        decimal dailyRent)
    {
        return new EstateCandidate(street, number, kind, price, dailyRent);
    }
}
