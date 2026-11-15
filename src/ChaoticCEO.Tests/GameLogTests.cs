using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class GameLogTests
{
    [Fact]
    public void GameLog_a_new_game_keeps_the_previous_log_and_starts_empty()
    {
        CeoLedger.Clear();
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "game-log-" + Guid.NewGuid().ToString("N")));
        try
        {
            var saved = new object();
            var fresh = new object();
            Assert.Equal("", GameLog.Key(null));
            Assert.Equal(GameLog.Key(saved), GameLog.Key(saved));
            Assert.NotEqual(GameLog.Key(saved), GameLog.Key(fresh));

            var opened = GameLog.Bind(dir.FullName, GameLog.Key(saved));
            var rent = new BuildingRent("ba:street_seventhavenue", 22, true, "rent");
            var rented = Rent.Record(dir.FullName, rent, 1588);
            var skipped = Starter.Skip(dir.FullName, 1588);
            Assert.StartsWith("game.opened ", opened);
            Assert.Contains("key=" + GameLog.Key(saved), opened);
            Assert.True(CeoLedger.TryLayout(dir.FullName, out _, out _, out var skippedLayout));
            Assert.True(skippedLayout);

            var again = GameLog.Bind(dir.FullName, GameLog.Key(saved));
            Assert.Equal("", again);
            Assert.True(CeoLedger.TryLayout(dir.FullName, out _, out _, out skippedLayout));
            Assert.True(skippedLayout);

            var next = GameLog.Bind(dir.FullName, GameLog.Key(fresh));
            Assert.StartsWith("game.opened ", next);
            Assert.Contains("key=" + GameLog.Key(fresh), next);
            Assert.False(CeoLedger.TryLayout(dir.FullName, out _, out _, out _));
            Assert.False(CeoLedger.TryRent(dir.FullName, out _, out _));

            var later = new object();
            var third = GameLog.Bind(dir.FullName, GameLog.Key(later));
            Assert.StartsWith("game.opened ", third);
            Assert.Contains(next, File.ReadAllText(GameLog.ArchivePath(dir.FullName, 2)));
            Assert.DoesNotContain(skipped, File.ReadAllText(LoadLog.CeoLogPath(dir.FullName)));

            var current = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));
            var archive = File.ReadAllText(GameLog.ArchivePath(dir.FullName, 1));
            Assert.DoesNotContain("layout.skipped ", current);
            Assert.DoesNotContain("building.rented ", current);
            Assert.Contains(skipped, archive);
            Assert.Contains(rented, archive);
            Assert.Contains(opened, archive);
            Assert.Contains(third, current);
            Assert.Contains(LoadLog.Line(LoadLog.PinnedBuild), current);
        }
        finally
        {
            CeoLedger.Clear();
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Chain_layout_skip_then_new_game_does_not_keep_it()
    {
        // Chain: rent → layout skip → game switch → new layout gate
        CeoLedger.Clear();
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "game-log-chain-" + Guid.NewGuid().ToString("N")));
        try
        {
            var saved = new object();
            var fresh = new object();
            LiveDump.BeginGame(dir.FullName, saved);
            var previous = new BuildingRent("ba:street_seventhavenue", 22, true, "rent");
            Rent.Record(dir.FullName, previous, 1588);
            var skipped = Starter.Skip(dir.FullName, 1588);
            Assert.True(CeoLedger.TryLayout(dir.FullName, out _, out _, out var skippedLayout));
            Assert.True(skippedLayout);

            var opened = LiveDump.BeginGame(dir.FullName, fresh);
            Assert.False(CeoLedger.TryLayout(dir.FullName, out _, out _, out _));

            var rent = new BuildingRent("ba:street_seventhavenue", 30, true, GameApi.Live().Call("clone"));
            var pick = Starter.Choose(Starter.StageOne(), alreadyOpen: false, GameApi.Live());
            var loaded = Starter.Record(dir.FullName, pick, rent, 1);
            Assert.StartsWith("layout.loaded ", loaded);
            Assert.True(CeoLedger.TryLayout(dir.FullName, out var street, out var number, out skippedLayout));
            Assert.Equal(rent.Street, street);
            Assert.Equal(rent.Number, number);
            Assert.False(skippedLayout);

            var current = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));
            var archive = File.ReadAllText(GameLog.ArchivePath(dir.FullName, 1));
            Assert.Contains(loaded, current);
            Assert.DoesNotContain(skipped, current);
            Assert.Contains(skipped, archive);
            Assert.DoesNotContain(loaded, archive);
            Assert.Contains(opened, current);
        }
        finally
        {
            CeoLedger.Clear();
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void GameLog_a_boot_keeps_the_previous_game_log()
    {
        CeoLedger.Clear();
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "game-log-boot-" + Guid.NewGuid().ToString("N")));
        try
        {
            var current = LoadLog.CeoLogPath(dir.FullName);
            Directory.CreateDirectory(Path.GetDirectoryName(current)!);
            File.WriteAllText(
                current,
                "layout.skipped version=1.17.12 day=1588 reason=open" + Environment.NewLine);

            var loaded = LoadLog.StartSession(dir.FullName);

            Assert.Equal(LoadLog.Line(LoadLog.PinnedBuild), loaded);
            Assert.Equal(loaded + Environment.NewLine, File.ReadAllText(current));
            Assert.Contains(
                "layout.skipped version=1.17.12 day=1588 reason=open",
                File.ReadAllText(GameLog.ArchivePath(dir.FullName, 1)));
            Assert.False(CeoLedger.TryLayout(dir.FullName, out _, out _, out _));
        }
        finally
        {
            CeoLedger.Clear();
            Directory.Delete(dir.FullName, true);
        }
    }
}
