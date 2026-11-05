using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class CloneTests
{
    [Fact]
    public void Clone_one_confirmed_layout_locks_that_street_and_blueprint()
    {
        var layout = Selected();

        var copied = Clone.Confirm(Api(), layout, layout.Street, layout.Blueprint, true);

        Assert.True(copied.Copied);
        Assert.Equal("ba:street_main", copied.Street);
        Assert.Equal("food", copied.Family);
        Assert.Equal("soda-line", copied.Blueprint);
        Assert.Equal(1, copied.Source);
        Assert.Equal(4, copied.Destination);
        Assert.Equal(layout.Street, copied.Street);
        Assert.Equal(layout.Blueprint, copied.Blueprint);
        Assert.Equal(layout.Source, copied.Source);
        Assert.Equal(layout.Destination, copied.Destination);
        Assert.Equal(
            "BusinessLayoutSets.BusinessLayoutSetHelper.LoadBusinessLayoutSet",
            copied.Call);
        Assert.DoesNotContain("SetBusinessLayout", copied.Call);
        Assert.DoesNotContain("ApplyInteriorDesign", copied.Call);
        Assert.DoesNotContain("Command_CloneItem", copied.Call);
        Assert.DoesNotContain("SendBuyBuildingOffer", copied.Call);
        Assert.DoesNotContain("ShutdownBusiness", copied.Call);
        Assert.DoesNotContain("SetPrice", copied.Call);
        Assert.DoesNotContain("AddDestination", copied.Call);
    }

    [Fact]
    public void Clone_an_unconfirmed_layout_stays()
    {
        using var dir = new TempLog();
        var layout = Selected();

        var copied = Clone.Confirm(Api(), layout, layout.Street, layout.Blueprint, false);

        Assert.False(copied.Copied);
        Assert.Equal("", copied.Street);
        Assert.Equal("", copied.Blueprint);
        Assert.Equal(0, copied.Source);
        Assert.Equal(0, copied.Destination);
        Assert.Equal("", copied.Call);
        Assert.Equal("", Clone.Record(dir.Path, copied));
        Assert.False(File.Exists(LoadLog.CeoLogPath(dir.Path)));
    }

    [Fact]
    public void Clone_a_different_street_stays()
    {
        var copied = Clone.Confirm(Api(), Selected(), "ba:street_fourthavenue", "soda-line", true);

        Assert.False(copied.Copied);
        Assert.Equal("", copied.Street);
        Assert.Equal("", copied.Blueprint);
        Assert.Equal("", copied.Call);
        Assert.NotEqual("ba:street_main", copied.Street);
        Assert.DoesNotContain("LoadBusinessLayoutSet", copied.Call);
    }

    [Fact]
    public void Clone_a_different_blueprint_stays()
    {
        var copied = Clone.Confirm(Api(), Selected(), "ba:street_main", "burger-line", true);

        Assert.False(copied.Copied);
        Assert.Equal("", copied.Blueprint);
        Assert.Equal("", copied.Call);
        Assert.NotEqual("soda-line", copied.Blueprint);
        Assert.DoesNotContain("LoadBusinessLayoutSet", copied.Call);
    }

    [Fact]
    public void Clone_a_buy_on_that_street_does_not_replace_the_blueprint()
    {
        var layout = Selected();
        var steps = new[]
        {
            new RoadStep("open", layout.Street, layout.Destination, 10d),
            new RoadStep("close", "ba:street_side", 9, 30d),
        };

        var offered = Offer.Confirm(Api(), steps, layout.Street, layout.Destination, true);
        var copied = Clone.Confirm(Api(), layout, layout.Street, layout.Blueprint, true);

        Assert.True(offered.Offered);
        Assert.Equal(layout.Street, offered.Street);
        Assert.Equal(layout.Destination, offered.Number);
        Assert.True(copied.Copied);
        Assert.Equal("soda-line", copied.Blueprint);
        Assert.Equal(layout.Blueprint, copied.Blueprint);
        Assert.NotEqual(offered.Call, copied.Call);
        Assert.Equal(
            "BusinessLayoutSets.BusinessLayoutSetHelper.LoadBusinessLayoutSet",
            copied.Call);
        Assert.DoesNotContain("SendBuyBuildingOffer", copied.Call);
    }

    [Fact]
    public void Clone_an_empty_layout_stays()
    {
        var copied = Clone.Confirm(
            Api(),
            new LayoutClone("", "", "", 0, 0),
            "ba:street_main",
            "soda-line",
            true);

        Assert.False(copied.Copied);
        Assert.Equal("", copied.Street);
        Assert.Equal("", copied.Blueprint);
        Assert.Equal(0, copied.Destination);
        Assert.Equal("", copied.Call);
    }

    [Fact]
    public void Clone_a_blank_family_stays()
    {
        var copied = Clone.Confirm(
            Api(),
            new LayoutClone("ba:street_main", "", "soda-line", 1, 4),
            "ba:street_main",
            "soda-line",
            true);

        Assert.False(copied.Copied);
        Assert.Equal("", copied.Family);
        Assert.Equal("", copied.Blueprint);
        Assert.Equal("", copied.Call);
        Assert.NotEqual("soda-line", copied.Blueprint);
    }

    [Fact]
    public void Clone_a_blank_blueprint_stays()
    {
        var copied = Clone.Confirm(
            Api(),
            new LayoutClone("ba:street_main", "food", "", 1, 4),
            "ba:street_main",
            "",
            true);

        Assert.False(copied.Copied);
        Assert.Equal("", copied.Blueprint);
        Assert.Equal("", copied.Call);
    }

    [Fact]
    public void Clone_a_layout_without_a_destination_stays()
    {
        var copied = Clone.Confirm(
            Api(),
            new LayoutClone("ba:street_main", "food", "soda-line", 1, 0),
            "ba:street_main",
            "soda-line",
            true);

        Assert.False(copied.Copied);
        Assert.Equal(0, copied.Destination);
        Assert.Equal("", copied.Call);
        Assert.NotEqual(4, copied.Destination);
    }

    [Fact]
    public void Clone_a_layout_without_a_source_stays()
    {
        var copied = Clone.Confirm(
            Api(),
            new LayoutClone("ba:street_main", "food", "soda-line", 0, 4),
            "ba:street_main",
            "soda-line",
            true);

        Assert.False(copied.Copied);
        Assert.Equal(0, copied.Source);
        Assert.Equal("", copied.Call);
    }

    [Fact]
    public void Clone_a_blank_clone_call_stays()
    {
        var layout = Selected();
        var copied = Clone.Confirm(
            new ApiNote(
                "3682",
                "Helpers.BusinessHelper.ShutdownBusiness",
                "BizManPresentation.SendBuyBuildingOffer",
                "Buildings.Office.Headquarters.PricingManagerPlan.SetPrice",
                "UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination",
                ""),
            layout,
            layout.Street,
            layout.Blueprint,
            true);

        Assert.False(copied.Copied);
        Assert.Equal("", copied.Street);
        Assert.Equal("", copied.Blueprint);
        Assert.Equal("", copied.Call);
    }

    [Fact]
    public void Clone_a_blank_buy_call_still_copies_the_layout()
    {
        var layout = Selected();
        var copied = Clone.Confirm(
            new ApiNote(
                "3682",
                "Helpers.BusinessHelper.ShutdownBusiness",
                "",
                "Buildings.Office.Headquarters.PricingManagerPlan.SetPrice",
                "UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination",
                "BusinessLayoutSets.BusinessLayoutSetHelper.LoadBusinessLayoutSet"),
            layout,
            layout.Street,
            layout.Blueprint,
            true);

        Assert.True(copied.Copied);
        Assert.Equal("soda-line", copied.Blueprint);
        Assert.Equal(
            "BusinessLayoutSets.BusinessLayoutSetHelper.LoadBusinessLayoutSet",
            copied.Call);
        Assert.DoesNotContain("SendBuyBuildingOffer", copied.Call);
    }

    [Fact]
    public void Clone_a_missing_layout_stays()
    {
        var copied = Clone.Confirm(Api(), null, "ba:street_main", "soda-line", true);

        Assert.False(copied.Copied);
        Assert.Equal("", copied.Street);
        Assert.Equal("", copied.Blueprint);
        Assert.Equal("", copied.Call);
    }

    [Fact]
    public void Clone_a_missing_api_stays()
    {
        var layout = Selected();
        var copied = Clone.Confirm(null, layout, layout.Street, layout.Blueprint, true);

        Assert.False(copied.Copied);
        Assert.Equal("", copied.Call);
        Assert.Equal("", copied.Blueprint);
    }

    [Fact]
    public void Clone_a_blank_street_stays()
    {
        var copied = Clone.Confirm(Api(), Selected(), "", "soda-line", true);

        Assert.False(copied.Copied);
        Assert.Equal("", copied.Street);
        Assert.Equal("", copied.Call);
    }

    [Fact]
    public void Clone_a_null_street_stays()
    {
        var copied = Clone.Confirm(Api(), Selected(), null, "soda-line", true);

        Assert.False(copied.Copied);
        Assert.Equal("", copied.Street);
        Assert.Equal("", copied.Blueprint);
        Assert.Equal("", copied.Call);
    }

    [Fact]
    public void Clone_a_null_blueprint_stays()
    {
        var copied = Clone.Confirm(Api(), Selected(), "ba:street_main", null, true);

        Assert.False(copied.Copied);
        Assert.Equal("", copied.Blueprint);
        Assert.Equal("", copied.Call);
    }

    [Fact]
    public void Clone_keeps_the_layout_family()
    {
        var layout = new LayoutClone("ba:street_main", "durables", "gift-line", 3, 8);

        var copied = Clone.Confirm(Api(), layout, layout.Street, layout.Blueprint, true);

        Assert.True(copied.Copied);
        Assert.Equal("durables", copied.Family);
        Assert.Equal("gift-line", copied.Blueprint);
        Assert.Equal(3, copied.Source);
        Assert.Equal(8, copied.Destination);
        Assert.NotEqual("food", copied.Family);
        Assert.NotEqual("soda-line", copied.Blueprint);
    }

    [Fact]
    public void Clone_each_copy_is_its_own_log_line()
    {
        using var dir = new TempLog();
        var firstLayout = Selected();
        var secondLayout = new LayoutClone("ba:street_other", "durables", "gift-line", 2, 8);

        var first = Clone.Confirm(Api(), firstLayout, firstLayout.Street, firstLayout.Blueprint, true);
        var second = Clone.Confirm(Api(), secondLayout, secondLayout.Street, secondLayout.Blueprint, true);
        var skipped = Clone.Confirm(Api(), firstLayout, firstLayout.Street, "burger-line", true);
        var firstLine = Clone.Record(dir.Path, first);
        var secondLine = Clone.Record(dir.Path, second);
        var text = File.ReadAllText(LoadLog.CeoLogPath(dir.Path));

        Assert.True(first.Copied);
        Assert.True(second.Copied);
        Assert.False(skipped.Copied);
        Assert.Equal("soda-line", first.Blueprint);
        Assert.Equal("gift-line", second.Blueprint);
        Assert.NotEqual(first.Street, second.Street);
        Assert.DoesNotContain("gift-line", firstLine);
        Assert.DoesNotContain("soda-line", secondLine);
        Assert.DoesNotContain("burger-line", firstLine);
        Assert.DoesNotContain("burger-line", secondLine);
        Assert.Equal("", Clone.Record(dir.Path, skipped));
        Assert.Equal(2, Count(text, "layout.cloned"));
        Assert.Contains(
            "call=BusinessLayoutSets.BusinessLayoutSetHelper.LoadBusinessLayoutSet",
            firstLine);
        Assert.Contains(
            "call=BusinessLayoutSets.BusinessLayoutSetHelper.LoadBusinessLayoutSet",
            secondLine);
        Assert.DoesNotContain("building.offered", text);
        Assert.DoesNotContain("store.closed", text);
        Assert.DoesNotContain("price.applied", text);
        Assert.DoesNotContain("staff.applied", text);
        Assert.DoesNotContain("logistics.redone", text);
        Assert.DoesNotContain("SendBuyBuildingOffer", text);
        Assert.DoesNotContain("ShutdownBusiness", text);
        Assert.DoesNotContain("SetPrice", text);
        Assert.DoesNotContain("AddDestination", text);
    }

    [Fact]
    public void Clone_a_null_copy_does_not_log()
    {
        using var dir = new TempLog();

        var copied = Clone.Reload(null);

        Assert.False(copied.Copied);
        Assert.Equal("", copied.Street);
        Assert.Equal("", copied.Blueprint);
        Assert.Equal(0, copied.Source);
        Assert.Equal(0, copied.Destination);
        Assert.Equal("", copied.Call);
        Assert.Equal("", Clone.Record(dir.Path, null));
        Assert.False(File.Exists(LoadLog.CeoLogPath(dir.Path)));
    }

    [Fact]
    public void Clone_reload_undoes_the_confirmed_copy()
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
        var layout = Selected();

        var copied = Clone.Confirm(Api(), layout, layout.Street, layout.Blueprint, true);
        var logged = Clone.Record(dir.Path, copied);
        var undone = Clone.Reload(copied);
        var text = File.ReadAllText(LoadLog.CeoLogPath(dir.Path));

        Assert.True(copied.Copied);
        Assert.False(undone.Copied);
        Assert.Equal("", undone.Street);
        Assert.Equal("", undone.Blueprint);
        Assert.Equal(0, undone.Source);
        Assert.Equal(0, undone.Destination);
        Assert.Equal("", undone.Call);
        Assert.NotEqual(copied.Street, undone.Street);
        Assert.NotEqual(copied.Blueprint, undone.Blueprint);
        Assert.Equal(statementsBefore, File.ReadAllText(StatementDump.CsvPath(dir.Path)));
        Assert.Equal(routesBefore, File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.Contains("ba:street_fourthavenue", statementsBefore);
        Assert.Contains("ba:itemname_sodacan", routesBefore);
        Assert.Contains(logged, text);
        Assert.Equal("", Clone.Record(dir.Path, undone));
        Assert.Equal(1, Count(File.ReadAllText(LoadLog.CeoLogPath(dir.Path)), "layout.cloned"));
        Assert.Equal(
            "layout.cloned version=1.11.6 street=ba:street_main family=food blueprint=soda-line source=1 destination=4 call=BusinessLayoutSets.BusinessLayoutSetHelper.LoadBusinessLayoutSet",
            logged);
        Assert.DoesNotContain("LoadBusinessLayoutSet", statementsBefore);
        Assert.DoesNotContain("LoadBusinessLayoutSet", routesBefore);
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

    private static LayoutClone Selected()
    {
        return new LayoutClone("ba:street_main", "food", "soda-line", 1, 4);
    }

    private static ApiNote Api()
    {
        return new ApiNote(
            "3682",
            "Helpers.BusinessHelper.ShutdownBusiness",
            "BizManPresentation.SendBuyBuildingOffer",
            "Buildings.Office.Headquarters.PricingManagerPlan.SetPrice",
            "UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination",
            "BusinessLayoutSets.BusinessLayoutSetHelper.LoadBusinessLayoutSet");
    }

    private sealed class TempLog : IDisposable
    {
        public TempLog()
        {
            Path = Directory.CreateTempSubdirectory("chaotic-clone").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
