using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class TelemetryTests
{
    [Fact]
    public void Telemetry_one_day_stays_one_row()
    {
        using var dir = new TempTelemetry();
        var summary = "audit.summary version=x day=1016 unconnected=1 emptySlots=2 dry=3 mixed=1 unstaffed=4 missingPlans=5 cashBelowReserve=6 pinMismatch=0 healed=1";
        var heal = "audit.healed version=x day=1016 action=connect store=ba:street_fourthavenue 28 warehouse=W11 item=ba:itemname_haircareproduct call=Add dry=false";
        Seed(dir.Path, heal + "\n" + summary + "\n");
        var bar = TopBar.Line(1016, 1000m, 20m, 7m, 1, true, 1);
        var health = "Stores supplied unconnected=9  Slots filled emptySlots=8  Plans present missingPlans=7  Last heal healed=1 action=connect";
        var controls = Controls.Row(autopilot: true, dryRun: false);

        var first = Telemetry.Record(dir.Path, bar, health, controls);
        var second = Telemetry.Record(dir.Path, bar, health, controls);
        var lines = File.ReadAllLines(Telemetry.CsvPath(dir.Path));
        var cells = lines[1].Split(',');

        Assert.Equal(
            "day,cash,netWorth,profit,stage,unconnected,emptySlots,dry,mixed,unstaffed,missingPlans,cashBelowReserve,pinMismatch,healed,actions",
            lines[0]);
        Assert.Equal(2, lines.Length);
        Assert.Equal("1016", cells[0]);
        Assert.Equal("1000", cells[1]);
        Assert.Equal("20", cells[2]);
        Assert.Equal("7", cells[3]);
        Assert.Equal("1", cells[4]);
        Assert.Equal("9", cells[5]);
        Assert.Equal("8", cells[6]);
        Assert.Equal("3", cells[7]);
        Assert.Equal("1", cells[8]);
        Assert.Equal("4", cells[9]);
        Assert.Equal("7", cells[10]);
        Assert.Equal("6", cells[11]);
        Assert.Equal("0", cells[12]);
        Assert.Equal("1", cells[13]);
        Assert.Equal("connect", cells[14]);
        Assert.DoesNotContain("+", cells[3]);
        Assert.Contains("telemetry.written version=" + LoadLog.Version + " day=1016 rows=1 autopilot=on dry=off actions=connect", first);
        Assert.Equal("", second);
        Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.Path)).Count(line => line.StartsWith("telemetry.written ")));
    }

    [Fact]
    public void Telemetry_a_loss_stays_minus_and_a_new_day_adds_a_row()
    {
        using var dir = new TempTelemetry();
        Seed(
            dir.Path,
            "audit.healed version=x day=1016 action=connect store=a warehouse=W1 item=hair call=Add dry=false\n"
                + "audit.healed version=x day=1016 action=slot warehouse=W7 slot=1 order=Order assign=Assign driver= dry=false\n"
                + "audit.summary version=x day=1016 unconnected=1 emptySlots=2 dry=0 mixed=0 unstaffed=0 missingPlans=0 cashBelowReserve=0 pinMismatch=0 healed=2\n");
        var bar = TopBar.Line(1016, 10m, 20m, -40.5m, 2, true, 0);
        var health = "Stores supplied unconnected=1  Slots filled emptySlots=2  Plans present missingPlans=0  Last heal healed=2 action=slot";
        var controls = Controls.Row(autopilot: true, dryRun: true);
        var first = Telemetry.Record(dir.Path, bar, health, controls);

        Seed(
            dir.Path,
            File.ReadAllText(LoadLog.CeoLogPath(dir.Path))
                + "audit.summary version=x day=1017 unconnected=0 emptySlots=0 dry=0 mixed=0 unstaffed=0 missingPlans=0 cashBelowReserve=0 pinMismatch=0 healed=0\n");
        var nextBar = TopBar.Line(1017, 11m, 21m, 4m, 2, true, 0);
        var nextHealth = "Stores supplied unconnected=0  Slots filled emptySlots=0  Plans present missingPlans=0  Last heal healed=0 none";
        var second = Telemetry.Record(dir.Path, nextBar, nextHealth, controls);
        var lines = File.ReadAllLines(Telemetry.CsvPath(dir.Path));
        var loss = lines[1].Split(',');
        var added = lines[2].Split(',');

        Assert.Contains("Profit -41", bar);
        Assert.Equal("-41", loss[3]);
        Assert.Equal("connect|slot", loss[14]);
        Assert.DoesNotContain("+", loss[3]);
        Assert.Equal("1017", added[0]);
        Assert.Equal("11", added[1]);
        Assert.Equal("4", added[3]);
        Assert.Equal("", added[14]);
        Assert.Equal(3, lines.Length);
        Assert.Contains("actions=connect|slot", first);
        Assert.Contains("dry=on", first);
        Assert.Contains("day=1017 rows=2", second);
        Assert.Equal(2, File.ReadAllLines(LoadLog.CeoLogPath(dir.Path)).Count(line => line.StartsWith("telemetry.written ")));
    }

    [Fact]
    public void Telemetry_a_blank_log_still_writes_one_row()
    {
        using var dir = new TempTelemetry();
        var bar = TopBar.Line(1016, 5m, 6m, 0m, 1, false, 0);
        var health = "Stores supplied unconnected=0  Slots filled emptySlots=0  Plans present missingPlans=0  Last heal healed=0 none";
        var written = Telemetry.Record(dir.Path, bar, health, Controls.Row(autopilot: false, dryRun: false));
        var cells = File.ReadAllLines(Telemetry.CsvPath(dir.Path))[1].Split(',');

        Assert.Equal("1016", cells[0]);
        Assert.Equal("5", cells[1]);
        Assert.Equal("0", cells[7]);
        Assert.Equal("0", cells[12]);
        Assert.Equal("", cells[14]);
        Assert.Contains("autopilot=off", written);
        Assert.Contains("dry=off", written);
        Assert.EndsWith("actions=", written);
    }

    private static void Seed(string localAppData, string body)
    {
        var path = LoadLog.CeoLogPath(localAppData);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, body);
    }

    private sealed class TempTelemetry : IDisposable
    {
        public TempTelemetry()
        {
            Path = Directory.CreateTempSubdirectory("chaotic-telemetry").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
