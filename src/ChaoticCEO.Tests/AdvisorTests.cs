using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class AdvisorTests
{
    [Fact]
    public void Rank_locks_the_bottom()
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

        Assert.Equal(new[] { "High", "Low" }, Names(ranked));
        Assert.Equal(100d, ranked[0].Profit);
        Assert.Equal(-40d, ranked[ranked.Count - 1].Profit);
        Assert.Equal("food", ranked[ranked.Count - 1].Family);
        Assert.Equal("ba:street_a", ranked[ranked.Count - 1].Street);
        Assert.Equal(1, ranked[ranked.Count - 1].Number);
    }

    [Fact]
    public void Rank_orders_by_profit_not_sales()
    {
        var days = new[]
        {
            new FinancialDay(
                1,
                new[]
                {
                    new StatementRow("ba:street_a", 1, 99999f, 0f, 0f, 0f, 0f, 0f, -5f),
                    new StatementRow("ba:street_b", 2, 1f, 0f, 0f, 0f, 0f, 0f, 50f),
                }),
        };
        var portfolio = new[]
        {
            Biz("Busy", "food", "ba:street_a", 1),
            Biz("Quiet", "food", "ba:street_b", 2),
        };

        var ranked = Advisor.Rank(days, portfolio);

        Assert.Equal(new[] { "Quiet", "Busy" }, Names(ranked));
        Assert.Equal(-5d, ranked[ranked.Count - 1].Profit);
    }

    [Fact]
    public void Rank_tie_later_name_is_the_bottom()
    {
        var days = new[]
        {
            new FinancialDay(
                1,
                new[]
                {
                    Row("ba:street_a", 1, -5f),
                    Row("ba:street_b", 2, -5f),
                    Row("ba:street_c", 3, 20f),
                }),
        };
        var portfolio = new[]
        {
            Biz("Ann", "food", "ba:street_a", 1),
            Biz("Zed", "food", "ba:street_b", 2),
            Biz("Mid", "food", "ba:street_c", 3),
        };

        var ranked = Advisor.Rank(days, portfolio);

        Assert.Equal(new[] { "Mid", "Ann", "Zed" }, Names(ranked));
    }

    [Fact]
    public void Rank_unmatched_statement_is_not_ranked()
    {
        var days = new[]
        {
            new FinancialDay(
                1,
                new[]
                {
                    Row("ba:street_a", 1, -1000f),
                    Row("ba:street_b", 2, 10f),
                }),
        };
        var portfolio = new[] { Biz("Known", "food", "ba:street_b", 2) };

        var ranked = Advisor.Rank(days, portfolio);

        Assert.Equal(new[] { "Known" }, Names(ranked));
        Assert.Equal(10d, ranked[0].Profit);
    }

    [Fact]
    public void Rank_skips_a_blank_business()
    {
        var days = new[]
        {
            new FinancialDay(
                1,
                new[]
                {
                    Row("ba:street_a", 1, -1000f),
                    Row("ba:street_b", 2, 10f),
                }),
        };
        var portfolio = new[]
        {
            Biz("", "food", "ba:street_a", 1),
            Biz("Known", "food", "ba:street_b", 2),
        };

        var ranked = Advisor.Rank(days, portfolio);

        Assert.Equal(new[] { "Known" }, Names(ranked));
    }

    [Fact]
    public void CloseCandidates_drops_the_best_and_keeps_the_lowest()
    {
        var rows = new StatementRow[11];
        var portfolio = new PortfolioBusiness[11];
        for (var index = 0; index < 11; index++)
        {
            rows[index] = Row("ba:street_a", index, index);
            portfolio[index] = Biz("Shop " + index, "food", "ba:street_a", index);
        }

        var candidates = Advisor.CloseCandidates(new[] { new FinancialDay(1, rows) }, portfolio);

        Assert.Equal(10, candidates.Count);
        Assert.Equal("Shop 0", candidates[candidates.Count - 1].Business);
        Assert.DoesNotContain(candidates, row => row.Business == "Shop 10");
        Assert.Equal("Shop 0", Advisor.Rank(new[] { new FinancialDay(1, rows) }, portfolio).Last().Business);
    }

    [Fact]
    public void CloseCandidates_tie_keeps_the_later_name()
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

        var candidates = Advisor.CloseCandidates(new[] { new FinancialDay(1, rows) }, portfolio);

        Assert.Equal(10, candidates.Count);
        Assert.Equal("Zed", candidates[0].Business);
        Assert.DoesNotContain(candidates, row => row.Business == "Ann");
    }

    [Fact]
    public void CloseCandidates_keeps_a_short_list_in_rank_order()
    {
        var days = new[]
        {
            new FinancialDay(
                1,
                new[]
                {
                    Row("ba:street_a", 1, 100f),
                    Row("ba:street_b", 2, -40f),
                    Row("ba:street_c", 3, 5f),
                }),
        };
        var portfolio = new[]
        {
            Biz("High", "food", "ba:street_a", 1),
            Biz("Low", "food", "ba:street_b", 2),
            Biz("Mid", "service", "ba:street_c", 3),
        };

        var candidates = Advisor.CloseCandidates(days, portfolio);

        Assert.Equal(new[] { "High", "Mid", "Low" }, Names(candidates));
    }

    [Fact]
    public void StreetClusters_three_of_one_family_qualifies()
    {
        var portfolio = new[]
        {
            Biz("Food 1", "food", "ba:street_main", 1),
            Biz("Food 2", "food", "ba:street_main", 2),
            Biz("Food 3", "food", "ba:street_main", 3),
            Biz("Side 1", "food", "ba:street_side", 1),
            Biz("Side 2", "food", "ba:street_side", 2),
            Biz("Cut", "service", "ba:street_main", 8),
            Biz("Cut 2", "service", "ba:street_main", 9),
        };

        var clusters = Advisor.StreetClusters(portfolio);

        Assert.Equal(new[] { ("ba:street_main", "food", 3) }, Clusters(clusters));
    }

    [Fact]
    public void StreetClusters_two_families_on_one_street_both_qualify()
    {
        var portfolio = new List<PortfolioBusiness>();
        for (var number = 1; number <= 3; number++)
        {
            portfolio.Add(Biz("Food " + number, "food", "ba:street_main", number));
            portfolio.Add(Biz("Service " + number, "service", "ba:street_main", number + 10));
        }

        var clusters = Advisor.StreetClusters(portfolio);

        Assert.Equal(
            new[]
            {
                ("ba:street_main", "food", 3),
                ("ba:street_main", "service", 3),
            },
            Clusters(clusters));
    }

    [Fact]
    public void StreetClusters_skips_a_blank_family()
    {
        var portfolio = new[]
        {
            Biz("One", "", "ba:street_main", 1),
            Biz("Two", "", "ba:street_main", 2),
            Biz("Three", "", "ba:street_main", 3),
            Biz("Food", "food", "", 4),
        };

        Assert.Empty(Advisor.StreetClusters(portfolio));
    }

    [Fact]
    public void WarehouseFamilyMix_one_unknown_item_is_not_a_mix()
    {
        var held = new[]
        {
            new WarehouseHold("W4", "ba:street_a", 1, "ba:itemname_notinstock", 1),
        };

        Assert.Empty(Advisor.WarehouseFamilyMix(held));
    }

    [Fact]
    public void WarehouseFamilyMix_two_families_mix_and_a_lamp_does_not()
    {
        var held = new[]
        {
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_sodacan", 45),
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_paperbag", 1),
            new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_ceilinglampwarehouse", 1),
            new WarehouseHold("W6", "ba:street_fifthavenue", 43, "ba:itemname_sodacan", 1),
            new WarehouseHold("W6", "ba:street_fifthavenue", 43, "ba:itemname_haircareproduct", 1),
            new WarehouseHold("W2", "ba:street_b", 2, "ba:itemname_burger", 1),
            new WarehouseHold("W2", "ba:street_b", 2, "ba:itemname_cheapgift", 1),
            new WarehouseHold("W10", "ba:street_a", 1, "ba:itemname_burger", 1),
            new WarehouseHold("W10", "ba:street_a", 1, "ba:itemname_notinstock", 1),
        };

        var mixes = Advisor.WarehouseFamilyMix(held);

        Assert.Equal(new[] { "W2", "W6", "W10" }, mixes.Select(row => row.Warehouse));
        Assert.Equal("food, specialty", mixes[0].Families);
        Assert.Equal("food, service", mixes[1].Families);
        Assert.Equal("food, unknown", mixes[2].Families);
        Assert.Equal("ba:street_b", mixes[0].Street);
        Assert.Equal(2, mixes[0].Number);
    }

    [Fact]
    public void WarehouseFamilyMix_keeps_each_address()
    {
        var held = new[]
        {
            new WarehouseHold("W1", "ba:street_a", 2, "ba:itemname_sodacan", 1),
            new WarehouseHold("W1", "ba:street_a", 2, "ba:itemname_cheapgift", 1),
            new WarehouseHold("W1", "ba:street_a", 1, "ba:itemname_burger", 1),
            new WarehouseHold("W1", "ba:street_a", 1, "ba:itemname_novel", 1),
        };

        var mixes = Advisor.WarehouseFamilyMix(held);

        Assert.Equal(new[] { 1, 2 }, mixes.Select(row => row.Number));
        Assert.All(mixes, row => Assert.Equal("food, specialty", row.Families));
    }

    private static StatementRow Row(string street, int number, float profit)
    {
        return new StatementRow(street, number, 0f, 0f, 0f, 0f, 0f, 0f, profit);
    }

    private static PortfolioBusiness Biz(string business, string family, string street, int number)
    {
        return new PortfolioBusiness(business, family, street, number);
    }

    private static string[] Names(IReadOnlyList<RankedBusiness> rows)
    {
        return rows.Select(row => row.Business).ToArray();
    }

    private static (string Street, string Family, int Count)[] Clusters(IReadOnlyList<StreetCluster> rows)
    {
        return rows.Select(row => (row.Street, row.Family, row.Count)).ToArray();
    }
}
