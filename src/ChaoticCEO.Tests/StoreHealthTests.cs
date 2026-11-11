using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class StoreHealthTests
{
    private static readonly string[] GiftStock = Wholesaler.GiftShopWholesalersStock().ToArray();

    [Fact]
    public void StoreHealth_assess_locks_each_G1_to_G5_from_fixture_facts()
    {
        // Harvest: day-14 Temporarily Closed + empty panels + flat customers while Alerts proxy lied green.
        var closed = StoreHealth.Assess(Facts(temporarilyClosed: true));
        var emptyBags = StoreHealth.Assess(
            Facts(
                cargo: Cargo(
                    ("ba:itemname_cheapgift", 10),
                    ("ba:itemname_cheapflower", 10),
                    ("ba:itemname_sodacan", 10),
                    ("ba:itemname_umbrella", 10),
                    ("ba:itemname_paperbag", 0))));
        var unstaffed = StoreHealth.Assess(Facts(registerStaffed: false, cleanerStaffed: true));
        var lowSat = StoreHealth.Assess(Facts(satisfaction: 49f));
        var noSales = StoreHealth.Assess(Facts(scheduledOpen: true, customers: 0, revenue: 0m));
        var lostMoney = StoreHealth.Assess(Facts(openDayProfits: new[] { 80m, 90m, -6m }));
        var shortProof = StoreHealth.Assess(Facts(openDayProfits: new[] { 10m, -20m, 5m }));
        var ready = StoreHealth.Assess(Facts());

        Assert.False(closed.Open);
        Assert.False(closed.Ready);
        Assert.False(emptyBags.Stocked);
        Assert.False(emptyBags.Ready);
        Assert.False(unstaffed.Staffed);
        Assert.False(lowSat.Staffed);
        Assert.False(noSales.Selling);
        Assert.False(lostMoney.Profitable);
        Assert.False(shortProof.Profitable);
        Assert.True(ready.Ready);
        Assert.Equal("ba:street_seventhavenue 30", ready.Place);
        Assert.True(StoreHealth.Assess(Facts(scheduledOpen: false, customers: 0, revenue: 0m)).Selling);
        Assert.Equal(StoreHealthGate.SatisfactionFloor, 50f);
        Assert.Equal(3, StoreHealthGate.ProfitProofDays);
    }

    [Fact]
    public void StoreHealth_fixture_cargo_proof_fails_when_any_layout_item_or_paperbag_is_missing()
    {
        // Harvest: umbrella / paperbag reds while gifts still on the panel.
        var missingUmbrella = StoreHealth.FixtureCargoProof(
            GiftStock,
            Cargo(
                ("ba:itemname_cheapgift", 5),
                ("ba:itemname_cheapflower", 5),
                ("ba:itemname_sodacan", 5),
                ("ba:itemname_paperbag", 5)));
        var overnight = StoreHealth.FixtureCargoProof(
            GiftStock,
            Cargo(
                ("ba:itemname_cheapgift", 5),
                ("ba:itemname_cheapflower", 5),
                ("ba:itemname_sodacan", 5),
                ("ba:itemname_paperbag", 5),
                ("ba:itemname_umbrella", 5)));

        Assert.False(missingUmbrella);
        Assert.True(overnight);
        Assert.False(StoreHealth.FixtureCargoProof(GiftStock, null));
        Assert.False(StoreHealth.FixtureCargoProof(null, Cargo(("ba:itemname_paperbag", 1))));
    }

    [Fact]
    public void StoreHeal_decide_picks_the_first_failed_gate_before_expansion()
    {
        Assert.Equal("reopen", StoreHeal.Decide(StoreHealth.Assess(Facts(temporarilyClosed: true))).Action);
        Assert.Equal(
            "restock",
            StoreHeal.Decide(
                StoreHealth.Assess(
                    Facts(
                        cargo: Cargo(
                            ("ba:itemname_cheapgift", 1),
                            ("ba:itemname_cheapflower", 1),
                            ("ba:itemname_sodacan", 1),
                            ("ba:itemname_umbrella", 1),
                            ("ba:itemname_paperbag", 0))))).Action);
        Assert.Equal("restaff", StoreHeal.Decide(StoreHealth.Assess(Facts(registerStaffed: false))).Action);
        Assert.Equal(
            "prices",
            StoreHeal.Decide(StoreHealth.Assess(Facts(scheduledOpen: true, customers: 0, revenue: 0m))).Action);
        Assert.Equal(
            "hold",
            StoreHeal.Decide(StoreHealth.Assess(Facts(openDayProfits: new[] { 40m, 40m, -1m }))).Action);
        Assert.False(StoreHeal.Decide(StoreHealth.Assess(Facts())).Chosen);
        Assert.False(StoreHeal.Decide(null).Chosen);
    }

    [Fact]
    public void StoreHealth_completed_day_sales_keep_prior_proof_when_morning_zeros()
    {
        // Harvest: day-roll morning customers=0 revenue=0 wiped g4 while yesterday sold.
        var morning = StoreHealth.CompletedDaySales(
            todayCustomers: 0,
            todayRevenue: 0m,
            priorCustomers: 14,
            priorRevenue: 420m);
        var live = StoreHealth.CompletedDaySales(9, 300m, 14, 420m);
        var cold = StoreHealth.CompletedDaySales(0, 0m, 0, 0m);

        Assert.Equal(14, morning.Customers);
        Assert.Equal(420m, morning.Revenue);
        Assert.Equal(9, live.Customers);
        Assert.Equal(300m, live.Revenue);
        Assert.Equal(0, cold.Customers);
        Assert.Equal(0m, cold.Revenue);
    }

    [Fact]
    public void StoreHealth_order_history_and_statements_prove_g4_g5_when_dailyIncomes_empty()
    {
        // Harvest: 1.17.1 soak had customers from units but revenue=0 profits= empty (dailyIncomes unused).
        var order = StoreHealth.OrderDaySales(totalCustomers: 141, totalRevenue: 1128m);
        Assert.Equal(141, order.Customers);
        Assert.Equal(1128m, order.Revenue);
        var profits = StoreHealth.StatementProfits(
            new[]
            {
                new FinancialDay(
                    2,
                    new[]
                    {
                        new StatementRow("ba:street_seventhavenue", 15, 400f, 100f, 50f, 20f, 0f, 0f, 80f),
                    }),
                new FinancialDay(
                    3,
                    new[]
                    {
                        new StatementRow("ba:street_seventhavenue", 15, 420f, 100f, 50f, 20f, 0f, 0f, 90f),
                    }),
                new FinancialDay(
                    4,
                    new[]
                    {
                        new StatementRow("ba:street_seventhavenue", 15, 450f, 100f, 50f, 20f, 0f, 0f, 100f),
                    }),
                new FinancialDay(
                    5,
                    new[]
                    {
                        new StatementRow("ba:street_seventhavenue", 15, 0f, 0f, 0f, 0f, 0f, 0f, 0f),
                    }),
            },
            "ba:street_seventhavenue",
            15,
            currentDay: 5);
        var completed = StoreHealth.DropIncompleteTail(profits, todayUnits: 0);
        Assert.Equal(new[] { 80m, 90m, 100m }, completed);
        var sales = StoreHealth.CompletedDaySales(0, 0m, order.Customers, order.Revenue);
        var health = StoreHealth.Assess(
            Facts(customers: sales.Customers, revenue: sales.Revenue, openDayProfits: completed));
        Assert.True(health.Selling);
        Assert.True(health.Profitable);
        Assert.True(health.Ready);
    }

    [Fact]
    public void StoreHealth_drop_incomplete_tail_clears_day_roll_zero_before_g5()
    {
        // Harvest: dailyIncomes ended with a fresh 0 at day.roll while ProfitProofDays already green.
        var kept = StoreHealth.DropIncompleteTail(new[] { 80m, 90m, 100m, 0m }, todayUnits: 0);
        var realZero = StoreHealth.DropIncompleteTail(new[] { 80m, 90m, 0m, 0m }, todayUnits: 0);
        var midDay = StoreHealth.DropIncompleteTail(new[] { 80m, 90m, 100m }, todayUnits: 5);

        Assert.Equal(new[] { 80m, 90m, 100m }, kept);
        Assert.Equal(new[] { 80m, 90m, 0m }, realZero);
        Assert.Equal(new[] { 80m, 90m, 100m }, midDay);
        Assert.True(StoreHealth.Assess(Facts(customers: 14, revenue: 420m, openDayProfits: kept)).Ready);
        Assert.False(StoreHealth.Assess(Facts(openDayProfits: new[] { 80m, 90m, 100m, 0m })).Profitable);
    }

    [Fact]
    public void Stall_decide_waits_StuckDays_then_remedies_then_pauses()
    {
        Assert.Equal(3, Stall.StuckDays);
        var unprofitable = StoreHealth.Assess(Facts(openDayProfits: new[] { 40m, 40m, -1m }));
        Assert.Equal("g5", Stall.FailedGate(unprofitable));
        Assert.False(Stall.Decide(unprofitable, falseDays: 2, tried: null).Chosen);
        var first = Stall.Decide(unprofitable, falseDays: Stall.StuckDays, tried: null);
        Assert.True(first.Chosen);
        Assert.False(first.Pause);
        Assert.Equal("g5", first.Gate);
        Assert.Equal("restock", first.Action);
        var second = Stall.Decide(unprofitable, falseDays: Stall.StuckDays, tried: new[] { "restock" });
        Assert.Equal("prices", second.Action);
        var stuck = Stall.Decide(
            unprofitable,
            falseDays: Stall.StuckDays,
            tried: new[] { "restock", "prices" });
        Assert.True(stuck.Pause);
        Assert.Equal("g5", stuck.Gate);
        Assert.Equal("", stuck.Action);
        Assert.False(Stall.Decide(StoreHealth.Assess(Facts()), Stall.StuckDays, null).Chosen);
    }

    [Fact]
    public void Chain_completed_day_ready_unlocks_expansion_and_stall_pauses_when_exhausted()
    {
        // Chain: morning zeros → completed-day sales → DropIncompleteTail → Ready → ExpansionAllowed
        //      → stuck g5 remedies → autopilot.paused reason=stuck
        var sales = StoreHealth.CompletedDaySales(0, 0m, 12, 400m);
        var profits = StoreHealth.DropIncompleteTail(new[] { 80m, 90m, 100m, 0m }, todayUnits: 0);
        var health = StoreHealth.Assess(
            Facts(customers: sales.Customers, revenue: sales.Revenue, openDayProfits: profits));
        var candidates = new[]
        {
            new OpenCandidate(
                "ba:businesstype_giftshop",
                "goods",
                40m,
                wholesalerSells: true,
                "specialty",
                ownedOfFamily: 1,
                familyHasWarehouse: false),
        };

        Assert.True(health.Ready);
        Assert.True(StoreHealthGate.ExpansionAllowed(new[] { health }));
        Assert.True(Open.Decide(1, candidates, 0m, new[] { health }).Chosen);

        var losing = StoreHealth.Assess(Facts(openDayProfits: new[] { 10m, 10m, -5m }));
        Assert.False(StoreHealthGate.ExpansionAllowed(new[] { losing }));
        Assert.False(Open.Decide(1, candidates, 0m, new[] { losing }).Chosen);
        var stuck = Stall.Decide(losing, Stall.StuckDays, new[] { "restock", "prices" });
        Assert.True(stuck.Pause);
        var line = Stall.PauseLine(losing.Place, stuck.Gate);
        Assert.Contains("autopilot.paused", line);
        Assert.Contains("reason=stuck", line);
        Assert.Contains("gate=g5", line);
        Assert.Contains("place=" + losing.Place, line);
    }

    [Fact]
    public void Stall_record_pause_writes_stuck_line()
    {
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-stall-pause-" + Guid.NewGuid().ToString("N")));
        try
        {
            var written = Stall.RecordPause(dir.FullName, "ba:street_seventhavenue 30", "g5");
            Assert.Contains("reason=stuck", written);
            Assert.Contains("gate=g5", written);
            Assert.Contains(written, File.ReadAllText(LoadLog.CeoLogPath(dir.FullName)));
        }
        finally
        {
            Directory.Delete(dir.FullName, recursive: true);
        }
    }

    [Fact]
    public void Delivery_mid_week_survival_sizes_contract_for_open_days_until_next_drop()
    {
        // Harvest: amount=70 sold out in ~1 open day; nextDeliveryDay jumped 2→8→15→22→29.
        Assert.Equal(2, Wholesaler.CoverOpenDays);
        Assert.Equal(70, Wholesaler.StageOneDailyBurn);
        Assert.Equal(140, Wholesaler.ContractAmount(Wholesaler.StageOneDailyBurn));
        Assert.Equal(50, Wholesaler.ContractAmount(dailyBurn: 0));
        Assert.Equal(50, Wholesaler.ContractAmount(dailyBurn: 5));
        Assert.Equal(280, Wholesaler.ItemAmount("ba:itemname_paperbag", Wholesaler.StageOneDailyBurn));
        Assert.Equal(140, Wholesaler.ItemAmount("ba:itemname_cheapgift", Wholesaler.StageOneDailyBurn));
        Assert.False(Wholesaler.MidWeekSurvives(onHand: 70, dailyBurn: 70, openDaysUntilNext: 6));
        Assert.True(Wholesaler.MidWeekSurvives(onHand: 140, dailyBurn: 70, openDaysUntilNext: 1));
        Assert.True(Wholesaler.MidWeekSurvives(onHand: 1, dailyBurn: 0, openDaysUntilNext: 6));
    }

    [Fact]
    public void Wholesaler_reschedule_trips_weekly_nextDeliveryDay_back_to_tomorrow()
    {
        // Harvest: day-24 routes still showed nextDeliveryDay=29 while paper bags were empty.
        Assert.True(Wholesaler.NeedsReschedule(currentDay: 24, nextDeliveryDay: 29));
        Assert.True(Wholesaler.NeedsReschedule(currentDay: 3, nextDeliveryDay: 8));
        Assert.False(Wholesaler.NeedsReschedule(currentDay: 24, nextDeliveryDay: 25));
        Assert.False(Wholesaler.NeedsReschedule(currentDay: 24, nextDeliveryDay: 24));
        Assert.Equal(25, Wholesaler.RescheduleDay(24));
        Assert.Equal(4, Wholesaler.RescheduleDay(3));
    }

    [Fact]
    public void StoreHealth_zero_amount_paperbag_cargo_fails_G2_while_other_items_remain()
    {
        // Harvest: day-24 alert "Cash Register is out of Paper Bag" while prior store.health kept g2=true.
        var counts = StoreHealth.CountCargoAmounts(
            new[]
            {
                new CargoAmount("ba:itemname_cheapgift", 40),
                new CargoAmount("ba:itemname_cheapflower", 40),
                new CargoAmount("ba:itemname_sodacan", 40),
                new CargoAmount("ba:itemname_umbrella", 40),
                new CargoAmount("ba:itemname_paperbag", 0),
            });
        var health = StoreHealth.Assess(Facts(cargo: counts));

        Assert.False(StoreHealth.FixtureCargoProof(GiftStock, counts));
        Assert.False(health.Stocked);
        Assert.Equal("restock", StoreHeal.Decide(health).Action);
    }

    [Fact]
    public void StoreHealth_line_names_each_gate_for_the_evidence_sink()
    {
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-store-health-" + Guid.NewGuid().ToString("N")));
        try
        {
            var health = StoreHealth.Assess(
                Facts(
                    cargo: Cargo(
                        ("ba:itemname_cheapgift", 1),
                        ("ba:itemname_cheapflower", 1),
                        ("ba:itemname_sodacan", 1),
                        ("ba:itemname_umbrella", 1),
                        ("ba:itemname_paperbag", 0))));
            var line = StoreHealth.Line(health);
            var written = StoreHealth.Record(dir.FullName, health);

            Assert.Equal(
                "store.health version=" + LoadLog.Version
                    + " place=ba:street_seventhavenue 30 g1=true g2=false g3=true g4=true g5=true ready=false"
                    + " customers=12 revenue=400 profits=80|90|100",
                line);
            Assert.Equal(line, written);
            Assert.Contains(line, File.ReadAllText(LoadLog.CeoLogPath(dir.FullName)));
        }
        finally
        {
            Directory.Delete(dir.FullName, recursive: true);
        }
    }

    [Fact]
    public void Chain_store_health_assess_heal_blocks_open_until_ready()
    {
        // Chain: fixture cargo → G2 assess → Layer-1 heal → open blocked
        var facts = Facts(
            cargo: Cargo(
                ("ba:itemname_cheapgift", 8),
                ("ba:itemname_cheapflower", 8),
                ("ba:itemname_sodacan", 8),
                ("ba:itemname_umbrella", 0),
                ("ba:itemname_paperbag", 8)));
        var health = StoreHealth.Assess(facts);
        var heal = StoreHeal.Decide(health);
        var owned = new[] { health };
        var candidates = new[]
        {
            new OpenCandidate(
                "ba:businesstype_giftshop",
                "goods",
                40m,
                wholesalerSells: true,
                "specialty",
                ownedOfFamily: 1,
                familyHasWarehouse: false),
        };

        Assert.False(StoreHealth.FixtureCargoProof(GiftStock, facts.CargoOnHand));
        Assert.False(health.Stocked);
        Assert.False(health.Ready);
        Assert.Equal("restock", heal.Action);
        Assert.True(heal.Chosen);
        Assert.False(StoreHealthGate.ExpansionAllowed(owned));
        Assert.False(Open.Decide(1, candidates, 0m, owned).Chosen);

        var healthyFacts = Facts();
        var healthy = StoreHealth.Assess(healthyFacts);
        Assert.True(StoreHealth.FixtureCargoProof(GiftStock, healthyFacts.CargoOnHand));
        Assert.True(healthy.Ready);
        Assert.False(StoreHeal.Decide(healthy).Chosen);
        Assert.True(StoreHealthGate.ExpansionAllowed(new[] { healthy }));
        Assert.True(Open.Decide(1, candidates, 0m, new[] { healthy }).Chosen);
    }

    [Fact]
    public void Chain_delivery_amount_covers_mid_week_burn_before_open_gate()
    {
        // Chain: weekly next → reschedule tomorrow → amount from burn → G2 cargo → open allowed
        Assert.True(Wholesaler.NeedsReschedule(8, 15));
        var next = Wholesaler.RescheduleDay(8);
        Assert.Equal(9, next);
        var burn = Wholesaler.StageOneDailyBurn;
        var giftAmount = Wholesaler.ItemAmount("ba:itemname_cheapgift", burn);
        var bagAmount = Wholesaler.ItemAmount("ba:itemname_paperbag", burn);
        Assert.True(Wholesaler.MidWeekSurvives(giftAmount, burn, openDaysUntilNext: 1));
        Assert.True(Wholesaler.MidWeekSurvives(bagAmount, burn, openDaysUntilNext: 1));
        var cargo = Cargo(
            ("ba:itemname_cheapgift", giftAmount),
            ("ba:itemname_cheapflower", giftAmount),
            ("ba:itemname_sodacan", giftAmount),
            ("ba:itemname_paperbag", bagAmount),
            ("ba:itemname_umbrella", giftAmount));
        var health = StoreHealth.Assess(Facts(cargo: cargo, openDayProfits: new[] { 80m, 90m, 100m }));
        Assert.True(health.Ready);
        Assert.False(StoreHeal.Decide(health).Chosen);
        Assert.True(
            Open.Decide(
                1,
                new[]
                {
                    new OpenCandidate(
                        "ba:businesstype_giftshop",
                        "goods",
                        40m,
                        true,
                        "specialty",
                        1,
                        false),
                },
                0m,
                new[] { health }).Chosen);
    }

    private static StoreFacts Facts(
        bool temporarilyClosed = false,
        IReadOnlyDictionary<string, int>? cargo = null,
        bool registerStaffed = true,
        bool cleanerStaffed = true,
        float satisfaction = 80f,
        bool scheduledOpen = true,
        int customers = 12,
        decimal revenue = 400m,
        IReadOnlyList<decimal>? openDayProfits = null)
    {
        return new StoreFacts(
            "ba:street_seventhavenue 30",
            temporarilyClosed,
            GiftStock,
            cargo ?? FullCargo(),
            registerStaffed,
            cleanerStaffed,
            satisfaction,
            scheduledOpen,
            customers,
            revenue,
            openDayProfits ?? new[] { 80m, 90m, 100m });
    }

    private static IReadOnlyDictionary<string, int> FullCargo()
    {
        return Cargo(
            ("ba:itemname_cheapgift", 50),
            ("ba:itemname_cheapflower", 50),
            ("ba:itemname_sodacan", 50),
            ("ba:itemname_paperbag", 50),
            ("ba:itemname_umbrella", 50));
    }

    private static IReadOnlyDictionary<string, int> Cargo(params (string Item, int Count)[] rows)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            map[row.Item] = row.Count;
        }

        return map;
    }
}
