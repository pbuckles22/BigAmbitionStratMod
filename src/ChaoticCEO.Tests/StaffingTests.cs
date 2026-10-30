using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class StaffingTests
{
    [Fact]
    public void Staffing_lock_one_plan()
    {
        var run = new DeliveryRun("W1", "food", "Shop", "ba:itemname_sodacan", null);
        var plan = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", "food", "hr-soda", 40),
                new StaffOffer("headhunter", "food", "hh-soda", 4),
            });

        Assert.Equal("food", plan.Role);
        Assert.Equal("hr", plan.Kind);
        Assert.Equal("hr-soda", plan.PlanId);
        Assert.Equal(40, plan.Amount);
    }

    [Fact]
    public void Staffing_the_larger_amount_is_kept()
    {
        var run = new DeliveryRun("W1", "food", "Shop", "ba:itemname_sodacan", null);
        var plan = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", "food", "hr-soda", 4),
                new StaffOffer("headhunter", "food", "hh-soda", 20),
            });

        Assert.Equal("headhunter", plan.Kind);
        Assert.Equal("hh-soda", plan.PlanId);
        Assert.Equal(20, plan.Amount);
    }

    [Fact]
    public void Staffing_a_tie_keeps_the_later_plan()
    {
        var run = new DeliveryRun("W1", "food", "Shop", "ba:itemname_sodacan", null);
        var plan = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", "food", "hr-soda", 4),
                new StaffOffer("headhunter", "food", "hh-soda", 4),
            });

        Assert.Equal("headhunter", plan.Kind);
        Assert.Equal("hh-soda", plan.PlanId);
        Assert.Equal(4, plan.Amount);
    }

    [Fact]
    public void Staffing_amount_zero_is_not_a_plan()
    {
        var run = new DeliveryRun("W1", "food", "Shop", "ba:itemname_sodacan", null);
        var plan = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", "food", "hr-empty", 0),
                new StaffOffer("headhunter", "food", "hh-soda", 4),
            });

        Assert.Equal("headhunter", plan.Kind);
        Assert.Equal("hh-soda", plan.PlanId);
    }

    [Fact]
    public void Staffing_an_unlimited_plan_beats_a_finite_count()
    {
        var run = new DeliveryRun("W1", "food", "Shop", "ba:itemname_sodacan", null);
        var plan = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", "food", "hr-soda", 40),
                new StaffOffer("headhunter", "food", "hh-open", -1),
            });

        Assert.Equal("headhunter", plan.Kind);
        Assert.Equal("hh-open", plan.PlanId);
        Assert.Equal(-1, plan.Amount);
    }

    [Fact]
    public void Staffing_a_finite_count_does_not_beat_an_unlimited_plan()
    {
        var run = new DeliveryRun("W1", "food", "Shop", "ba:itemname_sodacan", null);
        var plan = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("headhunter", "food", "hh-open", -1),
                new StaffOffer("hr", "food", "hr-soda", 40),
            });

        Assert.Equal("headhunter", plan.Kind);
        Assert.Equal("hh-open", plan.PlanId);
        Assert.Equal(-1, plan.Amount);
    }

    [Fact]
    public void Staffing_a_later_unlimited_plan_wins()
    {
        var run = new DeliveryRun("W1", "food", "Shop", "ba:itemname_sodacan", null);
        var plan = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("headhunter", "food", "hh-early", -1),
                new StaffOffer("hr", "food", "hr-later", -1),
            });

        Assert.Equal("hr", plan.Kind);
        Assert.Equal("hr-later", plan.PlanId);
    }

    [Fact]
    public void Staffing_another_role_does_not_change_the_plan()
    {
        var run = new DeliveryRun("W1", "food", "Shop", "ba:itemname_sodacan", null);
        var plan = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("headhunter", "specialty", "hh-gift", 1000),
                new StaffOffer("hr", "food", "hr-soda", 2),
            });

        Assert.Equal("food", plan.Role);
        Assert.Equal("hr", plan.Kind);
        Assert.Equal("hr-soda", plan.PlanId);
        Assert.Equal(2, plan.Amount);
    }

    [Fact]
    public void Staffing_an_unknown_kind_is_not_a_plan()
    {
        var run = new DeliveryRun("W1", "food", "Shop", "ba:itemname_sodacan", null);
        var plan = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("shelf", "food", "not-a-plan", 1000),
                new StaffOffer("hr", "food", "hr-soda", 2),
            });

        Assert.Equal("hr", plan.Kind);
        Assert.Equal("hr-soda", plan.PlanId);
    }

    [Fact]
    public void Staffing_a_later_zero_does_not_replace_the_plan()
    {
        var run = new DeliveryRun("W1", "food", "Shop", "ba:itemname_sodacan", null);
        var plan = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", "food", "hr-soda", 4),
                new StaffOffer("headhunter", "food", "hh-empty", 0),
            });

        Assert.Equal("hr", plan.Kind);
        Assert.Equal("hr-soda", plan.PlanId);
        Assert.Equal(4, plan.Amount);
    }

    [Fact]
    public void Staffing_an_empty_family_has_no_plan()
    {
        var run = new DeliveryRun("W1", "", "", "", null);
        var plan = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", "food", "hr-soda", 40),
            });

        Assert.Equal("", plan.Role);
        Assert.Equal("", plan.Kind);
        Assert.Equal("", plan.PlanId);
        Assert.Equal(0, plan.Amount);
    }
}
