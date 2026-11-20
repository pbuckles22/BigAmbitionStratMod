using System.Collections.Generic;
using System.Reflection;
using BigAmbitions.Items;
using Blueprints;
using Buildings.Indoors;
using Extensions;
using Helpers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChaoticCEO
{
    public static class StorageShelfBridge
    {
        private static readonly Dictionary<string, ShelfSpot?> Sizes = new Dictionary<string, ShelfSpot?>();

        private static MethodInfo? _colliderSize;

        public static bool InCargo(BuildingRegistration? building)
        {
            return BoxedCount(building) > 0;
        }

        public static int BoxedCount(BuildingRegistration? building)
        {
            if (building?.itemInstances == null)
            {
                return 0;
            }

            var total = 0;
            foreach (var item in building.itemInstances.Values)
            {
                // Placed storage shelves hold product, not boxed furniture — skip those holders.
                // Closed boxes / hand trucks / crates may nest a Storage Shelf under cargo.
                if (item?.cargoInstances == null || item.itemName == StorageShelf.Item)
                {
                    continue;
                }

                foreach (var cargo in item.cargoInstances)
                {
                    if (cargo == null)
                    {
                        continue;
                    }

                    if (cargo.itemName == StorageShelf.Item && cargo.amount > 0)
                    {
                        total += cargo.amount;
                    }

                    if (cargo.nestedCargoInstances == null)
                    {
                        continue;
                    }

                    foreach (var nested in cargo.nestedCargoInstances)
                    {
                        if (nested != null && nested.itemName == StorageShelf.Item && nested.amount > 0)
                        {
                            total += nested.amount;
                        }
                    }
                }
            }

            return total;
        }

        public static int PlacedCount(BuildingRegistration? building)
        {
            if (building?.itemInstances == null)
            {
                return 0;
            }

            var total = 0;
            foreach (var item in building.itemInstances.Values)
            {
                if (item != null
                    && item.itemName == StorageShelf.Item
                    && string.IsNullOrEmpty(item.parentId))
                {
                    total++;
                }
            }

            return total;
        }

        /// <summary>Storage shelves parented to the shipping pallet. They are owned and not on the floor.</summary>
        public static int ShippingCount(BuildingRegistration? building)
        {
            if (building?.itemInstances == null)
            {
                return 0;
            }

            var total = 0;
            foreach (var item in building.itemInstances.Values)
            {
                if (item != null
                    && item.itemName == StorageShelf.Item
                    && !string.IsNullOrEmpty(item.parentId))
                {
                    total++;
                }
            }

            return total;
        }

        /// <summary>Move one shipping-area shelf onto <paramref name="seat"/> and clear its parent.</summary>
        public static bool MoveShipping(BuildingRegistration building, ShelfSeat seat, out string reason)
        {
            reason = "";
            if (!seat.Found)
            {
                reason = seat.Reason;
                return false;
            }

            if (building.itemInstances == null)
            {
                reason = "noshipping";
                return false;
            }

            ItemInstance? shelf = null;
            foreach (var item in building.itemInstances.Values)
            {
                if (item != null
                    && item.itemName == StorageShelf.Item
                    && !string.IsNullOrEmpty(item.parentId))
                {
                    shelf = item;
                    break;
                }
            }

            if (shelf == null)
            {
                reason = "noshipping";
                return false;
            }

            try
            {
                shelf.parentId = "";
                shelf.position = new SerializableVector3(seat.X, RowHeight(building, shelf), seat.Z);
                shelf.yRotation = seat.Yaw;
                Show(building, shelf);
                GameEvent.Invoke("ba:gameevent_itemcargochanged");
                SaveGameManager.MarkChange();
                return true;
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "StorageShelfBridge.MoveShipping.1", "");
                reason = "error:" + ex.GetType().Name;
                return false;
            }
        }

        public static int SellShipping(BuildingRegistration? building, int amount, out float price)
        {
            price = 0f;
            if (building?.itemInstances == null || amount <= 0)
            {
                return 0;
            }

            var sold = 0;
            var doomed = new List<ItemInstance>();
            foreach (var item in building.itemInstances.Values)
            {
                if (sold + doomed.Count >= amount)
                {
                    break;
                }

                if (item == null
                    || item.itemName != StorageShelf.Item
                    || string.IsNullOrEmpty(item.parentId)
                    || (item.cargoInstances != null && item.cargoInstances.Count > 0))
                {
                    continue;
                }

                doomed.Add(item);
            }

            foreach (var item in doomed)
            {
                if (sold >= amount)
                {
                    break;
                }

                var credit = StorageShelf.Buyback(item.GetSellingPrice());
                Hide(building, item);
                building.RemoveItemInstanceFromBuilding(item, true);
                price += credit;
                sold++;
                Credit(credit, "ba:transaction_itemsold");
            }

            if (sold > 0)
            {
                GameEvent.Invoke("ba:gameevent_itemcargochanged");
                SaveGameManager.MarkChange();
            }

            return sold;
        }

        /// <summary>Units per delivered box for one product, or 0 when the item is unknown.</summary>
        public static int BoxSize(string? item)
        {
            if (string.IsNullOrEmpty(item))
            {
                return 0;
            }

            try
            {
                var cached = ItemsGetter.GetByName(item, true);
                return cached == null ? 0 : cached.boxSize;
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "StorageShelfBridge.BoxSize.1", "");
                return 0;
            }
        }

        /// <summary>
        /// Units the selling fixtures hold for one product: ceil(boxSize × cargoCapacityMultiplier) per fixture,
        /// the same number the game's restock and delivery fill to.
        /// </summary>
        public static int DisplayCapacity(BuildingRegistration? building, string? item)
        {
            var boxSize = BoxSize(item);
            if (building?.itemInstances == null || boxSize <= 0)
            {
                return 0;
            }

            var total = 0;
            foreach (var placed in building.itemInstances.Values)
            {
                var cached = placed?.ItemCached;
                if (cached == null
                    || !string.IsNullOrEmpty(placed!.parentId)
                    || (cached.type & (ItemType.PointOfSale | ItemType.ShowcaseShelf)) == 0)
                {
                    continue;
                }

                var stock = placed.cargoInstances != null && placed.cargoInstances.Count == 1
                    ? placed.cargoInstances[0]?.itemName
                    : null;
                var sold = string.IsNullOrEmpty(stock) ? placed.playerItemPurchaserSettings?.itemName : stock;
                if (sold != item)
                {
                    continue;
                }

                total += Mathf.CeilToInt(boxSize * cached.cargoCapacityMultiplier);
            }

            return total;
        }

        /// <summary>Storage slots holding a box right now, across placed storage shelves.</summary>
        public static int UsedSlots(BuildingRegistration? building)
        {
            if (building?.itemInstances == null)
            {
                return 0;
            }

            var used = 0;
            foreach (var item in building.itemInstances.Values)
            {
                if (item?.cargoInstances != null
                    && item.itemName == StorageShelf.Item
                    && string.IsNullOrEmpty(item.parentId))
                {
                    used += item.cargoInstances.Count;
                }
            }

            return used;
        }

        public static ShelfSeat Seat(BuildingRegistration? building)
        {
            if (!StorageShelf.PlaceEnabled)
            {
                return ShelfSeat.None("hold-spike");
            }

            if (building?.itemInstances == null)
            {
                return ShelfSeat.None("nobuilding");
            }

            var floor = Floor(building, out var reason);
            if (floor == null)
            {
                return ShelfSeat.None(reason);
            }

            var shelves = new List<ShelfSpot>();
            var others = new List<ShelfSpot>();
            foreach (var item in building.itemInstances.Values)
            {
                var spot = Footprint(item);
                if (spot == null)
                {
                    continue;
                }

                if (item.itemName == StorageShelf.Item)
                {
                    shelves.Add(spot);
                }
                else
                {
                    others.Add(spot);
                }
            }

            return StorageShelf.Seat(floor, shelves, others);
        }

        private static bool _surveyed;

        public static int Survey(string? localAppData, BuildingRegistration? building, int day)
        {
            if (_surveyed || string.IsNullOrEmpty(localAppData) || building?.itemInstances == null)
            {
                return 0;
            }

            _surveyed = true;
            var written = 0;
            var floor = Floor(building, out _);
            if (floor != null && floor.Count > 0)
            {
                float xMin = 0f, xMax = 0f, zMin = 0f, zMax = 0f;
                var first = true;
                foreach (var item in building.itemInstances.Values)
                {
                    var spot = Footprint(item);
                    if (spot == null)
                    {
                        continue;
                    }

                    var (cx, cz) = spot.Center();
                    if (first)
                    {
                        xMin = xMax = cx;
                        zMin = zMax = cz;
                        first = false;
                    }
                    else
                    {
                        xMin = Mathf.Min(xMin, cx);
                        xMax = Mathf.Max(xMax, cx);
                        zMin = Mathf.Min(zMin, cz);
                        zMax = Mathf.Max(zMax, cz);
                    }
                }

                if (!first)
                {
                    StorageShelf.RecordSurvey(
                        localAppData,
                        StorageShelf.RoomLine(0.25f, xMin, xMax, zMin, zMax, day));
                    written++;
                }
            }

            foreach (var item in building.itemInstances.Values)
            {
                if (item == null
                    || item.itemName != StorageShelf.Item
                    || !string.IsNullOrEmpty(item.parentId))
                {
                    continue;
                }

                var size = Size(item) ?? new ShelfSpot(0f, 0f, 0f, StorageShelf.FootprintWidth, StorageShelf.FootprintDepth);
                var boxes = 0;
                if (item.cargoInstances != null)
                {
                    foreach (var cargo in item.cargoInstances)
                    {
                        if (cargo != null && cargo.amount > 0)
                        {
                            boxes += cargo.amount;
                        }
                    }
                }

                StorageShelf.RecordSurvey(
                    localAppData,
                    StorageShelf.SurveyLine(
                        item.position.x,
                        item.position.z,
                        item.yRotation,
                        size.Width,
                        size.Depth,
                        boxes,
                        day));
                written++;
            }

            return written;
        }

        public static void ResetSurvey()
        {
            _surveyed = false;
        }

        public static bool Place(BuildingRegistration building, ShelfSeat seat, out string reason)
        {
            reason = "";
            if (!seat.Found)
            {
                reason = seat.Reason;
                return false;
            }

            if (!Box(building, out var holder, out var box) || holder == null || box == null)
            {
                reason = "nocargo";
                return false;
            }

            try
            {
                var placed = box.InitializeNewInstance();
                placed.position = new SerializableVector3(seat.X, RowHeight(building, holder), seat.Z);
                placed.yRotation = seat.Yaw;
                if (box.amount > 1)
                {
                    box.amount -= 1;
                }
                else
                {
                    holder.RemoveFromCargo(box);
                }

                building.AddItemInstanceToBuilding(placed);
                Show(building, placed);
                GameEvent.Invoke("ba:gameevent_itemcargochanged");
                return true;
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "StorageShelfBridge.Place.1", "");
                reason = "error:" + ex.GetType().Name;
                return false;
            }
        }

        public static int EmptyPlacedCount(BuildingRegistration? building)
        {
            return CountPlaced(building, stocked: false);
        }

        public static int StockedPlacedCount(BuildingRegistration? building)
        {
            return CountPlaced(building, stocked: true);
        }

        /// <summary>
        /// True when a library row grown from the best anchor (other furniture only as blockers)
        /// can hold <paramref name="need"/> + 1 shelves.
        /// </summary>
        public static bool CanSpare(BuildingRegistration? building, int need)
        {
            if (need <= 0)
            {
                return false;
            }

            if (building?.itemInstances == null)
            {
                return true;
            }

            var floor = Floor(building, out _);
            if (floor == null)
            {
                return true;
            }

            SplitFootprints(building, out var shelves, out var others);
            if (shelves.Count == 0)
            {
                return true;
            }

            var anchor = BestAnchor(floor, shelves, others);
            var row = new List<ShelfSpot> { anchor };
            while (row.Count < need + 1)
            {
                var next = StorageShelf.Seat(floor, row, others);
                if (!next.Found)
                {
                    return false;
                }

                var size = shelves[0];
                row.Add(new ShelfSpot(next.X, next.Z, next.Yaw, size.Width, size.Depth, size.OffsetX, size.OffsetZ));
            }

            return true;
        }

        public static int SellCargo(BuildingRegistration? building, int amount, out float price)
        {
            price = 0f;
            if (building == null || amount <= 0)
            {
                return 0;
            }

            var sold = 0;
            while (sold < amount)
            {
                if (!Box(building, out var holder, out var box) || holder == null || box == null)
                {
                    break;
                }

                var stack = box.amount <= 0 ? 1 : box.amount;
                var take = amount - sold;
                if (take > stack)
                {
                    take = stack;
                }

                var full = box.GetSellingPrice();
                var unit = stack <= 0 ? full : full / stack;
                var credit = StorageShelf.Buyback(unit) * take;
                if (take >= stack)
                {
                    holder.RemoveFromCargo(box);
                }
                else
                {
                    box.amount -= take;
                }

                price += credit;
                sold += take;
                Credit(credit, "ba:transaction_cargoholderinventorysold");
            }

            if (sold > 0)
            {
                GameEvent.Invoke("ba:gameevent_itemcargochanged");
                SaveGameManager.MarkChange();
            }

            return sold;
        }

        public static int SellEmptyPlaced(BuildingRegistration? building, int amount, out float price)
        {
            price = 0f;
            if (building?.itemInstances == null || amount <= 0)
            {
                return 0;
            }

            var sold = 0;
            var doomed = new List<ItemInstance>();
            foreach (var item in building.itemInstances.Values)
            {
                if (sold + doomed.Count >= amount)
                {
                    break;
                }

                if (item == null
                    || item.itemName != StorageShelf.Item
                    || !string.IsNullOrEmpty(item.parentId)
                    || (item.cargoInstances != null && item.cargoInstances.Count > 0))
                {
                    continue;
                }

                doomed.Add(item);
            }

            foreach (var item in doomed)
            {
                if (sold >= amount)
                {
                    break;
                }

                var credit = StorageShelf.Buyback(item.GetSellingPrice());
                Hide(building, item);
                building.RemoveItemInstanceFromBuilding(item, true);
                price += credit;
                sold++;
                Credit(credit, "ba:transaction_itemsold");
            }

            if (sold > 0)
            {
                GameEvent.Invoke("ba:gameevent_itemcargochanged");
                SaveGameManager.MarkChange();
            }

            return sold;
        }

        /// <summary>
        /// Pack blocked and excess empty shelves into cargo so Place can reseat the keep set.
        /// Leaves the best accessible anchor on the floor when possible.
        /// </summary>
        public static int PackForReseat(
            BuildingRegistration? building,
            int keep,
            out List<(float x, float z, float yaw, string reason)> packed)
        {
            packed = new List<(float x, float z, float yaw, string reason)>();
            if (building?.itemInstances == null || keep < 0)
            {
                return 0;
            }

            var floor = Floor(building, out _);
            SplitFootprints(building, out var shelves, out var others);
            ItemInstance? anchor = null;
            if (floor != null && shelves.Count > 0)
            {
                var boxes = BoxCounts(building, shelves);
                var home = StorageShelf.PatternAnchor(shelves, boxes);
                if (home == null)
                {
                    home = BestAnchor(floor, shelves, others);
                }

                anchor = FindPlaced(building, home.X, home.Z, home.Yaw);
                foreach (var spot in StorageShelf.ShelvesToReseat(floor, shelves, others, boxes))
                {
                    var item = FindPlaced(building, spot.X, spot.Z, spot.Yaw);
                    if (item == null || item == anchor)
                    {
                        continue;
                    }

                    if (item.cargoInstances != null && item.cargoInstances.Count > 0)
                    {
                        continue;
                    }

                    if (!PackOne(building, item, out var rx, out var rz, out var ryaw))
                    {
                        continue;
                    }

                    packed.Add((rx, rz, ryaw, "pattern"));
                    SplitFootprints(building, out shelves, out others);
                }
            }

            // Pack every empty non-anchor shelf that fails accessibility.
            foreach (var item in PlacedList(building))
            {
                if (item == anchor)
                {
                    continue;
                }

                if (item.cargoInstances != null && item.cargoInstances.Count > 0)
                {
                    continue;
                }

                var spot = Footprint(item);
                if (floor == null || spot == null)
                {
                    continue;
                }

                var blockers = BlockersExcept(shelves, others, spot);
                if (StorageShelf.Accessible(floor, spot, blockers))
                {
                    continue;
                }

                if (!PackOne(building, item, out var px, out var pz, out var pyaw))
                {
                    continue;
                }

                packed.Add((px, pz, pyaw, "blocked"));
                SplitFootprints(building, out shelves, out others);
            }

            // Pack empty non-anchor shelves while above keep.
            while (PlacedCount(building) > keep)
            {
                ItemInstance? victim = null;
                foreach (var item in PlacedList(building))
                {
                    if (item == anchor)
                    {
                        continue;
                    }

                    if (item.cargoInstances != null && item.cargoInstances.Count > 0)
                    {
                        continue;
                    }

                    victim = item;
                    break;
                }

                if (victim == null)
                {
                    break;
                }

                if (!PackOne(building, victim, out var px, out var pz, out var pyaw))
                {
                    break;
                }

                packed.Add((px, pz, pyaw, "excess"));
            }

            return packed.Count;
        }

        private static int CountPlaced(BuildingRegistration? building, bool stocked)
        {
            if (building?.itemInstances == null)
            {
                return 0;
            }

            var total = 0;
            foreach (var item in building.itemInstances.Values)
            {
                if (item == null
                    || item.itemName != StorageShelf.Item
                    || !string.IsNullOrEmpty(item.parentId))
                {
                    continue;
                }

                var has = item.cargoInstances != null && item.cargoInstances.Count > 0;
                if (has == stocked)
                {
                    total++;
                }
            }

            return total;
        }

        private static void Credit(float amount, string transaction)
        {
            if (amount == 0f)
            {
                return;
            }

            var data = new Dictionary<string, string> { ["itemSoldInfo"] = StorageShelf.Item };
            var info = new TransactionInfo(transaction, data, isTaxDeductible: false);
            GameManager.ChangeMoneySafe(amount, info);
        }

        private static void Hide(BuildingRegistration building, ItemInstance item)
        {
            var manager = InstanceBehavior<BuildingManager>.Instance;
            if (!BuildingManager.IsInsideBuilding || manager == null || manager.buildingRegistration != building)
            {
                return;
            }

            var all = manager.allItemControllers;
            if (all == null)
            {
                return;
            }

            for (var i = all.Count - 1; i >= 0; i--)
            {
                var controller = all[i];
                if (controller == null || controller.ItemInstance != item)
                {
                    continue;
                }

                all.RemoveAt(i);
                Object.Destroy(controller.gameObject);
            }
        }

        private static bool PackOne(BuildingRegistration building, ItemInstance item, out float x, out float z, out float yaw)
        {
            x = item.position.x;
            z = item.position.z;
            yaw = item.yRotation;
            if (!CargoHolder(building, out var holder) || holder == null)
            {
                return false;
            }

            try
            {
                var cargo = item.ConvertToCargoInstance();
                Hide(building, item);
                building.RemoveItemInstanceFromBuilding(item, true);
                holder.AddToCargo(cargo);
                GameEvent.Invoke("ba:gameevent_itemcargochanged");
                SaveGameManager.MarkChange();
                return true;
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "StorageShelfBridge.PackOne.1", "");
                return false;
            }
        }

        private static bool CargoHolder(BuildingRegistration building, out ItemInstance? holder)
        {
            holder = null;
            if (building.itemInstances == null)
            {
                return false;
            }

            foreach (var item in building.itemInstances.Values)
            {
                if (item?.cargoInstances == null || item.itemName == StorageShelf.Item)
                {
                    continue;
                }

                holder = item;
                return true;
            }

            return false;
        }

        private static List<ItemInstance> PlacedList(BuildingRegistration building)
        {
            var list = new List<ItemInstance>();
            if (building.itemInstances == null)
            {
                return list;
            }

            foreach (var item in building.itemInstances.Values)
            {
                if (item != null
                    && item.itemName == StorageShelf.Item
                    && string.IsNullOrEmpty(item.parentId))
                {
                    list.Add(item);
                }
            }

            return list;
        }

        private static ItemInstance? FindPlaced(BuildingRegistration building, float x, float z, float yaw)
        {
            ItemInstance? best = null;
            var bestDist = float.MaxValue;
            foreach (var item in PlacedList(building))
            {
                var dx = item.position.x - x;
                var dz = item.position.z - z;
                var dist = (dx * dx) + (dz * dz);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = item;
                }
            }

            return best;
        }

        private static void SplitFootprints(
            BuildingRegistration building,
            out List<ShelfSpot> shelves,
            out List<ShelfSpot> others)
        {
            shelves = new List<ShelfSpot>();
            others = new List<ShelfSpot>();
            if (building.itemInstances == null)
            {
                return;
            }

            foreach (var item in building.itemInstances.Values)
            {
                var spot = Footprint(item);
                if (spot == null)
                {
                    continue;
                }

                if (item.itemName == StorageShelf.Item)
                {
                    shelves.Add(spot);
                }
                else
                {
                    others.Add(spot);
                }
            }
        }

        private static ShelfSpot BestAnchor(ShelfFloor floor, List<ShelfSpot> shelves, List<ShelfSpot> others)
        {
            ShelfSpot best = shelves[0];
            foreach (var shelf in shelves)
            {
                var blockers = BlockersExcept(shelves, others, shelf);
                if (StorageShelf.Accessible(floor, shelf, blockers))
                {
                    return shelf;
                }

                best = shelf;
            }

            return best;
        }

        private static List<int> BoxCounts(BuildingRegistration building, IReadOnlyList<ShelfSpot> shelves)
        {
            var boxes = new List<int>(shelves.Count);
            foreach (var spot in shelves)
            {
                var amount = 0;
                var item = FindPlaced(building, spot.X, spot.Z, spot.Yaw);
                if (item?.cargoInstances != null)
                {
                    foreach (var cargo in item.cargoInstances)
                    {
                        if (cargo != null && cargo.amount > 0)
                        {
                            amount += cargo.amount;
                        }
                    }
                }

                boxes.Add(amount);
            }

            return boxes;
        }

        private static List<ShelfSpot> BlockersExcept(
            IReadOnlyList<ShelfSpot> shelves,
            IReadOnlyList<ShelfSpot> others,
            ShelfSpot self)
        {
            var blockers = new List<ShelfSpot>(others);
            foreach (var shelf in shelves)
            {
                if (System.Math.Abs(shelf.X - self.X) < 0.01f
                    && System.Math.Abs(shelf.Z - self.Z) < 0.01f
                    && System.Math.Abs(shelf.Yaw - self.Yaw) < 0.01f)
                {
                    continue;
                }

                blockers.Add(shelf);
            }

            return blockers;
        }

        private static void Show(BuildingRegistration building, ItemInstance placed)
        {
            var manager = InstanceBehavior<BuildingManager>.Instance;
            if (!BuildingManager.IsInsideBuilding || manager == null || manager.buildingRegistration != building)
            {
                return;
            }

            var controller = manager.InstantiateSingleInstance(placed);
            var all = manager.allItemControllers;
            if (controller != null && all != null && !all.Contains(controller))
            {
                all.Add(controller);
            }
        }

        private static float RowHeight(BuildingRegistration building, ItemInstance holder)
        {
            foreach (var item in building.itemInstances.Values)
            {
                if (item != null && item.itemName == StorageShelf.Item && string.IsNullOrEmpty(item.parentId))
                {
                    return item.position.y;
                }
            }

            return holder.position.y;
        }

        private static bool Box(BuildingRegistration? building, out ItemInstance? holder, out CargoInstance? box)
        {
            holder = null;
            box = null;
            if (building?.itemInstances == null)
            {
                return false;
            }

            foreach (var item in building.itemInstances.Values)
            {
                if (item?.cargoInstances == null || item.itemName == StorageShelf.Item)
                {
                    continue;
                }

                foreach (var cargo in item.cargoInstances)
                {
                    if (cargo != null && cargo.itemName == StorageShelf.Item && cargo.amount > 0)
                    {
                        holder = item;
                        box = cargo;
                        return true;
                    }
                }

                // Furniture truck often nests the shelf under a closed box. Promote once for Place.
                foreach (var cargo in item.cargoInstances)
                {
                    if (cargo?.nestedCargoInstances == null)
                    {
                        continue;
                    }

                    for (var i = 0; i < cargo.nestedCargoInstances.Count; i++)
                    {
                        var nested = cargo.nestedCargoInstances[i];
                        if (nested == null || nested.itemName != StorageShelf.Item || nested.amount <= 0)
                        {
                            continue;
                        }

                        var promoted = nested.ConvertToCargoInstance();
                        cargo.nestedCargoInstances.RemoveAt(i);
                        item.AddToCargo(promoted);
                        holder = item;
                        box = promoted;
                        return true;
                    }
                }
            }

            return false;
        }

        private static ShelfFloor? Floor(BuildingRegistration building, out string reason)
        {
            reason = "";
            try
            {
                var manager = InstanceBehavior<BuildingManager>.Instance;
                var structure = manager?.BuildingSizeResolver?.GetInstantiatedBuilding(new BuildingSizeInfo(building));
                var grid = structure == null ? null : structure.GetComponent<GridGenerator>();
                if (structure == null || grid?.gridMatrices == null || grid.gridMatrices.Count == 0)
                {
                    reason = "nogrid";
                    return null;
                }

                var cells = new List<ShelfCell>();
                foreach (var matrix in grid.gridMatrices)
                {
                    if (matrix?.gridArrays == null)
                    {
                        continue;
                    }

                    for (var z = 0; z < matrix.rowsGround && z < matrix.gridArrays.Count; z++)
                    {
                        var row = matrix.gridArrays[z]?.gridCells;
                        if (row == null)
                        {
                            continue;
                        }

                        foreach (var cell in row)
                        {
                            if (cell == null)
                            {
                                continue;
                            }

                            var world = structure.transform.TransformPoint(cell.position);
                            cells.Add(new ShelfCell(world.x, world.z, !cell.isOccupied && !cell.isWall));
                        }
                    }
                }

                if (cells.Count == 0)
                {
                    reason = "nogrid";
                    return null;
                }

                return new ShelfFloor(grid.groundCellSize, cells);
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "StorageShelfBridge.Floor.1", "");
                reason = "error:" + ex.GetType().Name;
                return null;
            }
        }

        private static ShelfSpot? Footprint(ItemInstance? item)
        {
            if (item == null || string.IsNullOrEmpty(item.itemName) || !string.IsNullOrEmpty(item.parentId))
            {
                return null;
            }

            var cached = item.ItemCached;
            if (cached == null
                || cached.wallMounted
                || cached.snapToCeiling
                || (cached.type & ItemType.FlatDecoration) != 0)
            {
                return null;
            }

            var size = Size(item);
            if (size == null)
            {
                return null;
            }

            return new ShelfSpot(item.position.x, item.position.z, item.yRotation, size.Width, size.Depth, size.OffsetX, size.OffsetZ);
        }

        private static ShelfSpot? Size(ItemInstance item)
        {
            if (Sizes.TryGetValue(item.itemName, out var known))
            {
                return known;
            }

            ShelfSpot? size = null;
            try
            {
                _colliderSize ??= typeof(BuildingManager).Assembly
                    .GetType("Buildings.BuildingTypes.Special.MovingCompany.MovingServiceHelper")
                    ?.GetMethod("TryGetColliderSizeAndCenter", BindingFlags.NonPublic | BindingFlags.Static);
                var controller = PrefabHelper.LoadItemControllerFromPrefab(item.itemName);
                if (_colliderSize != null && controller != null)
                {
                    var args = new object?[] { controller, item, null, null };
                    if (_colliderSize.Invoke(null, args) is bool ok && ok && args[2] is Vector3 extent && args[3] is Vector3 center)
                    {
                        var width = Mathf.Abs(extent.x);
                        var depth = Mathf.Abs(extent.z);
                        var offsetX = center.x;
                        var offsetZ = center.z;
                        if (item.itemName == StorageShelf.Item)
                        {
                            var resolved = StorageShelf.ResolveFootprint(width, depth);
                            if (resolved.Width != width || resolved.Depth != depth)
                            {
                                // Half-extents were doubled into the live footprint — scale the center the same way.
                                offsetX *= resolved.Width / System.Math.Max(width, 0.01f);
                                offsetZ *= resolved.Depth / System.Math.Max(depth, 0.01f);
                            }

                            width = resolved.Width;
                            depth = resolved.Depth;
                        }

                        size = new ShelfSpot(0f, 0f, 0f, width, depth, offsetX, offsetZ);
                    }
                }
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "StorageShelfBridge.Size.1", "");
                size = null;
            }

            if (size == null && item.itemName == StorageShelf.Item)
            {
                size = new ShelfSpot(0f, 0f, 0f, StorageShelf.FootprintWidth, StorageShelf.FootprintDepth);
            }

            Sizes[item.itemName] = size;
            return size;
        }
    }
}
