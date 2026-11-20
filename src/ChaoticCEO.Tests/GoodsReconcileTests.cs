using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class GoodsReconcileTests
{
    private const string Gift = "ba:itemname_cheapgift";
    private const string Street = "ba:street_seventhavenue";
    private const string Hampton = "ba:street_seventhavenue";

    [Fact]
    public void Rack_stays_unbought_when_shelf_need_is_enough()
    {
        var enough = StorageShelf.Verdict(boxesNeeded: 16, placedShelves: 1);
        var blocked = StorageRack.Order(
            Street,
            22,
            target: 280,
            fixtureCap: 200,
            spendRoom: StorageRack.RackSpendRoom,
            pending: false,
            floorRoom: true,
            GameApi.Live(),
            enough,
            ownedRacks: 0,
            pendingRacks: 0);
        var open = StorageRack.Order(
            Street,
            22,
            target: 280,
            fixtureCap: 200,
            spendRoom: StorageRack.RackSpendRoom,
            pending: false,
            floorRoom: true,
            GameApi.Live());

        Assert.Equal("enough", enough);
        Assert.True(280 >= 200 * StorageRack.RackTriggerRatio);
        Assert.False(blocked.Ordered);
        Assert.True(open.Ordered);
    }

    [Fact]
    public void Owned_plus_pending_racks_stop_the_next_order()
    {
        var shortShelf = StorageShelf.Verdict(boxesNeeded: 17, placedShelves: 1);
        var capped = StorageRack.Order(
            Street,
            22,
            target: 280,
            fixtureCap: 200,
            spendRoom: 5000m,
            pending: false,
            floorRoom: true,
            GameApi.Live(),
            shortShelf,
            ownedRacks: 1,
            pendingRacks: 0);
        var inFlight = StorageRack.Order(
            Street,
            22,
            target: 280,
            fixtureCap: 200,
            spendRoom: 5000m,
            pending: false,
            floorRoom: true,
            GameApi.Live(),
            shortShelf,
            ownedRacks: 0,
            pendingRacks: 1);
        var first = StorageRack.Order(
            Street,
            22,
            target: 280,
            fixtureCap: 200,
            spendRoom: 5000m,
            pending: false,
            floorRoom: true,
            GameApi.Live(),
            shortShelf,
            ownedRacks: 0,
            pendingRacks: 0);

        Assert.Equal("short", shortShelf);
        Assert.Equal(1, GoodsReconcile.OwnedPendingCap);
        Assert.False(capped.Ordered);
        Assert.False(inFlight.Ordered);
        Assert.True(first.Ordered);
    }

    [Fact]
    public void On_hand_rises_within_n_days_after_a_large_replace()
    {
        var rose = GoodsReconcile.Rose(
            replace: true,
            need: 248,
            fromDay: 2,
            fromOnHand: 0,
            later: new[]
            {
                new HandDay(3, 248),
                new HandDay(7, 65),
            });
        var late = GoodsReconcile.Rose(
            replace: true,
            need: 248,
            fromDay: 2,
            fromOnHand: 0,
            later: new[] { new HandDay(7, 248) });
        var flat = GoodsReconcile.Rose(
            replace: true,
            need: 248,
            fromDay: 2,
            fromOnHand: 0,
            later: new[]
            {
                new HandDay(3, 0),
                new HandDay(4, 0),
            });
        var small = GoodsReconcile.Rose(
            replace: true,
            need: GoodsReconcile.LargeNeed - 1,
            fromDay: 2,
            fromOnHand: 0,
            later: new[] { new HandDay(3, 248) });
        var delivered = GoodsReconcile.GoodsLine(3, 3, Street, 15, Gift, 248, before: 0, after: 248);
        var missed = GoodsReconcile.GoodsLine(3, 3, Street, 15, Gift, 248, before: 0, after: 0);
        var otherDay = GoodsReconcile.GoodsLine(2, 3, Street, 15, Gift, 248, before: 0, after: 248);

        Assert.Equal(Wholesaler.DefaultGapDays, GoodsReconcile.RiseDays);
        Assert.True(rose);
        Assert.False(late);
        Assert.False(flat);
        Assert.False(small);
        Assert.StartsWith("goods.delivered version=" + LoadLog.Version, delivered);
        Assert.Contains("need=248", delivered);
        Assert.Contains("before=0", delivered);
        Assert.Contains("after=248", delivered);
        Assert.Contains("delta=248", delivered);
        Assert.StartsWith("goods.missed version=" + LoadLog.Version, missed);
        Assert.Contains("delta=0", missed);
        Assert.Equal("", otherDay);
    }

    [Fact]
    public void A_blank_1200_delivery_takes_the_furniture_street()
    {
        var open = new[]
        {
            new OpenFurniture(Hampton, 13, Street, 15, StorageRack.Item, 3, StorageRack.Price),
            new OpenFurniture(Hampton, 13, "ba:street_thirdstreet", 51, StorageShelf.Item, 3, StorageShelf.Price),
        };
        var cursor = 0;
        var firstStreet = "";
        var firstNumber = 0;
        var secondStreet = "";
        var secondNumber = 0;
        var keptStreet = "";
        var keptNumber = 0;
        GoodsReconcile.Attach("", 0, -1200m, open, ref cursor, out firstStreet, out firstNumber);
        GoodsReconcile.Attach("", 0, -1200m, open, ref cursor, out secondStreet, out secondNumber);
        GoodsReconcile.Attach(Street, 22, -1200m, open, ref cursor, out keptStreet, out keptNumber);
        var line = GoodsReconcile.OpenLine(3, open[0]);

        Assert.Equal(Street, firstStreet);
        Assert.Equal(15, firstNumber);
        Assert.Equal("ba:street_thirdstreet", secondStreet);
        Assert.Equal(51, secondNumber);
        Assert.Equal(Street, keptStreet);
        Assert.Equal(22, keptNumber);
        Assert.Equal(
            "furniture.open version=" + LoadLog.Version
                + " day=3 from=" + Hampton
                + " fromNumber=13 to=" + Street
                + " toNumber=15 item=" + StorageRack.Item
                + " deliver=3 price=" + StorageRack.Price.ToString(System.Globalization.CultureInfo.InvariantCulture),
            line);
    }

    [Fact]
    public void A_smaller_blank_move_leaves_the_furniture_street_for_the_rack()
    {
        var open = new[]
        {
            new OpenFurniture(Hampton, 13, Street, 15, StorageRack.Item, 3, StorageRack.Price),
        };
        var cursor = 0;
        GoodsReconcile.Attach("", 0, -40m, open, ref cursor, out var skippedStreet, out var skippedNumber);
        Assert.Equal(0, cursor);
        GoodsReconcile.Attach("", 0, -1200m, open, ref cursor, out var matchedStreet, out var matchedNumber);

        Assert.Equal("", skippedStreet);
        Assert.Equal(0, skippedNumber);
        Assert.Equal(Street, matchedStreet);
        Assert.Equal(15, matchedNumber);
    }

    [Fact]
    public void Eleven_blank_furniture_moves_each_receive_a_street()
    {
        var open = new OpenFurniture[11];
        var amounts = new decimal[11];
        var streets = new string[11];
        var numbers = new int[11];
        for (var i = 0; i < 11; i++)
        {
            open[i] = new OpenFurniture(Hampton, 13, Street, 15 + i, StorageRack.Item, 3, 1200);
            amounts[i] = -1200m;
            streets[i] = "";
            numbers[i] = 0;
        }

        var cursor = 0;
        for (var i = 0; i < 11; i++)
        {
            GoodsReconcile.Attach(streets[i], numbers[i], amounts[i], open, ref cursor, out streets[i], out numbers[i]);
        }

        Assert.Equal(11, cursor);
        for (var i = 0; i < 11; i++)
        {
            Assert.Equal(Street, streets[i]);
            Assert.Equal(15 + i, numbers[i]);
        }
    }

    [Fact]
    public void Same_day_replace_waits_one_delivery_cycle()
    {
        var first = GoodsReconcile.NextDelivery(
            replace: true,
            currentDay: 5,
            coverNext: 5,
            scheduledDay: 9,
            lastReplaceDay: 0,
            rose: false);
        var held = GoodsReconcile.NextDelivery(
            replace: true,
            currentDay: 5,
            coverNext: 5,
            scheduledDay: 9,
            lastReplaceDay: 5,
            rose: false);
        var after = GoodsReconcile.NextDelivery(
            replace: true,
            currentDay: 7,
            coverNext: 7,
            scheduledDay: 9,
            lastReplaceDay: 5,
            rose: false);
        var landed = GoodsReconcile.NextDelivery(
            replace: true,
            currentDay: 6,
            coverNext: 6,
            scheduledDay: 9,
            lastReplaceDay: 5,
            rose: true);

        Assert.Equal(5, first);
        Assert.Equal(9, held);
        Assert.Equal(7, after);
        Assert.Equal(6, landed);
    }

    [Fact]
    public void Chain_cover_need_delivery_land_rack_gate()
    {
        // Chain: cover need → delivery land → rack gate
        var days = new[]
        {
            new CoverSale(3, 40, open: true),
            new CoverSale(4, 40, open: true),
        };
        var plan = Wholesaler.Cover(
            Gift,
            currentDay: 5,
            nextDeliveryDay: 9,
            days,
            onHand: 0,
            fixtureCap: 200,
            stands: 2);
        var land = GoodsReconcile.Land(plan, day: 6, deliveryDay: 6, Street, 22, before: 0, after: 248);
        var rose = GoodsReconcile.Rose(
            plan.Replace,
            plan.Need,
            fromDay: 5,
            fromOnHand: 0,
            later: new[]
            {
                new HandDay(6, 248),
                new HandDay(7, 65),
            });
        var verdict = StorageShelf.Verdict(boxesNeeded: 16, placedShelves: 1);
        var pick = StorageRack.Order(
            Street,
            22,
            land.Target,
            plan.FixtureCap,
            spendRoom: StorageRack.RackSpendRoom,
            pending: false,
            floorRoom: true,
            GameApi.Live(),
            verdict,
            ownedRacks: 0,
            pendingRacks: 0);

        Assert.True(plan.Replace);
        Assert.Equal(5, plan.NextDay);
        Assert.True(plan.Need >= GoodsReconcile.LargeNeed);
        Assert.True(land.Target >= plan.FixtureCap * StorageRack.RackTriggerRatio);
        Assert.Equal(plan.Need, land.Need);
        Assert.StartsWith("goods.delivered version=" + LoadLog.Version, land.Line);
        Assert.Contains("need=" + plan.Need.ToString(System.Globalization.CultureInfo.InvariantCulture), land.Line);
        Assert.Contains("delta=248", land.Line);
        Assert.True(rose);
        Assert.Equal("enough", verdict);
        Assert.False(pick.Ordered);
    }

    [Fact]
    public void Chain_cover_need_hold_delivery_goods_land()
    {
        // Chain: cover need → delivery hold → goods land
        var days = new[]
        {
            new CoverSale(3, 40, open: true),
            new CoverSale(4, 40, open: true),
        };
        var plan = Wholesaler.Cover(
            Gift,
            currentDay: 5,
            nextDeliveryDay: 9,
            days,
            onHand: 0,
            fixtureCap: 200,
            stands: 2);
        var kept = GoodsReconcile.NextDelivery(
            plan.Replace,
            currentDay: 5,
            plan.NextDay,
            scheduledDay: 9,
            lastReplaceDay: 5,
            rose: false);
        var land = GoodsReconcile.Land(plan, day: kept, deliveryDay: kept, Street, 22, before: 0, after: 248);

        Assert.True(plan.Replace);
        Assert.Equal(5, plan.NextDay);
        Assert.Equal(9, kept);
        Assert.NotEqual(plan.NextDay, kept);
        Assert.StartsWith("goods.delivered version=" + LoadLog.Version, land.Line);
        Assert.Contains("need=" + plan.Need.ToString(System.Globalization.CultureInfo.InvariantCulture), land.Line);
        Assert.Contains("day=9", land.Line);
    }

    [Fact]
    public void Chain_cover_need_on_hand_rose_releases_the_hold()
    {
        // Chain: cover need → on-hand rose → delivery release
        var days = new[]
        {
            new CoverSale(3, 40, open: true),
            new CoverSale(4, 40, open: true),
        };
        var plan = Wholesaler.Cover(
            Gift,
            currentDay: 5,
            nextDeliveryDay: 9,
            days,
            onHand: 0,
            fixtureCap: 200,
            stands: 2);
        var rose = GoodsReconcile.Rose(
            plan.Replace,
            plan.Need,
            fromDay: 5,
            fromOnHand: plan.OnHand,
            later: new[] { new HandDay(6, 248) });
        var released = GoodsReconcile.NextDelivery(
            plan.Replace,
            currentDay: 6,
            plan.NextDay,
            scheduledDay: 9,
            lastReplaceDay: 5,
            rose);

        Assert.True(plan.Replace);
        Assert.True(plan.Need >= GoodsReconcile.LargeNeed);
        Assert.Equal(0, plan.OnHand);
        Assert.True(rose);
        Assert.Equal(plan.NextDay, released);
        Assert.NotEqual(9, released);
    }
}
