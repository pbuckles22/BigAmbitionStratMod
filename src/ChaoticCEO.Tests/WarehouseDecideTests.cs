using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class WarehouseDecideTests
{
    [Fact]
    public void Warehouses_decide_entering_a_family_opens_the_next_empty_shelved_warehouse()
    {
        var pick = Warehouses.Decide(
            "specialty",
            enteringFamily: true,
            hasStockWithoutHome: false,
            new[]
            {
                Level("W1", "food", onHand: 40, daily: 4, ordered: false, shelves: 100),
                Level("W12", "", onHand: 0, daily: 0, ordered: false, shelves: 99),
                Level("W13", "", onHand: 0, daily: 0, ordered: false, shelves: 95),
            },
            trucksBusy: false);

        Assert.True(pick.Chosen);
        Assert.Equal("specialty", pick.Family);
        Assert.Equal("W12", pick.Warehouse);
        Assert.Equal("home", pick.Action);
        Assert.Equal("enter", pick.Reason);
    }

    [Fact]
    public void Warehouses_decide_stock_with_no_home_takes_the_next_empty_shelved_warehouse()
    {
        var pick = Warehouses.Decide(
            "specialty",
            enteringFamily: false,
            hasStockWithoutHome: true,
            new[]
            {
                Level("W9", "durables", onHand: 10, daily: 4, ordered: true, shelves: 100),
                Level("W12", "", onHand: 0, daily: 0, ordered: false, shelves: 99),
                Level("W14", "", onHand: 0, daily: 0, ordered: false, shelves: 0),
            },
            trucksBusy: false);

        Assert.True(pick.Chosen);
        Assert.Equal("W12", pick.Warehouse);
        Assert.Equal("home", pick.Action);
        Assert.Equal("stock", pick.Reason);
        Assert.NotEqual("W14", pick.Warehouse);
    }

    [Fact]
    public void Warehouses_decide_a_building_with_no_pallet_shelves_is_not_a_home()
    {
        var pick = Warehouses.Decide(
            "specialty",
            enteringFamily: true,
            hasStockWithoutHome: false,
            new[]
            {
                Level("W14", "", onHand: 0, daily: 0, ordered: false, shelves: 0),
            },
            trucksBusy: false);

        Assert.False(pick.Chosen);
        Assert.Equal("", pick.Warehouse);
        Assert.Equal("", pick.Action);
    }

    [Fact]
    public void Warehouses_decide_an_empty_warehouse_with_nothing_to_receive_stays_empty()
    {
        var pick = Warehouses.Decide(
            "specialty",
            enteringFamily: false,
            hasStockWithoutHome: false,
            new[]
            {
                Level("W1", "food", onHand: 40, daily: 4, ordered: false, shelves: 100),
                Level("W12", "", onHand: 0, daily: 0, ordered: false, shelves: 99),
            },
            trucksBusy: false);

        Assert.False(pick.Chosen);
        Assert.Equal("", pick.Warehouse);
    }

    [Fact]
    public void Warehouses_decide_trucks_over_busy_claim_a_second_warehouse()
    {
        var pick = Warehouses.Decide(
            "food",
            enteringFamily: false,
            hasStockWithoutHome: false,
            new[]
            {
                Level("W1", "food", onHand: 40, daily: 4, ordered: false, shelves: 100),
                Level("W12", "", onHand: 0, daily: 0, ordered: false, shelves: 99),
            },
            trucksBusy: true);

        Assert.True(pick.Chosen);
        Assert.Equal("W12", pick.Warehouse);
        Assert.Equal("second", pick.Action);
        Assert.Equal("trucks", pick.Reason);
    }

    [Fact]
    public void Warehouses_decide_an_ordered_home_under_refill_days_claims_a_second_warehouse()
    {
        var pick = Warehouses.Decide(
            "food",
            enteringFamily: false,
            hasStockWithoutHome: false,
            new[]
            {
                Level("W1", "food", onHand: 10, daily: 4, ordered: true, shelves: 100),
                Level("W12", "", onHand: 0, daily: 0, ordered: false, shelves: 99),
            },
            trucksBusy: false);

        Assert.True(pick.Chosen);
        Assert.Equal("W12", pick.Warehouse);
        Assert.Equal("second", pick.Action);
        Assert.Equal("short", pick.Reason);
    }

    [Fact]
    public void Warehouses_decide_a_normal_refill_dip_without_an_order_does_not_claim_a_second()
    {
        var pick = Warehouses.Decide(
            "food",
            enteringFamily: false,
            hasStockWithoutHome: false,
            new[]
            {
                Level("W1", "food", onHand: 10, daily: 4, ordered: false, shelves: 100),
                Level("W12", "", onHand: 0, daily: 0, ordered: false, shelves: 99),
            },
            trucksBusy: false);

        Assert.False(pick.Chosen);
        Assert.Equal("", pick.Action);
    }

    [Fact]
    public void Warehouses_decide_a_family_that_already_has_a_home_does_not_open_another_on_enter()
    {
        var pick = Warehouses.Decide(
            "food",
            enteringFamily: true,
            hasStockWithoutHome: false,
            new[]
            {
                Level("W1", "food", onHand: 40, daily: 4, ordered: false, shelves: 100),
                Level("W12", "", onHand: 0, daily: 0, ordered: false, shelves: 99),
            },
            trucksBusy: false);

        Assert.False(pick.Chosen);
    }

    [Fact]
    public void Warehouses_decide_a_second_family_stays_unhomed_until_an_owned_warehouse_can_take_it()
    {
        var pick = Warehouses.Decide(
            "specialty",
            enteringFamily: true,
            hasStockWithoutHome: false,
            new[]
            {
                Level("W1", "food", onHand: 40, daily: 4, ordered: false, shelves: 100),
            },
            trucksBusy: false);

        Assert.False(pick.Chosen);
        Assert.Equal("", pick.Warehouse);
    }

    [Fact]
    public void Warehouses_decide_a_later_empty_shelved_warehouse_wins_when_the_earlier_one_is_taken()
    {
        var pick = Warehouses.Decide(
            "specialty",
            enteringFamily: true,
            hasStockWithoutHome: false,
            new[]
            {
                Level("W12", "durables", onHand: 10, daily: 2, ordered: false, shelves: 99),
                Level("W13", "", onHand: 0, daily: 0, ordered: false, shelves: 95),
            },
            trucksBusy: false);

        Assert.True(pick.Chosen);
        Assert.Equal("W13", pick.Warehouse);
        Assert.Equal("home", pick.Action);
        Assert.Equal("enter", pick.Reason);
    }

    [Fact]
    public void Warehouses_decide_skips_blank_family_and_empty_lists()
    {
        var blank = Warehouses.Decide(
            "",
            enteringFamily: true,
            hasStockWithoutHome: false,
            new[] { Level("W12", "", 0, 0, false, 99) },
            trucksBusy: false);
        var missing = Warehouses.Decide(
            "food",
            enteringFamily: true,
            hasStockWithoutHome: false,
            null,
            trucksBusy: false);

        Assert.False(blank.Chosen);
        Assert.False(missing.Chosen);
    }

    [Fact]
    public void Warehouses_mixed_keeps_the_largest_family()
    {
        Assert.Equal(
            "food",
            Warehouses.KeepFamily(new Dictionary<string, int>
            {
                ["specialty"] = 40,
                ["food"] = 58,
                ["durables"] = 3,
            }));
        Assert.Equal(
            "specialty",
            Warehouses.KeepFamily(new Dictionary<string, int>
            {
                ["food"] = 40,
                ["specialty"] = 40,
            }));
        Assert.Equal("", Warehouses.KeepFamily(null));
        Assert.Equal("", Warehouses.KeepFamily(new Dictionary<string, int>()));
    }

    private static WarehouseLevel Level(
        string warehouse,
        string family,
        int onHand,
        int daily,
        bool ordered,
        int shelves)
    {
        return new WarehouseLevel(warehouse, family, onHand, daily, ordered, mixed: false, shelves);
    }
}
