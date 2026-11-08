using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class ManagerDecideTests
{
    [Fact]
    public void Managers_names_hr_and_hunt_spans()
    {
        Assert.Equal(25, Managers.HrSpan);
        Assert.Equal(5, Managers.HuntSpan);
    }

    [Fact]
    public void Managers_decide_stage_two_creates_a_missing_pricing_plan_before_any_hire()
    {
        var pick = Managers.Decide(
            2,
            new[] { "W1" },
            employees: 10,
            openRoles: 2,
            seats: Array.Empty<ManagerSeat>(),
            pricingPlanExists: false);

        Assert.True(pick.Chosen);
        Assert.Equal("pricing", pick.Role);
        Assert.Equal("plan", pick.Action);
        Assert.Equal("", pick.Warehouse);
    }

    [Fact]
    public void Managers_decide_stage_two_hires_the_first_missing_baseline_seat()
    {
        var pick = Managers.Decide(
            2,
            new[] { "W1" },
            employees: 10,
            openRoles: 2,
            seats: new[]
            {
                Seat("logistics", "W1", assigned: true),
            },
            pricingPlanExists: true);

        Assert.True(pick.Chosen);
        Assert.Equal("purchasing", pick.Role);
        Assert.Equal("hire", pick.Action);
        Assert.Equal("W1", pick.Warehouse);
    }

    [Fact]
    public void Managers_decide_one_logistics_and_one_purchasing_per_warehouse()
    {
        var pick = Managers.Decide(
            3,
            new[] { "W1", "W2" },
            employees: 10,
            openRoles: 2,
            seats: new[]
            {
                Seat("logistics", "W1", assigned: true),
                Seat("purchasing", "W1", assigned: true),
                Seat("hr", "", assigned: true),
                Seat("headhunter", "", assigned: true),
            },
            pricingPlanExists: true);

        Assert.True(pick.Chosen);
        Assert.Equal("logistics", pick.Role);
        Assert.Equal("hire", pick.Action);
        Assert.Equal("W2", pick.Warehouse);
    }

    [Fact]
    public void Managers_decide_hr_scales_by_hr_span()
    {
        var pick = Managers.Decide(
            3,
            new[] { "W1" },
            employees: Managers.HrSpan + 1,
            openRoles: 1,
            seats: new[]
            {
                Seat("logistics", "W1", assigned: true),
                Seat("purchasing", "W1", assigned: true),
                Seat("hr", "", assigned: true),
                Seat("headhunter", "", assigned: true),
            },
            pricingPlanExists: true);

        Assert.True(pick.Chosen);
        Assert.Equal("hr", pick.Role);
        Assert.Equal("hire", pick.Action);
        Assert.Equal("", pick.Warehouse);
    }

    [Fact]
    public void Managers_decide_headhunter_scales_by_hunt_span()
    {
        var pick = Managers.Decide(
            3,
            new[] { "W1" },
            employees: 10,
            openRoles: Managers.HuntSpan + 1,
            seats: new[]
            {
                Seat("logistics", "W1", assigned: true),
                Seat("purchasing", "W1", assigned: true),
                Seat("hr", "", assigned: true),
                Seat("headhunter", "", assigned: true),
            },
            pricingPlanExists: true);

        Assert.True(pick.Chosen);
        Assert.Equal("headhunter", pick.Role);
        Assert.Equal("hire", pick.Action);
        Assert.Equal("", pick.Warehouse);
    }

    [Fact]
    public void Managers_decide_a_plan_without_an_assigned_person_counts_as_missing()
    {
        var pick = Managers.Decide(
            2,
            new[] { "W1" },
            employees: 10,
            openRoles: 2,
            seats: new[]
            {
                Seat("logistics", "W1", assigned: false),
                Seat("purchasing", "W1", assigned: true),
                Seat("hr", "", assigned: true),
                Seat("headhunter", "", assigned: true),
            },
            pricingPlanExists: true);

        Assert.True(pick.Chosen);
        Assert.Equal("logistics", pick.Role);
        Assert.Equal("hire", pick.Action);
        Assert.Equal("W1", pick.Warehouse);
    }

    [Fact]
    public void Managers_decide_stage_one_hires_nobody()
    {
        var pick = Managers.Decide(
            1,
            Array.Empty<string>(),
            employees: 3,
            openRoles: 3,
            seats: Array.Empty<ManagerSeat>(),
            pricingPlanExists: false);

        Assert.False(pick.Chosen);
        Assert.Equal("", pick.Role);
        Assert.Equal("", pick.Action);
    }

    [Fact]
    public void Managers_decide_a_full_roster_needs_nobody()
    {
        var pick = Managers.Decide(
            3,
            new[] { "W1" },
            employees: 10,
            openRoles: 2,
            seats: new[]
            {
                Seat("logistics", "W1", assigned: true),
                Seat("purchasing", "W1", assigned: true),
                Seat("hr", "", assigned: true),
                Seat("headhunter", "", assigned: true),
            },
            pricingPlanExists: true);

        Assert.False(pick.Chosen);
        Assert.Equal("", pick.Role);
    }

    [Fact]
    public void Managers_decide_skips_blank_and_missing_seats()
    {
        var blank = Managers.Decide(2, new[] { "W1" }, 10, 2, null, pricingPlanExists: true);
        var empty = Managers.Decide(
            2,
            new[] { "W1" },
            10,
            2,
            Array.Empty<ManagerSeat>(),
            pricingPlanExists: true);
        var noWarehouses = Managers.Decide(
            2,
            Array.Empty<string>(),
            10,
            2,
            Array.Empty<ManagerSeat>(),
            pricingPlanExists: true);

        Assert.False(blank.Chosen);
        Assert.True(empty.Chosen);
        Assert.Equal("logistics", empty.Role);
        Assert.Equal("W1", empty.Warehouse);
        Assert.True(noWarehouses.Chosen);
        Assert.Equal("hr", noWarehouses.Role);
    }

    [Fact]
    public void Managers_decide_logistics_beats_hr_when_both_are_short()
    {
        var pick = Managers.Decide(
            3,
            new[] { "W1", "W2" },
            employees: Managers.HrSpan + 1,
            openRoles: 1,
            seats: new[]
            {
                Seat("logistics", "W1", assigned: true),
                Seat("purchasing", "W1", assigned: true),
                Seat("purchasing", "W2", assigned: true),
                Seat("hr", "", assigned: true),
                Seat("headhunter", "", assigned: true),
            },
            pricingPlanExists: true);

        Assert.True(pick.Chosen);
        Assert.Equal("logistics", pick.Role);
        Assert.Equal("W2", pick.Warehouse);
    }

    [Fact]
    public void Managers_decide_stage_two_with_no_warehouse_still_needs_baseline_hr()
    {
        var pick = Managers.Decide(
            2,
            null,
            employees: 10,
            openRoles: 2,
            seats: Array.Empty<ManagerSeat>(),
            pricingPlanExists: true);

        Assert.True(pick.Chosen);
        Assert.Equal("hr", pick.Role);
        Assert.Equal("hire", pick.Action);
        Assert.Equal("", pick.Warehouse);
    }

    private static ManagerSeat Seat(string role, string warehouse, bool assigned)
    {
        return new ManagerSeat(role, warehouse, assigned);
    }
}
