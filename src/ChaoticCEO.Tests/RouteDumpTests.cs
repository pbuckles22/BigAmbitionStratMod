using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class RouteDumpTests
{
    [Fact]
    public void RoutesWritten_locks_the_delivery()
    {
        using var dir = new TempDump();
        var routes = new[]
        {
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_firstavenue 38",
                "ba:itemname_sodacan",
                1016,
                ""),
        };
        var holds = new[]
        {
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_sodacan", 45),
        };

        var line = RouteDump.Write(dir.Path, routes, holds);

        var routesCsv = File.ReadAllText(RouteDump.RoutesPath(dir.Path)).Replace("\r\n", "\n");
        var holdsCsv = File.ReadAllText(RouteDump.WarehousesPath(dir.Path)).Replace("\r\n", "\n");
        Assert.Equal(
            """
            warehouse,destination,item,day,truck
            ba:street_fourthstreet 46,ba:street_firstavenue 38,ba:itemname_sodacan,1016,
            """.Replace("\r\n", "\n") + "\n",
            routesCsv);
        Assert.Equal(
            """
            warehouse,street,number,item,amount
            W1,ba:street_fourthstreet,46,ba:itemname_sodacan,45
            """.Replace("\r\n", "\n") + "\n",
            holdsCsv);
        Assert.Equal(
            "routes.written version=1.5.6 routes=1 warehouses=1 warehouse=ba:street_fourthstreet 46 destination=ba:street_firstavenue 38 item=ba:itemname_sodacan day=1016",
            line);
        Assert.Equal(line, File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")).TrimEnd('\r', '\n'));
    }

    [Fact]
    public void RoutesWritten_keeps_the_first_plan()
    {
        using var dir = new TempDump();
        IReadOnlyList<DeliveryStop> routes = new DeliveryStop[]
        {
            null!,
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_firstavenue 38",
                "ba:itemname_sodacan",
                1016,
                ""),
            new DeliveryStop(
                "ba:street_firststreet 37",
                "ba:street_fifthavenue 36",
                "ba:itemname_cheapgift",
                1017,
                "ba:vehicle_van"),
        };

        var line = RouteDump.Write(dir.Path, routes, new WarehouseHold[0]);

        var csv = File.ReadAllText(RouteDump.RoutesPath(dir.Path));
        Assert.Equal(
            "routes.written version=1.5.6 routes=2 warehouses=0 warehouse=ba:street_fourthstreet 46 destination=ba:street_firstavenue 38 item=ba:itemname_sodacan day=1016",
            line);
        Assert.Contains("ba:itemname_cheapgift", csv);
        Assert.Contains("ba:vehicle_van", csv);
        Assert.DoesNotContain("day=1017", line);
    }

    [Fact]
    public void RoutesWritten_writes_headers_when_empty()
    {
        using var dir = new TempDump();

        var line = RouteDump.Write(dir.Path, new DeliveryStop[0], new WarehouseHold[0]);

        Assert.Equal(
            "warehouse,destination,item,day,truck\n",
            File.ReadAllText(RouteDump.RoutesPath(dir.Path)).Replace("\r\n", "\n"));
        Assert.Equal(
            "warehouse,street,number,item,amount\n",
            File.ReadAllText(RouteDump.WarehousesPath(dir.Path)).Replace("\r\n", "\n"));
        Assert.Equal(
            "routes.written version=1.5.6 routes=0 warehouses=0 warehouse= destination= item= day=",
            line);
    }

    [Fact]
    public void WarehouseHold_skips_a_placed_shelf()
    {
        var holds = RouteDump.CountHolds(
            "W1",
            "ba:street_fourthstreet",
            46,
            new[]
            {
                "ba:itemname_palletshelf",
                "ba:itemname_ceilinglampwarehouse",
                "ba:itemname_flatbedspawner",
                "ba:itemname_deliveryspot",
            },
            new[] { "ba:itemname_sodacan", "ba:itemname_palletshelf" });

        Assert.Equal(2, holds.Count);
        Assert.Equal("W1", holds[0].Warehouse);
        Assert.Equal("ba:street_fourthstreet", holds[0].Street);
        Assert.Equal(46, holds[0].Number);
        Assert.Equal("ba:itemname_sodacan", holds[0].Item);
        Assert.Equal(1, holds[0].Amount);
        Assert.Equal("ba:itemname_palletshelf", holds[1].Item);
        Assert.Equal(1, holds[1].Amount);
        Assert.DoesNotContain(holds, hold => hold.Item == "ba:itemname_ceilinglampwarehouse");
        Assert.DoesNotContain(holds, hold => hold.Item == "ba:itemname_flatbedspawner");
        Assert.DoesNotContain(holds, hold => hold.Item == "ba:itemname_deliveryspot");
    }

    private sealed class TempDump : IDisposable
    {
        public TempDump()
        {
            Path = Directory.CreateTempSubdirectory("chaotic-routes").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
