using System;
using System.IO;
using Xunit;

namespace ChaoticCEO.Tests;

[Collection(nameof(AutopilotState))]
public class CrashTraceTests
{
    [Fact]
    public void Crash_caught_line_names_the_exception_the_payload_and_the_steps()
    {
        CrashTrace.Reset();
        using var dir = new TempLog();
        var path = Path.Combine(dir.Path, "ceo.log");
        CrashTrace.Pulse(4);
        CrashTrace.Begin();
        CrashTrace.Note("TryRent", "street=seventh number=26");

        Exception thrown;
        try
        {
            throw new NullReferenceException("station missing\nstill open");
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        var line = CrashTrace.Catch(path, thrown, "TryLayout", "bare=14 cash=15000");
        var log = File.ReadAllText(path);

        Assert.StartsWith("crash.caught version=" + LoadLog.Version, line);
        Assert.Contains(" where=TryLayout ", line);
        Assert.Contains(" type=NullReferenceException ", line);
        Assert.Contains(" slug=UNHANDLED ", line);
        Assert.Contains(" day=4 ", line);
        Assert.Contains(" payload=bare=14_cash=15000 ", line);
        Assert.Contains(" trail=TryRent{street=seventh_number=26}>TryLayout{bare=14_cash=15000} ", line);
        Assert.Contains(" message=station_missing_still_open ", line);
        Assert.Contains(" stack=", line);
        Assert.DoesNotContain("\n", line);
        Assert.Equal(line + Environment.NewLine, log);
    }

    [Fact]
    public void Crash_same_exception_logs_once_until_the_day_changes()
    {
        CrashTrace.Reset();
        using var dir = new TempLog();
        var path = Path.Combine(dir.Path, "ceo.log");
        CrashTrace.Pulse(4);
        var thrown = new InvalidOperationException("bare lot");

        var first = CrashTrace.Catch(path, thrown, "TryRent", "bare=14");
        var again = CrashTrace.Catch(path, thrown, "TryRent", "bare=15");
        CrashTrace.Pulse(5);
        var nextDay = CrashTrace.Catch(path, thrown, "TryRent", "bare=14");
        var log = File.ReadAllText(path);

        Assert.Contains("day=4", first);
        Assert.Equal("", again);
        Assert.Contains("day=5", nextDay);
        Assert.Equal(2, Count(log, "crash.caught "));
    }

    [Fact]
    public void Guard_records_the_throw_and_the_next_step_still_runs()
    {
        CrashTrace.Reset();
        using var dir = new TempLog();
        CrashTrace.Bind(dir.Path);
        CrashTrace.Pulse(4);
        CrashTrace.Begin();
        var ran = false;

        Assert.True(CrashTrace.Guard("TryRent", "street=seventh", () => { }));
        Assert.False(CrashTrace.Guard("TryLayout", "bare=14", () => throw new InvalidOperationException("bare lot")));
        Assert.True(CrashTrace.Guard("TryOps", "goods=0", () => ran = true));
        Assert.False(CrashTrace.Guard("TryLayout", "bare=14", () => throw new InvalidOperationException("bare lot")));

        var log = File.ReadAllText(LoadLog.CeoLogPath(dir.Path));
        Assert.True(ran);
        Assert.Contains("where=TryLayout", log);
        Assert.Contains("trail=TryRent{street=seventh}>TryLayout{bare=14}", log);
        Assert.DoesNotContain("TryOps", log);
        Assert.Equal(1, Count(log, "crash.caught "));
    }

    [Fact]
    public void Crash_load_then_morning_steps_then_caught_exception_keeps_the_path()
    {
        // Chain: load → morning steps → crash
        CrashTrace.Reset();
        using var dir = new TempLog();
        var loaded = LoadLog.StartSession(dir.Path);
        var armed = CrashTrace.Arm(dir.Path);
        CrashTrace.Pulse(4);
        CrashTrace.Begin();
        CrashTrace.Guard("TryRent", "street=seventh number=26", () => { });
        CrashTrace.Guard("TryLayout", "bare=14 cash=15000", () => throw new NullReferenceException("station"));

        var log = File.ReadAllText(LoadLog.CeoLogPath(dir.Path));
        Assert.Contains(loaded, log);
        Assert.Contains("trace.armed version=" + LoadLog.Version, armed);
        Assert.Contains(armed, log);
        Assert.Contains("crash.caught version=" + LoadLog.Version, log);
        Assert.Contains("where=TryLayout", log);
        Assert.Contains("type=NullReferenceException", log);
        Assert.Contains("slug=UNHANDLED", log);
        Assert.Contains("payload=bare=14_cash=15000", log);
        Assert.Contains("trail=TryRent{street=seventh_number=26}>TryLayout{bare=14_cash=15000}", log);
    }

    [Fact]
    public void Crash_load_then_morning_steps_then_win_keeps_the_path()
    {
        // Chain: load → morning steps → win
        CrashTrace.Reset();
        using var dir = new TempLog();
        var loaded = LoadLog.StartSession(dir.Path);
        CrashTrace.Arm(dir.Path);
        CrashTrace.Pulse(40);
        CrashTrace.Begin();
        CrashTrace.Guard("TryRent", "street=seventh number=26", () => { });
        CrashTrace.Guard("TryLayout", "bare=0", () => { });
        Win.ClearApproved();
        var reached = Win.Record(LoadLog.CeoLogPath(dir.Path), 40, TopBar.WinNetWorth);

        var log = File.ReadAllText(LoadLog.CeoLogPath(dir.Path));
        Assert.Contains(loaded, log);
        Assert.StartsWith("win.reached version=" + LoadLog.Version, reached);
        Assert.Contains(reached, log);
        Assert.Contains(
            "win.trail version=" + LoadLog.Version
            + " day=40 netWorth=" + TopBar.WinNetWorth.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + " trail=TryRent{street=seventh_number=26}>TryLayout{bare=0}",
            log);
        Assert.Equal(1, Count(log, "win.reached "));
        Assert.Equal(1, Count(log, "win.trail "));
    }

    private static int Count(string text, string token)
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
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ceo-crash-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (Exception)
            {
            }
        }
    }
}
