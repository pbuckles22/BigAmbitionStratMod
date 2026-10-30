using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class ShutdownTests
{
    [Fact]
    public void Shutdown_one_confirmed_close_locks_that_store()
    {
        var steps = new[]
        {
            new RoadStep("open", "ba:street_fourthavenue", 28, 1099d),
            new RoadStep("close", "ba:street_side", 9, 30d),
        };

        var shut = Shutdown.Confirm(Api(), steps, "ba:street_side", 9, true);

        Assert.True(shut.Closed);
        Assert.Equal("ba:street_side", shut.Street);
        Assert.Equal(9, shut.Number);
        Assert.Equal("Helpers.BusinessHelper.ShutdownBusiness", shut.Call);
        Assert.NotEqual(steps[0].Street, shut.Street);
    }

    [Fact]
    public void Shutdown_an_unconfirmed_close_stays_open()
    {
        using var dir = new TempLog();
        var steps = new[]
        {
            new RoadStep("close", "ba:street_side", 9, 30d),
        };

        var shut = Shutdown.Confirm(Api(), steps, "ba:street_side", 9, false);

        Assert.False(shut.Closed);
        Assert.Equal("", shut.Street);
        Assert.Equal(0, shut.Number);
        Assert.Equal("", shut.Call);
        Assert.Equal("", Shutdown.Record(dir.Path, shut));
        Assert.False(File.Exists(LoadLog.CeoLogPath(dir.Path)));
    }

    [Fact]
    public void Shutdown_an_open_step_is_not_closed()
    {
        var steps = new[]
        {
            new RoadStep("open", "ba:street_fourthavenue", 28, 1099d),
            new RoadStep("close", "ba:street_side", 9, 30d),
        };

        var shut = Shutdown.Confirm(Api(), steps, "ba:street_fourthavenue", 28, true);

        Assert.False(shut.Closed);
        Assert.Equal("", shut.Street);
        Assert.Equal(0, shut.Number);
        Assert.Equal("", shut.Call);
        Assert.DoesNotContain("SendBuyBuildingOffer", shut.Call);
        Assert.DoesNotContain("SetPrice", shut.Call);
        Assert.DoesNotContain("AddDestination", shut.Call);
    }

    [Fact]
    public void Shutdown_a_store_outside_the_list_stays_open()
    {
        var steps = new[]
        {
            new RoadStep("close", "ba:street_side", 9, 30d),
        };

        var shut = Shutdown.Confirm(Api(), steps, "ba:street_other", 4, true);

        Assert.False(shut.Closed);
        Assert.Equal("", shut.Street);
        Assert.Equal(0, shut.Number);
        Assert.Equal("", shut.Call);
    }

    [Fact]
    public void Shutdown_a_blank_close_call_stays_open()
    {
        var steps = new[]
        {
            new RoadStep("close", "ba:street_side", 9, 30d),
        };

        var shut = Shutdown.Confirm(
            new ApiNote("3682", "", "buy", "price", "delivery"),
            steps,
            "ba:street_side",
            9,
            true);

        Assert.False(shut.Closed);
        Assert.Equal("", shut.Call);
    }

    [Fact]
    public void Shutdown_a_missing_list_stays_open()
    {
        var shut = Shutdown.Confirm(Api(), null, "ba:street_side", 9, true);

        Assert.False(shut.Closed);
        Assert.Equal("", shut.Street);
        Assert.Equal(0, shut.Number);
    }

    [Fact]
    public void Shutdown_number_zero_stays_open()
    {
        var steps = new[]
        {
            new RoadStep("close", "ba:street_side", 0, 30d),
        };

        var shut = Shutdown.Confirm(Api(), steps, "ba:street_side", 0, true);

        Assert.False(shut.Closed);
        Assert.Equal(0, shut.Number);
        Assert.Equal("", shut.Street);
    }

    [Fact]
    public void Shutdown_a_blank_street_stays_open()
    {
        var steps = new[]
        {
            new RoadStep("close", "", 9, 30d),
        };

        var shut = Shutdown.Confirm(Api(), steps, "", 9, true);

        Assert.False(shut.Closed);
        Assert.Equal("", shut.Street);
        Assert.Equal("", shut.Call);
    }

    [Fact]
    public void Shutdown_the_other_number_on_that_street_stays_open()
    {
        var steps = new[]
        {
            new RoadStep("close", "ba:street_side", 9, 30d),
        };

        var shut = Shutdown.Confirm(Api(), steps, "ba:street_side", 8, true);

        Assert.False(shut.Closed);
        Assert.Equal("", shut.Street);
        Assert.Equal(0, shut.Number);
        Assert.Equal("", shut.Call);
    }

    [Fact]
    public void Shutdown_a_missing_api_stays_open()
    {
        var steps = new[]
        {
            new RoadStep("close", "ba:street_side", 9, 30d),
        };

        var shut = Shutdown.Confirm(null, steps, "ba:street_side", 9, true);

        Assert.False(shut.Closed);
        Assert.Equal("", shut.Call);
    }

    [Fact]
    public void Shutdown_a_null_step_does_not_close_the_store()
    {
        var steps = new RoadStep[]
        {
            null!,
            new RoadStep("close", "ba:street_side", 9, 30d),
        };

        var shut = Shutdown.Confirm(Api(), steps, "ba:street_side", 9, true);

        Assert.True(shut.Closed);
        Assert.Equal("ba:street_side", shut.Street);
        Assert.Equal(9, shut.Number);
    }

    [Fact]
    public void Shutdown_each_close_is_its_own_log_line()
    {
        using var dir = new TempLog();
        var steps = new[]
        {
            new RoadStep("close", "ba:street_side", 9, 5000d),
            new RoadStep("close", "ba:street_other", 4, 10d),
        };

        var first = Shutdown.Confirm(Api(), steps, "ba:street_side", 9, true);
        var second = Shutdown.Confirm(Api(), steps, "ba:street_other", 4, true);
        var firstLine = Shutdown.Record(dir.Path, first);
        var secondLine = Shutdown.Record(dir.Path, second);
        var text = File.ReadAllText(LoadLog.CeoLogPath(dir.Path));

        Assert.True(first.Closed);
        Assert.True(second.Closed);
        Assert.Equal("ba:street_side", first.Street);
        Assert.Equal(9, first.Number);
        Assert.Equal("ba:street_other", second.Street);
        Assert.Equal(4, second.Number);
        Assert.NotEqual(first.Street, second.Street);
        Assert.DoesNotContain("ba:street_other", firstLine);
        Assert.DoesNotContain("number=9", secondLine);
        Assert.Equal(2, Count(text, "store.closed"));
        Assert.Contains("call=Helpers.BusinessHelper.ShutdownBusiness", firstLine);
        Assert.Contains("call=Helpers.BusinessHelper.ShutdownBusiness", secondLine);
    }

    [Fact]
    public void Shutdown_reload_undoes_the_confirmed_close()
    {
        using var dir = new TempLog();
        var steps = new[]
        {
            new RoadStep("close", "ba:street_side", 9, 30d),
        };
        var row = new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -30f);

        var shut = Shutdown.Confirm(Api(), steps, "ba:street_side", 9, true);
        var logged = Shutdown.Record(dir.Path, shut);
        var undone = Shutdown.Reload(shut);

        Assert.True(shut.Closed);
        Assert.False(undone.Closed);
        Assert.Equal("", undone.Street);
        Assert.Equal(0, undone.Number);
        Assert.Equal("", undone.Call);
        Assert.NotEqual(shut.Street, undone.Street);
        Assert.Equal(-30f, row.Profit);
        Assert.Equal("ba:street_side", row.Street);
        Assert.Contains(logged, File.ReadAllText(LoadLog.CeoLogPath(dir.Path)));
        Assert.Equal("", Shutdown.Record(dir.Path, undone));
        Assert.Equal(1, Count(File.ReadAllText(LoadLog.CeoLogPath(dir.Path)), "store.closed"));
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
            Path = Directory.CreateTempSubdirectory("chaotic-shutdown").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
