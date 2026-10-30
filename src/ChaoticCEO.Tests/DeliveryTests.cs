using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class DeliveryTests
{
    [Fact]
    public void Delivery_one_confirmed_reassignment_locks_that_move()
    {
        var review = GiftOntoFood();

        var redo = Delivery.Confirm(Api(), review, "ba:street_side 9", true);

        Assert.True(redo.Moved);
        Assert.Equal("ba:street_side 9", redo.Destination);
        Assert.Equal("W1", redo.From);
        Assert.Equal("W2", redo.To);
        Assert.Equal("specialty", redo.Family);
        Assert.Equal(
            "UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination",
            redo.Call);
        Assert.DoesNotContain("ShutdownBusiness", redo.Call);
        Assert.DoesNotContain("SendBuyBuildingOffer", redo.Call);
        Assert.DoesNotContain("SetPrice", redo.Call);
        Assert.Equal(review.Move!.Destination, redo.Destination);
        Assert.Equal(review.Move.From, redo.From);
        Assert.Equal(review.Move.To, redo.To);
        Assert.Equal(review.Move.Family, redo.Family);
    }

    [Fact]
    public void Delivery_an_unconfirmed_reassignment_stays()
    {
        using var dir = new TempLog();
        var review = GiftOntoFood();

        var redo = Delivery.Confirm(Api(), review, "ba:street_side 9", false);

        Assert.False(redo.Moved);
        Assert.Equal("", redo.Destination);
        Assert.Equal("", redo.From);
        Assert.Equal("", redo.To);
        Assert.Equal("", redo.Family);
        Assert.Equal("", redo.Call);
        Assert.Equal("", Delivery.Record(dir.Path, redo));
        Assert.False(File.Exists(LoadLog.CeoLogPath(dir.Path)));
    }

    [Fact]
    public void Delivery_a_different_destination_stays()
    {
        var review = GiftOntoFood();

        var redo = Delivery.Confirm(Api(), review, "ba:street_other 4", true);

        Assert.False(redo.Moved);
        Assert.Equal("", redo.Destination);
        Assert.Equal("", redo.From);
        Assert.Equal("", redo.To);
        Assert.Equal("", redo.Call);
        Assert.DoesNotContain("AddDestination", redo.Call);
        Assert.DoesNotContain("ShutdownBusiness", redo.Call);
    }

    [Fact]
    public void Delivery_a_review_with_no_move_stays()
    {
        var review = Logistics.Review(
            Api(),
            new[]
            {
                Stop("ba:street_fourthstreet 46", "ba:street_firstavenue 38", Soda, 1016),
            },
            new[]
            {
                Hold("W1", "ba:street_fourthstreet", 46, Soda, 45),
            });

        var redo = Delivery.Confirm(Api(), review, "ba:street_firstavenue 38", true);

        Assert.Null(review.Move);
        Assert.False(redo.Moved);
        Assert.Equal("", redo.Destination);
        Assert.Equal("", redo.Call);
    }

    [Fact]
    public void Delivery_a_blank_delivery_call_stays()
    {
        var redo = Delivery.Confirm(
            new ApiNote("3682", "close", "buy", "price", ""),
            GiftOntoFood(),
            "ba:street_side 9",
            true);

        Assert.False(redo.Moved);
        Assert.Equal("", redo.Call);
        Assert.Equal("", redo.Destination);
    }

    [Fact]
    public void Delivery_a_missing_review_stays()
    {
        var redo = Delivery.Confirm(Api(), null, "ba:street_side 9", true);

        Assert.False(redo.Moved);
        Assert.Equal("", redo.Destination);
        Assert.Equal("", redo.Call);
    }

    [Fact]
    public void Delivery_a_missing_api_stays()
    {
        var redo = Delivery.Confirm(null, GiftOntoFood(), "ba:street_side 9", true);

        Assert.False(redo.Moved);
        Assert.Equal("", redo.Call);
        Assert.Equal("", redo.Destination);
    }

    [Fact]
    public void Delivery_a_null_destination_stays()
    {
        var redo = Delivery.Confirm(Api(), GiftOntoFood(), null, true);

        Assert.False(redo.Moved);
        Assert.Equal("", redo.Destination);
        Assert.Equal("", redo.From);
        Assert.Equal("", redo.To);
        Assert.Equal("", redo.Call);
    }

    [Fact]
    public void Delivery_a_null_redo_does_not_log()
    {
        using var dir = new TempLog();

        var undone = Delivery.Reload(null);

        Assert.False(undone.Moved);
        Assert.Equal("", undone.Destination);
        Assert.Equal("", undone.Call);
        Assert.Equal("", Delivery.Record(dir.Path, null));
        Assert.False(File.Exists(LoadLog.CeoLogPath(dir.Path)));
    }

    [Fact]
    public void Delivery_a_blank_destination_stays()
    {
        var redo = Delivery.Confirm(Api(), GiftOntoFood(), "", true);

        Assert.False(redo.Moved);
        Assert.Equal("", redo.Destination);
        Assert.Equal("", redo.To);
        Assert.Equal("", redo.Call);
    }

    [Fact]
    public void Delivery_a_close_street_is_not_a_redo()
    {
        var review = GiftOntoFood();

        var redo = Delivery.Confirm(Api(), review, "ba:street_side", true);

        Assert.False(redo.Moved);
        Assert.Equal("", redo.Destination);
        Assert.Equal("", redo.Call);
        Assert.DoesNotContain("ShutdownBusiness", redo.Call);
        Assert.NotEqual(review.Move!.Destination, redo.Destination);
    }

    [Fact]
    public void Delivery_the_other_store_on_that_street_stays()
    {
        var redo = Delivery.Confirm(Api(), GiftOntoFood(), "ba:street_side 8", true);

        Assert.False(redo.Moved);
        Assert.Equal("", redo.Destination);
        Assert.Equal("", redo.From);
        Assert.Equal("", redo.To);
        Assert.Equal("", redo.Call);
    }

    [Fact]
    public void Delivery_each_redo_is_its_own_log_line()
    {
        using var dir = new TempLog();
        var gift = GiftOntoFood();
        var service = LaterService();

        var first = Delivery.Confirm(Api(), gift, "ba:street_side 9", true);
        var second = Delivery.Confirm(Api(), service, "ba:street_b 2", true);
        var firstLine = Delivery.Record(dir.Path, first);
        var secondLine = Delivery.Record(dir.Path, second);
        var text = File.ReadAllText(LoadLog.CeoLogPath(dir.Path));

        Assert.True(first.Moved);
        Assert.True(second.Moved);
        Assert.Equal("ba:street_side 9", first.Destination);
        Assert.Equal("W1", first.From);
        Assert.Equal("W2", first.To);
        Assert.Equal("specialty", first.Family);
        Assert.Equal("ba:street_b 2", second.Destination);
        Assert.Equal("W0", second.From);
        Assert.Equal("Wservice", second.To);
        Assert.Equal("service", second.Family);
        Assert.NotEqual(first.Destination, second.Destination);
        Assert.DoesNotContain("ba:street_b 2", firstLine);
        Assert.DoesNotContain("ba:street_side 9", secondLine);
        Assert.DoesNotContain("to=W2", secondLine);
        Assert.Equal(2, Count(text, "logistics.redone"));
        Assert.Contains(
            "call=UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination",
            firstLine);
        Assert.Contains(
            "call=UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination",
            secondLine);
        Assert.DoesNotContain("ShutdownBusiness", text);
    }

    [Fact]
    public void Delivery_reload_undoes_the_confirmed_reassignment()
    {
        using var dir = new TempLog();
        var stops = new[]
        {
            Stop("ba:street_fourthstreet 46", "ba:street_firstavenue 38", Soda, 1016),
            Stop("ba:street_fourthstreet 46", "ba:street_side 9", Gift, null),
        };
        var holds = new[]
        {
            Hold("W1", "ba:street_fourthstreet", 46, Soda, 45),
            Hold("W2", "ba:street_b", 2, Gift, 20),
        };
        RouteDump.Write(dir.Path, stops, holds);
        var routesPath = RouteDump.RoutesPath(dir.Path);
        var before = File.ReadAllText(routesPath);
        var review = Logistics.Review(Api(), stops, holds);

        var redo = Delivery.Confirm(Api(), review, review.Move!.Destination, true);
        var logged = Delivery.Record(dir.Path, redo);
        var undone = Delivery.Reload(redo);

        Assert.True(redo.Moved);
        Assert.Equal("W2", redo.To);
        Assert.False(undone.Moved);
        Assert.Equal("", undone.Destination);
        Assert.Equal("", undone.From);
        Assert.Equal("", undone.To);
        Assert.Equal("", undone.Family);
        Assert.Equal("", undone.Call);
        Assert.NotEqual(redo.Destination, undone.Destination);
        Assert.Contains("ba:street_fourthstreet 46,ba:street_side 9,ba:itemname_cheapgift", before);
        Assert.Equal(before, File.ReadAllText(routesPath));
        Assert.Contains(logged, File.ReadAllText(LoadLog.CeoLogPath(dir.Path)));
        Assert.Equal("", Delivery.Record(dir.Path, undone));
        Assert.Equal(1, Count(File.ReadAllText(LoadLog.CeoLogPath(dir.Path)), "logistics.redone"));
        Assert.DoesNotContain("W2,ba:street_side 9", File.ReadAllText(routesPath));
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

    private static LogisticsReview GiftOntoFood()
    {
        return Logistics.Review(
            Api(),
            new[]
            {
                Stop("ba:street_fourthstreet 46", "ba:street_firstavenue 38", Soda, 1016),
                Stop("ba:street_fourthstreet 46", "ba:street_side 9", Gift, null),
            },
            new[]
            {
                Hold("W1", "ba:street_fourthstreet", 46, Soda, 45),
                Hold("W2", "ba:street_b", 2, Gift, 20),
            });
    }

    private static LogisticsReview LaterService()
    {
        return Logistics.Review(
            Api(),
            new[]
            {
                Stop("ba:street_w0 1", "ba:street_a 1", Gift, null),
                Stop("ba:street_w0 1", "ba:street_b 2", Hair, null),
            },
            new[]
            {
                Hold("W0", "ba:street_w0", 1, Soda, 5),
                Hold("Wgift", "ba:street_gift", 3, Gift, 4),
                Hold("Wservice", "ba:street_service", 4, Hair, 4),
            });
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

    private static DeliveryStop Stop(string warehouse, string destination, string item, int? day)
    {
        return new DeliveryStop(warehouse, destination, item, day, "");
    }

    private static WarehouseHold Hold(string warehouse, string street, int number, string item, int amount)
    {
        return new WarehouseHold(warehouse, street, number, item, amount);
    }

    private const string Soda = "ba:itemname_sodacan";

    private const string Gift = "ba:itemname_cheapgift";

    private const string Hair = "ba:itemname_haircareproduct";

    private sealed class TempLog : IDisposable
    {
        public TempLog()
        {
            Path = Directory.CreateTempSubdirectory("chaotic-delivery").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
