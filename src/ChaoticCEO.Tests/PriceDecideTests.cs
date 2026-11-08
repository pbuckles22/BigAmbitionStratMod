using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class PriceDecideTests
{
    [Fact]
    public void Prices_names_price_step_and_satisfied()
    {
        Assert.Equal(0.05m, Prices.PriceStep);
        Assert.Equal(0.75m, Prices.PriceSatisfied);
    }

    [Fact]
    public void Prices_decide_leaves_the_default_price()
    {
        var pick = Prices.Decide(
            "ba:itemname_gift",
            currentPrice: 10m,
            defaultPrice: 10m,
            wholesale: 4m,
            priorUnits: 5,
            currentUnits: 3,
            satisfaction: 0.5m,
            trials: null);

        Assert.True(pick.Chosen);
        Assert.Equal("ba:itemname_gift", pick.Item);
        Assert.Equal(10.5m, pick.Price);
        Assert.NotEqual(10m, pick.Price);
    }

    [Fact]
    public void Prices_decide_raises_by_price_step_while_units_hold_and_satisfaction_stays()
    {
        var pick = Prices.Decide(
            "ba:itemname_gift",
            currentPrice: 20m,
            defaultPrice: 10m,
            wholesale: 4m,
            priorUnits: 8,
            currentUnits: 8,
            satisfaction: Prices.PriceSatisfied,
            trials: new[]
            {
                new ItemSoldPerPriceEntry("ba:itemname_gift", 18f, 9),
            });

        Assert.True(pick.Chosen);
        Assert.Equal(21m, pick.Price);
    }

    [Fact]
    public void Prices_decide_steps_back_when_units_drop()
    {
        var pick = Prices.Decide(
            "ba:itemname_gift",
            currentPrice: 20m,
            defaultPrice: 10m,
            wholesale: 4m,
            priorUnits: 10,
            currentUnits: 9,
            satisfaction: 0.9m,
            trials: new[]
            {
                new ItemSoldPerPriceEntry("ba:itemname_gift", 12f, 10),
                new ItemSoldPerPriceEntry("ba:itemname_gift", 18f, 5),
                new ItemSoldPerPriceEntry("ba:itemname_gift", 10f, 20),
            });

        Assert.True(pick.Chosen);
        Assert.Equal(12m, pick.Price);
    }

    [Fact]
    public void Prices_decide_steps_back_when_satisfaction_falls_below_the_floor()
    {
        var pick = Prices.Decide(
            "ba:itemname_gift",
            currentPrice: 20m,
            defaultPrice: 10m,
            wholesale: 4m,
            priorUnits: 5,
            currentUnits: 5,
            satisfaction: Prices.PriceSatisfied - 0.01m,
            trials: new[]
            {
                new ItemSoldPerPriceEntry("ba:itemname_gift", 16f, 4),
                new ItemSoldPerPriceEntry("ba:itemname_gift", 14f, 6),
            });

        Assert.True(pick.Chosen);
        Assert.Equal(14m, pick.Price);
    }

    [Fact]
    public void Prices_decide_a_step_back_tie_keeps_the_later_price()
    {
        var pick = Prices.Decide(
            "ba:itemname_gift",
            currentPrice: 20m,
            defaultPrice: 10m,
            wholesale: 5m,
            priorUnits: 8,
            currentUnits: 4,
            satisfaction: 0.9m,
            trials: new[]
            {
                new ItemSoldPerPriceEntry("ba:itemname_gift", 15f, 4),
                new ItemSoldPerPriceEntry("ba:itemname_gift", 13f, 5),
            });

        Assert.True(pick.Chosen);
        Assert.Equal(13m, pick.Price);
    }

    [Fact]
    public void Prices_decide_never_returns_the_default_on_a_step_back()
    {
        var pick = Prices.Decide(
            "ba:itemname_gift",
            currentPrice: 20m,
            defaultPrice: 10m,
            wholesale: 1m,
            priorUnits: 8,
            currentUnits: 2,
            satisfaction: 0.2m,
            trials: new[]
            {
                new ItemSoldPerPriceEntry("ba:itemname_gift", 10f, 50),
                new ItemSoldPerPriceEntry("ba:itemname_gift", 12f, 3),
            });

        Assert.True(pick.Chosen);
        Assert.Equal(12m, pick.Price);
        Assert.NotEqual(10m, pick.Price);
    }

    [Fact]
    public void Prices_decide_skips_blank_item_and_non_positive_prices()
    {
        var blank = Prices.Decide("", 10m, 8m, 1m, 1, 1, 1m, null);
        var zeroCurrent = Prices.Decide("ba:itemname_gift", 0m, 8m, 1m, 1, 1, 1m, null);
        var negativeDefault = Prices.Decide("ba:itemname_gift", 10m, -1m, 1m, 1, 1, 1m, null);

        Assert.False(blank.Chosen);
        Assert.False(zeroCurrent.Chosen);
        Assert.True(negativeDefault.Chosen);
        Assert.Equal(10.5m, negativeDefault.Price);
    }

    [Fact]
    public void Prices_decide_empty_trials_on_a_step_back_keep_the_current_price()
    {
        var pick = Prices.Decide(
            "ba:itemname_gift",
            currentPrice: 20m,
            defaultPrice: 10m,
            wholesale: 4m,
            priorUnits: 8,
            currentUnits: 2,
            satisfaction: 0.2m,
            trials: Array.Empty<ItemSoldPerPriceEntry>());

        Assert.True(pick.Chosen);
        Assert.Equal(20m, pick.Price);
    }
}
