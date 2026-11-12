using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class CashierTests
{
    [Fact]
    public void Cashier_names_the_hire_on_a_signed_gift_shop()
    {
        var api = GameApi.Live();
        var rent = new BuildingRent("ba:street_seventhavenue", 26, true, api.Call("rent"));
        var layout = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
        var signed = Wholesaler.Sign(rent, layout, Wholesaler.StageOne(), alreadySigned: false, api);
        var hired = Cashier.Hire(rent, layout, signed, alreadyHired: false, api);
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-cashier-" + Guid.NewGuid().ToString("N")));
        try
        {
            var written = Cashier.Record(dir.FullName, hired, 1);
            var again = Cashier.Record(dir.FullName, hired, 1);

            Assert.True(signed.Signed);
            Assert.True(hired.Hired);
            Assert.Equal("Cashier", hired.Role);
            Assert.Equal(Cashier.Skill, hired.Skill);
            Assert.Equal("SelfServiceEmployee", hired.Type);
            Assert.Equal("ba:street_seventhavenue", hired.Street);
            Assert.Equal(26, hired.Number);
            Assert.Equal("Helpers.EmployeeHelper.HireCandidate", hired.Call);
            Assert.Equal("Controllers.CashRegisterController.AssignEmployee", hired.StationCall);
            Assert.Equal(hired.Call, api.Call("hire"));
            Assert.Equal(hired.StationCall, api.Call("cashier"));
            Assert.Equal(
                "hire.cashier version=" + LoadLog.Version
                    + " day=1 street=ba:street_seventhavenue number=26 role=Cashier skill=" + Cashier.Skill
                    + " type=SelfServiceEmployee call=" + hired.Call
                    + " stationCall=" + hired.StationCall,
                written);
            Assert.Equal(written, again);
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.FullName)).Count(line => line.StartsWith("hire.cashier ")));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Cashier_retries_a_missing_or_unstarted_register()
    {
        Assert.True(Cashier.Retry("nostation"));
        Assert.True(Cashier.Retry("notstarted"));
        Assert.True(Cashier.Retry("nocharacter"));
        Assert.False(Cashier.Retry("exception"));
        Assert.False(Cashier.Retry("noemployee"));
        Assert.False(Cashier.Retry("empty"));
        Assert.False(Cashier.Retry(""));
        Assert.False(Cashier.Retry(null));
    }

    [Fact]
    public void Cashier_a_gift_shop_register_is_self_service()
    {
        Assert.Equal("SelfServiceEmployee", Cashier.RegisterType("SelfService"));
        Assert.Equal("SelfServiceEmployee", Cashier.RegisterType("CinemaTheater"));
        Assert.Equal("FullServiceEmployee", Cashier.RegisterType("FullService"));
        Assert.Equal("BarmanEmployee", Cashier.RegisterType("Nightclub"));
        Assert.Equal(Cashier.GiftType, Cashier.RegisterType(null));
        Assert.Equal(Cashier.GiftType, Cashier.RegisterType(""));
    }

    [Fact]
    public void Cashier_keeps_the_register_empty_when_the_store_cannot_hire()
    {
        var api = GameApi.Live();
        var rent = new BuildingRent("ba:street_seventhavenue", 26, true, api.Call("rent"));
        var layout = Starter.Choose(Starter.StageOne(), alreadyOpen: false, api);
        var signed = Wholesaler.Sign(rent, layout, Wholesaler.StageOne(), alreadySigned: false, api);
        var again = Cashier.Hire(rent, layout, signed, alreadyHired: true, api);
        var unsigned = Cashier.Hire(
            rent,
            layout,
            Wholesaler.Sign(rent, layout, Wholesaler.StageOne(), alreadySigned: true, api),
            alreadyHired: false,
            api);
        var closed = Cashier.Hire(
            rent,
            Starter.Choose(Starter.StageOne(), alreadyOpen: true, api),
            signed,
            alreadyHired: false,
            api);
        var unrented = Cashier.Hire(
            new BuildingRent("ba:street_seventhavenue", 26, false, api.Call("rent")),
            layout,
            signed,
            alreadyHired: false,
            api);
        var noCall = Cashier.Hire(rent, layout, signed, alreadyHired: false, api: null);

        Assert.False(again.Hired);
        Assert.Equal("", again.Call);
        Assert.False(unsigned.Hired);
        Assert.False(closed.Hired);
        Assert.False(unrented.Hired);
        Assert.False(noCall.Hired);
        Assert.Equal(Cashier.Skill, "ba:skill_customerservice");
    }

    [Fact]
    public void Cashier_a_retired_register_is_not_called_a_quit()
    {
        Assert.Equal("retired", CashierCover.Reason(resigned: true, retired: true, poached: true));
        Assert.Equal("retired", CashierCover.Reason(resigned: false, retired: true, poached: false));
        Assert.Equal("quit", CashierCover.Reason(resigned: true, retired: false, poached: false));
        Assert.Equal("poached", CashierCover.Reason(resigned: false, retired: false, poached: true));
        Assert.Equal("absent", CashierCover.Reason(resigned: false, retired: false, poached: false));
        var absent = Cashier.Cover(
            "ba:street_seventhavenue 22",
            registerStaffed: false,
            resigned: false,
            retired: false,
            poached: false,
            GameApi.Live());
        Assert.True(absent.Hire);
        Assert.Equal("absent", absent.Reason);
        Assert.Equal(22, absent.Pick.Number);
    }

    [Fact]
    public void Cashier_load_day_roll_store_health_cover_audit_hires_an_empty_register()
    {
        // Chain: load → day roll → store health → cashier cover → audit hire
        var facts = new StoreFacts(
            "ba:street_seventhavenue 22",
            temporarilyClosed: false,
            neededItems: new[] { "ba:itemname_cheapgift" },
            cargoOnHand: new Dictionary<string, int> { ["ba:itemname_cheapgift"] = 10 },
            registerStaffed: false,
            cleanerStaffed: true,
            staffSatisfaction: 100f,
            scheduledOpen: true,
            dailyCustomers: 0,
            dailyRevenue: 0m,
            recentOpenDayProfits: new[] { 10m, 10m, 10m });
        var health = StoreHealth.Assess(facts);
        var api = GameApi.Live();
        var gap = Cashier.Cover(
            health.Place,
            facts.RegisterStaffed,
            resigned: false,
            retired: true,
            poached: false,
            api);
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-cashier-gap-" + Guid.NewGuid().ToString("N")));
        try
        {
            var line = Cashier.Replaced(dir.FullName, gap.Pick, 1496, gap.Reason);
            var staffed = facts.RegisterStaffed
                ? new[] { health.Place }
                : Array.Empty<string>();
            var report = Audit.Check(
                dir.FullName,
                1496,
                api,
                new AuditWorld(
                    null, null, null, null, null, null, null, true, null, null,
                    new[] { health.Place },
                    staffed),
                new AuditGuard(true, false, 0m, 0m, 0, LoadLog.PinnedBuild),
                _ => HealFate.Done);
            var kept = Cashier.Cover(health.Place, registerStaffed: true, resigned: false, retired: true, poached: false, api);

            Assert.False(facts.RegisterStaffed);
            Assert.False(health.Staffed);
            Assert.True(gap.Hire);
            Assert.Equal("retired", gap.Reason);
            Assert.True(gap.Pick.Hired);
            Assert.Equal("ba:street_seventhavenue", gap.Pick.Street);
            Assert.Equal(22, gap.Pick.Number);
            Assert.Equal(api.Call("hire"), gap.Pick.Call);
            Assert.Equal(api.Call("cashier"), gap.Pick.StationCall);
            Assert.Contains("reason=retired", line);
            var written = string.Join("\n", report.Lines);
            Assert.Contains(
                "audit.healed version=" + LoadLog.Version + " day=1496 action=hire store=" + health.Place,
                written);
            Assert.False(kept.Hire);
            Assert.Equal("", kept.Reason);
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }
}
