using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class HealthTests
{
    [Fact]
    public void Health_names_stores_supplied_slots_filled_plans_present_and_the_last_heal()
    {
        var summary = "audit.summary version=1.17.21 day=1016 unconnected=2 emptySlots=1 dry=4 mixed=7 unstaffed=0 missingPlans=3 cashBelowReserve=0 pinMismatch=0 healed=1";
        var healed = "audit.healed version=1.17.21 day=1016 action=connect store=ba:street_fourthavenue 28 warehouse=W11 item=ba:itemname_haircareproduct call=AddDestination dry=false";

        var line = Health.Section(summary, healed);

        Assert.Equal(
            "Stores supplied unconnected=2  Slots filled emptySlots=1  Plans present missingPlans=3  Last heal healed=1 action=connect store=ba:street_fourthavenue 28 warehouse=W11 item=ba:itemname_haircareproduct",
            line);
    }

    [Fact]
    public void Health_keeps_the_latest_summary_when_an_older_heal_is_still_in_the_log()
    {
        var connect = "audit.healed version=1.17.21 day=1016 action=connect store=ba:street_fourthavenue 28 warehouse=W11 item=ba:itemname_haircareproduct call=AddDestination dry=false";
        var slot = "audit.healed version=1.17.21 day=1016 action=slot warehouse=W7 slot=1 order=Order assign=AssignVehicle driver= dry=false";
        var loud = "audit.summary version=1.17.21 day=1016 unconnected=2 emptySlots=1 dry=0 mixed=0 unstaffed=0 missingPlans=3 cashBelowReserve=0 pinMismatch=0 healed=2";
        var quiet = "audit.summary version=1.17.21 day=1017 unconnected=0 emptySlots=0 dry=0 mixed=0 unstaffed=0 missingPlans=0 cashBelowReserve=0 pinMismatch=0 healed=0";

        var shown = Health.Section(loud, connect + "\n" + slot);
        var calm = Health.Section(quiet, connect + "\n" + slot + "\n" + loud + "\n" + quiet);
        var blank = Health.Section(null, null);

        Assert.Equal(
            "Stores supplied unconnected=2  Slots filled emptySlots=1  Plans present missingPlans=3  Last heal healed=2 action=slot warehouse=W7 slot=1 order=Order assign=AssignVehicle driver=",
            shown);
        Assert.DoesNotContain("action=connect", shown);
        Assert.Equal(
            "Stores supplied unconnected=0  Slots filled emptySlots=0  Plans present missingPlans=0  Last heal healed=0 none",
            calm);
        Assert.Equal(
            "Stores supplied unconnected=0  Slots filled emptySlots=0  Plans present missingPlans=0  Last heal healed=0 none",
            blank);
    }

    [Fact]
    public void Health_names_a_plan_heal_by_its_role()
    {
        var summary = "audit.summary version=1.17.21 day=1016 unconnected=0 emptySlots=0 dry=0 mixed=0 unstaffed=0 missingPlans=1 cashBelowReserve=0 pinMismatch=0 healed=1";
        var healed = "audit.healed version=1.17.21 day=1016 action=plan role=logistics call=AssignEmployee dry=false";

        var line = Health.Section(summary, healed);

        Assert.Contains("Plans present missingPlans=1", line);
        Assert.EndsWith("Last heal healed=1 action=plan role=logistics", line);
        Assert.DoesNotContain("call=", line);
    }
}
