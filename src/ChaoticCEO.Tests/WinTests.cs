using ChaoticCEO;

namespace ChaoticCEO.Tests;

[Collection(nameof(AutopilotState))]
public class WinTests
{
    [Fact]
    public void Win_milestone_defaults_to_win_net_worth_and_accepts_owner_target()
    {
        Win.ClearApproved();
        Assert.Equal(TopBar.WinNetWorth, Win.Milestone);
        Assert.Equal(100000000m, Win.Milestone);

        Win.Approve(80_000_000m);
        Assert.Equal(80_000_000m, Win.Milestone);

        Win.ClearApproved();
        Assert.Equal(TopBar.WinNetWorth, Win.Milestone);
    }

    [Fact]
    public void Win_crossed_is_true_at_or_above_milestone()
    {
        Win.ClearApproved();
        Assert.False(Win.Crossed(TopBar.WinNetWorth - 1m));
        Assert.True(Win.Crossed(TopBar.WinNetWorth));
        Assert.True(Win.Crossed(TopBar.WinNetWorth + 1m));

        Win.Approve(50_000_000m);
        Assert.False(Win.Crossed(49_999_999m));
        Assert.True(Win.Crossed(50_000_000m));
        Win.ClearApproved();
    }

    [Fact]
    public void Win_record_logs_once_when_crossing_and_keeps_autopilot_on()
    {
        using var dir = new TempLog();
        var path = Path.Combine(dir.Path, "ceo.log");
        File.WriteAllText(path, LoadLog.Line(LoadLog.PinnedBuild) + Environment.NewLine);
        Autopilot.Enabled = true;
        Win.ClearApproved();

        var first = Win.Record(path, day: 40, netWorth: TopBar.WinNetWorth);
        var again = Win.Record(path, day: 41, netWorth: TopBar.WinNetWorth + 1m);
        var log = File.ReadAllText(path);

        Assert.Equal(
            "win.reached version=" + LoadLog.Version
            + " day=40 netWorth=" + TopBar.WinNetWorth.ToString(System.Globalization.CultureInfo.InvariantCulture),
            first);
        Assert.Equal("", again);
        Assert.Contains(first, log);
        Assert.Equal(1, CountToken(log, "win.reached "));
        Assert.True(Autopilot.Enabled);
    }

    [Fact]
    public void Win_record_skips_when_below_milestone()
    {
        using var dir = new TempLog();
        var path = Path.Combine(dir.Path, "ceo.log");
        File.WriteAllText(path, "");
        Autopilot.Enabled = true;
        Win.ClearApproved();

        var line = Win.Record(path, day: 10, netWorth: TopBar.WinNetWorth - 1m);

        Assert.Equal("", line);
        Assert.Equal("", File.ReadAllText(path));
        Assert.True(Autopilot.Enabled);
    }

    [Fact]
    public void Win_record_uses_approved_target_from_stage_sim()
    {
        // Winner: approved. Owner-approved 8.6 target logs before WinNetWorth.
        using var dir = new TempLog();
        var path = Path.Combine(dir.Path, "ceo.log");
        File.WriteAllText(path, "");
        Win.Approve(25_000_000m);
        Autopilot.Enabled = true;

        var missed = Win.Record(path, day: 20, netWorth: 24_999_999m);
        var hit = Win.Record(path, day: 21, netWorth: 25_000_000m);

        Assert.Equal("", missed);
        Assert.StartsWith("win.reached version=" + LoadLog.Version + " day=21 netWorth=25000000", hit);
        Assert.True(Autopilot.Enabled);
        Win.ClearApproved();
    }

    [Fact]
    public void Win_record_empty_or_invalid_is_blank()
    {
        Win.ClearApproved();
        Assert.Equal("", Win.Record(null!, 1, TopBar.WinNetWorth));
        Assert.Equal("", Win.Record("", 1, TopBar.WinNetWorth));
        Assert.Equal("", Win.Record(" ", 1, TopBar.WinNetWorth));

        using var dir = new TempLog();
        var path = Path.Combine(dir.Path, "ceo.log");
        Assert.Equal("", Win.Record(path, 0, TopBar.WinNetWorth));
        Assert.Equal("", Win.Record(path, -1, TopBar.WinNetWorth));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Win_approve_zero_or_negative_keeps_win_net_worth()
    {
        Win.Approve(12_000_000m);
        Assert.Equal(12_000_000m, Win.Milestone);

        Win.Approve(0m);
        Assert.Equal(TopBar.WinNetWorth, Win.Milestone);

        Win.Approve(-1m);
        Assert.Equal(TopBar.WinNetWorth, Win.Milestone);
        Win.ClearApproved();
    }

    [Fact]
    public void Win_already_logged_blocks_a_second_crossing_line()
    {
        using var dir = new TempLog();
        var path = Path.Combine(dir.Path, "ceo.log");
        File.WriteAllText(
            path,
            "win.reached version=" + LoadLog.Version + " day=5 netWorth=100000000" + Environment.NewLine);
        Win.ClearApproved();

        Assert.True(Win.AlreadyLogged(path));
        Assert.Equal("", Win.Record(path, day: 99, netWorth: TopBar.WinNetWorth * 2m));
        Assert.Equal(1, CountToken(File.ReadAllText(path), "win.reached "));
    }

    private static int CountToken(string text, string token)
    {
        var count = 0;
        var at = 0;
        while ((at = text.IndexOf(token, at, StringComparison.Ordinal)) >= 0)
        {
            count++;
            at += token.Length;
        }

        return count;
    }

    private sealed class TempLog : IDisposable
    {
        public TempLog()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ceo-win-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
            }
        }
    }
}
