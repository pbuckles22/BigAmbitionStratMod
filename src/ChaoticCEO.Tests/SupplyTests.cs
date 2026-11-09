using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class SupplyTests
{
    private const string Hair = "ba:itemname_haircareproduct";
    private const string Soda = "ba:itemname_sodacan";
    private const string Burger = "ba:itemname_burger";
    private const string Gift = "ba:itemname_cheapgift";
    private const string ExpensiveGift = "ba:itemname_expensivegift";
    private const string CheapFlower = "ba:itemname_cheapflower";
    private const string ExpensiveFlower = "ba:itemname_expensiveflower";
    private const string Fifth = "ba:street_fifthavenue 36";
    private const string Salon = "ba:street_fourthavenue 28";
    private const string Side = "ba:street_side 9";
    private const string First = "ba:street_firstavenue 38";
    private const string Wholesaler = "ba:street_firststreet 37";

    [Fact]
    public void Supply_a_mixed_warehouse_is_the_hub_for_its_majority_family_and_not_for_the_smaller_one()
    {
        var holds = new[]
        {
            Hold("W1", "ba:street_fourthstreet", 46, Soda, 10),
            Hold("W1", "ba:street_fourthstreet", 46, "ba:itemname_smartphone1", 3),
        };

        var food = Supply.List(
            Array.Empty<DeliveryStop>(),
            holds,
            new[] { new GoodsNeed(Side, Soda) },
            Array.Empty<DailyUnits>());
        var durables = Supply.List(
            Array.Empty<DeliveryStop>(),
            holds,
            new[] { new GoodsNeed(Side, "ba:itemname_smartphone1") },
            Array.Empty<DailyUnits>());

        var foodGap = Assert.Single(food.Unsupplied);
        Assert.Equal("noplan", foodGap.Reason);
        Assert.Equal("W1", foodGap.Warehouse);
        var durablesGap = Assert.Single(durables.Unsupplied);
        Assert.Equal("noplan", durablesGap.Reason);
        Assert.Equal("", durablesGap.Warehouse);
    }

    [Fact]
    public void Supply_no_plan_names_the_warehouse()
    {
        var list = Supply.List(
            Array.Empty<DeliveryStop>(),
            new[] { Hold("W4", "ba:street_sixthavenue", 34, Hair, 40) },
            new[] { new GoodsNeed(Salon, Hair) },
            Array.Empty<DailyUnits>());

        var gap = Assert.Single(list.Unsupplied);
        Assert.Equal(Salon, gap.Store);
        Assert.Equal("W4", gap.Warehouse);
        Assert.Equal(Hair, gap.Item);
        Assert.Equal("noplan", gap.Reason);
        Assert.DoesNotContain(Salon, list.Wholesaler);
    }

    [Fact]
    public void Supply_stock_on_hand_without_a_plan_is_unsupplied()
    {
        var list = Supply.List(
            Array.Empty<DeliveryStop>(),
            new[] { Hold("W4", "ba:street_sixthavenue", 34, Hair, 500) },
            new[] { new GoodsNeed(Salon, Hair) },
            new[] { new DailyUnits(Salon, Hair, 1014, 4) });

        var gap = Assert.Single(list.Unsupplied);
        Assert.Equal("noplan", gap.Reason);
        Assert.Equal("W4", gap.Warehouse);
    }

    [Fact]
    public void Supply_a_short_buffer_names_the_warehouse()
    {
        var list = Supply.List(
            new[] { Plan("ba:street_fourthstreet 46", Side, Soda) },
            new[] { Hold("W1", "ba:street_fourthstreet", 46, Soda, 10) },
            new[] { new GoodsNeed(Side, Soda) },
            new[] { new DailyUnits(Side, Soda, 1014, 4) });

        var gap = Assert.Single(list.Unsupplied);
        Assert.Equal(Side, gap.Store);
        Assert.Equal("W1", gap.Warehouse);
        Assert.Equal(Soda, gap.Item);
        Assert.Equal("short", gap.Reason);
        Assert.Empty(list.Wholesaler);
    }

    [Fact]
    public void Supply_stockout_days_do_not_shrink_the_burn()
    {
        var list = Supply.List(
            new[] { Plan("W1", Side, Soda) },
            new[] { Hold("W1", "ba:street_fourthstreet", 46, Soda, 10) },
            new[] { new GoodsNeed(Side, Soda) },
            new[]
            {
                new DailyUnits(Side, Soda, 1014, 4),
                new DailyUnits(Side, Soda, 1015, 0),
                new DailyUnits(Side, Soda, 1016, 0),
                new DailyUnits(Side, Soda, 1017, 0),
            });

        var gap = Assert.Single(list.Unsupplied);
        Assert.Equal("short", gap.Reason);
        Assert.Equal("W1", gap.Warehouse);
    }

    [Fact]
    public void Supply_stock_on_hand_without_a_healthy_burn_is_short()
    {
        var list = Supply.List(
            new[] { Plan("W1", Side, Soda) },
            new[] { Hold("W1", "ba:street_fourthstreet", 46, Soda, 500) },
            new[] { new GoodsNeed(Side, Soda) },
            Array.Empty<DailyUnits>());

        var gap = Assert.Single(list.Unsupplied);
        Assert.Equal("short", gap.Reason);
        Assert.Equal("W1", gap.Warehouse);
    }

    [Fact]
    public void Supply_one_unit_under_seven_days_is_short()
    {
        var list = Supply.List(
            new[] { Plan("W2", First, Burger) },
            new[] { Hold("W2", "ba:street_fifthavenue", 43, Burger, 27) },
            new[] { new GoodsNeed(First, Burger) },
            new[] { new DailyUnits(First, Burger, 1014, 4) });

        var gap = Assert.Single(list.Unsupplied);
        Assert.Equal("short", gap.Reason);
        Assert.Equal("W2", gap.Warehouse);
    }

    [Fact]
    public void Supply_nowhere_is_unsupplied()
    {
        var list = Supply.List(
            Array.Empty<DeliveryStop>(),
            Array.Empty<WarehouseHold>(),
            new[] { new GoodsNeed(Salon, Hair) },
            Array.Empty<DailyUnits>());

        var gap = Assert.Single(list.Unsupplied);
        Assert.Equal(Salon, gap.Store);
        Assert.Equal("", gap.Warehouse);
        Assert.Equal("noplan", gap.Reason);
        Assert.DoesNotContain(Salon, list.Wholesaler);
    }

    [Fact]
    public void Supply_seven_healthy_days_is_supplied()
    {
        var list = Supply.List(
            new[] { Plan("W2", First, Burger) },
            new[] { Hold("W2", "ba:street_fifthavenue", 43, Burger, 28) },
            new[] { new GoodsNeed(First, Burger) },
            new[] { new DailyUnits(First, Burger, 1014, 4) });

        Assert.Empty(list.Unsupplied);
        Assert.Empty(list.Wholesaler);
    }

    [Fact]
    public void Supply_connected_burns_add()
    {
        var other = "ba:street_secondavenue 2";
        var list = Supply.List(
            new[]
            {
                Plan("W1", Side, Soda),
                Plan("W1", other, Soda),
            },
            new[] { Hold("W1", "ba:street_fourthstreet", 46, Soda, 40) },
            new[]
            {
                new GoodsNeed(Side, Soda),
                new GoodsNeed(other, Soda),
            },
            new[]
            {
                new DailyUnits(Side, Soda, 1014, 4),
                new DailyUnits(other, Soda, 1014, 4),
            });

        Assert.Equal(2, list.Unsupplied.Count);
        Assert.All(list.Unsupplied, gap =>
        {
            Assert.Equal("short", gap.Reason);
            Assert.Equal("W1", gap.Warehouse);
            Assert.Equal(Soda, gap.Item);
        });
    }

    [Fact]
    public void Supply_an_unplanned_burn_stays_off_the_buffer()
    {
        var list = Supply.List(
            new[] { Plan("W1", First, Soda) },
            new[] { Hold("W1", "ba:street_fourthstreet", 46, Soda, 28) },
            new[]
            {
                new GoodsNeed(First, Soda),
                new GoodsNeed(Salon, Soda),
            },
            new[]
            {
                new DailyUnits(First, Soda, 1014, 4),
                new DailyUnits(Salon, Soda, 1014, 100),
            });

        Assert.DoesNotContain(list.Unsupplied, gap => gap.Store == First);
        var salon = Assert.Single(list.Unsupplied);
        Assert.Equal(Salon, salon.Store);
        Assert.Equal("noplan", salon.Reason);
        Assert.Equal("W1", salon.Warehouse);
    }

    [Fact]
    public void Supply_two_warehouses_the_larger_hold_wins()
    {
        var list = Supply.List(
            Array.Empty<DeliveryStop>(),
            new[]
            {
                Hold("W1", "ba:street_a", 1, Soda, 3),
                Hold("W2", "ba:street_b", 2, Soda, 10),
            },
            new[] { new GoodsNeed(Side, Soda) },
            Array.Empty<DailyUnits>());

        var gap = Assert.Single(list.Unsupplied);
        Assert.Equal("W2", gap.Warehouse);
        Assert.Equal("noplan", gap.Reason);
    }

    [Fact]
    public void Supply_two_warehouses_a_tie_keeps_the_later()
    {
        var list = Supply.List(
            Array.Empty<DeliveryStop>(),
            new[]
            {
                Hold("W1", "ba:street_a", 1, Soda, 10),
                Hold("W2", "ba:street_b", 2, Soda, 10),
            },
            new[] { new GoodsNeed(Side, Soda) },
            Array.Empty<DailyUnits>());

        var gap = Assert.Single(list.Unsupplied);
        Assert.Equal("W2", gap.Warehouse);
        Assert.NotEqual("W1", gap.Warehouse);
    }

    [Fact]
    public void Supply_a_plan_on_the_smaller_warehouse_still_supplies()
    {
        var list = Supply.List(
            new[] { Plan("ba:street_a 1", Side, Soda) },
            new[]
            {
                Hold("W1", "ba:street_a", 1, Soda, 28),
                Hold("W2", "ba:street_b", 2, Soda, 100),
            },
            new[] { new GoodsNeed(Side, Soda) },
            new[] { new DailyUnits(Side, Soda, 1014, 4) });

        Assert.Empty(list.Unsupplied);
    }

    [Fact]
    public void Supply_a_wholesaler_item_a_warehouse_holds_is_unsupplied()
    {
        var list = Supply.List(
            new[] { Contract(Wholesaler, Fifth, Gift) },
            new[] { Hold("W1", "ba:street_a", 1, Gift, 20) },
            new[] { new GoodsNeed(Fifth, Gift) },
            Array.Empty<DailyUnits>());

        var gap = Assert.Single(list.Unsupplied);
        Assert.Equal(Fifth, gap.Store);
        Assert.Equal("W1", gap.Warehouse);
        Assert.Equal("noplan", gap.Reason);
        Assert.DoesNotContain(Fifth, list.Wholesaler);
    }

    [Fact]
    public void Supply_an_empty_warehouse_leaves_the_contract()
    {
        var list = Supply.List(
            new[] { Contract(Wholesaler, Fifth, Gift) },
            new[] { Hold("W1", "ba:street_a", 1, Gift, 0) },
            new[] { new GoodsNeed(Fifth, Gift) },
            Array.Empty<DailyUnits>());

        Assert.Equal(new[] { Fifth }, list.Wholesaler);
        Assert.Empty(list.Unsupplied);
    }

    [Fact]
    public void Supply_a_plan_to_an_empty_warehouse_names_the_one_that_holds_it()
    {
        var list = Supply.List(
            new[] { Plan("ba:street_sixthavenue 34", Salon, Hair) },
            new[]
            {
                Hold("W4", "ba:street_sixthavenue", 34, Hair, 0),
                Hold("W9", "ba:street_ninth", 9, Hair, 12),
            },
            new[] { new GoodsNeed(Salon, Hair) },
            new[] { new DailyUnits(Salon, Hair, 1014, 4) });

        var gap = Assert.Single(list.Unsupplied);
        Assert.Equal("noplan", gap.Reason);
        Assert.Equal("W9", gap.Warehouse);
        Assert.NotEqual("W4", gap.Warehouse);
    }

    [Fact]
    public void Supply_a_plan_into_an_empty_warehouse_is_dry()
    {
        var list = Supply.List(
            new[] { Plan("ba:street_sixthavenue 34", Salon, Hair) },
            new[] { Hold("W4", "ba:street_sixthavenue", 34, Hair, 0) },
            new[] { new GoodsNeed(Salon, Hair) },
            new[] { new DailyUnits(Salon, Hair, 1014, 4) });

        var gap = Assert.Single(list.Unsupplied);
        Assert.Equal("dry", gap.Reason);
        Assert.Equal("W4", gap.Warehouse);
        Assert.Equal(Salon, gap.Store);
        Assert.DoesNotContain(Salon, list.Wholesaler);
    }

    [Fact]
    public void Supply_a_recurring_plan_beats_the_dated_contract()
    {
        var list = Supply.List(
            new[]
            {
                Contract(Wholesaler, Side, Soda),
                Plan("W1", Side, Soda),
            },
            new[] { Hold("W1", "ba:street_fourthstreet", 46, Soda, 28) },
            new[] { new GoodsNeed(Side, Soda) },
            new[] { new DailyUnits(Side, Soda, 1014, 4) });

        Assert.Empty(list.Unsupplied);
        Assert.DoesNotContain(Side, list.Wholesaler);
    }

    [Fact]
    public void Supply_dump_routes_list_keeps_fifth_avenue_and_a_dry_store()
    {
        // Chain: dump → routes → list
        using var dir = new TempDump();
        var written = RouteDump.Write(
            dir.Path,
            new[]
            {
                Contract(Wholesaler, Fifth, Gift),
                Contract(Wholesaler, Fifth, ExpensiveGift),
                Contract(Wholesaler, Fifth, CheapFlower),
                Contract(Wholesaler, Fifth, ExpensiveFlower),
                Plan("ba:street_fourthstreet 46", Side, Soda),
                Plan("ba:street_fifthavenue 43", First, Burger),
            },
            new[]
            {
                Hold("W4", "ba:street_sixthavenue", 34, Hair, 40),
                Hold("W1", "ba:street_fourthstreet", 46, Soda, 10),
                Hold("W2", "ba:street_fifthavenue", 43, Burger, 28),
            });

        var list = Supply.FromDump(
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path),
            new[]
            {
                new GoodsNeed(Fifth, Gift),
                new GoodsNeed(Fifth, ExpensiveGift),
                new GoodsNeed(Fifth, CheapFlower),
                new GoodsNeed(Fifth, ExpensiveFlower),
                new GoodsNeed(Salon, Hair),
                new GoodsNeed(Side, Soda),
                new GoodsNeed(First, Burger),
            },
            new[]
            {
                new DailyUnits(Side, Soda, 1014, 4),
                new DailyUnits(Side, Soda, 1015, 0),
                new DailyUnits(Side, Soda, 1016, 0),
                new DailyUnits(Side, Soda, 1017, 0),
                new DailyUnits(First, Burger, 1014, 4),
            });

        Assert.StartsWith("routes.written", written);
        Assert.Equal(new[] { Fifth }, list.Wholesaler);
        Assert.DoesNotContain(Salon, list.Wholesaler);
        Assert.DoesNotContain(list.Unsupplied, gap => gap.Store == Fifth);
        Assert.DoesNotContain(list.Unsupplied, gap => gap.Store == First);
        var salon = Assert.Single(list.Unsupplied, gap => gap.Store == Salon);
        Assert.Equal("W4", salon.Warehouse);
        Assert.Equal(Hair, salon.Item);
        Assert.Equal("noplan", salon.Reason);
        var side = Assert.Single(list.Unsupplied, gap => gap.Store == Side);
        Assert.Equal("W1", side.Warehouse);
        Assert.Equal(Soda, side.Item);
        Assert.Equal("short", side.Reason);
    }

    [Fact]
    public void Supply_dump_routes_list_names_the_larger_warehouse()
    {
        // Chain: dump → routes → list
        using var dir = new TempDump();
        RouteDump.Write(
            dir.Path,
            Array.Empty<DeliveryStop>(),
            new[]
            {
                Hold("W1", "ba:street_a", 1, Soda, 3),
                Hold("W2", "ba:street_b", 2, Soda, 10),
            });

        var list = Supply.FromDump(
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path),
            new[] { new GoodsNeed(Side, Soda) },
            Array.Empty<DailyUnits>());

        var gap = Assert.Single(list.Unsupplied);
        Assert.Equal(Side, gap.Store);
        Assert.Equal("W2", gap.Warehouse);
        Assert.Equal(Soda, gap.Item);
        Assert.Equal("noplan", gap.Reason);
        Assert.NotEqual("W1", gap.Warehouse);
        Assert.Empty(list.Wholesaler);
    }

    [Fact]
    public void Supply_dump_routes_list_adds_connected_burns()
    {
        // Chain: dump → routes → list
        using var dir = new TempDump();
        var other = "ba:street_secondavenue 2";
        RouteDump.Write(
            dir.Path,
            new[]
            {
                Plan("ba:street_fourthstreet 46", Side, Soda),
                Plan("ba:street_fourthstreet 46", other, Soda),
            },
            new[] { Hold("W1", "ba:street_fourthstreet", 46, Soda, 40) });

        var list = Supply.FromDump(
            RouteDump.RoutesPath(dir.Path),
            RouteDump.WarehousesPath(dir.Path),
            new[]
            {
                new GoodsNeed(Side, Soda),
                new GoodsNeed(other, Soda),
            },
            new[]
            {
                new DailyUnits(Side, Soda, 1014, 4),
                new DailyUnits(other, Soda, 1014, 4),
            });

        Assert.Equal(2, list.Unsupplied.Count);
        Assert.All(list.Unsupplied, gap =>
        {
            Assert.Equal("short", gap.Reason);
            Assert.Equal("W1", gap.Warehouse);
        });
        Assert.Empty(list.Wholesaler);
    }

    private static DeliveryStop Plan(string warehouse, string destination, string item)
    {
        return new DeliveryStop(warehouse, destination, item, null, "");
    }

    private static DeliveryStop Contract(string warehouse, string destination, string item)
    {
        return new DeliveryStop(warehouse, destination, item, 1016, "");
    }

    [Fact]
    public void Supply_day_1014_salon_selling_is_noplan_on_W11()
    {
        var selling = Days(4);
        var list = Supply.List(Array.Empty<DeliveryStop>(), HairHub(), new[] { new GoodsNeed(Salon, Hair) }, selling);

        var gap = Assert.Single(list.Unsupplied);
        Assert.Equal(Salon, gap.Store);
        Assert.Equal("W11", gap.Warehouse);
        Assert.Equal(Hair, gap.Item);
        Assert.Equal("noplan", gap.Reason);
        Assert.NotEqual("W6", gap.Warehouse);
        Assert.DoesNotContain(Salon, list.Wholesaler);

        var afterZero = Supply.List(
            Array.Empty<DeliveryStop>(),
            HairHub(),
            new[] { new GoodsNeed(Salon, Hair) },
            new[]
            {
                selling[0],
                new DailyUnits(Salon, Hair, 1015, 0),
            });
        var still = Assert.Single(afterZero.Unsupplied);
        Assert.Equal("noplan", still.Reason);
        Assert.Equal("W11", still.Warehouse);
    }

    [Fact]
    public void Supply_a_connected_hub_under_seven_days_is_short_while_selling()
    {
        var list = Supply.List(
            new[] { Plan("W11", Salon, Hair) },
            new[] { Hold("W11", "ba:street_twelfthstreet", 1, Hair, 15) },
            new[] { new GoodsNeed(Salon, Hair) },
            Days(4));

        var gap = Assert.Single(list.Unsupplied);
        Assert.Equal("short", gap.Reason);
        Assert.Equal("W11", gap.Warehouse);
        Assert.Equal(28, gap.Target);
    }

    [Fact]
    public void Supply_burn_of_4_three_zeros_and_a_failed_connect_needs_28()
    {
        var days = new[]
        {
            new DailyUnits(Salon, Hair, 1014, 4),
            new DailyUnits(Salon, Hair, 1015, 0),
            new DailyUnits(Salon, Hair, 1016, 0),
            new DailyUnits(Salon, Hair, 1017, 0),
        };
        var missed = Supply.List(Array.Empty<DeliveryStop>(), HairHub(), new[] { new GoodsNeed(Salon, Hair) }, days);
        var gap = Assert.Single(missed.Unsupplied);
        Assert.Equal("noplan", gap.Reason);
        Assert.Equal("W11", gap.Warehouse);
        Assert.Equal(28, gap.Target);

        var under = Supply.List(
            new[] { Plan("W11", Salon, Hair) },
            new[] { Hold("W11", "ba:street_twelfthstreet", 1, Hair, 27) },
            new[] { new GoodsNeed(Salon, Hair) },
            days);
        Assert.Equal(28, Assert.Single(under.Unsupplied).Target);
        Assert.Equal("short", Assert.Single(under.Unsupplied).Reason);

        var met = Supply.List(
            new[] { Plan("W11", Salon, Hair) },
            new[] { Hold("W11", "ba:street_twelfthstreet", 1, Hair, 28) },
            new[] { new GoodsNeed(Salon, Hair) },
            days);
        Assert.Empty(met.Unsupplied);
    }

    [Fact]
    public void Supply_the_next_healthy_day_of_6_needs_42()
    {
        var days = new[]
        {
            new DailyUnits(Salon, Hair, 1014, 4),
            new DailyUnits(Salon, Hair, 1015, 0),
            new DailyUnits(Salon, Hair, 1016, 0),
            new DailyUnits(Salon, Hair, 1017, 0),
            new DailyUnits(Salon, Hair, 1018, 6),
        };
        var under = Supply.List(
            new[] { Plan("W11", Salon, Hair) },
            new[] { Hold("W11", "ba:street_twelfthstreet", 1, Hair, 41) },
            new[] { new GoodsNeed(Salon, Hair) },
            days);
        var gap = Assert.Single(under.Unsupplied);
        Assert.Equal("short", gap.Reason);
        Assert.Equal(42, gap.Target);

        var met = Supply.List(
            new[] { Plan("W11", Salon, Hair) },
            new[] { Hold("W11", "ba:street_twelfthstreet", 1, Hair, 42) },
            new[] { new GoodsNeed(Salon, Hair) },
            days);
        Assert.Empty(met.Unsupplied);
    }

    [Fact]
    public void Supply_fifth_avenue_stays_wholesale_until_the_hub_holds_one_day()
    {
        var days = new[] { new DailyUnits(Fifth, Gift, 1014, 4) };
        var thin = Supply.List(
            new[] { Contract(Wholesaler, Fifth, Gift) },
            new[] { Hold("W2", "ba:street_gift", 2, Gift, 3) },
            new[] { new GoodsNeed(Fifth, Gift) },
            days);
        Assert.Equal(new[] { Fifth }, thin.Wholesaler);
        Assert.Empty(thin.Unsupplied);

        var ready = Supply.List(
            new[] { Contract(Wholesaler, Fifth, Gift) },
            new[] { Hold("W2", "ba:street_gift", 2, Gift, 4) },
            new[] { new GoodsNeed(Fifth, Gift) },
            days);
        var gap = Assert.Single(ready.Unsupplied);
        Assert.Equal("noplan", gap.Reason);
        Assert.Equal("W2", gap.Warehouse);
        Assert.DoesNotContain(Fifth, ready.Wholesaler);
    }

    [Fact]
    public void Supply_a_hairdresser_needs_hair_and_a_gym_does_not()
    {
        var needs = new List<GoodsNeed>();
        Supply.Collect(needs, "ba:businesstype_hairdresser", Salon);
        Supply.Collect(needs, "ba:businesstype_gym", Side);
        Supply.Collect(needs, "ba:businesstype_lawfirm", First);
        Supply.Collect(needs, "ba:businesstype_fastfoodrestaurant", First);

        Assert.Contains(needs, need => need.Store == Salon && need.Item == Hair);
        Assert.DoesNotContain(needs, need => need.Store == Side);
        Assert.DoesNotContain(needs, need => need.Store == First && need.Item == Hair);
        Assert.Contains(needs, need => need.Store == First && need.Item == Burger);
    }

    [Fact]
    public void Supply_only_the_item_the_store_sells_sets_the_buffer_from_that_burn()
    {
        var needs = new List<GoodsNeed>();
        var burn = new List<DailyUnits>();
        Supply.FromStore(
            needs,
            burn,
            "ba:businesstype_fastfoodrestaurant",
            First,
            new[] { Burger, "ba:itemname_paperbag", "ba:itemname_palletshelf" },
            new[] { new SaleTick(Burger, 1014, 4), new SaleTick(Burger, 1015, 0) });
        Supply.FromStore(
            needs,
            burn,
            "ba:businesstype_hairdresser",
            Salon,
            Array.Empty<string>(),
            new[] { new SaleTick(Hair, 1014, 6) });

        Assert.Equal(Burger, Assert.Single(needs, need => need.Store == First).Item);
        Assert.DoesNotContain(needs, need => need.Item == Soda || need.Item == "ba:itemname_paperbag");
        Assert.Equal(Hair, Assert.Single(needs, need => need.Store == Salon).Item);
        Assert.Equal(4, Assert.Single(burn, day => day.Store == First).Units);
        Assert.Equal(6, Assert.Single(burn, day => day.Store == Salon).Units);

        var list = Supply.List(
            Array.Empty<DeliveryStop>(),
            HairHub(Hold("W1", "ba:street_food", 1, Burger, 10)),
            needs,
            burn);
        Assert.Equal(28, Assert.Single(list.Unsupplied, gap => gap.Store == First).Target);
        Assert.Equal(42, Assert.Single(list.Unsupplied, gap => gap.Store == Salon).Target);
        Assert.Equal(2, list.Unsupplied.Count);
    }

    [Fact]
    public void Supply_a_second_salon_keeps_its_own_burn()
    {
        var other = "ba:street_sixthavenue 12";
        var needs = new List<GoodsNeed>();
        var burn = new List<DailyUnits>();
        Supply.FromStore(
            needs,
            burn,
            "ba:businesstype_hairdresser",
            Salon,
            new[] { Hair },
            new[] { new SaleTick(Hair, 1014, 4) });
        Supply.FromStore(
            needs,
            burn,
            "ba:businesstype_hairdresser",
            other,
            Array.Empty<string>(),
            new[] { new SaleTick(Hair, 1014, 6), new SaleTick(Burger, 1014, 9) });

        Assert.Equal(Hair, Assert.Single(needs, need => need.Store == other).Item);
        Assert.DoesNotContain(needs, need => need.Store == other && need.Item == Burger);

        var list = Supply.List(Array.Empty<DeliveryStop>(), HairHub(), needs, burn);
        Assert.Equal(28, Assert.Single(list.Unsupplied, gap => gap.Store == Salon).Target);
        Assert.Equal(42, Assert.Single(list.Unsupplied, gap => gap.Store == other).Target);
    }

    [Fact]
    public void Supply_dump_routes_forecast_snapshot_connect_forecast_keeps_fifth_wholesale()
    {
        // Chain: dump → routes → forecast → snapshot → connect → forecast
        using var dir = new TempDump();
        var routes = new[] { Contract(Wholesaler, Fifth, Gift) };
        var holds = HairHub(Hold("W2", "ba:street_gift", 2, Gift, 3));
        var written = RouteDump.Write(dir.Path, routes, holds);
        var before = File.ReadAllText(RouteDump.RoutesPath(dir.Path));
        var days = new[]
        {
            new DailyUnits(Salon, Hair, 1014, 4),
            new DailyUnits(Salon, Hair, 1015, 0),
            new DailyUnits(Salon, Hair, 1016, 0),
            new DailyUnits(Salon, Hair, 1017, 0),
            new DailyUnits(Salon, Hair, 1018, 9, open: false),
            new DailyUnits(Fifth, Gift, 1014, 4),
        };

        var night = SupplyNight.Run(
            dir.Path,
            1014,
            new[]
            {
                new GoodsNeed(Fifth, Gift),
                new GoodsNeed(Salon, Hair),
            },
            days,
            dryRun: false);

        Assert.StartsWith("routes.written", written);
        Assert.Equal(new[] { Fifth }, night.Forecast.Wholesaler);
        var salon = Assert.Single(night.Forecast.Unsupplied, gap => gap.Store == Salon);
        Assert.Equal("noplan", salon.Reason);
        Assert.Equal("W11", salon.Warehouse);
        Assert.Equal(Hair, salon.Item);
        var shot = File.ReadAllText(SupplyNight.SnapshotPath(dir.Path));
        Assert.Contains("snapshot.taken version=1.15.4", shot);
        Assert.Contains(before, shot);
        Assert.DoesNotContain(Salon, shot);
        var again = Assert.Single(night.Recheck.Unsupplied, gap => gap.Store == Salon);
        Assert.NotEqual("noplan", again.Reason);
        Assert.Equal("short", again.Reason);
        Assert.Equal("W11", again.Warehouse);
        Assert.Equal(28, again.Target);
        Assert.Equal(new[] { Fifth }, night.Recheck.Wholesaler);
        Assert.Contains(Salon, File.ReadAllText(RouteDump.RoutesPath(dir.Path)));
    }

    [Fact]
    public void Supply_a_closed_Saturday_keeps_the_last_healthy_burn()
    {
        var days = new[]
        {
            new DailyUnits(Salon, Hair, 1014, 4),
            new DailyUnits(Salon, Hair, 1015, 0),
            new DailyUnits(Salon, Hair, 1016, 0),
            new DailyUnits(Salon, Hair, 1017, 0),
            new DailyUnits(Salon, Hair, 1018, 9, open: false),
        };
        var under = Supply.List(
            new[] { Plan("W11", Salon, Hair) },
            new[] { Hold("W11", "ba:street_twelfthstreet", 1, Hair, 27) },
            new[] { new GoodsNeed(Salon, Hair) },
            days);
        var gap = Assert.Single(under.Unsupplied);
        Assert.Equal("short", gap.Reason);
        Assert.Equal(28, gap.Target);

        var met = Supply.List(
            new[] { Plan("W11", Salon, Hair) },
            new[] { Hold("W11", "ba:street_twelfthstreet", 1, Hair, 28) },
            new[] { new GoodsNeed(Salon, Hair) },
            days);
        Assert.Empty(met.Unsupplied);
    }

    [Fact]
    public void Supply_only_a_player_store_enters_the_forecast()
    {
        Assert.True(Supply.PlayerStore("Chaotic Hair #9", rentedByPlayer: true, ownedByPlayer: false, rivalId: null));
        Assert.True(Supply.PlayerStore("Chaotic Clothing #3", rentedByPlayer: false, ownedByPlayer: true, rivalId: ""));
        Assert.False(Supply.PlayerStore("Rival Salon", rentedByPlayer: false, ownedByPlayer: false, rivalId: "rival_4"));
        Assert.False(Supply.PlayerStore("Rival In Player Building", rentedByPlayer: false, ownedByPlayer: true, rivalId: "rival_2"));
        Assert.False(Supply.PlayerStore("City Salon", rentedByPlayer: false, ownedByPlayer: false, rivalId: null));
        Assert.False(Supply.PlayerStore("", rentedByPlayer: true, ownedByPlayer: false, rivalId: null));

        var needs = new List<GoodsNeed>();
        var burn = new List<DailyUnits>();
        Supply.FromBuilding(
            needs,
            burn,
            new StoreRecord("Rival Salon", false, false, "rival_4", "ba:businesstype_hairdresser", Side),
            new[] { Hair },
            new[] { new SaleTick(Hair, 1014, 9) });
        Supply.FromBuilding(
            needs,
            burn,
            new StoreRecord("Chaotic Hair #9", true, false, null, "ba:businesstype_hairdresser", Salon),
            new[] { Hair },
            new[] { new SaleTick(Hair, 1014, 4) });

        Assert.Equal(Salon, Assert.Single(needs).Store);
        Assert.Equal(Salon, Assert.Single(burn).Store);
    }

    [Fact]
    public void Supply_burn_picks_the_highest_healthy_day_even_when_unsorted()
    {
        var days = new[]
        {
            new DailyUnits(Salon, Hair, 1015, 0),
            new DailyUnits(Salon, Hair, 1014, 788),
            new DailyUnits(Salon, Hair, 1008, 171),
        };

        var list = Supply.List(Array.Empty<DeliveryStop>(), HairHub(), new[] { new GoodsNeed(Salon, Hair) }, days);

        Assert.Equal(5516, Assert.Single(list.Unsupplied).Target);
    }

    [Fact]
    public void Supply_night_skips_blank_warehouses_without_a_move_or_a_strike()
    {
        using var dir = new TempDump();
        var needs = new List<GoodsNeed>
        {
            new GoodsNeed("ba:street_a 1", Soda),
            new GoodsNeed("ba:street_b 2", Soda),
            new GoodsNeed("ba:street_c 3", Soda),
            new GoodsNeed("ba:street_d 4", Soda),
            new GoodsNeed(Salon, Hair),
        };
        RouteDump.Write(dir.Path, Array.Empty<DeliveryStop>(), HairHub());

        var night = SupplyNight.Run(dir.Path, 1014, needs, Days(4), dryRun: false);

        var salon = Assert.Single(night.Recheck.Unsupplied, gap => gap.Store == Salon);
        Assert.Equal("short", salon.Reason);
        Assert.Equal(4, night.Recheck.Unsupplied.Count(gap => gap.Item == Soda && gap.Reason == "noplan" && gap.Warehouse.Length == 0));
        var lines = File.ReadAllLines(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log"));
        Assert.DoesNotContain(lines, line => line.StartsWith("supply.move ") && line.Contains(" warehouse= "));
        Assert.Contains(
            "supply.move version=" + LoadLog.Version + " day=1014 store=ba:street_fourthavenue 28 warehouse=W11 item=ba:itemname_haircareproduct amount=4 dry=false",
            lines);
    }

    [Fact]
    public void Supply_night_stops_at_ten_connects()
    {

        var many = new List<GoodsNeed>();
        var holds = new List<WarehouseHold>();
        for (var i = 1; i <= 11; i++)
        {
            var store = "ba:street_row " + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            many.Add(new GoodsNeed(store, Hair));
            holds.Add(Hold("W" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), "ba:street_hub", i, Hair, 15));
        }

        using var capped = new TempDump();
        RouteDump.Write(capped.Path, Array.Empty<DeliveryStop>(), holds);
        var night = SupplyNight.Run(capped.Path, 1014, many, Array.Empty<DailyUnits>(), dryRun: false);
        var plans = 0;
        var shorts = 0;
        foreach (var gap in night.Recheck.Unsupplied)
        {
            if (gap.Reason == "noplan")
            {
                plans++;
                Assert.Equal("ba:street_row 11", gap.Store);
            }

            if (gap.Reason == "short")
            {
                shorts++;
            }
        }

        Assert.Equal(1, plans);
        Assert.Equal(10, shorts);
    }

    private static DailyUnits[] Days(int units)
    {
        return new[] { new DailyUnits(Salon, Hair, 1014, units) };
    }

    private static WarehouseHold[] HairHub(params WarehouseHold[] extra)
    {
        var rows = new List<WarehouseHold>
        {
            Hold("W6", "ba:street_fifthavenue", 43, Hair, 40),
            Hold("W6", "ba:street_fifthavenue", 43, Burger, 8),
            Hold("W11", "ba:street_twelfthstreet", 1, Hair, 15),
            Hold("W11", "ba:street_twelfthstreet", 1, "ba:itemname_palletshelf", 4),
        };
        rows.AddRange(extra);
        return rows.ToArray();
    }

    private static WarehouseHold Hold(string warehouse, string street, int number, string item, int amount)
    {
        return new WarehouseHold(warehouse, street, number, item, amount);
    }

    private sealed class TempDump : IDisposable
    {
        public TempDump()
        {
            Path = Directory.CreateTempSubdirectory("chaotic-supply").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
