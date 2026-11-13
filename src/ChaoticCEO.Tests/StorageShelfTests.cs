using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class StorageShelfTests
{
    private const string Umbrella = "ba:itemname_umbrella";
    private const string Street = "ba:street_seventhavenue";

    // Storage room x 995..1000, z 53..56. Wall x 1000..1001. Bathroom x 1001..1005.
    // A storage shelf is 2 m wide and 1 m deep; at yaw 90 it covers 1 m of x and 2 m of z.
    private static ShelfFloor Shop(float roomZLow = 53f, float roomZHigh = 56f)
    {
        var cells = new List<ShelfCell>();
        AddCells(cells, 995f, 1000f, roomZLow, roomZHigh, free: true);
        AddCells(cells, 1000f, 1001f, roomZLow, roomZHigh, free: false);
        AddCells(cells, 1001f, 1005f, roomZLow, roomZHigh, free: true);
        return new ShelfFloor(0.25f, cells);
    }

    private static void AddCells(List<ShelfCell> cells, float xLow, float xHigh, float zLow, float zHigh, bool free)
    {
        for (var x = xLow + 0.125f; x < xHigh; x += 0.25f)
        {
            for (var z = zLow + 0.125f; z < zHigh; z += 0.25f)
            {
                cells.Add(new ShelfCell(x, z, free));
            }
        }
    }

    private static ShelfSpot Rack(float x, float z = 54.5f, float yaw = 90f)
    {
        return new ShelfSpot(x, z, yaw, width: 2f, depth: 1f);
    }

    private static ShelfSeat OpenSeat()
    {
        return StorageShelf.Seat(Shop(), new[] { Rack(997f), Rack(998f) }, Array.Empty<ShelfSpot>());
    }

    [Fact]
    public void Order_buys_one_storage_shelf_when_the_fixture_is_full()
    {
        var pick = StorageShelf.Order(Street, 22, freeSpace: 0, cash: 75900m, pending: false, inCargo: false, seat: true, GameApi.Live());

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
        var room = StorageShelf.Order(Street, 22, freeSpace: 23, cash: 75900m, pending: false, inCargo: false, seat: true, GameApi.Live());
        var broke = StorageShelf.Order(Street, 22, freeSpace: 0, cash: 1199m, pending: false, inCargo: false, seat: true, GameApi.Live());
        var waiting = StorageShelf.Order(Street, 22, freeSpace: 0, cash: 75900m, pending: true, inCargo: false, seat: true, GameApi.Live());
        var exact = StorageShelf.Order(Street, 22, freeSpace: 0, cash: 1200m, pending: false, inCargo: false, seat: true, GameApi.Live());

        Assert.False(room.Ordered);
        Assert.False(broke.Ordered);
        Assert.False(waiting.Ordered);
        Assert.True(exact.Ordered);
    }

    [Fact]
    public void Shelf_an_unplaced_shelf_does_not_order_another()
    {
        var pick = StorageShelf.Order(Street, 22, freeSpace: 0, cash: 292000m, pending: false, inCargo: true, seat: true, GameApi.Live());

        Assert.False(pick.Ordered);
    }

    [Fact]
    public void Shelf_a_full_storage_room_orders_nothing()
    {
        var pick = StorageShelf.Order(Street, 22, freeSpace: 0, cash: 292000m, pending: false, inCargo: false, seat: false, GameApi.Live());

        Assert.False(pick.Ordered);
    }

    [Fact]
    public void Seat_a_new_shelf_touches_the_end_of_the_storage_row()
    {
        var seat = StorageShelf.Seat(Shop(), new[] { Rack(997f), Rack(998f) }, Array.Empty<ShelfSpot>());

        Assert.True(seat.Found);
        Assert.Equal(999f, seat.X, 3);
        Assert.Equal(54.5f, seat.Z, 3);
        Assert.Equal(90f, seat.Yaw, 3);
    }

    [Fact]
    public void Seat_never_crosses_the_wall_into_the_bathroom()
    {
        var seat = StorageShelf.Seat(Shop(), new[] { Rack(997f), Rack(998f), Rack(999f) }, Array.Empty<ShelfSpot>());

        Assert.True(seat.Found);
        Assert.Equal(996f, seat.X, 3);
        Assert.Equal(54.5f, seat.Z, 3);
    }

    [Fact]
    public void Seat_a_full_storage_room_has_no_seat()
    {
        var seat = StorageShelf.Seat(
            Shop(),
            new[] { Rack(996f), Rack(997f), Rack(998f), Rack(999f) },
            Array.Empty<ShelfSpot>());

        Assert.False(seat.Found);
        Assert.Equal("full", seat.Reason);
    }

    [Fact]
    public void Seat_never_opens_a_second_rank_beside_a_full_row()
    {
        var seat = StorageShelf.Seat(
            Shop(roomZLow: 50f, roomZHigh: 59f),
            new[] { Rack(996f), Rack(997f), Rack(998f), Rack(999f) },
            Array.Empty<ShelfSpot>());

        Assert.False(seat.Found);
    }

    [Fact]
    public void Seat_fills_a_gap_in_the_row_before_the_ends()
    {
        var seat = StorageShelf.Seat(Shop(), new[] { Rack(996f), Rack(999f) }, Array.Empty<ShelfSpot>());

        Assert.True(seat.Found);
        Assert.Equal(997f, seat.X, 3);
        Assert.Equal(54.5f, seat.Z, 3);
    }

    [Fact]
    public void Seat_skips_a_spot_another_item_already_stands_on()
    {
        var box = new ShelfSpot(999f, 54.5f, 0f, width: 0.6f, depth: 0.6f);

        var seat = StorageShelf.Seat(Shop(), new[] { Rack(997f), Rack(998f) }, new[] { box });

        Assert.True(seat.Found);
        Assert.Equal(996f, seat.X, 3);
    }

    [Fact]
    public void Seat_ignores_an_item_that_only_touches_the_seat_edge()
    {
        var flush = new ShelfSpot(999.75f, 54.5f, 0f, width: 0.5f, depth: 0.5f);

        var seat = StorageShelf.Seat(Shop(), new[] { Rack(997f), Rack(998f) }, new[] { flush });

        Assert.True(seat.Found);
        Assert.Equal(999f, seat.X, 3);
    }

    [Fact]
    public void Seat_a_row_off_the_grid_has_no_seat()
    {
        var seat = StorageShelf.Seat(Shop(), new[] { Rack(10f, 10f), Rack(11f, 10f) }, Array.Empty<ShelfSpot>());

        Assert.False(seat.Found);
        Assert.Equal("offgrid", seat.Reason);
    }

    [Fact]
    public void Seat_no_storage_shelf_has_no_seat()
    {
        var seat = StorageShelf.Seat(Shop(), Array.Empty<ShelfSpot>(), Array.Empty<ShelfSpot>());

        Assert.False(seat.Found);
        Assert.Equal("norow", seat.Reason);
    }

    [Fact]
    public void Seat_a_single_shelf_extends_along_its_width()
    {
        var seat = StorageShelf.Seat(
            Shop(roomZLow: 50f, roomZHigh: 59f),
            new[] { Rack(997f) },
            Array.Empty<ShelfSpot>());

        Assert.True(seat.Found);
        Assert.Equal(997f, seat.X, 3);
        Assert.Equal(52.5f, seat.Z, 3);
    }

    [Fact]
    public void Seat_a_lone_shelf_boxed_in_along_its_width_extends_along_its_facing()
    {
        // Harvest: day 1571–1572 on 1.17.8 logged shelf.held reason=full with one storage shelf at (997, 54.5) yaw 90.
        var handTruck = new ShelfSpot(997.03f, 52.33f, 0f, width: 1f, depth: 1f);

        var seat = StorageShelf.Seat(Shop(), new[] { Rack(997f) }, new[] { handTruck });

        Assert.True(seat.Found);
        Assert.Equal(998f, seat.X, 3);
        Assert.Equal(54.5f, seat.Z, 3);
        Assert.Equal(90f, seat.Yaw, 3);
    }

    [Fact]
    public void Chain_day_roll_full_fixture_lone_shelf_places_the_cargo_shelf_in_front()
    {
        // Chain: day roll → full fixture → shelf in cargo → lone storage shelf → seat along its facing → placed
        var day = Morning.CoverDay(null, autopilotOn: true, day: 1573, coveredDay: 1572);
        var plan = FullFixture(day);
        var handTruck = new ShelfSpot(997.03f, 52.33f, 0f, width: 1f, depth: 1f);
        var seat = StorageShelf.Seat(Shop(), new[] { Rack(997f) }, new[] { handTruck });

        var attend = StorageShelf.Attend(Street, 22, plan.Need == 0 && plan.OnHand > 0, inCargo: true, pending: false, cash: 292000m, seat, GameApi.Live());

        Assert.Equal(ShelfAction.Place, attend.Action);
        Assert.False(attend.Pick.Ordered);
        Assert.Equal(998f, attend.Seat.X, 3);
        Assert.Equal(54.5f, attend.Seat.Z, 3);
    }

    [Fact]
    public void Seat_follows_the_largest_touching_row_not_a_stray_shelf()
    {
        var stray = new ShelfSpot(1003f, 54.5f, 0f, width: 2f, depth: 1f);

        var seat = StorageShelf.Seat(Shop(), new[] { stray, Rack(997f), Rack(998f) }, Array.Empty<ShelfSpot>());

        Assert.True(seat.Found);
        Assert.Equal(999f, seat.X, 3);
        Assert.Equal(90f, seat.Yaw, 3);
    }

    [Fact]
    public void Seat_keeps_the_collider_offset_of_the_row()
    {
        var shifted = new[]
        {
            new ShelfSpot(997f, 54.3f, 90f, width: 2f, depth: 1f, offsetX: 0.2f, offsetZ: 0f),
            new ShelfSpot(998f, 54.3f, 90f, width: 2f, depth: 1f, offsetX: 0.2f, offsetZ: 0f),
        };

        var seat = StorageShelf.Seat(Shop(), shifted, Array.Empty<ShelfSpot>());

        Assert.True(seat.Found);
        Assert.Equal(999f, seat.X, 3);
        Assert.Equal(54.3f, seat.Z, 3);
    }

    [Fact]
    public void Attend_places_the_shelf_in_cargo_on_the_storage_seat_and_orders_nothing()
    {
        var seat = OpenSeat();

        var attend = StorageShelf.Attend(Street, 22, full: true, inCargo: true, pending: false, cash: 292000m, seat, GameApi.Live());

        Assert.Equal(ShelfAction.Place, attend.Action);
        Assert.False(attend.Pick.Ordered);
        Assert.Equal(999f, attend.Seat.X, 3);
        Assert.Equal("BuildingRegistration.AddItemInstanceToBuilding", attend.Call);
    }

    [Fact]
    public void Attend_holds_the_shelf_in_cargo_when_the_storage_room_is_full()
    {
        var attend = StorageShelf.Attend(Street, 22, full: true, inCargo: true, pending: false, cash: 292000m, ShelfSeat.None("full"), GameApi.Live());

        Assert.Equal(ShelfAction.Hold, attend.Action);
        Assert.False(attend.Pick.Ordered);
        Assert.Equal("full", attend.Reason);
    }

    [Fact]
    public void Attend_orders_only_when_the_fixture_is_full_and_a_seat_is_open()
    {
        var seat = OpenSeat();

        var order = StorageShelf.Attend(Street, 22, full: true, inCargo: false, pending: false, cash: 292000m, seat, GameApi.Live());
        var room = StorageShelf.Attend(Street, 22, full: false, inCargo: false, pending: false, cash: 292000m, seat, GameApi.Live());
        var noSeat = StorageShelf.Attend(Street, 22, full: true, inCargo: false, pending: false, cash: 292000m, ShelfSeat.None("full"), GameApi.Live());

        Assert.Equal(ShelfAction.Order, order.Action);
        Assert.True(order.Pick.Ordered);
        Assert.Equal(ShelfAction.None, room.Action);
        Assert.Equal(ShelfAction.Hold, noSeat.Action);
        Assert.Equal("full", noSeat.Reason);
        Assert.False(noSeat.Pick.Ordered);
    }

    [Fact]
    public void Place_line_names_the_seat_and_the_call()
    {
        var attend = StorageShelf.Attend(Street, 22, full: true, inCargo: true, pending: false, cash: 292000m, OpenSeat(), GameApi.Live());

        var line = StorageShelf.PlaceLine(attend, 1571);

        Assert.Equal(
            "shelf.placed version=" + LoadLog.Version
                + " day=1571 street=ba:street_seventhavenue number=22 item=ba:itemname_storageshelf"
                + " x=999.00 z=54.50 yaw=90 call=BuildingRegistration.AddItemInstanceToBuilding",
            line);
    }

    [Fact]
    public void Hold_line_names_the_reason_and_writes_nothing_for_an_order()
    {
        var hold = StorageShelf.Attend(Street, 22, full: true, inCargo: true, pending: false, cash: 292000m, ShelfSeat.None("offgrid"), GameApi.Live());
        var order = StorageShelf.Attend(Street, 22, full: true, inCargo: false, pending: false, cash: 292000m, OpenSeat(), GameApi.Live());

        Assert.Equal(
            "shelf.held version=" + LoadLog.Version
                + " day=1571 street=ba:street_seventhavenue number=22 item=ba:itemname_storageshelf inCargo=true reason=offgrid",
            StorageShelf.HoldLine(hold, 1571));
        Assert.Equal("", StorageShelf.HoldLine(order, 1571));
        Assert.Equal("", StorageShelf.PlaceLine(order, 1571));
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
        var seat = StorageShelf.Seat(Shop(), new[] { Rack(997f), Rack(998f) }, Array.Empty<ShelfSpot>());
        var pick = StorageShelf.Order(Street, 22, plan.FixtureCap, cash: 75900m, pending: false, inCargo: false, seat.Found, GameApi.Live());

        Assert.Equal(0, space);
        Assert.Equal(0, plan.Need);
        Assert.Equal(space, plan.FixtureCap);
        Assert.True(pick.Ordered);
        Assert.Equal(StorageShelf.Item, pick.Item);
    }

    private static CoverPlan FullFixture(int day)
    {
        var days = new[]
        {
            new CoverSale(day - 2, 16, open: true),
            new CoverSale(day - 1, 20, open: true),
        };
        var onHand = 40;
        var capacity = Wholesaler.FixtureCapacity(
            Umbrella,
            new[] { new ShelfFixture("ba:itemname_productpanel", Umbrella, 30) });
        return Wholesaler.Cover(Umbrella, day, day + 2, days, onHand, capacity);
    }

    [Fact]
    public void Chain_day_roll_full_fixture_shelf_in_cargo_orders_nothing()
    {
        // Chain: day roll → full fixture → shelf in cargo → no order
        var day = Morning.CoverDay(null, autopilotOn: true, day: 1571, coveredDay: 1570);
        var plan = FullFixture(day);
        var full = plan.Need == 0 && plan.OnHand > 0;
        var seat = StorageShelf.Seat(Shop(), new[] { Rack(997f), Rack(998f) }, Array.Empty<ShelfSpot>());

        var attend = StorageShelf.Attend(Street, 22, full, inCargo: true, pending: false, cash: 292000m, seat, GameApi.Live());

        Assert.Equal(1571, day);
        Assert.True(full);
        Assert.False(attend.Pick.Ordered);
        Assert.Equal("", StorageShelf.Line(attend.Pick, day));
        Assert.Equal(ShelfAction.Place, attend.Action);
    }

    [Fact]
    public void Chain_day_roll_full_fixture_full_storage_room_holds_and_orders_nothing()
    {
        // Chain: day roll → full fixture → storage room full → hold, no order
        var day = Morning.CoverDay(null, autopilotOn: true, day: 1571, coveredDay: 1570);
        var plan = FullFixture(day);
        var full = plan.Need == 0 && plan.OnHand > 0;
        var row = new[] { Rack(996f), Rack(997f), Rack(998f), Rack(999f) };
        var seat = StorageShelf.Seat(Shop(), row, Array.Empty<ShelfSpot>());

        var empty = StorageShelf.Attend(Street, 22, full, inCargo: false, pending: false, cash: 292000m, seat, GameApi.Live());
        var held = StorageShelf.Attend(Street, 22, full, inCargo: true, pending: false, cash: 292000m, seat, GameApi.Live());

        Assert.True(full);
        Assert.Equal(ShelfAction.Hold, empty.Action);
        Assert.False(empty.Pick.Ordered);
        Assert.Equal(ShelfAction.Hold, held.Action);
        Assert.Equal("full", held.Reason);
    }

    [Fact]
    public void Chain_day_roll_full_fixture_order_place_then_the_next_seat_touches_it()
    {
        // Chain: day roll → full fixture → order → shelf in cargo → storage seat → placed → next morning seat
        var row = new List<ShelfSpot> { Rack(997f), Rack(998f) };

        var orderDay = Morning.CoverDay(null, autopilotOn: true, day: 1571, coveredDay: 1570);
        var orderPlan = FullFixture(orderDay);
        var ordered = StorageShelf.Attend(
            Street, 22, orderPlan.Need == 0 && orderPlan.OnHand > 0, inCargo: false, pending: false, cash: 292000m,
            StorageShelf.Seat(Shop(), row, Array.Empty<ShelfSpot>()), GameApi.Live());

        var placeDay = Morning.CoverDay(null, autopilotOn: true, day: 1572, coveredDay: orderDay);
        var placePlan = FullFixture(placeDay);
        var placed = StorageShelf.Attend(
            Street, 22, placePlan.Need == 0 && placePlan.OnHand > 0, inCargo: true, pending: false, cash: 290800m,
            StorageShelf.Seat(Shop(), row, Array.Empty<ShelfSpot>()), GameApi.Live());
        row.Add(Rack(placed.Seat.X, placed.Seat.Z, placed.Seat.Yaw));

        var nextDay = Morning.CoverDay(null, autopilotOn: true, day: 1573, coveredDay: placeDay);
        var next = StorageShelf.Seat(Shop(), row, Array.Empty<ShelfSpot>());

        Assert.Equal(ShelfAction.Order, ordered.Action);
        Assert.Equal(ShelfAction.Place, placed.Action);
        Assert.Equal(999f, placed.Seat.X, 3);
        Assert.Equal(1573, nextDay);
        Assert.True(next.Found);
        Assert.Equal(996f, next.X, 3);
        Assert.Equal(54.5f, next.Z, 3);
    }
}
