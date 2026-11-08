using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class StaffDemandTests
{
    [Fact]
    public void StaffDemand_clears_an_unmet_demand_on_day_three()
    {
        var api = GameApi.Live();
        var cleared = StaffDemand.Clear(
            unmetDays: StaffDemand.DemandDays,
            demandName: "ba:jobdemand_healthinsurance",
            employeeId: "emp-1",
            api);
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-demand-" + Guid.NewGuid().ToString("N")));
        try
        {
            var written = StaffDemand.Record(dir.FullName, cleared, 1);
            var again = StaffDemand.Record(dir.FullName, cleared, 1);

            Assert.True(cleared.Cleared);
            Assert.Equal(StaffDemand.DemandDays, 3);
            Assert.Equal("ba:jobdemand_healthinsurance", cleared.Demand);
            Assert.Equal("emp-1", cleared.EmployeeId);
            Assert.Equal("Entities.EmployeeInstance.GiveBonus", cleared.Call);
            Assert.Equal(cleared.Call, api.Call("employee demand"));
            Assert.Equal(
                "demand.cleared version=" + LoadLog.Version
                    + " day=1 employee=emp-1 demand=ba:jobdemand_healthinsurance call=" + cleared.Call,
                written);
            Assert.Equal(written, again);
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.FullName)).Count(line => line.StartsWith("demand.cleared ")));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void StaffDemand_waits_before_day_three_and_skips_a_blank_row()
    {
        var api = GameApi.Live();
        var early = StaffDemand.Clear(unmetDays: 2, demandName: "ba:jobdemand_healthinsurance", employeeId: "emp-1", api);
        var blankDemand = StaffDemand.Clear(unmetDays: 3, demandName: "", employeeId: "emp-1", api);
        var blankEmployee = StaffDemand.Clear(unmetDays: 3, demandName: "ba:jobdemand_healthinsurance", employeeId: "", api);
        var noCall = StaffDemand.Clear(unmetDays: 3, demandName: "ba:jobdemand_healthinsurance", employeeId: "emp-1", api: null);

        Assert.False(early.Cleared);
        Assert.Equal("", early.Call);
        Assert.False(blankDemand.Cleared);
        Assert.False(blankEmployee.Cleared);
        Assert.False(noCall.Cleared);
    }
}
