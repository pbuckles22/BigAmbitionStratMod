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
        Assert.Equal("day.rolled version=1.13.1 day=1014", rolled);
        Assert.Equal(
            new[]
            {
                "csv.written version=1.13.1 rows=1 day=1013 street=ba:street_fourthavenue number=28 profit=50687.4805",
                "routes.written version=1.13.1 routes=1 warehouses=1 warehouse=ba:street_fourthstreet 46 destination=ba:street_firstavenue 38 item=ba:itemname_sodacan day=1016",
                "day.rolled version=1.13.1 day=1014",
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
        Assert.Equal("day.rolled version=1.13.1 day=1013", rolled);
        var lines = File.ReadAllLines(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log"));
        Assert.Equal(3, lines.Length);
        Assert.Equal("day.rolled version=1.13.1 day=1013", lines[2]);
        Assert.StartsWith("csv.written version=1.13.1 rows=1 day=1012 ", lines[0]);
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

        Assert.Equal("day.rolled version=1.13.1 day=1014", first);
        Assert.Equal("day.rolled version=1.13.1 day=1015", second);
        var lines = File.ReadAllLines(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log"));
        Assert.Equal(6, lines.Length);
        Assert.Equal("day.rolled version=1.13.1 day=1014", lines[2]);
        Assert.Equal(
            "csv.written version=1.13.1 rows=1 day=1014 street=ba:street_today number=1 profit=9",
            lines[3]);
        Assert.Equal("day.rolled version=1.13.1 day=1015", lines[5]);
        Assert.Contains("1014,ba:street_today,1,", File.ReadAllText(StatementDump.CsvPath(dir.Path)));
    }

    [Fact]
    public void DayRolled_logs_one_supply_forecast_for_the_selling_salon()
    {
        using var dir = new TempDump();
        var watch = new DayWatch();
        var salon = "ba:street_fourthavenue 28";
        var hair = "ba:itemname_haircareproduct";
        var holds = new[]
        {
            new WarehouseHold("W6", "ba:street_fifthavenue", 43, hair, 40),
            new WarehouseHold("W6", "ba:street_fifthavenue", 43, "ba:itemname_burger", 8),
            new WarehouseHold("W11", "ba:street_twelfthstreet", 1, hair, 15),
        };
        var needs = new[] { new GoodsNeed(salon, hair) };
        var sales = new[] { new DailyUnits(salon, hair, 1014, 4) };

        Assert.Null(watch.Tick(dir.Path, 1014, Days(1013), Array.Empty<DeliveryStop>(), holds, needs, sales));
        var rolled = watch.Tick(dir.Path, 1015, Days(1013), Array.Empty<DeliveryStop>(), holds, needs, sales);
        var again = watch.Tick(dir.Path, 1015, Days(1013), Array.Empty<DeliveryStop>(), holds, needs, sales);

        Assert.Equal("day.rolled version=1.13.1 day=1015", rolled);
        Assert.Null(again);
        var lines = File.ReadAllLines(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log"));
        var forecasts = 0;
        foreach (var line in lines)
        {
            if (line.StartsWith("supply.forecast "))
            {
                forecasts++;
                Assert.Equal(
                    "supply.forecast version=1.13.1 day=1015 store=ba:street_fourthavenue 28 warehouse=W11 item=ba:itemname_haircareproduct reason=noplan buffer=28",
                    line);
            }
        }

        Assert.Equal(1, forecasts);
        Assert.Contains("supply.summary version=1.13.1 day=1015 gaps=1", lines);
        Assert.Contains("supply.move version=1.13.1 day=1015 store=ba:street_fourthavenue 28 warehouse=W11 item=ba:itemname_haircareproduct amount=4 dry=false", lines);
        Assert.Contains("supply.rechecked version=1.13.1 day=1015 store=ba:street_fourthavenue 28 warehouse=W11 item=ba:itemname_haircareproduct reason=short", lines);
        Assert.Contains("snapshot.taken version=1.13.1", File.ReadAllText(SupplyNight.SnapshotPath(dir.Path)));
        Assert.Contains(salon, File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
    }

    [Fact]
    public void DayRolled_day_1015_salon_with_no_unit_burn_is_noplan_on_W11()
    {
        using var dir = new TempDump();
        var watch = new DayWatch();
        var salon = "ba:street_fourthavenue 28";
        var fifth = "ba:street_fifthavenue 36";
        var hair = "ba:itemname_haircareproduct";
        var gift = "ba:itemname_cheapgift";
        var holds = new[]
        {
            new WarehouseHold("W6", "ba:street_fifthavenue", 43, hair, 40),
            new WarehouseHold("W6", "ba:street_fifthavenue", 43, "ba:itemname_burger", 8),
            new WarehouseHold("W11", "ba:street_twelfthstreet", 1, hair, 15),
        };
        var routes = new[]
        {
            new DeliveryStop("ba:street_firststreet 37", fifth, gift, 1016, ""),
        };
        var needs = new[]
        {
            new GoodsNeed(salon, hair),
            new GoodsNeed(fifth, gift),
        };

        Assert.Null(watch.Tick(dir.Path, 1014, Days(1013), routes, holds, needs, Array.Empty<DailyUnits>()));
        var rolled = watch.Tick(dir.Path, 1015, Days(1013), routes, holds, needs, Array.Empty<DailyUnits>());

        Assert.Equal("day.rolled version=1.13.1 day=1015", rolled);
        var lines = File.ReadAllLines(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log"));
        Assert.Contains(
            "supply.forecast version=1.13.1 day=1015 store=ba:street_fourthavenue 28 warehouse=W11 item=ba:itemname_haircareproduct reason=noplan buffer=0",
            lines);
        Assert.DoesNotContain(lines, line => line.StartsWith("supply.forecast ") && line.Contains(fifth));
        Assert.Contains(fifth, File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.Contains(salon, File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.Contains(
            "supply.move version=1.13.1 day=1015 store=ba:street_fourthavenue 28 warehouse=W11 item=ba:itemname_haircareproduct amount=1 dry=false",
            lines);
    }

    [Fact]
    public void DayRolled_dump_routes_forecast_snapshot_connect_forecast_arms_the_salon()
    {
        // Chain: dump → routes → forecast → snapshot → connect → forecast
        using var dir = new TempDump();
        var watch = new DayWatch();
        var salon = "ba:street_fourthavenue 28";
        var fifth = "ba:street_fifthavenue 36";
        var hair = "ba:itemname_haircareproduct";
        var gift = "ba:itemname_cheapgift";
        var rival = "ba:street_eighthavenue 3";
        var clothes = "ba:street_fifthavenue 54";
        var needs = new List<GoodsNeed>();
        var burn = new List<DailyUnits>();
        Supply.FromBuilding(
            needs,
            burn,
            new StoreRecord("Chaotic Clothing #5", true, false, null, "ba:businesstype_clothingstore", clothes),
            new[]
            {
                "ba:itemname_classiccheapfemaleclothing",
                "ba:itemname_classiccheapmaleclothing",
                "ba:itemname_moderncheapfemaleclothing",
            },
            new[] { new SaleTick("ba:itemname_classiccheapfemaleclothing", 1014, 7) });
        Supply.FromBuilding(
            needs,
            burn,
            new StoreRecord("Rival Salon", false, false, "rival_4", "ba:businesstype_hairdresser", rival),
            new[] { hair },
            new[] { new SaleTick(hair, 1014, 9) });
        Supply.FromBuilding(
            needs,
            burn,
            new StoreRecord("Chaotic Hair #9", true, false, null, "ba:businesstype_hairdresser", salon),
            new[] { hair },
            new[] { new SaleTick(hair, 1014, 4), new SaleTick(hair, 1008, 1) });
        Supply.FromBuilding(
            needs,
            burn,
            new StoreRecord("Chaotic Gifts #1", false, true, "", "ba:businesstype_giftshop", fifth),
            new[] { gift },
            new[] { new SaleTick(gift, 1014, 4) });
        var routes = new[]
        {
            new DeliveryStop("ba:street_firststreet 37", fifth, gift, 1016, ""),
        };
        var holds = new[]
        {
            new WarehouseHold("W6", "ba:street_fifthavenue", 43, hair, 40),
            new WarehouseHold("W6", "ba:street_fifthavenue", 43, "ba:itemname_burger", 8),
            new WarehouseHold("W11", "ba:street_twelfthstreet", 1, hair, 15),
            new WarehouseHold("W2", "ba:street_gift", 2, gift, 3),
        };

        Assert.Null(watch.Tick(dir.Path, 1014, Days(1013), routes, holds, needs, burn));
        var rolled = watch.Tick(dir.Path, 1015, Days(1013), routes, holds, needs, burn);

        Assert.Equal("day.rolled version=" + LoadLog.Version + " day=1015", rolled);
        var lines = File.ReadAllLines(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log"));
        Assert.Contains(
            "supply.forecast version=" + LoadLog.Version + " day=1015 store=ba:street_fourthavenue 28 warehouse=W11 item=ba:itemname_haircareproduct reason=noplan buffer=28",
            lines);
        Assert.DoesNotContain(lines, line => line.StartsWith("supply.forecast ") && line.Contains(fifth));
        Assert.DoesNotContain(lines, line => line.Contains(rival));
        Assert.Equal(3, lines.Count(line => line.StartsWith("supply.forecast ") && line.Contains(clothes) && line.Contains(" warehouse= ")));
        Assert.Contains(
            "supply.move version=" + LoadLog.Version + " day=1015 store=ba:street_fourthavenue 28 warehouse=W11 item=ba:itemname_haircareproduct amount=4 dry=false",
            lines);
        Assert.DoesNotContain(lines, line => line.StartsWith("supply.move ") && line.Contains(" warehouse= "));
        Assert.Contains(
            "supply.rechecked version=" + LoadLog.Version + " day=1015 store=ba:street_fourthavenue 28 warehouse=W11 item=ba:itemname_haircareproduct reason=short",
            lines);
        var written = File.ReadAllText(RouteDump.RoutesPath(dir.Path));
        Assert.Contains(fifth, written);
        Assert.Contains(salon, written);
        Assert.Contains(gift, written);
        Assert.DoesNotContain(clothes, written);
        var shot = File.ReadAllText(SupplyNight.SnapshotPath(dir.Path));
        Assert.Contains("snapshot.taken version=" + LoadLog.Version, shot);
        Assert.Contains(fifth, shot);
        Assert.DoesNotContain(salon, shot);
    }

    [Fact]
    public void DayRolled_dump_routes_forecast_snapshot_connect_forecast_arms_a_second_salon()
    {
        // Chain: dump → routes → forecast → snapshot → connect → forecast
        using var dir = new TempDump();
        var watch = new DayWatch();
        var salon = "ba:street_fourthavenue 28";
        var other = "ba:street_sixthavenue 12";
        var fifth = "ba:street_fifthavenue 36";
        var hair = "ba:itemname_haircareproduct";
        var gift = "ba:itemname_cheapgift";
        var needs = new List<GoodsNeed>();
        var burn = new List<DailyUnits>();
        Supply.FromStore(
            needs,
            burn,
            "ba:businesstype_hairdresser",
            salon,
            new[] { hair },
            new[] { new SaleTick(hair, 1014, 4) });
        Supply.FromStore(
            needs,
            burn,
            "ba:businesstype_hairdresser",
            other,
            Array.Empty<string>(),
            new[] { new SaleTick(hair, 1014, 6) });
        Supply.FromStore(
            needs,
            burn,
            "ba:businesstype_giftshop",
            fifth,
            new[] { gift },
            new[] { new SaleTick(gift, 1014, 4) });
        var routes = new[]
        {
            new DeliveryStop("ba:street_firststreet 37", fifth, gift, 1016, ""),
        };
        var holds = new[]
        {
            new WarehouseHold("W11", "ba:street_twelfthstreet", 1, hair, 15),
            new WarehouseHold("W2", "ba:street_gift", 2, gift, 3),
        };

        Assert.Null(watch.Tick(dir.Path, 1014, Days(1013), routes, holds, needs, burn));
        var rolled = watch.Tick(dir.Path, 1015, Days(1013), routes, holds, needs, burn);

        Assert.Equal("day.rolled version=" + LoadLog.Version + " day=1015", rolled);
        var lines = File.ReadAllLines(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log"));
        Assert.Contains(
            "supply.forecast version=" + LoadLog.Version + " day=1015 store=ba:street_sixthavenue 12 warehouse=W11 item=ba:itemname_haircareproduct reason=noplan buffer=42",
            lines);
        Assert.Contains(
            "supply.move version=" + LoadLog.Version + " day=1015 store=ba:street_sixthavenue 12 warehouse=W11 item=ba:itemname_haircareproduct amount=6 dry=false",
            lines);
        Assert.Contains(
            "supply.rechecked version=" + LoadLog.Version + " day=1015 store=ba:street_sixthavenue 12 warehouse=W11 item=ba:itemname_haircareproduct reason=short",
            lines);
        Assert.Equal(2, lines.Count(line => line.StartsWith("supply.forecast ")));
        var written = File.ReadAllText(RouteDump.RoutesPath(dir.Path));
        Assert.Contains(other, written);
        Assert.Contains(fifth, written);
        Assert.DoesNotContain(lines, line => line.StartsWith("supply.move ") && line.Contains(fifth));
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
