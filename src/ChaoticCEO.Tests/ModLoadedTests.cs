using System.Diagnostics;
using System.Reflection;
using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class ModLoadedTests
{
    [Fact]
    public void ModLoaded_locks_the_game_build()
    {
        using var dir = new TempLog();
        var path = Path.Combine(dir.Path, "nested", "ceo.log");

        var line = LoadLog.Record(path);

        Assert.Equal("mod.loaded version=1.11.8.1 build=3682", line);
        Assert.Equal(line, File.ReadAllText(path).TrimEnd('\r', '\n'));
    }

    [Fact]
    public void ModLoaded_a_different_build_is_not_3682()
    {
        using var dir = new TempLog();
        var path = Path.Combine(dir.Path, "ceo.log");

        var line = LoadLog.Record(path, "1000");

        Assert.Equal("mod.loaded version=1.11.8.1 build=1000", line);
        Assert.DoesNotContain("3682", File.ReadAllText(path));
    }

    [Fact]
    public void ModLoaded_appends_each_load()
    {
        using var dir = new TempLog();
        var path = Path.Combine(dir.Path, "ceo.log");

        LoadLog.Record(path, "1");
        LoadLog.Record(path, "2");

        Assert.Equal(
            new[]
            {
                "mod.loaded version=1.11.8.1 build=1",
                "mod.loaded version=1.11.8.1 build=2",
            },
            File.ReadAllLines(path));
    }

    [Fact]
    public void CeoLog_lands_in_ChaoticCEO()
    {
        var path = LoadLog.CeoLogPath(@"C:\Users\x\AppData\Local");

        Assert.Equal(
            Path.Combine(@"C:\Users\x\AppData\Local", "ChaoticCEO", "ceo.log"),
            path);
    }

    [Fact]
    public void ModLoaded_writes_the_pinned_build_under_ChaoticCEO()
    {
        using var dir = new TempLog();

        var line = LoadLog.RecordPinned(dir.Path);

        var path = Path.Combine(dir.Path, "ChaoticCEO", "ceo.log");
        Assert.Equal("mod.loaded version=1.11.8.1 build=3682", line);
        Assert.Equal(line, File.ReadAllText(path).TrimEnd('\r', '\n'));
    }

    [Fact]
    public void Compile_stamps_the_informational_version()
    {
        var product = FileVersionInfo.GetVersionInfo(typeof(LoadLog).Assembly.Location).ProductVersion;

        Assert.Matches("^" + LoadLog.Version.Replace(".", "\\.") + @"\+\d+$", product);
    }

    [Fact]
    public void Install_copies_the_built_dll()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = Path.Combine(
            local.Replace(@"\Local", @"\LocalLow"),
            "Hovgaard Games",
            "Big Ambitions",
            "ModsLocal");
        var installed = LoadLog.InstallDll(root);

        Assert.Equal(File.ReadAllBytes(typeof(LoadLog).Assembly.Location), File.ReadAllBytes(installed));
    }

    [Fact]
    public void Install_names_the_ChaoticCEO_dll()
    {
        var path = LoadLog.InstallDll(@"D:\ModsLocal");

        Assert.Equal(
            Path.Combine(@"D:\ModsLocal", "ChaoticCEO", "ChaoticCEO.dll"),
            path);
    }

    [Fact]
    public void Dll_pin_loaded_locks_the_build_on_the_log_line()
    {
        // Chain: dll → pin → loaded
        var pin = PinDlls();
        using var dir = new TempLog();
        var path = Path.Combine(dir.Path, "ceo.log");

        var line = LoadLog.Record(path, pin.Build);

        Assert.Equal(LoadLog.PinnedBuild, pin.Build);
        Assert.Equal(pin.Build, pin.ShippedBuild);
        Assert.Equal(LoadLog.Line(pin.Build), line);
        Assert.Equal(line, File.ReadAllText(path).TrimEnd('\r', '\n'));
    }

    [Fact]
    public void Dll_pin_loaded_keeps_the_build_off_the_dll_hash()
    {
        // Chain: dll → pin → loaded distinct
        var pin = PinDlls();
        using var dir = new TempLog();
        var path = Path.Combine(dir.Path, "ceo.log");

        LoadLog.Record(path, pin.Build);
        var recorded = File.ReadAllText(path);

        Assert.Contains($"build={pin.Build}", recorded);
        Assert.NotEqual(pin.DllHash, pin.ApiHash);
        Assert.DoesNotContain(pin.DllHash, recorded);
        Assert.DoesNotContain(pin.ApiHash, recorded);
    }

    [Fact]
    public void Dll_pin_loaded_records_the_default_build()
    {
        // Chain: dll → pin → loaded default
        var pin = PinDlls();
        using var dir = new TempLog();

        var line = LoadLog.RecordPinned(dir.Path);
        var recorded = File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log"));

        Assert.Equal(pin.ShippedBuild, pin.Build);
        Assert.Contains("build=" + pin.Build, line);
        Assert.Contains("build=" + pin.Build, recorded);
        Assert.DoesNotContain(pin.DllHash, recorded);
    }

    [Fact]
    public void Dll_pin_loaded_csv_locks_csv_written()
    {
        // Chain: dll → pin → loaded → csv
        var pin = PinDlls();
        using var dir = new TempLog();

        var loaded = LoadLog.RecordPinned(dir.Path);
        var written = StatementDump.Write(
            dir.Path,
            1014,
            new[]
            {
                new FinancialDay(
                    1014,
                    new[] { new StatementRow("ba:street_today", 1, 9f, 0f, 0f, 0f, 0f, 0f, 9f) }),
                new FinancialDay(
                    1013,
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
            });

        var log = File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log"));
        Assert.Equal(LoadLog.Line(pin.Build), loaded);
        Assert.Contains(loaded, log);
        Assert.Equal(
            "csv.written version=1.11.8.1 rows=1 day=1013 street=ba:street_fourthavenue number=28 profit=50687.4805",
            written);
        Assert.Contains(written, log);
        Assert.DoesNotContain(pin.DllHash, log);
        Assert.DoesNotContain("ba:street_today", File.ReadAllText(StatementDump.CsvPath(dir.Path)));
    }

    [Fact]
    public void Dll_pin_loaded_csv_locks_an_empty_yesterday()
    {
        // Chain: dll → pin → loaded → csv empty
        var pin = PinDlls();
        using var dir = new TempLog();

        var loaded = LoadLog.RecordPinned(dir.Path);
        var written = StatementDump.Write(
            dir.Path,
            1014,
            new[] { new FinancialDay(1014, new[] { new StatementRow("ba:street_today", 1, 9f, 0f, 0f, 0f, 0f, 0f, 9f) }) });

        var log = File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log"));
        var csv = File.ReadAllText(StatementDump.CsvPath(dir.Path)).Replace("\r\n", "\n");
        Assert.Equal(LoadLog.Line(pin.Build), loaded);
        Assert.StartsWith(loaded, log.TrimStart('\uFEFF'));
        Assert.Equal("csv.written version=1.11.8.1 rows=0 day=1013 street= number= profit=", written);
        Assert.Contains(written, log);
        Assert.Equal("day,street,number,sales,goods,wages,rent,marketing,theft,profit\n", csv);
        Assert.DoesNotContain(pin.DllHash, log);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_locks_routes_written()
    {
        // Chain: dll → pin → loaded → csv → routes
        var pin = PinDlls();
        using var dir = new TempLog();

        var loaded = LoadLog.RecordPinned(dir.Path);
        var written = StatementDump.Write(
            dir.Path,
            1014,
            new[]
            {
                new FinancialDay(
                    1013,
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
            });
        var routes = RouteDump.Write(
            dir.Path,
            new[]
            {
                new DeliveryStop(
                    "ba:street_fourthstreet 46",
                    "ba:street_firstavenue 38",
                    "ba:itemname_sodacan",
                    1016,
                    ""),
            },
            new[]
            {
                new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_sodacan", 45),
            });

        var log = File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")).Replace("\r\n", "\n");
        Assert.Equal(LoadLog.Line(pin.Build), loaded);
        Assert.Equal(
            loaded + "\n" + written + "\n" + routes + "\n",
            log);
        Assert.Equal(
            "routes.written version=1.11.8.1 routes=1 warehouses=1 warehouse=ba:street_fourthstreet 46 destination=ba:street_firstavenue 38 item=ba:itemname_sodacan day=1016",
            routes);
        Assert.Contains("50687.4805", File.ReadAllText(StatementDump.CsvPath(dir.Path)));
        Assert.Contains("ba:street_firstavenue 38", File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.Contains("45", File.ReadAllText(RouteDump.WarehousesPath(dir.Path)));
        Assert.DoesNotContain(pin.DllHash, log);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_locks_an_empty_delivery()
    {
        // Chain: dll → pin → loaded → csv → routes empty
        var pin = PinDlls();
        using var dir = new TempLog();

        var loaded = LoadLog.RecordPinned(dir.Path);
        var written = StatementDump.Write(
            dir.Path,
            1014,
            new[]
            {
                new FinancialDay(
                    1013,
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
            });
        var routes = RouteDump.Write(dir.Path, new DeliveryStop[0], new WarehouseHold[0]);

        var log = File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")).Replace("\r\n", "\n");
        Assert.Equal(LoadLog.Line(pin.Build), loaded);
        Assert.Equal(loaded + "\n" + written + "\n" + routes + "\n", log);
        Assert.Equal(
            "routes.written version=1.11.8.1 routes=0 warehouses=0 warehouse= destination= item= day=",
            routes);
        Assert.Contains(written, log);
        Assert.Equal(
            "warehouse,destination,item,day,truck\n",
            File.ReadAllText(RouteDump.RoutesPath(dir.Path)).Replace("\r\n", "\n"));
        Assert.Contains("50687.4805", File.ReadAllText(StatementDump.CsvPath(dir.Path)));
        Assert.DoesNotContain(pin.DllHash, log);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_skips_a_placed_shelf()
    {
        // Chain: dll → pin → loaded → csv → routes shelf
        var pin = PinDlls();
        using var dir = new TempLog();

        var loaded = LoadLog.RecordPinned(dir.Path);
        var written = StatementDump.Write(
            dir.Path,
            1014,
            new[]
            {
                new FinancialDay(
                    1013,
                    new[]
                    {
                        new StatementRow(
                            "ba:street_fourthavenue",
                            28,
                            10f,
                            0f,
                            0f,
                            0f,
                            0f,
                            0f,
                            10f),
                    }),
            });
        var holds = RouteDump.CountHolds(
            "W1",
            "ba:street_fourthstreet",
            46,
            new[] { "ba:itemname_ceilinglampwarehouse", "ba:itemname_palletshelf" },
            new[] { "ba:itemname_sodacan" });
        var routes = RouteDump.Write(
            dir.Path,
            new[]
            {
                new DeliveryStop(
                    "ba:street_firststreet 37",
                    "ba:street_fifthavenue 36",
                    "ba:itemname_cheapgift",
                    1016,
                    ""),
            },
            holds);

        var log = File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")).Replace("\r\n", "\n");
        var warehouses = File.ReadAllText(RouteDump.WarehousesPath(dir.Path));
        Assert.Equal(LoadLog.Line(pin.Build), loaded);
        Assert.Equal(loaded + "\n" + written + "\n" + routes + "\n", log);
        Assert.Contains("W1,ba:street_fourthstreet,46,ba:itemname_sodacan,1", warehouses.Replace("\r\n", "\n"));
        Assert.DoesNotContain("ba:itemname_ceilinglampwarehouse", warehouses);
        Assert.DoesNotContain("ba:itemname_palletshelf", warehouses);
        Assert.Contains("ba:itemname_cheapgift", File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.DoesNotContain(pin.DllHash, log);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_locks_day_rolled()
    {
        // Chain: dll → pin → loaded → csv → routes → day
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = ChainStops();
        var holds = ChainHolds();

        var loaded = LoadLog.RecordPinned(dir.Path);
        var written = StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        var armed = watch.Tick(dir.Path, 1013, days, stops, holds);
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);

        var log = File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")).Replace("\r\n", "\n");
        Assert.Null(armed);
        Assert.Equal(LoadLog.Line(pin.Build), loaded);
        Assert.Equal("day.rolled version=1.11.8.1 day=1014", rolled);
        Assert.Equal(
            loaded + "\n" + written + "\n" + routes + "\n" + written + "\n" + routes + "\n" + rolled + "\n",
            log);
        Assert.Contains("50687.4805", File.ReadAllText(StatementDump.CsvPath(dir.Path)));
        Assert.Contains("ba:street_firstavenue 38", File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.DoesNotContain(pin.DllHash, log);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_locks_an_empty_rollover()
    {
        // Chain: dll → pin → loaded → csv → routes → day empty
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();

        var loaded = LoadLog.RecordPinned(dir.Path);
        var written = StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, new DeliveryStop[0], new WarehouseHold[0]);
        var watch = new DayWatch();
        var armed = watch.Tick(dir.Path, 1013, days, new DeliveryStop[0], new WarehouseHold[0]);
        var rolled = watch.Tick(dir.Path, 1014, days, new DeliveryStop[0], new WarehouseHold[0]);

        var log = File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")).Replace("\r\n", "\n");
        Assert.Null(armed);
        Assert.Equal(LoadLog.Line(pin.Build), loaded);
        Assert.Equal("day.rolled version=1.11.8.1 day=1014", rolled);
        Assert.Equal(
            loaded + "\n" + written + "\n" + routes + "\n" + written + "\n" + routes + "\n" + rolled + "\n",
            log);
        Assert.Equal(
            "routes.written version=1.11.8.1 routes=0 warehouses=0 warehouse= destination= item= day=",
            routes);
        Assert.Contains("50687.4805", File.ReadAllText(StatementDump.CsvPath(dir.Path)));
        Assert.Equal(
            "warehouse,destination,item,day,truck\n",
            File.ReadAllText(RouteDump.RoutesPath(dir.Path)).Replace("\r\n", "\n"));
        Assert.DoesNotContain(pin.DllHash, log);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_locks_the_following_day()
    {
        // Chain: dll → pin → loaded → csv → routes → day → next
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = new[]
        {
            new FinancialDay(
                1013,
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
                1014,
                new[] { new StatementRow("ba:street_today", 1, 9f, 0f, 0f, 0f, 0f, 0f, 9f) }),
        };
        var stops = ChainStops();
        var holds = ChainHolds();

        var loaded = LoadLog.RecordPinned(dir.Path);
        var written = StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var next = watch.Tick(dir.Path, 1015, days, stops, holds);

        var log = File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")).Replace("\r\n", "\n");
        Assert.Equal(LoadLog.Line(pin.Build), loaded);
        Assert.Equal("day.rolled version=1.11.8.1 day=1014", rolled);
        Assert.Equal("day.rolled version=1.11.8.1 day=1015", next);
        Assert.Contains(loaded + "\n" + written + "\n" + routes + "\n", log);
        Assert.EndsWith(rolled + "\n" + "csv.written version=1.11.8.1 rows=1 day=1014 street=ba:street_today number=1 profit=9" + "\n" + routes + "\n" + next + "\n", log);
        Assert.Equal(2, log.Split(new[] { "day.rolled" }, StringSplitOptions.None).Length - 1);
        Assert.Contains("1014,ba:street_today,1,", File.ReadAllText(StatementDump.CsvPath(dir.Path)));
        Assert.DoesNotContain(pin.DllHash, log);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_locks_the_bottom()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = ChainStops();
        var holds = ChainHolds();

        var loaded = LoadLog.RecordPinned(dir.Path);
        var written = StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        var armed = watch.Tick(dir.Path, 1013, days, stops, holds);
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var candidates = Advisor.CloseCandidatesFile(StatementDump.CsvPath(dir.Path));
        var mixes = Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path));

        var bottom = ranked[ranked.Count - 1];
        Assert.Null(armed);
        Assert.Equal(LoadLog.Line(pin.Build), loaded);
        Assert.Equal("day.rolled version=1.11.8.1 day=1014", rolled);
        Assert.Equal(
            "csv.written version=1.11.8.1 rows=1 day=1013 street=ba:street_fourthavenue number=28 profit=50687.4805",
            written);
        Assert.Contains("ba:street_firstavenue 38", File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.Equal("ba:street_fourthavenue", bottom.Street);
        Assert.Equal(28, bottom.Number);
        Assert.Equal(50687.4805, bottom.Profit, 4);
        Assert.Equal(bottom.Street, candidates[candidates.Count - 1].Street);
        Assert.Equal(bottom.Number, candidates[candidates.Count - 1].Number);
        Assert.Equal(50687.4805, candidates[candidates.Count - 1].Profit, 4);
        Assert.Empty(mixes);
        Assert.Contains(routes, File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")));
        Assert.DoesNotContain(pin.DllHash, File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_locks_a_family_mix()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank mix
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = new[]
        {
            new FinancialDay(
                1013,
                new[]
                {
                    new StatementRow("ba:street_high", 1, 1f, 0f, 0f, 0f, 0f, 0f, 100f),
                    new StatementRow("ba:street_low", 2, 999f, 0f, 0f, 0f, 0f, 0f, -40f),
                }),
        };
        var holds = RouteDump.CountHolds(
            "W1",
            "ba:street_fourthstreet",
            46,
            new[] { "ba:itemname_ceilinglampwarehouse", "ba:itemname_paperbag" },
            new[] { "ba:itemname_sodacan", "ba:itemname_cheapgift" });
        var soda = RouteDump.CountHolds(
            "W6",
            "ba:street_fifthavenue",
            43,
            Array.Empty<string>(),
            new[] { "ba:itemname_sodacan" });
        var stock = new List<WarehouseHold>(holds);
        stock.AddRange(soda);

        var loaded = LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        RouteDump.Write(dir.Path, ChainStops(), stock);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, ChainStops(), stock));
        var rolled = watch.Tick(dir.Path, 1014, days, ChainStops(), stock);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var mixes = Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path));
        var warehouses = File.ReadAllText(RouteDump.WarehousesPath(dir.Path));

        var bottom = ranked[ranked.Count - 1];
        Assert.Equal(LoadLog.Line(pin.Build), loaded);
        Assert.Equal("day.rolled version=1.11.8.1 day=1014", rolled);
        Assert.Equal(new[] { "ba:street_high", "ba:street_low" }, ranked.Select(row => row.Street));
        Assert.Equal(2, bottom.Number);
        Assert.Equal(-40d, bottom.Profit);
        Assert.Equal(new[] { "W1" }, mixes.Select(row => row.Warehouse));
        Assert.Equal("food, specialty", mixes[0].Families);
        Assert.Equal("ba:street_fourthstreet", mixes[0].Street);
        Assert.Equal(46, mixes[0].Number);
        Assert.DoesNotContain("ba:itemname_ceilinglampwarehouse", warehouses);
        Assert.DoesNotContain("ba:itemname_paperbag", warehouses);
        Assert.Contains("ba:itemname_sodacan", warehouses);
        Assert.DoesNotContain(pin.DllHash, File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_locks_an_empty_rollover()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank empty
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();

        var loaded = LoadLog.RecordPinned(dir.Path);
        var written = StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, new DeliveryStop[0], new WarehouseHold[0]);
        var watch = new DayWatch();
        var armed = watch.Tick(dir.Path, 1013, days, new DeliveryStop[0], new WarehouseHold[0]);
        var rolled = watch.Tick(dir.Path, 1014, days, new DeliveryStop[0], new WarehouseHold[0]);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var mixes = Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path));

        Assert.Null(armed);
        Assert.Equal(LoadLog.Line(pin.Build), loaded);
        Assert.Equal("day.rolled version=1.11.8.1 day=1014", rolled);
        Assert.Equal(
            "routes.written version=1.11.8.1 routes=0 warehouses=0 warehouse= destination= item= day=",
            routes);
        Assert.Contains("50687.4805", File.ReadAllText(StatementDump.CsvPath(dir.Path)));
        Assert.Equal("ba:street_fourthavenue", ranked[ranked.Count - 1].Street);
        Assert.Equal(28, ranked[ranked.Count - 1].Number);
        Assert.Empty(mixes);
        Assert.Contains(written, File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")));
        Assert.DoesNotContain(pin.DllHash, File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_locks_the_red_row()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = ChainStops();
        var holds = ChainHolds();

        var loaded = LoadLog.RecordPinned(dir.Path);
        var written = StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        var armed = watch.Tick(dir.Path, 1013, days, stops, holds);
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());

        var red = rows.Where(row => row.Tone == "red").ToArray();
        var bottom = ranked[ranked.Count - 1];
        Assert.Null(armed);
        Assert.Equal(LoadLog.Line(pin.Build), loaded);
        Assert.Equal("day.rolled version=1.11.8.1 day=1014", rolled);
        Assert.Equal(
            "csv.written version=1.11.8.1 rows=1 day=1013 street=ba:street_fourthavenue number=28 profit=50687.4805",
            written);
        Assert.Contains("ba:street_firstavenue 38", File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.Equal(bottom.Street, red[red.Length - 1].Street);
        Assert.Equal(bottom.Number, red[red.Length - 1].Number);
        Assert.Equal(bottom.Profit, red[red.Length - 1].Profit, 4);
        Assert.Equal("ba:street_fourthavenue", red[red.Length - 1].Street);
        Assert.Equal(28, red[red.Length - 1].Number);
        Assert.Equal(50687.4805, red[red.Length - 1].Profit, 4);
        Assert.DoesNotContain(rows, row => row.Tone == "warning");
        Assert.Contains(routes, File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")));
        Assert.DoesNotContain(pin.DllHash, File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_locks_the_mix_warning()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay mix
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = new[]
        {
            new FinancialDay(
                1013,
                new[]
                {
                    new StatementRow("ba:street_high", 1, 1f, 0f, 0f, 0f, 0f, 0f, 100f),
                    new StatementRow("ba:street_low", 2, 999f, 0f, 0f, 0f, 0f, 0f, -40f),
                }),
        };
        var holds = RouteDump.CountHolds(
            "W1",
            "ba:street_fourthstreet",
            46,
            new[] { "ba:itemname_ceilinglampwarehouse", "ba:itemname_paperbag" },
            new[] { "ba:itemname_sodacan", "ba:itemname_cheapgift" });

        var loaded = LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        RouteDump.Write(dir.Path, ChainStops(), holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, ChainStops(), holds));
        var rolled = watch.Tick(dir.Path, 1014, days, ChainStops(), holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());

        var red = rows.Where(row => row.Tone == "red").ToArray();
        var warnings = rows.Where(row => row.Tone == "warning").ToArray();
        var warehouses = File.ReadAllText(RouteDump.WarehousesPath(dir.Path));
        var bottom = ranked[ranked.Count - 1];
        Assert.Equal(LoadLog.Line(pin.Build), loaded);
        Assert.Equal("day.rolled version=1.11.8.1 day=1014", rolled);
        Assert.Equal(bottom.Street, red[red.Length - 1].Street);
        Assert.Equal(bottom.Number, red[red.Length - 1].Number);
        Assert.Equal(bottom.Profit, red[red.Length - 1].Profit);
        Assert.Equal("ba:street_low", red[red.Length - 1].Street);
        Assert.Equal(2, red[red.Length - 1].Number);
        Assert.Equal(-40d, red[red.Length - 1].Profit);
        Assert.Equal(new[] { "W1" }, warnings.Select(row => row.Business));
        Assert.Equal("food, specialty", warnings[0].Detail);
        Assert.Equal("ba:street_fourthstreet", warnings[0].Street);
        Assert.Equal(46, warnings[0].Number);
        Assert.DoesNotContain("ba:itemname_ceilinglampwarehouse", warehouses);
        Assert.DoesNotContain("ba:itemname_paperbag", warehouses);
        Assert.Contains("ba:itemname_sodacan", warehouses);
        Assert.DoesNotContain(pin.DllHash, File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_locks_the_explain_line()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = ChainStops();
        var holds = ChainHolds();

        var loaded = LoadLog.RecordPinned(dir.Path);
        var written = StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        var armed = watch.Tick(dir.Path, 1013, days, stops, holds);
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        var shown = Advice.Record(dir.Path, rows);

        var red = rows.Where(row => row.Tone == "red").ToArray();
        var bottom = ranked[ranked.Count - 1];
        var log = File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log"));
        Assert.Null(armed);
        Assert.Equal(LoadLog.Line(pin.Build), loaded);
        Assert.Equal("day.rolled version=" + LoadLog.Version + " day=1014", rolled);
        Assert.Equal(
            "csv.written version=" + LoadLog.Version + " rows=1 day=1013 street=ba:street_fourthavenue number=28 profit=50687.4805",
            written);
        Assert.Contains("ba:street_firstavenue 38", File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.Equal(bottom.Street, red[red.Length - 1].Street);
        Assert.Equal(bottom.Number, red[red.Length - 1].Number);
        Assert.Equal(bottom.Profit, red[red.Length - 1].Profit, 4);
        Assert.Single(shown);
        Assert.Equal(
            "advice.shown version=" + LoadLog.Version
                + " tone=red business=ba:street_fourthavenue street=ba:street_fourthavenue number=28 explain=Close ba:street_fourthavenue. Profit 50687.4805.",
            shown[0]);
        Assert.Equal("ba:street_fourthavenue", red[red.Length - 1].Business);
        Assert.Contains(shown[0], log);
        Assert.Contains(routes, log);
        Assert.DoesNotContain(pin.DllHash, log);
        Assert.Equal(1, log.Split('\n').Count(line => line.StartsWith("advice.shown ", StringComparison.Ordinal)));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_locks_the_mix_line()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice mix
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = new[]
        {
            new FinancialDay(
                1013,
                new[]
                {
                    new StatementRow("ba:street_high", 1, 1f, 0f, 0f, 0f, 0f, 0f, 100f),
                    new StatementRow("ba:street_low", 2, 999f, 0f, 0f, 0f, 0f, 0f, -40f),
                }),
        };
        var holds = RouteDump.CountHolds(
            "W1",
            "ba:street_fourthstreet",
            46,
            new[] { "ba:itemname_ceilinglampwarehouse", "ba:itemname_paperbag" },
            new[] { "ba:itemname_sodacan", "ba:itemname_cheapgift" });

        var loaded = LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        RouteDump.Write(dir.Path, ChainStops(), holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, ChainStops(), holds));
        var rolled = watch.Tick(dir.Path, 1014, days, ChainStops(), holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        var shown = Advice.Record(dir.Path, rows);

        var red = rows.Where(row => row.Tone == "red").ToArray();
        var warnings = rows.Where(row => row.Tone == "warning").ToArray();
        var log = File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log"));
        Assert.Equal(LoadLog.Line(pin.Build), loaded);
        Assert.Equal("day.rolled version=" + LoadLog.Version + " day=1014", rolled);
        Assert.Equal("ba:street_low", red[red.Length - 1].Street);
        Assert.Equal(-40d, red[red.Length - 1].Profit);
        Assert.Equal(new[] { "W1" }, warnings.Select(row => row.Business));
        Assert.Equal(rows.Count, shown.Count);
        Assert.Contains(
            "advice.shown version=" + LoadLog.Version
                + " tone=red business=ba:street_low street=ba:street_low number=2 explain=Close ba:street_low. Profit -40.",
            shown);
        Assert.Contains(
            "advice.shown version=" + LoadLog.Version
                + " tone=warning business=W1 street=ba:street_fourthstreet number=46 explain=W1 mixes food, specialty.",
            shown);
        Assert.Equal(shown.Count, log.Split('\n').Count(line => line.StartsWith("advice.shown ", StringComparison.Ordinal)));
        Assert.DoesNotContain(pin.DllHash, log);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_locks_the_sold_price()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = ChainStops();
        var holds = ChainHolds();

        var loaded = LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        var armed = watch.Tick(dir.Path, 1013, days, stops, holds);
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        var shown = Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });

        var log = File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log"));
        Assert.Null(armed);
        Assert.Equal(LoadLog.Line(pin.Build), loaded);
        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.Contains(item, File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.NotEmpty(shown);
        Assert.StartsWith("advice.shown version=" + LoadLog.Version + " ", shown[0]);
        Assert.Contains(shown[0], log);
        Assert.Equal(item, price.Item);
        Assert.Equal(2.75f, price.Price);
        Assert.DoesNotContain(pin.DllHash, log);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_keeps_the_route_item()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices route
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = ChainStops();
        var holds = ChainHolds();

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        var shown = Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry("ba:itemname_cheapgift", 9.5f, 100),
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.Contains(item, File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.NotEmpty(shown);
        Assert.Equal(item, price.Item);
        Assert.Equal(2.75f, price.Price);
        Assert.DoesNotContain(pin.DllHash, File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_locks_the_run()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = ChainStops();
        var holds = ChainHolds();

        var loaded = LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        var armed = watch.Tick(dir.Path, 1013, days, stops, holds);
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        var shown = Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);

        var log = File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log"));
        Assert.Null(armed);
        Assert.Equal(LoadLog.Line(pin.Build), loaded);
        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.Contains(item, File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.NotEmpty(shown);
        Assert.StartsWith("advice.shown version=" + LoadLog.Version + " ", shown[0]);
        Assert.Contains(shown[0], log);
        Assert.Equal(item, price.Item);
        Assert.Equal(2.75f, price.Price);
        Assert.Equal("W1", run.Warehouse);
        Assert.Equal("food", run.Family);
        Assert.Equal("ba:street_firstavenue 38", run.Destination);
        Assert.Equal(item, run.Item);
        Assert.Null(run.Day);
        Assert.DoesNotContain(pin.DllHash, log);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_keeps_the_larger_family()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans family
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = new[]
        {
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_firstavenue 38",
                "ba:itemname_sodacan",
                1016,
                ""),
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_fifthavenue 36",
                "ba:itemname_cheapgift",
                1016,
                ""),
        };
        var holds = new[]
        {
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_sodacan", 8),
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_cheapgift", 40),
        };

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        var shown = Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.Contains("ba:itemname_sodacan", File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.Contains("ba:itemname_cheapgift", File.ReadAllText(RouteDump.WarehousesPath(dir.Path)));
        Assert.NotEmpty(shown);
        Assert.Equal("ba:itemname_sodacan", price.Item);
        Assert.Equal(2.75f, price.Price);
        Assert.Equal("specialty", run.Family);
        Assert.Equal("ba:street_fifthavenue 36", run.Destination);
        Assert.Equal("ba:itemname_cheapgift", run.Item);
        Assert.Null(run.Day);
        Assert.DoesNotContain(pin.DllHash, File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_keeps_the_recurring_run()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans recurring
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = new[]
        {
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_fifthavenue 36",
                "ba:itemname_sodacan",
                1016,
                ""),
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_firstavenue 38",
                "ba:itemname_burger",
                null,
                ""),
        };
        var holds = new[]
        {
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_sodacan", 5),
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_burger", 5),
        };

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        var shown = Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.Contains("ba:itemname_burger", File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.NotEmpty(shown);
        Assert.Equal("ba:itemname_sodacan", price.Item);
        Assert.Equal(2.75f, price.Price);
        Assert.Equal("food", run.Family);
        Assert.Equal("ba:street_firstavenue 38", run.Destination);
        Assert.Equal("ba:itemname_burger", run.Item);
        Assert.Null(run.Day);
        Assert.DoesNotContain(pin.DllHash, File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_locks_the_role()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = ChainStops();
        var holds = ChainHolds();

        var loaded = LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        var armed = watch.Tick(dir.Path, 1013, days, stops, holds);
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        var shown = Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-soda", 40),
                new StaffOffer("headhunter", run.Family, "hh-soda", 4),
            });

        var log = File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log"));
        Assert.Null(armed);
        Assert.Equal(LoadLog.Line(pin.Build), loaded);
        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.Contains(item, File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.NotEmpty(shown);
        Assert.Equal(item, price.Item);
        Assert.Equal(2.75f, price.Price);
        Assert.Equal("food", run.Family);
        Assert.Equal(item, run.Item);
        Assert.Null(run.Day);
        Assert.Equal(run.Family, staff.Role);
        Assert.Equal("hr", staff.Kind);
        Assert.Equal("hr-soda", staff.PlanId);
        Assert.Equal(40, staff.Amount);
        Assert.DoesNotContain(pin.DllHash, log);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_keeps_the_plan_family()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing family
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = new[]
        {
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_firstavenue 38",
                "ba:itemname_sodacan",
                1016,
                ""),
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_fifthavenue 36",
                "ba:itemname_cheapgift",
                1016,
                ""),
        };
        var holds = new[]
        {
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_sodacan", 8),
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_cheapgift", 40),
        };

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        var shown = Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", "food", "hr-soda", 100),
                new StaffOffer("headhunter", "specialty", "hh-gift", 4),
            });

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.Contains("ba:itemname_cheapgift", File.ReadAllText(RouteDump.WarehousesPath(dir.Path)));
        Assert.NotEmpty(shown);
        Assert.Equal("ba:itemname_sodacan", price.Item);
        Assert.Equal("specialty", run.Family);
        Assert.Equal("ba:itemname_cheapgift", run.Item);
        Assert.Equal(run.Family, staff.Role);
        Assert.Equal("headhunter", staff.Kind);
        Assert.Equal("hh-gift", staff.PlanId);
        Assert.Equal(4, staff.Amount);
        Assert.DoesNotContain(pin.DllHash, File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_skips_an_empty_plan()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing empty
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = ChainStops();
        var holds = ChainHolds();

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        var shown = Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", "food", "hr-empty", 0),
                new StaffOffer("headhunter", "specialty", "hh-gift", 100),
            });

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.NotEmpty(shown);
        Assert.Equal(item, price.Item);
        Assert.Equal(item, run.Item);
        Assert.Equal("food", run.Family);
        Assert.Equal("food", staff.Role);
        Assert.Equal("", staff.Kind);
        Assert.Equal("", staff.PlanId);
        Assert.Equal(0, staff.Amount);
        Assert.DoesNotContain(pin.DllHash, File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_locks_the_clone()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = ChainStops();
        var holds = ChainHolds();

        var loaded = LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        var armed = watch.Tick(dir.Path, 1013, days, stops, holds);
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        var shown = Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-soda", 40),
                new StaffOffer("headhunter", run.Family, "hh-soda", 4),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", staff.Role, "ba:street_main", 1),
                new PortfolioBusiness("Food 2", staff.Role, "ba:street_main", 2),
                new PortfolioBusiness("Food 3", staff.Role, "ba:street_main", 3),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", staff.Role, 1, "soda-line", 12, false),
                new ChainLayout("ba:street_main", staff.Role, 4, "", 0, true),
            });

        Assert.Null(armed);
        Assert.Equal(LoadLog.Line(pin.Build), loaded);
        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.Contains(item, File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.NotEmpty(shown);
        Assert.Equal(item, price.Item);
        Assert.Equal("food", run.Family);
        Assert.Equal(run.Family, staff.Role);
        Assert.Equal("hr", staff.Kind);
        Assert.Equal(staff.Role, clusters[0].Family);
        Assert.Equal(3, clusters[0].Count);
        Assert.Equal("ba:street_main", clone.Street);
        Assert.Equal(staff.Role, clone.Family);
        Assert.Equal("soda-line", clone.Blueprint);
        Assert.Equal(1, clone.Source);
        Assert.Equal(4, clone.Destination);
        Assert.DoesNotContain(pin.DllHash, File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_keeps_the_plan_family()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout family
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = new[]
        {
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_firstavenue 38",
                "ba:itemname_sodacan",
                1016,
                ""),
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_fifthavenue 36",
                "ba:itemname_cheapgift",
                1016,
                ""),
        };
        var holds = new[]
        {
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_sodacan", 8),
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_cheapgift", 40),
        };

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        var shown = Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", "food", "hr-soda", 100),
                new StaffOffer("headhunter", "specialty", "hh-gift", 4),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", "food", "ba:street_main", 1),
                new PortfolioBusiness("Food 2", "food", "ba:street_main", 2),
                new PortfolioBusiness("Food 3", "food", "ba:street_main", 3),
                new PortfolioBusiness("Gift 1", staff.Role, "ba:street_side", 8),
                new PortfolioBusiness("Gift 2", staff.Role, "ba:street_side", 9),
                new PortfolioBusiness("Gift 3", staff.Role, "ba:street_side", 10),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", "food", 1, "soda-line", 100, false),
                new ChainLayout("ba:street_main", "food", 4, "", 0, true),
                new ChainLayout("ba:street_side", staff.Role, 8, "gift-line", 1, false),
                new ChainLayout("ba:street_side", staff.Role, 11, "", 0, true),
            });

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.NotEmpty(shown);
        Assert.Equal("ba:itemname_sodacan", price.Item);
        Assert.Equal("specialty", run.Family);
        Assert.Equal(run.Family, staff.Role);
        Assert.Equal("headhunter", staff.Kind);
        Assert.Equal("ba:street_side", clone.Street);
        Assert.Equal(staff.Role, clone.Family);
        Assert.Equal("gift-line", clone.Blueprint);
        Assert.Equal(8, clone.Source);
        Assert.Equal(11, clone.Destination);
        Assert.DoesNotContain(pin.DllHash, File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_skips_a_short_cluster()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout short
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = ChainStops();
        var holds = ChainHolds();

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        var shown = Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-empty", 0),
                new StaffOffer("headhunter", "specialty", "hh-gift", 100),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", staff.Role, "ba:street_main", 1),
                new PortfolioBusiness("Food 2", staff.Role, "ba:street_main", 2),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", staff.Role, 1, "soda-line", 12, false),
                new ChainLayout("ba:street_main", staff.Role, 4, "", 0, true),
            });

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.NotEmpty(shown);
        Assert.Equal(item, run.Item);
        Assert.Equal("food", staff.Role);
        Assert.Equal("", staff.Kind);
        Assert.Empty(clusters);
        Assert.Equal("", clone.Blueprint);
        Assert.Equal(0, clone.Source);
        Assert.Equal(0, clone.Destination);
        Assert.DoesNotContain(pin.DllHash, File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_locks_the_close()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = ChainStops();
        var holds = ChainHolds();

        var loaded = LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        var armed = watch.Tick(dir.Path, 1013, days, stops, holds);
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        var shown = Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-soda", 40),
                new StaffOffer("headhunter", run.Family, "hh-soda", 4),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", staff.Role, "ba:street_main", 1),
                new PortfolioBusiness("Food 2", staff.Role, "ba:street_main", 2),
                new PortfolioBusiness("Food 3", staff.Role, "ba:street_main", 3),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", staff.Role, 1, "soda-line", 12, false),
                new ChainLayout("ba:street_main", staff.Role, 4, "", 0, true),
            });
        var closed = Close.Lock(
            clone,
            new[]
            {
                new LedgerDay(clone.Street, 2, clone.Family, 1011, -10f),
                new LedgerDay(clone.Street, 2, clone.Family, 1012, -4f),
                new LedgerDay(clone.Street, 2, clone.Family, 1013, -1f),
            },
            3,
            true,
            true);

        Assert.Null(armed);
        Assert.Equal(LoadLog.Line(pin.Build), loaded);
        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.Contains(item, File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.NotEmpty(shown);
        Assert.Equal(item, price.Item);
        Assert.Equal("food", run.Family);
        Assert.Equal(run.Family, staff.Role);
        Assert.Equal("hr", staff.Kind);
        Assert.Equal("soda-line", clone.Blueprint);
        Assert.Equal(4, clone.Destination);
        Assert.Equal(clone.Street, closed.Street);
        Assert.Equal(clone.Family, closed.Family);
        Assert.Equal(2, closed.Number);
        Assert.True(closed.Closed);
        Assert.DoesNotContain(pin.DllHash, File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_skips_a_short_layout()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close short
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = ChainStops();
        var holds = ChainHolds();

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        var shown = Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-empty", 0),
                new StaffOffer("headhunter", "specialty", "hh-gift", 100),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", staff.Role, "ba:street_main", 1),
                new PortfolioBusiness("Food 2", staff.Role, "ba:street_main", 2),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", staff.Role, 1, "soda-line", 12, false),
                new ChainLayout("ba:street_main", staff.Role, 4, "", 0, true),
            });
        var closed = Close.Lock(
            clone,
            new[]
            {
                new LedgerDay("ba:street_main", 2, staff.Role, 1011, -10f),
                new LedgerDay("ba:street_main", 2, staff.Role, 1012, -4f),
                new LedgerDay("ba:street_main", 2, staff.Role, 1013, -1f),
            },
            3,
            true,
            true);

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.NotEmpty(shown);
        Assert.Equal(item, run.Item);
        Assert.Equal("food", staff.Role);
        Assert.Equal("", clone.Blueprint);
        Assert.Equal(0, clone.Destination);
        Assert.False(closed.Closed);
        Assert.Equal("", closed.Street);
        Assert.Equal(0, closed.Number);
        Assert.DoesNotContain(pin.DllHash, File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_locks_the_buy()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = ChainStops();
        var holds = ChainHolds();

        var loaded = LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        var armed = watch.Tick(dir.Path, 1013, days, stops, holds);
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        var shown = Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-soda", 40),
                new StaffOffer("headhunter", run.Family, "hh-soda", 4),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", staff.Role, "ba:street_main", 1),
                new PortfolioBusiness("Food 2", staff.Role, "ba:street_main", 2),
                new PortfolioBusiness("Food 3", staff.Role, "ba:street_main", 3),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", staff.Role, 1, "soda-line", 12, false),
                new ChainLayout("ba:street_main", staff.Role, 4, "", 0, true),
            });
        var closed = Close.Lock(
            clone,
            new[]
            {
                new LedgerDay(clone.Street, 2, clone.Family, 1011, -10f),
                new LedgerDay(clone.Street, 2, clone.Family, 1012, -4f),
                new LedgerDay(clone.Street, 2, clone.Family, 1013, -1f),
            },
            3,
            true,
            true);
        var bought = Buy.Lock(
            closed,
            new[]
            {
                new BuildingLot("hq", "ba:street_hq", 1),
                new BuildingLot("food", "ba:street_warehouse", 10),
                new BuildingLot("street", closed.Street, 20),
            });

        Assert.Null(armed);
        Assert.Equal(LoadLog.Line(pin.Build), loaded);
        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.Contains(item, File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.NotEmpty(shown);
        Assert.Equal(item, price.Item);
        Assert.Equal("food", run.Family);
        Assert.Equal(run.Family, staff.Role);
        Assert.Equal("hr", staff.Kind);
        Assert.Equal("soda-line", clone.Blueprint);
        Assert.Equal(4, clone.Destination);
        Assert.Equal(clone.Street, closed.Street);
        Assert.Equal(clone.Family, closed.Family);
        Assert.Equal(2, closed.Number);
        Assert.True(closed.Closed);
        Assert.True(bought.Bought);
        Assert.Equal("hq", bought.Place);
        Assert.Equal("ba:street_hq", bought.Street);
        Assert.Equal(1, bought.Number);
        Assert.DoesNotContain(pin.DllHash, File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_skips_an_empty_close()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy short
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = ChainStops();
        var holds = ChainHolds();

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        var shown = Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-empty", 0),
                new StaffOffer("headhunter", "specialty", "hh-gift", 100),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", staff.Role, "ba:street_main", 1),
                new PortfolioBusiness("Food 2", staff.Role, "ba:street_main", 2),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", staff.Role, 1, "soda-line", 12, false),
                new ChainLayout("ba:street_main", staff.Role, 4, "", 0, true),
            });
        var closed = Close.Lock(
            clone,
            new[]
            {
                new LedgerDay("ba:street_main", 2, staff.Role, 1011, -10f),
                new LedgerDay("ba:street_main", 2, staff.Role, 1012, -4f),
                new LedgerDay("ba:street_main", 2, staff.Role, 1013, -1f),
            },
            3,
            true,
            true);
        var bought = Buy.Lock(
            closed,
            new[]
            {
                new BuildingLot("hq", "ba:street_hq", 1),
                new BuildingLot("food", "ba:street_warehouse", 10),
                new BuildingLot("street", "ba:street_main", 20),
            });

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.NotEmpty(shown);
        Assert.Equal(item, run.Item);
        Assert.Equal("food", staff.Role);
        Assert.Equal("", clone.Blueprint);
        Assert.Equal(0, clone.Destination);
        Assert.False(closed.Closed);
        Assert.Equal("", closed.Street);
        Assert.Equal(0, closed.Number);
        Assert.False(bought.Bought);
        Assert.Equal("", bought.Place);
        Assert.Equal("", bought.Street);
        Assert.Equal(0, bought.Number);
        Assert.DoesNotContain(pin.DllHash, File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_keeps_hq_ahead_of_the_street()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy order
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = ChainStops();
        var holds = ChainHolds();

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        var shown = Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-soda", 40),
                new StaffOffer("headhunter", run.Family, "hh-soda", 4),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", staff.Role, "ba:street_main", 1),
                new PortfolioBusiness("Food 2", staff.Role, "ba:street_main", 2),
                new PortfolioBusiness("Food 3", staff.Role, "ba:street_main", 3),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", staff.Role, 1, "soda-line", 12, false),
                new ChainLayout("ba:street_main", staff.Role, 4, "", 0, true),
            });
        var closed = Close.Lock(
            clone,
            new[]
            {
                new LedgerDay(clone.Street, 2, clone.Family, 1011, -10f),
                new LedgerDay(clone.Street, 2, clone.Family, 1012, -4f),
                new LedgerDay(clone.Street, 2, clone.Family, 1013, -1f),
            },
            3,
            true,
            true);
        var bought = Buy.Lock(
            closed,
            new[]
            {
                new BuildingLot("street", closed.Street, 20),
                new BuildingLot("food", "ba:street_warehouse", 10),
                new BuildingLot("hq", "ba:street_annex", 7),
            });

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.NotEmpty(shown);
        Assert.Equal("food", run.Family);
        Assert.Equal(run.Family, staff.Role);
        Assert.Equal("soda-line", clone.Blueprint);
        Assert.True(closed.Closed);
        Assert.Equal(clone.Street, closed.Street);
        Assert.True(bought.Bought);
        Assert.Equal("hq", bought.Place);
        Assert.Equal("ba:street_annex", bought.Street);
        Assert.Equal(7, bought.Number);
        Assert.NotEqual(closed.Street, bought.Street);
        Assert.DoesNotContain(pin.DllHash, File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_locks_the_rows()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = ChainStops();
        var holds = ChainHolds();

        var loaded = LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        var armed = watch.Tick(dir.Path, 1013, days, stops, holds);
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        var shown = Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-soda", 40),
                new StaffOffer("headhunter", run.Family, "hh-soda", 4),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", staff.Role, "ba:street_main", 1),
                new PortfolioBusiness("Food 2", staff.Role, "ba:street_main", 2),
                new PortfolioBusiness("Food 3", staff.Role, "ba:street_main", 3),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", staff.Role, 1, "soda-line", 12, false),
                new ChainLayout("ba:street_main", staff.Role, 4, "", 0, true),
            });
        var closed = Close.Lock(
            clone,
            new[]
            {
                new LedgerDay(clone.Street, 2, clone.Family, 1011, -10f),
                new LedgerDay(clone.Street, 2, clone.Family, 1012, -4f),
                new LedgerDay(clone.Street, 2, clone.Family, 1013, -1f),
            },
            3,
            true,
            true);
        var bought = Buy.Lock(
            closed,
            new[]
            {
                new BuildingLot("hq", "ba:street_hq", 1),
                new BuildingLot("food", "ba:street_warehouse", 10),
                new BuildingLot("street", closed.Street, 20),
            });
        var panel = Panel.Rows(rows);

        var red = panel.Where(row => row.Tone == "red").ToArray();
        var bottom = red[red.Length - 1];
        Assert.Null(armed);
        Assert.Equal(LoadLog.Line(pin.Build), loaded);
        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.NotEmpty(shown);
        Assert.True(bought.Bought);
        Assert.Equal("hq", bought.Place);
        Assert.Equal("ba:street_hq", bought.Street);
        Assert.Equal(1, bought.Number);
        Assert.Equal("ba:street_fourthavenue", bottom.Business);
        Assert.Equal("ba:street_fourthavenue", bottom.Street);
        Assert.Equal(28, bottom.Number);
        Assert.Equal("Close ba:street_fourthavenue. Profit 50687.4805.", bottom.Explain);
        Assert.Equal(Advice.Explain(rows)[^1].Explain, bottom.Explain);
        Assert.DoesNotContain(pin.DllHash, File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_keeps_rows_when_the_buy_is_empty()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel open
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = ChainStops();
        var holds = ChainHolds();

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        var shown = Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-empty", 0),
                new StaffOffer("headhunter", "specialty", "hh-gift", 100),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", staff.Role, "ba:street_main", 1),
                new PortfolioBusiness("Food 2", staff.Role, "ba:street_main", 2),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", staff.Role, 1, "soda-line", 12, false),
                new ChainLayout("ba:street_main", staff.Role, 4, "", 0, true),
            });
        var closed = Close.Lock(
            clone,
            new[]
            {
                new LedgerDay("ba:street_main", 2, staff.Role, 1011, -10f),
                new LedgerDay("ba:street_main", 2, staff.Role, 1012, -4f),
                new LedgerDay("ba:street_main", 2, staff.Role, 1013, -1f),
            },
            3,
            true,
            true);
        var bought = Buy.Lock(
            closed,
            new[]
            {
                new BuildingLot("hq", "ba:street_hq", 1),
                new BuildingLot("food", "ba:street_warehouse", 10),
                new BuildingLot("street", "ba:street_main", 20),
            });
        var session = new PanelSession();
        var panel = session.Press(dir.Path, rows);
        session.Draw(rows);

        var bottom = panel.Last(row => row.Tone == "red");
        var log = File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log"));
        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.NotEmpty(shown);
        Assert.False(bought.Bought);
        Assert.Equal("", bought.Place);
        Assert.Equal("ba:street_fourthavenue", bottom.Business);
        Assert.Equal(28, bottom.Number);
        Assert.Equal("Close ba:street_fourthavenue. Profit 50687.4805.", bottom.Explain);
        Assert.Equal(1, log.Split('\n').Count(line => line.StartsWith("panel.opened ", StringComparison.Ordinal)));
        Assert.Contains("panel.opened version=" + LoadLog.Version + " rows=" + rows.Count.ToString(), log);
        Assert.Equal(shown.Count + panel.Count, log.Split('\n').Count(line => line.StartsWith("advice.shown ", StringComparison.Ordinal)));
        Assert.DoesNotContain(pin.DllHash, log);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_locks_the_calls()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = ChainStops();
        var holds = ChainHolds();

        var loaded = LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        var armed = watch.Tick(dir.Path, 1013, days, stops, holds);
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        var shown = Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-soda", 40),
                new StaffOffer("headhunter", run.Family, "hh-soda", 4),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", staff.Role, "ba:street_main", 1),
                new PortfolioBusiness("Food 2", staff.Role, "ba:street_main", 2),
                new PortfolioBusiness("Food 3", staff.Role, "ba:street_main", 3),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", staff.Role, 1, "soda-line", 12, false),
                new ChainLayout("ba:street_main", staff.Role, 4, "", 0, true),
            });
        var closed = Close.Lock(
            clone,
            new[]
            {
                new LedgerDay(clone.Street, 2, clone.Family, 1011, -10f),
                new LedgerDay(clone.Street, 2, clone.Family, 1012, -4f),
                new LedgerDay(clone.Street, 2, clone.Family, 1013, -1f),
            },
            3,
            true,
            true);
        var bought = Buy.Lock(
            closed,
            new[]
            {
                new BuildingLot("hq", "ba:street_hq", 1),
                new BuildingLot("food", "ba:street_warehouse", 10),
                new BuildingLot("street", closed.Street, 20),
            });
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));

        Assert.Null(armed);
        Assert.Equal(LoadLog.Line(pin.Build), loaded);
        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.NotEmpty(shown);
        Assert.Equal("hq", bought.Place);
        Assert.Equal(pin.ShippedBuild, api.Build);
        Assert.Equal("Helpers.BusinessHelper.ShutdownBusiness", api.Close);
        Assert.Equal("BizManPresentation.SendBuyBuildingOffer", api.Buy);
        Assert.Equal("Buildings.Office.Headquarters.PricingManagerPlan.SetPrice", api.Price);
        Assert.Equal(
            "UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination",
            api.Delivery);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_locks_the_calls_when_the_buy_is_empty()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api empty
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = ChainStops();
        var holds = ChainHolds();

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-empty", 0),
                new StaffOffer("headhunter", "specialty", "hh-gift", 100),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", staff.Role, "ba:street_main", 1),
                new PortfolioBusiness("Food 2", staff.Role, "ba:street_main", 2),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", staff.Role, 1, "soda-line", 12, false),
                new ChainLayout("ba:street_main", staff.Role, 4, "", 0, true),
            });
        var closed = Close.Lock(
            clone,
            new[]
            {
                new LedgerDay("ba:street_main", 2, staff.Role, 1011, -10f),
                new LedgerDay("ba:street_main", 2, staff.Role, 1012, -4f),
                new LedgerDay("ba:street_main", 2, staff.Role, 1013, -1f),
            },
            3,
            true,
            true);
        var bought = Buy.Lock(
            closed,
            new[]
            {
                new BuildingLot("hq", "ba:street_hq", 1),
                new BuildingLot("food", "ba:street_warehouse", 10),
                new BuildingLot("street", "ba:street_main", 20),
            });
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.False(bought.Bought);
        Assert.Equal("", bought.Place);
        Assert.Equal(pin.ShippedBuild, api.Build);
        Assert.Equal("Helpers.BusinessHelper.ShutdownBusiness", api.Close);
        Assert.Equal("BizManPresentation.SendBuyBuildingOffer", api.Buy);
        Assert.Equal("Buildings.Office.Headquarters.PricingManagerPlan.SetPrice", api.Price);
        Assert.Equal(
            "UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination",
            api.Delivery);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_blanks_the_calls_when_the_panel_is_empty()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api blank
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = Array.Empty<FinancialDay>();
        var stops = Array.Empty<DeliveryStop>();
        var holds = Array.Empty<WarehouseHold>();

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(item, Array.Empty<ItemSoldPerPriceEntry>());
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(run, Array.Empty<StaffOffer>());
        var clusters = Advisor.StreetClusters(Array.Empty<PortfolioBusiness>());
        var clone = Layout.Lock(staff, clusters, Array.Empty<ChainLayout>());
        var closed = Close.Lock(clone, Array.Empty<LedgerDay>(), 3, true, true);
        var bought = Buy.Lock(closed, Array.Empty<BuildingLot>());
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.Equal("", bought.Place);
        Assert.Empty(panel);
        Assert.Equal("3682", pin.ShippedBuild);
        Assert.Equal("", api.Build);
        Assert.Equal("", api.Close);
        Assert.Equal("", api.Buy);
        Assert.Equal("", api.Price);
        Assert.Equal("", api.Delivery);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_locks_the_explain_line()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score
        var pin = PinDlls();
        using var dir = new TempLog();
        var canonical = ChainDays()[0].Rows[0];
        var days = new[]
        {
            new FinancialDay(
                1011,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -10f),
                }),
            new FinancialDay(
                1012,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -20f),
                }),
            new FinancialDay(
                1013,
                new[]
                {
                    canonical,
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -30f),
                }),
        };
        var stops = ChainStops();
        var holds = ChainHolds();

        var loaded = LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        var armed = watch.Tick(dir.Path, 1013, days, stops, holds);
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        var shown = Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-soda", 40),
                new StaffOffer("headhunter", run.Family, "hh-soda", 4),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", staff.Role, "ba:street_main", 1),
                new PortfolioBusiness("Food 2", staff.Role, "ba:street_main", 2),
                new PortfolioBusiness("Food 3", staff.Role, "ba:street_main", 3),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", staff.Role, 1, "soda-line", 12, false),
                new ChainLayout("ba:street_main", staff.Role, 4, "", 0, true),
            });
        var closed = Close.Lock(
            clone,
            new[]
            {
                new LedgerDay(clone.Street, 2, clone.Family, 1011, -10f),
                new LedgerDay(clone.Street, 2, clone.Family, 1012, -4f),
                new LedgerDay(clone.Street, 2, clone.Family, 1013, -1f),
            },
            3,
            true,
            true);
        var bought = Buy.Lock(
            closed,
            new[]
            {
                new BuildingLot("hq", "ba:street_hq", 1),
                new BuildingLot("food", "ba:street_warehouse", 10),
                new BuildingLot("street", closed.Street, 20),
            });
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);

        Assert.Null(armed);
        Assert.Equal(LoadLog.Line(pin.Build), loaded);
        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.NotEmpty(shown);
        Assert.Equal("hq", bought.Place);
        Assert.Equal(pin.ShippedBuild, api.Build);
        Assert.Equal("Helpers.BusinessHelper.ShutdownBusiness", api.Close);
        Assert.Equal(2, closed.Number);
        Assert.Equal("ba:street_main", closed.Street);
        Assert.Equal(2, scored.Count);
        Assert.Equal(
            "Close ba:street_side 9. After rent -30. Streak 3. Trend -20.",
            scored[0].Explain);
        Assert.Equal(
            "Close ba:street_fourthavenue 28. After rent 50687.4805. Streak 0. Trend 0.",
            scored[1].Explain);
        Assert.NotEqual(closed.Street, scored[0].Street);
        Assert.DoesNotContain(scored, line => line.Number == closed.Number && line.Street == closed.Street);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_keeps_no_line_when_the_panel_is_empty()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score empty
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = Array.Empty<FinancialDay>();
        var stops = Array.Empty<DeliveryStop>();
        var holds = Array.Empty<WarehouseHold>();

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(item, Array.Empty<ItemSoldPerPriceEntry>());
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(run, Array.Empty<StaffOffer>());
        var clusters = Advisor.StreetClusters(Array.Empty<PortfolioBusiness>());
        var clone = Layout.Lock(staff, clusters, Array.Empty<ChainLayout>());
        var closed = Close.Lock(clone, Array.Empty<LedgerDay>(), 3, true, true);
        var bought = Buy.Lock(closed, Array.Empty<BuildingLot>());
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.Equal("", bought.Place);
        Assert.Empty(panel);
        Assert.Equal("", api.Build);
        Assert.Equal("3682", pin.ShippedBuild);
        Assert.Empty(scored);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_still_explains_the_store_when_the_layout_is_empty()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score layout
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = ChainStops();
        var holds = ChainHolds();

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(run, Array.Empty<StaffOffer>());
        var clusters = Advisor.StreetClusters(Array.Empty<PortfolioBusiness>());
        var clone = Layout.Lock(staff, clusters, Array.Empty<ChainLayout>());
        var closed = Close.Lock(
            clone,
            new[]
            {
                new LedgerDay("ba:street_fourthavenue", 28, "food", 1011, -10f),
                new LedgerDay("ba:street_fourthavenue", 28, "food", 1012, -4f),
                new LedgerDay("ba:street_fourthavenue", 28, "food", 1013, -1f),
            },
            3,
            true,
            true);
        var bought = Buy.Lock(closed, Array.Empty<BuildingLot>());
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.Equal("", clone.Street);
        Assert.False(closed.Closed);
        Assert.False(bought.Bought);
        Assert.Equal(pin.ShippedBuild, api.Build);
        Assert.Equal(
            "Close ba:street_fourthavenue 28. After rent 50687.4805. Streak 0. Trend 0.",
            scored[0].Explain);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_locks_the_reassignment()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics
        var pin = PinDlls();
        using var dir = new TempLog();
        var canonical = ChainDays()[0].Rows[0];
        var days = new[]
        {
            new FinancialDay(
                1011,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -10f),
                }),
            new FinancialDay(
                1012,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -20f),
                }),
            new FinancialDay(
                1013,
                new[]
                {
                    canonical,
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -30f),
                }),
        };
        var stops = new[]
        {
            ChainStops()[0],
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_side 9",
                "ba:itemname_cheapgift",
                null,
                ""),
        };
        var holds = new[]
        {
            ChainHolds()[0],
            new WarehouseHold("W2", "ba:street_b", 2, "ba:itemname_cheapgift", 20),
        };

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-soda", 40),
                new StaffOffer("headhunter", run.Family, "hh-soda", 4),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", staff.Role, "ba:street_main", 1),
                new PortfolioBusiness("Food 2", staff.Role, "ba:street_main", 2),
                new PortfolioBusiness("Food 3", staff.Role, "ba:street_main", 3),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", staff.Role, 1, "soda-line", 12, false),
                new ChainLayout("ba:street_main", staff.Role, 4, "", 0, true),
            });
        var closed = Close.Lock(
            clone,
            new[]
            {
                new LedgerDay(clone.Street, 2, clone.Family, 1011, -10f),
                new LedgerDay(clone.Street, 2, clone.Family, 1012, -4f),
                new LedgerDay(clone.Street, 2, clone.Family, 1013, -1f),
            },
            3,
            true,
            true);
        var bought = Buy.Lock(
            closed,
            new[]
            {
                new BuildingLot("hq", "ba:street_hq", 1),
                new BuildingLot("food", "ba:street_warehouse", 10),
                new BuildingLot("street", closed.Street, 20),
            });
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);
        var review = Logistics.FromRoutes(
            scored,
            api,
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.NotEmpty(scored);
        Assert.True(bought.Bought);
        Assert.Equal(pin.ShippedBuild, api.Build);
        Assert.Equal(
            "UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination",
            api.Delivery);
        Assert.Equal("W1", review.Services[0].Warehouse);
        Assert.Equal("ba:street_firstavenue 38", review.Services[0].Destination);
        Assert.Equal("food", review.Services[0].Family);
        Assert.Equal("ba:street_side 9", review.Move!.Destination);
        Assert.Equal("W1", review.Move.From);
        Assert.Equal("W2", review.Move.To);
        Assert.Equal("specialty", review.Move.Family);
        Assert.Equal(
            "Reassign ba:street_side 9. From W1. To W2. Family specialty.",
            review.Move.Explain);
        Assert.DoesNotContain("AddDestination", review.Move.Explain);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_keeps_no_reassignment_when_the_route_is_supplied()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics supplied
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = ChainStops();
        var holds = ChainHolds();

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(run, Array.Empty<StaffOffer>());
        var clusters = Advisor.StreetClusters(Array.Empty<PortfolioBusiness>());
        var clone = Layout.Lock(staff, clusters, Array.Empty<ChainLayout>());
        var closed = Close.Lock(clone, Array.Empty<LedgerDay>(), 3, true, true);
        var bought = Buy.Lock(closed, Array.Empty<BuildingLot>());
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);
        var review = Logistics.FromRoutes(
            scored,
            api,
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.NotEmpty(scored);
        Assert.False(bought.Bought);
        Assert.Equal(pin.ShippedBuild, api.Build);
        Assert.Null(review.Move);
        Assert.Equal("W1", review.Services[0].Warehouse);
        Assert.Equal("ba:street_firstavenue 38", review.Services[0].Destination);
        Assert.Equal("food", review.Services[0].Family);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_keeps_no_reassignment_when_the_panel_is_empty()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics empty
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = Array.Empty<FinancialDay>();
        var stops = new[]
        {
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_firstavenue 38",
                "ba:itemname_sodacan",
                1016,
                ""),
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_side 9",
                "ba:itemname_cheapgift",
                null,
                ""),
        };
        var holds = new[]
        {
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_sodacan", 45),
            new WarehouseHold("W2", "ba:street_b", 2, "ba:itemname_cheapgift", 20),
        };

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(item, Array.Empty<ItemSoldPerPriceEntry>());
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(run, Array.Empty<StaffOffer>());
        var clusters = Advisor.StreetClusters(Array.Empty<PortfolioBusiness>());
        var clone = Layout.Lock(staff, clusters, Array.Empty<ChainLayout>());
        var closed = Close.Lock(clone, Array.Empty<LedgerDay>(), 3, true, true);
        var bought = Buy.Lock(closed, Array.Empty<BuildingLot>());
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);
        var review = Logistics.FromRoutes(
            scored,
            api,
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));
        var direct = Logistics.Review(new ApiNote(pin.ShippedBuild, "", "", "", ""), stops, holds);

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.Empty(panel);
        Assert.Empty(scored);
        Assert.False(bought.Bought);
        Assert.Equal("", api.Build);
        Assert.Contains("ba:street_side 9", File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.Equal("W2", direct.Move!.To);
        Assert.Null(review.Move);
        Assert.Empty(review.Services);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_payback_locks_the_building()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback
        var pin = PinDlls();
        using var dir = new TempLog();
        var canonical = ChainDays()[0].Rows[0];
        var days = new[]
        {
            new FinancialDay(
                1011,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -10f),
                }),
            new FinancialDay(
                1012,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -20f),
                }),
            new FinancialDay(
                1013,
                new[]
                {
                    canonical,
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -30f),
                }),
        };
        var stops = new[]
        {
            ChainStops()[0],
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_side 9",
                "ba:itemname_cheapgift",
                null,
                ""),
        };
        var holds = new[]
        {
            ChainHolds()[0],
            new WarehouseHold("W2", "ba:street_b", 2, "ba:itemname_cheapgift", 20),
        };

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-soda", 40),
                new StaffOffer("headhunter", run.Family, "hh-soda", 4),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", staff.Role, "ba:street_main", 1),
                new PortfolioBusiness("Food 2", staff.Role, "ba:street_main", 2),
                new PortfolioBusiness("Food 3", staff.Role, "ba:street_main", 3),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", staff.Role, 1, "soda-line", 12, false),
                new ChainLayout("ba:street_main", staff.Role, 4, "", 0, true),
            });
        var closed = Close.Lock(
            clone,
            new[]
            {
                new LedgerDay(clone.Street, 2, clone.Family, 1011, -10f),
                new LedgerDay(clone.Street, 2, clone.Family, 1012, -4f),
                new LedgerDay(clone.Street, 2, clone.Family, 1013, -1f),
            },
            3,
            true,
            true);
        var bought = Buy.Lock(
            closed,
            new[]
            {
                new BuildingLot("hq", "ba:street_hq", 1),
                new BuildingLot("food", "ba:street_warehouse", 10),
                new BuildingLot("street", closed.Street, 20),
            });
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);
        var review = Logistics.FromRoutes(
            scored,
            api,
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));
        var plan = Payback.FromLedger(
            review,
            api,
            StatementDump.CsvPath(dir.Path),
            new[]
            {
                new BuildingAsk("ba:street_fourthavenue", 28, 10990d),
                new BuildingAsk("ba:street_side", 9, 4000d),
                new BuildingAsk(bought.Street, bought.Number, 10d),
            });

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.NotEmpty(scored);
        Assert.True(bought.Bought);
        Assert.Equal("ba:street_hq", bought.Street);
        Assert.Equal(pin.ShippedBuild, api.Build);
        Assert.Equal("ba:street_side 9", review.Move!.Destination);
        Assert.True(plan.Bought);
        Assert.Equal("ba:street_fourthavenue", plan.Street);
        Assert.Equal(28, plan.Number);
        Assert.Equal(10d, plan.Days);
        Assert.NotEqual(bought.Street, plan.Street);
        Assert.NotEqual(review.Move.Destination, plan.Street + " " + plan.Number.ToString());
        Assert.Equal(
            "Open ba:street_fourthavenue. Buy ba:street_fourthavenue 28. Payback 10.",
            plan.Explain);
        Assert.DoesNotContain("SendBuyBuildingOffer", plan.Explain);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_payback_locks_the_building_when_the_route_is_supplied()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback supplied
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = ChainStops();
        var holds = ChainHolds();

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(run, Array.Empty<StaffOffer>());
        var clusters = Advisor.StreetClusters(Array.Empty<PortfolioBusiness>());
        var clone = Layout.Lock(staff, clusters, Array.Empty<ChainLayout>());
        var closed = Close.Lock(clone, Array.Empty<LedgerDay>(), 3, true, true);
        var bought = Buy.Lock(closed, Array.Empty<BuildingLot>());
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);
        var review = Logistics.FromRoutes(
            scored,
            api,
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));
        var plan = Payback.FromLedger(
            review,
            api,
            StatementDump.CsvPath(dir.Path),
            new[]
            {
                new BuildingAsk("ba:street_fourthavenue", 28, 2198d),
                new BuildingAsk("ba:street_side", 9, 1d),
            });

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.NotEmpty(scored);
        Assert.False(bought.Bought);
        Assert.Equal(pin.ShippedBuild, api.Build);
        Assert.Null(review.Move);
        Assert.Equal("W1", review.Services[0].Warehouse);
        Assert.True(plan.Bought);
        Assert.Equal("ba:street_fourthavenue", plan.Street);
        Assert.Equal(28, plan.Number);
        Assert.Equal(2d, plan.Days);
        Assert.Equal(
            "Open ba:street_fourthavenue. Buy ba:street_fourthavenue 28. Payback 2.",
            plan.Explain);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_payback_a_tie_keeps_the_later_building()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback tie
        var pin = PinDlls();
        using var dir = new TempLog();
        var canonical = ChainDays()[0];
        var days = new[]
        {
            new FinancialDay(
                canonical.Day,
                new[]
                {
                    canonical.Rows[0],
                    new StatementRow("ba:street_later", 7, 0f, 0f, 0f, 20f, 0f, 0f, 1f),
                }),
        };
        var stops = ChainStops();
        var holds = ChainHolds();

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(run, Array.Empty<StaffOffer>());
        var clusters = Advisor.StreetClusters(Array.Empty<PortfolioBusiness>());
        var clone = Layout.Lock(staff, clusters, Array.Empty<ChainLayout>());
        var closed = Close.Lock(clone, Array.Empty<LedgerDay>(), 3, true, true);
        var bought = Buy.Lock(closed, Array.Empty<BuildingLot>());
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);
        var review = Logistics.FromRoutes(
            scored,
            api,
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));
        var plan = Payback.FromLedger(
            review,
            api,
            StatementDump.CsvPath(dir.Path),
            new[]
            {
                new BuildingAsk("ba:street_fourthavenue", 28, 10990d),
                new BuildingAsk("ba:street_later", 7, 200d),
            });

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.NotEmpty(scored);
        Assert.False(bought.Bought);
        Assert.Equal(pin.ShippedBuild, api.Build);
        Assert.Null(review.Move);
        Assert.Equal("food", review.Services[0].Family);
        Assert.True(plan.Bought);
        Assert.Equal("ba:street_later", plan.Street);
        Assert.Equal(7, plan.Number);
        Assert.Equal(10d, plan.Days);
        Assert.NotEqual("ba:street_fourthavenue", plan.Street);
        Assert.Equal("Open ba:street_later. Buy ba:street_later 7. Payback 10.", plan.Explain);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_payback_keeps_no_building_when_the_panel_is_empty()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback empty
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = Array.Empty<FinancialDay>();
        var stops = new[]
        {
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_firstavenue 38",
                "ba:itemname_sodacan",
                1016,
                ""),
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_side 9",
                "ba:itemname_cheapgift",
                null,
                ""),
        };
        var holds = new[]
        {
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_sodacan", 45),
            new WarehouseHold("W2", "ba:street_b", 2, "ba:itemname_cheapgift", 20),
        };

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(item, Array.Empty<ItemSoldPerPriceEntry>());
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(run, Array.Empty<StaffOffer>());
        var clusters = Advisor.StreetClusters(Array.Empty<PortfolioBusiness>());
        var clone = Layout.Lock(staff, clusters, Array.Empty<ChainLayout>());
        var closed = Close.Lock(clone, Array.Empty<LedgerDay>(), 3, true, true);
        var bought = Buy.Lock(closed, Array.Empty<BuildingLot>());
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);
        var review = Logistics.FromRoutes(
            scored,
            api,
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));
        var asks = new[]
        {
            new BuildingAsk("ba:street_fourthavenue", 28, 10990d),
            new BuildingAsk("ba:street_side", 9, 4000d),
        };
        var plan = Payback.FromLedger(
            review,
            api,
            StatementDump.CsvPath(dir.Path),
            asks);
        var directApi = new ApiNote(pin.ShippedBuild, "", "", "", "");
        var directReview = Logistics.Review(directApi, stops, holds);
        var direct = Payback.Decide(
            directApi,
            directReview,
            new[]
            {
                new FinancialDay(
                    1013,
                    new[]
                    {
                        new StatementRow("ba:street_fourthavenue", 28, 0f, 0f, 0f, 1099f, 0f, 0f, 0f),
                        new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, 0f),
                    }),
            },
            asks);

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.Empty(panel);
        Assert.Empty(scored);
        Assert.False(bought.Bought);
        Assert.Equal("", api.Build);
        Assert.Contains("ba:street_side 9", File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.Equal("W2", directReview.Move!.To);
        Assert.Null(review.Move);
        Assert.False(plan.Bought);
        Assert.Equal("", plan.Street);
        Assert.Equal(0, plan.Number);
        Assert.True(direct.Bought);
        Assert.Equal("ba:street_fourthavenue", direct.Street);
        Assert.Equal(28, direct.Number);
        Assert.Equal(10d, direct.Days);
        Assert.NotEqual(directReview.Move.Destination, direct.Street + " " + direct.Number.ToString());
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_payback_roadmap_locks_the_first_action()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap
        var pin = PinDlls();
        using var dir = new TempLog();
        var canonical = ChainDays()[0].Rows[0];
        var days = new[]
        {
            new FinancialDay(
                1011,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -10f),
                }),
            new FinancialDay(
                1012,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -20f),
                }),
            new FinancialDay(
                1013,
                new[]
                {
                    canonical,
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -30f),
                }),
        };
        var stops = new[]
        {
            ChainStops()[0],
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_side 9",
                "ba:itemname_cheapgift",
                null,
                ""),
        };
        var holds = new[]
        {
            ChainHolds()[0],
            new WarehouseHold("W2", "ba:street_b", 2, "ba:itemname_cheapgift", 20),
        };

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-soda", 40),
                new StaffOffer("headhunter", run.Family, "hh-soda", 4),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", staff.Role, "ba:street_main", 1),
                new PortfolioBusiness("Food 2", staff.Role, "ba:street_main", 2),
                new PortfolioBusiness("Food 3", staff.Role, "ba:street_main", 3),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", staff.Role, 1, "soda-line", 12, false),
                new ChainLayout("ba:street_main", staff.Role, 4, "", 0, true),
            });
        var closed = Close.Lock(
            clone,
            new[]
            {
                new LedgerDay(clone.Street, 2, clone.Family, 1011, -10f),
                new LedgerDay(clone.Street, 2, clone.Family, 1012, -4f),
                new LedgerDay(clone.Street, 2, clone.Family, 1013, -1f),
            },
            3,
            true,
            true);
        var bought = Buy.Lock(
            closed,
            new[]
            {
                new BuildingLot("hq", "ba:street_hq", 1),
                new BuildingLot("food", "ba:street_warehouse", 10),
                new BuildingLot("street", closed.Street, 20),
            });
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);
        var review = Logistics.FromRoutes(
            scored,
            api,
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));
        var plan = Payback.FromLedger(
            review,
            api,
            StatementDump.CsvPath(dir.Path),
            new[]
            {
                new BuildingAsk("ba:street_fourthavenue", 28, 10990d),
                new BuildingAsk("ba:street_side", 9, 4000d),
                new BuildingAsk(bought.Street, bought.Number, 10d),
            });
        var steps = Roadmap.Order(api, scored, review, plan, days, 3);

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.NotEmpty(scored);
        Assert.True(bought.Bought);
        Assert.Equal("ba:street_hq", bought.Street);
        Assert.Equal(pin.ShippedBuild, api.Build);
        Assert.Equal("ba:street_side 9", review.Move!.Destination);
        Assert.True(plan.Bought);
        Assert.Equal("ba:street_fourthavenue", plan.Street);
        Assert.Equal(28, plan.Number);
        Assert.Equal(10d, plan.Days);
        Assert.Equal(2, steps.Count);
        Assert.Equal("open", steps[0].Kind);
        Assert.Equal("ba:street_fourthavenue", steps[0].Street);
        Assert.Equal(28, steps[0].Number);
        Assert.Equal(1099d, steps[0].DailyGain);
        Assert.Equal(
            "Open ba:street_fourthavenue. Buy ba:street_fourthavenue 28. Daily gain 1099.",
            steps[0].Explain);
        Assert.Equal("close", steps[1].Kind);
        Assert.Equal("ba:street_side", steps[1].Street);
        Assert.Equal(30d, steps[1].DailyGain);
        Assert.NotEqual(bought.Street, steps[0].Street);
        Assert.NotEqual(review.Move.Destination, steps[0].Street + " " + steps[0].Number.ToString());
        Assert.DoesNotContain("SendBuyBuildingOffer", steps[0].Explain);
        Assert.DoesNotContain("ShutdownBusiness", steps[0].Explain);
        Assert.DoesNotContain("SetPrice", steps[0].Explain);
        Assert.DoesNotContain("AddDestination", steps[0].Explain);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_payback_roadmap_shutdown_locks_that_store()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown
        var pin = PinDlls();
        using var dir = new TempLog();
        var canonical = ChainDays()[0].Rows[0];
        var days = new[]
        {
            new FinancialDay(
                1011,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -10f),
                }),
            new FinancialDay(
                1012,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -20f),
                }),
            new FinancialDay(
                1013,
                new[]
                {
                    canonical,
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -30f),
                }),
        };
        var stops = new[]
        {
            ChainStops()[0],
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_side 9",
                "ba:itemname_cheapgift",
                null,
                ""),
        };
        var holds = new[]
        {
            ChainHolds()[0],
            new WarehouseHold("W2", "ba:street_b", 2, "ba:itemname_cheapgift", 20),
        };

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-soda", 40),
                new StaffOffer("headhunter", run.Family, "hh-soda", 4),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", staff.Role, "ba:street_main", 1),
                new PortfolioBusiness("Food 2", staff.Role, "ba:street_main", 2),
                new PortfolioBusiness("Food 3", staff.Role, "ba:street_main", 3),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", staff.Role, 1, "soda-line", 12, false),
                new ChainLayout("ba:street_main", staff.Role, 4, "", 0, true),
            });
        var closed = Close.Lock(
            clone,
            new[]
            {
                new LedgerDay(clone.Street, 2, clone.Family, 1011, -10f),
                new LedgerDay(clone.Street, 2, clone.Family, 1012, -4f),
                new LedgerDay(clone.Street, 2, clone.Family, 1013, -1f),
            },
            3,
            true,
            true);
        var bought = Buy.Lock(
            closed,
            new[]
            {
                new BuildingLot("hq", "ba:street_hq", 1),
                new BuildingLot("food", "ba:street_warehouse", 10),
                new BuildingLot("street", closed.Street, 20),
            });
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);
        var review = Logistics.FromRoutes(
            scored,
            api,
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));
        var plan = Payback.FromLedger(
            review,
            api,
            StatementDump.CsvPath(dir.Path),
            new[]
            {
                new BuildingAsk("ba:street_fourthavenue", 28, 10990d),
                new BuildingAsk("ba:street_side", 9, 4000d),
                new BuildingAsk(bought.Street, bought.Number, 10d),
            });
        var steps = Roadmap.Order(api, scored, review, plan, days, 3);
        var shut = Shutdown.Confirm(api, steps, steps[1].Street, steps[1].Number, true);
        var logged = Shutdown.Record(dir.Path, shut);

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.NotEmpty(scored);
        Assert.True(bought.Bought);
        Assert.Equal("ba:street_hq", bought.Street);
        Assert.Equal(pin.ShippedBuild, api.Build);
        Assert.Equal("ba:street_side 9", review.Move!.Destination);
        Assert.True(plan.Bought);
        Assert.Equal("open", steps[0].Kind);
        Assert.Equal("close", steps[1].Kind);
        Assert.Equal("ba:street_side", steps[1].Street);
        Assert.Equal(9, steps[1].Number);
        Assert.True(shut.Closed);
        Assert.Equal(steps[1].Street, shut.Street);
        Assert.Equal(steps[1].Number, shut.Number);
        Assert.Equal("ba:street_side", shut.Street);
        Assert.Equal(9, shut.Number);
        Assert.Equal(api.Close, shut.Call);
        Assert.NotEqual(steps[0].Street, shut.Street);
        Assert.Equal(
            "store.closed version=" + LoadLog.Version
                + " street=ba:street_side number=9 call=Helpers.BusinessHelper.ShutdownBusiness",
            logged);
        Assert.Contains(logged, File.ReadAllText(LoadLog.CeoLogPath(dir.Path)));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_payback_roadmap_shutdown_leaves_the_other_store_open()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown separate
        var pin = PinDlls();
        using var dir = new TempLog();
        var canonical = ChainDays()[0].Rows[0];
        var days = new[]
        {
            new FinancialDay(
                1011,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -10f),
                }),
            new FinancialDay(
                1012,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -20f),
                }),
            new FinancialDay(
                1013,
                new[]
                {
                    canonical,
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -5000f),
                    new StatementRow("ba:street_other", 4, 0f, 0f, 0f, 1f, 0f, 0f, -10f),
                }),
        };
        var stops = new[]
        {
            ChainStops()[0],
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_side 9",
                "ba:itemname_cheapgift",
                null,
                ""),
        };
        var holds = new[]
        {
            ChainHolds()[0],
            new WarehouseHold("W2", "ba:street_b", 2, "ba:itemname_cheapgift", 20),
        };

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-soda", 40),
                new StaffOffer("headhunter", run.Family, "hh-soda", 4),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", staff.Role, "ba:street_main", 1),
                new PortfolioBusiness("Food 2", staff.Role, "ba:street_main", 2),
                new PortfolioBusiness("Food 3", staff.Role, "ba:street_main", 3),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", staff.Role, 1, "soda-line", 12, false),
                new ChainLayout("ba:street_main", staff.Role, 4, "", 0, true),
            });
        var closed = Close.Lock(
            clone,
            new[]
            {
                new LedgerDay(clone.Street, 2, clone.Family, 1011, -10f),
                new LedgerDay(clone.Street, 2, clone.Family, 1012, -4f),
                new LedgerDay(clone.Street, 2, clone.Family, 1013, -1f),
            },
            3,
            true,
            true);
        var bought = Buy.Lock(
            closed,
            new[]
            {
                new BuildingLot("hq", "ba:street_hq", 1),
                new BuildingLot("food", "ba:street_warehouse", 10),
                new BuildingLot("street", closed.Street, 20),
            });
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);
        var review = Logistics.FromRoutes(
            scored,
            api,
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));
        var plan = Payback.FromLedger(
            review,
            api,
            StatementDump.CsvPath(dir.Path),
            new[]
            {
                new BuildingAsk("ba:street_fourthavenue", 28, 10990d),
                new BuildingAsk("ba:street_side", 9, 4000d),
                new BuildingAsk(bought.Street, bought.Number, 10d),
            });
        var steps = Roadmap.Order(api, scored, review, plan, days, 3);
        var shut = Shutdown.Confirm(api, steps, steps[0].Street, steps[0].Number, true);
        var held = Shutdown.Confirm(api, steps, steps[2].Street, steps[2].Number, false);
        var opened = Shutdown.Confirm(api, steps, steps[1].Street, steps[1].Number, true);
        var logged = Shutdown.Record(dir.Path, shut);
        var undone = Shutdown.Reload(shut);
        var ledger = File.ReadAllText(StatementDump.CsvPath(dir.Path));

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.Equal(pin.ShippedBuild, api.Build);
        Assert.Equal(3, steps.Count);
        Assert.Equal("close", steps[0].Kind);
        Assert.Equal("ba:street_side", steps[0].Street);
        Assert.Equal(9, steps[0].Number);
        Assert.Equal(5000d, steps[0].DailyGain);
        Assert.Equal("open", steps[1].Kind);
        Assert.Equal("ba:street_fourthavenue", steps[1].Street);
        Assert.Equal("close", steps[2].Kind);
        Assert.Equal("ba:street_other", steps[2].Street);
        Assert.Equal(4, steps[2].Number);
        Assert.True(shut.Closed);
        Assert.Equal(steps[0].Street, shut.Street);
        Assert.Equal(9, shut.Number);
        Assert.Equal(api.Close, shut.Call);
        Assert.False(held.Closed);
        Assert.Equal("", held.Street);
        Assert.Equal(0, held.Number);
        Assert.False(opened.Closed);
        Assert.Equal("", opened.Call);
        Assert.DoesNotContain("SendBuyBuildingOffer", opened.Call);
        Assert.False(undone.Closed);
        Assert.Equal("", undone.Street);
        Assert.Equal("", undone.Call);
        Assert.Contains("ba:street_side", ledger);
        Assert.Contains("ba:street_other", ledger);
        Assert.Contains(logged, File.ReadAllText(LoadLog.CeoLogPath(dir.Path)));
        Assert.DoesNotContain("ba:street_other", logged);
        Assert.Equal("", Shutdown.Record(dir.Path, held));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_payback_roadmap_the_larger_loss_is_first()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap loss
        var pin = PinDlls();
        using var dir = new TempLog();
        var canonical = ChainDays()[0].Rows[0];
        var days = new[]
        {
            new FinancialDay(
                1011,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -10f),
                }),
            new FinancialDay(
                1012,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -20f),
                }),
            new FinancialDay(
                1013,
                new[]
                {
                    canonical,
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -5000f),
                }),
        };
        var stops = new[]
        {
            ChainStops()[0],
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_side 9",
                "ba:itemname_cheapgift",
                null,
                ""),
        };
        var holds = new[]
        {
            ChainHolds()[0],
            new WarehouseHold("W2", "ba:street_b", 2, "ba:itemname_cheapgift", 20),
        };

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-soda", 40),
                new StaffOffer("headhunter", run.Family, "hh-soda", 4),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", staff.Role, "ba:street_main", 1),
                new PortfolioBusiness("Food 2", staff.Role, "ba:street_main", 2),
                new PortfolioBusiness("Food 3", staff.Role, "ba:street_main", 3),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", staff.Role, 1, "soda-line", 12, false),
                new ChainLayout("ba:street_main", staff.Role, 4, "", 0, true),
            });
        var closed = Close.Lock(
            clone,
            new[]
            {
                new LedgerDay(clone.Street, 2, clone.Family, 1011, -10f),
                new LedgerDay(clone.Street, 2, clone.Family, 1012, -4f),
                new LedgerDay(clone.Street, 2, clone.Family, 1013, -1f),
            },
            3,
            true,
            true);
        var bought = Buy.Lock(
            closed,
            new[]
            {
                new BuildingLot("hq", "ba:street_hq", 1),
                new BuildingLot("food", "ba:street_warehouse", 10),
                new BuildingLot("street", closed.Street, 20),
            });
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);
        var review = Logistics.FromRoutes(
            scored,
            api,
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));
        var plan = Payback.FromLedger(
            review,
            api,
            StatementDump.CsvPath(dir.Path),
            new[]
            {
                new BuildingAsk("ba:street_fourthavenue", 28, 10990d),
                new BuildingAsk("ba:street_side", 9, 4000d),
                new BuildingAsk(bought.Street, bought.Number, 10d),
            });
        var steps = Roadmap.Order(api, scored, review, plan, days, 3);

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.NotEmpty(scored);
        Assert.True(bought.Bought);
        Assert.Equal(pin.ShippedBuild, api.Build);
        Assert.Equal("ba:street_side 9", review.Move!.Destination);
        Assert.True(plan.Bought);
        Assert.Equal("ba:street_fourthavenue", plan.Street);
        Assert.Equal(10d, plan.Days);
        Assert.Equal("close", steps[0].Kind);
        Assert.Equal("ba:street_side", steps[0].Street);
        Assert.Equal(9, steps[0].Number);
        Assert.Equal(5000d, steps[0].DailyGain);
        Assert.Equal("open", steps[1].Kind);
        Assert.Equal(1099d, steps[1].DailyGain);
        Assert.NotEqual(plan.Street, steps[0].Street);
        Assert.DoesNotContain("Reassign", steps[0].Explain);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_payback_roadmap_a_tie_keeps_the_later_step()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap tie
        var pin = PinDlls();
        using var dir = new TempLog();
        var canonical = ChainDays()[0].Rows[0];
        var days = new[]
        {
            new FinancialDay(
                1013,
                new[]
                {
                    canonical,
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -1099f),
                }),
        };
        var stops = new[]
        {
            ChainStops()[0],
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_side 9",
                "ba:itemname_cheapgift",
                null,
                ""),
        };
        var holds = new[]
        {
            ChainHolds()[0],
            new WarehouseHold("W2", "ba:street_b", 2, "ba:itemname_cheapgift", 20),
        };

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(run, Array.Empty<StaffOffer>());
        var clusters = Advisor.StreetClusters(Array.Empty<PortfolioBusiness>());
        var clone = Layout.Lock(staff, clusters, Array.Empty<ChainLayout>());
        var closed = Close.Lock(clone, Array.Empty<LedgerDay>(), 3, true, true);
        var bought = Buy.Lock(closed, Array.Empty<BuildingLot>());
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);
        var review = Logistics.FromRoutes(
            scored,
            api,
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));
        var plan = Payback.FromLedger(
            review,
            api,
            StatementDump.CsvPath(dir.Path),
            new[]
            {
                new BuildingAsk("ba:street_fourthavenue", 28, 10990d),
                new BuildingAsk("ba:street_side", 9, 4000d),
            });
        var steps = Roadmap.Order(api, scored, review, plan, days, 3);

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.NotEmpty(scored);
        Assert.False(bought.Bought);
        Assert.Equal(pin.ShippedBuild, api.Build);
        Assert.Equal("ba:street_side 9", review.Move!.Destination);
        Assert.True(plan.Bought);
        Assert.Equal("ba:street_fourthavenue", plan.Street);
        Assert.Equal(10d, plan.Days);
        Assert.Equal("open", steps[0].Kind);
        Assert.Equal("ba:street_fourthavenue", steps[0].Street);
        Assert.Equal(1099d, steps[0].DailyGain);
        Assert.Equal("close", steps[1].Kind);
        Assert.Equal("ba:street_side", steps[1].Street);
        Assert.Equal(1099d, steps[1].DailyGain);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_payback_roadmap_keeps_no_steps_when_the_panel_is_empty()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap empty
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = Array.Empty<FinancialDay>();
        var stops = new[]
        {
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_firstavenue 38",
                "ba:itemname_sodacan",
                1016,
                ""),
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_side 9",
                "ba:itemname_cheapgift",
                null,
                ""),
        };
        var holds = new[]
        {
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_sodacan", 45),
            new WarehouseHold("W2", "ba:street_b", 2, "ba:itemname_cheapgift", 20),
        };

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(item, Array.Empty<ItemSoldPerPriceEntry>());
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(run, Array.Empty<StaffOffer>());
        var clusters = Advisor.StreetClusters(Array.Empty<PortfolioBusiness>());
        var clone = Layout.Lock(staff, clusters, Array.Empty<ChainLayout>());
        var closed = Close.Lock(clone, Array.Empty<LedgerDay>(), 3, true, true);
        var bought = Buy.Lock(closed, Array.Empty<BuildingLot>());
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);
        var review = Logistics.FromRoutes(
            scored,
            api,
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));
        var asks = new[]
        {
            new BuildingAsk("ba:street_fourthavenue", 28, 10990d),
            new BuildingAsk("ba:street_side", 9, 4000d),
        };
        var plan = Payback.FromLedger(
            review,
            api,
            StatementDump.CsvPath(dir.Path),
            asks);
        var steps = Roadmap.Order(api, scored, review, plan, days, 3);
        var directApi = new ApiNote(pin.ShippedBuild, "", "", "", "");
        var directDays = new[]
        {
            new FinancialDay(
                1013,
                new[]
                {
                    new StatementRow("ba:street_fourthavenue", 28, 0f, 0f, 0f, 1099f, 0f, 0f, 0f),
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, 0f),
                }),
        };
        var directReview = Logistics.Review(directApi, stops, holds);
        var direct = Payback.Decide(directApi, directReview, directDays, asks);
        var directScored = CloseScore.Score(directApi, directDays);
        var directSteps = Roadmap.Order(directApi, directScored, directReview, direct, directDays, 3);

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.Empty(panel);
        Assert.Empty(scored);
        Assert.False(bought.Bought);
        Assert.Equal("", api.Build);
        Assert.False(plan.Bought);
        Assert.Empty(steps);
        Assert.True(direct.Bought);
        Assert.Equal("open", directSteps[0].Kind);
        Assert.Equal("ba:street_fourthavenue", directSteps[0].Street);
        Assert.Equal(28, directSteps[0].Number);
        Assert.Equal(1099d, directSteps[0].DailyGain);
        Assert.NotEqual(directReview.Move!.Destination, directSteps[0].Street + " " + directSteps[0].Number.ToString());
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_payback_roadmap_locks_the_first_action_when_the_route_is_supplied()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap supplied
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = ChainStops();
        var holds = ChainHolds();

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(run, Array.Empty<StaffOffer>());
        var clusters = Advisor.StreetClusters(Array.Empty<PortfolioBusiness>());
        var clone = Layout.Lock(staff, clusters, Array.Empty<ChainLayout>());
        var closed = Close.Lock(clone, Array.Empty<LedgerDay>(), 3, true, true);
        var bought = Buy.Lock(closed, Array.Empty<BuildingLot>());
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);
        var review = Logistics.FromRoutes(
            scored,
            api,
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));
        var plan = Payback.FromLedger(
            review,
            api,
            StatementDump.CsvPath(dir.Path),
            new[]
            {
                new BuildingAsk("ba:street_fourthavenue", 28, 2198d),
                new BuildingAsk("ba:street_side", 9, 1d),
            });
        var steps = Roadmap.Order(api, scored, review, plan, days, 3);

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.NotEmpty(scored);
        Assert.False(bought.Bought);
        Assert.Equal(pin.ShippedBuild, api.Build);
        Assert.Null(review.Move);
        Assert.Equal("W1", review.Services[0].Warehouse);
        Assert.True(plan.Bought);
        Assert.Equal(2d, plan.Days);
        Assert.Single(steps);
        Assert.Equal("open", steps[0].Kind);
        Assert.Equal("ba:street_fourthavenue", steps[0].Street);
        Assert.Equal(28, steps[0].Number);
        Assert.Equal(1099d, steps[0].DailyGain);
        Assert.NotEqual(plan.Days, steps[0].DailyGain);
        Assert.Equal(
            "Open ba:street_fourthavenue. Buy ba:street_fourthavenue 28. Daily gain 1099.",
            steps[0].Explain);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_payback_roadmap_shutdown_delivery_locks_that_move()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown → delivery
        var pin = PinDlls();
        using var dir = new TempLog();
        var canonical = ChainDays()[0].Rows[0];
        var days = new[]
        {
            new FinancialDay(
                1011,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -10f),
                }),
            new FinancialDay(
                1012,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -20f),
                }),
            new FinancialDay(
                1013,
                new[]
                {
                    canonical,
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -30f),
                }),
        };
        var stops = new[]
        {
            ChainStops()[0],
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_side 9",
                "ba:itemname_cheapgift",
                null,
                ""),
        };
        var holds = new[]
        {
            ChainHolds()[0],
            new WarehouseHold("W2", "ba:street_b", 2, "ba:itemname_cheapgift", 20),
        };

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var routesBefore = File.ReadAllText(RouteDump.RoutesPath(dir.Path));
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-soda", 40),
                new StaffOffer("headhunter", run.Family, "hh-soda", 4),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", staff.Role, "ba:street_main", 1),
                new PortfolioBusiness("Food 2", staff.Role, "ba:street_main", 2),
                new PortfolioBusiness("Food 3", staff.Role, "ba:street_main", 3),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", staff.Role, 1, "soda-line", 12, false),
                new ChainLayout("ba:street_main", staff.Role, 4, "", 0, true),
            });
        var closed = Close.Lock(
            clone,
            new[]
            {
                new LedgerDay(clone.Street, 2, clone.Family, 1011, -10f),
                new LedgerDay(clone.Street, 2, clone.Family, 1012, -4f),
                new LedgerDay(clone.Street, 2, clone.Family, 1013, -1f),
            },
            3,
            true,
            true);
        var bought = Buy.Lock(
            closed,
            new[]
            {
                new BuildingLot("hq", "ba:street_hq", 1),
                new BuildingLot("food", "ba:street_warehouse", 10),
                new BuildingLot("street", closed.Street, 20),
            });
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);
        var review = Logistics.FromRoutes(
            scored,
            api,
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));
        var plan = Payback.FromLedger(
            review,
            api,
            StatementDump.CsvPath(dir.Path),
            new[]
            {
                new BuildingAsk("ba:street_fourthavenue", 28, 10990d),
                new BuildingAsk("ba:street_side", 9, 4000d),
                new BuildingAsk(bought.Street, bought.Number, 10d),
            });
        var steps = Roadmap.Order(api, scored, review, plan, days, 3);
        var shut = Shutdown.Confirm(api, steps, steps[1].Street, steps[1].Number, true);
        var closedLine = Shutdown.Record(dir.Path, shut);
        var redo = Delivery.Confirm(api, review, review.Move!.Destination, true);
        var logged = Delivery.Record(dir.Path, redo);

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.NotEmpty(scored);
        Assert.True(bought.Bought);
        Assert.Equal("ba:street_hq", bought.Street);
        Assert.Equal(pin.ShippedBuild, api.Build);
        Assert.Equal(
            "UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination",
            api.Delivery);
        Assert.Equal("ba:street_side 9", review.Move.Destination);
        Assert.Equal("W1", review.Move.From);
        Assert.Equal("W2", review.Move.To);
        Assert.Equal("specialty", review.Move.Family);
        Assert.Equal("close", steps[1].Kind);
        Assert.True(shut.Closed);
        Assert.Equal(api.Close, shut.Call);
        Assert.True(redo.Moved);
        Assert.Equal(review.Move.Destination, redo.Destination);
        Assert.Equal(review.Move.From, redo.From);
        Assert.Equal(review.Move.To, redo.To);
        Assert.Equal(review.Move.Family, redo.Family);
        Assert.Equal(api.Delivery, redo.Call);
        Assert.NotEqual(shut.Call, redo.Call);
        Assert.Equal(
            "logistics.redone version=" + LoadLog.Version
                + " destination=ba:street_side 9 from=W1 to=W2 family=specialty call=UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination",
            logged);
        Assert.Contains(logged, File.ReadAllText(LoadLog.CeoLogPath(dir.Path)));
        Assert.Contains(closedLine, File.ReadAllText(LoadLog.CeoLogPath(dir.Path)));
        Assert.Equal(routesBefore, File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.Contains("ba:street_fourthstreet 46,ba:street_side 9,ba:itemname_cheapgift", routesBefore);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_payback_roadmap_shutdown_delivery_leaves_the_route_file()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown → delivery undo
        var pin = PinDlls();
        using var dir = new TempLog();
        var canonical = ChainDays()[0].Rows[0];
        var days = new[]
        {
            new FinancialDay(
                1011,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -10f),
                }),
            new FinancialDay(
                1012,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -20f),
                }),
            new FinancialDay(
                1013,
                new[]
                {
                    canonical,
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -30f),
                }),
        };
        var stops = new[]
        {
            ChainStops()[0],
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_side 9",
                "ba:itemname_cheapgift",
                null,
                ""),
        };
        var holds = new[]
        {
            ChainHolds()[0],
            new WarehouseHold("W2", "ba:street_b", 2, "ba:itemname_cheapgift", 20),
        };

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var routesBefore = File.ReadAllText(RouteDump.RoutesPath(dir.Path));
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-soda", 40),
                new StaffOffer("headhunter", run.Family, "hh-soda", 4),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", staff.Role, "ba:street_main", 1),
                new PortfolioBusiness("Food 2", staff.Role, "ba:street_main", 2),
                new PortfolioBusiness("Food 3", staff.Role, "ba:street_main", 3),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", staff.Role, 1, "soda-line", 12, false),
                new ChainLayout("ba:street_main", staff.Role, 4, "", 0, true),
            });
        var closed = Close.Lock(
            clone,
            new[]
            {
                new LedgerDay(clone.Street, 2, clone.Family, 1011, -10f),
                new LedgerDay(clone.Street, 2, clone.Family, 1012, -4f),
                new LedgerDay(clone.Street, 2, clone.Family, 1013, -1f),
            },
            3,
            true,
            true);
        var bought = Buy.Lock(
            closed,
            new[]
            {
                new BuildingLot("hq", "ba:street_hq", 1),
                new BuildingLot("food", "ba:street_warehouse", 10),
                new BuildingLot("street", closed.Street, 20),
            });
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);
        var review = Logistics.FromRoutes(
            scored,
            api,
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));
        var plan = Payback.FromLedger(
            review,
            api,
            StatementDump.CsvPath(dir.Path),
            new[]
            {
                new BuildingAsk("ba:street_fourthavenue", 28, 10990d),
                new BuildingAsk("ba:street_side", 9, 4000d),
                new BuildingAsk(bought.Street, bought.Number, 10d),
            });
        var steps = Roadmap.Order(api, scored, review, plan, days, 3);
        var shut = Shutdown.Confirm(api, steps, steps[1].Street, steps[1].Number, true);
        Shutdown.Record(dir.Path, shut);
        var other = Delivery.Confirm(api, review, "ba:street_firstavenue 38", true);
        var held = Delivery.Confirm(api, review, review.Move!.Destination, false);
        var redo = Delivery.Confirm(api, review, review.Move.Destination, true);
        var logged = Delivery.Record(dir.Path, redo);
        var undone = Delivery.Reload(redo);
        var ledger = File.ReadAllText(LoadLog.CeoLogPath(dir.Path));

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.Equal(pin.ShippedBuild, api.Build);
        Assert.True(shut.Closed);
        Assert.Equal(api.Close, shut.Call);
        Assert.False(other.Moved);
        Assert.Equal("", other.Destination);
        Assert.Equal("", other.Call);
        Assert.False(held.Moved);
        Assert.Equal("", held.To);
        Assert.True(redo.Moved);
        Assert.Equal("W1", redo.From);
        Assert.Equal("W2", redo.To);
        Assert.Equal("specialty", redo.Family);
        Assert.Equal(api.Delivery, redo.Call);
        Assert.DoesNotContain("ShutdownBusiness", redo.Call);
        Assert.False(undone.Moved);
        Assert.Equal("", undone.Destination);
        Assert.Equal("", undone.Call);
        Assert.Equal(routesBefore, File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.Contains("ba:street_side", File.ReadAllText(StatementDump.CsvPath(dir.Path)));
        Assert.Contains(logged, ledger);
        Assert.Contains("store.closed", ledger);
        Assert.Equal("", Delivery.Record(dir.Path, other));
        Assert.Equal("", Delivery.Record(dir.Path, undone));
        Assert.Equal(1, CountLines(File.ReadAllText(LoadLog.CeoLogPath(dir.Path)), "logistics.redone"));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_payback_roadmap_shutdown_delivery_stays_when_the_route_is_supplied()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown → delivery supplied
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = ChainStops();
        var holds = ChainHolds();

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var routesBefore = File.ReadAllText(RouteDump.RoutesPath(dir.Path));
        var warehousesBefore = File.ReadAllText(RouteDump.WarehousesPath(dir.Path));
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(run, Array.Empty<StaffOffer>());
        var clusters = Advisor.StreetClusters(Array.Empty<PortfolioBusiness>());
        var clone = Layout.Lock(staff, clusters, Array.Empty<ChainLayout>());
        var closed = Close.Lock(clone, Array.Empty<LedgerDay>(), 3, true, true);
        var bought = Buy.Lock(closed, Array.Empty<BuildingLot>());
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);
        var review = Logistics.FromRoutes(
            scored,
            api,
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));
        var plan = Payback.FromLedger(
            review,
            api,
            StatementDump.CsvPath(dir.Path),
            new[]
            {
                new BuildingAsk("ba:street_fourthavenue", 28, 2198d),
                new BuildingAsk("ba:street_side", 9, 1d),
            });
        var steps = Roadmap.Order(api, scored, review, plan, days, 3);
        var shut = Shutdown.Confirm(api, steps, steps[0].Street, steps[0].Number, true);
        var redo = Delivery.Confirm(api, review, "ba:street_firstavenue 38", true);
        var logged = Delivery.Record(dir.Path, redo);

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.Equal(pin.ShippedBuild, api.Build);
        Assert.Null(review.Move);
        Assert.Equal("open", steps[0].Kind);
        Assert.False(shut.Closed);
        Assert.Equal("", shut.Call);
        Assert.False(redo.Moved);
        Assert.Equal("", redo.Destination);
        Assert.Equal("", redo.From);
        Assert.Equal("", redo.To);
        Assert.Equal("", redo.Call);
        Assert.DoesNotContain("AddDestination", redo.Call);
        Assert.Equal("", logged);
        Assert.DoesNotContain("logistics.redone", File.ReadAllText(LoadLog.CeoLogPath(dir.Path)));
        Assert.Equal(routesBefore, File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.Equal(warehousesBefore, File.ReadAllText(RouteDump.WarehousesPath(dir.Path)));
        Assert.Contains("ba:street_fourthstreet 46,ba:street_firstavenue 38", routesBefore);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_payback_roadmap_shutdown_delivery_apply_locks_that_price_and_that_plan()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown → delivery → apply
        var pin = PinDlls();
        using var dir = new TempLog();
        var canonical = ChainDays()[0].Rows[0];
        var days = new[]
        {
            new FinancialDay(
                1011,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -10f),
                }),
            new FinancialDay(
                1012,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -20f),
                }),
            new FinancialDay(
                1013,
                new[]
                {
                    canonical,
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -30f),
                }),
        };
        var stops = new[]
        {
            ChainStops()[0],
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_side 9",
                "ba:itemname_cheapgift",
                null,
                ""),
        };
        var holds = new[]
        {
            ChainHolds()[0],
            new WarehouseHold("W2", "ba:street_b", 2, "ba:itemname_cheapgift", 20),
        };

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var statementsBefore = File.ReadAllText(StatementDump.CsvPath(dir.Path));
        var routesBefore = File.ReadAllText(RouteDump.RoutesPath(dir.Path));
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-soda", 40),
                new StaffOffer("headhunter", run.Family, "hh-soda", 4),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", staff.Role, "ba:street_main", 1),
                new PortfolioBusiness("Food 2", staff.Role, "ba:street_main", 2),
                new PortfolioBusiness("Food 3", staff.Role, "ba:street_main", 3),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", staff.Role, 1, "soda-line", 12, false),
                new ChainLayout("ba:street_main", staff.Role, 4, "", 0, true),
            });
        var closed = Close.Lock(
            clone,
            new[]
            {
                new LedgerDay(clone.Street, 2, clone.Family, 1011, -10f),
                new LedgerDay(clone.Street, 2, clone.Family, 1012, -4f),
                new LedgerDay(clone.Street, 2, clone.Family, 1013, -1f),
            },
            3,
            true,
            true);
        var bought = Buy.Lock(
            closed,
            new[]
            {
                new BuildingLot("hq", "ba:street_hq", 1),
                new BuildingLot("food", "ba:street_warehouse", 10),
                new BuildingLot("street", closed.Street, 20),
            });
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);
        var review = Logistics.FromRoutes(
            scored,
            api,
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));
        var plan = Payback.FromLedger(
            review,
            api,
            StatementDump.CsvPath(dir.Path),
            new[]
            {
                new BuildingAsk("ba:street_fourthavenue", 28, 10990d),
                new BuildingAsk("ba:street_side", 9, 4000d),
                new BuildingAsk(bought.Street, bought.Number, 10d),
            });
        var steps = Roadmap.Order(api, scored, review, plan, days, 3);
        var shut = Shutdown.Confirm(api, steps, steps[1].Street, steps[1].Number, true);
        var redo = Delivery.Confirm(api, review, review.Move!.Destination, true);
        var priced = Apply.ConfirmPrice(api, price, price.Item, true);
        var staffed = Apply.ConfirmStaff(api, staff, staff.PlanId, true);
        var priceLine = Apply.RecordPrice(dir.Path, priced);
        var staffLine = Apply.RecordStaff(dir.Path, staffed);

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.Equal(pin.ShippedBuild, api.Build);
        Assert.True(shut.Closed);
        Assert.True(redo.Moved);
        Assert.Equal(review.Move.Destination, redo.Destination);
        Assert.Equal("ba:itemname_sodacan", item);
        Assert.Equal(item, price.Item);
        Assert.Equal(2.75f, price.Price);
        Assert.Equal("food", run.Family);
        Assert.Equal(run.Family, staff.Role);
        Assert.Equal("hr", staff.Kind);
        Assert.Equal("hr-soda", staff.PlanId);
        Assert.Equal(40, staff.Amount);
        Assert.NotEqual("hh-soda", staff.PlanId);
        Assert.True(priced.Applied);
        Assert.Equal(price.Item, priced.Item);
        Assert.Equal(price.Price, priced.Price);
        Assert.Equal(api.Price, priced.Call);
        Assert.Equal("ba:itemname_sodacan", priced.Item);
        Assert.Equal(2.75f, priced.Price);
        Assert.True(staffed.Applied);
        Assert.Equal(staff.Role, staffed.Role);
        Assert.Equal(staff.Kind, staffed.Kind);
        Assert.Equal(staff.PlanId, staffed.PlanId);
        Assert.Equal(staff.Amount, staffed.Amount);
        Assert.Equal("food", staffed.Role);
        Assert.Equal("hr-soda", staffed.PlanId);
        Assert.Equal(40, staffed.Amount);
        Assert.Equal("", staffed.Call);
        Assert.NotEqual(redo.Call, priced.Call);
        Assert.NotEqual(shut.Call, priced.Call);
        Assert.NotEqual(priced.Item, staffed.PlanId);
        Assert.Equal(
            "price.applied version=" + LoadLog.Version
                + " item=ba:itemname_sodacan price=2.75 call=Buildings.Office.Headquarters.PricingManagerPlan.SetPrice",
            priceLine);
        Assert.Equal(
            "staff.applied version=" + LoadLog.Version
                + " role=food kind=hr plan=hr-soda amount=40",
            staffLine);
        Assert.Contains(priceLine, File.ReadAllText(LoadLog.CeoLogPath(dir.Path)));
        Assert.Contains(staffLine, File.ReadAllText(LoadLog.CeoLogPath(dir.Path)));
        Assert.Equal(statementsBefore, File.ReadAllText(StatementDump.CsvPath(dir.Path)));
        Assert.Equal(routesBefore, File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.DoesNotContain("price=2.75", routesBefore);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_payback_roadmap_shutdown_delivery_apply_undo_leaves_the_files()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown → delivery → apply undo
        var pin = PinDlls();
        using var dir = new TempLog();
        var canonical = ChainDays()[0].Rows[0];
        var days = new[]
        {
            new FinancialDay(
                1011,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -10f),
                }),
            new FinancialDay(
                1012,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -20f),
                }),
            new FinancialDay(
                1013,
                new[]
                {
                    canonical,
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -30f),
                }),
        };
        var stops = new[]
        {
            ChainStops()[0],
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_side 9",
                "ba:itemname_cheapgift",
                null,
                ""),
        };
        var holds = new[]
        {
            ChainHolds()[0],
            new WarehouseHold("W2", "ba:street_b", 2, "ba:itemname_cheapgift", 20),
        };

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var statementsBefore = File.ReadAllText(StatementDump.CsvPath(dir.Path));
        var routesBefore = File.ReadAllText(RouteDump.RoutesPath(dir.Path));
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
                new ItemSoldPerPriceEntry(item, 9.5f, 1),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-soda", 40),
                new StaffOffer("headhunter", run.Family, "hh-soda", 4),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", staff.Role, "ba:street_main", 1),
                new PortfolioBusiness("Food 2", staff.Role, "ba:street_main", 2),
                new PortfolioBusiness("Food 3", staff.Role, "ba:street_main", 3),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", staff.Role, 1, "soda-line", 12, false),
                new ChainLayout("ba:street_main", staff.Role, 4, "", 0, true),
            });
        var closed = Close.Lock(
            clone,
            new[]
            {
                new LedgerDay(clone.Street, 2, clone.Family, 1011, -10f),
                new LedgerDay(clone.Street, 2, clone.Family, 1012, -4f),
                new LedgerDay(clone.Street, 2, clone.Family, 1013, -1f),
            },
            3,
            true,
            true);
        var bought = Buy.Lock(
            closed,
            new[]
            {
                new BuildingLot("hq", "ba:street_hq", 1),
                new BuildingLot("food", "ba:street_warehouse", 10),
                new BuildingLot("street", closed.Street, 20),
            });
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);
        var review = Logistics.FromRoutes(
            scored,
            api,
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));
        var plan = Payback.FromLedger(
            review,
            api,
            StatementDump.CsvPath(dir.Path),
            new[]
            {
                new BuildingAsk("ba:street_fourthavenue", 28, 10990d),
                new BuildingAsk("ba:street_side", 9, 4000d),
                new BuildingAsk(bought.Street, bought.Number, 10d),
            });
        var steps = Roadmap.Order(api, scored, review, plan, days, 3);
        Shutdown.Confirm(api, steps, steps[1].Street, steps[1].Number, true);
        Delivery.Confirm(api, review, review.Move!.Destination, true);
        var otherItem = Apply.ConfirmPrice(api, price, "ba:itemname_cheapgift", true);
        var heldPrice = Apply.ConfirmPrice(api, price, price.Item, false);
        var otherPlan = Apply.ConfirmStaff(api, staff, "hh-soda", true);
        var heldStaff = Apply.ConfirmStaff(api, staff, staff.PlanId, false);
        var priced = Apply.ConfirmPrice(api, price, price.Item, true);
        var staffed = Apply.ConfirmStaff(api, staff, staff.PlanId, true);
        var priceLine = Apply.RecordPrice(dir.Path, priced);
        var staffLine = Apply.RecordStaff(dir.Path, staffed);
        var undonePrice = Apply.ReloadPrice(priced);
        var undoneStaff = Apply.ReloadStaff(staffed);
        var ledger = File.ReadAllText(LoadLog.CeoLogPath(dir.Path));

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.Equal(pin.ShippedBuild, api.Build);
        Assert.Equal(2.75f, price.Price);
        Assert.Equal("hr-soda", staff.PlanId);
        Assert.False(otherItem.Applied);
        Assert.Equal("", otherItem.Item);
        Assert.Equal("", otherItem.Call);
        Assert.False(heldPrice.Applied);
        Assert.Equal(0f, heldPrice.Price);
        Assert.False(otherPlan.Applied);
        Assert.Equal("", otherPlan.PlanId);
        Assert.Equal(0, otherPlan.Amount);
        Assert.False(heldStaff.Applied);
        Assert.Equal("", heldStaff.Kind);
        Assert.True(priced.Applied);
        Assert.Equal(2.75f, priced.Price);
        Assert.Equal(api.Price, priced.Call);
        Assert.True(staffed.Applied);
        Assert.Equal(40, staffed.Amount);
        Assert.Equal("food", staffed.Role);
        Assert.False(undonePrice.Applied);
        Assert.Equal("", undonePrice.Item);
        Assert.Equal("", undonePrice.Call);
        Assert.False(undoneStaff.Applied);
        Assert.Equal("", undoneStaff.PlanId);
        Assert.Equal(statementsBefore, File.ReadAllText(StatementDump.CsvPath(dir.Path)));
        Assert.Equal(routesBefore, File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.Contains(priceLine, ledger);
        Assert.Contains(staffLine, ledger);
        Assert.Equal("", Apply.RecordPrice(dir.Path, otherItem));
        Assert.Equal("", Apply.RecordStaff(dir.Path, otherPlan));
        Assert.Equal("", Apply.RecordPrice(dir.Path, undonePrice));
        Assert.Equal("", Apply.RecordStaff(dir.Path, undoneStaff));
        Assert.Equal(1, CountLines(File.ReadAllText(LoadLog.CeoLogPath(dir.Path)), "price.applied"));
        Assert.Equal(1, CountLines(File.ReadAllText(LoadLog.CeoLogPath(dir.Path)), "staff.applied"));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_payback_roadmap_shutdown_delivery_apply_stays_when_the_price_and_plan_are_empty()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown → delivery → apply empty
        var pin = PinDlls();
        using var dir = new TempLog();
        var days = ChainDays();
        var stops = ChainStops();
        var holds = ChainHolds();

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var statementsBefore = File.ReadAllText(StatementDump.CsvPath(dir.Path));
        var routesBefore = File.ReadAllText(RouteDump.RoutesPath(dir.Path));
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 0),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(run, Array.Empty<StaffOffer>());
        var clusters = Advisor.StreetClusters(Array.Empty<PortfolioBusiness>());
        var clone = Layout.Lock(staff, clusters, Array.Empty<ChainLayout>());
        var closed = Close.Lock(clone, Array.Empty<LedgerDay>(), 3, true, true);
        var bought = Buy.Lock(closed, Array.Empty<BuildingLot>());
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);
        var review = Logistics.FromRoutes(
            scored,
            api,
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));
        var plan = Payback.FromLedger(
            review,
            api,
            StatementDump.CsvPath(dir.Path),
            new[]
            {
                new BuildingAsk("ba:street_fourthavenue", 28, 2198d),
                new BuildingAsk("ba:street_side", 9, 1d),
            });
        var steps = Roadmap.Order(api, scored, review, plan, days, 3);
        var shut = Shutdown.Confirm(api, steps, steps[0].Street, steps[0].Number, true);
        var redo = Delivery.Confirm(api, review, "ba:street_firstavenue 38", true);
        var priced = Apply.ConfirmPrice(api, price, price.Item, true);
        var staffed = Apply.ConfirmStaff(api, staff, staff.PlanId, true);
        var priceLine = Apply.RecordPrice(dir.Path, priced);
        var staffLine = Apply.RecordStaff(dir.Path, staffed);

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.Equal(pin.ShippedBuild, api.Build);
        Assert.Equal("open", steps[0].Kind);
        Assert.False(shut.Closed);
        Assert.Null(review.Move);
        Assert.False(redo.Moved);
        Assert.Equal(0f, price.Price);
        Assert.Equal("", staff.PlanId);
        Assert.Equal(0, staff.Amount);
        Assert.False(priced.Applied);
        Assert.Equal("", priced.Item);
        Assert.Equal(0f, priced.Price);
        Assert.Equal("", priced.Call);
        Assert.False(staffed.Applied);
        Assert.Equal("", staffed.Role);
        Assert.Equal("", staffed.PlanId);
        Assert.Equal(0, staffed.Amount);
        Assert.Equal("", priceLine);
        Assert.Equal("", staffLine);
        Assert.DoesNotContain("price.applied", File.ReadAllText(LoadLog.CeoLogPath(dir.Path)));
        Assert.DoesNotContain("staff.applied", File.ReadAllText(LoadLog.CeoLogPath(dir.Path)));
        Assert.Equal(statementsBefore, File.ReadAllText(StatementDump.CsvPath(dir.Path)));
        Assert.Equal(routesBefore, File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.False(bought.Bought);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_payback_roadmap_shutdown_delivery_apply_keeps_the_unlimited_plan()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown → delivery → apply unlimited
        var pin = PinDlls();
        using var dir = new TempLog();
        var canonical = ChainDays()[0].Rows[0];
        var days = new[]
        {
            new FinancialDay(
                1011,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -10f),
                }),
            new FinancialDay(
                1012,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -20f),
                }),
            new FinancialDay(
                1013,
                new[]
                {
                    canonical,
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -30f),
                }),
        };
        var stops = new[]
        {
            ChainStops()[0],
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_side 9",
                "ba:itemname_cheapgift",
                null,
                ""),
        };
        var holds = new[]
        {
            ChainHolds()[0],
            new WarehouseHold("W2", "ba:street_b", 2, "ba:itemname_cheapgift", 20),
        };

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var routesBefore = File.ReadAllText(RouteDump.RoutesPath(dir.Path));
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-soda", 40),
                new StaffOffer("headhunter", run.Family, "hh-open", -1),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", staff.Role, "ba:street_main", 1),
                new PortfolioBusiness("Food 2", staff.Role, "ba:street_main", 2),
                new PortfolioBusiness("Food 3", staff.Role, "ba:street_main", 3),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", staff.Role, 1, "soda-line", 12, false),
                new ChainLayout("ba:street_main", staff.Role, 4, "", 0, true),
            });
        var closed = Close.Lock(
            clone,
            new[]
            {
                new LedgerDay(clone.Street, 2, clone.Family, 1011, -10f),
                new LedgerDay(clone.Street, 2, clone.Family, 1012, -4f),
                new LedgerDay(clone.Street, 2, clone.Family, 1013, -1f),
            },
            3,
            true,
            true);
        var bought = Buy.Lock(
            closed,
            new[]
            {
                new BuildingLot("hq", "ba:street_hq", 1),
                new BuildingLot("food", "ba:street_warehouse", 10),
                new BuildingLot("street", closed.Street, 20),
            });
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);
        var review = Logistics.FromRoutes(
            scored,
            api,
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));
        var plan = Payback.FromLedger(
            review,
            api,
            StatementDump.CsvPath(dir.Path),
            new[]
            {
                new BuildingAsk("ba:street_fourthavenue", 28, 10990d),
                new BuildingAsk("ba:street_side", 9, 4000d),
                new BuildingAsk(bought.Street, bought.Number, 10d),
            });
        var steps = Roadmap.Order(api, scored, review, plan, days, 3);
        var shut = Shutdown.Confirm(api, steps, steps[1].Street, steps[1].Number, true);
        var redo = Delivery.Confirm(api, review, review.Move!.Destination, true);
        var priced = Apply.ConfirmPrice(api, price, price.Item, true);
        var staffed = Apply.ConfirmStaff(api, staff, staff.PlanId, true);
        var staffLine = Apply.RecordStaff(dir.Path, staffed);

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.Equal(pin.ShippedBuild, api.Build);
        Assert.True(shut.Closed);
        Assert.True(redo.Moved);
        Assert.Equal("headhunter", staff.Kind);
        Assert.Equal("hh-open", staff.PlanId);
        Assert.Equal(-1, staff.Amount);
        Assert.NotEqual("hr-soda", staff.PlanId);
        Assert.True(priced.Applied);
        Assert.Equal(2.75f, priced.Price);
        Assert.Equal(api.Price, priced.Call);
        Assert.True(staffed.Applied);
        Assert.Equal(staff.Kind, staffed.Kind);
        Assert.Equal(staff.PlanId, staffed.PlanId);
        Assert.Equal(staff.Amount, staffed.Amount);
        Assert.Equal("food", staffed.Role);
        Assert.Equal("headhunter", staffed.Kind);
        Assert.Equal("hh-open", staffed.PlanId);
        Assert.Equal(-1, staffed.Amount);
        Assert.Equal("", staffed.Call);
        Assert.Equal(
            "staff.applied version=" + LoadLog.Version
                + " role=food kind=headhunter plan=hh-open amount=-1",
            staffLine);
        Assert.DoesNotContain("hr-soda", staffLine);
        Assert.DoesNotContain("SetPrice", staffLine);
        Assert.Equal(routesBefore, File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_payback_roadmap_shutdown_delivery_apply_offer_locks_that_building()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown → delivery → apply → offer
        var pin = PinDlls();
        using var dir = new TempLog();
        var canonical = ChainDays()[0].Rows[0];
        var days = new[]
        {
            new FinancialDay(
                1011,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -10f),
                }),
            new FinancialDay(
                1012,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -20f),
                }),
            new FinancialDay(
                1013,
                new[]
                {
                    canonical,
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -30f),
                }),
        };
        var stops = new[]
        {
            ChainStops()[0],
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_side 9",
                "ba:itemname_cheapgift",
                null,
                ""),
        };
        var holds = new[]
        {
            ChainHolds()[0],
            new WarehouseHold("W2", "ba:street_b", 2, "ba:itemname_cheapgift", 20),
        };

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var statementsBefore = File.ReadAllText(StatementDump.CsvPath(dir.Path));
        var routesBefore = File.ReadAllText(RouteDump.RoutesPath(dir.Path));
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-soda", 40),
                new StaffOffer("headhunter", run.Family, "hh-soda", 4),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", staff.Role, "ba:street_main", 1),
                new PortfolioBusiness("Food 2", staff.Role, "ba:street_main", 2),
                new PortfolioBusiness("Food 3", staff.Role, "ba:street_main", 3),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", staff.Role, 1, "soda-line", 12, false),
                new ChainLayout("ba:street_main", staff.Role, 4, "", 0, true),
            });
        var closed = Close.Lock(
            clone,
            new[]
            {
                new LedgerDay(clone.Street, 2, clone.Family, 1011, -10f),
                new LedgerDay(clone.Street, 2, clone.Family, 1012, -4f),
                new LedgerDay(clone.Street, 2, clone.Family, 1013, -1f),
            },
            3,
            true,
            true);
        var bought = Buy.Lock(
            closed,
            new[]
            {
                new BuildingLot("hq", "ba:street_hq", 1),
                new BuildingLot("food", "ba:street_warehouse", 10),
                new BuildingLot("street", closed.Street, 20),
            });
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);
        var review = Logistics.FromRoutes(
            scored,
            api,
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));
        var plan = Payback.FromLedger(
            review,
            api,
            StatementDump.CsvPath(dir.Path),
            new[]
            {
                new BuildingAsk("ba:street_fourthavenue", 28, 10990d),
                new BuildingAsk("ba:street_side", 9, 4000d),
                new BuildingAsk(bought.Street, bought.Number, 10d),
            });
        var steps = Roadmap.Order(api, scored, review, plan, days, 3);
        var shut = Shutdown.Confirm(api, steps, steps[1].Street, steps[1].Number, true);
        var redo = Delivery.Confirm(api, review, review.Move!.Destination, true);
        var priced = Apply.ConfirmPrice(api, price, price.Item, true);
        var staffed = Apply.ConfirmStaff(api, staff, staff.PlanId, true);
        Apply.RecordPrice(dir.Path, priced);
        Apply.RecordStaff(dir.Path, staffed);
        var offered = Offer.Confirm(api, steps, steps[0].Street, steps[0].Number, true);
        var offerLine = Offer.Record(dir.Path, offered);

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.Equal(pin.ShippedBuild, api.Build);
        Assert.Equal("open", steps[0].Kind);
        Assert.True(plan.Bought);
        Assert.Equal(plan.Street, steps[0].Street);
        Assert.Equal(plan.Number, steps[0].Number);
        Assert.True(shut.Closed);
        Assert.True(redo.Moved);
        Assert.True(priced.Applied);
        Assert.True(staffed.Applied);
        Assert.True(offered.Offered);
        Assert.Equal("ba:street_fourthavenue", offered.Street);
        Assert.Equal(28, offered.Number);
        Assert.Equal(steps[0].Street, offered.Street);
        Assert.Equal(steps[0].Number, offered.Number);
        Assert.Equal(plan.Street, offered.Street);
        Assert.Equal(plan.Number, offered.Number);
        Assert.NotEqual(bought.Street, offered.Street);
        Assert.NotEqual(steps[1].Street, offered.Street);
        Assert.NotEqual(shut.Number, offered.Number);
        Assert.Equal(api.Buy, offered.Call);
        Assert.Equal("BizManPresentation.SendBuyBuildingOffer", offered.Call);
        Assert.NotEqual(shut.Call, offered.Call);
        Assert.NotEqual(priced.Call, offered.Call);
        Assert.NotEqual(redo.Call, offered.Call);
        Assert.DoesNotContain("ShutdownBusiness", offered.Call);
        Assert.DoesNotContain("SetPrice", offered.Call);
        Assert.DoesNotContain("AddDestination", offered.Call);
        Assert.DoesNotContain("SendBuyBuildingOffer", steps[0].Explain);
        Assert.Equal(
            "building.offered version=" + LoadLog.Version
                + " street=ba:street_fourthavenue number=28 call=BizManPresentation.SendBuyBuildingOffer",
            offerLine);
        Assert.Contains(offerLine, File.ReadAllText(LoadLog.CeoLogPath(dir.Path)));
        Assert.Equal(1, CountLines(File.ReadAllText(LoadLog.CeoLogPath(dir.Path)), "building.offered"));
        Assert.Equal(statementsBefore, File.ReadAllText(StatementDump.CsvPath(dir.Path)));
        Assert.Equal(routesBefore, File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.DoesNotContain("number=28", routesBefore);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_payback_roadmap_shutdown_delivery_apply_offer_undo_leaves_the_files()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown → delivery → apply → offer undo
        var pin = PinDlls();
        using var dir = new TempLog();
        var canonical = ChainDays()[0].Rows[0];
        var days = new[]
        {
            new FinancialDay(
                1011,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -10f),
                }),
            new FinancialDay(
                1012,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -20f),
                }),
            new FinancialDay(
                1013,
                new[]
                {
                    canonical,
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -30f),
                }),
        };
        var stops = new[]
        {
            ChainStops()[0],
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_side 9",
                "ba:itemname_cheapgift",
                null,
                ""),
        };
        var holds = new[]
        {
            ChainHolds()[0],
            new WarehouseHold("W2", "ba:street_b", 2, "ba:itemname_cheapgift", 20),
        };

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var statementsBefore = File.ReadAllText(StatementDump.CsvPath(dir.Path));
        var routesBefore = File.ReadAllText(RouteDump.RoutesPath(dir.Path));
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-soda", 40),
                new StaffOffer("headhunter", run.Family, "hh-soda", 4),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", staff.Role, "ba:street_main", 1),
                new PortfolioBusiness("Food 2", staff.Role, "ba:street_main", 2),
                new PortfolioBusiness("Food 3", staff.Role, "ba:street_main", 3),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", staff.Role, 1, "soda-line", 12, false),
                new ChainLayout("ba:street_main", staff.Role, 4, "", 0, true),
            });
        var closed = Close.Lock(
            clone,
            new[]
            {
                new LedgerDay(clone.Street, 2, clone.Family, 1011, -10f),
                new LedgerDay(clone.Street, 2, clone.Family, 1012, -4f),
                new LedgerDay(clone.Street, 2, clone.Family, 1013, -1f),
            },
            3,
            true,
            true);
        var bought = Buy.Lock(
            closed,
            new[]
            {
                new BuildingLot("hq", "ba:street_hq", 1),
                new BuildingLot("food", "ba:street_warehouse", 10),
                new BuildingLot("street", closed.Street, 20),
            });
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);
        var review = Logistics.FromRoutes(
            scored,
            api,
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));
        var plan = Payback.FromLedger(
            review,
            api,
            StatementDump.CsvPath(dir.Path),
            new[]
            {
                new BuildingAsk("ba:street_fourthavenue", 28, 10990d),
                new BuildingAsk("ba:street_side", 9, 4000d),
                new BuildingAsk(bought.Street, bought.Number, 10d),
            });
        var steps = Roadmap.Order(api, scored, review, plan, days, 3);
        var shut = Shutdown.Confirm(api, steps, steps[1].Street, steps[1].Number, true);
        var redo = Delivery.Confirm(api, review, review.Move!.Destination, true);
        var priced = Apply.ConfirmPrice(api, price, price.Item, true);
        var staffed = Apply.ConfirmStaff(api, staff, staff.PlanId, true);
        Apply.RecordPrice(dir.Path, priced);
        Apply.RecordStaff(dir.Path, staffed);
        var offered = Offer.Confirm(api, steps, plan.Street, plan.Number, true);
        var offerLine = Offer.Record(dir.Path, offered);
        var undone = Offer.Reload(offered);

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.Equal(pin.ShippedBuild, api.Build);
        Assert.True(shut.Closed);
        Assert.True(redo.Moved);
        Assert.True(priced.Applied);
        Assert.True(staffed.Applied);
        Assert.Equal("open", steps[0].Kind);
        Assert.True(offered.Offered);
        Assert.Equal("ba:street_fourthavenue", offered.Street);
        Assert.Equal(28, offered.Number);
        Assert.Equal(plan.Street, offered.Street);
        Assert.Equal(plan.Number, offered.Number);
        Assert.Equal(steps[0].Street, offered.Street);
        Assert.Equal(steps[0].Number, offered.Number);
        Assert.False(undone.Offered);
        Assert.Equal("", undone.Street);
        Assert.Equal(0, undone.Number);
        Assert.Equal("", undone.Call);
        Assert.NotEqual(offered.Street, undone.Street);
        Assert.NotEqual(bought.Street, offered.Street);
        Assert.Equal("", Offer.Record(dir.Path, undone));
        Assert.Equal(1, CountLines(File.ReadAllText(LoadLog.CeoLogPath(dir.Path)), "building.offered"));
        Assert.Contains(offerLine, File.ReadAllText(LoadLog.CeoLogPath(dir.Path)));
        Assert.Equal(statementsBefore, File.ReadAllText(StatementDump.CsvPath(dir.Path)));
        Assert.Equal(routesBefore, File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_payback_roadmap_shutdown_delivery_apply_offer_clone_locks_that_street_and_blueprint()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown → delivery → apply → offer → clone
        var pin = PinDlls();
        using var dir = new TempLog();
        var canonical = ChainDays()[0].Rows[0];
        var days = new[]
        {
            new FinancialDay(
                1011,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -10f),
                }),
            new FinancialDay(
                1012,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -20f),
                }),
            new FinancialDay(
                1013,
                new[]
                {
                    canonical,
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -30f),
                }),
        };
        var stops = new[]
        {
            ChainStops()[0],
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_side 9",
                "ba:itemname_cheapgift",
                null,
                ""),
        };
        var holds = new[]
        {
            ChainHolds()[0],
            new WarehouseHold("W2", "ba:street_b", 2, "ba:itemname_cheapgift", 20),
        };

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var statementsBefore = File.ReadAllText(StatementDump.CsvPath(dir.Path));
        var routesBefore = File.ReadAllText(RouteDump.RoutesPath(dir.Path));
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-soda", 40),
                new StaffOffer("headhunter", run.Family, "hh-soda", 4),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", staff.Role, "ba:street_main", 1),
                new PortfolioBusiness("Food 2", staff.Role, "ba:street_main", 2),
                new PortfolioBusiness("Food 3", staff.Role, "ba:street_main", 3),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", staff.Role, 1, "soda-line", 12, false),
                new ChainLayout("ba:street_main", staff.Role, 4, "", 0, true),
            });
        var closed = Close.Lock(
            clone,
            new[]
            {
                new LedgerDay(clone.Street, 2, clone.Family, 1011, -10f),
                new LedgerDay(clone.Street, 2, clone.Family, 1012, -4f),
                new LedgerDay(clone.Street, 2, clone.Family, 1013, -1f),
            },
            3,
            true,
            true);
        var bought = Buy.Lock(
            closed,
            new[]
            {
                new BuildingLot("hq", "ba:street_hq", 1),
                new BuildingLot("food", "ba:street_warehouse", 10),
                new BuildingLot("street", closed.Street, 20),
            });
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);
        var review = Logistics.FromRoutes(
            scored,
            api,
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));
        var plan = Payback.FromLedger(
            review,
            api,
            StatementDump.CsvPath(dir.Path),
            new[]
            {
                new BuildingAsk("ba:street_fourthavenue", 28, 10990d),
                new BuildingAsk("ba:street_side", 9, 4000d),
                new BuildingAsk(bought.Street, bought.Number, 10d),
            });
        var steps = Roadmap.Order(api, scored, review, plan, days, 3);
        var shut = Shutdown.Confirm(api, steps, steps[1].Street, steps[1].Number, true);
        var redo = Delivery.Confirm(api, review, review.Move!.Destination, true);
        var priced = Apply.ConfirmPrice(api, price, price.Item, true);
        var staffed = Apply.ConfirmStaff(api, staff, staff.PlanId, true);
        Apply.RecordPrice(dir.Path, priced);
        Apply.RecordStaff(dir.Path, staffed);
        var offered = Offer.Confirm(api, steps, steps[0].Street, steps[0].Number, true);
        Offer.Record(dir.Path, offered);
        var copied = Clone.Confirm(api, clone, clone.Street, clone.Blueprint, true);
        var cloneLine = Clone.Record(dir.Path, copied);

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.Equal(pin.ShippedBuild, api.Build);
        Assert.Equal("open", steps[0].Kind);
        Assert.True(offered.Offered);
        Assert.Equal("ba:street_fourthavenue", offered.Street);
        Assert.Equal(28, offered.Number);
        Assert.Equal("soda-line", clone.Blueprint);
        Assert.Equal("ba:street_main", clone.Street);
        Assert.Equal(1, clone.Source);
        Assert.Equal(4, clone.Destination);
        Assert.Equal("food", clone.Family);
        Assert.True(copied.Copied);
        Assert.Equal(clone.Street, copied.Street);
        Assert.Equal(clone.Family, copied.Family);
        Assert.Equal(clone.Blueprint, copied.Blueprint);
        Assert.Equal(clone.Source, copied.Source);
        Assert.Equal(clone.Destination, copied.Destination);
        Assert.Equal("ba:street_main", copied.Street);
        Assert.Equal("soda-line", copied.Blueprint);
        Assert.Equal(1, copied.Source);
        Assert.Equal(4, copied.Destination);
        Assert.NotEqual(offered.Street, copied.Street);
        Assert.NotEqual(bought.Street, copied.Street);
        Assert.NotEqual(shut.Number, copied.Destination);
        Assert.Equal(api.Clone, copied.Call);
        Assert.Equal(
            "BusinessLayoutSets.BusinessLayoutSetHelper.LoadBusinessLayoutSet",
            copied.Call);
        Assert.NotEqual(offered.Call, copied.Call);
        Assert.DoesNotContain("SendBuyBuildingOffer", copied.Call);
        Assert.DoesNotContain("ShutdownBusiness", copied.Call);
        Assert.DoesNotContain("SetPrice", copied.Call);
        Assert.DoesNotContain("AddDestination", copied.Call);
        Assert.DoesNotContain("SetBusinessLayout", copied.Call);
        Assert.Equal(
            "layout.cloned version=" + LoadLog.Version
                + " street=ba:street_main family=food blueprint=soda-line source=1 destination=4 call=BusinessLayoutSets.BusinessLayoutSetHelper.LoadBusinessLayoutSet",
            cloneLine);
        Assert.Contains(cloneLine, File.ReadAllText(LoadLog.CeoLogPath(dir.Path)));
        Assert.Equal(1, CountLines(File.ReadAllText(LoadLog.CeoLogPath(dir.Path)), "layout.cloned"));
        Assert.Equal(statementsBefore, File.ReadAllText(StatementDump.CsvPath(dir.Path)));
        Assert.Equal(routesBefore, File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
        Assert.DoesNotContain("LoadBusinessLayoutSet", statementsBefore);
        Assert.DoesNotContain("soda-line", routesBefore);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_payback_roadmap_shutdown_delivery_apply_offer_clone_undo_leaves_the_files()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown → delivery → apply → offer → clone undo
        var pin = PinDlls();
        using var dir = new TempLog();
        var canonical = ChainDays()[0].Rows[0];
        var days = new[]
        {
            new FinancialDay(
                1011,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -10f),
                }),
            new FinancialDay(
                1012,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -20f),
                }),
            new FinancialDay(
                1013,
                new[]
                {
                    canonical,
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -30f),
                }),
        };
        var stops = new[]
        {
            ChainStops()[0],
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_side 9",
                "ba:itemname_cheapgift",
                null,
                ""),
        };
        var holds = new[]
        {
            ChainHolds()[0],
            new WarehouseHold("W2", "ba:street_b", 2, "ba:itemname_cheapgift", 20),
        };

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var statementsBefore = File.ReadAllText(StatementDump.CsvPath(dir.Path));
        var routesBefore = File.ReadAllText(RouteDump.RoutesPath(dir.Path));
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-soda", 40),
                new StaffOffer("headhunter", run.Family, "hh-soda", 4),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", staff.Role, "ba:street_main", 1),
                new PortfolioBusiness("Food 2", staff.Role, "ba:street_main", 2),
                new PortfolioBusiness("Food 3", staff.Role, "ba:street_main", 3),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", staff.Role, 1, "soda-line", 12, false),
                new ChainLayout("ba:street_main", staff.Role, 4, "", 0, true),
            });
        var closed = Close.Lock(
            clone,
            new[]
            {
                new LedgerDay(clone.Street, 2, clone.Family, 1011, -10f),
                new LedgerDay(clone.Street, 2, clone.Family, 1012, -4f),
                new LedgerDay(clone.Street, 2, clone.Family, 1013, -1f),
            },
            3,
            true,
            true);
        var bought = Buy.Lock(
            closed,
            new[]
            {
                new BuildingLot("hq", "ba:street_hq", 1),
                new BuildingLot("food", "ba:street_warehouse", 10),
                new BuildingLot("street", closed.Street, 20),
            });
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);
        var review = Logistics.FromRoutes(
            scored,
            api,
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));
        var plan = Payback.FromLedger(
            review,
            api,
            StatementDump.CsvPath(dir.Path),
            new[]
            {
                new BuildingAsk("ba:street_fourthavenue", 28, 10990d),
                new BuildingAsk("ba:street_side", 9, 4000d),
                new BuildingAsk(bought.Street, bought.Number, 10d),
            });
        var steps = Roadmap.Order(api, scored, review, plan, days, 3);
        var shut = Shutdown.Confirm(api, steps, steps[1].Street, steps[1].Number, true);
        var redo = Delivery.Confirm(api, review, review.Move!.Destination, true);
        var priced = Apply.ConfirmPrice(api, price, price.Item, true);
        var staffed = Apply.ConfirmStaff(api, staff, staff.PlanId, true);
        Apply.RecordPrice(dir.Path, priced);
        Apply.RecordStaff(dir.Path, staffed);
        var offered = Offer.Confirm(api, steps, plan.Street, plan.Number, true);
        var offerLine = Offer.Record(dir.Path, offered);
        var copied = Clone.Confirm(api, clone, clone.Street, clone.Blueprint, true);
        var cloneLine = Clone.Record(dir.Path, copied);
        var undone = Clone.Reload(copied);

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.Equal(pin.ShippedBuild, api.Build);
        Assert.True(shut.Closed);
        Assert.True(redo.Moved);
        Assert.True(priced.Applied);
        Assert.True(staffed.Applied);
        Assert.True(offered.Offered);
        Assert.Equal("soda-line", clone.Blueprint);
        Assert.Equal("ba:street_main", clone.Street);
        Assert.True(copied.Copied);
        Assert.Equal(clone.Street, copied.Street);
        Assert.Equal(clone.Blueprint, copied.Blueprint);
        Assert.Equal("ba:street_main", copied.Street);
        Assert.Equal("soda-line", copied.Blueprint);
        Assert.Equal(1, copied.Source);
        Assert.Equal(4, copied.Destination);
        Assert.False(undone.Copied);
        Assert.Equal("", undone.Street);
        Assert.Equal("", undone.Family);
        Assert.Equal("", undone.Blueprint);
        Assert.Equal(0, undone.Source);
        Assert.Equal(0, undone.Destination);
        Assert.Equal("", undone.Call);
        Assert.NotEqual(copied.Street, undone.Street);
        Assert.NotEqual(copied.Blueprint, undone.Blueprint);
        Assert.NotEqual(offered.Street, copied.Street);
        Assert.Equal("", Clone.Record(dir.Path, undone));
        Assert.Equal(1, CountLines(File.ReadAllText(LoadLog.CeoLogPath(dir.Path)), "layout.cloned"));
        Assert.Contains(cloneLine, File.ReadAllText(LoadLog.CeoLogPath(dir.Path)));
        Assert.Contains(offerLine, File.ReadAllText(LoadLog.CeoLogPath(dir.Path)));
        Assert.Equal(statementsBefore, File.ReadAllText(StatementDump.CsvPath(dir.Path)));
        Assert.Equal(routesBefore, File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_payback_roadmap_shutdown_delivery_apply_offer_clone_shown_locks_the_open_on_the_first_line()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown → delivery → apply → offer → clone → shown
        var pin = PinDlls();
        using var dir = new TempLog();
        var canonical = ChainDays()[0].Rows[0];
        var days = new[]
        {
            new FinancialDay(
                1011,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -10f),
                }),
            new FinancialDay(
                1012,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -20f),
                }),
            new FinancialDay(
                1013,
                new[]
                {
                    canonical,
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -30f),
                }),
        };
        var stops = new[]
        {
            ChainStops()[0],
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_side 9",
                "ba:itemname_cheapgift",
                null,
                ""),
        };
        var holds = new[]
        {
            ChainHolds()[0],
            new WarehouseHold("W2", "ba:street_b", 2, "ba:itemname_cheapgift", 20),
        };

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-soda", 40),
                new StaffOffer("headhunter", run.Family, "hh-soda", 4),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", staff.Role, "ba:street_main", 1),
                new PortfolioBusiness("Food 2", staff.Role, "ba:street_main", 2),
                new PortfolioBusiness("Food 3", staff.Role, "ba:street_main", 3),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", staff.Role, 1, "soda-line", 12, false),
                new ChainLayout("ba:street_main", staff.Role, 4, "", 0, true),
            });
        var closed = Close.Lock(
            clone,
            new[]
            {
                new LedgerDay(clone.Street, 2, clone.Family, 1011, -10f),
                new LedgerDay(clone.Street, 2, clone.Family, 1012, -4f),
                new LedgerDay(clone.Street, 2, clone.Family, 1013, -1f),
            },
            3,
            true,
            true);
        var bought = Buy.Lock(
            closed,
            new[]
            {
                new BuildingLot("hq", "ba:street_hq", 1),
                new BuildingLot("food", "ba:street_warehouse", 10),
                new BuildingLot("street", closed.Street, 20),
            });
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);
        var review = Logistics.FromRoutes(
            scored,
            api,
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));
        var asks = new[]
        {
            new BuildingAsk("ba:street_fourthavenue", 28, 10990d),
            new BuildingAsk("ba:street_side", 9, 4000d),
            new BuildingAsk(bought.Street, bought.Number, 10d),
        };
        var steps = Roadmap.Order(api, scored, review, Payback.FromLedger(
            review,
            api,
            StatementDump.CsvPath(dir.Path),
            asks), days, 3);
        var shut = Shutdown.Confirm(api, steps, steps[1].Street, steps[1].Number, true);
        var redo = Delivery.Confirm(api, review, review.Move!.Destination, true);
        var priced = Apply.ConfirmPrice(api, price, price.Item, true);
        var staffed = Apply.ConfirmStaff(api, staff, staff.PlanId, true);
        Apply.RecordPrice(dir.Path, priced);
        Apply.RecordStaff(dir.Path, staffed);
        var offered = Offer.Confirm(api, steps, steps[0].Street, steps[0].Number, true);
        Offer.Record(dir.Path, offered);
        var copied = Clone.Confirm(api, clone, clone.Street, clone.Blueprint, true);
        var cloneLine = Clone.Record(dir.Path, copied);
        var board = new CityBoard(steps, review, rows);
        var session = new PanelSession();
        var opened = session.Press(dir.Path, board);
        var drawn = session.Draw(board);
        var live = Roadshow.Compose(
            days,
            stops,
            holds,
            asks,
            new[]
            {
                new PortfolioBusiness("Side", "specialty", "ba:street_side", 9),
                new PortfolioBusiness("Fourth", "food", "ba:street_fourthavenue", 28),
            });
        var move = opened.First(line => line.Tone == "move");

        Assert.StartsWith("day.rolled version=" + LoadLog.Version + " day=", rolled);
        Assert.Equal(pin.ShippedBuild, api.Build);
        Assert.Equal(pin.ShippedBuild, GameApi.Live().Build);
        Assert.True(shut.Closed);
        Assert.True(redo.Moved);
        Assert.True(priced.Applied);
        Assert.True(staffed.Applied);
        Assert.True(offered.Offered);
        Assert.True(copied.Copied);
        Assert.Contains(cloneLine, File.ReadAllText(LoadLog.CeoLogPath(dir.Path)));
        Assert.Equal("open", steps[0].Kind);
        Assert.Equal("ba:street_fourthavenue", steps[0].Street);
        Assert.Equal(28, steps[0].Number);
        Assert.Equal(1099d, steps[0].DailyGain);
        Assert.Equal(steps[0].Kind, live.Steps[0].Kind);
        Assert.Equal(steps[0].Street, live.Steps[0].Street);
        Assert.Equal(steps[0].Number, live.Steps[0].Number);
        Assert.Equal(steps[0].DailyGain, live.Steps[0].DailyGain);
        Assert.Equal(steps[0].Explain, opened[0].Explain);
        Assert.Equal("open", opened[0].Tone);
        Assert.Equal("step", opened[0].Business);
        Assert.Equal("ba:street_fourthavenue", opened[0].Street);
        Assert.Equal(28, opened[0].Number);
        Assert.Equal(opened[0].Street, drawn[0].Street);
        Assert.Equal(opened[0].Explain, drawn[0].Explain);
        Assert.NotEqual(copied.Street, opened[0].Street);
        Assert.NotEqual(bought.Street, opened[0].Street);
        Assert.Equal("ba:street_side 9", review.Move.Destination);
        Assert.Equal(review.Move.Explain, move.Explain);
        Assert.Equal("ba:street_side", move.Street);
        Assert.Equal(9, move.Number);
        Assert.Equal(move, opened[opened.Count - 1]);
        Assert.DoesNotContain(opened, line => line.Tone == "red");
        Assert.DoesNotContain(live.Steps, step => step.Kind == "move");
        var log = File.ReadAllText(LoadLog.CeoLogPath(dir.Path));
        Assert.Equal(1, CountLines(log, "roadmap.shown"));
        Assert.Contains(
            "roadmap.shown version=" + LoadLog.Version
                + " steps=2 street=ba:street_fourthavenue number=28 gain=1099 move=1",
            log);
        Assert.Equal("open  step  ba:street_fourthavenue 28", Panel.Text(opened)[0]);
        Assert.Contains("Daily gain 1099", Panel.Text(opened)[1]);
        Assert.Equal(rows.Count, CountLines(log, "advice.shown"));
        Assert.Equal(
            1,
            CountLines(File.ReadAllText(LoadLog.HistoryPath(dir.Path)), "roadmap.changed"));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_payback_roadmap_shutdown_delivery_apply_offer_clone_shown_closed_locks_store_closed()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown → delivery → apply → offer → clone → shown → closed
        using var dir = new TempLog();
        var shown = RunThroughShown(dir);
        var closeStep = shown.Live.Steps.First(step => step.Kind == "close");
        var statements = File.ReadAllText(StatementDump.CsvPath(dir.Path));
        var routes = File.ReadAllText(RouteDump.RoutesPath(dir.Path));

        ResolveGame();
        var buttons = ClosePress.Buttons(shown.Api, shown.Live);
        var button = buttons.First(row => row.Street == closeStep.Street && row.Number == closeStep.Number);
        var shut = ClosePress.Press(dir.Path, shown.Api, shown.Live, button.Street, button.Number, true);
        var log = File.ReadAllText(LoadLog.CeoLogPath(dir.Path));
        var shownAt = log.IndexOf("roadmap.shown ", StringComparison.Ordinal);
        var closedAt = log.IndexOf("store.closed ", StringComparison.Ordinal);

        Assert.Equal("Helpers.BusinessHelper.ShutdownBusiness", shown.Api.Close);
        Assert.Equal(shown.Api.Close, button.Call);
        Assert.Equal("Close", button.Label);
        Assert.Equal(closeStep.Street, shut.Street);
        Assert.Equal(closeStep.Number, shut.Number);
        Assert.True(shut.Closed);
        Assert.Equal(shown.Api.Close, shut.Call);
        Assert.NotEqual(shown.Live.Steps.First(step => step.Kind == "open").Street, shut.Street);
        Assert.True(closedAt > shownAt);
        Assert.Contains(
            "store.closed version=" + LoadLog.Version
                + " street=" + closeStep.Street
                + " number=" + closeStep.Number.ToString()
                + " call=" + shown.Api.Close,
            log);
        Assert.Equal(1, CountLines(log, "store.closed"));
        Assert.Equal(statements, File.ReadAllText(StatementDump.CsvPath(dir.Path)));
        Assert.Equal(routes, File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_payback_roadmap_shutdown_delivery_apply_offer_clone_shown_closed_gone_drops_the_shut_store()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown → delivery → apply → offer → clone → shown → closed → gone
        using var dir = new TempLog();
        var shown = RunThroughShown(dir);
        var closeStep = shown.Live.Steps.First(step => step.Kind == "close");
        var openStep = shown.Live.Steps.First(step => step.Kind == "open");

        ResolveGame();
        var shut = ClosePress.Press(dir.Path, shown.Api, shown.Live, closeStep.Street, closeStep.Number, true);
        var after = Roadshow.Compose(
            shown.Days,
            shown.Stops,
            shown.Holds,
            shown.Asks,
            shown.Portfolio,
            new[] { new PortfolioBusiness("", "", shut.Street, shut.Number) });

        Assert.True(shut.Closed);
        Assert.Contains(shown.Live.Steps, step => step.Kind == "close" && step.Street == shut.Street && step.Number == shut.Number);
        Assert.DoesNotContain(after.Steps, step => step.Kind == "close" && step.Street == shut.Street && step.Number == shut.Number);
        Assert.Contains(after.Steps, step => step.Kind == "open" && step.Street == openStep.Street && step.Number == openStep.Number);
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_payback_roadmap_shutdown_delivery_apply_offer_clone_shown_closed_gone_redone_moves_the_surviving_store()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown → delivery → apply → offer → clone → shown → closed → gone → redone
        using var dir = new TempLog();
        var shown = RunThroughShown(dir);
        var closeStep = shown.Live.Steps.First(step => step.Kind == "close");
        var routes = File.ReadAllText(RouteDump.RoutesPath(dir.Path));

        ResolveGame();
        var shut = ClosePress.Press(dir.Path, shown.Api, shown.Live, closeStep.Street, closeStep.Number, true);
        var after = Roadshow.Compose(
            shown.Days,
            shown.Stops,
            shown.Holds,
            shown.Asks,
            shown.Portfolio,
            new[] { new PortfolioBusiness("", "", shut.Street, shut.Number) });
        var move = after.Review.Move;
        var buttons = RedoPress.Buttons(shown.Api, after);
        var button = buttons.Single(row => row.Destination == move!.Destination);
        var redone = RedoPress.Press(dir.Path, shown.Api, after, button.Destination, true);
        var log = File.ReadAllText(LoadLog.CeoLogPath(dir.Path));

        Assert.True(shut.Closed);
        Assert.DoesNotContain(
            after.Steps,
            step => step.Kind == "close" && step.Street == shut.Street && step.Number == shut.Number);
        Assert.NotNull(move);
        Assert.Equal("ba:street_side 9", move.Destination);
        Assert.Equal("W1", move.From);
        Assert.Equal("W2", move.To);
        Assert.Equal("specialty", move.Family);
        Assert.Equal("Redo", button.Label);
        Assert.Equal(shown.Api.Delivery, button.Call);
        Assert.Equal(
            "UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination",
            button.Call);
        Assert.Equal(move.Destination, button.Destination);
        Assert.Equal(move.From, button.From);
        Assert.Equal(move.To, button.To);
        Assert.Equal(move.Family, button.Family);
        Assert.True(redone.Moved);
        Assert.Equal(move.Destination, redone.Destination);
        Assert.Equal("W2", redone.To);
        Assert.Equal(button.Call, redone.Call);
        Assert.Contains(
            "logistics.redone version=" + LoadLog.Version
                + " destination=ba:street_side 9 from=W1 to=W2 family=specialty call="
                + shown.Api.Delivery,
            log);
        Assert.Equal(1, CountLines(log, "logistics.redone"));
        Assert.Equal(routes, File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_payback_roadmap_shutdown_delivery_apply_offer_clone_shown_closed_gone_stayed_leaves_a_different_destination_put()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown → delivery → apply → offer → clone → shown → closed → gone → stayed
        using var dir = new TempLog();
        var shown = RunThroughShown(dir);
        var closeStep = shown.Live.Steps.First(step => step.Kind == "close");
        var openStep = shown.Live.Steps.First(step => step.Kind == "open");
        var routes = File.ReadAllText(RouteDump.RoutesPath(dir.Path));

        ResolveGame();
        var shut = ClosePress.Press(dir.Path, shown.Api, shown.Live, closeStep.Street, closeStep.Number, true);
        var after = Roadshow.Compose(
            shown.Days,
            shown.Stops,
            shown.Holds,
            shown.Asks,
            shown.Portfolio,
            new[] { new PortfolioBusiness("", "", shut.Street, shut.Number) });
        var other = openStep.Street + " " + openStep.Number.ToString();
        var stayed = RedoPress.Press(dir.Path, shown.Api, after, other, true);
        var log = File.ReadAllText(LoadLog.CeoLogPath(dir.Path));

        Assert.True(shut.Closed);
        Assert.DoesNotContain(
            after.Steps,
            step => step.Kind == "close" && step.Street == shut.Street && step.Number == shut.Number);
        Assert.NotNull(after.Review.Move);
        Assert.Equal("ba:street_side 9", after.Review.Move.Destination);
        Assert.NotEqual(after.Review.Move.Destination, other);
        Assert.DoesNotContain(RedoPress.Buttons(shown.Api, after), button => button.Destination == other);
        Assert.False(stayed.Moved);
        Assert.Equal("", stayed.Destination);
        Assert.Equal("", stayed.From);
        Assert.Equal("", stayed.To);
        Assert.Equal("", stayed.Call);
        Assert.Equal(0, CountLines(log, "logistics.redone"));
        Assert.Contains("store.closed ", log);
        Assert.Equal(routes, File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
    }

    [Fact]
    public void Dll_pin_loaded_csv_routes_day_rank_overlay_advice_prices_plans_staffing_layout_close_buy_panel_api_score_logistics_payback_roadmap_shutdown_delivery_apply_offer_clone_shown_stayed_leaves_the_open_step_open()
    {
        // Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown → delivery → apply → offer → clone → shown → stayed
        using var dir = new TempLog();
        var shown = RunThroughShown(dir);
        var openStep = shown.Live.Steps.First(step => step.Kind == "open");
        var statements = File.ReadAllText(StatementDump.CsvPath(dir.Path));
        var routes = File.ReadAllText(RouteDump.RoutesPath(dir.Path));

        var stayed = ClosePress.Press(
            dir.Path,
            shown.Api,
            shown.Live,
            openStep.Street,
            openStep.Number,
            true);
        var log = File.ReadAllText(LoadLog.CeoLogPath(dir.Path));

        Assert.Equal("open", openStep.Kind);
        Assert.DoesNotContain(
            ClosePress.Buttons(shown.Api, shown.Live),
            button => button.Street == openStep.Street && button.Number == openStep.Number);
        Assert.False(stayed.Closed);
        Assert.Equal("", stayed.Street);
        Assert.Equal(0, stayed.Number);
        Assert.Equal("", stayed.Call);
        Assert.Equal(0, CountLines(log, "store.closed"));
        Assert.Contains("roadmap.shown ", log);
        Assert.Equal(statements, File.ReadAllText(StatementDump.CsvPath(dir.Path)));
        Assert.Equal(routes, File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
    }

    private static void ResolveGame()
    {
        var managed = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam",
            "steamapps",
            "common",
            "Big Ambitions",
            "Big Ambitions_Data",
            "Managed");
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
        {
            var file = new AssemblyName(args.Name).Name + ".dll";
            var path = Path.Combine(managed, file);
            if (!File.Exists(path))
            {
                return null;
            }

            return Assembly.LoadFrom(path);
        };
    }

    private ShownPrefix RunThroughShown(TempLog dir)
    {
        var pin = PinDlls();
        var canonical = ChainDays()[0].Rows[0];
        var days = new[]
        {
            new FinancialDay(
                1011,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -10f),
                }),
            new FinancialDay(
                1012,
                new[]
                {
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -20f),
                }),
            new FinancialDay(
                1013,
                new[]
                {
                    canonical,
                    new StatementRow("ba:street_side", 9, 0f, 0f, 0f, 4f, 0f, 0f, -30f),
                }),
        };
        var stops = new[]
        {
            ChainStops()[0],
            new DeliveryStop(
                "ba:street_fourthstreet 46",
                "ba:street_side 9",
                "ba:itemname_cheapgift",
                null,
                ""),
        };
        var holds = new[]
        {
            ChainHolds()[0],
            new WarehouseHold("W2", "ba:street_b", 2, "ba:itemname_cheapgift", 20),
        };

        LoadLog.RecordPinned(dir.Path);
        StatementDump.Write(dir.Path, 1014, days);
        var routes = RouteDump.Write(dir.Path, stops, holds);
        var watch = new DayWatch();
        Assert.Null(watch.Tick(dir.Path, 1013, days, stops, holds));
        var rolled = watch.Tick(dir.Path, 1014, days, stops, holds);
        var ranked = Advisor.RankFile(StatementDump.CsvPath(dir.Path));
        var rows = Overlay.FromRank(
            ranked,
            Advisor.WarehouseFamilyMixFile(RouteDump.WarehousesPath(dir.Path)),
            Array.Empty<StreetCluster>());
        Advice.Record(dir.Path, rows);
        var item = Field(routes, "item");
        var price = Prices.Lock(
            item,
            new[]
            {
                new ItemSoldPerPriceEntry(item, 2.75f, 8),
            });
        var run = Plans.Lock("W1", holds, stops, price);
        var staff = Staffing.Lock(
            run,
            new[]
            {
                new StaffOffer("hr", run.Family, "hr-soda", 40),
                new StaffOffer("headhunter", run.Family, "hh-soda", 4),
            });
        var clusters = Advisor.StreetClusters(
            new[]
            {
                new PortfolioBusiness("Food 1", staff.Role, "ba:street_main", 1),
                new PortfolioBusiness("Food 2", staff.Role, "ba:street_main", 2),
                new PortfolioBusiness("Food 3", staff.Role, "ba:street_main", 3),
            });
        var clone = Layout.Lock(
            staff,
            clusters,
            new[]
            {
                new ChainLayout("ba:street_main", staff.Role, 1, "soda-line", 12, false),
                new ChainLayout("ba:street_main", staff.Role, 4, "", 0, true),
            });
        var closed = Close.Lock(
            clone,
            new[]
            {
                new LedgerDay(clone.Street, 2, clone.Family, 1011, -10f),
                new LedgerDay(clone.Street, 2, clone.Family, 1012, -4f),
                new LedgerDay(clone.Street, 2, clone.Family, 1013, -1f),
            },
            3,
            true,
            true);
        var bought = Buy.Lock(
            closed,
            new[]
            {
                new BuildingLot("hq", "ba:street_hq", 1),
                new BuildingLot("food", "ba:street_warehouse", 10),
                new BuildingLot("street", closed.Street, 20),
            });
        var panel = Panel.Rows(rows);
        var api = GameApi.Read(
            panel,
            File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md")));
        var scored = CloseScore.Score(api, days);
        var review = Logistics.FromRoutes(
            scored,
            api,
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path));
        var asks = new[]
        {
            new BuildingAsk("ba:street_fourthavenue", 28, 10990d),
            new BuildingAsk("ba:street_side", 9, 4000d),
            new BuildingAsk(bought.Street, bought.Number, 10d),
        };
        var steps = Roadmap.Order(api, scored, review, Payback.FromLedger(
            review,
            api,
            StatementDump.CsvPath(dir.Path),
            asks), days, 3);
        var shut = Shutdown.Confirm(api, steps, steps[1].Street, steps[1].Number, true);
        var redo = Delivery.Confirm(api, review, review.Move!.Destination, true);
        var priced = Apply.ConfirmPrice(api, price, price.Item, true);
        var staffed = Apply.ConfirmStaff(api, staff, staff.PlanId, true);
        Apply.RecordPrice(dir.Path, priced);
        Apply.RecordStaff(dir.Path, staffed);
        var offered = Offer.Confirm(api, steps, steps[0].Street, steps[0].Number, true);
        Offer.Record(dir.Path, offered);
        var copied = Clone.Confirm(api, clone, clone.Street, clone.Blueprint, true);
        Clone.Record(dir.Path, copied);
        var board = new CityBoard(steps, review, rows);
        var session = new PanelSession();
        var opened = session.Press(dir.Path, board);
        session.Draw(board);
        var portfolio = new[]
        {
            new PortfolioBusiness("Side", "specialty", "ba:street_side", 9),
            new PortfolioBusiness("Fourth", "food", "ba:street_fourthavenue", 28),
        };
        var live = Roadshow.Compose(days, stops, holds, asks, portfolio);

        Assert.NotNull(rolled);
        Assert.True(shut.Closed);
        Assert.True(redo.Moved);
        Assert.True(priced.Applied);
        Assert.True(staffed.Applied);
        Assert.True(offered.Offered);
        Assert.True(copied.Copied);
        Assert.Equal(pin.ShippedBuild, api.Build);
        Assert.Equal(opened[0].Street, live.Steps[0].Street);
        return new ShownPrefix(api, live, days, stops, holds, asks, portfolio);
    }

    private sealed class ShownPrefix
    {
        public ShownPrefix(
            ApiNote api,
            CityBoard live,
            IReadOnlyList<FinancialDay> days,
            IReadOnlyList<DeliveryStop> stops,
            IReadOnlyList<WarehouseHold> holds,
            IReadOnlyList<BuildingAsk> asks,
            IReadOnlyList<PortfolioBusiness> portfolio)
        {
            Api = api;
            Live = live;
            Days = days;
            Stops = stops;
            Holds = holds;
            Asks = asks;
            Portfolio = portfolio;
        }

        public ApiNote Api { get; }

        public CityBoard Live { get; }

        public IReadOnlyList<FinancialDay> Days { get; }

        public IReadOnlyList<DeliveryStop> Stops { get; }

        public IReadOnlyList<WarehouseHold> Holds { get; }

        public IReadOnlyList<BuildingAsk> Asks { get; }

        public IReadOnlyList<PortfolioBusiness> Portfolio { get; }
    }

    private static int CountLines(string text, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    private static string Field(string line, string name)
    {
        var key = " " + name + "=";
        var start = line.IndexOf(key, StringComparison.Ordinal);
        if (start < 0)
        {
            return "";
        }

        start += key.Length;
        var end = line.IndexOf(' ', start);
        if (end < 0)
        {
            end = line.Length;
        }

        return line.Substring(start, end - start);
    }

    private static FinancialDay[] ChainDays()
    {
        return new[]
        {
            new FinancialDay(
                1013,
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
        };
    }

    private static DeliveryStop[] ChainStops()
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

    private static WarehouseHold[] ChainHolds()
    {
        return new[]
        {
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_sodacan", 45),
        };
    }

    private static Pin PinDlls()
    {
        var repo = RepoRoot();
        var script = Path.Combine(Path.GetTempPath(), "chaotic-pin-" + Guid.NewGuid().ToString("N") + ".py");
        File.WriteAllText(script, """
            import sys
            from pathlib import Path
            repo = Path(sys.argv[1])
            sys.path.insert(0, str(repo / "script"))
            import game_build
            text = game_build.publish_pin([
                ("BigAmbitions.dll", b"managed-dll"),
                ("BigAmbitions.ModAPI.dll", b"api"),
            ])
            rows = game_build.read_pin(text)
            shipped = game_build.read_build(
                (repo / "doc" / "requirements" / "game-build.md").read_text(encoding="utf-8")
            )
            print(game_build.read_build(text))
            print(rows[0]["hash"])
            print(rows[1]["hash"])
            print(shipped)
            """);
        try
        {
            var start = new ProcessStartInfo("python")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add(script);
            start.ArgumentList.Add(repo);
            using var process = Process.Start(start)
                ?? throw new InvalidOperationException("python did not start");
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(stderr);
            }

            var lines = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return new Pin(lines[0], lines[1], lines[2], lines[3]);
        }
        finally
        {
            File.Delete(script);
        }
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

    private sealed record Pin(string Build, string DllHash, string ApiHash, string ShippedBuild);

    private sealed class TempLog : IDisposable
    {
        public TempLog()
        {
            Path = Directory.CreateTempSubdirectory("chaotic-ceo").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
