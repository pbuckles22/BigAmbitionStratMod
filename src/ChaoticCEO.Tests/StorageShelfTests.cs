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
        // Wall shelf + facing partner across a 1 m aisle — next seat is the back-to-back half.
        return StorageShelf.Seat(Shop(), new[] { Rack(995.5f), Rack(997.5f, yaw: 270f) }, Array.Empty<ShelfSpot>());
    }

    [Fact]
    public void BoxedInHolder_counts_nested_cargo_and_skips_placed_shelves_as_holders()
    {
        var nested = new[] { (StorageShelf.Item, 3) };
        Assert.Equal(3, StorageShelf.BoxedInHolder("ba:itemname_closedcardboardbox", cargoName: null, cargoAmount: 0, nested));
        Assert.Equal(2, StorageShelf.BoxedInHolder("ba:itemname_handtruck", StorageShelf.Item, 2, nested: null));
        Assert.Equal(0, StorageShelf.BoxedInHolder(StorageShelf.Item, StorageShelf.Item, 5, nested));
    }

    [Fact]
    public void Attend_holds_with_hold_spike_and_orders_nothing()
    {
        var attend = StorageShelf.Attend(
            Street, 22, shelvesNeeded: 3, inCargo: true, pending: false, cash: 292000m,
            ShelfSeat.None("hold-spike"), GameApi.Live());
        var buy = StorageShelf.Attend(
            Street, 22, shelvesNeeded: 3, inCargo: false, pending: false, cash: 292000m,
            ShelfSeat.None("hold-spike"), GameApi.Live());

        Assert.Equal(ShelfAction.Hold, attend.Action);
        Assert.Equal("hold-spike", attend.Reason);
        Assert.False(attend.Pick.Ordered);
        Assert.Equal(ShelfAction.Hold, buy.Action);
        Assert.False(buy.Pick.Ordered);
    }

    [Fact]
    public void SpacesNeeded_is_leftover_boxes_plus_boxes_on_the_truck()
    {
        Assert.Equal(230, StorageShelf.SpacesNeeded(onHand: 150, inbound: 80));
        Assert.Equal(80, StorageShelf.SpacesNeeded(onHand: 0, inbound: 80));
        Assert.Equal(0, StorageShelf.SpacesNeeded(onHand: -1, inbound: -5));
    }

    [Fact]
    public void ShelvesToOrder_buys_only_the_shortfall_and_counts_boxed_shelves_first()
    {
        // Retail units vs UnitCapacity (100), never Boxes (16 slots).
        // 8 shelves × 100 = 800 placed. Leftover+inbound 700 → buy nothing.
        Assert.Equal(0, StorageShelf.ShelvesToOrder(spacesNeeded: 700, placedCapacity: 800, boxedShelves: 0));
        // Display full (100) + no storage, need 250 more → ceil(250/100) = 3.
        Assert.Equal(3, StorageShelf.ShelvesToOrder(spacesNeeded: 350, placedCapacity: 100, boxedShelves: 0));
        // Same shortfall but one boxed/pending shelf already covers 100 → buy 2.
        Assert.Equal(2, StorageShelf.ShelvesToOrder(spacesNeeded: 350, placedCapacity: 100, boxedShelves: 1));
        // Boxed shelves alone cover the shortfall → buy nothing (place them first).
        Assert.Equal(0, StorageShelf.ShelvesToOrder(spacesNeeded: 350, placedCapacity: 100, boxedShelves: 3));
    }

    [Fact]
    public void ShelvesToOrder_does_not_treat_sixteen_box_slots_as_sixteen_retail_units()
    {
        // Harvest: ~1100 retail units across SKUs with 10 placed shelves must not demand 40+ buys.
        var spaces = 1100;
        var placedCap = 400 + (10 * StorageShelf.UnitCapacity);
        Assert.Equal(0, StorageShelf.ShelvesToOrder(spaces, placedCap, boxedShelves: 0));
        Assert.True(StorageShelf.UnitCapacity > StorageShelf.Boxes);
    }

    [Fact]
    public void Hold_storage_shelf_uses_unit_capacity_not_box_slot_count()
    {
        Assert.Equal(StorageShelf.UnitCapacity, Wholesaler.Hold(StorageShelf.Item, Umbrella));
        Assert.Equal(100, StorageShelf.UnitCapacity);
        Assert.Equal(16, StorageShelf.Boxes);
    }

    [Fact]
    public void Order_buys_the_number_of_storage_shelves_still_needed()
    {
        var pick = StorageShelf.Order(Street, 22, shelvesNeeded: 2, cash: 75900m, pending: false, inCargo: false, seat: true, GameApi.Live());

        Assert.True(pick.Ordered);
        Assert.Equal(StorageShelf.Item, pick.Item);
        Assert.Equal(1200, pick.Price);
        Assert.Equal(2, pick.Amount);
        Assert.Equal(Street, pick.Street);
        Assert.Equal(22, pick.Number);
        Assert.Equal(Street, pick.FromStreet);
        Assert.Equal(13, pick.FromNumber);
        Assert.Equal("Entities.FurnitureDeliveryContract", pick.Call);
    }

    [Fact]
    public void Order_leaves_the_store_alone_when_no_shelf_is_needed_or_the_order_cannot_be_paid()
    {
        var room = StorageShelf.Order(Street, 22, shelvesNeeded: 0, cash: 75900m, pending: false, inCargo: false, seat: true, GameApi.Live());
        var broke = StorageShelf.Order(Street, 22, shelvesNeeded: 1, cash: 1199m, pending: false, inCargo: false, seat: true, GameApi.Live());
        var waiting = StorageShelf.Order(Street, 22, shelvesNeeded: 1, cash: 75900m, pending: true, inCargo: false, seat: true, GameApi.Live());
        var exact = StorageShelf.Order(Street, 22, shelvesNeeded: 1, cash: 1200m, pending: false, inCargo: false, seat: true, GameApi.Live());

        Assert.False(room.Ordered);
        Assert.False(broke.Ordered);
        Assert.False(waiting.Ordered);
        Assert.True(exact.Ordered);
    }

    [Fact]
    public void Shelf_an_unplaced_shelf_does_not_order_another()
    {
        var pick = StorageShelf.Order(Street, 22, shelvesNeeded: 1, cash: 292000m, pending: false, inCargo: true, seat: true, GameApi.Live());

        Assert.False(pick.Ordered);
    }

    [Fact]
    public void Shelf_a_full_storage_room_orders_nothing()
    {
        var pick = StorageShelf.Order(Street, 22, shelvesNeeded: 1, cash: 292000m, pending: false, inCargo: false, seat: false, GameApi.Live());

        Assert.False(pick.Ordered);
    }

    [Fact]
    public void Seat_after_a_wall_shelf_leaves_a_one_meter_aisle_then_faces_it()
    {
        // Harvest: same-yaw ΔX=1 packed fronts under each other; NPCs only stock the exposed end.
        // Wall → 1.0 m aisle → facing shelf (yaw flipped).
        var seat = StorageShelf.Seat(Shop(), new[] { Rack(995.5f) }, Array.Empty<ShelfSpot>());

        Assert.True(seat.Found);
        Assert.Equal(997.5f, seat.X, 3);
        Assert.Equal(54.5f, seat.Z, 3);
        Assert.Equal(270f, seat.Yaw, 3);
    }

    [Fact]
    public void Seat_after_an_aisle_pair_adds_the_back_to_back_partner()
    {
        var seat = StorageShelf.Seat(
            Shop(),
            new[] { Rack(995.5f), Rack(997.5f, yaw: 270f) },
            Array.Empty<ShelfSpot>());

        Assert.True(seat.Found);
        Assert.Equal(998.5f, seat.X, 3);
        Assert.Equal(54.5f, seat.Z, 3);
        Assert.Equal(90f, seat.Yaw, 3);
    }

    [Fact]
    public void Seat_never_crosses_the_wall_into_the_bathroom()
    {
        var seat = StorageShelf.Seat(
            Shop(),
            new[] { Rack(995.5f), Rack(997.5f, yaw: 270f), Rack(998.5f) },
            Array.Empty<ShelfSpot>());

        Assert.False(seat.Found);
        Assert.Equal("full", seat.Reason);
    }

    [Fact]
    public void Seat_a_full_storage_room_has_no_seat()
    {
        var seat = StorageShelf.Seat(
            Shop(),
            new[] { Rack(995.5f), Rack(997.5f, yaw: 270f), Rack(998.5f) },
            Array.Empty<ShelfSpot>());

        Assert.False(seat.Found);
        Assert.Equal("full", seat.Reason);
    }

    [Fact]
    public void Seat_extends_along_width_after_the_aisle_row_is_full()
    {
        // Slice 4: when 1-2-2 X pattern is full, step a column along Width and keep HeaderLane.
        var seat = StorageShelf.Seat(
            Shop(roomZLow: 50f, roomZHigh: 59f),
            new[] { Rack(995.5f), Rack(997.5f, yaw: 270f), Rack(998.5f) },
            Array.Empty<ShelfSpot>());

        Assert.True(seat.Found);
        Assert.Equal(995.5f, seat.X, 3);
        Assert.Equal(52.5f, seat.Z, 3);
        Assert.Equal(90f, seat.Yaw, 3);
    }

    [Fact]
    public void Seat_a_full_pair_never_gets_a_third_shelf_touching_along_depth()
    {
        var seat = StorageShelf.Seat(
            Shop(),
            new[] { Rack(995.5f), Rack(997.5f, yaw: 270f), Rack(998.5f) },
            Array.Empty<ShelfSpot>());

        Assert.False(seat.Found);
        Assert.Equal("full", seat.Reason);
    }

    [Fact]
    public void Survey_line_names_the_footprint_and_boxes()
    {
        Assert.Equal(
            "shelf.surveyed version=" + LoadLog.Version
                + " day=1586 item=ba:itemname_storageshelf x=995.50 z=54.50 yaw=90 width=2.00 depth=1.00 boxes=4",
            StorageShelf.SurveyLine(995.5f, 54.5f, 90f, 2f, 1f, 4, 1586));
        Assert.Equal(
            "shelf.room version=" + LoadLog.Version
                + " day=1586 cell=0.25 xMin=995.00 xMax=999.00 zMin=53.00 zMax=56.00",
            StorageShelf.RoomLine(0.25f, 995f, 999f, 53f, 56f, 1586));
    }

    [Fact]
    public void Layout_constants_match_the_long_face_aisle_stack()
    {
        // Harvest: half-extent Size made 0.5 m packs; long face needs AisleWidth in front.
        Assert.Equal(2.0f, StorageShelf.FootprintWidth);
        Assert.Equal(1.0f, StorageShelf.FootprintDepth);
        Assert.Equal(1.0f, StorageShelf.AisleWidth);
        Assert.Equal(1.0f, StorageShelf.HeaderLane);
        Assert.Equal(2, StorageShelf.MaxBackToBack);
        Assert.True(StorageShelf.PlaceEnabled);
    }

    [Fact]
    public void Seat_fills_the_facing_shelf_in_an_aisle_gap_before_the_ends()
    {
        var seat = StorageShelf.Seat(Shop(), new[] { Rack(995.5f), Rack(998.5f) }, Array.Empty<ShelfSpot>());

        Assert.True(seat.Found);
        Assert.Equal(997.5f, seat.X, 3);
        Assert.Equal(54.5f, seat.Z, 3);
        Assert.Equal(270f, seat.Yaw, 3);
    }

    [Fact]
    public void Seat_skips_a_spot_another_item_already_stands_on()
    {
        var box = new ShelfSpot(998.5f, 54.5f, 0f, width: 0.6f, depth: 0.6f);

        var seat = StorageShelf.Seat(
            Shop(),
            new[] { Rack(995.5f), Rack(997.5f, yaw: 270f) },
            new[] { box });

        Assert.False(seat.Found);
    }

    [Fact]
    public void Seat_ignores_an_item_that_only_touches_the_seat_edge()
    {
        // Touch the long-side edge (along Z), not the front aisle the restocker needs.
        var flush = new ShelfSpot(998.5f, 53.2f, 0f, width: 0.4f, depth: 0.4f);

        var seat = StorageShelf.Seat(
            Shop(),
            new[] { Rack(995.5f), Rack(997.5f, yaw: 270f) },
            new[] { flush });

        Assert.True(seat.Found);
        Assert.Equal(998.5f, seat.X, 3);
        Assert.Equal(90f, seat.Yaw, 3);
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
    public void Seat_a_single_shelf_extends_along_its_width_when_the_room_is_deep_enough()
    {
        var seat = StorageShelf.Seat(
            Shop(roomZLow: 50f, roomZHigh: 59f),
            new[] { Rack(997f) },
            Array.Empty<ShelfSpot>());

        Assert.True(seat.Found);
        Assert.Equal(997f, seat.X, 3);
        Assert.Equal(52.5f, seat.Z, 3);
        Assert.Equal(90f, seat.Yaw, 3);
    }

    [Fact]
    public void Seat_a_lone_shelf_boxed_in_along_its_width_leaves_an_aisle_then_faces_it()
    {
        // Harvest: packing at ΔDepth=1 same yaw buried fronts; only the exposed end got boxes.
        var handTruck = new ShelfSpot(997.03f, 52.33f, 0f, width: 1f, depth: 1f);

        var seat = StorageShelf.Seat(Shop(), new[] { Rack(997f) }, new[] { handTruck });

        Assert.True(seat.Found);
        Assert.Equal(999f, seat.X, 3);
        Assert.Equal(54.5f, seat.Z, 3);
        Assert.Equal(270f, seat.Yaw, 3);
    }

    [Fact]
    public void Chain_day_roll_full_fixture_lone_shelf_places_the_cargo_shelf_across_the_aisle()
    {
        // Chain: day roll → full fixture → shelf in cargo → lone storage shelf → aisle seat → placed
        var day = Morning.CoverDay(null, autopilotOn: true, day: 1573, coveredDay: 1572);
        var plan = FullFixture(day);
        var handTruck = new ShelfSpot(997.03f, 52.33f, 0f, width: 1f, depth: 1f);
        var seat = StorageShelf.Seat(Shop(), new[] { Rack(997f) }, new[] { handTruck });

        var shelvesNeeded = ShelvesNeededFrom(plan, displayCap: 30, placedShelves: 1, boxedShelves: 1);
        var attend = StorageShelf.Attend(Street, 22, shelvesNeeded, inCargo: true, pending: false, cash: 292000m, seat, GameApi.Live());

        Assert.Equal(ShelfAction.Place, attend.Action);
        Assert.False(attend.Pick.Ordered);
        Assert.Equal(999f, attend.Seat.X, 3);
        Assert.Equal(54.5f, attend.Seat.Z, 3);
        Assert.Equal(270f, attend.Seat.Yaw, 3);
    }

    [Fact]
    public void Seat_follows_the_largest_touching_row_not_a_stray_shelf()
    {
        var stray = new ShelfSpot(1003f, 54.5f, 0f, width: 2f, depth: 1f);

        var seat = StorageShelf.Seat(
            Shop(),
            new[] { stray, Rack(995.5f), Rack(997.5f, yaw: 270f) },
            Array.Empty<ShelfSpot>());

        Assert.True(seat.Found);
        Assert.Equal(998.5f, seat.X, 3);
        Assert.Equal(90f, seat.Yaw, 3);
    }

    [Fact]
    public void Seat_keeps_the_collider_offset_of_the_row()
    {
        // Opposite yaws invert local X in world Z — opposite OffsetX keeps one lane.
        var shifted = new[]
        {
            new ShelfSpot(995.5f, 54.3f, 90f, width: 2f, depth: 1f, offsetX: 0.2f, offsetZ: 0f),
            new ShelfSpot(997.5f, 54.3f, 270f, width: 2f, depth: 1f, offsetX: -0.2f, offsetZ: 0f),
        };

        var seat = StorageShelf.Seat(Shop(), shifted, Array.Empty<ShelfSpot>());

        Assert.True(seat.Found, seat.Reason);
        Assert.Equal(998.5f, seat.X, 3);
        Assert.Equal(54.3f, seat.Z, 3);
        Assert.Equal(90f, seat.Yaw, 3);
    }

    [Fact]
    public void Attend_places_the_shelf_in_cargo_on_the_storage_seat_and_orders_nothing()
    {
        var seat = OpenSeat();

        var attend = StorageShelf.Attend(Street, 22, shelvesNeeded: 1, inCargo: true, pending: false, cash: 292000m, seat, GameApi.Live());

        Assert.Equal(ShelfAction.Place, attend.Action);
        Assert.False(attend.Pick.Ordered);
        Assert.Equal(998.5f, attend.Seat.X, 3);
        Assert.Equal(90f, attend.Seat.Yaw, 3);
        Assert.Equal("BuildingRegistration.AddItemInstanceToBuilding", attend.Call);
    }

    [Fact]
    public void Attend_holds_the_shelf_in_cargo_when_the_storage_room_is_full()
    {
        var attend = StorageShelf.Attend(Street, 22, shelvesNeeded: 1, inCargo: true, pending: false, cash: 292000m, ShelfSeat.None("full"), GameApi.Live());

        Assert.Equal(ShelfAction.Hold, attend.Action);
        Assert.False(attend.Pick.Ordered);
        Assert.Equal("full", attend.Reason);
    }

    [Fact]
    public void Attend_orders_only_when_shelves_are_needed_and_a_seat_is_open()
    {
        var seat = OpenSeat();

        var order = StorageShelf.Attend(Street, 22, shelvesNeeded: 1, inCargo: false, pending: false, cash: 292000m, seat, GameApi.Live());
        var room = StorageShelf.Attend(Street, 22, shelvesNeeded: 0, inCargo: false, pending: false, cash: 292000m, seat, GameApi.Live());
        var noSeat = StorageShelf.Attend(Street, 22, shelvesNeeded: 1, inCargo: false, pending: false, cash: 292000m, ShelfSeat.None("full"), GameApi.Live());

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
        var attend = StorageShelf.Attend(Street, 22, shelvesNeeded: 1, inCargo: true, pending: false, cash: 292000m, OpenSeat(), GameApi.Live());

        var line = StorageShelf.PlaceLine(attend, 1571);

        Assert.Equal(
            "shelf.placed version=" + LoadLog.Version
                + " day=1571 street=ba:street_seventhavenue number=22 item=ba:itemname_storageshelf"
                + " x=998.50 z=54.50 yaw=90 call=BuildingRegistration.AddItemInstanceToBuilding",
            line);
    }

    [Fact]
    public void Hold_line_names_the_reason_and_writes_nothing_for_an_order()
    {
        var hold = StorageShelf.Attend(Street, 22, shelvesNeeded: 1, inCargo: true, pending: false, cash: 292000m, ShelfSeat.None("offgrid"), GameApi.Live());
        var order = StorageShelf.Attend(Street, 22, shelvesNeeded: 1, inCargo: false, pending: false, cash: 292000m, OpenSeat(), GameApi.Live());

        Assert.Equal(
            "shelf.held version=" + LoadLog.Version
                + " day=1571 street=ba:street_seventhavenue number=22 item=ba:itemname_storageshelf inCargo=true reason=offgrid",
            StorageShelf.HoldLine(hold, 1571));
        Assert.Equal("", StorageShelf.HoldLine(order, 1571));
        Assert.Equal("", StorageShelf.PlaceLine(order, 1571));
    }

    [Fact]
    public void Chain_leftover_plus_inbound_orders_shelves_only_when_placed_capacity_is_short()
    {
        // Chain: on hand → inbound → spaces needed → shelves to order (Cover line stays full)
        var days = new[]
        {
            new CoverSale(3, 16, open: true),
            new CoverSale(4, 20, open: true),
        };
        var onHand = 40;
        var capacity = Wholesaler.FixtureCapacity(
            Umbrella,
            new[] { new ShelfFixture("ba:itemname_productpanel", Umbrella, 30) });
        var plan = Wholesaler.Cover(
            Umbrella,
            currentDay: 5,
            nextDeliveryDay: 7,
            days,
            onHand,
            capacity,
            stands: Wholesaler.RetailStands(Umbrella));
        var spaces = StorageShelf.SpacesNeeded(onHand, plan.Need);
        var shortfall = StorageShelf.ShelvesToOrder(spaces, placedCapacity: capacity, boxedShelves: 0);
        var plenty = StorageShelf.ShelvesToOrder(spaces, placedCapacity: capacity + (16 * StorageShelf.UnitCapacity), boxedShelves: 0);
        var seat = StorageShelf.Seat(Shop(), new[] { Rack(995.5f), Rack(997.5f, yaw: 270f) }, Array.Empty<ShelfSpot>());
        var pick = StorageShelf.Order(Street, 22, shortfall, cash: 75900m, pending: false, inCargo: false, seat.Found, GameApi.Live());

        Assert.True(plan.Need > 0);
        Assert.Equal(0, plan.FixtureCap);
        Assert.True(shortfall > 0);
        Assert.Equal(0, plenty);
        Assert.True(pick.Ordered);
        Assert.Equal(shortfall, pick.Amount);
        Assert.Equal(StorageShelf.Item, pick.Item);
        Assert.True(seat.Found);
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
        return Wholesaler.Cover(Umbrella, day, day + 2, days, onHand, capacity, stands: Wholesaler.RetailStands(Umbrella));
    }

    private static int ShelvesNeededFrom(CoverPlan plan, int displayCap, int placedShelves, int boxedShelves)
    {
        var spaces = StorageShelf.SpacesNeeded(plan.OnHand, plan.Need);
        var placedCap = displayCap + (placedShelves * StorageShelf.UnitCapacity);
        return StorageShelf.ShelvesToOrder(spaces, placedCap, boxedShelves);
    }

    [Fact]
    public void Chain_day_roll_short_capacity_shelf_in_cargo_orders_nothing()
    {
        // Chain: day roll → capacity short → shelf in cargo → place, no order
        var day = Morning.CoverDay(null, autopilotOn: true, day: 1571, coveredDay: 1570);
        var plan = FullFixture(day);
        var shelvesNeeded = ShelvesNeededFrom(plan, displayCap: 30, placedShelves: 0, boxedShelves: 1);
        var seat = StorageShelf.Seat(Shop(), new[] { Rack(995.5f), Rack(997.5f, yaw: 270f) }, Array.Empty<ShelfSpot>());

        var attend = StorageShelf.Attend(Street, 22, shelvesNeeded, inCargo: true, pending: false, cash: 292000m, seat, GameApi.Live());

        Assert.Equal(1571, day);
        Assert.True(plan.Need > 0);
        Assert.False(attend.Pick.Ordered);
        Assert.Equal("", StorageShelf.Line(attend.Pick, day));
        Assert.Equal(ShelfAction.Place, attend.Action);
        Assert.Equal(998.5f, attend.Seat.X, 3);
    }

    [Fact]
    public void Chain_day_roll_short_capacity_full_storage_room_holds_and_orders_nothing()
    {
        // Chain: day roll → capacity short → storage room full → hold, no order
        var day = Morning.CoverDay(null, autopilotOn: true, day: 1571, coveredDay: 1570);
        var plan = FullFixture(day);
        var shelvesNeeded = ShelvesNeededFrom(plan, displayCap: 30, placedShelves: 0, boxedShelves: 0);
        var row = new[] { Rack(995.5f), Rack(997.5f, yaw: 270f), Rack(998.5f) };
        var seat = StorageShelf.Seat(Shop(), row, Array.Empty<ShelfSpot>());

        var empty = StorageShelf.Attend(Street, 22, shelvesNeeded, inCargo: false, pending: false, cash: 292000m, seat, GameApi.Live());
        var held = StorageShelf.Attend(Street, 22, shelvesNeeded, inCargo: true, pending: false, cash: 292000m, seat, GameApi.Live());

        Assert.True(shelvesNeeded > 0);
        Assert.True(plan.Need > 0);
        Assert.Equal(ShelfAction.Hold, empty.Action);
        Assert.False(empty.Pick.Ordered);
        Assert.Equal(ShelfAction.Hold, held.Action);
        Assert.Equal("full", held.Reason);
    }

    [Fact]
    public void Chain_day_roll_cover_boxed_in_cargo_hold_spike_orders_nothing()
    {
        // Chain: day roll → cover → spaces needed → boxed in cargo → hold-spike → no order
        var day = Morning.CoverDay(null, autopilotOn: true, day: 1581, coveredDay: 1580);
        var plan = FullFixture(day);
        var nested = StorageShelf.BoxedInHolder("ba:itemname_closedcardboardbox", null, 0, new[] { (StorageShelf.Item, 2) });
        var shelvesNeeded = ShelvesNeededFrom(plan, displayCap: 30, placedShelves: 2, boxedShelves: nested);
        var attend = StorageShelf.Attend(
            Street, 22, shelvesNeeded, inCargo: nested > 0, pending: false, cash: 292000m,
            ShelfSeat.None("hold-spike"), GameApi.Live());

        Assert.Equal(1581, day);
        Assert.Equal(2, nested);
        Assert.Equal(0, shelvesNeeded);
        Assert.Equal(ShelfAction.Hold, attend.Action);
        Assert.Equal("hold-spike", attend.Reason);
        Assert.False(attend.Pick.Ordered);
        Assert.Equal("", StorageShelf.Line(attend.Pick, day));
    }

    [Fact]
    public void Chain_day_roll_short_capacity_order_place_then_the_next_seat_is_the_back_to_back_partner()
    {
        // Chain: day roll → capacity short → order → shelf in cargo → aisle seat → placed → next morning seat
        var row = new List<ShelfSpot> { Rack(995.5f), Rack(997.5f, yaw: 270f) };

        var orderDay = Morning.CoverDay(null, autopilotOn: true, day: 1571, coveredDay: 1570);
        var orderPlan = FullFixture(orderDay);
        var ordered = StorageShelf.Attend(
            Street, 22, ShelvesNeededFrom(orderPlan, displayCap: 30, placedShelves: 0, boxedShelves: 0), inCargo: false, pending: false, cash: 292000m,
            StorageShelf.Seat(Shop(), row, Array.Empty<ShelfSpot>()), GameApi.Live());

        var placeDay = Morning.CoverDay(null, autopilotOn: true, day: 1572, coveredDay: orderDay);
        var placePlan = FullFixture(placeDay);
        var placed = StorageShelf.Attend(
            Street, 22, ShelvesNeededFrom(placePlan, displayCap: 30, placedShelves: 2, boxedShelves: 1), inCargo: true, pending: false, cash: 290800m,
            StorageShelf.Seat(Shop(), row, Array.Empty<ShelfSpot>()), GameApi.Live());
        row.Add(Rack(placed.Seat.X, placed.Seat.Z, placed.Seat.Yaw));

        var nextDay = Morning.CoverDay(null, autopilotOn: true, day: 1573, coveredDay: placeDay);
        var next = StorageShelf.Seat(Shop(), row, Array.Empty<ShelfSpot>());

        Assert.Equal(ShelfAction.Order, ordered.Action);
        Assert.Equal(ShelfAction.Place, placed.Action);
        Assert.Equal(998.5f, placed.Seat.X, 3);
        Assert.Equal(90f, placed.Seat.Yaw, 3);
        Assert.Equal(1573, nextDay);
        Assert.False(next.Found);
        Assert.Equal("full", next.Reason);
    }
}
