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

    private static ShelfFloor OpenRoom(float xLow, float xHigh, float zLow, float zHigh)
    {
        var cells = new List<ShelfCell>();
        AddCells(cells, xLow, xHigh, zLow, zHigh, free: true);
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

    private static ShelfSpot[] PackedShop()
    {
        return new[]
        {
            Rack(995.5f),
            Rack(996.5f, yaw: 270f),
            Rack(997.5f),
            Rack(998.5f, yaw: 270f),
            Rack(999.5f),
        };
    }

    private static ShelfSeat OpenSeat()
    {
        // Wall shelf and the shelf 2 m away. The flush seat is the hole between them.
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
        // The facing partner sits one body away. The faces touch.
        var seat = StorageShelf.Seat(Shop(), new[] { Rack(995.5f) }, Array.Empty<ShelfSpot>());

        Assert.True(seat.Found);
        Assert.Equal(996.5f, seat.X, 3);
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
        Assert.Equal(996.5f, seat.X, 3);
        Assert.Equal(54.5f, seat.Z, 3);
        Assert.Equal(270f, seat.Yaw, 3);
    }

    [Fact]
    public void Seat_never_crosses_the_wall_into_the_bathroom()
    {
        var seat = StorageShelf.Seat(Shop(), PackedShop(), Array.Empty<ShelfSpot>());

        Assert.False(seat.Found);
        Assert.Equal("full", seat.Reason);
    }

    [Fact]
    public void Seat_a_full_storage_room_has_no_seat()
    {
        var seat = StorageShelf.Seat(Shop(), PackedShop(), Array.Empty<ShelfSpot>());

        Assert.False(seat.Found);
        Assert.Equal("full", seat.Reason);
    }

    [Fact]
    public void Seat_extends_along_width_after_the_aisle_row_is_full()
    {
        // A flush pair fills the depth run. The next shelf steps along the width.
        var seat = StorageShelf.Seat(
            Shop(roomZLow: 50f, roomZHigh: 59f),
            new[] { Rack(995.5f), Rack(996.5f, yaw: 270f) },
            Array.Empty<ShelfSpot>());

        Assert.True(seat.Found);
        Assert.Equal(995.5f, seat.X, 3);
        Assert.Equal(52.5f, seat.Z, 3);
        Assert.Equal(90f, seat.Yaw, 3);
    }

    [Fact]
    public void Seat_a_full_pair_never_gets_a_third_shelf_touching_along_depth()
    {
        var seat = StorageShelf.Seat(Shop(), PackedShop(), Array.Empty<ShelfSpot>());

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
    public void Day_1606_facing_gap_zero_puts_the_long_faces_together()
    {
        // Stocked shelf x=996.75 depth=0.50. The spare landed at x=997.75 (pitch 1 on a doubled body).
        // facingGap 0 uses the surveyed depth, so the long faces meet at x=997.25.
        var wall = new ShelfSpot(996.75f, 55f, 90f, width: 2.97f, depth: 0.50f);
        var pitch = StorageShelf.ApplyGap(0f);
        var seat = StorageShelf.Seat(OpenRoom(994f, 1006f, 50f, 60f), new[] { wall }, Array.Empty<ShelfSpot>());

        Assert.Equal(0f, pitch);
        Assert.Equal(0f, StorageShelf.FacingGap);
        Assert.Equal(wall.Depth, StorageShelf.AisleCenterStep(wall.Depth));
        Assert.True(seat.Found, seat.Reason);
        Assert.Equal(997.25f, seat.X, 3);
        Assert.Equal(55f, seat.Z, 3);
        Assert.Equal(270f, seat.Yaw, 3);
    }

    [Fact]
    public void Facing_gap_zero_leaves_the_width_end_one_shelf_apart()
    {
        // End of the run steps by shelf width. A zero facing gap does not pull that end cap in.
        StorageShelf.ApplyGap(0f);
        var seat = StorageShelf.Seat(
            Shop(roomZLow: 50f, roomZHigh: 59f),
            new[] { Rack(995.5f), Rack(996.5f, yaw: 270f) },
            Array.Empty<ShelfSpot>());

        Assert.True(seat.Found, seat.Reason);
        Assert.Equal(995.5f, seat.X, 3);
        Assert.Equal(52.5f, seat.Z, 3);
        Assert.Equal(90f, seat.Yaw, 3);
    }

    [Fact]
    public void Facing_gap_zero_on_a_full_body_still_sits_one_body_away()
    {
        var wall = new ShelfSpot(996.75f, 55f, 90f, width: 2.97f, depth: 1f);
        var seat = StorageShelf.Seat(OpenRoom(994f, 1006f, 50f, 60f), new[] { wall }, Array.Empty<ShelfSpot>());

        Assert.Equal(0f, StorageShelf.FacingGap);
        Assert.Equal(wall.Depth, StorageShelf.AisleCenterStep(wall.Depth));
        Assert.True(seat.Found, seat.Reason);
        Assert.Equal(270f, seat.Yaw, 3);
        Assert.Equal(55f, seat.Z, 3);
        Assert.Equal(wall.X + wall.Depth, seat.X, 3);
    }

    [Fact]
    public void Shelf_gap_file_uses_zero_as_touching_and_one_point_two_five_as_the_old_spacing()
    {
        try
        {
            Assert.Equal(0f, StorageShelf.ParseGap(null));
            Assert.Equal(0f, StorageShelf.ParseGap("not json"));
            Assert.Equal(0f, StorageShelf.ParseGap("{ \"facingGap\": 0, \"note\": \"faces touch\" }"));
            Assert.Equal(1.25f, StorageShelf.ParseGap("{ \"facingGap\": 1.25 }"));
            Assert.Equal(1.25f, StorageShelf.ParseGap("1.25"));
            Assert.Equal(-1f, StorageShelf.ParseGap("{ \"facingGap\": -1 }"));

            Assert.Equal(0f, StorageShelf.ApplyGap(0f));
            Assert.Equal(0f, StorageShelf.AislePitch);
            Assert.Equal(1.25f, StorageShelf.ApplyGap(1.25f));
            Assert.Equal(1.25f, StorageShelf.AislePitch);

            var wall = new ShelfSpot(996.75f, 55f, 90f, width: 2.97f, depth: 0.50f);
            var body = StorageShelf.ShelfBody(wall.Depth);
            var seat = StorageShelf.Seat(OpenRoom(994f, 1006f, 50f, 60f), new[] { wall }, Array.Empty<ShelfSpot>());
            Assert.True(seat.Found, seat.Reason);
            Assert.Equal(wall.X + (body * 1.25f), seat.X, 3);
            Assert.Equal(270f, seat.Yaw, 3);
        }
        finally
        {
            StorageShelf.ApplyGap(0f);
        }
    }

    [Fact]
    public void Shelf_gap_file_is_created_when_missing_and_keeps_a_later_edit()
    {
        var root = Path.Combine(Path.GetTempPath(), "chaoticceo-gap-" + Guid.NewGuid().ToString("N"));
        try
        {
            var created = StorageShelf.LoadGap(root);
            var path = StorageShelf.GapPath(root);

            Assert.Equal(0f, created);
            Assert.Equal(0f, StorageShelf.FacingGap);
            Assert.True(File.Exists(path));

            File.WriteAllText(path, "{ \"facingGap\": 1.25 }" + Environment.NewLine);
            Assert.Equal(1.25f, StorageShelf.LoadGap(root));
            Assert.Equal(
                "shelf.gap version=" + LoadLog.Version + " day=1594 facingGap=1.25 pitch=1.25",
                StorageShelf.GapLine(1594));
        }
        finally
        {
            StorageShelf.ApplyGap(0f);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void Layout_constants_match_the_long_face_aisle_stack()
    {
        // Harvest: live Size ~2.97×0.50; long face needs AisleWidth in front.
        Assert.Equal(3.0f, StorageShelf.FootprintWidth);
        Assert.Equal(0.5f, StorageShelf.FootprintDepth);
        Assert.Equal(0f, StorageShelf.FacingGap);
        Assert.Equal(0f, StorageShelf.AislePitch);
        Assert.Equal(1.0f, StorageShelf.AisleWidth);
        Assert.Equal(1200f, StorageShelf.Buyback(960f));
        Assert.Equal(1200f, StorageShelf.Buyback(1200f));
        Assert.Equal(1500f, StorageShelf.Buyback(1500f));
        Assert.Equal(1.0f, StorageShelf.HeaderLane);
        Assert.Equal(2, StorageShelf.MaxBackToBack);
        Assert.True(StorageShelf.PlaceEnabled);
    }

    [Fact]
    public void Seat_fills_the_facing_shelf_in_an_aisle_gap_before_the_ends()
    {
        var seat = StorageShelf.Seat(Shop(), new[] { Rack(995.5f), Rack(998.5f) }, Array.Empty<ShelfSpot>());

        Assert.True(seat.Found);
        Assert.Equal(996.5f, seat.X, 3);
        Assert.Equal(54.5f, seat.Z, 3);
        Assert.Equal(270f, seat.Yaw, 3);
    }

    [Fact]
    public void Seat_skips_a_spot_another_item_already_stands_on()
    {
        var box = new ShelfSpot(996.5f, 54.5f, 0f, width: 0.6f, depth: 0.6f);

        var seat = StorageShelf.Seat(
            Shop(),
            new[] { Rack(995.5f), Rack(997.5f, yaw: 270f) },
            new[] { box });

        Assert.True(seat.Found, seat.Reason);
        Assert.Equal(998.5f, seat.X, 3);
        Assert.Equal(90f, seat.Yaw, 3);
    }

    [Fact]
    public void Seat_ignores_an_item_that_only_touches_the_seat_edge()
    {
        // Touch the long-side edge (along Z), not the front aisle the restocker needs.
        var flush = new ShelfSpot(996.5f, 53.2f, 0f, width: 0.4f, depth: 0.4f);

        var seat = StorageShelf.Seat(
            Shop(),
            new[] { Rack(995.5f), Rack(997.5f, yaw: 270f) },
            new[] { flush });

        Assert.True(seat.Found);
        Assert.Equal(996.5f, seat.X, 3);
        Assert.Equal(270f, seat.Yaw, 3);
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
    public void Seat_a_single_shelf_faces_the_aisle_even_when_the_room_is_deep_enough()
    {
        // The second shelf is the aisle partner. A side-by-side column waits until the 1—aisle—2 run is full.
        var seat = StorageShelf.Seat(
            Shop(roomZLow: 50f, roomZHigh: 59f),
            new[] { Rack(997f) },
            Array.Empty<ShelfSpot>());

        Assert.True(seat.Found, seat.Reason);
        Assert.Equal(998f, seat.X, 3);
        Assert.Equal(54.5f, seat.Z, 3);
        Assert.Equal(270f, seat.Yaw, 3);
    }

    [Fact]
    public void Seat_a_lone_shelf_boxed_in_along_its_width_leaves_an_aisle_then_faces_it()
    {
        // Harvest: packing at ΔDepth=1 same yaw buried fronts; only the exposed end got boxes.
        var handTruck = new ShelfSpot(997.03f, 52.33f, 0f, width: 1f, depth: 1f);

        var seat = StorageShelf.Seat(Shop(), new[] { Rack(997f) }, new[] { handTruck });

        Assert.True(seat.Found);
        Assert.Equal(998f, seat.X, 3);
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
        Assert.Equal(998f, attend.Seat.X, 3);
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
        Assert.Equal(996.5f, seat.X, 3);
        Assert.Equal(270f, seat.Yaw, 3);
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
        Assert.Equal(996.5f, seat.X, 3);
        Assert.Equal(54.3f, seat.Z, 3);
        Assert.Equal(270f, seat.Yaw, 3);
    }

    [Fact]
    public void Attend_places_the_shelf_in_cargo_on_the_storage_seat_and_orders_nothing()
    {
        var seat = OpenSeat();

        var attend = StorageShelf.Attend(Street, 22, shelvesNeeded: 1, inCargo: true, pending: false, cash: 292000m, seat, GameApi.Live());

        Assert.Equal(ShelfAction.Place, attend.Action);
        Assert.False(attend.Pick.Ordered);
        Assert.Equal(996.5f, attend.Seat.X, 3);
        Assert.Equal(270f, attend.Seat.Yaw, 3);
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
                + " x=996.50 z=54.50 yaw=270 call=BuildingRegistration.AddItemInstanceToBuilding",
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
        Assert.Equal(30, plan.FixtureCap);
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
        Assert.Equal(996.5f, attend.Seat.X, 3);
    }

    [Fact]
    public void Chain_day_roll_short_capacity_full_storage_room_holds_and_orders_nothing()
    {
        // Chain: day roll → capacity short → storage room full → hold, no order
        var day = Morning.CoverDay(null, autopilotOn: true, day: 1571, coveredDay: 1570);
        var plan = FullFixture(day);
        var shelvesNeeded = ShelvesNeededFrom(plan, displayCap: 30, placedShelves: 0, boxedShelves: 0);
        var row = PackedShop();
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
    public void StorageBoxes_counts_only_units_the_displays_cannot_hold_in_whole_boxes()
    {
        Assert.Equal(8, StorageShelf.StorageBoxes(units: 346, displayCap: 200, boxSize: 20));
        Assert.Equal(1, StorageShelf.StorageBoxes(units: 201, displayCap: 200, boxSize: 20));
        Assert.Equal(0, StorageShelf.StorageBoxes(units: 200, displayCap: 200, boxSize: 20));
        Assert.Equal(0, StorageShelf.StorageBoxes(units: 50, displayCap: 200, boxSize: 20));
        Assert.Equal(0, StorageShelf.StorageBoxes(units: 346, displayCap: 0, boxSize: 0));
    }

    [Fact]
    public void BoxesNeeded_is_unknown_when_any_item_has_no_box_size()
    {
        var known = new[] { new ShelfStock("a", 346, 200, 20), new ShelfStock("b", 256, 96, 24) };
        var blind = new[] { new ShelfStock("a", 346, 200, 20), new ShelfStock("b", 256, 96, 0) };

        Assert.Equal(8 + 7, StorageShelf.BoxesNeeded(known));
        Assert.Equal(-1, StorageShelf.BoxesNeeded(blind));
        Assert.Equal(0, StorageShelf.BoxesNeeded(Array.Empty<ShelfStock>()));
    }

    [Fact]
    public void ShelvesForBoxes_uses_sixteen_slots_per_shelf()
    {
        Assert.Equal(0, StorageShelf.ShelvesForBoxes(0));
        Assert.Equal(1, StorageShelf.ShelvesForBoxes(1));
        Assert.Equal(1, StorageShelf.ShelvesForBoxes(16));
        Assert.Equal(2, StorageShelf.ShelvesForBoxes(17));
        Assert.Equal(4, StorageShelf.ShelvesForBoxes(64));
    }

    [Fact]
    public void ShelvesToOrderByBoxes_counts_placed_and_inbound_shelves_first()
    {
        Assert.Equal(1, StorageShelf.ShelvesToOrderByBoxes(boxesNeeded: 20, placedShelves: 1, inboundShelves: 0));
        Assert.Equal(0, StorageShelf.ShelvesToOrderByBoxes(boxesNeeded: 20, placedShelves: 1, inboundShelves: 1));
        Assert.Equal(0, StorageShelf.ShelvesToOrderByBoxes(boxesNeeded: 12, placedShelves: 4, inboundShelves: 0));
        Assert.Equal(2, StorageShelf.ShelvesToOrderByBoxes(boxesNeeded: 40, placedShelves: 1, inboundShelves: 0));
    }

    [Fact]
    public void ShelvesNeeded_box_math_wins_over_unit_math_when_box_sizes_are_known()
    {
        // Unit math (100/shelf) says 3 short; 12 boxes fit the one placed shelf.
        Assert.Equal(3, StorageShelf.ShelvesToOrder(spacesNeeded: 400, placedCapacity: 100, boxedShelves: 0));
        Assert.Equal(0, StorageShelf.ShelvesNeeded(boxesNeeded: 12, placedShelves: 1, inboundShelves: 0, spacesNeeded: 400, placedCap: 100));
        // Unknown box size falls back to the unit math.
        Assert.Equal(3, StorageShelf.ShelvesNeeded(boxesNeeded: -1, placedShelves: 1, inboundShelves: 0, spacesNeeded: 400, placedCap: 100));
    }

    [Fact]
    public void Need_line_names_boxes_shelves_spare_and_the_verdict()
    {
        Assert.Equal(
            "shelf.need version=" + LoadLog.Version
                + " day=1597 street=ba:street_seventhavenue number=22 boxes=12 shelves=1 keep=2 placed=4 slots=64 used=11 spare=3 verdict=enough",
            StorageShelf.NeedLine(1597, Street, 22, boxesNeeded: 12, placedShelves: 4, usedSlots: 11, keep: 2));
        Assert.EndsWith(
            "boxes=40 shelves=3 keep=4 placed=1 slots=16 used=16 spare=-2 verdict=short",
            StorageShelf.NeedLine(1597, Street, 22, boxesNeeded: 40, placedShelves: 1, usedSlots: 16, keep: 4));
        Assert.EndsWith(
            "boxes=-1 shelves=0 keep=0 placed=4 slots=64 used=11 spare=0 verdict=unknown",
            StorageShelf.NeedLine(1597, Street, 22, boxesNeeded: -1, placedShelves: 4, usedSlots: 11, keep: 0));
    }

    [Fact]
    public void KeepTarget_is_need_plus_one_when_room_has_spare_seat()
    {
        Assert.Equal(2, StorageShelf.KeepTarget(need: 1, roomCanSpare: true));
        Assert.Equal(3, StorageShelf.KeepTarget(need: 2, roomCanSpare: true));
    }

    [Fact]
    public void KeepTarget_stays_need_when_room_full_at_need()
    {
        Assert.Equal(1, StorageShelf.KeepTarget(need: 1, roomCanSpare: false));
        Assert.Equal(2, StorageShelf.KeepTarget(need: 2, roomCanSpare: false));
    }

    [Fact]
    public void KeepTarget_holds_the_empty_plus_one_when_it_blocks_its_own_seat()
    {
        // Harvest day 1601: spareSeats=false while placed=2 need=1, then the empty shelf was sold.
        Assert.Equal(2, StorageShelf.KeepTarget(need: 1, roomCanSpare: false, placed: 2));
        Assert.Equal(1, StorageShelf.KeepTarget(need: 1, roomCanSpare: false, placed: 1));
        Assert.Equal(1, StorageShelf.KeepTarget(need: 1, roomCanSpare: false, placed: 4));
    }

    [Fact]
    public void KeepTarget_is_zero_when_need_is_zero()
    {
        Assert.Equal(0, StorageShelf.KeepTarget(need: 0, roomCanSpare: true));
        Assert.Equal(0, StorageShelf.KeepTarget(need: -3, roomCanSpare: true));
    }

    [Fact]
    public void Excess_counts_cargo_and_placed_above_keep()
    {
        Assert.Equal(34, StorageShelf.Excess(have: StorageShelf.Have(placed: 4, cargo: 32, pending: 0), keep: 2));
        Assert.Equal(0, StorageShelf.Excess(have: 2, keep: 2));
        Assert.Equal(0, StorageShelf.Excess(have: 1, keep: 2));
    }

    [Fact]
    public void Order_uses_keep_not_bare_need()
    {
        // need=1, placed=1, spare seat → keep=2 → order 1
        Assert.Equal(1, StorageShelf.OrderForKeep(keep: 2, have: 1));
        // placed already at keep → order 0
        Assert.Equal(0, StorageShelf.OrderForKeep(keep: 2, have: 2));
        // far short still caps at one per morning
        Assert.Equal(1, StorageShelf.OrderForKeep(keep: 4, have: 0));
    }

    [Fact]
    public void Sell_does_not_drop_the_floor_below_keep_to_cover_a_pending_shelf()
    {
        // Harvest day 1589: placed the cargo shelf to keep=2, then sold it because pending=1 was still in the excess.
        var placed = 2;
        var keep = 2;
        var plan = StorageShelf.DecideSell(
            StorageShelf.Excess(StorageShelf.Have(placed, cargo: 11, pending: 1), keep),
            cargo: 11,
            emptyPlaced: 1,
            stockedPlaced: 1,
            placed,
            keep);

        Assert.Equal(11, plan.Cargo);
        Assert.Equal(0, plan.EmptyPlaced);
        // Leftover excess is the truck. The stocked shelf is logged as held, not sold.
        Assert.True(plan.HoldStocked);
    }

    [Fact]
    public void Order_does_not_buy_when_unplaced_cargo_already_covers_keep()
    {
        // Unplaced shelves count. keep is X+1. Cargo already past that is not another purchase.
        Assert.Equal(0, StorageShelf.OrderForKeep(keep: 2, have: StorageShelf.Have(placed: 1, cargo: 11, pending: 0)));
        Assert.Equal(0, StorageShelf.OrderForKeep(keep: 2, have: StorageShelf.Have(placed: 1, cargo: 1, pending: 0)));
        Assert.Equal(0, StorageShelf.OrderForKeep(keep: 2, have: StorageShelf.Have(placed: 0, cargo: 2, pending: 0)));
    }

    [Fact]
    public void Sell_prefers_cargo_before_placed()
    {
        var plan = StorageShelf.DecideSell(
            excess: 34, cargo: 32, emptyPlaced: 4, stockedPlaced: 0, placed: 4, keep: 2);
        Assert.Equal(32, plan.Cargo);
        Assert.Equal(2, plan.EmptyPlaced);
        Assert.False(plan.HoldStocked);
    }

    [Fact]
    public void Stocked_placed_is_held_not_sold()
    {
        var plan = StorageShelf.DecideSell(
            excess: 2, cargo: 0, emptyPlaced: 0, stockedPlaced: 3, placed: 3, keep: 1);
        Assert.Equal(0, plan.Cargo);
        Assert.Equal(0, plan.EmptyPlaced);
        Assert.True(plan.HoldStocked);
    }

    [Fact]
    public void Attend_does_not_sell_packed_cargo_before_place_reaches_keep()
    {
        // Harvest day 1588: pack then sell ate the spare — placed=1 while keep=2.
        var keep = 2;
        var placed = 1;
        var cargo = 2;
        var plan = StorageShelf.DecideSell(
            StorageShelf.Excess(StorageShelf.Have(placed, cargo, pending: 0), keep),
            cargo,
            emptyPlaced: placed,
            stockedPlaced: 0,
            placed,
            keep);

        Assert.Equal(0, plan.Cargo);
        Assert.Equal(0, plan.EmptyPlaced);
        Assert.False(plan.HoldStocked);
    }

    [Fact]
    public void Attend_sells_excess_cargo_after_place_reaches_keep()
    {
        var keep = 2;
        var placed = 2;
        var cargo = 3;
        var plan = StorageShelf.DecideSell(
            StorageShelf.Excess(StorageShelf.Have(placed, cargo, pending: 0), keep),
            cargo,
            emptyPlaced: placed,
            stockedPlaced: 0,
            placed,
            keep);

        Assert.Equal(3, plan.Cargo);
        Assert.Equal(0, plan.EmptyPlaced);
        Assert.False(plan.HoldStocked);
    }

    [Fact]
    public void Seat_accept_uses_live_half_depth_for_aisle_partner()
    {
        // Live depth 0.50 is the shelf. facingGap 0 puts the long faces together.
        var wall = new ShelfSpot(995.5f, 54.5f, 90f, width: 2.97f, depth: 0.50f);
        var seat = StorageShelf.Seat(Shop(), new[] { wall }, Array.Empty<ShelfSpot>());
        var partner = new ShelfSpot(seat.X, seat.Z, seat.Yaw, width: 2.97f, depth: 0.50f);

        Assert.True(seat.Found);
        Assert.Equal(996f, seat.X, 3);
        Assert.Equal(54.5f, seat.Z, 3);
        Assert.Equal(270f, seat.Yaw, 3);
        Assert.True(StorageShelf.Accessible(Shop(), wall, new[] { partner }));
        Assert.True(StorageShelf.Accessible(Shop(), partner, new[] { wall }));
    }

    [Fact]
    public void Seat_live_depth_aisle_gap_is_not_packed()
    {
        // New game: live depth 0.50 is the half-extent. depth + AisleWidth (1.5 m) leaves a 0.5 m packed gap.
        // Centers sit one full body (2×depth) plus AisleWidth apart.
        var wall = new ShelfSpot(995.5f, 54.5f, 90f, width: 2.97f, depth: 0.50f);
        var seat = StorageShelf.Seat(Shop(), new[] { wall }, Array.Empty<ShelfSpot>());
        var partner = new ShelfSpot(seat.X, seat.Z, seat.Yaw, wall.Width, wall.Depth);

        Assert.True(seat.Found, seat.Reason);
        Assert.Equal(270f, seat.Yaw, 3);
        Assert.Equal(54.5f, seat.Z, 3);
        Assert.Equal(995.5f + StorageShelf.AisleCenterStep(wall.Depth), seat.X, 3);
        Assert.True(StorageShelf.Accessible(Shop(), wall, new[] { partner }));
        Assert.True(StorageShelf.Accessible(Shop(), partner, new[] { wall }));
    }

    [Fact]
    public void Seat_new_game_same_yaw_half_meter_pair_is_not_an_aisle()
    {
        // GiftShopWholesalers A1 places shelves at x=997 and x=997.5, both yaw 90. That gap is the live depth.
        var rear = new ShelfSpot(997f, 8.5f, 90f, width: 2.97f, depth: 0.50f);
        var front = new ShelfSpot(997.5f, 8.5f, 90f, width: 2.97f, depth: 0.50f);
        var floor = OpenRoom(995f, 1002f, 6f, 11f);

        Assert.False(StorageShelf.Accessible(floor, rear, new[] { front }));

        var seat = StorageShelf.Seat(floor, new[] { front }, Array.Empty<ShelfSpot>());
        var partner = new ShelfSpot(seat.X, seat.Z, seat.Yaw, front.Width, front.Depth);

        Assert.True(seat.Found, seat.Reason);
        Assert.Equal(270f, seat.Yaw, 3);
        Assert.Equal(8.5f, seat.Z, 3);
        Assert.Equal(997.5f + StorageShelf.AisleCenterStep(front.Depth), seat.X, 3);
        Assert.True(StorageShelf.Accessible(floor, front, new[] { partner }));
        Assert.True(StorageShelf.Accessible(floor, partner, new[] { front }));
    }

    [Fact]
    public void Seat_one_shelf_in_a_wide_room_faces_the_aisle_before_a_side_neighbor()
    {
        // A wide gift shop has room beside the long face. The spare still faces the first shelf.
        var floor = OpenRoom(994f, 1006f, 50f, 60f);
        var wall = new ShelfSpot(996.75f, 55f, 90f, width: 2.97f, depth: 0.50f);
        var seat = StorageShelf.Seat(floor, new[] { wall }, Array.Empty<ShelfSpot>());

        Assert.True(seat.Found, seat.Reason);
        Assert.Equal(270f, seat.Yaw, 3);
        Assert.Equal(55f, seat.Z, 3);
        Assert.Equal(996.75f + StorageShelf.AisleCenterStep(wall.Depth), seat.X, 3);
    }

    [Fact]
    public void Reseat_packs_the_same_yaw_spare_and_leaves_an_aisle_pair()
    {
        // Harvest day 1588: stocked shelf at 996.75 and an empty shelf 0.5 m in front, both yaw 90.
        var floor = OpenRoom(994f, 1006f, 50f, 60f);
        var wall = new ShelfSpot(996.75f, 55f, 90f, width: 2.97f, depth: 0.50f);
        var jammed = new ShelfSpot(997.25f, 55f, 90f, width: 2.97f, depth: 0.50f);
        var reseat = StorageShelf.ShelvesToReseat(
            floor,
            new[] { wall, jammed },
            Array.Empty<ShelfSpot>(),
            new[] { 373, 0 });
        var aisle = StorageShelf.Seat(floor, new[] { wall }, Array.Empty<ShelfSpot>());
        var partner = new ShelfSpot(aisle.X, aisle.Z, aisle.Yaw, wall.Width, wall.Depth);
        var kept = StorageShelf.ShelvesToReseat(
            floor,
            new[] { wall, partner },
            Array.Empty<ShelfSpot>(),
            new[] { 373, 0 });

        Assert.Single(reseat);
        Assert.Equal(997.25f, reseat[0].X, 3);
        Assert.Equal(90f, reseat[0].Yaw, 3);
        Assert.Empty(kept);
        Assert.True(StorageShelf.Accessible(floor, wall, new[] { partner }));
        Assert.True(StorageShelf.Accessible(floor, partner, new[] { wall }));

        var third = StorageShelf.Seat(floor, new[] { wall, partner }, Array.Empty<ShelfSpot>());
        var back = new ShelfSpot(third.X, third.Z, third.Yaw, wall.Width, wall.Depth);
        var trio = StorageShelf.ShelvesToReseat(
            floor,
            new[] { wall, partner, back },
            Array.Empty<ShelfSpot>(),
            new[] { 373, 0, 0 });
        Assert.Empty(trio);
    }

    [Fact]
    public void Seat_live_depth_back_to_back_uses_the_full_body_not_the_half_extent()
    {
        // The facing mate already touches. The next seat stays one body behind it, not on the width end.
        var wall = new ShelfSpot(995.5f, 54.5f, 90f, width: 2.97f, depth: 0.50f);
        var across = new ShelfSpot(995.5f + wall.Depth, 54.5f, 270f, width: 2.97f, depth: 0.50f);
        var seat = StorageShelf.Seat(Shop(), new[] { wall, across }, Array.Empty<ShelfSpot>());

        Assert.True(seat.Found, seat.Reason);
        Assert.Equal(across.X + StorageShelf.ShelfBody(wall.Depth), seat.X, 3);
        Assert.Equal(54.5f, seat.Z, 3);
        Assert.Equal(90f, seat.Yaw, 3);
        Assert.True(StorageShelf.Accessible(Shop(), wall, new[] { across }));
        Assert.True(StorageShelf.Accessible(Shop(), across, new[] { wall }));
    }

    [Fact]
    public void Seat_live_pair_puts_the_third_shelf_against_the_spare()
    {
        // The spare already touches the stocked shelf. The next seat is against the spare's back, not an end cap.
        var floor = OpenRoom(994f, 1006f, 50f, 60f);
        var stocked = new ShelfSpot(996.75f, 55f, 90f, width: 2.97f, depth: 0.50f);
        var spare = new ShelfSpot(stocked.X + stocked.Depth, 55f, 270f, width: 2.97f, depth: 0.50f);
        var body = StorageShelf.ShelfBody(stocked.Depth);

        var spareFirst = StorageShelf.Seat(floor, new[] { spare, stocked }, Array.Empty<ShelfSpot>());
        var stockedFirst = StorageShelf.Seat(floor, new[] { stocked, spare }, Array.Empty<ShelfSpot>());

        Assert.True(spareFirst.Found, spareFirst.Reason);
        Assert.Equal(spare.X + body, spareFirst.X, 3);
        Assert.Equal(55f, spareFirst.Z, 3);
        Assert.Equal(90f, spareFirst.Yaw, 3);
        Assert.Equal(spareFirst.X, stockedFirst.X, 3);
        Assert.Equal(spareFirst.Yaw, stockedFirst.Yaw, 3);
        Assert.Equal(body, Math.Abs(spareFirst.X - spare.X), 3);
    }

    [Fact]
    public void Seat_a_west_facing_stocked_shelf_keeps_the_third_against_the_spare()
    {
        // The stocked shelf faces west and its back is the wall. The spare is the aisle partner.
        var floor = OpenRoom(990f, 998.5f, 50f, 60f);
        var stocked = new ShelfSpot(998f, 55f, 270f, width: 2.97f, depth: 0.50f);
        var spare = StorageShelf.Seat(floor, new[] { stocked }, Array.Empty<ShelfSpot>());
        var partner = new ShelfSpot(spare.X, spare.Z, spare.Yaw, stocked.Width, stocked.Depth);
        var body = StorageShelf.ShelfBody(stocked.Depth);

        var spareFirst = StorageShelf.Seat(floor, new[] { partner, stocked }, Array.Empty<ShelfSpot>());
        var stockedFirst = StorageShelf.Seat(floor, new[] { stocked, partner }, Array.Empty<ShelfSpot>());

        Assert.True(spare.Found, spare.Reason);
        Assert.Equal(998f - StorageShelf.AisleCenterStep(stocked.Depth), spare.X, 3);
        Assert.Equal(90f, spare.Yaw, 3);
        Assert.True(spareFirst.Found, spareFirst.Reason);
        Assert.Equal(partner.X - body, spareFirst.X, 3);
        Assert.Equal(270f, spareFirst.Yaw, 3);
        Assert.Equal(spareFirst.X, stockedFirst.X, 3);
        Assert.Equal(spareFirst.Yaw, stockedFirst.Yaw, 3);
    }

    [Fact]
    public void Order_does_not_buy_when_ninety_shipping_shelves_already_cover_keep()
    {
        // The spare was already in the shipping area. That shelf is the keep. Do not buy another.
        var keep = StorageShelf.KeepTarget(need: 1, roomCanSpare: true);
        var have = StorageShelf.Have(placed: 1, cargo: 0, pending: 0, shipping: 90);
        Assert.Equal(2, keep);
        Assert.Equal(0, StorageShelf.OrderForKeep(keep, have));
        Assert.Equal(0, StorageShelf.OrderForKeep(keep, StorageShelf.Have(placed: 1, cargo: 0, pending: 0, shipping: 1)));
    }

    [Fact]
    public void ContractDue_day_1605_still_owes_the_shelf_ordered_for_day_1602()
    {
        // The game skips a contract on a later morning that is still before the delivery hour.
        Assert.True(StorageShelf.ContractDue(day: 1605, deliveryDay: 1602));
        Assert.True(StorageShelf.ContractDue(day: 1602, deliveryDay: 1602));
        Assert.False(StorageShelf.ContractDue(day: 1601, deliveryDay: 1602));
        Assert.False(StorageShelf.ContractDue(day: 1605, deliveryDay: 0));
    }

    [Fact]
    public void ShippingToSell_waits_until_the_floor_reaches_keep_then_clears_the_pile()
    {
        var keep = StorageShelf.KeepTarget(need: 1, roomCanSpare: true);
        Assert.Equal(0, StorageShelf.ShippingToSell(placed: 1, cargo: 0, pending: 0, shipping: 90, keep));
        Assert.Equal(89, StorageShelf.ShippingToSell(placed: 2, cargo: 0, pending: 0, shipping: 89, keep));
        Assert.Equal(0, StorageShelf.ShippingToSell(placed: 2, cargo: 0, pending: 0, shipping: 0, keep));
    }

    [Fact]
    public void ResolveFootprint_keeps_live_storage_shelf_half_depth()
    {
        // Live collider is ~3×0.5 — do not rewrite it to the old 2×1 constants.
        var (width, depth) = StorageShelf.ResolveFootprint(2.97f, 0.50f);
        Assert.Equal(2.97f, width);
        Assert.Equal(0.50f, depth);
    }

    [Fact]
    public void Keep_line_names_need_keep_have_and_spare_seats()
    {
        Assert.Equal(
            "shelf.keep version=" + LoadLog.Version
                + " day=1588 street=ba:street_seventhavenue number=22 boxes=11 need=1 keep=2 placed=4 cargo=32 pending=0 have=36 shipping=0 spareSeats=true",
            StorageShelf.KeepLine(1588, Street, 22, boxes: 11, need: 1, keep: 2, placed: 4, cargo: 32, pending: 0, have: 36, spareSeats: true));
    }

    [Fact]
    public void Box_line_names_units_display_cap_box_size_and_boxes()
    {
        Assert.Equal(
            "shelf.box version=" + LoadLog.Version
                + " day=1597 street=ba:street_seventhavenue number=22 item=ba:itemname_cheapgift units=346 displayCap=200 boxSize=20 boxes=8",
            StorageShelf.BoxLine(new ShelfStock("ba:itemname_cheapgift", 346, 200, 20), 1597, Street, 22));
    }

    [Fact]
    public void Storage_four_item_gift_shop_day_1597_needs_two_of_four_placed_shelves()
    {
        // Harvest day 1597: units after truck = onHand + need per item; four shelves placed.
        // Display caps and box sizes are illustrative until shelf.box logs the live values.
        var stocks = new[]
        {
            new ShelfStock("ba:itemname_cheapgift", 187 + 159, 200, 20),
            new ShelfStock("ba:itemname_cheapflower", 198 + 40, 160, 20),
            new ShelfStock("ba:itemname_sodacan", 201 + 55, 192, 24),
            new ShelfStock("ba:itemname_umbrella", 192 + 60, 200, 10),
        };

        var boxes = StorageShelf.BoxesNeeded(stocks);

        Assert.Equal(8 + 4 + 3 + 6, boxes);
        Assert.Equal(2, StorageShelf.ShelvesForBoxes(boxes));
        Assert.Equal(0, StorageShelf.ShelvesToOrderByBoxes(boxes, placedShelves: 4, inboundShelves: 0));
        var keep = StorageShelf.KeepTarget(StorageShelf.ShelvesForBoxes(boxes), roomCanSpare: true);
        Assert.Equal(3, keep);
        Assert.EndsWith(
            "spare=2 verdict=enough",
            StorageShelf.NeedLine(1597, Street, 22, boxes, placedShelves: 4, usedSlots: boxes, keep));
    }

    [Fact]
    public void Chain_keep_pack_place_sell_leaves_placed_at_keep()
    {
        // Chain: keep → pack → place → sell
        var keep = StorageShelf.KeepTarget(need: 1, roomCanSpare: true);
        var placed = 4;
        var cargo = 0;

        var packed = placed - keep;
        placed -= packed;
        cargo += packed;

        while (placed < keep && cargo > 0)
        {
            placed++;
            cargo--;
        }

        var have = StorageShelf.Have(placed, cargo, pending: 0);
        var sell = StorageShelf.DecideSell(
            StorageShelf.Excess(have, keep), cargo, emptyPlaced: placed, stockedPlaced: 0, placed, keep);
        placed -= sell.EmptyPlaced;
        cargo -= sell.Cargo;

        Assert.Equal(2, keep);
        Assert.Equal(keep, placed);
        Assert.Equal(0, cargo);
        Assert.Equal(keep, StorageShelf.Have(placed, cargo, pending: 0));
    }

    [Fact]
    public void Chain_keep_pack_below_keep_place_then_sell_restores_placed()
    {
        // Chain: keep → pack (blocked drops under keep) → place → sell
        var keep = StorageShelf.KeepTarget(need: 1, roomCanSpare: true);
        var placed = 1;
        var cargo = 2;

        while (placed < keep && cargo > 0)
        {
            placed++;
            cargo--;
        }

        var have = StorageShelf.Have(placed, cargo, pending: 0);
        var sell = StorageShelf.DecideSell(
            StorageShelf.Excess(have, keep), cargo, emptyPlaced: placed, stockedPlaced: 0, placed, keep);
        placed -= sell.EmptyPlaced;
        cargo -= sell.Cargo;

        Assert.Equal(keep, placed);
        Assert.Equal(0, cargo);
    }

    [Fact]
    public void Chain_day_roll_cover_box_math_keep_sell_reseat_no_order_when_have_covers_keep()
    {
        // Chain: day roll → cover → box math → keep → sell excess → reseat → attend (no order when have>=keep)
        var day = Morning.CoverDay(null, autopilotOn: true, day: 1597, coveredDay: 1596);
        var plan = FullFixture(day);
        var units = StorageShelf.SpacesNeeded(plan.OnHand, plan.Need);
        var stock = new ShelfStock(Umbrella, units, displayCap: 30, boxSize: 10);
        var boxes = StorageShelf.BoxesNeeded(new[] { stock });
        var need = StorageShelf.NeedShelves(boxes, units, placedCap: 30);
        var keep = StorageShelf.KeepTarget(need, roomCanSpare: true);
        var have = StorageShelf.Have(placed: 4, cargo: 0, pending: 0);
        var sell = StorageShelf.DecideSell(
            StorageShelf.Excess(have, keep), cargo: 0, emptyPlaced: 4, stockedPlaced: 0, placed: 4, keep: keep);
        var afterSell = have - sell.Cargo - sell.EmptyPlaced;
        var order = StorageShelf.OrderForKeep(keep, afterSell);
        var attend = StorageShelf.Attend(Street, 22, order, inCargo: false, pending: false, cash: 292000m, OpenSeat(), GameApi.Live());

        Assert.Equal(1597, day);
        Assert.True(plan.Need > 0);
        Assert.True(boxes > 0 && boxes <= 4 * StorageShelf.Boxes);
        Assert.Equal(need + 1, keep);
        Assert.True(sell.EmptyPlaced >= 0);
        Assert.True(afterSell >= keep || sell.HoldStocked);
        Assert.Equal(0, order);
        Assert.Equal(ShelfAction.None, attend.Action);
        Assert.False(attend.Pick.Ordered);
        Assert.EndsWith(
            "verdict=enough",
            StorageShelf.NeedLine(day, Street, 22, boxes, placedShelves: 4, usedSlots: boxes, keep));
    }

    [Fact]
    public void Chain_live_half_depth_wall_aisle_partner_is_walkable_then_attend_places()
    {
        // Chain: cover → boxed → live-depth aisle seat → place
        var day = Morning.CoverDay(null, autopilotOn: true, day: 1588, coveredDay: 1587);
        var plan = FullFixture(day);
        var wall = new ShelfSpot(995.5f, 54.5f, 90f, width: 2.97f, depth: 0.50f);
        var seat = StorageShelf.Seat(Shop(), new[] { wall }, Array.Empty<ShelfSpot>());
        var shelvesNeeded = ShelvesNeededFrom(plan, displayCap: 30, placedShelves: 1, boxedShelves: 1);
        var attend = StorageShelf.Attend(Street, 22, shelvesNeeded, inCargo: true, pending: false, cash: 292000m, seat, GameApi.Live());
        var partner = new ShelfSpot(seat.X, seat.Z, seat.Yaw, width: 2.97f, depth: 0.50f);

        Assert.Equal(ShelfAction.Place, attend.Action);
        Assert.Equal(996f, attend.Seat.X, 3);
        Assert.Equal(270f, attend.Seat.Yaw, 3);
        Assert.True(StorageShelf.Accessible(Shop(), wall, new[] { partner }));
    }

    [Fact]
    public void Chain_cover_place_live_one_plus_one_is_accessible()
    {
        // Chain: cover → place live 1+1 → Accessible
        var day = Morning.CoverDay(null, autopilotOn: true, day: 4, coveredDay: 3);
        var plan = FullFixture(day);
        var wall = new ShelfSpot(995.5f, 54.5f, 90f, width: 2.97f, depth: 0.50f);
        var seat = StorageShelf.Seat(Shop(), new[] { wall }, Array.Empty<ShelfSpot>());
        var shelvesNeeded = ShelvesNeededFrom(plan, displayCap: 30, placedShelves: 1, boxedShelves: 1);
        var attend = StorageShelf.Attend(Street, 22, shelvesNeeded, inCargo: true, pending: false, cash: 292000m, seat, GameApi.Live());
        var partner = new ShelfSpot(attend.Seat.X, attend.Seat.Z, attend.Seat.Yaw, wall.Width, wall.Depth);

        Assert.Equal(4, day);
        Assert.True(plan.Need > 0);
        Assert.Equal(ShelfAction.Place, attend.Action);
        Assert.Equal(995.5f + StorageShelf.AisleCenterStep(wall.Depth), attend.Seat.X, 3);
        Assert.Equal(270f, attend.Seat.Yaw, 3);
        Assert.True(StorageShelf.Accessible(Shop(), wall, new[] { partner }));
        Assert.True(StorageShelf.Accessible(Shop(), partner, new[] { wall }));
    }

    [Fact]
    public void Chain_cover_place_cargo_to_keep_then_sell_leaves_the_floor_shelf()
    {
        // Chain: cover → place cargo to keep → sell leaves the floor shelf
        var day = Morning.CoverDay(null, autopilotOn: true, day: 1589, coveredDay: 1588);
        var plan = FullFixture(day);
        var need = StorageShelf.NeedShelves(5, StorageShelf.SpacesNeeded(plan.OnHand, plan.Need), placedCap: 200);
        var keep = StorageShelf.KeepTarget(need, roomCanSpare: true, placed: 1);
        var placed = 1;
        var cargo = 12;
        var pending = 1;
        if (placed < keep && cargo > 0)
        {
            placed++;
            cargo--;
        }

        var have = StorageShelf.Have(placed, cargo, pending);
        var sell = StorageShelf.DecideSell(
            StorageShelf.Excess(have, keep),
            cargo,
            emptyPlaced: placed,
            stockedPlaced: 0,
            placed,
            keep);
        placed -= sell.EmptyPlaced;
        cargo -= sell.Cargo;
        var order = StorageShelf.OrderForKeep(keep, StorageShelf.Have(placed, cargo, pending));

        Assert.Equal(1589, day);
        Assert.True(plan.Need > 0);
        Assert.Equal(2, keep);
        Assert.Equal(2, placed);
        Assert.Equal(0, sell.EmptyPlaced);
        Assert.Equal(11, sell.Cargo);
        Assert.Equal(0, order);
    }

    [Fact]
    public void Chain_cover_box_need_keeps_the_placed_plus_one_and_does_not_sell_it()
    {
        // Chain: cover → box need → keep placed +1 → no sell
        var day = Morning.CoverDay(null, autopilotOn: true, day: 1601, coveredDay: 1600);
        var plan = FullFixture(day);
        var boxes = 5;
        var need = StorageShelf.NeedShelves(boxes, StorageShelf.SpacesNeeded(plan.OnHand, plan.Need), placedCap: 200);
        var placed = 2;
        var keep = StorageShelf.KeepTarget(need, roomCanSpare: false, placed: placed);
        var have = StorageShelf.Have(placed, cargo: 0, pending: 0);
        var sell = StorageShelf.DecideSell(
            StorageShelf.Excess(have, keep),
            cargo: 0,
            emptyPlaced: 1,
            stockedPlaced: 1,
            placed,
            keep);

        Assert.Equal(1601, day);
        Assert.True(plan.Need > 0);
        Assert.Equal(1, need);
        Assert.Equal(2, keep);
        Assert.Equal(0, sell.EmptyPlaced);
        Assert.Equal(0, sell.Cargo);
    }

    [Fact]
    public void Chain_cover_box_need_sells_shelves_above_the_spare_when_the_room_is_full()
    {
        // Chain: cover → box need → keep at need → sell the extras
        var day = Morning.CoverDay(null, autopilotOn: true, day: 1601, coveredDay: 1600);
        var plan = FullFixture(day);
        var need = StorageShelf.NeedShelves(5, StorageShelf.SpacesNeeded(plan.OnHand, plan.Need), placedCap: 200);
        var placed = 4;
        var keep = StorageShelf.KeepTarget(need, roomCanSpare: false, placed: placed);
        var have = StorageShelf.Have(placed, cargo: 0, pending: 0);
        var sell = StorageShelf.DecideSell(
            StorageShelf.Excess(have, keep),
            cargo: 0,
            emptyPlaced: 3,
            stockedPlaced: 1,
            placed,
            keep);

        Assert.Equal(1601, day);
        Assert.True(plan.Need > 0);
        Assert.Equal(1, keep);
        Assert.Equal(3, sell.EmptyPlaced);
    }

    [Fact]
    public void Chain_cover_reseat_same_yaw_spare_across_the_aisle_then_back_to_back()
    {
        // Chain: cover → reseat same-yaw spare across the aisle → third is back-to-back
        var day = Morning.CoverDay(null, autopilotOn: true, day: 1590, coveredDay: 1589);
        var floor = OpenRoom(994f, 1006f, 50f, 60f);
        var wall = new ShelfSpot(996.75f, 55f, 90f, width: 2.97f, depth: 0.50f);
        var jammed = new ShelfSpot(997.25f, 55f, 90f, width: 2.97f, depth: 0.50f);
        var reseat = StorageShelf.ShelvesToReseat(
            floor,
            new[] { wall, jammed },
            Array.Empty<ShelfSpot>(),
            new[] { 373, 0 });
        var aisle = StorageShelf.Seat(floor, new[] { wall }, Array.Empty<ShelfSpot>());
        var partner = new ShelfSpot(aisle.X, aisle.Z, aisle.Yaw, wall.Width, wall.Depth);
        var third = StorageShelf.Seat(floor, new[] { wall, partner }, Array.Empty<ShelfSpot>());

        Assert.Equal(1590, day);
        Assert.Single(reseat);
        Assert.Equal(997.25f, reseat[0].X, 3);
        Assert.True(aisle.Found, aisle.Reason);
        Assert.Equal(270f, aisle.Yaw, 3);
        Assert.Equal(55f, aisle.Z, 3);
        Assert.Equal(996.75f + StorageShelf.AisleCenterStep(wall.Depth), aisle.X, 3);
        Assert.True(third.Found, third.Reason);
        Assert.Equal(90f, third.Yaw, 3);
        Assert.Equal(55f, third.Z, 3);
        Assert.Equal(aisle.X + (wall.Depth * 2f), third.X, 3);
    }

    [Fact]
    public void Chain_cover_place_live_pair_keep_three_puts_the_third_against_the_spare()
    {
        // Chain: cover → place live 1+1 → keep 3 → third against the spare
        var day = Morning.CoverDay(null, autopilotOn: true, day: 1590, coveredDay: 1589);
        var floor = OpenRoom(994f, 1006f, 50f, 60f);
        var stocked = new ShelfSpot(996.75f, 55f, 90f, width: 2.97f, depth: 0.50f);
        var aisle = StorageShelf.Seat(floor, new[] { stocked }, Array.Empty<ShelfSpot>());
        var spare = new ShelfSpot(aisle.X, aisle.Z, aisle.Yaw, stocked.Width, stocked.Depth);
        var keep = StorageShelf.KeepTarget(need: 2, roomCanSpare: aisle.Found);
        var third = StorageShelf.Seat(floor, new[] { spare, stocked }, Array.Empty<ShelfSpot>());
        var body = StorageShelf.ShelfBody(stocked.Depth);

        Assert.Equal(1590, day);
        Assert.True(aisle.Found, aisle.Reason);
        Assert.Equal(270f, aisle.Yaw, 3);
        Assert.Equal(55f, aisle.Z, 3);
        Assert.Equal(stocked.X + StorageShelf.AisleCenterStep(stocked.Depth), aisle.X, 3);
        Assert.Equal(3, keep);
        Assert.True(third.Found, third.Reason);
        Assert.Equal(90f, third.Yaw, 3);
        Assert.Equal(55f, third.Z, 3);
        Assert.Equal(aisle.X + body, third.X, 3);
    }

    [Fact]
    public void Chain_cover_need_plus_one_shipping_spare_does_not_order()
    {
        // Chain: cover → box need → keep is need+1 → shipping holds the spare → no order → sell the pile once the floor is at keep
        var day = Morning.CoverDay(null, autopilotOn: true, day: 1605, coveredDay: 1604);
        var boxes = StorageShelf.BoxesNeeded(new[]
        {
            new ShelfStock("ba:itemname_cheapgift", 352, 300, 300),
            new ShelfStock("ba:itemname_cheapflower", 234, 40, 100),
            new ShelfStock("ba:itemname_sodacan", 262, 120, 500),
            new ShelfStock("ba:itemname_umbrella", 252, 200, 300),
        });
        var need = StorageShelf.NeedShelves(boxes, spacesNeeded: 0, placedCap: 0);
        var keep = StorageShelf.KeepTarget(need, roomCanSpare: true);
        var owned = StorageShelf.Have(placed: 1, cargo: 0, pending: 0, shipping: 90);
        var order = StorageShelf.OrderForKeep(keep, owned);
        var beforeSeat = StorageShelf.ShippingToSell(placed: 1, cargo: 0, pending: 0, shipping: 90, keep);
        var afterSeat = StorageShelf.ShippingToSell(placed: keep, cargo: 0, pending: 0, shipping: 90 - (keep - 1), keep);
        var due = StorageShelf.ContractDue(day, deliveryDay: 1602);

        Assert.Equal(1605, day);
        Assert.Equal(5, boxes);
        Assert.Equal(1, need);
        Assert.Equal(need + 1, keep);
        Assert.Equal(0, order);
        Assert.Equal(0, beforeSeat);
        Assert.Equal(89, afterSeat);
        Assert.True(due);
    }

    [Fact]
    public void Chain_cover_live_stocked_shelf_faces_a_partner_with_no_air_then_the_third()
    {
        // Chain: cover → place live 1+1 → facing gap 0 → third against that partner
        var day = Morning.CoverDay(null, autopilotOn: true, day: 1594, coveredDay: 1593);
        var floor = OpenRoom(994f, 1006f, 50f, 60f);
        var stocked = new ShelfSpot(996.75f, 55f, 90f, width: 2.97f, depth: 0.50f);
        var oldSpare = new ShelfSpot(998f, 55f, 270f, width: 2.97f, depth: 0.50f);
        var packed = StorageShelf.ShelvesToReseat(
            floor,
            new[] { stocked, oldSpare },
            Array.Empty<ShelfSpot>(),
            new[] { 373, 0 });
        var aisle = StorageShelf.Seat(floor, new[] { stocked }, Array.Empty<ShelfSpot>());
        var spare = new ShelfSpot(aisle.X, aisle.Z, aisle.Yaw, stocked.Width, stocked.Depth);
        var third = StorageShelf.Seat(floor, new[] { spare, stocked }, Array.Empty<ShelfSpot>());
        var body = StorageShelf.ShelfBody(stocked.Depth);

        Assert.Equal(1594, day);
        Assert.Equal(0f, StorageShelf.FacingGap);
        Assert.Single(packed);
        Assert.Equal(998f, packed[0].X, 3);
        Assert.True(aisle.Found, aisle.Reason);
        Assert.Equal(270f, aisle.Yaw, 3);
        Assert.Equal(stocked.X + stocked.Depth, aisle.X, 3);
        Assert.True(third.Found, third.Reason);
        Assert.Equal(90f, third.Yaw, 3);
        Assert.Equal(aisle.X + body, third.X, 3);
    }

    [Fact]
    public void Chain_cover_gap_file_reseat_then_the_spare_sits_on_the_surveyed_depth()
    {
        // Chain: cover → shelf gap file → reseat the 1 m spare → facing seat on the surveyed depth
        var day = Morning.CoverDay(null, autopilotOn: true, day: 1606, coveredDay: 1605);
        var floor = OpenRoom(994f, 1006f, 50f, 60f);
        var stocked = new ShelfSpot(996.75f, 55f, 90f, width: 2.97f, depth: 0.50f);
        var spare = new ShelfSpot(997.75f, 55f, 270f, width: 2.97f, depth: 0.50f);
        var pitch = StorageShelf.ApplyGap(StorageShelf.ParseGap("{ \"facingGap\": 0 }"));
        var packed = StorageShelf.ShelvesToReseat(
            floor,
            new[] { stocked, spare },
            Array.Empty<ShelfSpot>(),
            new[] { 373, 0 });
        var seat = StorageShelf.Seat(floor, new[] { stocked }, Array.Empty<ShelfSpot>());

        Assert.Equal(1606, day);
        Assert.Equal(0f, pitch);
        Assert.Single(packed);
        Assert.Equal(997.75f, packed[0].X, 3);
        Assert.Equal(270f, packed[0].Yaw, 3);
        Assert.True(seat.Found, seat.Reason);
        Assert.Equal(997.25f, seat.X, 3);
        Assert.Equal(55f, seat.Z, 3);
        Assert.Equal(270f, seat.Yaw, 3);
    }

    [Fact]
    public void Chain_cover_touching_pair_sold_one_then_gap_one_opens_one_body()
    {
        // Chain: cover → gap 0 touching seat → sell one → order the replacement → edit facingGap to 1 → seat one body out
        var day = Morning.CoverDay(null, autopilotOn: true, day: 1591, coveredDay: 1590);
        var floor = OpenRoom(994f, 1006f, 50f, 60f);
        var stocked = new ShelfSpot(996.75f, 55f, 90f, width: 2.97f, depth: 0.50f);
        var root = Path.Combine(Path.GetTempPath(), "chaoticceo-gap-" + Guid.NewGuid().ToString("N"));
        try
        {
            var touchPitch = StorageShelf.LoadGap(root);
            var touching = StorageShelf.Seat(floor, new[] { stocked }, Array.Empty<ShelfSpot>());
            var order = StorageShelf.OrderForKeep(keep: 2, StorageShelf.Have(placed: 1, cargo: 0, pending: 0));
            File.WriteAllText(StorageShelf.GapPath(root), "{ \"facingGap\": 1 }" + Environment.NewLine);
            var opened = StorageShelf.LoadGap(root);
            var wide = StorageShelf.Seat(floor, new[] { stocked }, Array.Empty<ShelfSpot>());
            var body = StorageShelf.ShelfBody(stocked.Depth);

            Assert.Equal(1591, day);
            Assert.Equal(0f, touchPitch);
            Assert.True(touching.Found, touching.Reason);
            Assert.Equal(stocked.X + stocked.Depth, touching.X, 3);
            Assert.Equal(270f, touching.Yaw, 3);
            Assert.Equal(1, order);
            Assert.Equal(1f, opened);
            Assert.Equal(
                "shelf.gap version=" + LoadLog.Version + " day=1591 facingGap=1 pitch=1",
                StorageShelf.GapLine(1591));
            Assert.True(wide.Found, wide.Reason);
            Assert.Equal(touching.X + (body - stocked.Depth), wide.X, 3);
            Assert.Equal(270f, wide.Yaw, 3);
        }
        finally
        {
            StorageShelf.ApplyGap(0f);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void Chain_cover_shelf_gap_file_then_the_facing_seat()
    {
        // Chain: cover → shelf gap file → facing seat
        var day = Morning.CoverDay(null, autopilotOn: true, day: 1594, coveredDay: 1593);
        var floor = OpenRoom(994f, 1006f, 50f, 60f);
        var stocked = new ShelfSpot(996.75f, 55f, 90f, width: 2.97f, depth: 0.50f);
        var body = StorageShelf.ShelfBody(stocked.Depth);
        try
        {
            var flush = StorageShelf.ApplyGap(StorageShelf.ParseGap("{ \"facingGap\": 0 }"));
            var touching = StorageShelf.Seat(floor, new[] { stocked }, Array.Empty<ShelfSpot>());
            var wide = StorageShelf.ApplyGap(StorageShelf.ParseGap("{ \"facingGap\": 1.25 }"));
            var spaced = StorageShelf.Seat(floor, new[] { stocked }, Array.Empty<ShelfSpot>());

            Assert.Equal(1594, day);
            Assert.Equal(0f, flush);
            Assert.True(touching.Found, touching.Reason);
            Assert.Equal(stocked.X + stocked.Depth, touching.X, 3);
            Assert.Equal(270f, touching.Yaw, 3);
            Assert.Equal(1.25f, wide);
            Assert.True(spaced.Found, spaced.Reason);
            Assert.Equal(stocked.X + (body * wide), spaced.X, 3);
            Assert.Equal(270f, spaced.Yaw, 3);
        }
        finally
        {
            StorageShelf.ApplyGap(0f);
        }
    }

    [Fact]
    public void Chain_cover_place_live_one_plus_one_then_the_back_to_back_seat()
    {
        // Chain: cover → place live 1+1 → back-to-back seat
        var day = Morning.CoverDay(null, autopilotOn: true, day: 4, coveredDay: 3);
        var plan = FullFixture(day);
        var wall = new ShelfSpot(995.5f, 54.5f, 90f, width: 2.97f, depth: 0.50f);
        var aisle = StorageShelf.Seat(Shop(), new[] { wall }, Array.Empty<ShelfSpot>());
        var shelvesNeeded = ShelvesNeededFrom(plan, displayCap: 30, placedShelves: 1, boxedShelves: 1);
        var attend = StorageShelf.Attend(Street, 22, shelvesNeeded, inCargo: true, pending: false, cash: 292000m, aisle, GameApi.Live());
        var across = new ShelfSpot(attend.Seat.X, attend.Seat.Z, attend.Seat.Yaw, wall.Width, wall.Depth);
        var next = StorageShelf.Seat(Shop(), new[] { wall, across }, Array.Empty<ShelfSpot>());

        Assert.Equal(4, day);
        Assert.True(plan.Need > 0);
        Assert.Equal(ShelfAction.Place, attend.Action);
        Assert.Equal(996f, attend.Seat.X, 3);
        Assert.True(next.Found, next.Reason);
        Assert.Equal(997f, next.X, 3);
        Assert.Equal(90f, next.Yaw, 3);
        Assert.True(StorageShelf.Accessible(Shop(), wall, new[] { across }));
    }

    [Fact]
    public void Chain_day_roll_cover_box_math_overflowing_one_shelf_orders_toward_keep()
    {
        // Chain: day roll → cover → box math → keep → attend (order one toward keep)
        var day = Morning.CoverDay(null, autopilotOn: true, day: 1598, coveredDay: 1597);
        var plan = FullFixture(day);
        var units = StorageShelf.SpacesNeeded(plan.OnHand, plan.Need);
        // Box size 1 makes every overflow unit its own box — more than 16 slots.
        var boxes = StorageShelf.BoxesNeeded(new[] { new ShelfStock(Umbrella, units, displayCap: 30, boxSize: 1) });
        var need = StorageShelf.NeedShelves(boxes, units, placedCap: 30);
        var keep = StorageShelf.KeepTarget(need, roomCanSpare: true);
        var have = StorageShelf.Have(placed: 1, cargo: 0, pending: 0);
        var order = StorageShelf.OrderForKeep(keep, have);
        var attend = StorageShelf.Attend(Street, 22, order, inCargo: false, pending: false, cash: 292000m, OpenSeat(), GameApi.Live());

        Assert.True(boxes > StorageShelf.Boxes);
        Assert.True(need >= 2);
        Assert.Equal(need + 1, keep);
        Assert.Equal(1, order);
        Assert.Equal(ShelfAction.Order, attend.Action);
        Assert.True(attend.Pick.Ordered);
        Assert.EndsWith(
            "verdict=short",
            StorageShelf.NeedLine(day, Street, 22, boxes, placedShelves: 1, usedSlots: 16, keep));
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
        Assert.Equal(996.5f, placed.Seat.X, 3);
        Assert.Equal(270f, placed.Seat.Yaw, 3);
        Assert.Equal(1573, nextDay);
        Assert.True(next.Found, next.Reason);
        Assert.Equal(998.5f, next.X, 3);
        Assert.Equal(90f, next.Yaw, 3);
    }
}
