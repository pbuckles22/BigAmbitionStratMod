using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class CurvesTests
{
    [Fact]
    public void Curves_fit_locks_mean_sales_and_goods_rate()
    {
        var curves = Curves.Fit(
            new[]
            {
                Day(1000, Row("ba:street_a", 1, sales: 100f, goods: 20f, wages: 10f, rent: 5f)),
                Day(1001, Row("ba:street_a", 1, sales: 200f, goods: 40f, wages: 10f, rent: 5f)),
            });

        Assert.Single(curves);
        Assert.Equal("ba:street_a", curves[0].Street);
        Assert.Equal(1, curves[0].Number);
        Assert.Equal(150m, curves[0].DailySales);
        Assert.Equal(0.2m, curves[0].GoodsPerSale);
        Assert.Equal(2, curves[0].SampleDays);
    }

    [Fact]
    public void Curves_fit_averages_fixed_costs_across_days()
    {
        var curves = Curves.Fit(
            new[]
            {
                Day(1000, Row("ba:street_b", 2, sales: 50f, goods: 5f, wages: 8f, rent: 4f, marketing: 1f, theft: 0.5f)),
                Day(1001, Row("ba:street_b", 2, sales: 50f, goods: 5f, wages: 12f, rent: 4f, marketing: 3f, theft: 1.5f)),
            });

        Assert.Single(curves);
        Assert.Equal(10m, curves[0].DailyWages);
        Assert.Equal(4m, curves[0].DailyRent);
        Assert.Equal(2m, curves[0].DailyMarketing);
        Assert.Equal(1m, curves[0].DailyTheft);
    }

    [Fact]
    public void Curves_fit_zero_sales_days_do_not_poison_goods_rate()
    {
        var curves = Curves.Fit(
            new[]
            {
                Day(1000, Row("ba:street_c", 3, sales: 0f, goods: 0f, wages: 9f, rent: 2f)),
                Day(1001, Row("ba:street_c", 3, sales: 100f, goods: 25f, wages: 9f, rent: 2f)),
            });

        Assert.Single(curves);
        Assert.Equal(50m, curves[0].DailySales);
        Assert.Equal(0.25m, curves[0].GoodsPerSale);
        Assert.Equal(9m, curves[0].DailyWages);
    }

    [Fact]
    public void Curves_fit_cost_only_store_keeps_wages_and_rent_with_zero_demand()
    {
        var curves = Curves.Fit(
            new[]
            {
                Day(1000, Row("ba:street_w", 4, sales: 0f, goods: 0f, wages: 40f, rent: 10f)),
                Day(1001, Row("ba:street_w", 4, sales: 0f, goods: 0f, wages: 60f, rent: 10f)),
            });

        Assert.Single(curves);
        Assert.Equal(0m, curves[0].DailySales);
        Assert.Equal(0m, curves[0].GoodsPerSale);
        Assert.Equal(50m, curves[0].DailyWages);
        Assert.Equal(10m, curves[0].DailyRent);
    }

    [Fact]
    public void Curves_fit_keeps_stores_separate_and_orders_by_street_then_number()
    {
        var curves = Curves.Fit(
            new[]
            {
                Day(
                    1000,
                    Row("ba:street_z", 1, sales: 10f, goods: 1f, wages: 1f, rent: 1f),
                    Row("ba:street_a", 9, sales: 20f, goods: 2f, wages: 2f, rent: 2f),
                    Row("ba:street_a", 2, sales: 30f, goods: 3f, wages: 3f, rent: 3f)),
            });

        Assert.Equal(3, curves.Count);
        Assert.Equal(("ba:street_a", 2), (curves[0].Street, curves[0].Number));
        Assert.Equal(("ba:street_a", 9), (curves[1].Street, curves[1].Number));
        Assert.Equal(("ba:street_z", 1), (curves[2].Street, curves[2].Number));
        Assert.Equal(30m, curves[0].DailySales);
        Assert.Equal(20m, curves[1].DailySales);
        Assert.Equal(10m, curves[2].DailySales);
    }

    [Fact]
    public void Curves_fit_empty_or_null_is_empty()
    {
        Assert.Empty(Curves.Fit(null));
        Assert.Empty(Curves.Fit(Array.Empty<FinancialDay>()));
        Assert.Empty(Curves.Fit(new[] { new FinancialDay(1, Array.Empty<StatementRow>()) }));
    }

    [Fact]
    public void Curves_project_builds_the_statement_row_the_day_stepper_reads()
    {
        var curve = Curves.Fit(
            new[]
            {
                Day(1000, Row("ba:street_p", 7, sales: 100f, goods: 20f, wages: 10f, rent: 5f, marketing: 2f, theft: 1f)),
            })[0];

        var row = Curves.Project(curve);

        Assert.Equal("ba:street_p", row.Street);
        Assert.Equal(7, row.Number);
        Assert.Equal(100f, row.Sales);
        Assert.Equal(20f, row.Goods);
        Assert.Equal(10f, row.Wages);
        Assert.Equal(5f, row.Rent);
        Assert.Equal(2f, row.Marketing);
        Assert.Equal(1f, row.Theft);
        Assert.Equal(62f, row.Profit);
    }

    [Fact]
    public void Curves_fit_file_reads_full_ledger_columns()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "statements.csv");
        File.WriteAllText(
            path,
            "day,street,number,sales,goods,wages,rent,marketing,theft,profit\n"
            + "1000,ba:street_ledger,8,100,20,10,5,1,0.5,63.5\n"
            + "1001,ba:street_ledger,8,200,40,10,5,1,0.5,143.5\n");

        var curves = Curves.FitFile(path);

        Assert.Single(curves);
        Assert.Equal("ba:street_ledger", curves[0].Street);
        Assert.Equal(8, curves[0].Number);
        Assert.Equal(150m, curves[0].DailySales);
        Assert.Equal(0.2m, curves[0].GoodsPerSale);
        Assert.Equal(10m, curves[0].DailyWages);
        Assert.Equal(5m, curves[0].DailyRent);
        Assert.Equal(1m, curves[0].DailyMarketing);
        Assert.Equal(0.5m, curves[0].DailyTheft);
        Assert.Equal(2, curves[0].SampleDays);
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
            Path = Directory.CreateTempSubdirectory("ceo-curves").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
