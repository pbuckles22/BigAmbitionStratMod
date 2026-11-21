using ChaoticCEO;

namespace ChaoticCEO.Tests;

[Collection(nameof(AutopilotState))]
public class NamedBlockTests
{
    [Fact]
    public void Inside_false_is_not_inside()
    {
        var line = NamedBlock.Line(70, "layout", "not-inside", "ba:street_seventhavenue", 22);
        Assert.Equal(
            "block.named version=" + LoadLog.Version
                + " day=70 where=layout slug=not-inside street=ba:street_seventhavenue number=22",
            line);
    }

    [Fact]
    public void Each_silent_refusal_has_its_own_slug()
    {
        Assert.Contains("slug=no-rent", NamedBlock.Line(2, "layout", "no-rent", "", 0));
        Assert.Contains("slug=no-lot", NamedBlock.Line(2, "layout", "no-lot", "ba:street_a", 1));
        Assert.Contains("slug=no-layout", NamedBlock.Line(2, "ops", "no-layout", "", 0));
        Assert.Contains("slug=no-pick", NamedBlock.Line(2, "layout", "no-pick", "ba:street_a", 1));
        Assert.Contains("slug=no-items", NamedBlock.Line(2, "ops", "no-items", "ba:street_a", 1));
        Assert.Contains("slug=waiting", NamedBlock.Line(2, "ops", "waiting", "ba:street_a", 1));
        Assert.Contains("slug=bad-place", NamedBlock.Line(2, "fitout", "bad-place", "", 0));
        Assert.Contains(
            "slug=ops-station-not-initialized-frame0",
            NamedBlock.Line(2, "ops", "ops-station-not-initialized-frame0", "ba:street_a", 1));
    }

    [Fact]
    public void Bare_rented_lot_counts_and_frame0_still_retries()
    {
        Assert.Equal(
            1,
            BareLease.Count(new[]
            {
                new BareLeaseFact(true, "", "ba:street_a", 4),
                new BareLeaseFact(true, "ba:businesstype_giftshop", "ba:street_b", 2),
                new BareLeaseFact(false, "", "ba:street_c", 1),
            }));
        Assert.True(StoreOps.Retry("ops-station-not-initialized-frame0"));
        Assert.True(StoreOps.Retry("notstarted"));
    }

    [Fact]
    public void Record_writes_the_line_once()
    {
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-named-block-" + Guid.NewGuid().ToString("N")));
        try
        {
            CrashTrace.Reset();
            CrashTrace.Bind(dir.FullName);
            var line = NamedBlock.Record(dir.FullName, 3, "layout", "not-inside", "Main Street", 9);
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));
            Assert.Equal(
                "block.named version=" + LoadLog.Version
                    + " day=3 where=layout slug=not-inside street=Main_Street number=9",
                line);
            Assert.Contains(line, text);
            Assert.Contains("block.layout", CrashTrace.Trail());
        }
        finally
        {
            CrashTrace.Reset();
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Day1_frames_before_rent_log_no_rent_once()
    {
        // Harvest: new game day 1 wrote 1216 block.named slug=no-rent before building.rented.
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-named-block-once-" + Guid.NewGuid().ToString("N")));
        try
        {
            var first = NamedBlock.Record(dir.FullName, 1, "layout", "no-rent", "", 0);
            var again = NamedBlock.Record(dir.FullName, 1, "layout", "no-rent", "", 0);
            var ops = NamedBlock.Record(dir.FullName, 1, "ops", "no-layout", "", 0);
            var nextDay = NamedBlock.Record(dir.FullName, 2, "layout", "no-rent", "", 0);
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));
            Assert.Equal(first, again);
            Assert.Equal(1, Count(text, first));
            Assert.Equal(1, Count(text, ops));
            Assert.Contains("slug=no-layout", ops);
            Assert.Equal(1, Count(text, nextDay));
            Assert.Contains("day=2", nextDay);
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void New_game_logs_the_same_refusal_again()
    {
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-named-block-fresh-" + Guid.NewGuid().ToString("N")));
        try
        {
            NamedBlock.Record(dir.FullName, 1, "layout", "no-rent", "", 0);
            var loaded = LoadLog.StartSession(dir.FullName);
            var again = NamedBlock.Record(dir.FullName, 1, "layout", "no-rent", "", 0);
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));
            Assert.Contains(loaded, text);
            Assert.Equal(1, Count(text, again));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Second_save_logs_the_same_refusal_again()
    {
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-named-block-save-" + Guid.NewGuid().ToString("N")));
        try
        {
            var saved = new object();
            var fresh = new object();
            GameLog.Bind(dir.FullName, GameLog.Key(saved));
            NamedBlock.Record(dir.FullName, 1, "layout", "no-rent", "", 0);
            GameLog.Bind(dir.FullName, GameLog.Key(fresh));
            var again = NamedBlock.Record(dir.FullName, 1, "layout", "no-rent", "", 0);
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));
            Assert.Equal(1, Count(text, again));
            Assert.Contains("slug=no-rent", again);
        }
        finally
        {
            GameLog.Release(dir.FullName);
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Chain_load_morning_steps_named_block_once()
    {
        // Chain: load → morning steps → named block
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-named-block-chain-once-" + Guid.NewGuid().ToString("N")));
        try
        {
            var loaded = LoadLog.StartSession(dir.FullName);
            var first = NamedBlock.Record(dir.FullName, 1, "layout", "no-rent", "", 0);
            var again = NamedBlock.Record(dir.FullName, 1, "layout", "no-rent", "", 0);
            var ops = NamedBlock.Record(dir.FullName, 1, "ops", "no-layout", "", 0);
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));
            Assert.Contains(loaded, text);
            Assert.Equal(first, again);
            Assert.Equal(1, Count(text, first));
            Assert.Equal(1, Count(text, ops));
            Assert.Contains("slug=no-rent", first);
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Chain_load_morning_steps_named_block()
    {
        // Chain: load → morning steps → named block
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-named-block-chain-" + Guid.NewGuid().ToString("N")));
        try
        {
            CrashTrace.Reset();
            var loaded = LoadLog.StartSession(dir.FullName);
            CrashTrace.Bind(dir.FullName);
            CrashTrace.Arm(dir.FullName);
            CrashTrace.Pulse(5);
            CrashTrace.Begin();
            CrashTrace.Note("TryLayout", "day=5");
            var blocked = NamedBlock.Record(
                dir.FullName,
                5,
                "layout",
                "not-inside",
                "ba:street_seventhavenue",
                22);
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));
            Assert.Contains(loaded, text);
            Assert.Contains("trace.armed version=" + LoadLog.Version, text);
            Assert.Contains(blocked, text);
            Assert.Contains("slug=not-inside", blocked);
            Assert.Contains("TryLayout", CrashTrace.Trail());
            Assert.Contains("block.layout", CrashTrace.Trail());
        }
        finally
        {
            CrashTrace.Reset();
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Chain_fitout_ops_and_layout_share_the_block_line()
    {
        // Chain: load → morning steps → named block (fit-out and ops)
        var layout = NamedBlock.Line(8, "layout", "not-inside", "ba:street_a", 4);
        var ops = NamedBlock.Line(8, "ops", "no-items", "ba:street_a", 4);
        var fitout = NamedBlock.Line(8, "fitout", "not-inside", "ba:street_a", 4);
        Assert.StartsWith("block.named version=" + LoadLog.Version, layout);
        Assert.Contains("where=layout", layout);
        Assert.Contains("where=ops", ops);
        Assert.Contains("where=fitout", fitout);
        Assert.Contains("slug=not-inside", fitout);
        Assert.Contains("slug=no-items", ops);
    }

    private static int Count(string text, string line)
    {
        var hits = 0;
        foreach (var row in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (row == line)
            {
                hits++;
            }
        }

        return hits;
    }
}
