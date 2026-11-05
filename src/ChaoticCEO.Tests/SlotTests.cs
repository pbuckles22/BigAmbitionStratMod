using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class SlotTests
{
    [Fact]
    public void Slots_an_empty_slot_is_bought_and_a_filled_slot_is_left()
    {
        var api = GameApi.Live();
        var slots = new[]
        {
            new GarageSlot("W7", 1, filled: false),
            new GarageSlot("W7", 2, filled: true, driver: "driver-kept"),
        };

        var fills = Slots.Fill(api, slots, new[] { "driver-8" });

        var bought = Assert.Single(fills);
        Assert.Equal("W7", bought.Warehouse);
        Assert.Equal(1, bought.Index);
        Assert.Equal("Buildings.ContractVehicleForSale.Order", bought.Order);
        Assert.Equal("Entities.VehicleSlot.AssignVehicle", bought.Assign);
        Assert.Equal("driver-8", bought.Driver);
    }

    [Fact]
    public void Slots_seven_empty_slots_are_bought_when_no_driver_is_free()
    {
        var api = GameApi.Live();
        var slots = new List<GarageSlot>();
        for (var number = 1; number <= 14; number++)
        {
            var warehouse = "W" + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var firstEmpty = number is 7 or 8 or 9 or 13 or 14;
            var secondEmpty = number is 13 or 14;
            slots.Add(new GarageSlot(warehouse, 1, !firstEmpty, firstEmpty ? "" : "kept"));
            slots.Add(new GarageSlot(warehouse, 2, !secondEmpty, secondEmpty ? "" : "kept"));
        }

        var fills = Slots.Fill(api, slots, Array.Empty<string>());

        Assert.Equal(
            new[] { "W7/1", "W8/1", "W9/1", "W13/1", "W13/2", "W14/1", "W14/2" },
            fills.Select(fill => fill.Warehouse + "/" + fill.Index.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray());
        Assert.All(fills, fill =>
        {
            Assert.Equal("Buildings.ContractVehicleForSale.Order", fill.Order);
            Assert.Equal("Entities.VehicleSlot.AssignVehicle", fill.Assign);
            Assert.Equal("", fill.Driver);
        });
    }

    [Fact]
    public void Slots_a_warehouse_with_two_filled_slots_stays()
    {
        var api = GameApi.Live();
        var slots = new[]
        {
            new GarageSlot("W1", 1, filled: true, driver: "driver-1"),
            new GarageSlot("W1", 2, filled: true, driver: "driver-2"),
            new GarageSlot("W13", 1, filled: false),
            new GarageSlot("W13", 2, filled: false),
        };

        var fills = Slots.Fill(api, slots, new[] { "driver-a", "driver-b" });

        Assert.Equal(2, fills.Count);
        Assert.Equal("W13", fills[0].Warehouse);
        Assert.Equal(1, fills[0].Index);
        Assert.Equal("driver-a", fills[0].Driver);
        Assert.Equal("W13", fills[1].Warehouse);
        Assert.Equal(2, fills[1].Index);
        Assert.Equal("driver-b", fills[1].Driver);
        Assert.DoesNotContain(fills, fill => fill.Warehouse == "W1");
    }

    [Fact]
    public void Slots_load_day_roll_needs_refill_recorded_call_slot_fill_locks_order_and_assign()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill
        using var dir = new TempSlots();
        var loaded = LoadLog.StartSession(dir.Path);
        var days = new DayWatch();
        var needs = new NeedsWatch();
        var save = new object();
        var routes = new[] { new DeliveryStop("ba:street_fourthstreet 46", "ba:street_firstavenue 38", "ba:itemname_sodacan", 1016, "") };
        var holds = new[] { new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_sodacan", 45) };
        var statements = new[]
        {
            new FinancialDay(1015, new[] { new StatementRow("ba:street_fourthavenue", 28, 10f, 1f, 1f, 1f, 0f, 0f, 7f) }),
        };

        needs.Tick(dir.Path, save, 1015, new PlayerNeeds(37f, 12f, 64f, false, false));
        days.Tick(dir.Path, 1015, statements, routes, holds);
        var refill = needs.Tick(dir.Path, save, 1016, new PlayerNeeds(100f, 97f, 100f, true, true));
        var rolled = days.Tick(dir.Path, 1016, statements, routes, holds);
        var api = GameApi.Read(
            new[] { new PanelLine("red", "Low", "ba:street_fourthstreet", 46, "Close Low.") },
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var written = new SlotWatch().Tick(
            dir.Path,
            save,
            1016,
            api,
            new[]
            {
                new GarageSlot("W7", 1, filled: false),
                new GarageSlot("W7", 2, filled: true, driver: "driver-kept"),
            },
            new[] { "driver-8" });

        Assert.StartsWith("mod.loaded version=" + LoadLog.Version, loaded);
        Assert.NotNull(refill);
        Assert.Equal(Needs.Full, refill!.Hunger);
        Assert.Equal("day.rolled version=" + LoadLog.Version + " day=1016", rolled);
        Assert.Equal("Buildings.ContractVehicleForSale.Order", api.Call("truck buy"));
        Assert.Equal("Entities.VehicleSlot.AssignVehicle", api.Call("slot assign"));
        var line = Assert.Single(written);
        Assert.Equal(
            "slot.filled version=" + LoadLog.Version
                + " day=1016 warehouse=W7 slot=1 order=Buildings.ContractVehicleForSale.Order assign=Entities.VehicleSlot.AssignVehicle driver=driver-8",
            line);
        Assert.Equal(api.Call("truck buy"), Field(line, "order"));
        Assert.Equal(api.Call("slot assign"), Field(line, "assign"));
        var log = File.ReadAllLines(LoadLog.CeoLogPath(dir.Path));
        Assert.Equal(loaded, log[0]);
        Assert.Equal("day.rolled version=" + LoadLog.Version + " day=1016", log[^2]);
        Assert.Equal(line, log[^1]);
    }

    [Fact]
    public void Slots_load_day_roll_needs_refill_recorded_call_slot_fill_leaves_a_full_warehouse()
    {
        // Chain: load → day roll → needs refill → recorded call → slot fill → full warehouse
        using var dir = new TempSlots();
        LoadLog.StartSession(dir.Path);
        var days = new DayWatch();
        var needs = new NeedsWatch();
        var save = new object();
        var routes = new[] { new DeliveryStop("ba:street_fourthstreet 46", "ba:street_firstavenue 38", "ba:itemname_sodacan", 1016, "") };
        var holds = new[] { new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_sodacan", 45) };
        var statements = new[]
        {
            new FinancialDay(1015, new[] { new StatementRow("ba:street_fourthavenue", 28, 10f, 1f, 1f, 1f, 0f, 0f, 7f) }),
        };

        needs.Tick(dir.Path, save, 1015, new PlayerNeeds(37f, 12f, 64f, false, false));
        days.Tick(dir.Path, 1015, statements, routes, holds);
        var refill = needs.Tick(dir.Path, save, 1016, new PlayerNeeds(100f, 97f, 100f, true, true));
        days.Tick(dir.Path, 1016, statements, routes, holds);
        var api = GameApi.Read(
            new[] { new PanelLine("red", "Low", "ba:street_fourthstreet", 46, "Close Low.") },
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var watch = new SlotWatch();
        var slots = new[]
        {
            new GarageSlot("W1", 1, filled: true, driver: "driver-1"),
            new GarageSlot("W1", 2, filled: true, driver: "driver-2"),
        };
        var written = watch.Tick(dir.Path, save, 1016, api, slots, new[] { "driver-spare" });
        var again = watch.Tick(dir.Path, save, 1017, api, slots, new[] { "driver-spare" });

        Assert.NotNull(refill);
        Assert.Equal(Needs.Full, refill!.Hunger);
        Assert.Equal(
            new[] { "slot.summary version=" + LoadLog.Version + " day=1016 bought=0" },
            written);
        Assert.Empty(again);
        var log = File.ReadAllLines(LoadLog.CeoLogPath(dir.Path));
        Assert.DoesNotContain(log, line => line.StartsWith("slot.filled "));
        Assert.Equal("slot.summary version=" + LoadLog.Version + " day=1016 bought=0", log[^1]);
        Assert.Single(log, line => line.StartsWith("slot.summary "));
    }

    private static string Field(string line, string name)
    {
        var token = name + "=";
        var start = line.IndexOf(token, StringComparison.Ordinal);
        if (start < 0)
        {
            return "";
        }

        start += token.Length;
        var end = line.IndexOf(' ', start);
        return end < 0 ? line.Substring(start) : line.Substring(start, end - start);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "script", "game_build.py")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("repo root not found");
    }

    private sealed class TempSlots : IDisposable
    {
        public TempSlots()
        {
            Path = Directory.CreateTempSubdirectory("chaotic-slots").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
