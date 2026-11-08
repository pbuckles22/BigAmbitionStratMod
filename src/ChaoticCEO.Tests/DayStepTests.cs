using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class DayStepTests
{
    [Fact]
    public void DayStep_day_projects_the_statement_row_from_curves()
    {
        var curves = Curves.Fit(
            new[]
            {
                Day(1000, Row("ba:street_a", 1, sales: 100f, goods: 20f, wages: 10f, rent: 5f, marketing: 2f, theft: 1f)),
            });

        var stepped = DayStep.Day(1001, curves);

        Assert.Equal(1001, stepped.Day);
        Assert.Single(stepped.Rows);
        Assert.Equal("ba:street_a", stepped.Rows[0].Street);
        Assert.Equal(1, stepped.Rows[0].Number);
        Assert.Equal(100f, stepped.Rows[0].Sales);
        Assert.Equal(20f, stepped.Rows[0].Goods);
        Assert.Equal(10f, stepped.Rows[0].Wages);
        Assert.Equal(5f, stepped.Rows[0].Rent);
        Assert.Equal(2f, stepped.Rows[0].Marketing);
        Assert.Equal(1f, stepped.Rows[0].Theft);
        Assert.Equal(62f, stepped.Rows[0].Profit);
    }

    [Fact]
    public void DayStep_run_advances_days_with_the_same_projected_economics()
    {
        var curves = Curves.Fit(
            new[]
            {
                Day(1000, Row("ba:street_b", 2, sales: 50f, goods: 10f, wages: 8f, rent: 4f)),
            });

        var days = DayStep.Run(1000, 3, curves);

        Assert.Equal(new[] { 1000, 1001, 1002 }, days.Select(day => day.Day).ToArray());
        Assert.All(days, day =>
        {
            Assert.Single(day.Rows);
            Assert.Equal(50f, day.Rows[0].Sales);
            Assert.Equal(10f, day.Rows[0].Goods);
            Assert.Equal(28f, day.Rows[0].Profit);
        });
    }

    [Fact]
    public void DayStep_portfolio_matches_portfolio_csv_shape_with_tco()
    {
        var curves = Curves.Fit(
            new[]
            {
                Day(1000, Row("ba:street_c", 3, sales: 100f, goods: 20f, wages: 10f, rent: 5f, marketing: 1f)),
            });
        var days = DayStep.Run(1000, 2, curves);
        var portfolio = new[]
        {
            new PortfolioBusiness("Gift Shop", "specialty", "ba:street_c", 3),
        };

        var rows = DayStep.Portfolio(days, portfolio);

        Assert.Single(rows);
        Assert.Equal("Gift Shop", rows[0].Business);
        Assert.Equal("specialty", rows[0].Family);
        Assert.Equal("ba:street_c", rows[0].Street);
        Assert.Equal(3, rows[0].Number);
        Assert.Equal(36d, rows[0].Tco);
    }

    [Fact]
    public void DayStep_rank_uses_epic_four_advisor_on_projected_days()
    {
        var curves = Curves.Fit(
            new[]
            {
                Day(
                    1000,
                    Row("ba:street_low", 1, sales: 10f, goods: 5f, wages: 20f, rent: 5f),
                    Row("ba:street_high", 2, sales: 100f, goods: 10f, wages: 10f, rent: 5f)),
            });
        var days = DayStep.Run(1000, 1, curves);
        var portfolio = new[]
        {
            new PortfolioBusiness("Low", "food", "ba:street_low", 1),
            new PortfolioBusiness("High", "food", "ba:street_high", 2),
        };

        var ranked = DayStep.Rank(days, portfolio);

        Assert.Equal(new[] { "High", "Low" }, ranked.Select(row => row.Business).ToArray());
        Assert.Equal(75d, ranked[0].Profit);
        Assert.Equal(-20d, ranked[1].Profit);
        Assert.Equal("food", ranked[1].Family);
    }

    [Fact]
    public void DayStep_empty_or_null_curves_is_empty()
    {
        Assert.Empty(DayStep.Day(1, null).Rows);
        Assert.Empty(DayStep.Day(1, Array.Empty<StoreCurve>()).Rows);
        Assert.Empty(DayStep.Run(1, 2, null));
        Assert.Empty(DayStep.Run(1, 0, Curves.Fit(new[] { Day(1, Row("ba:street_a", 1, 1f, 0f, 0f, 0f)) })));
        Assert.Empty(DayStep.Portfolio(null, Array.Empty<PortfolioBusiness>()));
        Assert.Empty(DayStep.Rank(null, Array.Empty<PortfolioBusiness>()));
    }

    [Fact]
    public void DayStep_portfolio_drops_unmatched_streets_and_orders_by_street_then_number()
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
        var days = DayStep.Run(1000, 1, curves);
        var portfolio = new[]
        {
            new PortfolioBusiness("Zed", "office", "ba:street_z", 1),
            new PortfolioBusiness("Ann Nine", "food", "ba:street_a", 9),
            new PortfolioBusiness("Ann Two", "food", "ba:street_a", 2),
            new PortfolioBusiness("Ghost", "service", "ba:street_missing", 99),
        };

        var rows = DayStep.Portfolio(days, portfolio);

        Assert.Equal(3, rows.Count);
        Assert.Equal(("Ann Two", "ba:street_a", 2, 9d), (rows[0].Business, rows[0].Street, rows[0].Number, rows[0].Tco));
        Assert.Equal(("Ann Nine", "ba:street_a", 9, 6d), (rows[1].Business, rows[1].Street, rows[1].Number, rows[1].Tco));
        Assert.Equal(("Zed", "ba:street_z", 1, 3d), (rows[2].Business, rows[2].Street, rows[2].Number, rows[2].Tco));
    }

    [Fact]
    public void DayStep_day_keeps_projected_stores_in_street_number_order()
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

        var stepped = DayStep.Day(42, curves);

        Assert.Equal(42, stepped.Day);
        Assert.Equal(3, stepped.Rows.Count);
        Assert.Equal(("ba:street_a", 2), (stepped.Rows[0].Street, stepped.Rows[0].Number));
        Assert.Equal(("ba:street_a", 9), (stepped.Rows[1].Street, stepped.Rows[1].Number));
        Assert.Equal(("ba:street_z", 1), (stepped.Rows[2].Street, stepped.Rows[2].Number));
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
}
