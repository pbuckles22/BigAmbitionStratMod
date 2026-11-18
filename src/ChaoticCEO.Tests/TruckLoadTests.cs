using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class TruckLoadTests
{
    [Fact]
    public void TruckLoad_fraction_is_the_cargo_on_filled_slots_over_their_capacity()
    {
        var slots = new[]
        {
            new GarageCargo(carried: 90, capacity: 100, filled: true),
            new GarageCargo(carried: 80, capacity: 100, filled: false),
            new GarageCargo(carried: 40, capacity: 0, filled: true),
            new GarageCargo(carried: -5, capacity: 50, filled: true),
        };

        var fraction = TruckLoad.Fraction(slots);
        var empty = TruckLoad.Fraction(null);
        var none = TruckLoad.Fraction(Array.Empty<GarageCargo>());

        Assert.Equal(0.60m, fraction);
        Assert.Equal(0m, empty);
        Assert.Equal(0m, none);
        Assert.Equal(
            "truck.load version=" + LoadLog.Version + " day=4 carried=90 capacity=150 fraction=0.6",
            TruckLoad.Line(4, slots));
    }

    [Fact]
    public void TruckLoad_exactly_truck_busy_does_not_open_a_service()
    {
        var slots = new[]
        {
            new GarageCargo(carried: 85, capacity: 100, filled: true),
            new GarageCargo(carried: 85, capacity: 100, filled: true),
        };
        var fraction = TruckLoad.Fraction(slots);
        var open = Open.Decide(2, Opens(), fraction);

        Assert.Equal(Open.TruckBusy, fraction);
        Assert.True(open.Chosen);
        Assert.Equal("ba:businesstype_giftshop", open.Type);
    }

    [Fact]
    public void PaybackRate_best_is_the_larger_rent_over_price_and_a_tie_keeps_the_later_building()
    {
        var best = PaybackRate.Best(
            new[]
            {
                new BuildingYield("ba:street_early", 1, price: 1_000m, dailyRent: 100m),
                new BuildingYield("ba:street_later", 2, price: 2_000m, dailyRent: 200m),
                new BuildingYield("", 3, price: 100m, dailyRent: 90m),
                new BuildingYield("ba:street_zero", 0, price: 100m, dailyRent: 90m),
                new BuildingYield("ba:street_free", 4, price: 0m, dailyRent: 90m),
                new BuildingYield("ba:street_norent", 5, price: 100m, dailyRent: 0m),
            });
        var higher = PaybackRate.Best(
            new[]
            {
                new BuildingYield("ba:street_early", 1, price: 1_000m, dailyRent: 100m),
                new BuildingYield("ba:street_short", 8, price: 500m, dailyRent: 100m),
                new BuildingYield("ba:street_bad", 9, price: -1m, dailyRent: 50m),
            });
        var none = PaybackRate.Best(null);

        Assert.True(best.Chosen);
        Assert.Equal(0.10m, best.Rate);
        Assert.Equal("ba:street_later", best.Street);
        Assert.Equal(2, best.Number);
        Assert.True(higher.Chosen);
        Assert.Equal(0.20m, higher.Rate);
        Assert.Equal("ba:street_short", higher.Street);
        Assert.Equal(8, higher.Number);
        Assert.False(none.Chosen);
        Assert.Equal(0m, none.Rate);
        Assert.Equal("", none.Street);
        Assert.Equal(
            "payback.best version=" + LoadLog.Version + " day=4 rate=0.2 street=ba:street_short number=8",
            PaybackRate.Line(4, higher));
    }

    [Fact]
    public void PaybackRate_an_interest_equal_to_the_live_rate_does_not_repay()
    {
        var best = PaybackRate.Best(
            new[] { new BuildingYield("ba:street_early", 1, price: 1_000m, dailyRent: 100m) });
        var held = Money.Decide(
            stage: 2,
            cash: 10_000m,
            dailyCost: 10m,
            projectedDailyProfit: 100m,
            offers: null,
            loan: new LoanBalance(remaining: 5_000m, dailyInterest: 20, interestRate: best.Rate),
            bestPaybackRate: best.Rate);

        Assert.Equal(0.10m, best.Rate);
        Assert.False(held.Chosen);
        Assert.Equal("", held.Action);
    }

    [Fact]
    public void BuildingSignal_dirt_wins_theft_over_the_line_wins_next_and_a_met_demand_is_quiet()
    {
        var dirty = BuildingSignalRead.Read(
            new[]
            {
                new BuildingReading("ba:street_clean", 1, dirtSpots: 0, theft: 5m, sales: 100m, demandSets: 1, fulfilled: 0),
                new BuildingReading("ba:street_dirty", 2, dirtSpots: 2, theft: 1m, sales: 100m, demandSets: 0, fulfilled: 0),
            });
        var guarded = BuildingSignalRead.Read(
            new[]
            {
                new BuildingReading("ba:street_early", 1, dirtSpots: 0, theft: 2m, sales: 100m, demandSets: 1, fulfilled: 0),
                new BuildingReading("ba:street_later", 3, dirtSpots: 0, theft: 4m, sales: 100m, demandSets: 2, fulfilled: 0),
            });
        var exact = BuildingSignalRead.Read(
            new[]
            {
                new BuildingReading("ba:street_exact", 4, dirtSpots: 0, theft: 1m, sales: 100m, demandSets: 2, fulfilled: 2),
            });
        var unmet = BuildingSignalRead.Read(
            new[]
            {
                new BuildingReading("ba:street_met", 5, dirtSpots: -1, theft: 5m, sales: 0m, demandSets: 1, fulfilled: 1),
                new BuildingReading("ba:street_short", 6, dirtSpots: 0, theft: 0m, sales: 40m, demandSets: 3, fulfilled: 2),
            });
        var none = BuildingSignalRead.Read(null);

        Assert.True(dirty.HasDirt);
        Assert.Equal("ba:street_dirty", dirty.Street);
        Assert.Equal(2, dirty.Number);
        Assert.False(dirty.Unmet);
        Assert.False(guarded.HasDirt);
        Assert.Equal(4m, guarded.Theft);
        Assert.Equal(100m, guarded.Sales);
        Assert.Equal("ba:street_later", guarded.Street);
        Assert.False(guarded.Unmet);
        Assert.False(exact.HasDirt);
        Assert.False(exact.Unmet);
        Assert.Equal(0m, exact.Theft);
        Assert.Equal(0m, exact.Sales);
        Assert.False(unmet.HasDirt);
        Assert.True(unmet.Unmet);
        Assert.Equal("ba:street_short", unmet.Street);
        Assert.Equal(0m, unmet.Theft);
        Assert.Equal(40m, unmet.Sales);
        Assert.False(none.HasDirt);
        Assert.False(none.Unmet);
        Assert.Equal(
            "building.pulse version=" + LoadLog.Version + " day=4 street=ba:street_short number=6 dirt=0 theft=0 sales=40 unmet=1",
            BuildingSignalRead.Line(4, unmet));
    }

    [Fact]
    public void Chain_garage_cargo_sets_the_truck_load_then_the_open()
    {
        // Chain: garage → truck load → open
        var slots = new[]
        {
            new GarageCargo(carried: 90, capacity: 100, filled: true),
            new GarageCargo(carried: 100, capacity: 100, filled: false),
        };
        var fraction = TruckLoad.Fraction(slots);
        var step = WinRun.Next(
            stage: 2,
            opens: Opens(),
            pendingOpen: "",
            sites: null,
            cash: 20_000m,
            reserve: 0m,
            money: MoneyPick.None(),
            run: RunPick.None(),
            truckLoadFraction: fraction);

        Assert.Equal(0.90m, fraction);
        Assert.True(fraction > Open.TruckBusy);
        Assert.True(step.Chosen);
        Assert.Equal("open", step.Kind);
        Assert.Equal("ba:businesstype_lawfirm", step.Detail);
        Assert.Equal("Next open ba:businesstype_lawfirm", Playbook.Line(step));
    }

    [Fact]
    public void Chain_a_garage_at_truck_busy_keeps_the_goods_open()
    {
        // Chain: garage → truck load → open
        var slots = new[]
        {
            new GarageCargo(carried: 90, capacity: 100, filled: true),
            new GarageCargo(carried: 80, capacity: 100, filled: true),
        };
        var fraction = TruckLoad.Fraction(slots);
        var step = WinRun.Next(
            stage: 2,
            opens: Opens(),
            pendingOpen: "",
            sites: null,
            cash: 20_000m,
            reserve: 0m,
            money: MoneyPick.None(),
            run: RunPick.None(),
            truckLoadFraction: fraction);

        Assert.Equal(Open.TruckBusy, fraction);
        Assert.Equal("open", step.Kind);
        Assert.Equal("ba:businesstype_giftshop", step.Detail);
    }

    [Fact]
    public void Chain_buildings_set_the_payback_then_the_repay()
    {
        // Chain: buildings → payback → repay
        var best = PaybackRate.Best(
            new[]
            {
                new BuildingYield("ba:street_early", 1, price: 1_000m, dailyRent: 50m),
                new BuildingYield("ba:street_short", 8, price: 500m, dailyRent: 50m),
            });
        var repay = Money.Decide(
            stage: 2,
            cash: 10_000m,
            dailyCost: 10m,
            projectedDailyProfit: 100m,
            offers: null,
            loan: new LoanBalance(remaining: 5_000m, dailyInterest: 20, interestRate: 0.12m),
            bestPaybackRate: best.Rate);
        var held = Money.Decide(
            stage: 2,
            cash: 10_000m,
            dailyCost: 10m,
            projectedDailyProfit: 100m,
            offers: null,
            loan: new LoanBalance(remaining: 5_000m, dailyInterest: 4, interestRate: 0.08m),
            bestPaybackRate: best.Rate);
        var step = Playbook.Decide(
            open: OpenPick.None(),
            where: SitePick.None(),
            layout: LayoutPick.None(),
            prices: PricePick.None(),
            supply: SupplyPick.None(),
            trucks: TruckPick.None(),
            warehouses: WarehousePick.None(),
            managers: ManagerPick.None(),
            money: repay,
            realEstate: EstatePick.None(),
            close: ClosePick.None(),
            run: RunPick.None());

        Assert.Equal(0.10m, best.Rate);
        Assert.Equal("ba:street_short", best.Street);
        Assert.True(repay.Chosen);
        Assert.Equal("repay", repay.Action);
        Assert.Equal(5_000m, repay.Amount);
        Assert.False(held.Chosen);
        Assert.Equal("money", step.Kind);
        Assert.Equal("repay 5000", step.Detail);
    }

    [Fact]
    public void Chain_building_reading_sets_the_run_facts_then_the_remedy()
    {
        // Chain: building → run facts → remedy
        var readings = new[]
        {
            new BuildingReading("ba:street_clean", 1, dirtSpots: 0, theft: 0m, sales: 80m, demandSets: 1, fulfilled: 1),
            new BuildingReading("ba:street_dirty", 2, dirtSpots: 3, theft: 0m, sales: 80m, demandSets: 1, fulfilled: 1),
        };
        var facts = BuildingSignalRead.Facts(readings);
        var remedy = Run.Decide(facts);
        var step = Playbook.Decide(
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
            run: remedy);

        Assert.True(facts.HasDirt);
        Assert.False(facts.UnmetCustomerDemand);
        Assert.True(remedy.Chosen);
        Assert.Equal("cleaner", remedy.Action);
        Assert.Equal("run", step.Kind);
        Assert.Equal("cleaner", step.Detail);
    }

    [Fact]
    public void Chain_a_quiet_building_sets_no_remedy()
    {
        // Chain: building → run facts → remedy
        var facts = BuildingSignalRead.Facts(
            new[]
            {
                new BuildingReading("ba:street_exact", 4, dirtSpots: 0, theft: 1m, sales: 100m, demandSets: 2, fulfilled: 2),
            });
        var remedy = Run.Decide(facts);
        var step = Playbook.Decide(
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
            run: remedy);

        Assert.False(facts.HasDirt);
        Assert.Equal(0m, facts.Theft);
        Assert.False(facts.UnmetCustomerDemand);
        Assert.False(remedy.Chosen);
        Assert.Equal("", remedy.Action);
        Assert.False(step.Chosen);
    }

    private static IReadOnlyList<OpenCandidate> Opens()
    {
        return new[]
        {
            new OpenCandidate(
                "ba:businesstype_giftshop",
                "goods",
                90m,
                wholesalerSells: true,
                "specialty",
                0,
                familyHasWarehouse: true),
            new OpenCandidate(
                "ba:businesstype_lawfirm",
                "office",
                40m,
                wholesalerSells: false,
                "office",
                0,
                familyHasWarehouse: false),
        };
    }
}
