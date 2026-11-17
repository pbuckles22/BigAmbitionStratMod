using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class DistrictPaceTests
{
    [Fact]
    public void Where_names_the_district_caps()
    {
        Assert.Equal(3, Where.WholesalerDistrictMax);
        Assert.Equal(7, Where.PreGoldDistrictMax);
        Assert.Equal(Where.WholesalerDistrictMax, Where.Cap(importerRunning: false, goldOn: false));
        Assert.Equal(Where.PreGoldDistrictMax, Where.Cap(importerRunning: true, goldOn: false));
        Assert.Equal(int.MaxValue, Where.Cap(importerRunning: true, goldOn: true));
    }

    [Fact]
    public void Where_decide_a_fourth_business_on_a_wholesaler_neighborhood_is_not_the_site()
    {
        var cap = Where.Cap(importerRunning: false, goldOn: false);
        var pick = Where.Decide(WholesalerLots(), cash: 1000m, reserve: 100m, cap);
        var third = Where.Decide(ThirdStoreLots(), cash: 1000m, reserve: 100m, cap);

        Assert.Equal(3, cap);
        Assert.True(pick.Chosen);
        Assert.Equal("ba:street_tenthavenue", pick.Street);
        Assert.Equal(12, pick.Number);
        Assert.NotEqual("ba:street_seventhavenue", pick.Street);
        Assert.True(third.Chosen);
        Assert.Equal("ba:street_seventhavenue", third.Street);
        Assert.Equal(22, third.Number);
    }

    [Fact]
    public void Where_decide_gold_without_an_importer_still_sends_the_fourth_elsewhere()
    {
        var cap = Where.Cap(importerRunning: false, goldOn: true);
        var pick = Where.Decide(WholesalerLots(), cash: 1000m, reserve: 100m, cap);

        Assert.Equal(Where.WholesalerDistrictMax, cap);
        Assert.True(pick.Chosen);
        Assert.Equal("ba:street_tenthavenue", pick.Street);
        Assert.Equal(12, pick.Number);
        Assert.NotEqual("ba:street_seventhavenue", pick.Street);
    }

    [Fact]
    public void Where_decide_an_importer_neighborhood_at_seven_is_not_the_site()
    {
        var cap = Where.Cap(importerRunning: true, goldOn: false);
        var blocked = Where.Decide(ImporterLots(), cash: 1000m, reserve: 100m, cap);
        var open = Where.Decide(
            new[]
            {
                Lot("ba:street_seventhavenue", 22, "garment", businesses: 6, familyOnStreet: Where.ClusterMin, traffic: 20m),
                Lot("ba:street_tenthavenue", 12, "hells", businesses: 0, familyOnStreet: 0, traffic: 90m),
            },
            cash: 1000m,
            reserve: 100m,
            cap);

        Assert.Equal(7, cap);
        Assert.True(blocked.Chosen);
        Assert.Equal("ba:street_tenthavenue", blocked.Street);
        Assert.Equal(12, blocked.Number);
        Assert.True(open.Chosen);
        Assert.Equal("ba:street_seventhavenue", open.Street);
        Assert.Equal(22, open.Number);
    }

    [Fact]
    public void Where_decide_gold_lifts_the_cap()
    {
        var cap = Where.Cap(importerRunning: true, goldOn: true);
        var pick = Where.Decide(ImporterLots(), cash: 1000m, reserve: 100m, cap);

        Assert.Equal(int.MaxValue, cap);
        Assert.True(pick.Chosen);
        Assert.Equal("ba:street_seventhavenue", pick.Street);
        Assert.Equal(22, pick.Number);
    }

    [Fact]
    public void Where_decide_a_tie_under_the_cap_keeps_the_later_building()
    {
        var cap = Where.Cap(importerRunning: false, goldOn: false);
        var pick = Where.Decide(
            new[]
            {
                Lot("ba:street_seventhavenue", 22, "garment", businesses: 3, familyOnStreet: Where.ClusterMin, traffic: 90m),
                Lot("ba:street_tenthavenue", 12, "hells", businesses: 0, familyOnStreet: 0, traffic: 40m),
                Lot("ba:street_eighthavenue", 4, "midtown", businesses: 1, familyOnStreet: 0, traffic: 80m, dailyRent: 20m),
            },
            cash: 1000m,
            reserve: 100m,
            cap);

        Assert.True(pick.Chosen);
        Assert.Equal("ba:street_eighthavenue", pick.Street);
        Assert.Equal(4, pick.Number);
        Assert.NotEqual("ba:street_seventhavenue", pick.Street);
    }

    [Fact]
    public void Where_decide_a_full_neighborhood_is_not_a_fallback()
    {
        var cap = Where.Cap(importerRunning: false, goldOn: false);
        var bothFull = Where.Decide(
            new[]
            {
                Lot("ba:street_seventhavenue", 22, "garment", businesses: 3, familyOnStreet: Where.ClusterMin, traffic: 80m),
                Lot("ba:street_tenthavenue", 12, "hells", businesses: 3, familyOnStreet: 0, traffic: 10m),
            },
            cash: 1000m,
            reserve: 100m,
            cap);
        var otherOverReserve = Where.Decide(
            new[]
            {
                Lot("ba:street_seventhavenue", 22, "garment", businesses: 3, familyOnStreet: Where.ClusterMin, traffic: 80m),
                Lot("ba:street_tenthavenue", 12, "hells", businesses: 0, familyOnStreet: 0, traffic: 10m, cost: 950m),
            },
            cash: 1000m,
            reserve: 100m,
            cap);

        Assert.False(bothFull.Chosen);
        Assert.Equal("", bothFull.Street);
        Assert.Equal(0, bothFull.Number);
        Assert.False(otherOverReserve.Chosen);
        Assert.Equal("", otherOverReserve.Street);
    }

    [Fact]
    public void Chain_the_importer_flag_sets_the_cap_and_where_sends_store_four_elsewhere()
    {
        // Chain: importer flag → cap → where
        var importerRunning = false;
        var cap = Where.Cap(importerRunning, goldOn: false);
        var pick = Where.Decide(WholesalerLots(), cash: 1000m, reserve: 100m, cap);
        var step = Playbook.Decide(null, pick, null, null, null, null, null, null, null, null, null, null);

        Assert.Equal(Where.WholesalerDistrictMax, cap);
        Assert.True(pick.Chosen);
        Assert.Equal("ba:street_tenthavenue", pick.Street);
        Assert.Equal(12, pick.Number);
        Assert.True(step.Chosen);
        Assert.Equal("where", step.Kind);
        Assert.Equal("ba:street_tenthavenue 12", step.Detail);
        Assert.Equal("Next where ba:street_tenthavenue 12", Playbook.Line(step));
    }

    [Fact]
    public void Chain_an_importer_cap_keeps_the_cluster_that_already_holds_three()
    {
        // Chain: importer flag → cap → where
        var importerRunning = true;
        var cap = Where.Cap(importerRunning, goldOn: false);
        var pick = Where.Decide(
            new[]
            {
                Lot("ba:street_seventhavenue", 22, "garment", businesses: 3, familyOnStreet: Where.ClusterMin, traffic: 20m),
                Lot("ba:street_seventhavenue", 30, "garment", businesses: 3, familyOnStreet: Where.ClusterMin, traffic: 15m),
                Lot("ba:street_tenthavenue", 12, "hells", businesses: 0, familyOnStreet: 0, traffic: 90m),
            },
            cash: 1000m,
            reserve: 100m,
            cap);
        var step = Playbook.Decide(null, pick, null, null, null, null, null, null, null, null, null, null);

        Assert.Equal(Where.PreGoldDistrictMax, cap);
        Assert.True(cap > Where.WholesalerDistrictMax);
        Assert.True(pick.Chosen);
        Assert.Equal("ba:street_seventhavenue", pick.Street);
        Assert.Equal(22, pick.Number);
        Assert.Equal("where", step.Kind);
        Assert.Equal("ba:street_seventhavenue 22", step.Detail);
        Assert.NotEqual("ba:street_tenthavenue 12", step.Detail);
    }

    [Fact]
    public void Chain_gold_lifts_the_cap_and_where_keeps_the_eighth_on_the_cluster()
    {
        // Chain: importer flag → cap → where
        var importerRunning = true;
        var goldOn = true;
        var cap = Where.Cap(importerRunning, goldOn);
        var pick = Where.Decide(ImporterLots(), cash: 1000m, reserve: 100m, cap);
        var step = Playbook.Decide(null, pick, null, null, null, null, null, null, null, null, null, null);

        Assert.Equal(int.MaxValue, cap);
        Assert.True(pick.Chosen);
        Assert.Equal("ba:street_seventhavenue", pick.Street);
        Assert.Equal(22, pick.Number);
        Assert.Equal("where", step.Kind);
        Assert.Equal("ba:street_seventhavenue 22", step.Detail);
        Assert.Equal("Next where ba:street_seventhavenue 22", Playbook.Line(step));
    }

    private static SiteCandidate[] WholesalerLots()
    {
        return new[]
        {
            Lot("ba:street_seventhavenue", 22, "garment", businesses: 3, familyOnStreet: 3, traffic: 80m),
            Lot("ba:street_seventhavenue", 30, "garment", businesses: 3, familyOnStreet: 3, traffic: 70m),
            Lot("ba:street_tenthavenue", 12, "hells", businesses: 1, familyOnStreet: 1, traffic: 15m),
        };
    }

    private static SiteCandidate[] ThirdStoreLots()
    {
        return new[]
        {
            Lot("ba:street_seventhavenue", 22, "garment", businesses: 2, familyOnStreet: Where.ClusterMin, traffic: 20m),
            Lot("ba:street_tenthavenue", 12, "hells", businesses: 0, familyOnStreet: 0, traffic: 90m),
        };
    }

    private static SiteCandidate[] ImporterLots()
    {
        return new[]
        {
            Lot("ba:street_seventhavenue", 22, "garment", businesses: 7, familyOnStreet: Where.ClusterMin, traffic: 80m),
            Lot("ba:street_seventhavenue", 30, "garment", businesses: 7, familyOnStreet: Where.ClusterMin, traffic: 70m),
            Lot("ba:street_tenthavenue", 12, "hells", businesses: 6, familyOnStreet: 0, traffic: 15m),
        };
    }

    private static SiteCandidate Lot(
        string street,
        int number,
        string neighborhood,
        int businesses,
        int familyOnStreet,
        decimal traffic,
        decimal cost = 100m,
        decimal dailyRent = 10m)
    {
        return new SiteCandidate(
            street,
            number,
            familyOnStreet,
            traffic,
            dailyRent,
            cost,
            neighborhood,
            businesses);
    }
}
