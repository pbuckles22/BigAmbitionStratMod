using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class NeedsTests
{
    [Fact]
    public void Needs_hold_sets_each_need_to_its_maximum()
    {
        var held = Needs.Hold();

        Assert.Equal(100f, Needs.Full);
        Assert.Equal(Needs.Full, held.Energy);
        Assert.Equal(Needs.Full, held.Hunger);
        Assert.Equal(Needs.Full, held.Happiness);
        Assert.True(held.EnergyOff);
        Assert.True(held.HappinessOff);
    }

    [Fact]
    public void Needs_a_full_frozen_player_is_not_due()
    {
        Assert.False(Needs.Due(Needs.Hold()));
    }

    [Fact]
    public void Needs_each_low_need_is_due()
    {
        Assert.True(Needs.Due(new PlayerNeeds(99.9f, 100f, 100f, true, true)));
        Assert.True(Needs.Due(new PlayerNeeds(100f, 0f, 100f, true, true)));
        Assert.True(Needs.Due(new PlayerNeeds(100f, 100f, 42f, true, true)));
    }

    [Fact]
    public void Needs_each_flag_left_on_is_due()
    {
        Assert.True(Needs.Due(new PlayerNeeds(100f, 100f, 100f, false, true)));
        Assert.True(Needs.Due(new PlayerNeeds(100f, 100f, 100f, true, false)));
    }

    [Fact]
    public void Needs_a_need_above_full_is_not_due()
    {
        Assert.False(Needs.Due(new PlayerNeeds(100.5f, 100f, 100f, true, true)));
    }

    [Fact]
    public void Needs_held_line_names_the_day_and_the_values_before_the_hold()
    {
        var line = Needs.Line(1016, new PlayerNeeds(37.25f, 12f, 64.5f, false, false));

        Assert.Equal(
            "needs.held version=" + LoadLog.Version + " day=1016 energy=37.25 hunger=12 happiness=64.5 energyOff=false happinessOff=false",
            line);
    }

    [Fact]
    public void Needs_held_line_ignores_the_machine_culture()
    {
        var prior = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

            var line = Needs.Line(1016, new PlayerNeeds(37.25f, 12f, 64.5f, false, false));

            Assert.Contains(" energy=37.25 ", line);
            Assert.Contains(" happiness=64.5 ", line);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = prior;
        }
    }

    [Fact]
    public void NeedsWatch_the_first_frame_of_a_save_logs_once_and_fills()
    {
        using var dir = new TempNeeds();
        var watch = new NeedsWatch();
        var save = new object();

        var write = watch.Tick(dir.Path, save, 1016, new PlayerNeeds(37.25f, 12f, 64.5f, false, false));

        Assert.NotNull(write);
        Assert.Equal(Needs.Full, write!.Energy);
        Assert.Equal(Needs.Full, write.Hunger);
        Assert.Equal(Needs.Full, write.Happiness);
        Assert.True(write.EnergyOff);
        Assert.True(write.HappinessOff);
        Assert.Equal(
            new[]
            {
                "needs.held version=" + LoadLog.Version + " day=1016 energy=37.25 hunger=12 happiness=64.5 energyOff=false happinessOff=false",
            },
            File.ReadAllLines(LoadLog.CeoLogPath(dir.Path)));
    }

    [Fact]
    public void NeedsWatch_a_held_save_writes_nothing_on_later_frames()
    {
        using var dir = new TempNeeds();
        var watch = new NeedsWatch();
        var save = new object();
        watch.Tick(dir.Path, save, 1016, new PlayerNeeds(37f, 12f, 64f, false, false));

        var later = watch.Tick(dir.Path, save, 1016, Needs.Hold());
        var nextDay = watch.Tick(dir.Path, save, 1017, Needs.Hold());

        Assert.Null(later);
        Assert.Null(nextDay);
        Assert.Single(File.ReadAllLines(LoadLog.CeoLogPath(dir.Path)));
    }

    [Fact]
    public void NeedsWatch_a_drift_after_the_hold_refills_without_another_line()
    {
        using var dir = new TempNeeds();
        var watch = new NeedsWatch();
        var save = new object();
        watch.Tick(dir.Path, save, 1016, new PlayerNeeds(37f, 12f, 64f, false, false));

        var refill = watch.Tick(dir.Path, save, 1016, new PlayerNeeds(100f, 98.5f, 100f, true, true));

        Assert.NotNull(refill);
        Assert.Equal(Needs.Full, refill!.Hunger);
        Assert.Single(File.ReadAllLines(LoadLog.CeoLogPath(dir.Path)));
    }

    [Fact]
    public void NeedsWatch_a_save_already_full_and_frozen_logs_once_and_writes_nothing()
    {
        using var dir = new TempNeeds();
        var watch = new NeedsWatch();

        var write = watch.Tick(dir.Path, new object(), 1016, Needs.Hold());

        Assert.Null(write);
        Assert.Equal(
            new[]
            {
                "needs.held version=" + LoadLog.Version + " day=1016 energy=100 hunger=100 happiness=100 energyOff=true happinessOff=true",
            },
            File.ReadAllLines(LoadLog.CeoLogPath(dir.Path)));
    }

    [Fact]
    public void NeedsWatch_loading_another_save_logs_and_fills_again()
    {
        using var dir = new TempNeeds();
        var watch = new NeedsWatch();
        watch.Tick(dir.Path, new object(), 1016, new PlayerNeeds(37f, 12f, 64f, false, false));

        var second = watch.Tick(dir.Path, new object(), 400, new PlayerNeeds(80f, 70f, 50f, false, false));

        Assert.NotNull(second);
        var lines = File.ReadAllLines(LoadLog.CeoLogPath(dir.Path));
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("needs.held version=" + LoadLog.Version + " day=400 energy=80 ", lines[1]);
    }

    [Fact]
    public void NeedsWatch_a_null_save_does_nothing()
    {
        using var dir = new TempNeeds();
        var watch = new NeedsWatch();

        var write = watch.Tick(dir.Path, null, 1016, new PlayerNeeds(37f, 12f, 64f, false, false));

        Assert.Null(write);
        Assert.False(File.Exists(LoadLog.CeoLogPath(dir.Path)));
    }

    [Fact]
    public void NeedsHeld_load_day_roll_needs_refill_keeps_the_player_full_across_the_roll()
    {
        // Chain: load → day roll → needs refill
        using var dir = new TempNeeds();
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

        var first = needs.Tick(dir.Path, save, 1015, new PlayerNeeds(37f, 12f, 64f, false, false));
        var armed = days.Tick(dir.Path, 1015, statements, routes, holds);
        var held = first!;
        var drifted = new PlayerNeeds(held.Energy, held.Hunger - 3f, held.Happiness, held.EnergyOff, held.HappinessOff);
        var refill = needs.Tick(dir.Path, save, 1016, drifted);
        var rolled = days.Tick(dir.Path, 1016, statements, routes, holds);
        var after = needs.Tick(dir.Path, save, 1016, refill!);

        Assert.StartsWith("mod.loaded version=" + LoadLog.Version, loaded);
        Assert.Null(armed);
        Assert.Equal("day.rolled version=" + LoadLog.Version + " day=1016", rolled);
        Assert.Equal(Needs.Full, refill!.Hunger);
        Assert.False(Needs.Due(refill));
        Assert.Null(after);
        var lines = File.ReadAllLines(LoadLog.CeoLogPath(dir.Path));
        Assert.Equal(loaded, lines[0]);
        Assert.Equal(
            "needs.held version=" + LoadLog.Version + " day=1015 energy=37 hunger=12 happiness=64 energyOff=false happinessOff=false",
            lines[1]);
        Assert.Equal("day.rolled version=" + LoadLog.Version + " day=1016", lines[^1]);
        Assert.Single(lines, line => line.StartsWith("needs.held "));
    }

    private sealed class TempNeeds : IDisposable
    {
        public TempNeeds()
        {
            Path = Directory.CreateTempSubdirectory("chaotic-needs").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
