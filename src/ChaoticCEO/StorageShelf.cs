using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class ShelfPick
    {
        public ShelfPick(
            bool ordered,
            string item,
            int amount,
            int price,
            string street,
            int number,
            string fromStreet,
            int fromNumber,
            string call)
        {
            Ordered = ordered;
            Item = item ?? "";
            Amount = amount;
            Price = price;
            Street = street ?? "";
            Number = number;
            FromStreet = fromStreet ?? "";
            FromNumber = fromNumber;
            Call = call ?? "";
        }

        public bool Ordered { get; }

        public string Item { get; }

        public int Amount { get; }

        public int Price { get; }

        public string Street { get; }

        public int Number { get; }

        public string FromStreet { get; }

        public int FromNumber { get; }

        public string Call { get; }

        public static ShelfPick None()
        {
            return new ShelfPick(false, "", 0, 0, "", 0, "", 0, "");
        }
    }

    public static class StorageShelf
    {
        public const string Item = "ba:itemname_storageshelf";

        public const int Price = 1200;

        /// <summary>Physical box slots on one storage shelf (game UI).</summary>
        public const int Boxes = 16;

        /// <summary>
        /// Retail units one placed storage shelf covers in buy math. Must match Hold/StandCap —
        /// never use <see cref="Boxes"/> here (that is slots, not gift/soda units).
        /// </summary>
        public const int UnitCapacity = 100;

        /// <summary>Live footprint along local X (long face). Matches the prefab (~2.97), not half-extents.</summary>
        public const float FootprintWidth = 3.0f;

        /// <summary>Live footprint along local Z (depth / short ends). Live collider is ~0.50.</summary>
        public const float FootprintDepth = 0.5f;

        /// <summary>
        /// Center spacing of facing shelves, from shelf-gap.json.
        /// Zero puts the long faces together: centers are one surveyed depth apart.
        /// Any number above zero is that center spacing in shelf bodies
        /// (1.25 is the previous layout). Back-to-back centers stay one shelf body apart.
        /// A zero gap does not move the width end of a run.
        /// </summary>
        public static float FacingGap { get; private set; }

        /// <summary>
        /// Facing centers, in shelf bodies, when <see cref="FacingGap"/> is above zero.
        /// Zero in the file leaves this at 0. <see cref="AisleCenterStep"/> then uses the surveyed depth.
        /// </summary>
        public static float AislePitch => FacingGap > 0f ? FacingGap : 0f;

        public const string GapFileName = "shelf-gap.json";

        /// <summary>Older clear-floor sample. Facing pitch is <see cref="AislePitch"/>.</summary>
        public const float AisleWidth = 1.0f;

        /// <summary>Walk-around corridor kept free along the shelf width to one wall.</summary>
        public const float HeaderLane = 1.0f;

        /// <summary>At most one back-to-back pair along depth (library stack half).</summary>
        public const int MaxBackToBack = 2;

        /// <summary>
        /// When false, Bridge.Seat returns hold-spike so Attend places and orders nothing.
        /// Layout slices turn this on after the buy-stop and survey ships.
        /// </summary>
        public static bool PlaceEnabled = true;

        public const string HamptonStreet = "ba:street_seventhavenue";

        public const int HamptonNumber = 13;

        /// <summary>
        /// Count boxed storage shelves on one cargo holder. Placed shelves are never holders.
        /// Nested cargo under a closed box counts the same as a top-level cargo entry.
        /// </summary>
        public static int BoxedInHolder(
            string? holderName,
            string? cargoName,
            int cargoAmount,
            IReadOnlyList<(string Name, int Amount)>? nested)
        {
            if (holderName == Item)
            {
                return 0;
            }

            var total = 0;
            if (cargoName == Item && cargoAmount > 0)
            {
                total += cargoAmount;
            }

            if (nested == null)
            {
                return total;
            }

            foreach (var entry in nested)
            {
                if (entry.Name == Item && entry.Amount > 0)
                {
                    total += entry.Amount;
                }
            }

            return total;
        }

        /// <summary>
        /// Retail units already in the store plus units on the next truck.
        /// That is the space Cover must leave room for — not "display looks full."
        /// </summary>
        public static int SpacesNeeded(int onHand, int inbound)
        {
            var left = onHand < 0 ? 0 : onHand;
            var due = inbound < 0 ? 0 : inbound;
            return left + due;
        }

        /// <summary>
        /// How many new storage shelves to buy. <paramref name="placedCapacity"/> is already in
        /// retail units (display + placed shelves × <see cref="UnitCapacity"/>). Boxed or
        /// pending-delivery shelves each add <see cref="UnitCapacity"/> so we place/wait before buying.
        /// </summary>
        public static int ShelvesToOrder(int spacesNeeded, int placedCapacity, int boxedShelves)
        {
            var boxed = boxedShelves < 0 ? 0 : boxedShelves;
            var capacity = placedCapacity + boxed * UnitCapacity;
            if (spacesNeeded <= capacity)
            {
                return 0;
            }

            var shortfall = spacesNeeded - capacity;
            return (shortfall + UnitCapacity - 1) / UnitCapacity;
        }

        /// <summary>
        /// Storage slots one item fills after the truck: units the displays cannot hold, in whole boxes.
        /// Delivery tops up displays first, then fills boxes of <paramref name="boxSize"/> on storage shelves.
        /// </summary>
        public static int StorageBoxes(int units, int displayCap, int boxSize)
        {
            if (boxSize <= 0)
            {
                return 0;
            }

            var overflow = units - (displayCap < 0 ? 0 : displayCap);
            return overflow <= 0 ? 0 : (overflow + boxSize - 1) / boxSize;
        }

        /// <summary>Sum of <see cref="StorageBoxes"/>, or -1 when any item has no box size (fall back to unit math).</summary>
        public static int BoxesNeeded(IReadOnlyList<ShelfStock>? stocks)
        {
            if (stocks == null)
            {
                return 0;
            }

            var total = 0;
            foreach (var stock in stocks)
            {
                if (stock.BoxSize <= 0)
                {
                    return -1;
                }

                total += stock.Boxes;
            }

            return total;
        }

        public static int ShelvesForBoxes(int boxes, int slotsPerShelf = Boxes)
        {
            if (boxes <= 0)
            {
                return 0;
            }

            var slots = slotsPerShelf <= 0 ? Boxes : slotsPerShelf;
            return (boxes + slots - 1) / slots;
        }

        /// <summary>Shelves to buy so every storage box has a slot. Boxed or pending shelves count as placed.</summary>
        public static int ShelvesToOrderByBoxes(int boxesNeeded, int placedShelves, int inboundShelves)
        {
            var have = (placedShelves < 0 ? 0 : placedShelves) + (inboundShelves < 0 ? 0 : inboundShelves);
            var missing = ShelvesForBoxes(boxesNeeded) - have;
            return missing > 0 ? missing : 0;
        }

        /// <summary>Box math when every box size is known; otherwise the older unit math.</summary>
        public static int ShelvesNeeded(int boxesNeeded, int placedShelves, int inboundShelves, int spacesNeeded, int placedCap)
        {
            return boxesNeeded >= 0
                ? ShelvesToOrderByBoxes(boxesNeeded, placedShelves, inboundShelves)
                : ShelvesToOrder(spacesNeeded, placedCap, inboundShelves);
        }

        /// <summary>
        /// Shelves to keep on the floor: <c>need</c>, plus one spare when the room still has a seat.
        /// A spare already on the floor (placed == need + 1) is kept even when the seat check is false,
        /// because that shelf is standing on the only aisle cell.
        /// More than that spare, with no seat, sells back down to <c>need</c>.
        /// </summary>
        public static int KeepTarget(int need, bool roomCanSpare, int placed = 0)
        {
            if (need <= 0)
            {
                return 0;
            }

            if (roomCanSpare || placed == need + 1)
            {
                return need + 1;
            }

            return need;
        }

        /// <summary>
        /// Placed + cargo + pending trucks + shelves already in the shipping area.
        /// A shelf in shipping is owned. It is not another purchase.
        /// </summary>
        public static int Have(int placed, int cargo, int pending, int shipping = 0)
        {
            var p = placed < 0 ? 0 : placed;
            var c = cargo < 0 ? 0 : cargo;
            var d = pending < 0 ? 0 : pending;
            var s = shipping < 0 ? 0 : shipping;
            return p + c + d + s;
        }

        /// <summary>
        /// The game only hands over a furniture contract when the clock hour is at or past
        /// the delivery hour. A later morning, still before that hour, never matches, so the
        /// contract sits forever. Once the delivery day has arrived, our morning owes it.
        /// </summary>
        public static bool ContractDue(int day, int deliveryDay)
        {
            return deliveryDay > 0 && day >= deliveryDay;
        }

        /// <summary>
        /// Empty shelves in the shipping area above <paramref name="keep"/>, after cargo
        /// excess is sold. Nothing is sold until the floor itself has reached <paramref name="keep"/>.
        /// </summary>
        public static int ShippingToSell(int placed, int cargo, int pending, int shipping, int keep)
        {
            if (shipping <= 0 || placed < keep)
            {
                return 0;
            }

            var excess = Excess(Have(placed, cargo, pending, shipping), keep);
            var fromCargo = cargo < 0 ? 0 : cargo;
            if (fromCargo > excess)
            {
                fromCargo = excess;
            }

            var rest = excess - fromCargo;
            if (rest < 0)
            {
                return 0;
            }

            return rest < shipping ? rest : shipping;
        }

        /// <summary>How many shelves sit above the keep target.</summary>
        public static int Excess(int have, int keep)
        {
            var h = have < 0 ? 0 : have;
            var k = keep < 0 ? 0 : keep;
            return h > k ? h - k : 0;
        }

        /// <summary>
        /// Empty shelves that are not the 1—aisle—2 seats grown from the shelf in use.
        /// A same-yaw shelf stacked on that face is packed so Place can seat it across the aisle.
        /// The next seat after that pair is back-to-back, with no aisle.
        /// A stocked shelf stays where it is.
        /// </summary>
        public static IReadOnlyList<ShelfSpot> ShelvesToReseat(
            ShelfFloor? floor,
            IReadOnlyList<ShelfSpot>? shelves,
            IReadOnlyList<ShelfSpot>? others,
            IReadOnlyList<int>? boxes)
        {
            if (floor == null || shelves == null || shelves.Count <= 1)
            {
                return System.Array.Empty<ShelfSpot>();
            }

            var anchor = PatternAnchor(shelves, boxes);
            var kept = new List<ShelfSpot> { anchor };
            var rest = new List<ShelfSpot>();
            for (var i = 0; i < shelves.Count; i++)
            {
                if (SameSpot(shelves[i], anchor))
                {
                    continue;
                }

                rest.Add(shelves[i]);
            }

            var off = new List<ShelfSpot>();
            var blockers = others ?? System.Array.Empty<ShelfSpot>();
            while (rest.Count > 0)
            {
                var seat = Seat(floor, kept, blockers);
                if (!seat.Found)
                {
                    off.AddRange(rest);
                    break;
                }

                var match = -1;
                for (var i = 0; i < rest.Count; i++)
                {
                    if (NearSeat(rest[i], seat))
                    {
                        match = i;
                        break;
                    }
                }

                if (match < 0)
                {
                    off.AddRange(rest);
                    break;
                }

                kept.Add(rest[match]);
                rest.RemoveAt(match);
            }

            var empty = new List<ShelfSpot>();
            foreach (var spot in off)
            {
                if (BoxesOn(shelves, boxes, spot) > 0)
                {
                    continue;
                }

                empty.Add(spot);
            }

            return empty;
        }

        public static ShelfSpot PatternAnchor(IReadOnlyList<ShelfSpot> shelves, IReadOnlyList<int>? boxes)
        {
            var best = -1;
            var most = 0;
            for (var i = 0; i < shelves.Count; i++)
            {
                var amount = BoxAt(boxes, i);
                if (amount > most)
                {
                    most = amount;
                    best = i;
                }
            }

            if (best >= 0)
            {
                return shelves[best];
            }

            for (var i = 0; i < shelves.Count; i++)
            {
                for (var j = 0; j < shelves.Count; j++)
                {
                    if (i != j && InFrontOf(shelves[i], shelves[j]))
                    {
                        return shelves[i];
                    }
                }
            }

            return shelves[0];
        }

        private static bool InFrontOf(ShelfSpot anchor, ShelfSpot other)
        {
            if (!SameYaw(anchor.Yaw, other.Yaw))
            {
                return false;
            }

            var (ax, az) = anchor.Center();
            var (bx, bz) = other.Center();
            var (lx, lz) = ToLocal(bx - ax, bz - az, anchor.Yaw);
            return System.Math.Abs(lx) <= Touch && lz > Touch;
        }

        private static bool SameSpot(ShelfSpot a, ShelfSpot b)
        {
            return System.Math.Abs(a.X - b.X) <= Touch
                && System.Math.Abs(a.Z - b.Z) <= Touch
                && SameYaw(a.Yaw, b.Yaw);
        }

        private static bool NearSeat(ShelfSpot spot, ShelfSeat seat)
        {
            return System.Math.Abs(spot.X - seat.X) <= Touch
                && System.Math.Abs(spot.Z - seat.Z) <= Touch
                && SameYaw(spot.Yaw, seat.Yaw);
        }

        private static int BoxesOn(IReadOnlyList<ShelfSpot> shelves, IReadOnlyList<int>? boxes, ShelfSpot spot)
        {
            for (var i = 0; i < shelves.Count; i++)
            {
                if (SameSpot(shelves[i], spot))
                {
                    return BoxAt(boxes, i);
                }
            }

            return 0;
        }

        private static int BoxAt(IReadOnlyList<int>? boxes, int index)
        {
            if (boxes == null || index < 0 || index >= boxes.Count)
            {
                return 0;
            }

            return boxes[index] < 0 ? 0 : boxes[index];
        }

        /// <summary>
        /// Full shelf thickness. A surveyed depth under 1 m is the half-extent (live ~0.50 → body 1).
        /// </summary>
        public static float ShelfBody(float depth)
        {
            if (depth < 0f)
            {
                return 0f;
            }

            return depth < 1f ? depth * 2f : depth;
        }

        /// <summary>
        /// Thickness used for a facing pair. Above zero, the doubled body.
        /// Zero in the file uses the surveyed depth so the long faces meet.
        /// </summary>
        public static float FacingThickness(float depth)
        {
            if (depth < 0f)
            {
                return 0f;
            }

            return FacingGap > 0f ? ShelfBody(depth) : depth;
        }

        /// <summary>Center-to-center distance of two shelves that face each other across the space.</summary>
        public static float AisleCenterStep(float depth)
        {
            var thickness = FacingThickness(depth);
            return FacingGap > 0f ? thickness * FacingGap : thickness;
        }

        public static float ParseGap(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0f;
            }

            var trimmed = text.Trim();
            if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var bare))
            {
                return bare;
            }

            var key = trimmed.IndexOf("facingGap", System.StringComparison.OrdinalIgnoreCase);
            if (key < 0)
            {
                return 0f;
            }

            var colon = trimmed.IndexOf(':', key);
            if (colon < 0)
            {
                return 0f;
            }

            var start = colon + 1;
            while (start < trimmed.Length && char.IsWhiteSpace(trimmed[start]))
            {
                start++;
            }

            var end = start;
            if (end < trimmed.Length && (trimmed[end] == '-' || trimmed[end] == '+'))
            {
                end++;
            }

            var sawDigit = false;
            while (end < trimmed.Length && (char.IsDigit(trimmed[end]) || trimmed[end] == '.'))
            {
                if (char.IsDigit(trimmed[end]))
                {
                    sawDigit = true;
                }

                end++;
            }

            if (!sawDigit)
            {
                return 0f;
            }

            if (float.TryParse(trimmed.Substring(start, end - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            return 0f;
        }

        /// <summary>Sets the file value and returns the pitch the seats will use.</summary>
        public static float ApplyGap(float facingGap)
        {
            FacingGap = facingGap;
            return AislePitch;
        }

        public static string GapPath(string? localAppData)
        {
            return Path.Combine(localAppData ?? "", LoadLog.ModId, GapFileName);
        }

        public static string DefaultGapText()
        {
            return "{" + System.Environment.NewLine
                + "  \"facingGap\": 0," + System.Environment.NewLine
                + "  \"note\": \"0 = faces touch. 1.25 = centers 1.25 shelf-bodies apart (the previous layout). Any number above 0 is that center spacing.\"" + System.Environment.NewLine
                + "}" + System.Environment.NewLine;
        }

        public static float LoadGap(string? localAppData)
        {
            if (string.IsNullOrEmpty(localAppData))
            {
                return ApplyGap(0f);
            }

            var path = GapPath(localAppData);
            try
            {
                if (!File.Exists(path))
                {
                    var directory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.WriteAllText(path, DefaultGapText());
                }

                return ApplyGap(ParseGap(File.ReadAllText(path)));
            }
            catch (System.Exception)
            {
                return ApplyGap(0f);
            }
        }

        public static string GapLine(int day)
        {
            return "shelf.gap version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " facingGap=" + FacingGap.ToString("0.##", CultureInfo.InvariantCulture)
                + " pitch=" + AislePitch.ToString("0.##", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// What we get back for one shelf. The game resale is below the purchase price.
        /// Crediting the purchase price makes a buy-then-return even.
        /// </summary>
        public static float Buyback(float gameSell)
        {
            if (gameSell < Price)
            {
                return Price;
            }

            return gameSell;
        }

        /// <summary>Buy at most one shelf per morning toward <paramref name="keep"/>.</summary>
        public static int OrderForKeep(int keep, int have)
        {
            var missing = (keep < 0 ? 0 : keep) - (have < 0 ? 0 : have);
            if (missing <= 0)
            {
                return 0;
            }

            return missing > 1 ? 1 : missing;
        }

        /// <summary>
        /// Keep surveyed/live collider size when it is already the full shelf.
        /// Only rewrite undersized half-extents up to <see cref="FootprintWidth"/> × <see cref="FootprintDepth"/>.
        /// </summary>
        public static (float Width, float Depth) ResolveFootprint(float width, float depth)
        {
            if (width < FootprintWidth - 0.1f)
            {
                return (FootprintWidth, FootprintDepth);
            }

            return (width, depth);
        }

        /// <summary>
        /// Sell cargo first, then empty placed shells. Stocked placed shelves are held, never sold.
        /// Sell only after Place has reached <paramref name="keep"/> on the floor.
        /// </summary>
        public static ShelfSellPlan DecideSell(
            int excess,
            int cargo,
            int emptyPlaced,
            int stockedPlaced,
            int placed,
            int keep)
        {
            if (excess <= 0)
            {
                return ShelfSellPlan.None();
            }

            // Do not sell freshly packed cargo (or empty shells) until Place has reseated keep.
            if (placed < keep)
            {
                return ShelfSellPlan.None();
            }

            var fromCargo = excess < cargo ? excess : (cargo < 0 ? 0 : cargo);
            if (fromCargo < 0)
            {
                fromCargo = 0;
            }

            var left = excess - fromCargo;
            var empty = emptyPlaced < 0 ? 0 : emptyPlaced;
            var fromEmpty = left < empty ? left : empty;
            // A truck that has not arrived is part of the excess, but it is not a floor shelf.
            // Never sell the floor below keep to cover it.
            var floorSpare = placed > keep ? placed - keep : 0;
            if (fromEmpty > floorSpare)
            {
                fromEmpty = floorSpare;
            }

            if (fromEmpty < 0)
            {
                fromEmpty = 0;
            }

            left -= fromEmpty;
            var holdStocked = left > 0 && stockedPlaced > 0;
            return new ShelfSellPlan(fromCargo, fromEmpty, holdStocked);
        }

        /// <summary>Need in shelves from box math, or unit math when box sizes are unknown.</summary>
        public static int NeedShelves(int boxesNeeded, int spacesNeeded, int placedCap)
        {
            if (boxesNeeded >= 0)
            {
                return ShelvesForBoxes(boxesNeeded);
            }

            return ShelvesToOrder(spacesNeeded, placedCap, boxedShelves: 0);
        }

        public static string BoxLine(ShelfStock stock, int day, string? street, int number)
        {
            return "shelf.box version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + (street ?? "")
                + " number=" + number.ToString(CultureInfo.InvariantCulture)
                + " item=" + stock.Item
                + " units=" + stock.Units.ToString(CultureInfo.InvariantCulture)
                + " displayCap=" + stock.DisplayCap.ToString(CultureInfo.InvariantCulture)
                + " boxSize=" + stock.BoxSize.ToString(CultureInfo.InvariantCulture)
                + " boxes=" + stock.Boxes.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Daily verdict: boxes the store needs in storage vs shelves placed. <c>keep</c> is need (+1 spare).
        /// <c>spare</c> is placed shelves beyond bare need; <c>verdict=short</c> means a delivery can be refunded.
        /// </summary>
        public static string NeedLine(int day, string? street, int number, int boxesNeeded, int placedShelves, int usedSlots, int keep)
        {
            var shelves = ShelvesForBoxes(boxesNeeded);
            var known = boxesNeeded >= 0;
            var verdict = !known ? "unknown" : shelves > placedShelves ? "short" : "enough";
            var spare = known ? placedShelves - shelves : 0;
            return "shelf.need version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + (street ?? "")
                + " number=" + number.ToString(CultureInfo.InvariantCulture)
                + " boxes=" + boxesNeeded.ToString(CultureInfo.InvariantCulture)
                + " shelves=" + shelves.ToString(CultureInfo.InvariantCulture)
                + " keep=" + keep.ToString(CultureInfo.InvariantCulture)
                + " placed=" + placedShelves.ToString(CultureInfo.InvariantCulture)
                + " slots=" + (placedShelves * Boxes).ToString(CultureInfo.InvariantCulture)
                + " used=" + usedSlots.ToString(CultureInfo.InvariantCulture)
                + " spare=" + spare.ToString(CultureInfo.InvariantCulture)
                + " verdict=" + verdict;
        }

        public static string KeepLine(
            int day,
            string? street,
            int number,
            int boxes,
            int need,
            int keep,
            int placed,
            int cargo,
            int pending,
            int have,
            bool spareSeats,
            int shipping = 0)
        {
            return "shelf.keep version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + (street ?? "")
                + " number=" + number.ToString(CultureInfo.InvariantCulture)
                + " boxes=" + boxes.ToString(CultureInfo.InvariantCulture)
                + " need=" + need.ToString(CultureInfo.InvariantCulture)
                + " keep=" + keep.ToString(CultureInfo.InvariantCulture)
                + " placed=" + placed.ToString(CultureInfo.InvariantCulture)
                + " cargo=" + cargo.ToString(CultureInfo.InvariantCulture)
                + " pending=" + pending.ToString(CultureInfo.InvariantCulture)
                + " have=" + have.ToString(CultureInfo.InvariantCulture)
                + " shipping=" + shipping.ToString(CultureInfo.InvariantCulture)
                + " spareSeats=" + (spareSeats ? "true" : "false");
        }

        public static string ArrivedLine(int day, string? street, int number, int amount)
        {
            return "shelf.arrived version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + (street ?? "")
                + " number=" + number.ToString(CultureInfo.InvariantCulture)
                + " item=" + Item
                + " amount=" + amount.ToString(CultureInfo.InvariantCulture);
        }

        public static string SoldLine(
            int day,
            string? street,
            int number,
            string where,
            int amount,
            float price,
            int keep,
            int haveAfter,
            string call)
        {
            return "shelf.sold version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + (street ?? "")
                + " number=" + number.ToString(CultureInfo.InvariantCulture)
                + " where=" + (where ?? "")
                + " amount=" + amount.ToString(CultureInfo.InvariantCulture)
                + " price=" + price.ToString("0.##", CultureInfo.InvariantCulture)
                + " keep=" + keep.ToString(CultureInfo.InvariantCulture)
                + " haveAfter=" + haveAfter.ToString(CultureInfo.InvariantCulture)
                + " call=" + (call ?? "");
        }

        public static string PackedLine(int day, string? street, int number, float x, float z, float yaw, string reason)
        {
            return "shelf.packed version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + (street ?? "")
                + " number=" + number.ToString(CultureInfo.InvariantCulture)
                + " x=" + x.ToString("0.00", CultureInfo.InvariantCulture)
                + " z=" + z.ToString("0.00", CultureInfo.InvariantCulture)
                + " yaw=" + yaw.ToString("0", CultureInfo.InvariantCulture)
                + " reason=" + (reason ?? "");
        }

        public static string ReseatLine(
            int day,
            string? street,
            int number,
            float fromX,
            float fromZ,
            float fromYaw,
            float toX,
            float toZ,
            float toYaw,
            int keep)
        {
            return "shelf.reseat version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + (street ?? "")
                + " number=" + number.ToString(CultureInfo.InvariantCulture)
                + " fromX=" + fromX.ToString("0.00", CultureInfo.InvariantCulture)
                + " fromZ=" + fromZ.ToString("0.00", CultureInfo.InvariantCulture)
                + " fromYaw=" + fromYaw.ToString("0", CultureInfo.InvariantCulture)
                + " toX=" + toX.ToString("0.00", CultureInfo.InvariantCulture)
                + " toZ=" + toZ.ToString("0.00", CultureInfo.InvariantCulture)
                + " toYaw=" + toYaw.ToString("0", CultureInfo.InvariantCulture)
                + " keep=" + keep.ToString(CultureInfo.InvariantCulture);
        }

        public static ShelfPick Order(
            string? street,
            int number,
            int shelvesNeeded,
            decimal cash,
            bool pending,
            bool inCargo,
            bool seat,
            ApiNote? api)
        {
            var call = api == null ? "" : api.Call("storage shelf");
            var amount = shelvesNeeded < 0 ? 0 : shelvesNeeded;
            var cost = (decimal)Price * amount;
            if (amount <= 0
                || cash < cost
                || pending
                || inCargo
                || !seat
                || string.IsNullOrEmpty(street)
                || number <= 0
                || call.Length == 0)
            {
                return ShelfPick.None();
            }

            return new ShelfPick(
                true,
                Item,
                amount,
                Price,
                street,
                number,
                HamptonStreet,
                HamptonNumber,
                call);
        }

        public static string Line(ShelfPick? pick, int day)
        {
            if (pick == null || !pick.Ordered)
            {
                return "";
            }

            return "shelf.ordered version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + pick.Street
                + " number=" + pick.Number.ToString(CultureInfo.InvariantCulture)
                + " item=" + pick.Item
                + " amount=" + pick.Amount.ToString(CultureInfo.InvariantCulture)
                + " price=" + pick.Price.ToString(CultureInfo.InvariantCulture)
                + " from=" + pick.FromStreet
                + " fromNumber=" + pick.FromNumber.ToString(CultureInfo.InvariantCulture)
                + " call=" + pick.Call;
        }

        public static string Record(string localAppData, ShelfPick? pick, int day)
        {
            var line = Line(pick, day);
            if (line.Length == 0)
            {
                return "";
            }

            var path = LoadLog.CeoLogPath(localAppData ?? "");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            Append(localAppData, line);
            return line;
        }

        public static ShelfAttend Attend(
            string? street,
            int number,
            int shelvesNeeded,
            bool inCargo,
            bool pending,
            decimal cash,
            ShelfSeat? seat,
            ApiNote? api)
        {
            seat ??= ShelfSeat.None("noseat");
            if (inCargo)
            {
                var place = api == null ? "" : api.Call("place shelf");
                return seat.Found && place.Length > 0
                    ? new ShelfAttend(ShelfAction.Place, street, number, true, seat, ShelfPick.None(), place, "")
                    : new ShelfAttend(ShelfAction.Hold, street, number, true, seat, ShelfPick.None(), "", seat.Found ? "nocall" : seat.Reason);
            }

            if (shelvesNeeded <= 0 || pending)
            {
                return new ShelfAttend(ShelfAction.None, street, number, false, seat, ShelfPick.None(), "", "");
            }

            if (!seat.Found)
            {
                return new ShelfAttend(ShelfAction.Hold, street, number, false, seat, ShelfPick.None(), "", seat.Reason);
            }

            var pick = Order(street, number, shelvesNeeded, cash, pending, inCargo, seat.Found, api);
            return new ShelfAttend(
                pick.Ordered ? ShelfAction.Order : ShelfAction.None,
                street,
                number,
                false,
                seat,
                pick,
                pick.Call,
                "");
        }

        public static string PlaceLine(ShelfAttend? attend, int day)
        {
            if (attend == null || attend.Action != ShelfAction.Place)
            {
                return "";
            }

            return "shelf.placed version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + attend.Street
                + " number=" + attend.Number.ToString(CultureInfo.InvariantCulture)
                + " item=" + Item
                + " x=" + attend.Seat.X.ToString("0.00", CultureInfo.InvariantCulture)
                + " z=" + attend.Seat.Z.ToString("0.00", CultureInfo.InvariantCulture)
                + " yaw=" + attend.Seat.Yaw.ToString("0", CultureInfo.InvariantCulture)
                + " call=" + attend.Call;
        }

        public static string HoldLine(ShelfAttend? attend, int day)
        {
            if (attend == null || attend.Action != ShelfAction.Hold)
            {
                return "";
            }

            return "shelf.held version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + attend.Street
                + " number=" + attend.Number.ToString(CultureInfo.InvariantCulture)
                + " item=" + Item
                + " inCargo=" + (attend.InCargo ? "true" : "false")
                + " reason=" + attend.Reason;
        }

        public static string RecordAttend(string localAppData, ShelfAttend? attend, int day)
        {
            var line = PlaceLine(attend, day);
            if (line.Length == 0)
            {
                line = HoldLine(attend, day);
            }

            if (line.Length == 0)
            {
                return "";
            }

            Append(localAppData, line);
            return line;
        }

        public static ShelfSeat Seat(ShelfFloor? floor, IReadOnlyList<ShelfSpot>? shelves, IReadOnlyList<ShelfSpot>? others)
        {
            if (shelves == null || shelves.Count == 0)
            {
                return ShelfSeat.None("norow");
            }

            if (floor == null || floor.Count == 0)
            {
                return ShelfSeat.None("nogrid");
            }

            var row = LargestRow(shelves);
            foreach (var shelf in row)
            {
                if (!floor.Covers(shelf))
                {
                    return ShelfSeat.None("offgrid");
                }
            }

            var first = row[0];
            var alongWidth = true;
            if (row.Count > 1)
            {
                var (dx, dz) = Delta(row);
                var (lx, lz) = ToLocal(dx, dz, first.Yaw);
                alongWidth = System.Math.Abs(lx) >= System.Math.Abs(lz);
            }

            var blockers = new List<ShelfSpot>(shelves);
            if (others != null)
            {
                blockers.AddRange(others);
            }

            // One shelf starts the 1—aisle—2 run. Face the spare across the aisle
            // before stepping a same-yaw neighbor along the long face.
            if (row.Count == 1)
            {
                var firstAisle = AisleEnds(floor, row, blockers);
                if (firstAisle != null)
                {
                    return firstAisle;
                }
            }

            if (alongWidth)
            {
                var step = first.Width;
                var (ax, az) = ToWorld(1f, 0f, first.Yaw);
                row.Sort((a, b) => Project(a, ax, az).CompareTo(Project(b, ax, az)));

                for (var i = 0; i + 1 < row.Count; i++)
                {
                    var gap = Project(row[i + 1], ax, az) - Project(row[i], ax, az);
                    if (gap >= (2f * step) - Touch)
                    {
                        var fill = WithYaw(row[i].Moved(ax * step, az * step), row[i].Yaw);
                        if (Accept(floor, fill, blockers, row))
                        {
                            return ShelfSeat.At(fill);
                        }
                    }
                }

                var end = EndsSameYaw(floor, row, blockers, ax, az, step);
                if (end != null)
                {
                    return end;
                }

                if (row.Count == 1)
                {
                    end = AisleEnds(floor, row, blockers);
                    if (end != null)
                    {
                        return end;
                    }
                }

                return ShelfSeat.None("full");
            }

            // An opposite pair cancels the facing vote. Start from the shelf whose back is
            // buried (the wall side). When both backs are open, start from the lower corner
            // so the spare — the shelf across the aisle — gets the next one against it.
            if (row.Count == 2 && OppositeYawPair(row[0].Yaw, row[1].Yaw))
            {
                var start = PairStart(floor, row, blockers);
                if (!SameSpot(row[0], start))
                {
                    (row[0], row[1]) = (row[1], row[0]);
                }
            }

            var facing = FacingAxis(row);
            row.Sort((a, b) => Project(a, facing.x, facing.z).CompareTo(Project(b, facing.x, facing.z)));

            var aisleFill = AisleGap(floor, row, blockers, facing.x, facing.z);
            if (aisleFill != null)
            {
                return aisleFill;
            }

            var aisleEnd = AisleEnds(floor, row, blockers);
            if (aisleEnd != null)
            {
                return aisleEnd;
            }

            // Depth pattern is full: extend a column along width when the room has a header lane.
            var widthEnd = WidthColumn(floor, row, blockers);
            return widthEnd ?? ShelfSeat.None("full");
        }

        private static ShelfSeat? EndsSameYaw(ShelfFloor floor, List<ShelfSpot> row, IReadOnlyList<ShelfSpot> blockers, float ax, float az, float step)
        {
            var high = WithYaw(row[row.Count - 1].Moved(ax * step, az * step), row[row.Count - 1].Yaw);
            if (Accept(floor, high, blockers, row) && LeavesHeader(floor, high, ax, az))
            {
                return ShelfSeat.At(high);
            }

            var low = WithYaw(row[0].Moved(-ax * step, -az * step), row[0].Yaw);
            if (Accept(floor, low, blockers, row) && LeavesHeader(floor, low, ax, az))
            {
                return ShelfSeat.At(low);
            }

            return null;
        }

        private static ShelfSeat? WidthColumn(ShelfFloor floor, List<ShelfSpot> row, IReadOnlyList<ShelfSpot> blockers)
        {
            var step = row[0].Width;
            var (ax, az) = ToWorld(1f, 0f, row[0].Yaw);
            foreach (var shelf in row)
            {
                var high = WithYaw(shelf.Moved(ax * step, az * step), shelf.Yaw);
                if (Accept(floor, high, blockers, row) && LeavesHeader(floor, high, ax, az))
                {
                    return ShelfSeat.At(high);
                }

                var low = WithYaw(shelf.Moved(-ax * step, -az * step), shelf.Yaw);
                if (Accept(floor, low, blockers, row) && LeavesHeader(floor, low, ax, az))
                {
                    return ShelfSeat.At(low);
                }
            }

            return null;
        }

        private static ShelfSeat? AisleEnds(ShelfFloor floor, List<ShelfSpot> row, IReadOnlyList<ShelfSpot> blockers)
        {
            var facing = FacingAxis(row);
            var high = AisleStep(row[row.Count - 1], facing.x, facing.z);
            if (Accept(floor, high, blockers, row))
            {
                return ShelfSeat.At(high);
            }

            var low = AisleStep(row[0], -facing.x, -facing.z);
            if (Accept(floor, low, blockers, row))
            {
                return ShelfSeat.At(low);
            }

            return null;
        }

        private static ShelfSeat? AisleGap(ShelfFloor floor, List<ShelfSpot> row, IReadOnlyList<ShelfSpot> blockers, float ax, float az)
        {
            for (var i = 0; i + 1 < row.Count; i++)
            {
                var fill = AisleStep(row[i], ax, az);
                var projected = Project(fill, ax, az);
                if (projected < Project(row[i + 1], ax, az) - Touch
                    && Accept(floor, fill, blockers, row))
                {
                    return ShelfSeat.At(fill);
                }
            }

            return null;
        }

        /// <summary>
        /// Next seat along the grow axis: a space when the face would be buried, otherwise back-to-back.
        /// Facing centers sit <see cref="AisleCenterStep"/> apart. Back-to-back centers stay one body apart.
        /// The width end of a run steps by shelf width and does not use this.
        /// </summary>
        internal static ShelfSpot AisleStep(ShelfSpot from, float growX, float growZ)
        {
            var (fx, fz) = Facing(from.Yaw);
            var toward = (fx * growX) + (fz * growZ);
            var yaw = OppositeYaw(from.Yaw);
            var body = ShelfBody(from.Depth);
            if (toward > 0.5f)
            {
                return WithYaw(from.Moved(growX * AisleCenterStep(from.Depth), growZ * AisleCenterStep(from.Depth)), yaw);
            }

            return WithYaw(from.Moved(growX * body, growZ * body), yaw);
        }

        /// <summary>
        /// Keep the world-space collider offset when the seat yaw flips (local axes rotate).
        /// </summary>
        private static ShelfSpot WithYaw(ShelfSpot spot, float yaw)
        {
            var (wx, wz) = ToWorld(spot.OffsetX, spot.OffsetZ, spot.Yaw);
            var (lx, lz) = ToLocal(wx, wz, yaw);
            return new ShelfSpot(spot.X, spot.Z, yaw, spot.Width, spot.Depth, lx, lz);
        }

        private static bool Accept(ShelfFloor floor, ShelfSpot seat, IReadOnlyList<ShelfSpot> blockers, IReadOnlyList<ShelfSpot> row)
        {
            if (!Clear(floor, seat, blockers) || !FrontAisleClear(floor, seat, blockers))
            {
                return false;
            }

            // Back-to-back only when the column still has fewer than MaxBackToBack shelves.
            foreach (var shelf in row)
            {
                if (!OppositeYawPair(shelf.Yaw, seat.Yaw))
                {
                    continue;
                }

                var (sx, sz) = shelf.Center();
                var (cx, cz) = seat.Center();
                var (lx, lz) = ToLocal(cx - sx, cz - sz, shelf.Yaw);
                if (System.Math.Abs(lx) > Touch || System.Math.Abs(System.Math.Abs(lz) - shelf.Depth) > Touch)
                {
                    continue;
                }

                if (ColumnCount(row, shelf) >= MaxBackToBack)
                {
                    return false;
                }
            }

            return true;
        }

        private static int ColumnCount(IReadOnlyList<ShelfSpot> row, ShelfSpot shelf)
        {
            var count = 0;
            var (sx, sz) = shelf.Center();
            foreach (var other in row)
            {
                var (ox, oz) = other.Center();
                var (lx, lz) = ToLocal(ox - sx, oz - sz, shelf.Yaw);
                if (System.Math.Abs(lx) > Touch)
                {
                    continue;
                }

                // Same seat or backs touching — never count a shelf across an aisle.
                if (System.Math.Abs(lz) <= Touch
                    || (OppositeYawPair(shelf.Yaw, other.Yaw)
                        && System.Math.Abs(System.Math.Abs(lz) - shelf.Depth) <= Touch))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>True when the long face has a clear aisle so a restocker can reach the shelf.</summary>
        public static bool Accessible(ShelfFloor? floor, ShelfSpot seat, IReadOnlyList<ShelfSpot>? blockers)
        {
            if (floor == null)
            {
                return false;
            }

            return FrontAisleClear(floor, seat, blockers ?? System.Array.Empty<ShelfSpot>());
        }

        private static bool FrontAisleClear(ShelfFloor floor, ShelfSpot seat, IReadOnlyList<ShelfSpot> blockers)
        {
            var (fx, fz) = Facing(seat.Yaw);
            var (cx, cz) = seat.Center();
            var (wx, wz) = ToWorld(1f, 0f, seat.Yaw);
            var halfWidth = System.Math.Max(0f, (seat.Width / 2f) - WallInset);
            var thickness = FacingThickness(seat.Depth);
            var air = AisleCenterStep(seat.Depth) - thickness;
            var face = thickness / 2f;
            // No air left: still sample the face, so a shelf standing on it stays blocked.
            var start = air > Sample ? Sample : 0f;
            var limit = air > Sample ? air - Sample + Touch : Touch;
            for (var along = start; along <= limit; along += Sample)
            {
                var depth = face + along;
                foreach (var lateral in Steps(halfWidth))
                {
                    var x = cx + (fx * depth) + (wx * lateral);
                    var z = cz + (fz * depth) + (wz * lateral);
                    if (!floor.Free(x, z))
                    {
                        return false;
                    }

                    foreach (var other in blockers)
                    {
                        // The facing mate and a back-to-back partner are the run, not an aisle blocker.
                        if (BehindOrBeside(seat, other) || FacingMate(seat, other) || !other.Contains(x, z, Touch))
                        {
                            continue;
                        }

                        return false;
                    }
                }
            }

            if (air <= Sample)
            {
                // A same-yaw shelf that starts on this face buries it. The facing mate does not.
                var buried = face + (thickness / 4f);
                foreach (var lateral in Steps(halfWidth))
                {
                    var x = cx + (fx * buried) + (wx * lateral);
                    var z = cz + (fz * buried) + (wz * lateral);
                    foreach (var other in blockers)
                    {
                        if (BehindOrBeside(seat, other) || FacingMate(seat, other) || !other.Contains(x, z, Touch))
                        {
                            continue;
                        }

                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>Opposite shelf whose long face meets this one. Not an end-of-run neighbor.</summary>
        private static bool FacingMate(ShelfSpot seat, ShelfSpot other)
        {
            if (!OppositeYawPair(seat.Yaw, other.Yaw))
            {
                return false;
            }

            var (cx, cz) = seat.Center();
            var (ox, oz) = other.Center();
            var (lx, lz) = ToLocal(ox - cx, oz - cz, seat.Yaw);
            if (System.Math.Abs(lx) > Touch || lz <= 0f)
            {
                return false;
            }

            return System.Math.Abs(lz - AisleCenterStep(seat.Depth)) <= Touch;
        }

        private static bool BehindOrBeside(ShelfSpot seat, ShelfSpot other)
        {
            var (cx, cz) = seat.Center();
            var (ox, oz) = other.Center();
            var (lx, lz) = ToLocal(ox - cx, oz - cz, seat.Yaw);
            // In front of the long face only matters for aisle clearance.
            return lz <= (seat.Depth / 2f) + Touch;
        }

        private static bool LeavesHeader(ShelfFloor floor, ShelfSpot seat, float ax, float az)
        {
            var (cx, cz) = seat.Center();
            var half = seat.Width / 2f;
            foreach (var sign in new[] { 1f, -1f })
            {
                var clear = true;
                for (var d = Sample; d <= HeaderLane + Touch; d += Sample)
                {
                    var x = cx + (ax * sign * (half + d));
                    var z = cz + (az * sign * (half + d));
                    if (!floor.Known(x, z) || !floor.Free(x, z))
                    {
                        clear = false;
                        break;
                    }
                }

                if (clear)
                {
                    return true;
                }
            }

            return false;
        }

        private static ShelfSpot PairStart(ShelfFloor floor, List<ShelfSpot> row, IReadOnlyList<ShelfSpot> blockers)
        {
            var buried = new List<ShelfSpot>();
            foreach (var shelf in row)
            {
                if (BackBuried(floor, shelf, blockers, row))
                {
                    buried.Add(shelf);
                }
            }

            if (buried.Count == 1)
            {
                return buried[0];
            }

            var start = row[0];
            for (var i = 1; i < row.Count; i++)
            {
                var shelf = row[i];
                if (shelf.X < start.X - Touch || (System.Math.Abs(shelf.X - start.X) <= Touch && shelf.Z < start.Z))
                {
                    start = shelf;
                }
            }

            return start;
        }

        /// <summary>True when a back-to-back partner on this shelf's back does not fit.</summary>
        private static bool BackBuried(ShelfFloor floor, ShelfSpot shelf, IReadOnlyList<ShelfSpot> blockers, IReadOnlyList<ShelfSpot> row)
        {
            var (fx, fz) = Facing(shelf.Yaw);
            var behind = AisleStep(shelf, -fx, -fz);
            return !Accept(floor, behind, blockers, row);
        }

        private static (float x, float z) FacingAxis(List<ShelfSpot> row)
        {
            var votesX = 0f;
            var votesZ = 0f;
            foreach (var shelf in row)
            {
                var (fx, fz) = Facing(shelf.Yaw);
                votesX += fx;
                votesZ += fz;
            }

            if ((votesX * votesX) + (votesZ * votesZ) < 0.01f)
            {
                return Facing(row[0].Yaw);
            }

            var length = (float)System.Math.Sqrt((votesX * votesX) + (votesZ * votesZ));
            return (votesX / length, votesZ / length);
        }

        private static (float x, float z) Facing(float yaw)
        {
            return ToWorld(0f, 1f, yaw);
        }

        internal static float OppositeYaw(float yaw)
        {
            return Normalize(yaw + 180f);
        }

        internal const float Touch = 0.05f;

        internal const float WallInset = 0.25f;

        private const float Sample = 0.125f;

        private const float RowLinkSteps = 4f;

        private static List<ShelfSpot> LargestRow(IReadOnlyList<ShelfSpot> shelves)
        {
            var seen = new bool[shelves.Count];
            var best = new List<ShelfSpot>();
            for (var start = 0; start < shelves.Count; start++)
            {
                if (seen[start])
                {
                    continue;
                }

                var group = new List<ShelfSpot>();
                var queue = new Queue<int>();
                queue.Enqueue(start);
                seen[start] = true;
                while (queue.Count > 0)
                {
                    var at = queue.Dequeue();
                    group.Add(shelves[at]);
                    for (var next = 0; next < shelves.Count; next++)
                    {
                        if (!seen[next] && Linked(shelves[at], shelves[next]))
                        {
                            seen[next] = true;
                            queue.Enqueue(next);
                        }
                    }
                }

                if (group.Count > best.Count)
                {
                    best = group;
                }
            }

            return best;
        }

        private static bool Linked(ShelfSpot a, ShelfSpot b)
        {
            var (ax, az) = a.Center();
            var (bx, bz) = b.Center();
            var (lx, lz) = ToLocal(bx - ax, bz - az, a.Yaw);
            if (SameYaw(a.Yaw, b.Yaw))
            {
                // Side-by-side along width only. Same-yaw depth stacks bury the long face — never one row.
                return System.Math.Abs(lz) <= Touch
                    && System.Math.Abs(lx) <= (RowLinkSteps * a.Width) + Touch;
            }

            if (!OppositeYawPair(a.Yaw, b.Yaw))
            {
                return false;
            }

            // Facing across an aisle or backs touching — same lateral lane.
            return System.Math.Abs(lx) <= Touch
                && System.Math.Abs(lz) <= (a.Depth + AisleWidth + a.Depth) + Touch;
        }

        public static string SurveyLine(float x, float z, float yaw, float width, float depth, int boxes, int day)
        {
            return "shelf.surveyed version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " item=" + Item
                + " x=" + x.ToString("0.00", CultureInfo.InvariantCulture)
                + " z=" + z.ToString("0.00", CultureInfo.InvariantCulture)
                + " yaw=" + yaw.ToString("0", CultureInfo.InvariantCulture)
                + " width=" + width.ToString("0.00", CultureInfo.InvariantCulture)
                + " depth=" + depth.ToString("0.00", CultureInfo.InvariantCulture)
                + " boxes=" + boxes.ToString(CultureInfo.InvariantCulture);
        }

        public static string RoomLine(float cellSize, float xMin, float xMax, float zMin, float zMax, int day)
        {
            return "shelf.room version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " cell=" + cellSize.ToString("0.00", CultureInfo.InvariantCulture)
                + " xMin=" + xMin.ToString("0.00", CultureInfo.InvariantCulture)
                + " xMax=" + xMax.ToString("0.00", CultureInfo.InvariantCulture)
                + " zMin=" + zMin.ToString("0.00", CultureInfo.InvariantCulture)
                + " zMax=" + zMax.ToString("0.00", CultureInfo.InvariantCulture);
        }

        public static string RecordSurvey(string localAppData, string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return "";
            }

            Append(localAppData, line);
            return line;
        }

        private static bool SameYaw(float a, float b)
        {
            var diff = System.Math.Abs(Normalize(a) - Normalize(b));
            return diff <= 1f || diff >= 359f;
        }

        private static bool OppositeYawPair(float a, float b)
        {
            var diff = System.Math.Abs(Normalize(a) - Normalize(b));
            return System.Math.Abs(diff - 180f) <= 1f;
        }

        private static float Normalize(float yaw)
        {
            var value = yaw % 360f;
            return value < 0f ? value + 360f : value;
        }

        private static (float dx, float dz) Delta(List<ShelfSpot> row)
        {
            var bestDx = 0f;
            var bestDz = 0f;
            var bestLength = -1f;
            for (var i = 0; i < row.Count; i++)
            {
                for (var j = i + 1; j < row.Count; j++)
                {
                    var (ix, iz) = row[i].Center();
                    var (jx, jz) = row[j].Center();
                    var dx = jx - ix;
                    var dz = jz - iz;
                    var length = (dx * dx) + (dz * dz);
                    if (length > bestLength)
                    {
                        bestLength = length;
                        bestDx = dx;
                        bestDz = dz;
                    }
                }
            }

            return (bestDx, bestDz);
        }

        private static float Project(ShelfSpot spot, float ax, float az)
        {
            var (cx, cz) = spot.Center();
            return (cx * ax) + (cz * az);
        }

        private static bool Clear(ShelfFloor floor, ShelfSpot seat, IReadOnlyList<ShelfSpot> blockers)
        {
            foreach (var (x, z) in Samples(seat, WallInset))
            {
                if (!floor.Free(x, z))
                {
                    return false;
                }
            }

            foreach (var other in blockers)
            {
                foreach (var (x, z) in Samples(seat, Touch))
                {
                    if (other.Contains(x, z, Touch))
                    {
                        return false;
                    }
                }

                foreach (var (x, z) in Samples(other, Touch))
                {
                    if (seat.Contains(x, z, Touch))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        internal static IEnumerable<(float x, float z)> Samples(ShelfSpot spot, float inset)
        {
            var (cx, cz) = spot.Center();
            var halfWidth = System.Math.Max(0f, (spot.Width / 2f) - inset);
            var halfDepth = System.Math.Max(0f, (spot.Depth / 2f) - inset);
            foreach (var lx in Steps(halfWidth))
            {
                foreach (var lz in Steps(halfDepth))
                {
                    var (wx, wz) = ToWorld(lx, lz, spot.Yaw);
                    yield return (cx + wx, cz + wz);
                }
            }
        }

        private static IEnumerable<float> Steps(float half)
        {
            var count = (int)System.Math.Ceiling((2f * half) / Sample);
            if (count <= 0)
            {
                yield return 0f;
                yield break;
            }

            for (var i = 0; i <= count; i++)
            {
                yield return -half + ((2f * half) * i / count);
            }
        }

        internal static (float x, float z) ToWorld(float lx, float lz, float yaw)
        {
            var radians = yaw * System.Math.PI / 180.0;
            var cos = (float)System.Math.Cos(radians);
            var sin = (float)System.Math.Sin(radians);
            return ((lx * cos) + (lz * sin), (-lx * sin) + (lz * cos));
        }

        internal static (float x, float z) ToLocal(float wx, float wz, float yaw)
        {
            var radians = yaw * System.Math.PI / 180.0;
            var cos = (float)System.Math.Cos(radians);
            var sin = (float)System.Math.Sin(radians);
            return ((wx * cos) - (wz * sin), (wx * sin) + (wz * cos));
        }

        private static void Append(string? localAppData, string line)
        {
            var path = LoadLog.CeoLogPath(localAppData ?? "");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + System.Environment.NewLine);
        }
    }

    public sealed class ShelfSellPlan
    {
        public ShelfSellPlan(int cargo, int emptyPlaced, bool holdStocked)
        {
            Cargo = cargo < 0 ? 0 : cargo;
            EmptyPlaced = emptyPlaced < 0 ? 0 : emptyPlaced;
            HoldStocked = holdStocked;
        }

        public int Cargo { get; }

        public int EmptyPlaced { get; }

        public bool HoldStocked { get; }

        public static ShelfSellPlan None()
        {
            return new ShelfSellPlan(0, 0, false);
        }
    }

    public sealed class ShelfStock
    {
        public ShelfStock(string? item, int units, int displayCap, int boxSize)
        {
            Item = item ?? "";
            Units = units < 0 ? 0 : units;
            DisplayCap = displayCap < 0 ? 0 : displayCap;
            BoxSize = boxSize;
        }

        public string Item { get; }

        /// <summary>Units in the store right after the next truck (on hand + inbound).</summary>
        public int Units { get; }

        /// <summary>Units the selling fixtures hold: ceil(boxSize × fixture cargoCapacityMultiplier) each.</summary>
        public int DisplayCap { get; }

        public int BoxSize { get; }

        public int Boxes => StorageShelf.StorageBoxes(Units, DisplayCap, BoxSize);
    }

    public enum ShelfAction
    {
        None,
        Order,
        Place,
        Hold,
    }

    public sealed class ShelfAttend
    {
        public ShelfAttend(
            ShelfAction action,
            string? street,
            int number,
            bool inCargo,
            ShelfSeat seat,
            ShelfPick pick,
            string call,
            string reason)
        {
            Action = action;
            Street = street ?? "";
            Number = number;
            InCargo = inCargo;
            Seat = seat;
            Pick = pick;
            Call = call ?? "";
            Reason = reason ?? "";
        }

        public ShelfAction Action { get; }

        public string Street { get; }

        public int Number { get; }

        public bool InCargo { get; }

        public ShelfSeat Seat { get; }

        public ShelfPick Pick { get; }

        public string Call { get; }

        public string Reason { get; }
    }

    public sealed class ShelfSeat
    {
        private ShelfSeat(bool found, float x, float z, float yaw, string reason)
        {
            Found = found;
            X = x;
            Z = z;
            Yaw = yaw;
            Reason = reason ?? "";
        }

        public bool Found { get; }

        public float X { get; }

        public float Z { get; }

        public float Yaw { get; }

        public string Reason { get; }

        public static ShelfSeat At(ShelfSpot spot)
        {
            return new ShelfSeat(true, spot.X, spot.Z, spot.Yaw, "");
        }

        public static ShelfSeat None(string reason)
        {
            return new ShelfSeat(false, 0f, 0f, 0f, reason);
        }
    }

    public sealed class ShelfSpot
    {
        public ShelfSpot(float x, float z, float yaw, float width, float depth, float offsetX = 0f, float offsetZ = 0f)
        {
            X = x;
            Z = z;
            Yaw = yaw;
            Width = width;
            Depth = depth;
            OffsetX = offsetX;
            OffsetZ = offsetZ;
        }

        public float X { get; }

        public float Z { get; }

        public float Yaw { get; }

        public float Width { get; }

        public float Depth { get; }

        public float OffsetX { get; }

        public float OffsetZ { get; }

        public (float x, float z) Center()
        {
            var (wx, wz) = StorageShelf.ToWorld(OffsetX, OffsetZ, Yaw);
            return (X + wx, Z + wz);
        }

        public ShelfSpot Moved(float dx, float dz)
        {
            return new ShelfSpot(X + dx, Z + dz, Yaw, Width, Depth, OffsetX, OffsetZ);
        }

        public bool Contains(float x, float z, float shrink)
        {
            var (cx, cz) = Center();
            var (lx, lz) = StorageShelf.ToLocal(x - cx, z - cz, Yaw);
            return System.Math.Abs(lx) < (Width / 2f) - shrink
                && System.Math.Abs(lz) < (Depth / 2f) - shrink;
        }
    }

    public sealed class ShelfCell
    {
        public ShelfCell(float x, float z, bool free)
        {
            X = x;
            Z = z;
            IsFree = free;
        }

        public float X { get; }

        public float Z { get; }

        public bool IsFree { get; }
    }

    public sealed class ShelfFloor
    {
        private readonly Dictionary<(int, int), bool> _cells = new Dictionary<(int, int), bool>();

        private readonly float _size;

        private readonly float _originX;

        private readonly float _originZ;

        public ShelfFloor(float size, IEnumerable<ShelfCell>? cells)
        {
            _size = size > 0f ? size : 0.25f;
            var first = true;
            foreach (var cell in cells ?? System.Array.Empty<ShelfCell>())
            {
                if (cell == null)
                {
                    continue;
                }

                if (first)
                {
                    _originX = cell.X;
                    _originZ = cell.Z;
                    first = false;
                }

                var key = Key(cell.X, cell.Z);
                _cells[key] = _cells.TryGetValue(key, out var free) ? free && cell.IsFree : cell.IsFree;
            }
        }

        public int Count => _cells.Count;

        public bool Known(float x, float z)
        {
            return _cells.ContainsKey(Key(x, z));
        }

        public bool Free(float x, float z)
        {
            return _cells.TryGetValue(Key(x, z), out var free) && free;
        }

        public bool Covers(ShelfSpot spot)
        {
            foreach (var (x, z) in StorageShelf.Samples(spot, StorageShelf.WallInset))
            {
                if (!Known(x, z))
                {
                    return false;
                }
            }

            return true;
        }

        private (int, int) Key(float x, float z)
        {
            return (
                (int)System.Math.Floor(((x - _originX) / _size) + 0.5f),
                (int)System.Math.Floor(((z - _originZ) / _size) + 0.5f));
        }
    }
}
