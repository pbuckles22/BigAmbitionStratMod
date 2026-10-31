using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class DayRollTests
{
    [Fact]
    public void ShouldDump_the_first_sight_is_not_a_change()
    {
        Assert.False(DayRoll.ShouldDump(null, 1013));
        Assert.False(DayRoll.ShouldDump(1013, 1013));
        Assert.True(DayRoll.ShouldDump(1013, 1014));
    }

    [Fact]
    public void DayRolled_defers_capture_until_the_day_changes()
    {
        var watch = new DayWatch();

        Assert.False(watch.Due(1013));
        Assert.False(watch.Due(1013));
        Assert.True(watch.Due(1014));
        Assert.True(watch.Due(1014));
    }

    [Fact]
    public void DayRolled_locks_the_new_day()
    {
        using var dir = new TempDump();
        var watch = new DayWatch();

        var armed = watch.Tick(dir.Path, 1013, Days(1013), Routes(), Holds());
        var rolled = watch.Tick(dir.Path, 1014, Days(1013), Routes(), Holds());
        var again = watch.Tick(dir.Path, 1014, Days(1013), Routes(), Holds());

        Assert.Null(armed);
        Assert.Null(again);
        Assert.Equal("day.rolled version=1.7.2 day=1014", rolled);
        Assert.Equal(
            new[]
            {
                "csv.written version=1.7.2 rows=1 day=1013 street=ba:street_fourthavenue number=28 profit=50687.4805",
                "routes.written version=1.7.2 routes=1 warehouses=1 warehouse=ba:street_fourthstreet 46 destination=ba:street_firstavenue 38 item=ba:itemname_sodacan day=1016",
                "day.rolled version=1.7.2 day=1014",
            },
            File.ReadAllLines(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")));
        Assert.Contains("1013,ba:street_fourthavenue,28,", File.ReadAllText(StatementDump.CsvPath(dir.Path)));
        Assert.DoesNotContain("1014,ba:street", File.ReadAllText(StatementDump.CsvPath(dir.Path)));
        Assert.Contains("ba:street_firstavenue 38", File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.Contains(",45", File.ReadAllText(RouteDump.WarehousesPath(dir.Path)));
    }

    [Fact]
    public void DayRolled_a_lower_day_still_dumps_once()
    {
        using var dir = new TempDump();
        var watch = new DayWatch();

        var armed = watch.Tick(dir.Path, 1014, Days(1012), Routes(), Holds());
        var rolled = watch.Tick(dir.Path, 1013, Days(1012), Routes(), Holds());
        var again = watch.Tick(dir.Path, 1013, Days(1012), Routes(), Holds());

        Assert.Null(armed);
        Assert.Null(again);
        Assert.Equal("day.rolled version=1.7.2 day=1013", rolled);
        var lines = File.ReadAllLines(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log"));
        Assert.Equal(3, lines.Length);
        Assert.Equal("day.rolled version=1.7.2 day=1013", lines[2]);
        Assert.StartsWith("csv.written version=1.7.2 rows=1 day=1012 ", lines[0]);
    }

    [Fact]
    public void DayRolled_dumps_again_on_the_next_day()
    {
        using var dir = new TempDump();
        var watch = new DayWatch();
        var days = Days(1013);
        var routes = Routes();
        var holds = Holds();

        Assert.Null(watch.Tick(dir.Path, 1013, days, routes, holds));
        var first = watch.Tick(dir.Path, 1014, days, routes, holds);
        var second = watch.Tick(dir.Path, 1015, days, routes, holds);

        Assert.Equal("day.rolled version=1.7.2 day=1014", first);
        Assert.Equal("day.rolled version=1.7.2 day=1015", second);
        var lines = File.ReadAllLines(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log"));
        Assert.Equal(6, lines.Length);
        Assert.Equal("day.rolled version=1.7.2 day=1014", lines[2]);
        Assert.Equal(
            "csv.written version=1.7.2 rows=1 day=1014 street=ba:street_today number=1 profit=9",
            lines[3]);
        Assert.Equal("day.rolled version=1.7.2 day=1015", lines[5]);
        Assert.Contains("1014,ba:street_today,1,", File.ReadAllText(StatementDump.CsvPath(dir.Path)));
    }

    private static FinancialDay[] Days(int day)
    {
        return new[]
        {
            new FinancialDay(
                day,
                new[]
                {
                    new StatementRow(
                        "ba:street_fourthavenue",
                        28,
                        65477f,
                        3897.2825f,
                        9793.2383f,
                        1099f,
                        0f,
                        0f,
                        50687.4805f),
                }),
            new FinancialDay(
                day + 1,
                new[] { new StatementRow("ba:street_today", 1, 9f, 0f, 0f, 0f, 0f, 0f, 9f) }),
        };
    }

    private static DeliveryStop[] Routes()
    {
        return new[]
        {
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_firstavenue 38",
                "ba:itemname_sodacan",
                1016,
                ""),
        };
    }

    private static WarehouseHold[] Holds()
    {
        return new[]
        {
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_sodacan", 45),
        };
    }

    private sealed class TempDump : IDisposable
    {
        public TempDump()
        {
            Path = Directory.CreateTempSubdirectory("chaotic-day").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
