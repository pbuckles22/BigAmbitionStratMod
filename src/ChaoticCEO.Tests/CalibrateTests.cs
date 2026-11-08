using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class CalibrateTests
{
    [Fact]
    public void Calibrate_check_sim_from_day_1000_lands_within_tolerance_of_ledger_day_1014()
    {
        var ledger = new[]
        {
            Day(1000, Row("ba:street_a", 1, sales: 100f, goods: 20f, wages: 10f, rent: 5f)),
            Day(1001, Row("ba:street_a", 1, sales: 100f, goods: 20f, wages: 10f, rent: 5f)),
            Day(1014, Row("ba:street_a", 1, sales: 110f, goods: 22f, wages: 10f, rent: 5f)),
        };

        var result = Calibrate.Check(ledger);

        Assert.Equal(Calibrate.StartDay, result.StartDay);
        Assert.Equal(Calibrate.EndDay, result.EndDay);
        Assert.Equal(65m, result.Projected);
        Assert.Equal(73m, result.Actual);
        Assert.True(result.Error <= Calibrate.Tolerance);
        Assert.True(result.Within);
    }

    [Fact]
    public void Calibrate_error_locks_relative_gap_and_within_uses_tolerance()
    {
        Assert.Equal(0.1m, Calibrate.Error(90m, 100m));
        Assert.True(Calibrate.Within(90m, 100m, 0.1m));
        Assert.False(Calibrate.Within(90m, 100m, 0.05m));
        Assert.Equal(Calibrate.Tolerance, 0.20m);
    }

    [Fact]
    public void Calibrate_check_outside_tolerance_is_not_within()
    {
        var ledger = new[]
        {
            Day(1000, Row("ba:street_b", 2, sales: 100f, goods: 20f, wages: 10f, rent: 5f)),
            Day(1014, Row("ba:street_b", 2, sales: 200f, goods: 40f, wages: 10f, rent: 5f)),
        };

        var result = Calibrate.Check(ledger);

        Assert.Equal(65m, result.Projected);
        Assert.Equal(145m, result.Actual);
        Assert.True(result.Error > Calibrate.Tolerance);
        Assert.False(result.Within);
    }

    [Fact]
    public void Calibrate_check_fits_only_days_at_or_before_start_then_steps_to_end()
    {
        var ledger = new[]
        {
            Day(999, Row("ba:street_c", 3, sales: 50f, goods: 10f, wages: 5f, rent: 5f)),
            Day(1000, Row("ba:street_c", 3, sales: 150f, goods: 30f, wages: 5f, rent: 5f)),
            Day(1005, Row("ba:street_c", 3, sales: 999f, goods: 1f, wages: 1f, rent: 1f)),
            Day(1014, Row("ba:street_c", 3, sales: 100f, goods: 20f, wages: 5f, rent: 5f)),
        };

        var result = Calibrate.Check(ledger);

        Assert.Equal(70m, result.Projected);
        Assert.Equal(70m, result.Actual);
        Assert.Equal(0m, result.Error);
        Assert.True(result.Within);
    }

    [Fact]
    public void Calibrate_check_sums_empire_profit_across_matching_stores()
    {
        var ledger = new[]
        {
            Day(
                1000,
                Row("ba:street_a", 1, sales: 100f, goods: 20f, wages: 10f, rent: 5f),
                Row("ba:street_z", 9, sales: 40f, goods: 8f, wages: 4f, rent: 2f)),
            Day(
                1014,
                Row("ba:street_a", 1, sales: 100f, goods: 20f, wages: 10f, rent: 5f),
                Row("ba:street_z", 9, sales: 40f, goods: 8f, wages: 4f, rent: 2f),
                Row("ba:street_new", 2, sales: 500f, goods: 0f, wages: 0f, rent: 0f)),
        };

        var result = Calibrate.Check(ledger);

        Assert.Equal(91m, result.Projected);
        Assert.Equal(91m, result.Actual);
        Assert.True(result.Within);
    }

    [Fact]
    public void Calibrate_check_empty_or_missing_end_day_is_not_within()
    {
        Assert.False(Calibrate.Check(null).Within);
        Assert.False(Calibrate.Check(Array.Empty<FinancialDay>()).Within);
        Assert.False(Calibrate.Check(new[]
        {
            Day(1000, Row("ba:street_a", 1, sales: 10f, goods: 1f, wages: 1f, rent: 1f)),
        }).Within);
    }

    [Fact]
    public void Calibrate_check_file_reads_ledger_csv_and_locks_day_1000_to_1014()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "statements.csv");
        File.WriteAllText(
            path,
            "day,street,number,sales,goods,wages,rent,marketing,theft,profit\n"
            + "1000,ba:street_ledger,8,100,20,10,5,0,0,65\n"
            + "1014,ba:street_ledger,8,100,20,10,5,0,0,65\n");

        var result = Calibrate.CheckFile(path);

        Assert.Equal(65m, result.Projected);
        Assert.Equal(65m, result.Actual);
        Assert.Equal(0m, result.Error);
        Assert.True(result.Within);
    }

    [Fact]
    public void Calibrate_within_near_zero_actual_uses_unit_floor()
    {
        Assert.Equal(0m, Calibrate.Error(0m, 0m));
        Assert.True(Calibrate.Within(0m, 0m, Calibrate.Tolerance));
        Assert.Equal(0.5m, Calibrate.Error(0.5m, 0m));
        Assert.False(Calibrate.Within(0.5m, 0m, 0.2m));
    }

    private static FinancialDay Day(int day, params StatementRow[] rows)
    {
        return new FinancialDay(day, rows);
    }

    private static StatementRow Row(
        string street,
        int number,
        float sales,
        float goods,
        float wages,
        float rent,
        float marketing = 0f,
        float theft = 0f)
    {
        var profit = sales - goods - wages - rent - marketing - theft;
        return new StatementRow(street, number, sales, goods, wages, rent, marketing, theft, profit);
    }

    private sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            Path = Directory.CreateTempSubdirectory("ceo-calibrate").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
