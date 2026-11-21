using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class CeoLedgerTests
{
    [Fact]
    public void FrameTick_is_due_only_after_the_interval()
    {
        Assert.False(FrameTick.Due(0.4f, 0.5f));
        Assert.True(FrameTick.Due(0.5f, 0.5f));
        Assert.True(FrameTick.Due(0.6f, 0.5f));
        Assert.Equal(1.1f, FrameTick.Next(0.6f), 3);
        Assert.Equal(0.5f, FrameTick.Interval);
    }

    [Fact]
    public void CeoLedger_remembers_rent_without_rereading_the_log()
    {
        CeoLedger.Clear();
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-ledger-" + Guid.NewGuid().ToString("N")));
        try
        {
            var rent = new BuildingRent("ba:street_seventhavenue", 26, true, GameApi.Live().Call("rent"));
            var written = Rent.Record(dir.FullName, rent, 1);
            File.Delete(LoadLog.CeoLogPath(dir.FullName));

            Assert.StartsWith("building.rented ", written);
            Assert.True(CeoLedger.TryRent(out var street, out var number));
            Assert.Equal("ba:street_seventhavenue", street);
            Assert.Equal(26, number);
            Assert.True(CeoLedger.TryRent(out _, out _));
        }
        finally
        {
            CeoLedger.Clear();
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void CeoLedger_hydrates_once_from_ceo_log_then_stays_in_memory()
    {
        CeoLedger.Clear();
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-ledger-hydrate-" + Guid.NewGuid().ToString("N")));
        try
        {
            var path = LoadLog.CeoLogPath(dir.FullName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(
                path,
                "building.rented version=1.18.6 day=1 street=ba:street_seventhavenue number=22 call=x"
                    + Environment.NewLine
                    + "layout.loaded version=1.18.6 day=1 street=ba:street_seventhavenue number=22 type=g layout=Gift call=y"
                    + Environment.NewLine);

            Assert.True(CeoLedger.TryRent(dir.FullName, out var street, out var number));
            Assert.Equal("ba:street_seventhavenue", street);
            Assert.Equal(22, number);
            File.Delete(path);
            Assert.True(CeoLedger.TryLayout(dir.FullName, out street, out number, out var skipped));
            Assert.Equal("ba:street_seventhavenue", street);
            Assert.Equal(22, number);
            Assert.False(skipped);
        }
        finally
        {
            CeoLedger.Clear();
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Chain_rent_record_to_ledger_to_last_layout_gate()
    {
        // Chain: rent record → ledger remember → last rent → layout gate
        CeoLedger.Clear();
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-ledger-chain-" + Guid.NewGuid().ToString("N")));
        try
        {
            var rent = new BuildingRent("ba:street_seventhavenue", 15, true, GameApi.Live().Call("rent"));
            var recorded = Rent.Record(dir.FullName, rent, 1);
            Assert.True(CeoLedger.TryRent(dir.FullName, out var street, out var number));
            Assert.Equal("ba:street_seventhavenue", street);
            Assert.Equal(15, number);

            var layout = Starter.Choose(Starter.StageOne(), alreadyOpen: false, GameApi.Live());
            var loaded = Starter.Record(dir.FullName, layout, rent, 1);
            Assert.StartsWith("layout.loaded ", loaded);
            Assert.True(CeoLedger.TryLayout(dir.FullName, out street, out number, out var skipped));
            Assert.Equal(street, rent.Street);
            Assert.Equal(number, rent.Number);
            Assert.False(skipped);
            Assert.Contains(recorded, File.ReadAllText(LoadLog.CeoLogPath(dir.FullName)));
        }
        finally
        {
            CeoLedger.Clear();
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void CeoLedger_layout_skip_wins_over_an_earlier_load()
    {
        CeoLedger.Clear();
        CeoLedger.Ingest("layout.loaded version=1.18.6 day=1 street=ba:street_a number=1 type=g layout=x call=y");
        CeoLedger.Ingest("layout.skipped version=1.18.6 day=2 reason=open");
        Assert.True(CeoLedger.TryLayout(out _, out _, out var skipped));
        Assert.True(skipped);
    }
}
