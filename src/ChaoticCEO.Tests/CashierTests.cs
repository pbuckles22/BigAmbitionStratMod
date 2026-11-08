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
}
