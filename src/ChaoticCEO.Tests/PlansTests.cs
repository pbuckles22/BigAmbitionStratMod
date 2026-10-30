using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class PlansTests
{
    [Fact]
    public void Plans_lock_one_family_run()
    {
        var price = new SoldPrice("ba:itemname_sodacan", 2.75f);
        var run = Plans.Lock(
            "W1",
            new[]
            {
                new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_sodacan", 45),
            },
            new[]
            {
                new DeliveryStop(
                    "ba:street_fourthstreet 46",
                    "ba:street_firstavenue 38",
                    "ba:itemname_sodacan",
                    1016,
                    ""),
            },
            price);

        Assert.Equal("W1", run.Warehouse);
        Assert.Equal("food", run.Family);
        Assert.Equal("ba:street_firstavenue 38", run.Destination);
        Assert.Equal("ba:itemname_sodacan", run.Item);
        Assert.Null(run.Day);
    }

    [Fact]
    public void Plans_the_larger_family_is_kept()
    {
        var run = Plans.Lock(
            "W1",
            new[]
            {
                new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_sodacan", 3),
                new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_paperbag", 100),
                new WarehouseHold("W1", "ba:street_fourthstreet", 46, "ba:itemname_cheapgift", 8),
            },
            new[]
            {
                new DeliveryStop("ba:street_fourthstreet 46", "ba:street_firstavenue 38", "ba:itemname_sodacan", 1016, ""),
                new DeliveryStop("ba:street_fourthstreet 46", "ba:street_fifthavenue 36", "ba:itemname_cheapgift", 1016, ""),
            },
            new SoldPrice("ba:itemname_sodacan", 2.75f));

        Assert.Equal("specialty", run.Family);
        Assert.Equal("ba:street_fifthavenue 36", run.Destination);
        Assert.Equal("ba:itemname_cheapgift", run.Item);
        Assert.Null(run.Day);
    }

    [Fact]
    public void Plans_same_family_amounts_add()
    {
        var run = Plans.Lock(
            "W1",
            new[]
            {
                new WarehouseHold("W1", "ba:street_a", 1, "ba:itemname_sodacan", 3),
                new WarehouseHold("W1", "ba:street_a", 1, "ba:itemname_burger", 3),
                new WarehouseHold("W1", "ba:street_a", 1, "ba:itemname_cheapgift", 5),
            },
            new[]
            {
                new DeliveryStop("ba:street_a 1", "Gift", "ba:itemname_cheapgift", null, ""),
                new DeliveryStop("ba:street_a 1", "Soda", "ba:itemname_sodacan", null, ""),
            },
            new SoldPrice("ba:itemname_cheapgift", 9.5f));

        Assert.Equal("food", run.Family);
        Assert.Equal("Soda", run.Destination);
        Assert.Equal("ba:itemname_sodacan", run.Item);
    }

    [Fact]
    public void Plans_a_tie_keeps_the_later_family()
    {
        var run = Plans.Lock(
            "W1",
            new[]
            {
                new WarehouseHold("W1", "ba:street_a", 1, "ba:itemname_sodacan", 4),
                new WarehouseHold("W1", "ba:street_a", 1, "ba:itemname_cheapgift", 4),
            },
            new[]
            {
                new DeliveryStop("ba:street_a 1", "Soda", "ba:itemname_sodacan", null, ""),
                new DeliveryStop("ba:street_a 1", "Gift", "ba:itemname_cheapgift", null, ""),
            },
            new SoldPrice("ba:itemname_sodacan", 2.75f));

        Assert.Equal("specialty", run.Family);
        Assert.Equal("Gift", run.Destination);
        Assert.Equal("ba:itemname_cheapgift", run.Item);
    }

    [Fact]
    public void Plans_amount_zero_is_not_cargo()
    {
        var run = Plans.Lock(
            "W1",
            new[]
            {
                new WarehouseHold("W1", "ba:street_a", 1, "ba:itemname_cheapgift", 0),
                new WarehouseHold("W1", "ba:street_a", 1, "ba:itemname_sodacan", 4),
            },
            new[]
            {
                new DeliveryStop("ba:street_a 1", "Soda", "ba:itemname_sodacan", null, ""),
                new DeliveryStop("ba:street_a 1", "Gift", "ba:itemname_cheapgift", null, ""),
            },
            new SoldPrice("ba:itemname_cheapgift", 9.5f));

        Assert.Equal("food", run.Family);
        Assert.Equal("Soda", run.Destination);
        Assert.Equal("ba:itemname_sodacan", run.Item);
    }

    [Fact]
    public void Plans_a_lamp_is_not_a_family()
    {
        var run = Plans.Lock(
            "W1",
            new[]
            {
                new WarehouseHold("W1", "ba:street_a", 1, "ba:itemname_ceilinglampwarehouse", 100),
                new WarehouseHold("W1", "ba:street_a", 1, "ba:itemname_paperbag", 50),
                new WarehouseHold("W1", "ba:street_a", 1, "ba:itemname_sodacan", 1),
            },
            new[]
            {
                new DeliveryStop("ba:street_a 1", "Lamp", "ba:itemname_ceilinglampwarehouse", null, ""),
                new DeliveryStop("ba:street_a 1", "Soda", "ba:itemname_sodacan", null, ""),
            },
            new SoldPrice("ba:itemname_ceilinglampwarehouse", 1f));

        Assert.Equal("food", run.Family);
        Assert.Equal("Soda", run.Destination);
        Assert.Equal("ba:itemname_sodacan", run.Item);
    }

    [Fact]
    public void Plans_a_recurring_stop_beats_the_priced_contract()
    {
        var run = Plans.Lock(
            "W1",
            new[]
            {
                new WarehouseHold("W1", "ba:street_a", 1, "ba:itemname_sodacan", 5),
                new WarehouseHold("W1", "ba:street_a", 1, "ba:itemname_burger", 5),
            },
            new[]
            {
                new DeliveryStop("ba:street_a 1", "Burger", "ba:itemname_burger", null, ""),
                new DeliveryStop("ba:street_a 1", "Soda", "ba:itemname_sodacan", 1016, ""),
            },
            new SoldPrice("ba:itemname_sodacan", 2.75f));

        Assert.Equal("food", run.Family);
        Assert.Equal("Burger", run.Destination);
        Assert.Equal("ba:itemname_burger", run.Item);
        Assert.Null(run.Day);
    }

    [Fact]
    public void Plans_the_sold_price_picks_the_item_in_the_family()
    {
        var run = Plans.Lock(
            "W1",
            new[]
            {
                new WarehouseHold("W1", "ba:street_a", 1, "ba:itemname_sodacan", 5),
                new WarehouseHold("W1", "ba:street_a", 1, "ba:itemname_burger", 5),
            },
            new[]
            {
                new DeliveryStop("ba:street_a 1", "Soda", "ba:itemname_sodacan", null, ""),
                new DeliveryStop("ba:street_a 1", "Burger", "ba:itemname_burger", null, ""),
            },
            new SoldPrice("ba:itemname_sodacan", 2.75f));

        Assert.Equal("food", run.Family);
        Assert.Equal("Soda", run.Destination);
        Assert.Equal("ba:itemname_sodacan", run.Item);
        Assert.Null(run.Day);
    }

    [Fact]
    public void Plans_a_later_recurring_stop_wins()
    {
        var run = Plans.Lock(
            "W1",
            new[]
            {
                new WarehouseHold("W1", "ba:street_a", 1, "ba:itemname_burger", 5),
            },
            new[]
            {
                new DeliveryStop("ba:street_a 1", "Early", "ba:itemname_burger", null, ""),
                new DeliveryStop("ba:street_a 1", "Late", "ba:itemname_burger", null, ""),
            },
            new SoldPrice("ba:itemname_notsold", 1f));

        Assert.Equal("Late", run.Destination);
        Assert.Null(run.Day);
    }

    [Fact]
    public void Plans_another_warehouse_does_not_change_the_run()
    {
        var run = Plans.Lock(
            "W1",
            new[]
            {
                new WarehouseHold("W2", "ba:street_b", 2, "ba:itemname_cheapgift", 1000),
                new WarehouseHold("W1", "ba:street_a", 1, "ba:itemname_sodacan", 1),
            },
            new[]
            {
                new DeliveryStop("ba:street_b 2", "Gift", "ba:itemname_cheapgift", null, ""),
                new DeliveryStop("ba:street_a 1", "Soda", "ba:itemname_sodacan", null, ""),
            },
            new SoldPrice("ba:itemname_cheapgift", 9.5f));

        Assert.Equal("W1", run.Warehouse);
        Assert.Equal("food", run.Family);
        Assert.Equal("Soda", run.Destination);
        Assert.Equal("ba:itemname_sodacan", run.Item);
    }

    [Fact]
    public void Plans_a_stop_on_the_warehouse_name_still_runs()
    {
        var run = Plans.Lock(
            "W1",
            new[]
            {
                new WarehouseHold("W1", "ba:street_a", 1, "ba:itemname_sodacan", 2),
            },
            new[]
            {
                new DeliveryStop("W1", "Soda", "ba:itemname_sodacan", 1016, ""),
            },
            new SoldPrice("ba:itemname_sodacan", 2.75f));

        Assert.Equal("Soda", run.Destination);
        Assert.Equal("food", run.Family);
        Assert.Null(run.Day);
    }

    [Fact]
    public void Plans_one_warehouse_name_keeps_one_family_across_addresses()
    {
        var run = Plans.Lock(
            "W1",
            new[]
            {
                new WarehouseHold("W1", "ba:street_a", 1, "ba:itemname_sodacan", 2),
                new WarehouseHold("W1", "ba:street_b", 9, "ba:itemname_cheapgift", 9),
            },
            new[]
            {
                new DeliveryStop("ba:street_a 1", "Soda", "ba:itemname_sodacan", null, ""),
                new DeliveryStop("ba:street_b 9", "Gift", "ba:itemname_cheapgift", null, ""),
            },
            new SoldPrice("ba:itemname_sodacan", 2.75f));

        Assert.Equal("specialty", run.Family);
        Assert.Equal("Gift", run.Destination);
        Assert.Equal("ba:itemname_cheapgift", run.Item);
    }

    [Fact]
    public void Plans_an_unknown_item_is_its_own_family()
    {
        var run = Plans.Lock(
            "W1",
            new[]
            {
                new WarehouseHold("W1", "ba:street_a", 1, "ba:itemname_sodacan", 1),
                new WarehouseHold("W1", "ba:street_a", 1, "ba:itemname_notinstock", 5),
            },
            new[]
            {
                new DeliveryStop("ba:street_a 1", "Soda", "ba:itemname_sodacan", null, ""),
                new DeliveryStop("ba:street_a 1", "Other", "ba:itemname_notinstock", null, ""),
            },
            new SoldPrice("ba:itemname_sodacan", 2.75f));

        Assert.Equal("unknown", run.Family);
        Assert.Equal("Other", run.Destination);
        Assert.Equal("ba:itemname_notinstock", run.Item);
    }

    [Fact]
    public void Plans_an_empty_warehouse_has_no_run()
    {
        var run = Plans.Lock("W1", Array.Empty<WarehouseHold>(), Array.Empty<DeliveryStop>(), new SoldPrice("", 0f));

        Assert.Equal("W1", run.Warehouse);
        Assert.Equal("", run.Family);
        Assert.Equal("", run.Destination);
        Assert.Equal("", run.Item);
        Assert.Null(run.Day);
    }
}
