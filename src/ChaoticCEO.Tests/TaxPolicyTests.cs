using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class TaxPolicyTests
{
    [Fact]
    public void TaxPolicy_names_the_reserve_rate_the_survival_floor_and_the_shield()
    {
        Assert.Equal(0.25m, TaxPolicy.EstimatedTaxRate);
        Assert.Equal(3, TaxPolicy.MinSurvivalDays);
        Assert.Equal(500m, TaxPolicy.MinTaxPartialStep);
        Assert.Equal(2, TaxPolicy.UrgentTaxDays);
        Assert.Equal(5, TaxPolicy.TaxShieldWindowDays);
        Assert.Equal(0.60m, TaxPolicy.TaxShieldDaySpend);
        Assert.Equal(Money.ReserveDays, 7);
    }

    [Fact]
    public void TaxPolicy_accrues_unbilled_tax_from_profit_minus_capex()
    {
        var accrued = TaxPolicy.UnbilledAccruedTax(cycleNetProfit: 100_000m, deductibleCapEx: 20_000m);
        var wiped = TaxPolicy.UnbilledAccruedTax(cycleNetProfit: 10_000m, deductibleCapEx: 40_000m);
        var loss = TaxPolicy.UnbilledAccruedTax(cycleNetProfit: -5_000m, deductibleCapEx: 0m);
        var ignored = TaxPolicy.UnbilledAccruedTax(cycleNetProfit: 8_000m, deductibleCapEx: -1_000m);

        Assert.Equal(20_000m, accrued);
        Assert.Equal(0m, wiped);
        Assert.Equal(0m, loss);
        Assert.Equal(2_000m, ignored);
    }

    [Fact]
    public void TaxPolicy_reserve_is_seven_days_plus_billed_and_unbilled_tax()
    {
        var unbilled = TaxPolicy.UnbilledAccruedTax(100_000m, 20_000m);
        var taxReserve = TaxPolicy.TotalTaxReserve(
            billedCurrentTax: 5_000m,
            billedBackTax: 1_000m,
            unbilledAccruedTax: unbilled);
        var reserve = TaxPolicy.TotalReserve(dailyCost: 10m, taxReserve);

        Assert.Equal(20_000m, unbilled);
        Assert.Equal(26_000m, taxReserve);
        Assert.Equal(70m + 26_000m, reserve);
        Assert.Equal(0m, TaxPolicy.TotalTaxReserve(-5m, -8m, -1m));
        Assert.Equal(0m, TaxPolicy.TotalReserve(-10m, -4m));
    }

    [Fact]
    public void TaxPolicy_back_tax_pay_stops_at_three_survival_days()
    {
        var pick = TaxPolicy.DecidePayment(
            cash: 10_000m,
            dailyCost: 1_000m,
            billedCurrentTax: 4_000m,
            billedBackTax: 8_000m,
            daysUntilDue: 1);

        Assert.True(pick.Chosen);
        Assert.Equal("back-partial", pick.Kind);
        Assert.Equal(7_000m, pick.Amount);
        Assert.Equal(3_000m, 10_000m - pick.Amount);
        Assert.Equal(TaxPolicy.MinSurvivalDays * 1_000m, 10_000m - pick.Amount);
    }

    [Fact]
    public void TaxPolicy_pays_back_tax_in_full_when_three_days_still_remain()
    {
        var pick = TaxPolicy.DecidePayment(
            cash: 12_000m,
            dailyCost: 1_000m,
            billedCurrentTax: 4_000m,
            billedBackTax: 8_000m,
            daysUntilDue: 1);

        Assert.True(pick.Chosen);
        Assert.Equal("back", pick.Kind);
        Assert.Equal(8_000m, pick.Amount);
    }

    [Fact]
    public void TaxPolicy_partial_back_tax_starts_at_the_five_hundred_step()
    {
        var step = TaxPolicy.DecidePayment(
            cash: 3_500m,
            dailyCost: 1_000m,
            billedCurrentTax: 0m,
            billedBackTax: 8_000m,
            daysUntilDue: 0);
        var under = TaxPolicy.DecidePayment(
            cash: 3_499m,
            dailyCost: 1_000m,
            billedCurrentTax: 0m,
            billedBackTax: 8_000m,
            daysUntilDue: 0);

        Assert.True(step.Chosen);
        Assert.Equal("back-partial", step.Kind);
        Assert.Equal(500m, step.Amount);
        Assert.Equal(3_000m, 3_500m - step.Amount);
        Assert.False(under.Chosen);
        Assert.Equal(0m, under.Amount);
        Assert.Equal(
            "tax.held version=" + LoadLog.Version + " day=4 reason=survival",
            TaxPolicy.HoldLine(4));
    }

    [Fact]
    public void TaxPolicy_a_current_bill_that_is_not_due_stays_above_the_seven_day_reserve()
    {
        var pick = TaxPolicy.DecidePayment(
            cash: 12_000m,
            dailyCost: 1_000m,
            billedCurrentTax: 10_000m,
            billedBackTax: 0m,
            daysUntilDue: 10);

        Assert.True(pick.Chosen);
        Assert.Equal("current-partial", pick.Kind);
        Assert.Equal(5_000m, pick.Amount);
        Assert.Equal(7_000m, 12_000m - pick.Amount);
        Assert.NotEqual(9_000m, pick.Amount);
    }

    [Fact]
    public void TaxPolicy_pays_no_back_tax_when_the_survival_room_is_under_the_partial_step()
    {
        var pick = TaxPolicy.DecidePayment(
            cash: 3_400m,
            dailyCost: 1_000m,
            billedCurrentTax: 0m,
            billedBackTax: 8_000m,
            daysUntilDue: 0);

        Assert.False(pick.Chosen);
        Assert.Equal("", pick.Kind);
        Assert.Equal(0m, pick.Amount);
    }

    [Fact]
    public void TaxPolicy_urgent_current_tax_pays_down_to_three_survival_days()
    {
        var pick = TaxPolicy.DecidePayment(
            cash: 12_000m,
            dailyCost: 1_000m,
            billedCurrentTax: 10_000m,
            billedBackTax: 0m,
            daysUntilDue: 1);

        Assert.True(pick.Chosen);
        Assert.Equal("current-partial", pick.Kind);
        Assert.Equal(9_000m, pick.Amount);
    }

    [Fact]
    public void TaxPolicy_shield_spend_is_sixty_percent_inside_five_days_for_a_deductible_buy()
    {
        var ordinary = TaxPolicy.EffectiveSpendRoom(
            cash: 100_000m,
            dailyCost: 2_000m,
            taxReserve: 20_000m,
            isDeductibleCapEx: false,
            daysUntilTaxAssessment: 3);
        var shield = TaxPolicy.EffectiveSpendRoom(
            cash: 100_000m,
            dailyCost: 2_000m,
            taxReserve: 20_000m,
            isDeductibleCapEx: true,
            daysUntilTaxAssessment: 3);
        var lastShieldDay = TaxPolicy.EffectiveSpendRoom(
            cash: 100_000m,
            dailyCost: 2_000m,
            taxReserve: 20_000m,
            isDeductibleCapEx: true,
            daysUntilTaxAssessment: 5);
        var afterWindow = TaxPolicy.EffectiveSpendRoom(
            cash: 100_000m,
            dailyCost: 2_000m,
            taxReserve: 20_000m,
            isDeductibleCapEx: true,
            daysUntilTaxAssessment: 6);

        Assert.Equal(16_500m, ordinary);
        Assert.Equal(42_600m, shield);
        Assert.Equal(shield, lastShieldDay);
        Assert.Equal(17_750m, afterWindow);
    }

    [Fact]
    public void TaxPolicy_loan_principal_is_not_a_deduction()
    {
        var loan = TaxPolicy.EffectiveSpendRoom(
            cash: 100_000m,
            dailyCost: 2_000m,
            taxReserve: 20_000m,
            isDeductibleCapEx: false,
            daysUntilTaxAssessment: 3);
        var loanLater = TaxPolicy.EffectiveSpendRoom(
            cash: 100_000m,
            dailyCost: 2_000m,
            taxReserve: 20_000m,
            isDeductibleCapEx: false,
            daysUntilTaxAssessment: 9);
        var truck = TaxPolicy.EffectiveSpendRoom(
            cash: 100_000m,
            dailyCost: 2_000m,
            taxReserve: 20_000m,
            isDeductibleCapEx: true,
            daysUntilTaxAssessment: 3);

        Assert.Equal(16_500m, loan);
        Assert.Equal(loanLater, loan);
        Assert.True(truck > loan);
        Assert.Equal(Money.DaySpend, 0.25m);
        Assert.True(TaxPolicy.TaxShieldDaySpend > Money.DaySpend);
    }

    [Fact]
    public void TaxPolicy_playbook_shows_the_tax_pick_ahead_of_open_and_money()
    {
        var pay = TaxPolicy.DecidePayment(10_000m, 1_000m, 4_000m, 8_000m, 1);
        var step = Playbook.Decide(
            open: new OpenPick("ba:businesstype_giftshop", true),
            where: SitePick.None(),
            layout: LayoutPick.None(),
            prices: PricePick.None(),
            supply: SupplyPick.None(),
            trucks: TruckPick.None(),
            warehouses: WarehousePick.None(),
            managers: ManagerPick.None(),
            money: new MoneyPick("repay", 5_000m, 0, true),
            realEstate: EstatePick.None(),
            close: ClosePick.None(),
            run: new RunPick("tax", true),
            tax: pay);

        Assert.True(step.Chosen);
        Assert.Equal("tax", step.Kind);
        Assert.Equal("back-partial 7000", step.Detail);
        Assert.Equal("Next tax back-partial 7000", Playbook.Line(step));
    }

    [Fact]
    public void TaxPolicy_sleep_and_close_beat_the_tax_pick()
    {
        var pay = new TaxPaymentPick("back", 8_000m, true);
        var sleep = Playbook.Decide(
            open: OpenPick.None(),
            where: SitePick.None(),
            layout: LayoutPick.None(),
            prices: PricePick.None(),
            supply: SupplyPick.None(),
            trucks: TruckPick.None(),
            warehouses: WarehousePick.None(),
            managers: ManagerPick.None(),
            money: MoneyPick.None(),
            realEstate: EstatePick.None(),
            close: ClosePick.None(),
            run: new RunPick("sleep", true),
            tax: pay);
        var close = Playbook.Decide(
            open: OpenPick.None(),
            where: SitePick.None(),
            layout: LayoutPick.None(),
            prices: PricePick.None(),
            supply: SupplyPick.None(),
            trucks: TruckPick.None(),
            warehouses: WarehousePick.None(),
            managers: ManagerPick.None(),
            money: MoneyPick.None(),
            realEstate: EstatePick.None(),
            close: new ClosePick("close", true),
            run: RunPick.None(),
            tax: pay);

        Assert.Equal("run", sleep.Kind);
        Assert.Equal("sleep", sleep.Detail);
        Assert.Equal("close", close.Kind);
        Assert.Equal("close", close.Detail);
    }

    [Fact]
    public void Chain_profit_minus_capex_sets_the_tax_reserve_then_the_back_tax_pay_and_the_shield()
    {
        // Chain: profit − capex → tax reserve → pay back taxes → shield spend
        const decimal profit = 100_000m;
        const decimal capex = 20_000m;
        const decimal dailyCost = 1_000m;
        const decimal cash = 10_000m;
        var accrued = TaxPolicy.UnbilledAccruedTax(profit, capex);
        var taxReserve = TaxPolicy.TotalTaxReserve(
            billedCurrentTax: 4_000m,
            billedBackTax: 8_000m,
            unbilledAccruedTax: accrued);
        var reserve = TaxPolicy.TotalReserve(dailyCost, taxReserve);
        var pay = TaxPolicy.DecidePayment(cash, dailyCost, billedCurrentTax: 4_000m, billedBackTax: 8_000m, daysUntilDue: 1);
        var shield = TaxPolicy.EffectiveSpendRoom(
            cash: 100_000m,
            dailyCost: 2_000m,
            taxReserve: taxReserve,
            isDeductibleCapEx: true,
            daysUntilTaxAssessment: 3);
        var amounts = TaxPolicy.Amounts(pay);
        var paid = TaxBill.Pay(amounts.current, amounts.back, GameApi.Live());
        var step = Playbook.Decide(
            open: new OpenPick("ba:businesstype_giftshop", true),
            where: SitePick.None(),
            layout: LayoutPick.None(),
            prices: PricePick.None(),
            supply: SupplyPick.None(),
            trucks: TruckPick.None(),
            warehouses: WarehousePick.None(),
            managers: ManagerPick.None(),
            money: new MoneyPick("repay", 5_000m, 0, true),
            realEstate: EstatePick.None(),
            close: ClosePick.None(),
            run: RunPick.None(),
            tax: pay);

        Assert.Equal(20_000m, accrued);
        Assert.Equal(32_000m, taxReserve);
        Assert.Equal((dailyCost * Money.ReserveDays) + taxReserve, reserve);
        Assert.Equal("back-partial", pay.Kind);
        Assert.Equal(7_000m, pay.Amount);
        Assert.Equal(3_000m, cash - pay.Amount);
        Assert.Equal(37_200m, shield);
        Assert.Equal(0m, amounts.current);
        Assert.Equal(7_000m, amounts.back);
        Assert.True(paid.Paid);
        Assert.Equal(0m, paid.Current);
        Assert.Equal(7_000m, paid.Back);
        Assert.Equal("", paid.CurrentCall);
        Assert.Equal("Helpers.TaxHelper.PayBackTaxes", paid.BackCall);
        Assert.Equal("Helpers.TaxHelper.PayBackTaxes", Autopilot.Call(step, GameApi.Live()));
        Assert.Equal("tax", step.Kind);
        Assert.Equal("back-partial 7000", step.Detail);
    }

    [Fact]
    public void Chain_unbilled_tax_keeps_a_current_bill_above_seven_days_and_a_loan_off_the_shield()
    {
        // Chain: profit − capex → tax reserve → pay back taxes → shield spend
        var accrued = TaxPolicy.UnbilledAccruedTax(cycleNetProfit: 100_000m, deductibleCapEx: 20_000m);
        var taxReserve = TaxPolicy.TotalTaxReserve(
            billedCurrentTax: 10_000m,
            billedBackTax: 0m,
            unbilledAccruedTax: accrued);
        var pay = TaxPolicy.DecidePayment(
            cash: 12_000m,
            dailyCost: 1_000m,
            billedCurrentTax: 10_000m,
            billedBackTax: 0m,
            daysUntilDue: 10);
        var shield = TaxPolicy.EffectiveSpendRoom(
            cash: 100_000m,
            dailyCost: 2_000m,
            taxReserve: taxReserve,
            isDeductibleCapEx: true,
            daysUntilTaxAssessment: 3);
        var loan = TaxPolicy.EffectiveSpendRoom(
            cash: 100_000m,
            dailyCost: 2_000m,
            taxReserve: taxReserve,
            isDeductibleCapEx: false,
            daysUntilTaxAssessment: 3);
        var amounts = TaxPolicy.Amounts(pay);
        var paid = TaxBill.Pay(amounts.current, amounts.back, GameApi.Live());
        var step = Playbook.Decide(
            open: OpenPick.None(),
            where: SitePick.None(),
            layout: LayoutPick.None(),
            prices: PricePick.None(),
            supply: SupplyPick.None(),
            trucks: TruckPick.None(),
            warehouses: WarehousePick.None(),
            managers: ManagerPick.None(),
            money: new MoneyPick("repay", 5_000m, 0, true),
            realEstate: EstatePick.None(),
            close: ClosePick.None(),
            run: RunPick.None(),
            tax: pay);

        Assert.Equal(20_000m, accrued);
        Assert.Equal(30_000m, taxReserve);
        Assert.Equal("current-partial", pay.Kind);
        Assert.Equal(5_000m, pay.Amount);
        Assert.Equal(7_000m, 12_000m - pay.Amount);
        Assert.Equal(38_100m, shield);
        Assert.Equal(14_000m, loan);
        Assert.True(shield > loan);
        Assert.Equal(5_000m, amounts.current);
        Assert.Equal(0m, amounts.back);
        Assert.True(paid.Paid);
        Assert.Equal("Helpers.TaxHelper.PayCurrentTaxes", paid.CurrentCall);
        Assert.Equal("", paid.BackCall);
        Assert.Equal("Helpers.TaxHelper.PayCurrentTaxes", Autopilot.Call(step, GameApi.Live()));
        Assert.Equal("tax", step.Kind);
        Assert.Equal("current-partial 5000", step.Detail);
        Assert.NotEqual("money", step.Kind);
    }
}
