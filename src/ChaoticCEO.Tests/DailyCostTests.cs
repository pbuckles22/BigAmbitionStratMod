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
        Assert.Equal(
            "cost.read version=" + LoadLog.Version
                + " day=5 cash=100 wages=30 rent=35 marketing=5 loan=6 truck=4 unassigned=0 daily=80 reserve=560",
            line);
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
    public void Day_2_seventh_avenue_cost_read_matches_the_pass()
    {
        var days = new[]
        {
            new FinancialDay(
                1,
                new[]
                {
                    new StatementRow("ba:street_seventhavenue", 26, 0f, 0f, 0f, 0f, 0f, 0f, 0f),
                }),
        };
        var parts = DailyCost.Read(days, today: 2, loanPayments: 0m, truckCost: 0m);
        var profit = Money.ProjectedProfit(TopBar.Yesterday(days, 2));
        var flag = Audit.CashBelowReserve(15_000m, parts.Daily);
        var line = DailyCost.Line(2, cash: 15_000m, parts);
        var summary = Audit.Summary(2, new AuditCounts(0, 0, 0, 0, 0, 1, flag, 0), 0);

        Assert.Equal(0m, profit);
        Assert.NotEqual(40m, profit);
        Assert.Equal(0, flag);
        Assert.Equal(
            "cost.read version=" + LoadLog.Version
                + " day=2 cash=15000 wages=0 rent=0 marketing=0 loan=0 truck=0 unassigned=0 daily=0 reserve=0",
            line);
        Assert.Equal(
            "audit.summary version=" + LoadLog.Version
                + " day=2 unconnected=0 emptySlots=0 dry=0 mixed=0 unstaffed=0 missingPlans=1 cashBelowReserve=0 pinMismatch=0 healed=0",
            summary);
    }

    [Fact]
    public void Chain_statements_set_the_flag_on_the_audit_line()
    {
        // Chain: statements → daily cost → reserve → audit
        var days = YesterdayAndToday();
        var cost = DailyCost.From(days, today: 5, loanPayments: 6m, truckCost: 4m);
        var reserve = Money.Reserve(cost);
        var flag = Audit.CashBelowReserve(100m, cost);
        var summary = Audit.Summary(5, new AuditCounts(0, 0, 0, 0, 0, 0, flag, 0), 0);

        Assert.Equal(80m, cost);
        Assert.Equal(560m, reserve);
        Assert.Equal(1, flag);
        Assert.Contains("cashBelowReserve=1", summary);
        Assert.Contains("healed=0", summary);
    }

    [Fact]
    public void Record_appends_the_cost_line()
    {
        using var dir = new TempLog();
        var days = YesterdayAndToday();
        var parts = DailyCost.Read(days, today: 5, loanPayments: 6m, truckCost: 4m);
        var line = DailyCost.Line(5, cash: 100m, parts);
        var recorded = DailyCost.Record(dir.Path, line);
        var blank = DailyCost.Record("", line);
        var file = File.ReadAllText(LoadLog.CeoLogPath(dir.Path)).Trim();

        Assert.Equal(line, recorded);
        Assert.Equal(line, blank);
        Assert.Equal(line, file);
        Assert.Contains("daily=80", file);
        Assert.Contains("reserve=560", file);
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

    private sealed class TempLog : IDisposable
    {
        public TempLog()
        {
            Path = Directory.CreateTempSubdirectory("chaotic-cost").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
