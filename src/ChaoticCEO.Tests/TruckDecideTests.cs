using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class TruckDecideTests
{
    [Fact]
    public void Trucks_names_garage_slots_and_truck_busy()
    {
        Assert.Equal(2, Trucks.GarageSlots);
        Assert.Equal(0.85m, Open.TruckBusy);
    }

    [Fact]
    public void Trucks_decide_an_empty_slot_on_a_delivering_warehouse_is_the_first_spend_after_reserve()
    {
        var pick = Trucks.Decide(
            new[]
            {
                new GarageSlot("W1", 1, filled: true),
                new GarageSlot("W1", 2, filled: false),
                new GarageSlot("W7", 1, filled: false),
            },
            delivering: new[] { "W1", "W7" },
            cash: 10_000m,
            reserve: 1_000m,
            truckCost: 500m);

        Assert.True(pick.Chosen);
        Assert.Equal("W1", pick.Warehouse);
        Assert.Equal(2, pick.Slot);
    }

    [Fact]
    public void Trucks_decide_cash_inside_the_reserve_buys_no_truck()
    {
        var pick = Trucks.Decide(
            new[] { new GarageSlot("W1", 1, filled: false) },
            delivering: new[] { "W1" },
            cash: 1_200m,
            reserve: 1_000m,
            truckCost: 500m);

        Assert.False(pick.Chosen);
        Assert.Equal("", pick.Warehouse);
        Assert.Equal(0, pick.Slot);
    }

    [Fact]
    public void Trucks_decide_exact_reserve_plus_cost_still_buys()
    {
        var pick = Trucks.Decide(
            new[] { new GarageSlot("W1", 1, filled: false) },
            delivering: new[] { "W1" },
            cash: 1_500m,
            reserve: 1_000m,
            truckCost: 500m);

        Assert.True(pick.Chosen);
        Assert.Equal("W1", pick.Warehouse);
        Assert.Equal(1, pick.Slot);
    }

    [Fact]
    public void Trucks_decide_a_non_delivering_warehouse_is_not_the_first_spend()
    {
        var pick = Trucks.Decide(
            new[]
            {
                new GarageSlot("W14", 1, filled: false),
                new GarageSlot("W1", 1, filled: false),
            },
            delivering: new[] { "W1" },
            cash: 10_000m,
            reserve: 1_000m,
            truckCost: 500m);

        Assert.True(pick.Chosen);
        Assert.Equal("W1", pick.Warehouse);
        Assert.Equal(1, pick.Slot);
    }

    [Fact]
    public void Trucks_decide_a_filled_slot_is_left()
    {
        var pick = Trucks.Decide(
            new[]
            {
                new GarageSlot("W1", 1, filled: true),
                new GarageSlot("W1", 2, filled: true),
            },
            delivering: new[] { "W1" },
            cash: 10_000m,
            reserve: 0m,
            truckCost: 1m);

        Assert.False(pick.Chosen);
    }

    [Fact]
    public void Trucks_decide_a_later_empty_slot_on_the_same_pass_keeps_the_earlier_warehouse()
    {
        var pick = Trucks.Decide(
            new[]
            {
                new GarageSlot("W7", 1, filled: false),
                new GarageSlot("W8", 1, filled: false),
            },
            delivering: new[] { "W7", "W8" },
            cash: 10_000m,
            reserve: 0m,
            truckCost: 1m);

        Assert.True(pick.Chosen);
        Assert.Equal("W7", pick.Warehouse);
        Assert.Equal(1, pick.Slot);
    }

    [Fact]
    public void Trucks_decide_skips_blank_and_empty_lists()
    {
        var none = Trucks.Decide(null, new[] { "W1" }, 10_000m, 0m, 1m);
        var blankDelivering = Trucks.Decide(
            new[] { new GarageSlot("W1", 1, filled: false) },
            delivering: null,
            cash: 10_000m,
            reserve: 0m,
            truckCost: 1m);
        var badIndex = Trucks.Decide(
            new[] { new GarageSlot("W1", 0, filled: false) },
            delivering: new[] { "W1" },
            cash: 10_000m,
            reserve: 0m,
            truckCost: 1m);

        Assert.False(none.Chosen);
        Assert.False(blankDelivering.Chosen);
        Assert.False(badIndex.Chosen);
    }
}
