using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class RoadshowTests
{
    [Fact]
    public void Roadshow_the_open_step_leads_and_the_move_is_its_own_line()
    {
        var steps = new[]
        {
            new RoadStep("open", "ba:street_fourthavenue", 28, 1099d),
            new RoadStep("close", "ba:street_side", 9, 30d),
        };
        var review = new LogisticsReview(
            Array.Empty<StoreService>(),
            Array.Empty<MixedWarehouse>(),
            Array.Empty<ShortStore>(),
            new StoreMove("ba:street_side 9", "W1", "W2", "specialty"));
        var overlay = OneOverlay();
        var board = new CityBoard(steps, review, overlay);

        var lines = Roadshow.Lines(board);
        var text = Panel.Text(lines);

        Assert.Equal("open", lines[0].Tone);
        Assert.Equal("step", lines[0].Business);
        Assert.Equal("ba:street_fourthavenue", lines[0].Street);
        Assert.Equal(28, lines[0].Number);
        Assert.Equal(steps[0].Explain, lines[0].Explain);
        Assert.Equal("open  step  ba:street_fourthavenue 28", text[0]);
        Assert.Contains("Daily gain 1099", text[1]);
        Assert.Equal("close", lines[1].Tone);
        Assert.Equal("move", lines[2].Tone);
        Assert.Equal("logistics", lines[2].Business);
        Assert.Equal("ba:street_side", lines[2].Street);
        Assert.Equal(9, lines[2].Number);
        Assert.Equal(review.Move!.Explain, lines[2].Explain);
        Assert.Equal("move  logistics  ba:street_side 9", text[4]);
        Assert.NotEqual(lines[0].Street, lines[2].Street);
        Assert.DoesNotContain(board.Steps, step => step.Kind == "move");
        Assert.Equal(3, lines.Count);
        Assert.DoesNotContain(lines, line => line.Tone == "red");
    }

    [Fact]
    public void Roadshow_a_review_without_a_move_skips_that_line()
    {
        var board = new CityBoard(
            new[] { new RoadStep("close", "ba:street_side", 9, 30d) },
            LogisticsReview.None(),
            Array.Empty<OverlayRow>());

        var lines = Roadshow.Lines(board);

        Assert.Single(lines);
        Assert.Equal("close", lines[0].Tone);
        Assert.DoesNotContain(lines, line => line.Tone == "move");
    }

    [Fact]
    public void Roadshow_eleven_losing_stores_keep_ten_steps()
    {
        var rows = new StatementRow[11];
        for (var number = 1; number <= 11; number++)
        {
            rows[number - 1] = new StatementRow("ba:street_s", number, 0f, 0f, 0f, 0f, 0f, 0f, -number);
        }

        var board = Roadshow.Compose(
            new[] { new FinancialDay(1013, rows) },
            Array.Empty<DeliveryStop>(),
            Array.Empty<WarehouseHold>(),
            Array.Empty<BuildingAsk>(),
            Portfolio(rows));

        Assert.Equal(Roadshow.Top, board.Steps.Count);
        Assert.Equal(10, board.Steps.Count);
        Assert.Equal("close", board.Steps[0].Kind);
        Assert.Equal(11, board.Steps[0].Number);
        Assert.Equal(-11d, board.Steps[0].DailyGain);
        Assert.DoesNotContain(board.Steps, step => step.Number == 1);
        Assert.Null(board.Review.Move);
    }

    [Fact]
    public void Roadshow_a_shut_store_leaves_the_close_list_and_an_unlisted_store_stays()
    {
        var days = new[]
        {
            new FinancialDay(
                1013,
                new[]
                {
                    new StatementRow("ba:street_tenthstreet", 13, 0f, 0f, 0f, 0f, 0f, 0f, -100f),
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 0f, 0f, 0f, -40f),
                    new StatementRow("ba:street_fourthavenue", 28, 0f, 0f, 0f, 50f, 0f, 0f, 10f),
                }),
        };
        var asks = new[] { new BuildingAsk("ba:street_fourthavenue", 28, 5000d) };
        var portfolio = new[] { new PortfolioBusiness("Side", "specialty", "ba:street_side", 9) };

        var kept = Roadshow.Compose(
            days,
            Array.Empty<DeliveryStop>(),
            Array.Empty<WarehouseHold>(),
            asks,
            portfolio);
        var board = Roadshow.Compose(
            days,
            Array.Empty<DeliveryStop>(),
            Array.Empty<WarehouseHold>(),
            asks,
            portfolio,
            new[] { new PortfolioBusiness("", "", "ba:street_tenthstreet", 13) });

        Assert.Contains(kept.Steps, step => step.Kind == "close" && step.Street == "ba:street_tenthstreet" && step.Number == 13);
        Assert.Contains(board.Steps, step => step.Kind == "close" && step.Street == "ba:street_side" && step.Number == 9);
        Assert.DoesNotContain(board.Steps, step => step.Street == "ba:street_tenthstreet" && step.Number == 13);
        Assert.Contains(
            board.Steps,
            step => step.Kind == "open" && step.Street == "ba:street_fourthavenue" && step.Number == 28);
    }

    [Fact]
    public void Roadshow_a_named_building_at_the_same_address_keeps_the_store()
    {
        var shut = LiveDump.WithoutABusiness(new[]
        {
            ("ba:street_tenthstreet", 13, false),
            ("ba:street_tenthstreet", 13, true),
            ("ba:street_side", 9, false),
            ("", 4, false),
        });

        Assert.Single(shut);
        Assert.Equal("ba:street_side", shut[0].Street);
        Assert.Equal(9, shut[0].Number);
    }

    [Fact]
    public void Roadshow_a_for_sale_price_keeps_the_address()
    {
        var prices = Roadshow.Prices(
            new[]
            {
                new BuildingAsk("ba:street_a", 1, 5000d),
                new BuildingAsk("ba:street_b", 2, 0d),
                new BuildingAsk("", 3, 80d),
                new BuildingAsk("ba:street_d", 0, 80d),
            },
            new[]
            {
                new BuildingAsk("ba:street_a", 1, 100d),
                new BuildingAsk("ba:street_c", 3, 40d),
            });

        Assert.Equal(2, prices.Count);
        Assert.Equal("ba:street_a", prices[0].Street);
        Assert.Equal(1, prices[0].Number);
        Assert.Equal(5000d, prices[0].Price);
        Assert.Equal("ba:street_c", prices[1].Street);
        Assert.Equal(40d, prices[1].Price);
    }

    [Fact]
    public void Roadshow_a_second_frame_does_not_log()
    {
        using var dir = new TempLog();
        var board = new CityBoard(
            new[] { new RoadStep("open", "ba:street_fourthavenue", 28, 1099d) },
            LogisticsReview.None(),
            Array.Empty<OverlayRow>());
        var session = new PanelSession();

        var opened = session.Press(dir.Path, board);
        session.Draw(board);
        session.Draw(board);

        var log = File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log"));
        Assert.Equal("ba:street_fourthavenue", opened[0].Street);
        Assert.Equal(1, Count(log, "roadmap.shown"));
        Assert.Contains(
            "roadmap.shown version=" + LoadLog.Version
                + " steps=1 street=ba:street_fourthavenue number=28 gain=1099 move=0",
            log);
        Assert.Equal(1, Count(log, "panel.opened"));

        session.Press(dir.Path, board);
        Assert.False(session.IsOpen);
        Assert.Equal(log, File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")));
        Assert.Equal(1, Count(File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "history.log")), "roadmap.changed"));
    }

    [Fact]
    public void Roadshow_the_same_roadmap_is_one_history_line()
    {
        using var dir = new TempLog();
        var board = new CityBoard(
            new[] { new RoadStep("open", "ba:street_fourthavenue", 28, 1099d) },
            LogisticsReview.None(),
            Array.Empty<OverlayRow>());
        var session = new PanelSession();

        session.Press(dir.Path, board);
        session.Press(dir.Path, board);
        session.Press(dir.Path, board);

        var current = File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log"));
        var history = File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "history.log"));
        Assert.Equal(2, Count(current, "roadmap.shown"));
        Assert.Equal(0, Count(current, "advice.shown"));
        Assert.Equal(1, Count(history, "roadmap.changed"));
        Assert.Contains("steps=1 street=ba:street_fourthavenue number=28 gain=1099 move=0", history);
    }

    [Fact]
    public void LoadLog_a_boot_keeps_the_change_and_starts_a_current_log()
    {
        using var dir = new TempLog();
        var current = Path.Combine(dir.Path, "ChaoticCEO", "ceo.log");
        Directory.CreateDirectory(Path.GetDirectoryName(current)!);
        File.WriteAllText(
            current,
            "advice.shown version=1.17.6 tone=red business=Chaotic Gym #2 street=ba:street_firststreet number=35 explain=Close Chaotic Gym #2. Profit 3777.6381."
                + Environment.NewLine
                + "roadmap.shown version=1.7.1 steps=10 street=ba:street_tenthstreet number=13 gain=13771.4512 move=1"
                + Environment.NewLine
                + "roadmap.shown version=1.17.6 steps=10 street=ba:street_tenthstreet number=13 gain=13771.4512 move=1"
                + Environment.NewLine
                + "store.closed version=1.17.6 street=ba:street_tenthstreet number=13 call=Helpers.BusinessHelper.ShutdownBusiness"
                + Environment.NewLine);

        var loaded = LoadLog.StartSession(dir.Path);
        var again = LoadLog.StartSession(dir.Path);

        var log = File.ReadAllText(current);
        var history = File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "history.log"));
        Assert.Equal("mod.loaded version=" + LoadLog.Version + " build=3682", loaded);
        Assert.Equal(loaded, again);
        Assert.Equal(loaded + Environment.NewLine, log);
        Assert.DoesNotContain("advice.shown", log);
        Assert.DoesNotContain("advice.shown", history);
        Assert.Equal(1, Count(history, "roadmap.changed"));
        Assert.Contains(
            "roadmap.changed version=1.7.1 steps=10 street=ba:street_tenthstreet number=13 gain=13771.4512 move=1",
            history);
        Assert.Contains("store.closed version=1.17.6 street=ba:street_tenthstreet number=13", history);
        Assert.Equal(1, Count(history, "store.closed"));
    }

    [Fact]
    public void LoadLog_history_keeps_the_latest_200_changes()
    {
        using var dir = new TempLog();
        var current = Path.Combine(dir.Path, "ChaoticCEO", "ceo.log");
        Directory.CreateDirectory(Path.GetDirectoryName(current)!);
        var lines = new string[LoadLog.HistoryLimit + 1];
        for (var index = 0; index < lines.Length; index++)
        {
            lines[index] = "store.closed version=1.17.6 street=ba:street_s number="
                + (index + 1).ToString()
                + " call=Helpers.BusinessHelper.ShutdownBusiness";
        }

        File.WriteAllLines(current, lines);
        LoadLog.StartSession(dir.Path);

        var history = File.ReadAllLines(Path.Combine(dir.Path, "ChaoticCEO", "history.log"));
        Assert.Equal(LoadLog.HistoryLimit, history.Length);
        Assert.Contains("number=2 ", history[0]);
        Assert.Contains("number=" + lines.Length.ToString() + " ", history[history.Length - 1]);
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

    private static IReadOnlyList<OverlayRow> OneOverlay()
    {
        var days = new[]
        {
            new FinancialDay(1, new[] { new StatementRow("ba:street_a", 1, 0f, 0f, 0f, 0f, 0f, 0f, -80f) }),
        };
        return Overlay.Build(
            days,
            new[] { new PortfolioBusiness("Low", "food", "ba:street_a", 1) },
            Array.Empty<WarehouseHold>());
    }

    private static PortfolioBusiness[] Portfolio(StatementRow[] rows)
    {
        var businesses = new PortfolioBusiness[rows.Length];
        for (var index = 0; index < rows.Length; index++)
        {
            businesses[index] = new PortfolioBusiness(
                "Store " + rows[index].Number.ToString(),
                "food",
                rows[index].Street,
                rows[index].Number);
        }

        return businesses;
    }

    private sealed class TempLog : IDisposable
    {
        public TempLog()
        {
            Path = Directory.CreateTempSubdirectory("chaotic-roadshow").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
