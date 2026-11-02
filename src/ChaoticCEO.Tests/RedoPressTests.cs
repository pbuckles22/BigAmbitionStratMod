using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class RedoPressTests
{
    [Fact]
    public void Redo_button_calls_AddDestination()
    {
        using var dir = new TempLog();
        var api = GameApi.Live();
        var review = GiftOntoFood(api);
        var board = Board(review);
        var calls = new List<string>();

        var buttons = RedoPress.Buttons(api, board);
        var redone = RedoPress.Press(
            dir.Path,
            api,
            board,
            buttons[0].Destination,
            true,
            (call, destination, from, to, family) =>
                calls.Add(call + " " + destination + " " + from + " " + to + " " + family));

        Assert.Single(buttons);
        Assert.Equal("Redo", buttons[0].Label);
        Assert.Equal(
            "UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination",
            buttons[0].Call);
        Assert.Equal("ba:street_side 9", buttons[0].Destination);
        Assert.Equal("W1", buttons[0].From);
        Assert.Equal("W2", buttons[0].To);
        Assert.Equal("specialty", buttons[0].Family);
        Assert.Equal(review.Move!.Destination, buttons[0].Destination);
        Assert.Equal(review.Move.From, buttons[0].From);
        Assert.Equal(review.Move.To, buttons[0].To);
        Assert.Equal(review.Move.Family, buttons[0].Family);
        Assert.DoesNotContain("ShutdownBusiness", buttons[0].Call);
        Assert.DoesNotContain(buttons, button => button.Destination == "ba:street_fourthavenue 28");
        Assert.True(redone.Moved);
        Assert.Equal(buttons[0].Destination, redone.Destination);
        Assert.Equal(buttons[0].From, redone.From);
        Assert.Equal(buttons[0].To, redone.To);
        Assert.Equal(buttons[0].Family, redone.Family);
        Assert.Equal(buttons[0].Call, redone.Call);
        Assert.Equal(
            new[]
            {
                "UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination ba:street_side 9 W1 W2 specialty",
            },
            calls);
        Assert.Contains(
            "logistics.redone version=" + LoadLog.Version
                + " destination=ba:street_side 9 from=W1 to=W2 family=specialty call=UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination",
            File.ReadAllText(LoadLog.CeoLogPath(dir.Path)));
    }

    [Fact]
    public void Redo_confirm_replaces_the_redo_button()
    {
        var idle = RedoPress.Controls(false);
        var armed = RedoPress.Controls(true);

        Assert.Equal(new[] { "Redo" }, idle);
        Assert.Equal(new[] { "Confirm", "Cancel" }, armed);
        Assert.DoesNotContain("Redo", armed);
        Assert.DoesNotContain("Close", armed);
    }

    [Fact]
    public void Redo_an_unconfirmed_click_and_a_review_with_no_move_do_not_call_AddDestination()
    {
        using var dir = new TempLog();
        var api = GameApi.Live();
        var review = GiftOntoFood(api);
        var board = Board(review);
        var calls = new List<string>();

        var buttons = RedoPress.Buttons(api, board);
        var unconfirmed = RedoPress.Press(
            dir.Path,
            api,
            board,
            "ba:street_side 9",
            false,
            (call, destination, from, to, family) =>
                calls.Add(call + " " + destination + " " + from + " " + to + " " + family));
        var other = RedoPress.Press(
            dir.Path,
            api,
            board,
            "ba:street_fourthavenue 28",
            true,
            (call, destination, from, to, family) =>
                calls.Add(call + " " + destination + " " + from + " " + to + " " + family));
        var blank = RedoPress.Buttons(
            new ApiNote("3682", "close", "buy", "price", ""),
            board);
        var none = RedoPress.Buttons(api, new CityBoard(
            board.Steps,
            LogisticsReview.None(),
            Array.Empty<OverlayRow>()));
        var log = File.Exists(LoadLog.CeoLogPath(dir.Path))
            ? File.ReadAllText(LoadLog.CeoLogPath(dir.Path))
            : "";

        Assert.Single(buttons);
        Assert.Equal("Redo", buttons[0].Label);
        Assert.Equal("ba:street_side 9", buttons[0].Destination);
        Assert.Empty(blank);
        Assert.Empty(none);
        Assert.False(unconfirmed.Moved);
        Assert.Equal("", unconfirmed.Destination);
        Assert.Equal("", unconfirmed.Call);
        Assert.False(other.Moved);
        Assert.Equal("", other.Destination);
        Assert.Equal("", other.From);
        Assert.Equal("", other.To);
        Assert.Equal("", other.Call);
        Assert.Empty(calls);
        Assert.DoesNotContain("AddDestination", log);
        Assert.DoesNotContain("logistics.redone", log);
    }

    [Fact]
    public void Redo_moves_the_store_onto_the_named_warehouse_plan()
    {
        var warehouses = new[] { "W0", "W1", "W2" };
        var stops = new IReadOnlyList<DeskStop>[]
        {
            new[] { new DeskStop("ba:street_side", 9) },
            new[] { new DeskStop("ba:street_side", 9), new DeskStop("ba:street_other", 4) },
            new[] { new DeskStop("ba:street_firstavenue", 38) },
        };

        var picked = RedoDesk.Pick(
            warehouses,
            stops,
            "W1",
            "W2",
            "ba:street_side",
            9,
            out var source,
            out var target,
            out var stop);
        var missing = RedoDesk.Pick(
            warehouses,
            stops,
            "W1",
            "W9",
            "ba:street_side",
            9,
            out var missingSource,
            out var missingTarget,
            out var missingStop);

        Assert.True(picked);
        Assert.Equal(1, source);
        Assert.Equal(2, target);
        Assert.Equal(0, stop);
        Assert.Equal("W1", warehouses[source]);
        Assert.Equal("W2", warehouses[target]);
        Assert.Equal("ba:street_side", stops[source][stop].Street);
        Assert.Equal(9, stops[source][stop].Number);
        Assert.NotEqual(0, source);
        Assert.False(missing);
        Assert.Equal(-1, missingSource);
        Assert.Equal(-1, missingTarget);
        Assert.Equal(-1, missingStop);
    }

    [Fact]
    public void Redo_a_store_that_is_not_on_a_plan_still_names_the_warehouse()
    {
        var warehouses = new[] { "W0", "W7" };
        var stops = new IReadOnlyList<DeskStop>[]
        {
            new[] { new DeskStop("ba:street_other", 4) },
            Array.Empty<DeskStop>(),
        };

        var picked = RedoDesk.Pick(
            warehouses,
            stops,
            "ba:street_firststreet 37",
            "W7",
            "ba:street_fifthavenue",
            36,
            out var source,
            out var target,
            out var stop);
        var home = RedoDesk.Warehouse(warehouses, "W7");
        var missing = RedoDesk.Warehouse(warehouses, "W9");

        Assert.False(picked);
        Assert.Equal(-1, source);
        Assert.Equal(-1, target);
        Assert.Equal(-1, stop);
        Assert.Equal(1, home);
        Assert.Equal("W7", warehouses[home]);
        Assert.Equal(-1, missing);
    }

    [Fact]
    public void Redo_a_warehouse_with_no_plan_is_created()
    {
        Assert.Equal("add", RedoDesk.Attach(true, 1));
        Assert.Equal("create", RedoDesk.Attach(true, -1));
        Assert.Equal("miss", RedoDesk.Attach(false, -1));
    }

    private static CityBoard Board(LogisticsReview review)
    {
        return new CityBoard(
            new[]
            {
                new RoadStep("open", "ba:street_fourthavenue", 28, 1099d),
                new RoadStep("close", "ba:street_side", 9, 30d),
            },
            review,
            Array.Empty<OverlayRow>());
    }

    private static LogisticsReview GiftOntoFood(ApiNote api)
    {
        return Logistics.Review(
            api,
            new[]
            {
                new DeliveryStop(
                    "ba:street_fourthstreet 46",
                    "ba:street_firstavenue 38",
                    "ba:itemname_sodacan",
                    1016,
                    ""),
                new DeliveryStop(
                    "ba:street_fourthstreet 46",
                    "ba:street_side 9",
                    "ba:itemname_cheapgift",
                    null,
                    ""),
            },
            new[]
            {
                new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_sodacan", 45),
                new WarehouseHold("W2", "ba:street_b", 2, "ba:itemname_cheapgift", 20),
            });
    }

    private sealed class TempLog : IDisposable
    {
        public TempLog()
        {
            Path = Directory.CreateTempSubdirectory("chaotic-redo").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
