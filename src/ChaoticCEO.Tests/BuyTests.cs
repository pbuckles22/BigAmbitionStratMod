using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class BuyTests
{
    [Fact]
    public void Buy_lock_the_order()
    {
        var bought = Buy.Lock(
            FoodClose(),
            new[]
            {
                Lot("hq", "ba:street_hq", 1),
                Lot("food", "ba:street_warehouse", 10),
                Lot("street", "ba:street_main", 20),
            });

        Assert.True(bought.Bought);
        Assert.Equal("hq", bought.Place);
        Assert.Equal("ba:street_hq", bought.Street);
        Assert.Equal(1, bought.Number);
    }

    [Fact]
    public void Buy_the_food_warehouse_when_hq_is_absent()
    {
        var bought = Buy.Lock(
            FoodClose(),
            new[]
            {
                Lot("food", "ba:street_warehouse", 10),
                Lot("street", "ba:street_main", 20),
            });

        Assert.True(bought.Bought);
        Assert.Equal("food", bought.Place);
        Assert.Equal("ba:street_warehouse", bought.Street);
        Assert.Equal(10, bought.Number);
    }

    [Fact]
    public void Buy_an_owned_street_when_hq_and_the_warehouse_are_absent()
    {
        var bought = Buy.Lock(
            FoodClose(),
            new[]
            {
                Lot("street", "ba:street_main", 20),
            });

        Assert.True(bought.Bought);
        Assert.Equal("street", bought.Place);
        Assert.Equal("ba:street_main", bought.Street);
        Assert.Equal(20, bought.Number);
    }

    [Fact]
    public void Buy_a_later_warehouse_does_not_beat_hq()
    {
        var bought = Buy.Lock(
            FoodClose(),
            new[]
            {
                Lot("food", "ba:street_warehouse", 10),
                Lot("street", "ba:street_main", 20),
                Lot("hq", "ba:street_hq", 1),
            });

        Assert.Equal("hq", bought.Place);
        Assert.Equal(1, bought.Number);
        Assert.True(bought.Bought);
    }

    [Fact]
    public void Buy_a_tie_keeps_the_later_hq()
    {
        var bought = Buy.Lock(
            FoodClose(),
            new[]
            {
                Lot("hq", "ba:street_hq", 1),
                Lot("food", "ba:street_warehouse", 10),
                Lot("hq", "ba:street_annex", 7),
            });

        Assert.Equal("hq", bought.Place);
        Assert.Equal("ba:street_annex", bought.Street);
        Assert.Equal(7, bought.Number);
        Assert.True(bought.Bought);
    }

    [Fact]
    public void Buy_a_tie_keeps_the_later_food_warehouse()
    {
        var bought = Buy.Lock(
            FoodClose(),
            new[]
            {
                Lot("food", "ba:street_warehouse", 10),
                Lot("street", "ba:street_main", 20),
                Lot("food", "ba:street_cold", 11),
            });

        Assert.Equal("food", bought.Place);
        Assert.Equal("ba:street_cold", bought.Street);
        Assert.Equal(11, bought.Number);
    }

    [Fact]
    public void Buy_a_tie_keeps_the_later_owned_street()
    {
        var bought = Buy.Lock(
            FoodClose(),
            new[]
            {
                Lot("street", "ba:street_main", 20),
                Lot("street", "ba:street_main", 21),
            });

        Assert.Equal("street", bought.Place);
        Assert.Equal("ba:street_main", bought.Street);
        Assert.Equal(21, bought.Number);
        Assert.True(bought.Bought);
    }

    [Fact]
    public void Buy_another_street_is_not_owned()
    {
        var bought = Buy.Lock(
            FoodClose(),
            new[]
            {
                Lot("street", "ba:street_side", 20),
            });

        Assert.False(bought.Bought);
        Assert.Equal("", bought.Place);
        Assert.Equal("", bought.Street);
        Assert.Equal(0, bought.Number);
    }

    [Fact]
    public void Buy_an_open_store_keeps_no_buy()
    {
        var bought = Buy.Lock(
            new StoreClose("ba:street_main", 2, "food", false),
            new[]
            {
                Lot("hq", "ba:street_hq", 1),
                Lot("food", "ba:street_warehouse", 10),
                Lot("street", "ba:street_main", 20),
            });

        Assert.False(bought.Bought);
        Assert.Equal("", bought.Place);
        Assert.Equal(0, bought.Number);
    }

    [Fact]
    public void Buy_an_empty_close_keeps_no_buy()
    {
        var bought = Buy.Lock(
            new StoreClose("", 0, "", false),
            new[]
            {
                Lot("hq", "ba:street_hq", 1),
                Lot("food", "ba:street_warehouse", 10),
                Lot("street", "ba:street_main", 20),
            });

        Assert.False(bought.Bought);
        Assert.Equal("", bought.Street);
        Assert.Equal(0, bought.Number);
    }

    [Fact]
    public void Buy_a_missing_list_keeps_no_buy()
    {
        var bought = Buy.Lock(FoodClose(), null);

        Assert.False(bought.Bought);
        Assert.Equal("", bought.Place);
        Assert.Equal(0, bought.Number);
    }

    [Fact]
    public void Buy_a_blank_street_is_not_a_building()
    {
        var bought = Buy.Lock(
            FoodClose(),
            new[]
            {
                Lot("hq", "", 1),
                Lot("food", "ba:street_warehouse", 10),
            });

        Assert.Equal("food", bought.Place);
        Assert.Equal(10, bought.Number);
        Assert.True(bought.Bought);
    }

    [Fact]
    public void Buy_number_zero_is_not_a_building()
    {
        var bought = Buy.Lock(
            FoodClose(),
            new[]
            {
                Lot("hq", "ba:street_hq", 0),
                Lot("street", "ba:street_main", 20),
            });

        Assert.Equal("street", bought.Place);
        Assert.Equal(20, bought.Number);
        Assert.True(bought.Bought);
    }

    [Fact]
    public void Buy_an_unknown_place_is_not_a_buy()
    {
        var bought = Buy.Lock(
            FoodClose(),
            new[]
            {
                Lot("warehouse", "ba:street_warehouse", 10),
                Lot("specialty", "ba:street_gift", 4),
            });

        Assert.False(bought.Bought);
        Assert.Equal("", bought.Place);
        Assert.Equal(0, bought.Number);
    }

    private static StoreClose FoodClose()
    {
        return new StoreClose("ba:street_main", 2, "food", true);
    }

    private static BuildingLot Lot(string place, string street, int number)
    {
        return new BuildingLot(place, street, number);
    }
}
