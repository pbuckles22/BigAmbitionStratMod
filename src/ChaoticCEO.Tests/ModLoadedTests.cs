using System.Diagnostics;
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

        Assert.Equal("mod.loaded version=1.6.1.2 build=3682", line);
        Assert.Equal(line, File.ReadAllText(path).TrimEnd('\r', '\n'));
    }

    [Fact]
    public void ModLoaded_a_different_build_is_not_3682()
    {
        using var dir = new TempLog();
        var path = Path.Combine(dir.Path, "ceo.log");

        var line = LoadLog.Record(path, "1000");

        Assert.Equal("mod.loaded version=1.6.1.2 build=1000", line);
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
                "mod.loaded version=1.6.1.2 build=1",
                "mod.loaded version=1.6.1.2 build=2",
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
        Assert.Equal("mod.loaded version=1.6.1.2 build=3682", line);
        Assert.Equal(line, File.ReadAllText(path).TrimEnd('\r', '\n'));
    }

    [Fact]
    public void Compile_stamps_the_informational_version()
    {
        var product = FileVersionInfo.GetVersionInfo(typeof(LoadLog).Assembly.Location).ProductVersion;

        Assert.Matches(@"^1\.6\.1\.2\+\d+$", product);
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
            "csv.written version=1.6.1.2 rows=1 day=1013 street=ba:street_fourthavenue number=28 profit=50687.4805",
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
        Assert.Equal("csv.written version=1.6.1.2 rows=0 day=1013 street= number= profit=", written);
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
            "routes.written version=1.6.1.2 routes=1 warehouses=1 warehouse=ba:street_fourthstreet 46 destination=ba:street_firstavenue 38 item=ba:itemname_sodacan day=1016",
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
            "routes.written version=1.6.1.2 routes=0 warehouses=0 warehouse= destination= item= day=",
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
        Assert.Equal("day.rolled version=1.6.1.2 day=1014", rolled);
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
        Assert.Equal("day.rolled version=1.6.1.2 day=1014", rolled);
        Assert.Equal(
            loaded + "\n" + written + "\n" + routes + "\n" + written + "\n" + routes + "\n" + rolled + "\n",
            log);
        Assert.Equal(
            "routes.written version=1.6.1.2 routes=0 warehouses=0 warehouse= destination= item= day=",
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
        Assert.Equal("day.rolled version=1.6.1.2 day=1014", rolled);
        Assert.Equal("day.rolled version=1.6.1.2 day=1015", next);
        Assert.Contains(loaded + "\n" + written + "\n" + routes + "\n", log);
        Assert.EndsWith(rolled + "\n" + "csv.written version=1.6.1.2 rows=1 day=1014 street=ba:street_today number=1 profit=9" + "\n" + routes + "\n" + next + "\n", log);
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
        Assert.Equal("day.rolled version=1.6.1.2 day=1014", rolled);
        Assert.Equal(
            "csv.written version=1.6.1.2 rows=1 day=1013 street=ba:street_fourthavenue number=28 profit=50687.4805",
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
        Assert.Equal("day.rolled version=1.6.1.2 day=1014", rolled);
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
        Assert.Equal("day.rolled version=1.6.1.2 day=1014", rolled);
        Assert.Equal(
            "routes.written version=1.6.1.2 routes=0 warehouses=0 warehouse= destination= item= day=",
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
        Assert.Equal("day.rolled version=1.6.1.2 day=1014", rolled);
        Assert.Equal(
            "csv.written version=1.6.1.2 rows=1 day=1013 street=ba:street_fourthavenue number=28 profit=50687.4805",
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
        Assert.Equal("day.rolled version=1.6.1.2 day=1014", rolled);
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
