using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class StoreCloseTests
{
    [Fact]
    public void Close_button_calls_ShutdownBusiness()
    {
        using var dir = new TempLog();
        var api = GameApi.Live();
        var board = new CityBoard(
            new[]
            {
                new RoadStep("open", "ba:street_fourthavenue", 28, 1099d),
                new RoadStep("close", "ba:street_side", 9, 30d),
            },
            LogisticsReview.None(),
            Array.Empty<OverlayRow>());
        var calls = new List<string>();

        var buttons = ClosePress.Buttons(api, board);
        var shut = ClosePress.Press(
            dir.Path,
            api,
            board,
            buttons[0].Street,
            buttons[0].Number,
            true,
            (call, street, number) => calls.Add(call + " " + street + " " + number));

        Assert.Single(buttons);
        Assert.Equal("Close", buttons[0].Label);
        Assert.Equal("Helpers.BusinessHelper.ShutdownBusiness", buttons[0].Call);
        Assert.Equal("ba:street_side", buttons[0].Street);
        Assert.Equal(9, buttons[0].Number);
        Assert.NotEqual("ba:street_fourthavenue", buttons[0].Street);
        Assert.True(shut.Closed);
        Assert.Equal(buttons[0].Street, shut.Street);
        Assert.Equal(buttons[0].Number, shut.Number);
        Assert.Equal(buttons[0].Call, shut.Call);
        Assert.Equal(
            new[] { "Helpers.BusinessHelper.ShutdownBusiness ba:street_side 9" },
            calls);
        Assert.Contains(
            "store.closed version=" + LoadLog.Version
                + " street=ba:street_side number=9 call=Helpers.BusinessHelper.ShutdownBusiness",
            File.ReadAllText(LoadLog.CeoLogPath(dir.Path)));
    }

    [Fact]
    public void Close_confirm_replaces_the_close_button()
    {
        var idle = ClosePress.Controls(false);
        var armed = ClosePress.Controls(true);

        Assert.Equal(new[] { "Close" }, idle);
        Assert.Equal(new[] { "Confirm", "Cancel" }, armed);
        Assert.DoesNotContain("Close", armed);
    }

    [Fact]
    public void Close_an_open_step_and_an_unconfirmed_click_do_not_call_ShutdownBusiness()
    {
        using var dir = new TempLog();
        var api = GameApi.Live();
        var board = new CityBoard(
            new[]
            {
                new RoadStep("open", "ba:street_fourthavenue", 28, 1099d),
                new RoadStep("close", "ba:street_side", 9, 30d),
                new RoadStep("close", "ba:street_other", 4, 10d),
            },
            LogisticsReview.None(),
            Array.Empty<OverlayRow>());
        var calls = new List<string>();

        var buttons = ClosePress.Buttons(api, board);
        var unconfirmed = ClosePress.Press(
            dir.Path,
            api,
            board,
            "ba:street_side",
            9,
            false,
            (call, street, number) => calls.Add(call + " " + street + " " + number));
        var open = ClosePress.Press(
            dir.Path,
            api,
            board,
            "ba:street_fourthavenue",
            28,
            true,
            (call, street, number) => calls.Add(call + " " + street + " " + number));
        var second = ClosePress.Press(
            dir.Path,
            api,
            board,
            buttons[1].Street,
            buttons[1].Number,
            true,
            (call, street, number) => calls.Add(call + " " + street + " " + number));
        var log = File.Exists(LoadLog.CeoLogPath(dir.Path))
            ? File.ReadAllText(LoadLog.CeoLogPath(dir.Path))
            : "";

        Assert.Equal(2, buttons.Count);
        Assert.Equal("Close", buttons[0].Label);
        Assert.Equal("Close", buttons[1].Label);
        Assert.Equal("ba:street_side", buttons[0].Street);
        Assert.Equal(9, buttons[0].Number);
        Assert.Equal("ba:street_other", buttons[1].Street);
        Assert.Equal(4, buttons[1].Number);
        Assert.Equal("Helpers.BusinessHelper.ShutdownBusiness", buttons[0].Call);
        Assert.Equal("Helpers.BusinessHelper.ShutdownBusiness", buttons[1].Call);
        Assert.DoesNotContain(buttons, button => button.Street == "ba:street_fourthavenue");
        Assert.False(unconfirmed.Closed);
        Assert.Equal("", unconfirmed.Call);
        Assert.False(open.Closed);
        Assert.Equal("", open.Street);
        Assert.Equal(0, open.Number);
        Assert.Equal("", open.Call);
        Assert.True(second.Closed);
        Assert.Equal("ba:street_other", second.Street);
        Assert.Equal(4, second.Number);
        Assert.Equal("Helpers.BusinessHelper.ShutdownBusiness", second.Call);
        Assert.Equal(new[] { "Helpers.BusinessHelper.ShutdownBusiness ba:street_other 4" }, calls);
        Assert.DoesNotContain("ba:street_side", log);
        Assert.DoesNotContain("ba:street_fourthavenue", log);
        Assert.Contains(
            "store.closed version=" + LoadLog.Version
                + " street=ba:street_other number=4 call=Helpers.BusinessHelper.ShutdownBusiness",
            log);
        Assert.Equal(1, Count(log, "store.closed"));
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

    private sealed class TempLog : IDisposable
    {
        public TempLog()
        {
            Path = Directory.CreateTempSubdirectory("chaotic-store-close").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
