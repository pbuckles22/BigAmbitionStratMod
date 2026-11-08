using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class MoneyDecideTests
{
    [Fact]
    public void Money_names_reserve_days_and_day_spend()
    {
        Assert.Equal(7, Money.ReserveDays);
        Assert.Equal(0.25m, Money.DaySpend);
        Assert.Equal(BankLoan.LoanCover, Money.LoanCover);
    }

    [Fact]
    public void Money_reserve_is_reserve_days_of_total_costs()
    {
        Assert.Equal(70m, Money.Reserve(10m));
        Assert.Equal(0m, Money.Reserve(0m));
        Assert.Equal(0m, Money.Reserve(-1m));
    }

    [Fact]
    public void Money_spend_room_is_day_spend_of_cash_above_reserve()
    {
        Assert.Equal(25m, Money.SpendRoom(cash: 170m, dailyCost: 10m));
        Assert.Equal(0m, Money.SpendRoom(cash: 70m, dailyCost: 10m));
        Assert.Equal(0m, Money.SpendRoom(cash: 69m, dailyCost: 10m));
    }

    [Fact]
    public void Money_decide_stage_one_takes_the_largest_loan_under_loan_cover()
    {
        var pick = Money.Decide(
            1,
            cash: 1_000m,
            dailyCost: 10m,
            projectedDailyProfit: 200m,
            offers: new[]
            {
                new LoanOffer(1_000m, 80),
                new LoanOffer(2_000m, 100),
                new LoanOffer(3_000m, 120),
            },
            loan: null,
            bestPaybackRate: 0.1m);

        Assert.True(pick.Chosen);
        Assert.Equal("loan", pick.Action);
        Assert.Equal(2_000m, pick.Amount);
        Assert.Equal(100, pick.DailyPayment);
    }

    [Fact]
    public void Money_decide_rejects_a_loan_whose_daily_payment_beats_loan_cover()
    {
        var pick = Money.Decide(
            1,
            cash: 1_000m,
            dailyCost: 10m,
            projectedDailyProfit: 100m,
            offers: new[]
            {
                new LoanOffer(2_000m, 60),
            },
            loan: null,
            bestPaybackRate: 0.1m);

        Assert.False(pick.Chosen);
        Assert.Equal("", pick.Action);
        Assert.Equal(0m, pick.Amount);
    }

    [Fact]
    public void Money_decide_repays_when_interest_beats_the_best_payback_rate()
    {
        var pick = Money.Decide(
            2,
            cash: 50_000m,
            dailyCost: 10m,
            projectedDailyProfit: 200m,
            offers: Array.Empty<LoanOffer>(),
            loan: new LoanBalance(remaining: 5_000m, dailyInterest: 20, interestRate: 0.12m),
            bestPaybackRate: 0.08m);

        Assert.True(pick.Chosen);
        Assert.Equal("repay", pick.Action);
        Assert.Equal(5_000m, pick.Amount);
    }

    [Fact]
    public void Money_decide_keeps_a_loan_whose_interest_does_not_beat_payback()
    {
        var pick = Money.Decide(
            2,
            cash: 50_000m,
            dailyCost: 10m,
            projectedDailyProfit: 200m,
            offers: Array.Empty<LoanOffer>(),
            loan: new LoanBalance(remaining: 5_000m, dailyInterest: 20, interestRate: 0.05m),
            bestPaybackRate: 0.08m);

        Assert.False(pick.Chosen);
        Assert.Equal("", pick.Action);
    }

    [Fact]
    public void Money_decide_repay_beats_a_fresh_loan_when_both_qualify()
    {
        var pick = Money.Decide(
            1,
            cash: 50_000m,
            dailyCost: 10m,
            projectedDailyProfit: 200m,
            offers: new[]
            {
                new LoanOffer(2_000m, 80),
            },
            loan: new LoanBalance(remaining: 1_000m, dailyInterest: 10, interestRate: 0.2m),
            bestPaybackRate: 0.05m);

        Assert.True(pick.Chosen);
        Assert.Equal("repay", pick.Action);
        Assert.Equal(1_000m, pick.Amount);
    }

    [Fact]
    public void Money_decide_stage_two_does_not_take_a_bootstrap_loan()
    {
        var pick = Money.Decide(
            2,
            cash: 50_000m,
            dailyCost: 10m,
            projectedDailyProfit: 200m,
            offers: new[]
            {
                new LoanOffer(2_000m, 80),
            },
            loan: null,
            bestPaybackRate: 0.1m);

        Assert.False(pick.Chosen);
        Assert.Equal("", pick.Action);
    }

    [Fact]
    public void Money_decide_skips_blank_offers_and_empty_lists()
    {
        var missing = Money.Decide(1, 1_000m, 10m, 200m, null, null, 0.1m);
        var empty = Money.Decide(1, 1_000m, 10m, 200m, Array.Empty<LoanOffer>(), null, 0.1m);

        Assert.False(missing.Chosen);
        Assert.False(empty.Chosen);
    }

    [Fact]
    public void Money_spend_room_never_spends_the_reserve()
    {
        var reserve = Money.Reserve(40m);
        Assert.Equal(280m, reserve);
        Assert.Equal(0m, Money.SpendRoom(cash: reserve, dailyCost: 40m));
        Assert.Equal(5m, Money.SpendRoom(cash: reserve + 20m, dailyCost: 40m));
    }
}
