using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class SupplyDecideTests
{
    private const string Gift = "ba:itemname_cheapgift";
    private const string Soda = "ba:itemname_sodacan";
    private const string Store = "ba:street_main 4";
    private const string Other = "ba:street_side 9";

    [Fact]
    public void Supply_names_refill_days()
    {
        Assert.Equal(7, Supply.RefillDays);
    }

    [Fact]
    public void Supply_decide_stage_one_uses_a_wholesaler_contract()
    {
        var pick = Supply.Decide(
            1,
            Store,
            Gift,
            new[] { Hold("W1", Gift, 40) },
            Array.Empty<DeliveryStop>());

        Assert.True(pick.Chosen);
        Assert.Equal("wholesaler", pick.Source);
        Assert.Equal("", pick.Warehouse);
        Assert.False(pick.DropContract);
    }

    [Fact]
    public void Supply_decide_stage_two_moves_a_held_item_onto_the_warehouse_plan()
    {
        var pick = Supply.Decide(
            2,
            Store,
            Gift,
            new[] { Hold("W12", Gift, 40) },
            Array.Empty<DeliveryStop>());

        Assert.True(pick.Chosen);
        Assert.Equal("warehouse", pick.Source);
        Assert.Equal("W12", pick.Warehouse);
        Assert.True(pick.DropContract);
    }

    [Fact]
    public void Supply_decide_a_wholesaler_contract_stays_only_for_an_item_no_warehouse_holds()
    {
        var kept = Supply.Decide(
            3,
            Store,
            Gift,
            new[] { Hold("W1", Soda, 40) },
            Array.Empty<DeliveryStop>());
        var dropped = Supply.Decide(
            3,
            Store,
            Soda,
            new[] { Hold("W1", Soda, 40) },
            Array.Empty<DeliveryStop>());

        Assert.True(kept.Chosen);
        Assert.Equal("wholesaler", kept.Source);
        Assert.False(kept.DropContract);
        Assert.True(dropped.Chosen);
        Assert.Equal("warehouse", dropped.Source);
        Assert.Equal("W1", dropped.Warehouse);
        Assert.True(dropped.DropContract);
    }

    [Fact]
    public void Supply_decide_a_recurring_plan_stop_beats_a_dated_contract()
    {
        var pick = Supply.Decide(
            2,
            Store,
            Gift,
            new[] { Hold("W12", Gift, 40), Hold("W2", Gift, 10) },
            new[]
            {
                new DeliveryStop("ba:street_firststreet 37", Store, Gift, day: 1016, truck: ""),
                new DeliveryStop("W12", Store, Gift, day: null, truck: "T1"),
            });

        Assert.True(pick.Chosen);
        Assert.Equal("warehouse", pick.Source);
        Assert.Equal("W12", pick.Warehouse);
        Assert.True(pick.DropContract);
    }

    [Fact]
    public void Supply_decide_a_dated_contract_stays_until_a_hub_holds_the_item()
    {
        var pick = Supply.Decide(
            2,
            Store,
            Gift,
            Array.Empty<WarehouseHold>(),
            new[]
            {
                new DeliveryStop("ba:street_firststreet 37", Store, Gift, day: 1016, truck: ""),
            });

        Assert.True(pick.Chosen);
        Assert.Equal("wholesaler", pick.Source);
        Assert.Equal("", pick.Warehouse);
        Assert.False(pick.DropContract);
    }

    [Fact]
    public void Supply_decide_one_store_and_family_keep_one_supplier()
    {
        var first = Supply.Decide(
            4,
            Store,
            Gift,
            new[] { Hold("W12", Gift, 40) },
            new[]
            {
                new DeliveryStop("W12", Store, Gift, day: null, truck: "T1"),
                new DeliveryStop("W9", Store, Gift, day: null, truck: "T2"),
            });
        var otherStore = Supply.Decide(
            4,
            Other,
            Gift,
            new[] { Hold("W12", Gift, 40) },
            new[]
            {
                new DeliveryStop("W9", Other, Gift, day: null, truck: "T2"),
            });

        Assert.True(first.Chosen);
        Assert.Equal("W12", first.Warehouse);
        Assert.True(otherStore.Chosen);
        Assert.Equal("W9", otherStore.Warehouse);
    }

    [Fact]
    public void Supply_decide_skips_blank_store_and_item()
    {
        var blankStore = Supply.Decide(2, "", Gift, new[] { Hold("W1", Gift, 1) }, null);
        var blankItem = Supply.Decide(2, Store, "", new[] { Hold("W1", Gift, 1) }, null);
        var missingHolds = Supply.Decide(2, Store, Gift, null, null);

        Assert.False(blankStore.Chosen);
        Assert.False(blankItem.Chosen);
        Assert.True(missingHolds.Chosen);
        Assert.Equal("wholesaler", missingHolds.Source);
        Assert.False(missingHolds.DropContract);
    }

    [Fact]
    public void Supply_needs_refill_when_stock_falls_below_refill_days_of_burn()
    {
        Assert.True(Supply.NeedsRefill(onHand: 27, dailyBurn: 4));
        Assert.False(Supply.NeedsRefill(onHand: 28, dailyBurn: 4));
        Assert.False(Supply.NeedsRefill(onHand: 0, dailyBurn: 0));
        Assert.Equal(28, Supply.OrderTarget(dailyBurn: 4));
        Assert.Equal(0, Supply.OrderTarget(dailyBurn: 0));
    }

    [Fact]
    public void Supply_first_order_uses_wholesaler_burn_before_stores_connect()
    {
        Assert.Equal(42, Supply.OrderTarget(dailyBurn: 6));
        Assert.True(Supply.NeedsRefill(onHand: 0, dailyBurn: 6));
    }

    [Fact]
    public void Supply_decide_a_zero_amount_hold_is_not_a_hub()
    {
        var pick = Supply.Decide(
            2,
            Store,
            Gift,
            new[] { Hold("W12", Gift, 0) },
            Array.Empty<DeliveryStop>());

        Assert.True(pick.Chosen);
        Assert.Equal("wholesaler", pick.Source);
        Assert.Equal("", pick.Warehouse);
        Assert.False(pick.DropContract);
    }

    [Fact]
    public void Supply_decide_a_recurring_stop_without_stock_keeps_the_contract()
    {
        var pick = Supply.Decide(
            2,
            Store,
            Gift,
            Array.Empty<WarehouseHold>(),
            new[]
            {
                new DeliveryStop("W12", Store, Gift, day: null, truck: "T1"),
            });

        Assert.True(pick.Chosen);
        Assert.Equal("warehouse", pick.Source);
        Assert.Equal("W12", pick.Warehouse);
        Assert.False(pick.DropContract);
    }

    private static WarehouseHold Hold(string warehouse, string item, int amount)
    {
        return new WarehouseHold(warehouse, "ba:street_wh", 1, item, amount);
    }
}
