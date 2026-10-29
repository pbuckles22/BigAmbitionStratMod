using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class OverlayTests
{
    [Fact]
    public void Overlay_locks_the_red_row()
    {
        var days = new[]
        {
            new FinancialDay(
                1,
                new[]
                {
                    Row("ba:street_a", 1, 0f),
                    Row("ba:street_b", 2, 100f),
                }),
            new FinancialDay(2, new[] { Row("ba:street_a", 1, -80f) }),
        };
        var portfolio = new[]
        {
            Biz("Low", "food", "ba:street_a", 1),
            Biz("High", "food", "ba:street_b", 2),
        };

        var ranked = Advisor.Rank(days, portfolio);
        var rows = Overlay.Build(days, portfolio, Array.Empty<WarehouseHold>());

        var red = rows.Where(row => row.Tone == "red").ToArray();
        Assert.Equal(new[] { "High", "Low" }, red.Select(row => row.Business));
        Assert.Equal(ranked[ranked.Count - 1].Business, red[red.Length - 1].Business);
        Assert.Equal("ba:street_a", red[red.Length - 1].Street);
        Assert.Equal(1, red[red.Length - 1].Number);
        Assert.Equal(-40d, red[red.Length - 1].Profit);
        Assert.Equal("food", red[red.Length - 1].Detail);
    }

    [Fact]
    public void Overlay_warns_on_a_mixed_warehouse()
    {
        var holds = new[]
        {
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_sodacan", 45),
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_paperbag", 1),
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_ceilinglampwarehouse", 1),
            new WarehouseHold("W6", "ba:street_fifthavenue", 43, "ba:itemname_sodacan", 1),
            new WarehouseHold("W6", "ba:street_fifthavenue", 43, "ba:itemname_haircareproduct", 1),
        };

        var rows = Overlay.Build(Array.Empty<FinancialDay>(), Array.Empty<PortfolioBusiness>(), holds);

        var warnings = rows.Where(row => row.Tone == "warning").ToArray();
        Assert.Equal(new[] { "W6" }, warnings.Select(row => row.Business));
        Assert.Equal("food, service", warnings[0].Detail);
        Assert.Equal("ba:street_fifthavenue", warnings[0].Street);
        Assert.Equal(43, warnings[0].Number);
        Assert.DoesNotContain(rows, row => row.Business == "W1");
    }

    [Fact]
    public void Overlay_names_where_to_open_next()
    {
        var portfolio = new List<PortfolioBusiness>();
        for (var number = 1; number <= 3; number++)
        {
            portfolio.Add(Biz("Food " + number, "food", "ba:street_main", number));
            portfolio.Add(Biz("Service " + number, "service", "ba:street_main", number + 10));
        }

        portfolio.Add(Biz("Side 1", "food", "ba:street_side", 1));
        portfolio.Add(Biz("Side 2", "food", "ba:street_side", 2));

        var rows = Overlay.Build(Array.Empty<FinancialDay>(), portfolio, Array.Empty<WarehouseHold>());

        var opens = rows.Where(row => row.Tone == "open").ToArray();
        Assert.Equal(
            new[]
            {
                ("ba:street_main", "food", 3),
                ("ba:street_main", "service", 3),
            },
            opens.Select(row => (row.Street, row.Detail, row.Number)));
    }

    [Fact]
    public void Overlay_a_winner_is_not_a_red_row()
    {
        var rows = new StatementRow[11];
        var portfolio = new PortfolioBusiness[11];
        for (var index = 0; index < 11; index++)
        {
            rows[index] = Row("ba:street_a", index, index);
            portfolio[index] = Biz("Shop " + index, "food", "ba:street_a", index);
        }

        var days = new[] { new FinancialDay(1, rows) };
        var overlay = Overlay.Build(days, portfolio, Array.Empty<WarehouseHold>());
        var red = overlay.Where(row => row.Tone == "red").ToArray();
        var candidates = Advisor.CloseCandidates(days, portfolio);

        Assert.Equal(candidates.Select(row => row.Business), red.Select(row => row.Business));
        Assert.Equal("Shop 0", red[red.Length - 1].Business);
        Assert.Equal(0d, red[red.Length - 1].Profit);
        Assert.DoesNotContain(red, row => row.Business == "Shop 10");
    }

    [Fact]
    public void Overlay_tie_keeps_the_later_name_red()
    {
        var rows = new List<StatementRow>
        {
            Row("ba:street_ann", 1, 50f),
            Row("ba:street_zed", 2, 50f),
        };
        var portfolio = new List<PortfolioBusiness>
        {
            Biz("Ann", "office", "ba:street_ann", 1),
            Biz("Zed", "office", "ba:street_zed", 2),
        };
        for (var index = 0; index < 9; index++)
        {
            rows.Add(Row("ba:street_shop", index, 0f));
            portfolio.Add(Biz("Shop " + index, "food", "ba:street_shop", index));
        }

        var red = Overlay.Build(new[] { new FinancialDay(1, rows) }, portfolio, Array.Empty<WarehouseHold>())
            .Where(row => row.Tone == "red")
            .ToArray();

        Assert.Equal("Zed", red[0].Business);
        Assert.Equal("Shop 8", red[red.Length - 1].Business);
        Assert.Equal(0d, red[red.Length - 1].Profit);
        Assert.DoesNotContain(red, row => row.Business == "Ann");
    }

    private static StatementRow Row(string street, int number, float profit)
    {
        return new StatementRow(street, number, 0f, 0f, 0f, 0f, 0f, 0f, profit);
    }

    private static PortfolioBusiness Biz(string business, string family, string street, int number)
    {
        return new PortfolioBusiness(business, family, street, number);
    }
}
