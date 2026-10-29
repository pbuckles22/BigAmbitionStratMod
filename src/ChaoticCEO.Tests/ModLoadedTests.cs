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

        Assert.Equal("mod.loaded version=1.4.2 build=3682", line);
        Assert.Equal(line, File.ReadAllText(path).TrimEnd('\r', '\n'));
    }

    [Fact]
    public void ModLoaded_a_different_build_is_not_3682()
    {
        using var dir = new TempLog();
        var path = Path.Combine(dir.Path, "ceo.log");

        var line = LoadLog.Record(path, "1000");

        Assert.Equal("mod.loaded version=1.4.2 build=1000", line);
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
                "mod.loaded version=1.4.2 build=1",
                "mod.loaded version=1.4.2 build=2",
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
        Assert.Equal("mod.loaded version=1.4.2 build=3682", line);
        Assert.Equal(line, File.ReadAllText(path).TrimEnd('\r', '\n'));
    }

    [Fact]
    public void Compile_stamps_the_informational_version()
    {
        var product = FileVersionInfo.GetVersionInfo(typeof(LoadLog).Assembly.Location).ProductVersion;

        Assert.Matches(@"^1\.4\.2\+\d+$", product);
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
            "csv.written version=1.4.2 rows=1 day=1013 street=ba:street_fourthavenue number=28 profit=50687.4805",
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
        Assert.Equal("csv.written version=1.4.2 rows=0 day=1013 street= number= profit=", written);
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
            "routes.written version=1.4.2 routes=1 warehouses=1 warehouse=ba:street_fourthstreet 46 destination=ba:street_firstavenue 38 item=ba:itemname_sodacan day=1016",
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
            "routes.written version=1.4.2 routes=0 warehouses=0 warehouse= destination= item= day=",
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
        Assert.Equal("day.rolled version=1.4.2 day=1014", rolled);
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
        Assert.Equal("day.rolled version=1.4.2 day=1014", rolled);
        Assert.Equal(
            loaded + "\n" + written + "\n" + routes + "\n" + written + "\n" + routes + "\n" + rolled + "\n",
            log);
        Assert.Equal(
            "routes.written version=1.4.2 routes=0 warehouses=0 warehouse= destination= item= day=",
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
        Assert.Equal("day.rolled version=1.4.2 day=1014", rolled);
        Assert.Equal("day.rolled version=1.4.2 day=1015", next);
        Assert.Contains(loaded + "\n" + written + "\n" + routes + "\n", log);
        Assert.EndsWith(rolled + "\n" + "csv.written version=1.4.2 rows=1 day=1014 street=ba:street_today number=1 profit=9" + "\n" + routes + "\n" + next + "\n", log);
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
        Assert.Equal("day.rolled version=1.4.2 day=1014", rolled);
        Assert.Equal(
            "csv.written version=1.4.2 rows=1 day=1013 street=ba:street_fourthavenue number=28 profit=50687.4805",
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
        Assert.Equal("day.rolled version=1.4.2 day=1014", rolled);
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
        Assert.Equal("day.rolled version=1.4.2 day=1014", rolled);
        Assert.Equal(
            "routes.written version=1.4.2 routes=0 warehouses=0 warehouse= destination= item= day=",
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
        Assert.Equal("day.rolled version=1.4.2 day=1014", rolled);
        Assert.Equal(
            "csv.written version=1.4.2 rows=1 day=1013 street=ba:street_fourthavenue number=28 profit=50687.4805",
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
        Assert.Equal("day.rolled version=1.4.2 day=1014", rolled);
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
