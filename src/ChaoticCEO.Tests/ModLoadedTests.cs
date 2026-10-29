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

        Assert.Equal("mod.loaded version=1.3.3 build=3682", line);
        Assert.Equal(line, File.ReadAllText(path).TrimEnd('\r', '\n'));
    }

    [Fact]
    public void ModLoaded_a_different_build_is_not_3682()
    {
        using var dir = new TempLog();
        var path = Path.Combine(dir.Path, "ceo.log");

        var line = LoadLog.Record(path, "1000");

        Assert.Equal("mod.loaded version=1.3.3 build=1000", line);
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
                "mod.loaded version=1.3.3 build=1",
                "mod.loaded version=1.3.3 build=2",
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
        Assert.Equal("mod.loaded version=1.3.3 build=3682", line);
        Assert.Equal(line, File.ReadAllText(path).TrimEnd('\r', '\n'));
    }

    [Fact]
    public void Compile_stamps_the_informational_version()
    {
        var product = FileVersionInfo.GetVersionInfo(typeof(LoadLog).Assembly.Location).ProductVersion;

        Assert.Matches(@"^1\.3\.3\+\d+$", product);
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
            "csv.written version=1.3.3 rows=1 day=1013 street=ba:street_fourthavenue number=28 profit=50687.4805",
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
        Assert.Equal("csv.written version=1.3.3 rows=0 day=1013 street= number= profit=", written);
        Assert.Contains(written, log);
        Assert.Equal("day,street,number,sales,goods,wages,rent,marketing,theft,profit\n", csv);
        Assert.DoesNotContain(pin.DllHash, log);
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
