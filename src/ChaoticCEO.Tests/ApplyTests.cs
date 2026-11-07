using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class ApplyTests
{
    [Fact]
    public void Apply_one_confirmed_price_and_one_confirmed_staff_plan()
    {
        var price = Soda();
        var plan = SodaCrew();

        var priced = Apply.ConfirmPrice(Api(), price, price.Item, true);
        var staffed = Apply.ConfirmStaff(Api(), plan, plan.PlanId, true);

        Assert.True(priced.Applied);
        Assert.Equal("ba:itemname_sodacan", priced.Item);
        Assert.Equal(2.75f, priced.Price);
        Assert.Equal(
            "Buildings.Office.Headquarters.PricingManagerPlan.SetPrice",
            priced.Call);
        Assert.Equal(price.Item, priced.Item);
        Assert.Equal(price.Price, priced.Price);
        Assert.DoesNotContain("ShutdownBusiness", priced.Call);
        Assert.DoesNotContain("SendBuyBuildingOffer", priced.Call);
        Assert.DoesNotContain("AddDestination", priced.Call);
        Assert.DoesNotContain("HrManagerPlan", priced.Call);
        Assert.True(staffed.Applied);
        Assert.Equal("food", staffed.Role);
        Assert.Equal("hr", staffed.Kind);
        Assert.Equal("hr-soda", staffed.PlanId);
        Assert.Equal(40, staffed.Amount);
        Assert.Equal("", staffed.Call);
        Assert.Equal(plan.Role, staffed.Role);
        Assert.Equal(plan.Kind, staffed.Kind);
        Assert.Equal(plan.PlanId, staffed.PlanId);
        Assert.Equal(plan.Amount, staffed.Amount);
        Assert.NotEqual(priced.Call, staffed.Call);
        Assert.DoesNotContain("SetPrice", staffed.Call);
    }

    [Fact]
    public void Apply_an_unconfirmed_price_stays()
    {
        using var dir = new TempLog();

        var priced = Apply.ConfirmPrice(Api(), Soda(), "ba:itemname_sodacan", false);

        Assert.False(priced.Applied);
        Assert.Equal("", priced.Item);
        Assert.Equal(0f, priced.Price);
        Assert.Equal("", priced.Call);
        Assert.Equal("", Apply.RecordPrice(dir.Path, priced));
        Assert.False(File.Exists(LoadLog.CeoLogPath(dir.Path)));
    }

    [Fact]
    public void Apply_an_unconfirmed_staff_plan_stays()
    {
        using var dir = new TempLog();

        var staffed = Apply.ConfirmStaff(Api(), SodaCrew(), "hr-soda", false);

        Assert.False(staffed.Applied);
        Assert.Equal("", staffed.Role);
        Assert.Equal("", staffed.Kind);
        Assert.Equal("", staffed.PlanId);
        Assert.Equal(0, staffed.Amount);
        Assert.Equal("", staffed.Call);
        Assert.Equal("", Apply.RecordStaff(dir.Path, staffed));
        Assert.False(File.Exists(LoadLog.CeoLogPath(dir.Path)));
    }

    [Fact]
    public void Apply_a_different_item_stays()
    {
        var priced = Apply.ConfirmPrice(Api(), Soda(), "ba:itemname_cheapgift", true);

        Assert.False(priced.Applied);
        Assert.Equal("", priced.Item);
        Assert.Equal(0f, priced.Price);
        Assert.Equal("", priced.Call);
        Assert.DoesNotContain("SetPrice", priced.Call);
    }

    [Fact]
    public void Apply_a_different_plan_stays()
    {
        var staffed = Apply.ConfirmStaff(Api(), SodaCrew(), "hh-soda", true);

        Assert.False(staffed.Applied);
        Assert.Equal("", staffed.PlanId);
        Assert.Equal("", staffed.Role);
        Assert.Equal(0, staffed.Amount);
    }

    [Fact]
    public void Apply_a_zero_price_stays()
    {
        var locked = Prices.Lock(
            "ba:itemname_sodacan",
            new[]
            {
                new ItemSoldPerPriceEntry("ba:itemname_sodacan", 2.75f, 0),
            });

        var priced = Apply.ConfirmPrice(Api(), locked, locked.Item, true);

        Assert.Equal(0f, locked.Price);
        Assert.False(priced.Applied);
        Assert.Equal("", priced.Item);
        Assert.Equal("", priced.Call);
    }

    [Fact]
    public void Apply_an_empty_staff_plan_stays()
    {
        var locked = Staffing.Lock(
            new DeliveryRun("W1", "food", "Shop", "ba:itemname_sodacan", null),
            new[]
            {
                new StaffOffer("hr", "food", "hr-empty", 0),
            });

        var staffed = Apply.ConfirmStaff(Api(), locked, "hr-empty", true);

        Assert.Equal("", locked.PlanId);
        Assert.Equal(0, locked.Amount);
        Assert.False(staffed.Applied);
        Assert.Equal("", staffed.PlanId);
        Assert.Equal("", staffed.Role);
    }

    [Fact]
    public void Apply_a_blank_price_call_stays()
    {
        var priced = Apply.ConfirmPrice(
            new ApiNote("3682", "close", "buy", "", "delivery"),
            Soda(),
            "ba:itemname_sodacan",
            true);
        var staffed = Apply.ConfirmStaff(
            new ApiNote("3682", "close", "buy", "", "delivery"),
            SodaCrew(),
            "hr-soda",
            true);

        Assert.False(priced.Applied);
        Assert.Equal("", priced.Call);
        Assert.Equal("", priced.Item);
        Assert.True(staffed.Applied);
        Assert.Equal("hr-soda", staffed.PlanId);
        Assert.Equal(40, staffed.Amount);
        Assert.Equal("", staffed.Call);
    }

    [Fact]
    public void Apply_a_missing_price_stays()
    {
        var priced = Apply.ConfirmPrice(Api(), null, "ba:itemname_sodacan", true);

        Assert.False(priced.Applied);
        Assert.Equal("", priced.Item);
        Assert.Equal("", priced.Call);
    }

    [Fact]
    public void Apply_a_missing_plan_stays()
    {
        var staffed = Apply.ConfirmStaff(Api(), null, "hr-soda", true);

        Assert.False(staffed.Applied);
        Assert.Equal("", staffed.PlanId);
        Assert.Equal("", staffed.Call);
    }

    [Fact]
    public void Apply_a_missing_api_stays()
    {
        var priced = Apply.ConfirmPrice(null, Soda(), "ba:itemname_sodacan", true);
        var staffed = Apply.ConfirmStaff(null, SodaCrew(), "hr-soda", true);

        Assert.False(priced.Applied);
        Assert.Equal("", priced.Call);
        Assert.False(staffed.Applied);
        Assert.Equal("", staffed.PlanId);
    }

    [Fact]
    public void Apply_a_null_item_stays()
    {
        var priced = Apply.ConfirmPrice(Api(), Soda(), null, true);

        Assert.False(priced.Applied);
        Assert.Equal("", priced.Item);
        Assert.Equal(0f, priced.Price);
        Assert.Equal("", priced.Call);
    }

    [Fact]
    public void Apply_a_null_plan_stays()
    {
        var staffed = Apply.ConfirmStaff(Api(), SodaCrew(), null, true);

        Assert.False(staffed.Applied);
        Assert.Equal("", staffed.PlanId);
        Assert.Equal("", staffed.Role);
        Assert.Equal(0, staffed.Amount);
    }

    [Fact]
    public void Apply_a_null_apply_does_not_log()
    {
        using var dir = new TempLog();

        var priced = Apply.ReloadPrice(null);
        var staffed = Apply.ReloadStaff(null);

        Assert.False(priced.Applied);
        Assert.Equal("", priced.Item);
        Assert.Equal("", priced.Call);
        Assert.False(staffed.Applied);
        Assert.Equal("", staffed.PlanId);
        Assert.Equal("", Apply.RecordPrice(dir.Path, null));
        Assert.Equal("", Apply.RecordStaff(dir.Path, null));
        Assert.False(File.Exists(LoadLog.CeoLogPath(dir.Path)));
    }

    [Fact]
    public void Apply_a_blank_item_stays()
    {
        var priced = Apply.ConfirmPrice(Api(), Soda(), "", true);

        Assert.False(priced.Applied);
        Assert.Equal("", priced.Item);
        Assert.Equal("", priced.Call);
    }

    [Fact]
    public void Apply_a_blank_plan_stays()
    {
        var staffed = Apply.ConfirmStaff(Api(), SodaCrew(), "", true);

        Assert.False(staffed.Applied);
        Assert.Equal("", staffed.PlanId);
        Assert.Equal("", staffed.Kind);
    }

    [Fact]
    public void Apply_a_close_street_is_not_a_price()
    {
        var priced = Apply.ConfirmPrice(Api(), Soda(), "ba:street_side", true);

        Assert.False(priced.Applied);
        Assert.Equal("", priced.Item);
        Assert.Equal("", priced.Call);
        Assert.DoesNotContain("ShutdownBusiness", priced.Call);
        Assert.NotEqual(Soda().Item, priced.Item);
    }

    [Fact]
    public void Apply_the_other_item_stays()
    {
        var priced = Apply.ConfirmPrice(Api(), Soda(), "ba:itemname_sodacan_other", true);

        Assert.False(priced.Applied);
        Assert.Equal("", priced.Item);
        Assert.Equal(0f, priced.Price);
        Assert.Equal("", priced.Call);
    }

    [Fact]
    public void Apply_an_unknown_staff_kind_stays()
    {
        var staffed = Apply.ConfirmStaff(
            Api(),
            new StaffPlan("food", "clerk", "clerk-soda", 40),
            "clerk-soda",
            true);

        Assert.False(staffed.Applied);
        Assert.Equal("", staffed.PlanId);
        Assert.Equal("", staffed.Kind);
        Assert.Equal(0, staffed.Amount);
    }

    [Fact]
    public void Apply_a_zero_amount_on_the_selected_plan_stays()
    {
        using var dir = new TempLog();
        var plan = new StaffPlan("food", "hr", "hr-zero", 0);

        var staffed = Apply.ConfirmStaff(Api(), plan, plan.PlanId, true);

        Assert.Equal("hr-zero", plan.PlanId);
        Assert.Equal(0, plan.Amount);
        Assert.False(staffed.Applied);
        Assert.Equal("", staffed.PlanId);
        Assert.Equal("", staffed.Role);
        Assert.Equal(0, staffed.Amount);
        Assert.Equal("", Apply.RecordStaff(dir.Path, staffed));
        Assert.False(File.Exists(LoadLog.CeoLogPath(dir.Path)));
    }

    [Fact]
    public void Apply_an_unlimited_staff_plan_is_kept()
    {
        var plan = new StaffPlan("food", "headhunter", "hh-open", -1);

        var staffed = Apply.ConfirmStaff(Api(), plan, plan.PlanId, true);

        Assert.True(staffed.Applied);
        Assert.Equal("headhunter", staffed.Kind);
        Assert.Equal("hh-open", staffed.PlanId);
        Assert.Equal(-1, staffed.Amount);
        Assert.Equal("food", staffed.Role);
        Assert.Equal("", staffed.Call);
        Assert.DoesNotContain("SetPrice", staffed.Call);
    }

    [Fact]
    public void Apply_each_apply_is_its_own_log_line()
    {
        using var dir = new TempLog();
        var gift = new SoldPrice("ba:itemname_cheapgift", 9.5f);
        var hunter = new StaffPlan("specialty", "headhunter", "hh-gift", 12);

        var firstPrice = Apply.ConfirmPrice(Api(), Soda(), Soda().Item, true);
        var secondPrice = Apply.ConfirmPrice(Api(), gift, gift.Item, true);
        var firstStaff = Apply.ConfirmStaff(Api(), SodaCrew(), SodaCrew().PlanId, true);
        var secondStaff = Apply.ConfirmStaff(Api(), hunter, hunter.PlanId, true);
        var sodaLine = Apply.RecordPrice(dir.Path, firstPrice);
        var giftLine = Apply.RecordPrice(dir.Path, secondPrice);
        var crewLine = Apply.RecordStaff(dir.Path, firstStaff);
        var hunterLine = Apply.RecordStaff(dir.Path, secondStaff);
        var text = File.ReadAllText(LoadLog.CeoLogPath(dir.Path));

        Assert.True(firstPrice.Applied);
        Assert.True(secondPrice.Applied);
        Assert.True(firstStaff.Applied);
        Assert.True(secondStaff.Applied);
        Assert.Equal(2.75f, firstPrice.Price);
        Assert.Equal(9.5f, secondPrice.Price);
        Assert.Equal("hr-soda", firstStaff.PlanId);
        Assert.Equal("hh-gift", secondStaff.PlanId);
        Assert.DoesNotContain("ba:itemname_cheapgift", sodaLine);
        Assert.DoesNotContain("ba:itemname_sodacan", giftLine);
        Assert.DoesNotContain("hh-gift", crewLine);
        Assert.DoesNotContain("hr-soda", hunterLine);
        Assert.DoesNotContain("staff.applied", sodaLine);
        Assert.DoesNotContain("price.applied", crewLine);
        Assert.Equal(2, Count(text, "price.applied"));
        Assert.Equal(2, Count(text, "staff.applied"));
        Assert.Contains(
            "call=Buildings.Office.Headquarters.PricingManagerPlan.SetPrice",
            sodaLine);
        Assert.Contains("price=9.5", giftLine);
        Assert.Contains("plan=hr-soda", crewLine);
        Assert.Contains("amount=12", hunterLine);
        Assert.Contains("kind=headhunter", hunterLine);
        Assert.DoesNotContain("SetPrice", crewLine);
        Assert.DoesNotContain("ShutdownBusiness", text);
        Assert.DoesNotContain("AddDestination", text);
    }

    [Fact]
    public void Apply_reload_undoes_the_price_and_the_plan()
    {
        using var dir = new TempLog();
        var days = new[]
        {
            new FinancialDay(
                1013,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -30f),
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

        var priced = Apply.ConfirmPrice(Api(), Soda(), Soda().Item, true);
        var staffed = Apply.ConfirmStaff(Api(), SodaCrew(), SodaCrew().PlanId, true);
        var priceLine = Apply.RecordPrice(dir.Path, priced);
        var staffLine = Apply.RecordStaff(dir.Path, staffed);
        var undonePrice = Apply.ReloadPrice(priced);
        var undoneStaff = Apply.ReloadStaff(staffed);
        var text = File.ReadAllText(LoadLog.CeoLogPath(dir.Path));

        Assert.True(priced.Applied);
        Assert.True(staffed.Applied);
        Assert.False(undonePrice.Applied);
        Assert.Equal("", undonePrice.Item);
        Assert.Equal(0f, undonePrice.Price);
        Assert.Equal("", undonePrice.Call);
        Assert.False(undoneStaff.Applied);
        Assert.Equal("", undoneStaff.Role);
        Assert.Equal("", undoneStaff.Kind);
        Assert.Equal("", undoneStaff.PlanId);
        Assert.Equal(0, undoneStaff.Amount);
        Assert.NotEqual(priced.Item, undonePrice.Item);
        Assert.NotEqual(staffed.PlanId, undoneStaff.PlanId);
        Assert.Equal(statementsBefore, File.ReadAllText(StatementDump.CsvPath(dir.Path)));
        Assert.Equal(routesBefore, File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.Contains("ba:street_side", statementsBefore);
        Assert.Contains("ba:itemname_sodacan", routesBefore);
        Assert.Contains(priceLine, text);
        Assert.Contains(staffLine, text);
        Assert.Equal("", Apply.RecordPrice(dir.Path, undonePrice));
        Assert.Equal("", Apply.RecordStaff(dir.Path, undoneStaff));
        Assert.Equal(1, Count(File.ReadAllText(LoadLog.CeoLogPath(dir.Path)), "price.applied"));
        Assert.Equal(1, Count(File.ReadAllText(LoadLog.CeoLogPath(dir.Path)), "staff.applied"));
        Assert.Equal(
            "price.applied version=1.12.4 item=ba:itemname_sodacan price=2.75 call=Buildings.Office.Headquarters.PricingManagerPlan.SetPrice",
            priceLine);
        Assert.Equal(
            "staff.applied version=1.12.4 role=food kind=hr plan=hr-soda amount=40",
            staffLine);
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

    private static SoldPrice Soda()
    {
        return new SoldPrice("ba:itemname_sodacan", 2.75f);
    }

    private static StaffPlan SodaCrew()
    {
        return new StaffPlan("food", "hr", "hr-soda", 40);
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
            Path = Directory.CreateTempSubdirectory("chaotic-apply").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
