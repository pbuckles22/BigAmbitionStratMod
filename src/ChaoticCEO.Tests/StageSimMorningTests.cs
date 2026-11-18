using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class StageSimMorningTests
{
    [Fact]
    public void StageSim_morning_close_matches_the_live_close_decide()
    {
        var stepped = LosingDay();
        var live = Close.Decide(
            "goods",
            Close.FixDays,
            Close.FixDays,
            Close.RedDays,
            supplied: true,
            staffed: true);
        var morning = StageSim.Morning(Ready(stepped, redProfitDays: Close.RedDays));

        Assert.True(live.Chosen);
        Assert.Equal("close", live.Action);
        Assert.Equal(live.Action, morning.Close.Action);
        Assert.Equal(live.Chosen, morning.Close.Chosen);
        Assert.Equal("close", morning.Step.Kind);
        Assert.Equal(live.Action, morning.Step.Detail);
    }

    [Fact]
    public void StageSim_morning_tax_matches_the_live_payment()
    {
        var stepped = LosingDay();
        var facts = Ready(stepped, redProfitDays: Close.RedDays - 1);
        var morning = StageSim.Morning(facts);
        var cost = DailyCost.From(new[] { stepped }, facts.Today, 0m, 0m);
        var live = TaxPolicy.DecidePayment(
            facts.Cash,
            cost,
            facts.BilledCurrentTax,
            facts.BilledBackTax,
            facts.DaysUntilDue);

        Assert.False(morning.Close.Chosen);
        Assert.Equal(live.Kind, morning.Tax.Kind);
        Assert.Equal(live.Amount, morning.Tax.Amount);
        Assert.True(morning.Tax.Chosen);
        Assert.Equal("tax", morning.Step.Kind);
        Assert.Equal("back 8000", morning.Step.Detail);
    }

    [Fact]
    public void StageSim_morning_cost_is_the_day_step_not_a_fixed_wage()
    {
        var stepped = LosingDay();
        var facts = Ready(stepped, redProfitDays: 0, backTax: 0m, currentTax: 0m);
        var morning = StageSim.Morning(facts);
        var row = stepped.Rows[0];
        var live = DailyCost.From(new[] { stepped }, facts.Today, 0m, 0m);

        Assert.Equal(40f, row.Wages);
        Assert.Equal(25f, row.Rent);
        Assert.Equal(65m, live);
        Assert.Equal(live, morning.DailyCost);
        Assert.NotEqual(10m, morning.DailyCost);
        Assert.NotEqual(30m, morning.DailyCost);
    }

    [Fact]
    public void StageSim_morning_wholesaler_supply_matches_the_live_decide()
    {
        var stepped = LosingDay();
        var facts = new StageDay(
            today: stepped.Day + 1,
            cash: 20_000m,
            days: new[] { stepped },
            kind: "warehouse",
            suppliedDays: 0,
            staffedDays: 0,
            redProfitDays: 0,
            supplied: false,
            staffed: false,
            stage: 1,
            store: "ba:street_loss 1",
            item: "ba:itemname_cheapgift",
            askSupply: true);
        var live = Supply.Decide(facts.Stage, facts.Store, facts.Item, facts.Holds, facts.Stops);
        var morning = StageSim.Morning(facts);

        Assert.Equal("wholesaler", live.Source);
        Assert.True(live.Chosen);
        Assert.Equal(live.Source, morning.Supply.Source);
        Assert.Equal(live.Chosen, morning.Supply.Chosen);
        Assert.False(morning.Supply.DropContract);
        Assert.Equal("supply", morning.Step.Kind);
        Assert.Equal("wholesaler", morning.Step.Detail);
    }

    [Fact]
    public void StageSim_morning_run_matches_the_live_run_decide()
    {
        var stepped = LosingDay();
        var facts = Ready(stepped, redProfitDays: 0, backTax: 0m, currentTax: 0m, run: Dirt());
        var live = Run.Decide(facts.Run);
        var morning = StageSim.Morning(facts);

        Assert.False(morning.Close.Chosen);
        Assert.False(morning.Tax.Chosen);
        Assert.Equal(live.Action, morning.Run.Action);
        Assert.Equal(live.Chosen, morning.Run.Chosen);
        Assert.Equal("run", morning.Step.Kind);
        Assert.Equal("cleaner", morning.Step.Detail);
    }

    [Fact]
    public void Chain_day_step_feeds_the_live_decides_and_close_beats_tax()
    {
        // Chain: day step → live decides → close → tax
        // Winner: close. A red goods store beats the back-tax payment.
        var curve = LossCurve();
        var stepped = DayStep.Day(7, new[] { curve });
        var facts = Ready(stepped, redProfitDays: Close.RedDays);
        var morning = StageSim.Morning(facts);
        var cost = DailyCost.From(new[] { stepped }, facts.Today, 0m, 0m);
        var close = Close.Decide(
            "goods",
            Close.FixDays,
            Close.FixDays,
            Close.RedDays,
            supplied: true,
            staffed: true);
        var tax = TaxPolicy.DecidePayment(
            facts.Cash,
            cost,
            facts.BilledCurrentTax,
            facts.BilledBackTax,
            facts.DaysUntilDue);

        Assert.Equal(7, stepped.Day);
        Assert.Equal(65m, cost);
        Assert.Equal(cost, morning.DailyCost);
        Assert.Equal(close.Action, morning.Close.Action);
        Assert.Equal("back", tax.Kind);
        Assert.Equal(8_000m, tax.Amount);
        Assert.Equal(tax.Kind, morning.Tax.Kind);
        Assert.Equal(tax.Amount, morning.Tax.Amount);
        Assert.Equal("close", morning.Step.Kind);
        Assert.Equal(close.Action, morning.Step.Detail);
        Assert.Equal("Next close close", Playbook.Line(morning.Step));
    }

    [Fact]
    public void Chain_day_step_pays_tax_when_the_close_decide_stays_open()
    {
        // Chain: day step → live decides → close → tax
        // Winner: tax. One short red day keeps the store, and the payment uses the day-step cost.
        var stepped = DayStep.Day(7, new[] { LossCurve() });
        var facts = Ready(stepped, redProfitDays: Close.RedDays - 1);
        var morning = StageSim.Morning(facts);
        var cost = DailyCost.From(new[] { stepped }, facts.Today, 0m, 0m);
        var close = Close.Decide(
            "goods",
            Close.FixDays,
            Close.FixDays,
            facts.RedProfitDays,
            supplied: true,
            staffed: true);
        var tax = TaxPolicy.DecidePayment(
            facts.Cash,
            cost,
            facts.BilledCurrentTax,
            facts.BilledBackTax,
            facts.DaysUntilDue);

        Assert.False(close.Chosen);
        Assert.Equal(close.Chosen, morning.Close.Chosen);
        Assert.Equal(cost, morning.DailyCost);
        Assert.Equal(tax.Kind, morning.Tax.Kind);
        Assert.Equal(tax.Amount, morning.Tax.Amount);
        Assert.Equal("tax", morning.Step.Kind);
        Assert.Equal("back 8000", morning.Step.Detail);
        Assert.Equal("Next tax back 8000", Playbook.Line(morning.Step));
    }

    [Fact]
    public void StageSim_trial_wholesaler_marks_the_store_supplied()
    {
        var trial = StageSim.Trial(
            startCash: 30_000m,
            startNetWorth: 30_000m,
            RentCatalog(80m, storeProfit: 1_000m),
            seed: 1,
            horizonDays: 12);

        Assert.True(trial.GoodsStores >= 1);
        Assert.Equal(trial.GoodsStores, trial.SuppliedStores);
    }

    [Fact]
    public void StageSim_trial_cost_reads_the_site_rent()
    {
        var trial = StageSim.Trial(
            startCash: 30_000m,
            startNetWorth: 30_000m,
            RentCatalog(80m, storeProfit: 1_000m),
            seed: 1,
            horizonDays: 8);

        Assert.True(trial.GoodsStores >= 1);
        Assert.Equal(80m * trial.GoodsStores, trial.DailyCost);
        Assert.NotEqual(10m * trial.GoodsStores, trial.DailyCost);
    }

    [Fact]
    public void StageSim_trial_closes_a_red_goods_store_and_keeps_a_green_one()
    {
        var live = Close.Decide(
            "goods",
            Close.FixDays,
            Close.FixDays,
            Close.RedDays,
            supplied: true,
            staffed: true);
        var red = StageSim.Trial(
            startCash: 80_000m,
            startNetWorth: 80_000m,
            RentCatalog(80m, storeProfit: 50m),
            seed: 2,
            horizonDays: 40);
        var green = StageSim.Trial(
            startCash: 80_000m,
            startNetWorth: 80_000m,
            RentCatalog(10m, storeProfit: 1_000m),
            seed: 2,
            horizonDays: 40);

        Assert.Equal("close", live.Action);
        Assert.True(red.Closed >= 1);
        Assert.Equal(live.Action, red.LastClose);
        Assert.Equal(0, green.Closed);
        Assert.Equal("", green.LastClose);
    }

    private static FinancialDay LosingDay()
    {
        return DayStep.Day(7, new[] { LossCurve() });
    }

    private static StoreCurve LossCurve()
    {
        return new StoreCurve(
            "ba:street_loss",
            1,
            dailySales: 0m,
            goodsPerSale: 0m,
            dailyWages: 40m,
            dailyRent: 25m,
            dailyMarketing: 0m,
            dailyTheft: 0m,
            sampleDays: 1);
    }

    private static StageDay Ready(
        FinancialDay stepped,
        int redProfitDays,
        decimal backTax = 8_000m,
        decimal currentTax = 4_000m,
        RunFacts? run = null)
    {
        return new StageDay(
            today: stepped.Day + 1,
            cash: 20_000m,
            days: new[] { stepped },
            kind: "goods",
            suppliedDays: Close.FixDays,
            staffedDays: Close.FixDays,
            redProfitDays: redProfitDays,
            supplied: true,
            staffed: true,
            billedCurrentTax: currentTax,
            billedBackTax: backTax,
            daysUntilDue: 1,
            run: run,
            stage: 2,
            store: "ba:street_loss 1",
            item: "ba:itemname_cheapgift",
            askSupply: false);
    }

    private static RunFacts Dirt()
    {
        return new RunFacts(
            energy: 100f,
            hunger: 100f,
            happiness: 100f,
            unmetDemandDays: 0,
            employeeState: "",
            hasDirt: true,
            theft: 0m,
            sales: 0m,
            unmetCustomerDemand: false,
            marketingCost: 0m,
            marketingGain: 0m,
            taxDue: 0m,
            takeoverOffer: 0m,
            takeoverDailyProfit: 0m);
    }

    private static StageSimCatalog RentCatalog(decimal rent, decimal storeProfit)
    {
        var opens = new List<OpenCandidate>();
        var sites = new List<SiteCandidate>();
        for (var i = 1; i <= 4; i++)
        {
            opens.Add(new OpenCandidate(
                "ba:businesstype_giftshop",
                "goods",
                storeProfit,
                wholesalerSells: true,
                "specialty",
                ownedOfFamily: 0,
                familyHasWarehouse: false));
            sites.Add(new SiteCandidate(
                "ba:street_rent" + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                i,
                familyOnStreet: 0,
                traffic: 20m,
                dailyRent: rent,
                cost: 5_000m));
        }

        return new StageSimCatalog(opens, sites, loans: new[] { new LoanOffer(100_000m, 50) });
    }
}
