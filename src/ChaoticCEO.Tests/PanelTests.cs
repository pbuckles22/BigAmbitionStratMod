using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class PanelTests
{
    [Fact]
    public void Panel_locks_the_rows()
    {
        var days = new[]
        {
            new FinancialDay(
                1,
                new[]
                {
                    Row("ba:street_a", 1, 0f),
                    Row("ba:street_b", 2, 100f),
                }),
            new FinancialDay(2, new[] { Row("ba:street_a", 1, -80f) }),
        };
        var portfolio = new[]
        {
            Biz("Low", "food", "ba:street_a", 1),
            Biz("High", "food", "ba:street_b", 2),
        };
        var overlay = Overlay.Build(days, portfolio, Array.Empty<WarehouseHold>());
        var notes = Advice.Explain(overlay);

        var panel = Panel.Rows(overlay);

        Assert.Equal(notes.Count, panel.Count);
        for (var index = 0; index < notes.Count; index++)
        {
            Assert.Equal(notes[index].Tone, panel[index].Tone);
            Assert.Equal(notes[index].Business, panel[index].Business);
            Assert.Equal(notes[index].Street, panel[index].Street);
            Assert.Equal(notes[index].Number, panel[index].Number);
            Assert.Equal(notes[index].Explain, panel[index].Explain);
        }

        var bottom = panel.Last(row => row.Tone == "red");
        Assert.Equal("Low", bottom.Business);
        Assert.Equal("ba:street_a", bottom.Street);
        Assert.Equal(1, bottom.Number);
        Assert.Equal("Close Low. Profit -40.", bottom.Explain);
        var text = Panel.Text(panel);
        Assert.Equal("red  Low  ba:street_a 1", text[text.Count - 2]);
        Assert.Equal("Close Low. Profit -40.", text[text.Count - 1]);
    }

    [Fact]
    public void Panel_the_city_window_is_opaque()
    {
        var paint = Panel.Paint();

        Assert.True(paint.Alpha >= 0.9f);
        Assert.True(paint.Red < 0.2f);
        Assert.True(paint.Green < 0.2f);
        Assert.True(paint.Blue < 0.2f);
        Assert.True(paint.TextRed >= 0.9f);
        Assert.True(paint.TextGreen >= 0.9f);
        Assert.True(paint.TextBlue >= 0.9f);
        Assert.True(paint.FontSize >= 16);
    }

    [Fact]
    public void Panel_scroll_over_the_window_is_eaten()
    {
        Assert.True(Panel.EatScroll(true, 0f, 1f));
        Assert.True(Panel.EatScroll(true, -1f, 0f));
        Assert.False(Panel.EatScroll(false, 0f, 1f));
        Assert.False(Panel.EatScroll(true, 0f, 0f));
    }

    [Fact]
    public void Panel_a_wheel_over_the_open_window_is_held()
    {
        Assert.Equal(1f, Panel.HoldWheel(true, true, 1f));
        Assert.Equal(-2f, Panel.HoldWheel(true, true, -2f));
        Assert.Equal(0f, Panel.HoldWheel(true, false, 1f));
        Assert.Equal(0f, Panel.HoldWheel(false, true, 1f));
        Assert.Equal(0f, Panel.HoldWheel(true, true, 0f));
        Assert.Equal(20f, Panel.WheelStep(1f));
        Assert.Equal(-40f, Panel.WheelStep(-2f));
    }

    [Fact]
    public void Panel_a_second_frame_does_not_log()
    {
        using var dir = new TempLog();
        var overlay = OneOverlay();
        var session = new PanelSession();

        var opened = session.Press(dir.Path, overlay);
        var drawn = session.Draw(overlay);
        session.Draw(overlay);

        var log = File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log"));
        Assert.True(session.IsOpen);
        Assert.Equal("Low", opened.Last(row => row.Tone == "red").Business);
        Assert.Equal(opened.Count, drawn.Count);
        Assert.Equal("Close Low. Profit -40.", drawn.Last(row => row.Tone == "red").Explain);
        Assert.Equal(1, log.Split('\n').Count(line => line.StartsWith("panel.opened ", StringComparison.Ordinal)));
        Assert.Equal(overlay.Count, log.Split('\n').Count(line => line.StartsWith("advice.shown ", StringComparison.Ordinal)));
        Assert.Contains("panel.opened version=" + LoadLog.Version + " rows=" + overlay.Count.ToString(), log);

        session.Press(dir.Path, overlay);
        Assert.False(session.IsOpen);
        Assert.Empty(session.Draw(overlay));
        Assert.Equal(log, File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")));
    }

    [Fact]
    public void Panel_an_empty_overlay_logs_the_open()
    {
        using var dir = new TempLog();
        var session = new PanelSession();

        var lines = session.Press(dir.Path, Array.Empty<OverlayRow>());

        var log = File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log"));
        Assert.Empty(lines);
        Assert.Contains("panel.opened version=" + LoadLog.Version + " rows=0", log);
        Assert.DoesNotContain("advice.shown ", log);
    }

    [Fact]
    public void Panel_a_fast_food_store_is_food()
    {
        Assert.Equal("food", StoreFamily.For("ba:businesstype_fastfoodrestaurant"));
        Assert.Equal("office", StoreFamily.For("ba:businesstype_lawfirm"));
        Assert.Equal("warehouse", StoreFamily.For("ba:businesstype_wholesalestore"));
        Assert.Null(StoreFamily.For("ba:businesstype_unknown"));
        Assert.Null(StoreFamily.For(""));
        Assert.Null(StoreFamily.For(null));
    }

    private static IReadOnlyList<OverlayRow> OneOverlay()
    {
        var days = new[]
        {
            new FinancialDay(
                1,
                new[]
                {
                    Row("ba:street_a", 1, 0f),
                    Row("ba:street_b", 2, 100f),
                }),
            new FinancialDay(2, new[] { Row("ba:street_a", 1, -80f) }),
        };
        var portfolio = new[]
        {
            Biz("Low", "food", "ba:street_a", 1),
            Biz("High", "food", "ba:street_b", 2),
        };
        return Overlay.Build(days, portfolio, Array.Empty<WarehouseHold>());
    }

    private static StatementRow Row(string street, int number, float profit)
    {
        return new StatementRow(street, number, 0f, 0f, 0f, 0f, 0f, 0f, profit);
    }

    private static PortfolioBusiness Biz(string business, string family, string street, int number)
    {
        return new PortfolioBusiness(business, family, street, number);
    }

    private sealed class TempLog : IDisposable
    {
        public TempLog()
        {
            Path = Directory.CreateTempSubdirectory("chaotic-panel").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
