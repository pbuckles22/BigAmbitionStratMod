using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class StorageShelfTests
{
    private const string Umbrella = "ba:itemname_umbrella";
    private const string Street = "ba:street_seventhavenue";

    [Fact]
    public void Order_buys_one_storage_shelf_when_the_fixture_is_full()
    {
        var pick = StorageShelf.Order(Street, 22, freeSpace: 0, cash: 75900m, pending: false, GameApi.Live());

        Assert.True(pick.Ordered);
        Assert.Equal(StorageShelf.Item, pick.Item);
        Assert.Equal(1200, pick.Price);
        Assert.Equal(1, pick.Amount);
        Assert.Equal(Street, pick.Street);
        Assert.Equal(22, pick.Number);
        Assert.Equal(Street, pick.FromStreet);
        Assert.Equal(13, pick.FromNumber);
        Assert.Equal("Entities.FurnitureDeliveryContract", pick.Call);
    }

    [Fact]
    public void Order_leaves_the_store_alone_when_the_shelf_still_has_room_or_the_order_cannot_be_paid()
    {
        var room = StorageShelf.Order(Street, 22, freeSpace: 23, cash: 75900m, pending: false, GameApi.Live());
        var broke = StorageShelf.Order(Street, 22, freeSpace: 0, cash: 1199m, pending: false, GameApi.Live());
        var waiting = StorageShelf.Order(Street, 22, freeSpace: 0, cash: 75900m, pending: true, GameApi.Live());
        var exact = StorageShelf.Order(Street, 22, freeSpace: 0, cash: 1200m, pending: false, GameApi.Live());

        Assert.False(room.Ordered);
        Assert.False(broke.Ordered);
        Assert.False(waiting.Ordered);
        Assert.True(exact.Ordered);
    }

    [Fact]
    public void Chain_on_hand_free_space_line_orders_a_shelf_when_the_fixture_is_full()
    {
        // Chain: on hand → free space → line → shelf
        var days = new[]
        {
            new CoverSale(3, 16, open: true),
            new CoverSale(4, 20, open: true),
        };
        var onHand = 40;
        var capacity = Wholesaler.FixtureCapacity(
            Umbrella,
            new[] { new ShelfFixture("ba:itemname_productpanel", Umbrella, 30) });
        var space = Wholesaler.FreeSpace(onHand, capacity);
        var plan = Wholesaler.Cover(
            Umbrella,
            currentDay: 5,
            nextDeliveryDay: 7,
            days,
            onHand,
            capacity);
        var pick = StorageShelf.Order(Street, 22, plan.FixtureCap, cash: 75900m, pending: false, GameApi.Live());

        Assert.Equal(0, space);
        Assert.Equal(0, plan.Need);
        Assert.Equal(space, plan.FixtureCap);
        Assert.True(pick.Ordered);
        Assert.Equal(StorageShelf.Item, pick.Item);
    }
}
