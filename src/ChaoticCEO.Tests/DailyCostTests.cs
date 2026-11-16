using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class DailyCostTests
{
    [Fact]
    public void Reserve_is_seven_times_yesterdays_cost()
    {
        var days = YesterdayAndToday();
        var parts = DailyCost.Read(days, today: 5, loanPayments: 6m, truckCost: 4m);
        var reserve = Money.Reserve(parts.Daily);
        var line = DailyCost.Line(5, cash: 100m, parts);

        Assert.Equal(30m, parts.Wages);
        Assert.Equal(35m, parts.Rent);
        Assert.Equal(5m, parts.Marketing);
        Assert.Equal(6m, parts.Loan);
        Assert.Equal(4m, parts.Truck);
        Assert.Equal(0m, parts.Unassigned);
        Assert.Equal(80m, parts.Daily);
        Assert.Equal(560m, reserve);
        Assert.Equal(0m, DailyCost.From(null, 5, 0m, 0m));
        Assert.Contains("cost.read version=" + LoadLog.Version, line);
        Assert.Contains("daily=80", line);
        Assert.Contains("reserve=560", line);
        Assert.Contains("truck=4", line);
    }

    [Fact]
    public void Zero_profit_is_not_the_gift_shop_forty()
    {
        var gift = 0m;
        foreach (var starter in Starter.StageOne())
        {
            if (starter.BusinessType == "ba:businesstype_giftshop")
            {
                gift = starter.DailyProfit;
            }
        }

        var projected = Money.ProjectedProfit(0m);
        var realGiftDay = Money.ProjectedProfit(gift);
        var offer = new LoanOffer(BankLoan.MinimumAmount, (int)(gift * BankLoan.LoanCover));
        var fromZero = Money.Decide(
            1,
            cash: 10_000m,
            dailyCost: 80m,
            projectedDailyProfit: projected,
            offers: new[] { offer },
            loan: null,
            bestPaybackRate: 0.1m);
        var fromGiftDay = Money.Decide(
            1,
            cash: 10_000m,
            dailyCost: 80m,
            projectedDailyProfit: realGiftDay,
            offers: new[] { offer },
            loan: null,
            bestPaybackRate: 0.1m);

        Assert.Equal(40m, gift);
        Assert.Equal(0m, projected);
        Assert.NotEqual(gift, projected);
        Assert.False(fromZero.Chosen);
        Assert.Equal("", fromZero.Action);
        Assert.True(fromGiftDay.Chosen);
        Assert.Equal("loan", fromGiftDay.Action);
        Assert.Equal(gift, realGiftDay);
    }

    [Fact]
    public void Chain_statements_set_the_reserve_that_money_reads()
    {
        // Chain: statements → daily cost → reserve → money
        var days = YesterdayAndToday();
        var cost = DailyCost.From(days, today: 5, loanPayments: 6m, truckCost: 4m);
        var reserve = Money.Reserve(cost);
        var profit = Money.ProjectedProfit(TopBar.Yesterday(days, 5));
        var pick = Money.Decide(
            1,
            cash: 100m,
            dailyCost: cost,
            projectedDailyProfit: profit,
            offers: new[] { new LoanOffer(2_000m, 80) },
            loan: null,
            bestPaybackRate: 0.1m);

        Assert.Equal(80m, cost);
        Assert.Equal(560m, reserve);
        Assert.NotEqual(0m, reserve);
        Assert.Equal(200m, profit);
        Assert.Equal(1, Audit.CashBelowReserve(100m, cost));
        Assert.Equal(0, Audit.CashBelowReserve(reserve, cost));
        Assert.True(pick.Chosen);
        Assert.Equal("loan", pick.Action);
        Assert.Equal(2_000m, pick.Amount);
        Assert.Equal(80, pick.DailyPayment);
    }

    [Fact]
    public void Theft_and_a_later_day_stay_out_of_the_reserve()
    {
        var days = new[]
        {
            new FinancialDay(
                4,
                new[]
                {
                    new StatementRow("ba:street_a", 1, 900f, 500f, 10f, 10f, 0f, 400f, 0f),
                    null!,
                }),
            new FinancialDay(
                5,
                new[]
                {
                    new StatementRow("ba:street_a", 1, 0f, 0f, 0f, 900f, 0f, 0f, 0f),
                }),
        };
        var cost = DailyCost.From(days, today: 5, loanPayments: -3m, truckCost: 0m);
        var reserve = Money.Reserve(cost);

        Assert.Equal(20m, cost);
        Assert.Equal(140m, reserve);
        Assert.Equal(0, Audit.CashBelowReserve(140m, cost));
        Assert.Equal(23m, DailyCost.From(days, 5, loanPayments: 0m, truckCost: 0m, unassignedWages: 3m));
    }

    [Fact]
    public void A_loan_without_a_statement_still_sets_the_reserve()
    {
        var cost = DailyCost.From(Array.Empty<FinancialDay>(), today: 2, loanPayments: 5m, truckCost: 2m);
        var reserve = Money.Reserve(cost);

        Assert.Equal(7m, cost);
        Assert.Equal(49m, reserve);
        Assert.Equal(1, Audit.CashBelowReserve(48m, cost));
        Assert.Equal(0, Audit.CashBelowReserve(49m, cost));
    }

    [Fact]
    public void Chain_the_reserve_keeps_a_lot_that_spends_it()
    {
        // Chain: statements → daily cost → reserve → where
        var days = YesterdayAndToday();
        var cost = DailyCost.From(days, today: 5, loanPayments: 6m, truckCost: 4m);
        var reserve = Money.Reserve(cost);
        var lot = new SiteCandidate("ba:street_a", 9, familyOnStreet: 0, traffic: 20m, dailyRent: 10m, cost: 100m);
        var blocked = Where.Decide(new[] { lot }, cash: reserve, reserve);
        var room = Where.Decide(new[] { lot }, cash: reserve + 100m, reserve);

        Assert.Equal(560m, reserve);
        Assert.False(blocked.Chosen);
        Assert.Equal("", blocked.Street);
        Assert.True(room.Chosen);
        Assert.Equal("ba:street_a", room.Street);
        Assert.Equal(9, room.Number);
    }

    private static FinancialDay[] YesterdayAndToday()
    {
        return new[]
        {
            new FinancialDay(
                3,
                new[]
                {
                    new StatementRow("ba:street_old", 1, 0f, 0f, 999f, 999f, 999f, 0f, 999f),
                }),
            new FinancialDay(
                4,
                new[]
                {
                    new StatementRow("ba:street_a", 1, 900f, 500f, 20f, 30f, 4f, 100f, 50f),
                    new StatementRow("ba:street_b", 2, 100f, 40f, 10f, 5f, 1f, 0f, 150f),
                    new StatementRow("ba:street_c", 3, 0f, 0f, -8f, -4f, 0f, 0f, 0f),
                }),
            new FinancialDay(
                5,
                new[]
                {
                    new StatementRow("ba:street_a", 1, 0f, 0f, 999f, 999f, 999f, 0f, 0f),
                }),
        };
    }
}
