using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class DeliveryCoverTests
{
    private const string Umbrella = "ba:itemname_umbrella";
    private const string PaperBag = "ba:itemname_paperbag";

    [Fact]
    public void Cover_first_drop_is_twice_the_opening_guess()
    {
        var gift = Wholesaler.Cover(Umbrella, currentDay: 1, nextDeliveryDay: 2, days: null);
        var bags = Wholesaler.Cover(PaperBag, currentDay: 1, nextDeliveryDay: 2, days: null);

        Assert.Equal(25, Wholesaler.OpeningDailyGuess);
        Assert.Equal(50, Wholesaler.PaperBagSeed);
        Assert.Equal(50, gift.Target);
        Assert.Equal(50, gift.Need);
        Assert.Equal(300, bags.Target);
        Assert.Equal(300, bags.Need);
        Assert.True(gift.Guess);
        Assert.False(gift.Replace);
        Assert.Equal(2, gift.Window);
    }

    [Fact]
    public void Cover_sizes_the_gap_at_one_and_a_half_times_the_last_two_open_days()
    {
        var days = new[]
        {
            new CoverSale(3, 16, open: true),
            new CoverSale(4, 20, open: true),
        };

        var plan = Wholesaler.Cover(Umbrella, currentDay: 5, nextDeliveryDay: 7, days, onHand: 20, fixtureCap: 150);

        Assert.Equal(18m, plan.Average);
        Assert.Equal(2, plan.Gap);
        Assert.Equal(2, plan.Window);
        Assert.Equal(36, plan.Target);
        Assert.Equal(20, plan.OnHand);
        Assert.Equal(16, plan.Need);
        Assert.True(plan.Replace);
        Assert.Equal(6, plan.NextDay);
        Assert.False(plan.Guess);
    }

    [Fact]
    public void Cover_keeps_a_one_day_and_a_three_day_window_on_the_same_average()
    {
        var days = new[]
        {
            new CoverSale(2, 10, open: true),
            new CoverSale(3, 10, open: true),
        };

        var one = Wholesaler.Cover(Umbrella, currentDay: 4, nextDeliveryDay: 5, days, onHand: 10);
        var thin = Wholesaler.Cover(Umbrella, currentDay: 4, nextDeliveryDay: 7, days, onHand: 10);
        var stocked = Wholesaler.Cover(Umbrella, currentDay: 4, nextDeliveryDay: 7, days, onHand: 50);

        Assert.Equal(10, one.Need);
        Assert.Equal(2, one.Window);
        Assert.False(one.Replace);
        Assert.Equal(5, one.NextDay);
        Assert.Equal(10, thin.Need);
        Assert.Equal(2, thin.Window);
        Assert.True(thin.Replace);
        Assert.Equal(5, thin.NextDay);
        Assert.Equal(3, stocked.Window);
        Assert.False(stocked.Replace);
        Assert.Equal(7, stocked.NextDay);
        Assert.Equal(10, stocked.Need);
    }

    [Fact]
    public void Cover_uses_the_only_open_day_when_a_second_day_does_not_exist_yet()
    {
        var plan = Wholesaler.Cover(
            Umbrella,
            currentDay: 3,
            nextDeliveryDay: 5,
            new[] { new CoverSale(2, 16, open: true) });

        Assert.Equal(16m, plan.Average);
        Assert.Equal(32, plan.Need);
        Assert.Equal(1, plan.Samples);
    }

    [Fact]
    public void Cover_skips_a_closed_day_that_sits_between_two_open_days()
    {
        var plan = Wholesaler.Cover(
            Umbrella,
            currentDay: 6,
            nextDeliveryDay: 8,
            new[]
            {
                new CoverSale(3, 10, open: true),
                new CoverSale(4, 0, open: false),
                new CoverSale(5, 30, open: true),
            });

        Assert.Equal(20m, plan.Average);
        Assert.Equal(40, plan.Need);
        Assert.Equal(2, plan.Samples);
    }

    [Fact]
    public void Cover_rounds_a_half_unit_up()
    {
        var plan = Wholesaler.Cover(
            Umbrella,
            currentDay: 5,
            nextDeliveryDay: 7,
            new[]
            {
                new CoverSale(3, 15, open: true),
                new CoverSale(4, 16, open: true),
            });

        Assert.Equal(16m, plan.Average);
        Assert.Equal(32, plan.Target);
        Assert.Equal(32, plan.Need);
    }

    [Fact]
    public void Cover_skips_a_closed_zero_day_and_replaces_a_gap_longer_than_three()
    {
        // Harvest: 1.16.7.7 day 7 cut every line to 10 after a zero-customer day and pushed the truck out.
        var days = new[]
        {
            new CoverSale(3, 40, open: true),
            new CoverSale(4, 40, open: true),
            new CoverSale(5, 0, open: false),
        };

        var plan = Wholesaler.Cover(Umbrella, currentDay: 6, nextDeliveryDay: 12, days);

        Assert.Equal(40m, plan.Average);
        Assert.Equal(6, plan.Gap);
        Assert.Equal(2, plan.Window);
        Assert.Equal(80, plan.Target);
        Assert.Equal(80, plan.Need);
        Assert.True(plan.Replace);
        Assert.Equal(7, plan.NextDay);
    }

    [Fact]
    public void Cover_a_truck_due_today_stays_today()
    {
        // Harvest: 1.17.1 days 2–7 pushed a due truck to the next morning, so 15 7th Avenue never received stock.
        var plan = Wholesaler.Cover(
            Umbrella,
            currentDay: 6,
            nextDeliveryDay: 6,
            new[]
            {
                new CoverSale(4, 20, open: true),
                new CoverSale(5, 20, open: true),
            });

        Assert.Equal(0, plan.Gap);
        Assert.Equal(2, plan.Window);
        Assert.False(plan.Replace);
        Assert.Equal(6, plan.NextDay);
        Assert.Equal(40, plan.Target);
        Assert.Equal(40, plan.Need);
    }

    [Fact]
    public void Cover_pulls_a_missed_truck_to_today()
    {
        var plan = Wholesaler.Cover(
            Umbrella,
            currentDay: 6,
            nextDeliveryDay: 4,
            new[]
            {
                new CoverSale(4, 20, open: true),
                new CoverSale(5, 20, open: true),
            });

        Assert.Equal(2, plan.Window);
        Assert.True(plan.Replace);
        Assert.Equal(6, plan.NextDay);
        Assert.Equal(40, plan.Need);
    }

    [Fact]
    public void Chain_a_due_truck_stays_today_and_playbook_still_supplies_the_wholesaler()
    {
        // Chain: open days → due today → line stays on today → wholesaler supply → playbook
        var plan = Wholesaler.Cover(
            Umbrella,
            currentDay: 2,
            nextDeliveryDay: 2,
            new[] { new CoverSale(1, 16, open: true) });
        var supply = Supply.Decide(1, "ba:street_seventhavenue 15", Umbrella, null, null);
        var step = Playbook.Decide(null, null, null, null, supply, null, null, null, null, null, null, null);

        Assert.False(plan.Replace);
        Assert.Equal(2, plan.NextDay);
        Assert.Equal(32, plan.Need);
        Assert.Equal("wholesaler", step.Detail);
    }

    [Fact]
    public void Cover_a_thin_shelf_pulls_the_truck_before_the_count_hits_zero()
    {
        // Harvest: day 6 opened with 4 gifts and the truck stayed on day 8, so day 7 was empty.
        var plan = Wholesaler.Cover(
            Umbrella,
            currentDay: 6,
            nextDeliveryDay: 8,
            new[]
            {
                new CoverSale(4, 53, open: true),
                new CoverSale(5, 57, open: true),
            },
            onHand: 4);

        Assert.Equal(55m, plan.Average);
        Assert.Equal(2, plan.Gap);
        Assert.Equal(2, plan.Window);
        Assert.True(plan.Replace);
        Assert.Equal(7, plan.NextDay);
        Assert.Equal(110, plan.Target);
        Assert.Equal(106, plan.Need);
    }

    [Fact]
    public void Cover_a_stockout_remainder_does_not_cut_the_burn()
    {
        // Harvest: day 4 gifts sold 36 then 2 and the order was cut to 29 while the shelf was empty.
        var plan = Wholesaler.Cover(
            Umbrella,
            currentDay: 4,
            nextDeliveryDay: 4,
            new[]
            {
                new CoverSale(2, 36, open: true),
                new CoverSale(3, 2, open: true),
            });

        Assert.Equal(36m, plan.Average);
        Assert.Equal(2, plan.Window);
        Assert.False(plan.Replace);
        Assert.Equal(4, plan.NextDay);
        Assert.Equal(72, plan.Target);
        Assert.Equal(72, plan.Need);
    }

    [Fact]
    public void Cover_a_zero_sale_open_day_is_not_averaged_in()
    {
        var plan = Wholesaler.Cover(
            Umbrella,
            currentDay: 6,
            nextDeliveryDay: 6,
            new[]
            {
                new CoverSale(4, 29, open: true),
                new CoverSale(5, 0, open: true),
            },
            onHand: 0);

        Assert.Equal(29m, plan.Average);
        Assert.Equal(58, plan.Need);
    }

    [Fact]
    public void Chain_an_empty_shelf_pulls_a_later_truck_to_tomorrow()
    {
        // Chain: open days → stockout dropped → empty shelf → truck tomorrow → wholesaler supply → playbook
        var plan = Wholesaler.Cover(
            Umbrella,
            currentDay: 5,
            nextDeliveryDay: 8,
            new[]
            {
                new CoverSale(3, 2, open: true),
                new CoverSale(4, 29, open: true),
            });
        var supply = Supply.Decide(1, "ba:street_seventhavenue 15", Umbrella, null, null);
        var step = Playbook.Decide(null, null, null, null, supply, null, null, null, null, null, null, null);

        Assert.Equal(29m, plan.Average);
        Assert.Equal(3, plan.Gap);
        Assert.Equal(2, plan.Window);
        Assert.True(plan.Replace);
        Assert.Equal(6, plan.NextDay);
        Assert.Equal(58, plan.Need);
        Assert.Equal("wholesaler", step.Detail);
    }

    [Fact]
    public void Cover_line_records_the_two_sales_the_average_the_gap_and_the_need()
    {
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-cover-" + Guid.NewGuid().ToString("N")));
        try
        {
            var plan = Wholesaler.Cover(
                Umbrella,
                currentDay: 5,
                nextDeliveryDay: 7,
                new[]
                {
                    new CoverSale(3, 16, open: true),
                    new CoverSale(4, 20, open: true),
                });
            var line = Wholesaler.CoverLine(plan, day: 5, street: "ba:street_thirdstreet", number: 35);
            var written = Wholesaler.RecordCover(dir.FullName, plan, day: 5, street: "ba:street_thirdstreet", number: 35);

            Assert.Equal(
                "contract.cover version=" + LoadLog.Version
                    + " day=5 street=ba:street_thirdstreet number=35 item=" + Umbrella
                    + " sold=16|20 average=18 gap=2 window=2 multiplier=2.0 onHand=0 target=36 need=36 cap=0 replace=true next=6 guess=false",
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
    public void Chain_open_day_sales_set_the_cover_need_that_stocks_the_shelf()
    {
        // Chain: open days → burn → target → line → wholesaler supply → playbook
        var days = new[]
        {
            new CoverSale(3, 16, open: true),
            new CoverSale(4, 20, open: true),
        };
        var plan = Wholesaler.Cover(Umbrella, currentDay: 5, nextDeliveryDay: 7, days, onHand: 20, fixtureCap: 150);
        var line = Wholesaler.CoverLine(plan, 5, "ba:street_thirdstreet", 35);
        var supply = Supply.Decide(1, "ba:street_thirdstreet 35", Umbrella, null, null);
        var step = Playbook.Decide(null, null, null, null, supply, null, null, null, null, null, null, null);
        var cargo = new Dictionary<string, int>
        {
            ["ba:itemname_cheapgift"] = plan.Need,
            ["ba:itemname_cheapflower"] = plan.Need,
            ["ba:itemname_sodacan"] = plan.Need,
            ["ba:itemname_paperbag"] = plan.Need,
            ["ba:itemname_umbrella"] = plan.Need,
        };
        var health = StoreHealth.Assess(Shelf(cargo));

        Assert.Equal(36, plan.Target);
        Assert.Equal(16, plan.Need);
        Assert.Contains("onHand=20", line);
        Assert.Contains("target=36", line);
        Assert.Contains("need=16", line);
        Assert.Equal("supply", step.Kind);
        Assert.Equal("wholesaler", step.Detail);
        Assert.True(health.Stocked);
    }

    [Fact]
    public void Chain_a_closed_day_and_a_long_gap_replace_the_delivery_at_two_days()
    {
        // Chain: closed day skipped → two-day window → line minus on-hand → next day → playbook
        var days = new[]
        {
            new CoverSale(3, 40, open: true),
            new CoverSale(4, 40, open: true),
            new CoverSale(5, 0, open: false),
        };
        var plan = Wholesaler.Cover(PaperBag, currentDay: 6, nextDeliveryDay: 12, days, onHand: 31, fixtureCap: 250);
        var line = Wholesaler.CoverLine(plan, 6, "ba:street_thirdstreet", 35);
        var supply = Supply.Decide(1, "ba:street_thirdstreet 35", PaperBag, null, null);
        var step = Playbook.Decide(null, null, null, null, supply, null, null, null, null, null, null, null);

        Assert.Equal(280, plan.Target);
        Assert.Equal(249, plan.Need);
        Assert.Equal(7, plan.NextDay);
        Assert.Contains("sold=40|40", line);
        Assert.Contains("gap=6", line);
        Assert.Contains("window=2", line);
        Assert.Contains("onHand=31", line);
        Assert.Contains("need=249", line);
        Assert.Contains("replace=true", line);
        Assert.Contains("next=7", line);
        Assert.DoesNotContain("need=10", line);
        Assert.Equal("wholesaler", step.Detail);
    }

    [Fact]
    public void Cover_floors_an_open_day_that_sold_none_of_the_item()
    {
        var plan = Wholesaler.Cover(
            Umbrella,
            currentDay: 5,
            nextDeliveryDay: 7,
            new[]
            {
                new CoverSale(3, 0, open: true),
                new CoverSale(4, 0, open: true),
            });

        Assert.Equal(5m, plan.Average);
        Assert.Equal(10, plan.Target);
        Assert.Equal(10, plan.Need);
    }

    [Fact]
    public void Cover_sizes_a_paper_bag_from_store_orders_when_the_bag_itself_did_not_sell()
    {
        var plan = Wholesaler.Cover(
            PaperBag,
            currentDay: 5,
            nextDeliveryDay: 7,
            new[]
            {
                new CoverSale(3, 0, open: true, storeOrders: 20),
                new CoverSale(4, 0, open: true, storeOrders: 30),
            });

        Assert.Equal(25m, plan.Average);
        Assert.Equal(250, plan.Target);
        Assert.Equal(250, plan.Need);
    }

    [Fact]
    public void Cover_keeps_the_full_line_when_the_shelf_is_smaller()
    {
        var plan = Wholesaler.Cover(
            Umbrella,
            currentDay: 5,
            nextDeliveryDay: 7,
            new[]
            {
                new CoverSale(3, 16, open: true),
                new CoverSale(4, 20, open: true),
            },
            onHand: 10,
            fixtureCap: 30);

        Assert.Equal(36, plan.Target);
        Assert.Equal(26, plan.Need);
    }

    [Fact]
    public void Cover_keeps_the_minimum_line_when_on_hand_exceeds_the_target()
    {
        var plan = Wholesaler.Cover(
            Umbrella,
            currentDay: 5,
            nextDeliveryDay: 7,
            new[]
            {
                new CoverSale(3, 16, open: true),
                new CoverSale(4, 20, open: true),
            },
            onHand: 54,
            fixtureCap: 150);

        Assert.Equal(36, plan.Target);
        Assert.Equal(10, plan.Need);
    }

    [Fact]
    public void Cover_a_closed_day_does_not_push_a_truck_already_due_within_three_days()
    {
        var plan = Wholesaler.Cover(
            Umbrella,
            currentDay: 6,
            nextDeliveryDay: 7,
            new[]
            {
                new CoverSale(4, 40, open: true),
                new CoverSale(5, 0, open: false),
            },
            onHand: 31);

        Assert.Equal(40m, plan.Average);
        Assert.Equal(2, plan.Window);
        Assert.False(plan.Replace);
        Assert.Equal(7, plan.NextDay);
        Assert.Equal(80, plan.Target);
        Assert.Equal(49, plan.Need);
    }

    [Fact]
    public void Cover_keeps_paper_bag_sales_when_the_bag_itself_sold()
    {
        var plan = Wholesaler.Cover(
            PaperBag,
            currentDay: 5,
            nextDeliveryDay: 7,
            new[]
            {
                new CoverSale(3, 10, open: true, storeOrders: 80),
                new CoverSale(4, 10, open: true, storeOrders: 80),
            });

        Assert.Equal(10m, plan.Average);
        Assert.Equal(220, plan.Target);
        Assert.Equal(220, plan.Need);
    }

    [Fact]
    public void Cover_stand_floor_is_one_hundred_per_stand()
    {
        Assert.Equal(100, Wholesaler.StandCap);
        Assert.Equal(2.0m, Wholesaler.SalesWindowMultiplier);
        Assert.Equal(1, Wholesaler.RetailStands(PaperBag));
        Assert.Equal(2, Wholesaler.RetailStands(Umbrella));
        Assert.Equal(0, Wholesaler.StandFloor(0));
        Assert.Equal(0, Wholesaler.StandFloor(1));
        Assert.Equal(200, Wholesaler.StandFloor(2));
        Assert.Equal(300, Wholesaler.StandFloor(3));
    }

    [Fact]
    public void Cover_one_stand_keeps_the_sales_target()
    {
        var days = new[]
        {
            new CoverSale(3, 16, open: true),
            new CoverSale(4, 20, open: true),
        };

        var plan = Wholesaler.Cover(
            Umbrella,
            currentDay: 5,
            nextDeliveryDay: 7,
            days,
            onHand: 0,
            stands: Wholesaler.RetailStands(PaperBag));

        Assert.Equal(36, plan.Target);
        Assert.Equal(36, plan.Need);
    }

    [Fact]
    public void Cover_two_stands_add_twice_the_daily_sales_on_top_of_capacity()
    {
        var days = new[]
        {
            new CoverSale(22, 25, open: true),
            new CoverSale(23, 25, open: true),
        };

        var plan = Wholesaler.Cover(
            Umbrella,
            currentDay: 24,
            nextDeliveryDay: 24,
            days,
            onHand: 40,
            stands: 2);

        Assert.Equal(25m, plan.Average);
        Assert.Equal(2, plan.Window);
        Assert.False(plan.Replace);
        Assert.Equal(24, plan.NextDay);
        Assert.Equal(250, plan.Target);
        Assert.Equal(210, plan.Need);

        var later = Wholesaler.Cover(
            Umbrella,
            currentDay: 26,
            nextDeliveryDay: 26,
            days,
            onHand: 260,
            stands: 2);

        Assert.Equal(250, later.Target);
        Assert.Equal(10, later.Need);
    }

    [Fact]
    public void Chain_two_stands_add_the_floor_on_top_of_the_burn_and_playbook_still_supplies()
    {
        // Chain: open days → burn target → stand floor → line → wholesaler supply → playbook
        var days = new[]
        {
            new CoverSale(22, 24, open: true),
            new CoverSale(23, 24, open: true),
        };
        var stands = Wholesaler.RetailStands(Umbrella);
        var plan = Wholesaler.Cover(
            Umbrella,
            currentDay: 24,
            nextDeliveryDay: 24,
            days,
            onHand: 40,
            stands: stands);
        var supply = Supply.Decide(1, "ba:street_thirdstreet 35", Umbrella, null, null);
        var step = Playbook.Decide(null, null, null, null, supply, null, null, null, null, null, null, null);

        Assert.Equal(2, stands);
        Assert.Equal(24m, plan.Average);
        Assert.Equal(248, plan.Target);
        Assert.Equal(208, plan.Need);
        Assert.Equal("wholesaler", step.Detail);
    }

    [Fact]
    public void Cover_a_retail_guess_is_the_stand_floor_plus_twice_the_seed()
    {
        // Harvest: 1.17.1 day 2 wrote target=525 for a 25 guess. The floor stays 200.
        var plan = Wholesaler.Cover(
            Umbrella,
            currentDay: 2,
            nextDeliveryDay: 2,
            days: null,
            onHand: 0,
            stands: Wholesaler.RetailStands(Umbrella));

        Assert.Equal(2, Wholesaler.RetailStands(Umbrella));
        Assert.True(plan.Guess);
        Assert.Equal(25m, plan.Average);
        Assert.Equal(250, plan.Target);
        Assert.Equal(250, plan.Need);
    }

    [Fact]
    public void Chain_a_retail_guess_adds_the_stand_floor_and_playbook_still_supplies()
    {
        // Chain: no open days → seed → stand floor → line → playbook
        var stands = Wholesaler.RetailStands(Umbrella);
        var plan = Wholesaler.Cover(
            Umbrella,
            currentDay: 2,
            nextDeliveryDay: 2,
            days: null,
            onHand: 0,
            stands: stands);
        var supply = Supply.Decide(1, "ba:street_seventhavenue 15", Umbrella, null, null);
        var step = Playbook.Decide(null, null, null, null, supply, null, null, null, null, null, null, null);

        Assert.Equal(2, stands);
        Assert.Equal(250, plan.Target);
        Assert.Equal(250, plan.Need);
        Assert.Equal("supply", step.Kind);
        Assert.Equal("wholesaler", step.Detail);
    }

    [Fact]
    public void Chain_a_first_drop_bag_seed_is_the_wholesaler_line_playbook_supplies()
    {
        // Chain: no open days → bag seed → target → line → wholesaler supply → playbook
        var plan = Wholesaler.Cover(
            PaperBag,
            currentDay: 1,
            nextDeliveryDay: 2,
            days: null,
            stands: Wholesaler.RetailStands(PaperBag));
        var supply = Supply.Decide(1, "ba:street_thirdstreet 35", PaperBag, null, null);
        var step = Playbook.Decide(null, null, null, null, supply, null, null, null, null, null, null, null);

        Assert.Equal(50m, plan.Average);
        Assert.Equal(300, plan.Target);
        Assert.Equal(300, plan.Need);
        Assert.True(plan.Guess);
        Assert.Equal("supply", step.Kind);
        Assert.Equal("wholesaler", step.Detail);
    }

    [Fact]
    public void Cover_a_cash_register_holds_the_bag_floor_plus_twice_the_sales()
    {
        // Harvest: bags opened at 0. One register holds 200, then 2× the daily sales.
        var plan = Wholesaler.Cover(
            PaperBag,
            currentDay: 26,
            nextDeliveryDay: 26,
            new[]
            {
                new CoverSale(24, 76, open: true),
                new CoverSale(25, 73, open: true),
            },
            onHand: 0,
            stands: Wholesaler.RetailStands(PaperBag));

        Assert.Equal(200, Wholesaler.RegisterCap);
        Assert.Equal(0, Wholesaler.RegisterFloor(0));
        Assert.Equal(200, Wholesaler.RegisterFloor(1));
        Assert.Equal(1, Wholesaler.RetailStands(PaperBag));
        Assert.Equal(75m, plan.Average);
        Assert.Equal(350, plan.Target);
        Assert.Equal(350, plan.Need);

        var stocked = Wholesaler.Cover(
            PaperBag,
            currentDay: 27,
            nextDeliveryDay: 27,
            new[]
            {
                new CoverSale(24, 76, open: true),
                new CoverSale(25, 73, open: true),
            },
            onHand: 360,
            stands: Wholesaler.RetailStands(PaperBag));

        Assert.Equal(350, stocked.Target);
        Assert.Equal(10, stocked.Need);
    }

    [Fact]
    public void Chain_bag_sales_add_the_register_floor_and_playbook_still_supplies()
    {
        // Chain: open days → burn → register floor → line → playbook
        var stands = Wholesaler.RetailStands(PaperBag);
        var plan = Wholesaler.Cover(
            PaperBag,
            currentDay: 26,
            nextDeliveryDay: 26,
            new[]
            {
                new CoverSale(24, 76, open: true),
                new CoverSale(25, 73, open: true),
            },
            onHand: 0,
            stands: stands);
        var supply = Supply.Decide(1, "ba:street_seventhavenue 20", PaperBag, null, null);
        var step = Playbook.Decide(null, null, null, null, supply, null, null, null, null, null, null, null);

        Assert.Equal(1, stands);
        Assert.Equal(75m, plan.Average);
        Assert.Equal(350, plan.Target);
        Assert.Equal(350, plan.Need);
        Assert.Equal("supply", step.Kind);
        Assert.Equal("wholesaler", step.Detail);
    }

    private static StoreFacts Shelf(IReadOnlyDictionary<string, int> cargo)
    {
        return new StoreFacts(
            "ba:street_thirdstreet 35",
            temporarilyClosed: false,
            Wholesaler.GiftShopWholesalersStock(),
            cargo,
            registerStaffed: true,
            cleanerStaffed: true,
            staffSatisfaction: 80f,
            scheduledOpen: true,
            dailyCustomers: 40,
            dailyRevenue: 800m,
            recentOpenDayProfits: new[] { 80m, 90m, 100m });
    }
}
