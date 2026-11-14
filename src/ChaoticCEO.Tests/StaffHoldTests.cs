using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class StaffHoldTests
{
    private const int Year = 60;
    private const string Place = "ba:street_seventhavenue 22";

    private static StaffState Happy(int years = 30)
    {
        return new StaffState(100f, years * Year, complaining: false, hoursUntilNextComplaint: Year * 24 * 2, poachPending: false);
    }

    [Fact]
    public void Decide_a_due_sick_day_becomes_never()
    {
        var due = new StaffState(100f, 30 * Year, false, Year * 48, false, nextSickDay: 1600, absent: true);
        var earlier = new StaffState(100f, 30 * Year, false, Year * 48, false, nextSickDay: 1599);

        var plan = StaffHold.Decide(due, Year);
        var past = StaffHold.Decide(earlier, Year);

        Assert.Equal(StaffHold.NeverDay, plan.SickDay);
        Assert.True(plan.ClearAbsent);
        Assert.Equal(StaffHold.NeverDay, past.SickDay);
        Assert.False(past.ClearAbsent);
    }

    [Fact]
    public void Decide_a_scheduled_sick_day_becomes_never_before_it_is_due()
    {
        var later = new StaffState(100f, 30 * Year, false, Year * 48, false, nextSickDay: 1690);

        var plan = StaffHold.Decide(later, Year);

        Assert.Equal(StaffHold.NeverDay, plan.SickDay);
        Assert.True(plan.Any);
    }

    [Fact]
    public void Decide_leaves_a_worker_who_will_not_call_in_sick()
    {
        var plan = StaffHold.Decide(Happy(), Year);

        Assert.Equal(-1, plan.SickDay);
        Assert.False(plan.ClearAbsent);
    }

    [Fact]
    public void Apply_clears_a_call_that_already_landed_and_a_second_pass_stays_clear()
    {
        var called = new StaffState(100f, 30 * Year, false, Year * 48, false, nextSickDay: 1690, absent: true);

        var held = StaffHold.Apply(called, StaffHold.Decide(called, Year));
        var again = StaffHold.Decide(held, Year);

        Assert.Equal(StaffHold.NeverDay, held.NextSickDay);
        Assert.False(held.Absent);
        Assert.Equal(-1, again.SickDay);
        Assert.False(again.ClearAbsent);
    }

    [Fact]
    public void Decide_leaves_a_happy_young_worker_alone()
    {
        var plan = StaffHold.Decide(Happy(), Year);

        Assert.False(plan.Any);
        Assert.Equal(-1, plan.Age);
        Assert.Equal(-1, plan.ComplaintHours);
    }

    [Fact]
    public void Decide_raises_any_satisfaction_under_full()
    {
        var plan = StaffHold.Decide(new StaffState(99.4f, 30 * Year, false, Year * 48, false), Year);

        Assert.True(plan.Raise);
        Assert.True(plan.Any);
    }

    [Fact]
    public void Decide_resets_age_one_year_before_the_retirement_notice()
    {
        var atCeiling = StaffHold.Decide(new StaffState(100f, StaffHold.CeilingYears * Year, false, Year * 48, false), Year);
        var under = StaffHold.Decide(new StaffState(100f, (StaffHold.CeilingYears * Year) - 1, false, Year * 48, false), Year);
        var past = StaffHold.Decide(new StaffState(100f, (StaffHold.RetirementAge * Year) + 5, false, Year * 48, false), Year);

        Assert.Equal(StaffHold.HoldAgeYears * Year, atCeiling.Age);
        Assert.Equal(-1, under.Age);
        Assert.Equal(StaffHold.HoldAgeYears * Year, past.Age);
        Assert.Equal(65, StaffHold.CeilingYears);
    }

    [Fact]
    public void Decide_clears_a_complaint_and_pushes_the_next_one_a_year_out()
    {
        var plan = StaffHold.Decide(new StaffState(100f, 30 * Year, complaining: true, hoursUntilNextComplaint: 3, poachPending: false), Year);

        Assert.True(plan.ClearComplaint);
        Assert.Equal(Year * 24, plan.ComplaintHours);
    }

    [Fact]
    public void Decide_clears_a_pending_rival_poach()
    {
        var plan = StaffHold.Decide(new StaffState(100f, 30 * Year, false, Year * 48, poachPending: true), Year);

        Assert.True(plan.ClearPoach);
    }

    [Fact]
    public void Decide_treats_a_zero_year_length_as_one_day()
    {
        var plan = StaffHold.Decide(new StaffState(100f, 70, false, 0, false), daysPerYear: 0);

        Assert.Equal(StaffHold.HoldAgeYears, plan.Age);
        Assert.Equal(24, plan.ComplaintHours);
    }

    [Fact]
    public void Apply_then_Leaving_keeps_a_worker_who_would_have_retired_quit_or_been_poached()
    {
        var retiring = new StaffState(100f, StaffHold.RetirementAge * Year, false, Year * 48, false);
        var quitting = new StaffState(0f, 30 * Year, complaining: true, hoursUntilNextComplaint: 0, poachPending: false);
        var poached = new StaffState(100f, 30 * Year, false, Year * 48, poachPending: true);

        Assert.Equal("retired", StaffHold.Leaving(retiring, Year));
        Assert.Equal("quit", StaffHold.Leaving(quitting, Year));
        Assert.Equal("poached", StaffHold.Leaving(poached, Year));
        Assert.Equal("", StaffHold.Leaving(StaffHold.Apply(retiring, StaffHold.Decide(retiring, Year)), Year));
        Assert.Equal("", StaffHold.Leaving(StaffHold.Apply(quitting, StaffHold.Decide(quitting, Year)), Year));
        Assert.Equal("", StaffHold.Leaving(StaffHold.Apply(poached, StaffHold.Decide(poached, Year)), Year));
    }

    [Fact]
    public void Leaving_names_retired_before_quit_before_poached()
    {
        var all = new StaffState(0f, StaffHold.RetirementAge * Year, complaining: true, hoursUntilNextComplaint: 0, poachPending: true);
        var quitAndPoach = new StaffState(0f, 30 * Year, complaining: true, hoursUntilNextComplaint: 0, poachPending: true);

        Assert.Equal("retired", StaffHold.Leaving(all, Year));
        Assert.Equal("quit", StaffHold.Leaving(quitAndPoach, Year));
        var quietQuit = new StaffState(0f, 30 * Year, complaining: false, hoursUntilNextComplaint: Year * 48, poachPending: false);
        Assert.Equal("quit", StaffHold.Leaving(quietQuit, Year));
        Assert.Equal("", StaffHold.Leaving(StaffHold.Apply(quietQuit, StaffHold.Decide(quietQuit, Year)), Year));
    }

    [Fact]
    public void Line_names_every_count()
    {
        Assert.Equal(
            "staff.held version=" + LoadLog.Version + " day=1600 employees=3 raised=2 aged=1 complaints=0 poach=1 sick=2",
            StaffHold.Line(1600, employees: 3, raised: 2, aged: 1, complaints: 0, poach: 1, sick: 2));
    }

    [Fact]
    public void Watch_logs_the_first_pass_of_a_day_and_a_save_but_not_a_plain_hour()
    {
        var watch = new StaffHoldWatch();

        var first = watch.Tick(null, day: 1600, hour: 8, employees: 3, raised: 3, aged: 0, complaints: 0, poach: 0, sick: 0);
        var sameHour = watch.Due(1600, 8);
        var plainHour = watch.Tick(null, day: 1600, hour: 9, employees: 3, raised: 1, aged: 0, complaints: 0, poach: 0, sick: 0);
        var saved = watch.Tick(null, day: 1600, hour: 10, employees: 3, raised: 0, aged: 0, complaints: 1, poach: 0, sick: 0);
        var called = watch.Tick(null, day: 1600, hour: 11, employees: 3, raised: 0, aged: 0, complaints: 0, poach: 0, sick: 2);
        var nextDay = watch.Tick(null, day: 1601, hour: 0, employees: 3, raised: 0, aged: 0, complaints: 0, poach: 0, sick: 0);

        Assert.StartsWith("staff.held", first);
        Assert.False(sameHour);
        Assert.True(watch.Due(1601, 1));
        Assert.Equal("", plainHour);
        Assert.EndsWith("complaints=1 poach=0 sick=0", saved);
        Assert.EndsWith("sick=2", called);
        Assert.Contains("day=1601", nextDay);
    }

    [Fact]
    public void Staff_hold_is_on_for_testing()
    {
        Assert.True(StaffHold.Enabled);
    }

    [Fact]
    public void Chain_day_roll_staff_hold_register_stays_staffed_and_no_cashier_is_hired()
    {
        // Chain: day roll → staff hold → leaving reason → register cover (no hire)
        var day = Morning.CoverDay(null, autopilotOn: true, day: 1600, coveredDay: 1599);
        var cashier = new StaffState(0f, StaffHold.RetirementAge * Year, complaining: true, hoursUntilNextComplaint: 0, poachPending: true);
        var held = StaffHold.Apply(cashier, StaffHold.Decide(cashier, Year));
        var leaving = StaffHold.Leaving(held, Year);
        var gap = Cashier.Cover(
            Place,
            registerStaffed: leaving.Length == 0,
            resigned: leaving == "quit",
            retired: leaving == "retired",
            poached: leaving == "poached",
            GameApi.Live());

        var unheldReason = StaffHold.Leaving(cashier, Year);
        var unheld = Cashier.Cover(Place, registerStaffed: false, resigned: false, retired: unheldReason == "retired", poached: false, GameApi.Live());

        Assert.Equal(1600, day);
        Assert.Equal("", leaving);
        Assert.Equal(100f, held.Satisfaction);
        Assert.False(gap.Hire);
        Assert.True(unheld.Hire);
        Assert.Equal("retired", unheld.Reason);
    }

    [Fact]
    public void Chain_needs_hold_staff_hold_sick_stays_clear_and_the_register_stays_staffed()
    {
        // Chain: needs hold → staff hold → sick stays clear → register cover (no hire)
        var drained = new PlayerNeeds(40f, 50f, 60f, energyOff: false, happinessOff: false);
        var filled = Needs.Due(drained) ? Needs.Hold() : drained;
        var due = new StaffState(100f, 30 * Year, false, Year * 48, false, nextSickDay: 1600, absent: true);
        var held = StaffHold.Apply(due, StaffHold.Decide(due, Year));
        var again = StaffHold.Decide(held, Year);
        var cover = Cashier.Cover(
            Place,
            registerStaffed: !held.Absent && again.SickDay < 0,
            resigned: false,
            retired: false,
            poached: false,
            GameApi.Live());

        Assert.Equal(100f, filled.Energy);
        Assert.True(filled.EnergyOff);
        Assert.True(filled.HappinessOff);
        Assert.Equal(StaffHold.NeverDay, held.NextSickDay);
        Assert.False(held.Absent);
        Assert.Equal(-1, again.SickDay);
        Assert.False(again.ClearAbsent);
        Assert.False(cover.Hire);
        Assert.Equal("", cover.Reason);
    }
}
