using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class OfferTests
{
    [Fact]
    public void Offer_one_confirmed_building_locks_that_street_and_number()
    {
        var steps = OpenAndClose();

        var offered = Offer.Confirm(Api(), steps, "ba:street_fourthavenue", 28, true);

        Assert.True(offered.Offered);
        Assert.Equal("ba:street_fourthavenue", offered.Street);
        Assert.Equal(28, offered.Number);
        Assert.Equal("BizManPresentation.SendBuyBuildingOffer", offered.Call);
        Assert.Equal(steps[0].Street, offered.Street);
        Assert.Equal(steps[0].Number, offered.Number);
        Assert.NotEqual(steps[1].Street, offered.Street);
        Assert.DoesNotContain("ShutdownBusiness", offered.Call);
        Assert.DoesNotContain("SetPrice", offered.Call);
        Assert.DoesNotContain("AddDestination", offered.Call);
    }

    [Fact]
    public void Offer_an_unconfirmed_buy_stays()
    {
        using var dir = new TempLog();

        var offered = Offer.Confirm(Api(), OpenAndClose(), "ba:street_fourthavenue", 28, false);

        Assert.False(offered.Offered);
        Assert.Equal("", offered.Street);
        Assert.Equal(0, offered.Number);
        Assert.Equal("", offered.Call);
        Assert.Equal("", Offer.Record(dir.Path, offered));
        Assert.False(File.Exists(LoadLog.CeoLogPath(dir.Path)));
    }

    [Fact]
    public void Offer_a_close_on_that_address_does_not_block_the_open()
    {
        var steps = new[]
        {
            new RoadStep("close", "ba:street_fourthavenue", 28, 30d),
            new RoadStep("open", "ba:street_fourthavenue", 28, 1099d),
        };

        var offered = Offer.Confirm(Api(), steps, "ba:street_fourthavenue", 28, true);

        Assert.True(offered.Offered);
        Assert.Equal("ba:street_fourthavenue", offered.Street);
        Assert.Equal(28, offered.Number);
        Assert.Equal("BizManPresentation.SendBuyBuildingOffer", offered.Call);
        Assert.DoesNotContain("ShutdownBusiness", offered.Call);
    }

    [Fact]
    public void Offer_a_close_step_is_not_bought()
    {
        var offered = Offer.Confirm(Api(), OpenAndClose(), "ba:street_side", 9, true);

        Assert.False(offered.Offered);
        Assert.Equal("", offered.Street);
        Assert.Equal(0, offered.Number);
        Assert.Equal("", offered.Call);
        Assert.DoesNotContain("ShutdownBusiness", offered.Call);
        Assert.DoesNotContain("SendBuyBuildingOffer", offered.Call);
    }

    [Fact]
    public void Offer_a_building_outside_the_list_stays()
    {
        var offered = Offer.Confirm(Api(), OpenAndClose(), "ba:street_hq", 1, true);

        Assert.False(offered.Offered);
        Assert.Equal("", offered.Street);
        Assert.Equal(0, offered.Number);
        Assert.Equal("", offered.Call);
        Assert.NotEqual("ba:street_fourthavenue", offered.Street);
    }

    [Fact]
    public void Offer_the_other_number_on_that_street_stays()
    {
        var offered = Offer.Confirm(Api(), OpenAndClose(), "ba:street_fourthavenue", 29, true);

        Assert.False(offered.Offered);
        Assert.Equal("", offered.Street);
        Assert.Equal(0, offered.Number);
        Assert.Equal("", offered.Call);
    }

    [Fact]
    public void Offer_a_blank_buy_call_stays()
    {
        var offered = Offer.Confirm(
            new ApiNote(
                "3682",
                "Helpers.BusinessHelper.ShutdownBusiness",
                "",
                "Buildings.Office.Headquarters.PricingManagerPlan.SetPrice",
                "UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination"),
            OpenAndClose(),
            "ba:street_fourthavenue",
            28,
            true);

        Assert.False(offered.Offered);
        Assert.Equal("", offered.Street);
        Assert.Equal(0, offered.Number);
        Assert.Equal("", offered.Call);
    }

    [Fact]
    public void Offer_a_blank_close_call_still_offers_the_building()
    {
        var offered = Offer.Confirm(
            new ApiNote(
                "3682",
                "",
                "BizManPresentation.SendBuyBuildingOffer",
                "Buildings.Office.Headquarters.PricingManagerPlan.SetPrice",
                "UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination"),
            OpenAndClose(),
            "ba:street_fourthavenue",
            28,
            true);

        Assert.True(offered.Offered);
        Assert.Equal("ba:street_fourthavenue", offered.Street);
        Assert.Equal(28, offered.Number);
        Assert.Equal("BizManPresentation.SendBuyBuildingOffer", offered.Call);
        Assert.DoesNotContain("ShutdownBusiness", offered.Call);
    }

    [Fact]
    public void Offer_a_blank_price_call_still_offers_the_building()
    {
        var offered = Offer.Confirm(
            new ApiNote(
                "3682",
                "Helpers.BusinessHelper.ShutdownBusiness",
                "BizManPresentation.SendBuyBuildingOffer",
                "",
                "UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination"),
            OpenAndClose(),
            "ba:street_fourthavenue",
            28,
            true);

        Assert.True(offered.Offered);
        Assert.Equal("ba:street_fourthavenue", offered.Street);
        Assert.Equal(28, offered.Number);
        Assert.Equal("BizManPresentation.SendBuyBuildingOffer", offered.Call);
        Assert.DoesNotContain("SetPrice", offered.Call);
    }

    [Fact]
    public void Offer_a_missing_list_stays()
    {
        var offered = Offer.Confirm(Api(), null, "ba:street_fourthavenue", 28, true);

        Assert.False(offered.Offered);
        Assert.Equal("", offered.Street);
        Assert.Equal(0, offered.Number);
        Assert.Equal("", offered.Call);
    }

    [Fact]
    public void Offer_an_empty_list_stays()
    {
        var offered = Offer.Confirm(Api(), Array.Empty<RoadStep>(), "ba:street_fourthavenue", 28, true);

        Assert.False(offered.Offered);
        Assert.Equal("", offered.Street);
        Assert.Equal(0, offered.Number);
    }

    [Fact]
    public void Offer_number_zero_stays()
    {
        var steps = new[]
        {
            new RoadStep("open", "ba:street_fourthavenue", 0, 1099d),
        };

        var offered = Offer.Confirm(Api(), steps, "ba:street_fourthavenue", 0, true);

        Assert.False(offered.Offered);
        Assert.Equal(0, offered.Number);
        Assert.Equal("", offered.Street);
        Assert.Equal("", offered.Call);
    }

    [Fact]
    public void Offer_a_negative_number_stays()
    {
        var steps = new[]
        {
            new RoadStep("open", "ba:street_fourthavenue", -1, 1099d),
        };

        var offered = Offer.Confirm(Api(), steps, "ba:street_fourthavenue", -1, true);

        Assert.False(offered.Offered);
        Assert.Equal("", offered.Street);
        Assert.Equal(0, offered.Number);
        Assert.Equal("", offered.Call);
    }

    [Fact]
    public void Offer_a_blank_street_stays()
    {
        var steps = new[]
        {
            new RoadStep("open", "", 28, 1099d),
        };

        var offered = Offer.Confirm(Api(), steps, "", 28, true);

        Assert.False(offered.Offered);
        Assert.Equal("", offered.Street);
        Assert.Equal("", offered.Call);
    }

    [Fact]
    public void Offer_a_null_street_stays()
    {
        var offered = Offer.Confirm(Api(), OpenAndClose(), null, 28, true);

        Assert.False(offered.Offered);
        Assert.Equal("", offered.Street);
        Assert.Equal(0, offered.Number);
        Assert.Equal("", offered.Call);
    }

    [Fact]
    public void Offer_a_missing_api_stays()
    {
        var offered = Offer.Confirm(null, OpenAndClose(), "ba:street_fourthavenue", 28, true);

        Assert.False(offered.Offered);
        Assert.Equal("", offered.Call);
        Assert.Equal("", offered.Street);
        Assert.Equal(0, offered.Number);
    }

    [Fact]
    public void Offer_a_null_step_does_not_block_the_building()
    {
        var steps = new RoadStep[]
        {
            null!,
            new RoadStep("open", "ba:street_fourthavenue", 28, 1099d),
        };

        var offered = Offer.Confirm(Api(), steps, "ba:street_fourthavenue", 28, true);

        Assert.True(offered.Offered);
        Assert.Equal("ba:street_fourthavenue", offered.Street);
        Assert.Equal(28, offered.Number);
        Assert.Equal("BizManPresentation.SendBuyBuildingOffer", offered.Call);
    }

    [Fact]
    public void Offer_each_buy_is_its_own_log_line()
    {
        using var dir = new TempLog();
        var steps = new[]
        {
            new RoadStep("open", "ba:street_fourthavenue", 28, 1099d),
            new RoadStep("open", "ba:street_other", 4, 10d),
            new RoadStep("close", "ba:street_side", 9, 30d),
        };

        var first = Offer.Confirm(Api(), steps, "ba:street_fourthavenue", 28, true);
        var second = Offer.Confirm(Api(), steps, "ba:street_other", 4, true);
        var skipped = Offer.Confirm(Api(), steps, "ba:street_side", 9, true);
        var firstLine = Offer.Record(dir.Path, first);
        var secondLine = Offer.Record(dir.Path, second);
        var text = File.ReadAllText(LoadLog.CeoLogPath(dir.Path));

        Assert.True(first.Offered);
        Assert.True(second.Offered);
        Assert.False(skipped.Offered);
        Assert.Equal("ba:street_fourthavenue", first.Street);
        Assert.Equal(28, first.Number);
        Assert.Equal("ba:street_other", second.Street);
        Assert.Equal(4, second.Number);
        Assert.NotEqual(first.Street, second.Street);
        Assert.DoesNotContain("ba:street_other", firstLine);
        Assert.DoesNotContain("number=28", secondLine);
        Assert.DoesNotContain("ba:street_side", firstLine);
        Assert.DoesNotContain("ba:street_side", secondLine);
        Assert.Equal("", Offer.Record(dir.Path, skipped));
        Assert.Equal(2, Count(text, "building.offered"));
        Assert.Contains("call=BizManPresentation.SendBuyBuildingOffer", firstLine);
        Assert.Contains("call=BizManPresentation.SendBuyBuildingOffer", secondLine);
        Assert.DoesNotContain("store.closed", text);
        Assert.DoesNotContain("price.applied", text);
        Assert.DoesNotContain("staff.applied", text);
        Assert.DoesNotContain("logistics.redone", text);
        Assert.DoesNotContain("ShutdownBusiness", text);
        Assert.DoesNotContain("SetPrice", text);
        Assert.DoesNotContain("AddDestination", text);
    }

    [Fact]
    public void Offer_a_null_offer_does_not_log()
    {
        using var dir = new TempLog();

        var offered = Offer.Reload(null);

        Assert.False(offered.Offered);
        Assert.Equal("", offered.Street);
        Assert.Equal(0, offered.Number);
        Assert.Equal("", offered.Call);
        Assert.Equal("", Offer.Record(dir.Path, null));
        Assert.False(File.Exists(LoadLog.CeoLogPath(dir.Path)));
    }

    [Fact]
    public void Offer_reload_undoes_the_confirmed_buy()
    {
        using var dir = new TempLog();
        var days = new[]
        {
            new FinancialDay(
                1013,
                new[]
                {
                    new StatementRow("ba:street_fourthavenue", 28, 0f, 0f, 0f, 1099f, 0f, 0f, 40f),
                }),
        };
        var stops = new[]
        {
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_firstavenue 38",
                "ba:itemname_sodacan",
                1016,
                ""),
        };
        var holds = new[]
        {
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_sodacan", 45),
        };
        StatementDump.Write(dir.Path, 1014, days);
        RouteDump.Write(dir.Path, stops, holds);
        var statementsBefore = File.ReadAllText(StatementDump.CsvPath(dir.Path));
        var routesBefore = File.ReadAllText(RouteDump.RoutesPath(dir.Path));

        var offered = Offer.Confirm(Api(), OpenAndClose(), "ba:street_fourthavenue", 28, true);
        var logged = Offer.Record(dir.Path, offered);
        var undone = Offer.Reload(offered);
        var text = File.ReadAllText(LoadLog.CeoLogPath(dir.Path));

        Assert.True(offered.Offered);
        Assert.False(undone.Offered);
        Assert.Equal("", undone.Street);
        Assert.Equal(0, undone.Number);
        Assert.Equal("", undone.Call);
        Assert.NotEqual(offered.Street, undone.Street);
        Assert.NotEqual(offered.Number, undone.Number);
        Assert.Equal(statementsBefore, File.ReadAllText(StatementDump.CsvPath(dir.Path)));
        Assert.Equal(routesBefore, File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.Contains("ba:street_fourthavenue", statementsBefore);
        Assert.Contains("ba:itemname_sodacan", routesBefore);
        Assert.Contains(logged, text);
        Assert.Equal("", Offer.Record(dir.Path, undone));
        Assert.Equal(1, Count(File.ReadAllText(LoadLog.CeoLogPath(dir.Path)), "building.offered"));
        Assert.Equal(
            "building.offered version=1.16.2 street=ba:street_fourthavenue number=28 call=BizManPresentation.SendBuyBuildingOffer",
            logged);
        Assert.DoesNotContain("SendBuyBuildingOffer", statementsBefore);
        Assert.DoesNotContain("SendBuyBuildingOffer", routesBefore);
    }

    private static int Count(string text, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    private static RoadStep[] OpenAndClose()
    {
        return new[]
        {
            new RoadStep("open", "ba:street_fourthavenue", 28, 1099d),
            new RoadStep("close", "ba:street_side", 9, 30d),
        };
    }

    private static ApiNote Api()
    {
        return new ApiNote(
            "3682",
            "Helpers.BusinessHelper.ShutdownBusiness",
            "BizManPresentation.SendBuyBuildingOffer",
            "Buildings.Office.Headquarters.PricingManagerPlan.SetPrice",
            "UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination");
    }

    private sealed class TempLog : IDisposable
    {
        public TempLog()
        {
            Path = Directory.CreateTempSubdirectory("chaotic-offer").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
