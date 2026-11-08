using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class StageTests
{
    [Fact]
    public void Stage_names_the_gate_thresholds()
    {
        Assert.Equal(150000m, Stage.Stage2Cash);
        Assert.Equal(3, Stage.Stage2Stores);
        Assert.Equal(5, Stage.Stage3Supplied);
        Assert.Equal(2000000m, Stage.Stage4Cash);
    }

    [Fact]
    public void Stage_decide_stays_at_one_before_any_gate()
    {
        Assert.Equal(1, Stage.Decide(1, 0m, 0, 0, 0, 0, 0));
        Assert.Equal(1, Stage.Decide(1, Stage.Stage2Cash - 1m, Stage.Stage2Stores, 0, 0, 0, 0));
        Assert.Equal(1, Stage.Decide(1, Stage.Stage2Cash, Stage.Stage2Stores - 1, 0, 0, 0, 0));
        Assert.Equal(1, Stage.Decide(365, 149999.99m, 2, 4, 9, 3, 0));
    }

    [Fact]
    public void Stage_decide_enters_two_at_cash_and_store_floor()
    {
        Assert.Equal(2, Stage.Decide(10, Stage.Stage2Cash, Stage.Stage2Stores, 0, 0, 0, 0));
        Assert.Equal(2, Stage.Decide(10, Stage.Stage2Cash, 4, 0, 1, 2, 0));
        Assert.Equal(2, Stage.Decide(10, 500000m, 3, 4, 0, 1, 0));
    }

    [Fact]
    public void Stage_decide_enters_three_when_a_plan_is_live_and_supplied_hits_five()
    {
        Assert.Equal(3, Stage.Decide(20, 1000m, 5, Stage.Stage3Supplied, 1, 0, 1));
        Assert.Equal(3, Stage.Decide(20, Stage.Stage2Cash, 6, 6, 2, 2, 2));
        Assert.Equal(1, Stage.Decide(20, 1000m, 5, Stage.Stage3Supplied, 1, 0, 0));
        Assert.Equal(1, Stage.Decide(20, 1000m, 5, Stage.Stage3Supplied - 1, 1, 0, 1));
    }

    [Fact]
    public void Stage_decide_enters_four_when_cash_supply_and_slots_pass()
    {
        Assert.Equal(
            4,
            Stage.Decide(30, Stage.Stage4Cash, 5, 5, 2, 0, 1));
        Assert.Equal(
            4,
            Stage.Decide(30, 3000000m, 0, 0, 0, 0, 0));
        Assert.Equal(
            3,
            Stage.Decide(30, Stage.Stage4Cash - 1m, 5, 5, 2, 0, 1));
        Assert.Equal(
            2,
            Stage.Decide(30, Stage.Stage4Cash, 5, 4, 2, 0, 1));
        Assert.Equal(
            3,
            Stage.Decide(30, Stage.Stage4Cash, 5, 5, 2, 1, 1));
    }

    [Fact]
    public void Stage_decide_higher_gate_wins_when_several_match()
    {
        Assert.Equal(
            4,
            Stage.Decide(40, Stage.Stage4Cash, 5, 5, 2, 0, 1));
        Assert.Equal(
            3,
            Stage.Decide(40, Stage.Stage2Cash, 5, Stage.Stage3Supplied, 1, 0, 1));
        Assert.Equal(
            2,
            Stage.Decide(40, Stage.Stage2Cash, Stage.Stage2Stores, 0, 0, 0, 0));
    }

    [Fact]
    public void Stage_decide_ignores_day_and_warehouse_count()
    {
        Assert.Equal(
            2,
            Stage.Decide(1, Stage.Stage2Cash, Stage.Stage2Stores, 0, 0, 0, 0));
        Assert.Equal(
            2,
            Stage.Decide(9999, Stage.Stage2Cash, Stage.Stage2Stores, 0, 99, 0, 0));
        Assert.Equal(
            4,
            Stage.Decide(1, Stage.Stage4Cash, 3, 3, 0, 0, 0));
        Assert.Equal(
            4,
            Stage.Decide(9999, Stage.Stage4Cash, 3, 3, 50, 0, 0));
    }

    [Fact]
    public void Stage_read_feeds_decide_from_the_world_records()
    {
        var needs = new[]
        {
            new GoodsNeed("ba:street_a 1", "ba:itemname_gift"),
            new GoodsNeed("ba:street_b 2", "ba:itemname_gift"),
            new GoodsNeed("ba:street_c 3", "ba:itemname_gift"),
        };
        var stage2 = Stage.Read(
            10,
            Stage.Stage2Cash,
            needs,
            Array.Empty<DeliveryStop>(),
            Array.Empty<WarehouseHold>(),
            Array.Empty<GarageSlot>());
        var stops = new[]
        {
            new DeliveryStop("W1", "ba:street_a 1", "ba:itemname_gift", null, ""),
            new DeliveryStop("W1", "ba:street_b 2", "ba:itemname_gift", null, ""),
            new DeliveryStop("W1", "ba:street_c 3", "ba:itemname_gift", null, ""),
            new DeliveryStop("W1", "ba:street_d 4", "ba:itemname_gift", null, ""),
            new DeliveryStop("W1", "ba:street_e 5", "ba:itemname_gift", null, ""),
        };
        var fiveNeeds = new[]
        {
            new GoodsNeed("ba:street_a 1", "ba:itemname_gift"),
            new GoodsNeed("ba:street_b 2", "ba:itemname_gift"),
            new GoodsNeed("ba:street_c 3", "ba:itemname_gift"),
            new GoodsNeed("ba:street_d 4", "ba:itemname_gift"),
            new GoodsNeed("ba:street_e 5", "ba:itemname_gift"),
        };
        var stage3 = Stage.Read(
            20,
            1000m,
            fiveNeeds,
            stops,
            new[] { new WarehouseHold("W1", "ba:street_wh", 1, "ba:itemname_gift", 10) },
            Array.Empty<GarageSlot>());
        var stage4 = Stage.Read(
            30,
            Stage.Stage4Cash,
            fiveNeeds,
            stops,
            new[] { new WarehouseHold("W1", "ba:street_wh", 1, "ba:itemname_gift", 10) },
            Array.Empty<GarageSlot>());
        var blocked = Stage.Read(
            30,
            Stage.Stage4Cash,
            fiveNeeds,
            stops,
            new[] { new WarehouseHold("W1", "ba:street_wh", 1, "ba:itemname_gift", 10) },
            new[] { new GarageSlot("W1", 1, filled: false) });

        Assert.Equal(2, stage2);
        Assert.Equal(3, stage3);
        Assert.Equal(4, stage4);
        Assert.Equal(3, blocked);
    }

    [Fact]
    public void Stage_read_counts_unique_stores_and_skips_blank_names()
    {
        var needs = new[]
        {
            new GoodsNeed("ba:street_a 1", "ba:itemname_gift"),
            new GoodsNeed("ba:street_a 1", "ba:itemname_other"),
            new GoodsNeed("", "ba:itemname_gift"),
            new GoodsNeed("ba:street_b 2", "ba:itemname_gift"),
            new GoodsNeed("ba:street_c 3", "ba:itemname_gift"),
        };
        var open = Stage.Read(
            10,
            Stage.Stage2Cash,
            needs,
            Array.Empty<DeliveryStop>(),
            null,
            null);
        var stops = new[]
        {
            new DeliveryStop("", "ba:street_a 1", "ba:itemname_gift", null, ""),
            new DeliveryStop("W1", "ba:street_a 1", "ba:itemname_gift", null, ""),
            new DeliveryStop("W1", "ba:street_b 2", "ba:itemname_gift", null, ""),
            new DeliveryStop("W1", "ba:street_c 3", "ba:itemname_gift", null, ""),
        };
        var supplied = Stage.Read(
            10,
            Stage.Stage2Cash,
            needs,
            stops,
            Array.Empty<WarehouseHold>(),
            Array.Empty<GarageSlot>());

        Assert.Equal(2, open);
        Assert.Equal(2, supplied);
        Assert.Equal(
            2,
            Stage.Read(
                10,
                Stage.Stage4Cash,
                needs,
                Array.Empty<DeliveryStop>(),
                Array.Empty<WarehouseHold>(),
                new[] { new GarageSlot("W1", 1, filled: false) }));
        Assert.Equal(
            4,
            Stage.Read(
                10,
                Stage.Stage4Cash,
                null,
                null,
                null,
                Array.Empty<GarageSlot>()));
    }
}
