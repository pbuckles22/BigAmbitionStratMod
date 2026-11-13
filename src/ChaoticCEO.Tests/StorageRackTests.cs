using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class StorageRackTests
{
    private const string Umbrella = "ba:itemname_umbrella";
    private const string Street = "ba:street_seventhavenue";

    [Fact]
    public void Rack_orders_when_spend_room_and_target_reach_eighty_five_percent_of_fixture_space()
    {
        var pick = StorageRack.Order(
            Street,
            22,
            target: 85,
            fixtureCap: 100,
            spendRoom: StorageRack.RackSpendRoom,
            pending: false,
            floorRoom: true,
            GameApi.Live());

        Assert.True(pick.Ordered);
        Assert.Equal(StorageRack.Item, pick.Item);
        Assert.Equal(1, pick.Amount);
        Assert.Equal(StorageRack.Price, pick.Price);
        Assert.Equal(Street, pick.Street);
        Assert.Equal(22, pick.Number);
        Assert.Equal(StorageRack.HamptonStreet, pick.FromStreet);
        Assert.Equal(StorageRack.HamptonNumber, pick.FromNumber);
        Assert.Equal("Entities.FurnitureDeliveryContract", pick.Call);
        Assert.Equal(StorageRack.UnitCap, 200);
        Assert.Equal(0.85m, StorageRack.RackTriggerRatio);
        Assert.Equal(2500m, StorageRack.RackSpendRoom);
    }

    [Fact]
    public void Rack_leaves_the_store_alone_when_spend_room_target_or_floor_block_it()
    {
        var shortSpend = StorageRack.Order(
            Street, 22, target: 85, fixtureCap: 100, spendRoom: 2499m, pending: false, floorRoom: true, GameApi.Live());
        var underRatio = StorageRack.Order(
            Street, 22, target: 84, fixtureCap: 100, spendRoom: 2500m, pending: false, floorRoom: true, GameApi.Live());
        var pending = StorageRack.Order(
            Street, 22, target: 85, fixtureCap: 100, spendRoom: 2500m, pending: true, floorRoom: true, GameApi.Live());
        var noFloor = StorageRack.Order(
            Street, 22, target: 85, fixtureCap: 100, spendRoom: 2500m, pending: false, floorRoom: false, GameApi.Live());
        var blankCap = StorageRack.Order(
            Street, 22, target: 85, fixtureCap: 0, spendRoom: 2500m, pending: false, floorRoom: true, GameApi.Live());
        var noCall = StorageRack.Order(
            Street, 22, target: 85, fixtureCap: 100, spendRoom: 2500m, pending: false, floorRoom: true, api: null);

        Assert.False(shortSpend.Ordered);
        Assert.False(underRatio.Ordered);
        Assert.False(pending.Ordered);
        Assert.False(noFloor.Ordered);
        Assert.False(blankCap.Ordered);
        Assert.False(noCall.Ordered);
    }

    [Fact]
    public void Rack_records_one_ordered_line()
    {
        var pick = StorageRack.Order(
            Street, 22, target: 90, fixtureCap: 100, spendRoom: 5000m, pending: false, floorRoom: true, GameApi.Live());
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ceo-rack-" + Guid.NewGuid().ToString("N")));
        try
        {
            var written = StorageRack.Record(dir.FullName, pick, 1575);
            var again = StorageRack.Record(dir.FullName, pick, 1575);

            Assert.Equal(
                "rack.ordered version=" + LoadLog.Version
                    + " day=1575 street=" + Street
                    + " number=22 item=" + StorageRack.Item
                    + " amount=1 price=" + StorageRack.Price.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " from=" + StorageRack.HamptonStreet
                    + " fromNumber=" + StorageRack.HamptonNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " call=" + pick.Call,
                written);
            Assert.Equal(written, again);
            Assert.Equal(1, File.ReadAllLines(LoadLog.CeoLogPath(dir.FullName)).Count(line => line.StartsWith("rack.ordered ")));
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Rack_skips_a_blank_store_address()
    {
        var blankStreet = StorageRack.Order(
            "", 22, target: 85, fixtureCap: 100, spendRoom: 2500m, pending: false, floorRoom: true, GameApi.Live());
        var blankNumber = StorageRack.Order(
            Street, 0, target: 85, fixtureCap: 100, spendRoom: 2500m, pending: false, floorRoom: true, GameApi.Live());

        Assert.False(blankStreet.Ordered);
        Assert.False(blankNumber.Ordered);
    }

    [Fact]
    public void Chain_spend_room_target_vs_cap_orders_a_rack()
    {
        // Chain: spend room → target vs cap → rack
        var days = new[]
        {
            new CoverSale(3, 50, open: true),
            new CoverSale(4, 50, open: true),
        };
        var fixtures = new[] { new ShelfFixture("ba:itemname_productpanel", Umbrella, 100) };
        var capacity = Wholesaler.FixtureCapacity(Umbrella, fixtures);
        var plan = Wholesaler.Cover(
            Umbrella,
            currentDay: 5,
            nextDeliveryDay: 7,
            days,
            onHand: 20,
            fixtureCap: capacity,
            stands: 0);
        var spendRoom = Money.SpendRoom(cash: 70_000m, dailyCost: 40m);
        var pick = StorageRack.Order(
            Street,
            22,
            plan.Target,
            capacity,
            spendRoom,
            pending: false,
            floorRoom: true,
            GameApi.Live());

        Assert.Equal(100, capacity);
        Assert.Equal(17_430m, spendRoom);
        Assert.True(plan.Target >= capacity * StorageRack.RackTriggerRatio);
        Assert.True(spendRoom >= StorageRack.RackSpendRoom);
        Assert.True(pick.Ordered);
        Assert.Equal(StorageRack.Item, pick.Item);
    }

    [Fact]
    public void Chain_target_vs_cap_orders_a_rack()
    {
        // Chain: target vs cap → rack
        var days = new[]
        {
            new CoverSale(3, 50, open: true),
            new CoverSale(4, 50, open: true),
        };
        var fixtures = new[] { new ShelfFixture("ba:itemname_productpanel", Umbrella, 100) };
        var capacity = Wholesaler.FixtureCapacity(Umbrella, fixtures);
        var plan = Wholesaler.Cover(
            Umbrella,
            currentDay: 5,
            nextDeliveryDay: 7,
            days,
            onHand: 20,
            fixtureCap: capacity,
            stands: 0);
        var spendRoom = Money.SpendRoom(cash: 70_000m, dailyCost: 40m);
        var pick = StorageRack.Order(
            Street,
            22,
            plan.Target,
            capacity,
            spendRoom,
            pending: false,
            floorRoom: true,
            GameApi.Live());

        Assert.Equal(100, capacity);
        Assert.True(plan.Target >= capacity * StorageRack.RackTriggerRatio);
        Assert.True(spendRoom >= StorageRack.RackSpendRoom);
        Assert.True(pick.Ordered);
        Assert.Equal(StorageRack.Item, pick.Item);
    }

    [Fact]
    public void Chain_target_vs_cap_skips_a_rack_when_the_target_stays_under_the_ratio()
    {
        // Chain: target vs cap → rack
        var days = new[]
        {
            new CoverSale(3, 10, open: true),
            new CoverSale(4, 10, open: true),
        };
        var fixtures = new[] { new ShelfFixture("ba:itemname_productpanel", Umbrella, 100) };
        var capacity = Wholesaler.FixtureCapacity(Umbrella, fixtures);
        var plan = Wholesaler.Cover(
            Umbrella,
            currentDay: 5,
            nextDeliveryDay: 7,
            days,
            onHand: 20,
            fixtureCap: capacity,
            stands: 0);
        var spendRoom = Money.SpendRoom(cash: 70_000m, dailyCost: 40m);
        var pick = StorageRack.Order(
            Street,
            22,
            plan.Target,
            capacity,
            spendRoom,
            pending: false,
            floorRoom: true,
            GameApi.Live());

        Assert.Equal(100, capacity);
        Assert.True(plan.Target < capacity * StorageRack.RackTriggerRatio);
        Assert.True(spendRoom >= StorageRack.RackSpendRoom);
        Assert.False(pick.Ordered);
    }
}
