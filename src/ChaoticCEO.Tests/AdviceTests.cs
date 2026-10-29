using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class AdviceTests
{
    [Fact]
    public void Advice_locks_one_explain_line()
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
        var overlay = Overlay.Build(days, portfolio, Array.Empty<WarehouseHold>());
        var red = overlay.Where(row => row.Tone == "red").ToArray();
        var close = red[red.Length - 1];

        var advice = Advice.Explain(new[] { close });

        Assert.Single(advice);
        Assert.Equal("Low", advice[0].Business);
        Assert.Equal("red", advice[0].Tone);
        Assert.Equal("ba:street_a", advice[0].Street);
        Assert.Equal(1, advice[0].Number);
        Assert.Equal("Close Low. Profit -40.", advice[0].Explain);
        Assert.DoesNotContain("\n", advice[0].Explain);
        Assert.DoesNotContain("\r", advice[0].Explain);
    }

    [Fact]
    public void Advice_an_open_keeps_its_own_line()
    {
        var portfolio = new List<PortfolioBusiness>();
        for (var number = 1; number <= 3; number++)
        {
            portfolio.Add(Biz("Food " + number, "food", "ba:street_main", number));
            portfolio.Add(Biz("Service " + number, "service", "ba:street_main", number + 10));
        }

        portfolio.Add(Biz("Side 1", "food", "ba:street_side", 1));
        portfolio.Add(Biz("Side 2", "food", "ba:street_side", 2));

        var overlay = Overlay.Build(Array.Empty<FinancialDay>(), portfolio, Array.Empty<WarehouseHold>());
        var advice = Advice.Explain(overlay);

        Assert.Equal(2, advice.Count);
        Assert.Equal(
            new[]
            {
                "Open food on ba:street_main. 3 already.",
                "Open service on ba:street_main. 3 already.",
            },
            advice.Select(line => line.Explain));
        Assert.All(advice, line =>
        {
            Assert.Equal("open", line.Tone);
            Assert.Equal("ba:street_main", line.Street);
            Assert.Equal(3, line.Number);
            Assert.DoesNotContain("\n", line.Explain);
            Assert.DoesNotContain("\r", line.Explain);
        });
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
