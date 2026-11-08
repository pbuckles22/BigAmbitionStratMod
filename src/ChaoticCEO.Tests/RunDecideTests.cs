using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class RunDecideTests
{
    [Fact]
    public void Run_names_need_floor_and_reuses_staff_ops_and_market_covers()
    {
        Assert.Equal(TimeAdvance.NeedFloor, Run.NeedFloor);
        Assert.Equal(StaffDemand.DemandDays, Run.DemandDays);
        Assert.Equal(StoreOps.TheftMax, Run.TheftMax);
        Assert.Equal(Marketing.MarketingCover, Run.MarketingCover);
        Assert.Equal(RivalTakeover.PaybackMax, Run.PaybackMax);
    }

    [Fact]
    public void Run_decide_sleeps_before_energy_hits_need_floor()
    {
        var pick = Run.Decide(Facts(
            energy: Run.NeedFloor,
            hunger: 80f,
            happiness: 80f));

        Assert.True(pick.Chosen);
        Assert.Equal("sleep", pick.Action);
    }

    [Fact]
    public void Run_decide_eats_before_hunger_hits_need_floor()
    {
        var pick = Run.Decide(Facts(
            energy: 80f,
            hunger: Run.NeedFloor,
            happiness: 80f));

        Assert.True(pick.Chosen);
        Assert.Equal("eat", pick.Action);
    }

    [Fact]
    public void Run_decide_happiness_at_need_floor_sleeps_and_eats_with_sleep_first()
    {
        var pick = Run.Decide(Facts(
            energy: 80f,
            hunger: 80f,
            happiness: Run.NeedFloor));

        Assert.True(pick.Chosen);
        Assert.Equal("sleep", pick.Action);
    }

    [Fact]
    public void Run_decide_meets_an_employee_demand_after_demand_days()
    {
        var pick = Run.Decide(Facts(
            unmetDemandDays: Run.DemandDays,
            employeeState: "ok"));

        Assert.True(pick.Chosen);
        Assert.Equal("demand", pick.Action);
    }

    [Fact]
    public void Run_decide_replaces_a_sick_retired_quitting_or_poached_employee()
    {
        Assert.Equal("replace", Run.Decide(Facts(employeeState: "sick")).Action);
        Assert.Equal("replace", Run.Decide(Facts(employeeState: "retired")).Action);
        Assert.Equal("replace", Run.Decide(Facts(employeeState: "quitting")).Action);
        Assert.Equal("replace", Run.Decide(Facts(employeeState: "poached")).Action);
        Assert.True(Run.Decide(Facts(employeeState: "sick")).Chosen);
    }

    [Fact]
    public void Run_decide_assigns_a_cleaner_when_the_store_has_dirt()
    {
        var pick = Run.Decide(Facts(hasDirt: true));

        Assert.True(pick.Chosen);
        Assert.Equal("cleaner", pick.Action);
    }

    [Fact]
    public void Run_decide_assigns_a_guard_when_theft_beats_theft_max()
    {
        var pick = Run.Decide(Facts(theft: 2m, sales: 100m));

        Assert.True(pick.Chosen);
        Assert.Equal("guard", pick.Action);
    }

    [Fact]
    public void Run_decide_meets_a_customer_demand_the_store_can_cover()
    {
        var pick = Run.Decide(Facts(unmetCustomerDemand: true));

        Assert.True(pick.Chosen);
        Assert.Equal("customer", pick.Action);
    }

    [Fact]
    public void Run_decide_starts_marketing_when_cost_stays_under_marketing_cover()
    {
        var pick = Run.Decide(Facts(marketingCost: 100m, marketingGain: 300m));

        Assert.True(pick.Chosen);
        Assert.Equal("marketing", pick.Action);
    }

    [Fact]
    public void Run_decide_skips_marketing_when_cost_covers_half_the_gain()
    {
        var pick = Run.Decide(Facts(marketingCost: 150m, marketingGain: 300m));

        Assert.False(pick.Chosen);
        Assert.Equal("", pick.Action);
    }

    [Fact]
    public void Run_decide_pays_taxes_that_are_due()
    {
        var pick = Run.Decide(Facts(taxDue: 55m));

        Assert.True(pick.Chosen);
        Assert.Equal("tax", pick.Action);
    }

    [Fact]
    public void Run_decide_declines_a_takeover_that_does_not_beat_payback()
    {
        var pick = Run.Decide(Facts(
            takeoverOffer: 1_000m,
            takeoverDailyProfit: 10m));

        Assert.True(pick.Chosen);
        Assert.Equal("decline", pick.Action);
    }

    [Fact]
    public void Run_decide_keeps_a_takeover_that_beats_payback()
    {
        var pick = Run.Decide(Facts(
            takeoverOffer: 10m * Run.PaybackMax,
            takeoverDailyProfit: 10m));

        Assert.False(pick.Chosen);
        Assert.Equal("", pick.Action);
    }

    [Fact]
    public void Run_decide_player_needs_beat_staff_and_market_actions()
    {
        var pick = Run.Decide(Facts(
            energy: Run.NeedFloor,
            unmetDemandDays: Run.DemandDays,
            employeeState: "sick",
            hasDirt: true,
            taxDue: 40m));

        Assert.True(pick.Chosen);
        Assert.Equal("sleep", pick.Action);
    }

    [Fact]
    public void Run_decide_staff_beats_store_ops_and_market()
    {
        var pick = Run.Decide(Facts(
            unmetDemandDays: Run.DemandDays,
            hasDirt: true,
            taxDue: 40m));

        Assert.True(pick.Chosen);
        Assert.Equal("demand", pick.Action);
    }

    [Fact]
    public void Run_decide_quiet_facts_need_no_action()
    {
        var pick = Run.Decide(Facts());

        Assert.False(pick.Chosen);
        Assert.Equal("", pick.Action);
    }

    [Fact]
    public void Run_decide_theft_at_exactly_theft_max_does_not_hire_a_guard()
    {
        var pick = Run.Decide(Facts(theft: 1m, sales: 100m));

        Assert.False(pick.Chosen);
        Assert.Equal("", pick.Action);
    }

    private static RunFacts Facts(
        float energy = 80f,
        float hunger = 80f,
        float happiness = 80f,
        int unmetDemandDays = 0,
        string employeeState = "ok",
        bool hasDirt = false,
        decimal theft = 0m,
        decimal sales = 100m,
        bool unmetCustomerDemand = false,
        decimal marketingCost = 0m,
        decimal marketingGain = 0m,
        decimal taxDue = 0m,
        decimal takeoverOffer = 0m,
        decimal takeoverDailyProfit = 0m)
    {
        return new RunFacts(
            energy,
            hunger,
            happiness,
            unmetDemandDays,
            employeeState,
            hasDirt,
            theft,
            sales,
            unmetCustomerDemand,
            marketingCost,
            marketingGain,
            taxDue,
            takeoverOffer,
            takeoverDailyProfit);
    }
}
