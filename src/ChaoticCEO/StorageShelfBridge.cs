using System.Collections.Generic;
using System.Reflection;
using BigAmbitions.Items;
using Blueprints;
using Buildings.Indoors;
using Extensions;
using Helpers;
using UnityEngine;

namespace ChaoticCEO
{
    public static class StorageShelfBridge
    {
        private static readonly Dictionary<string, ShelfSpot?> Sizes = new Dictionary<string, ShelfSpot?>();

        private static MethodInfo? _colliderSize;

        public static bool InCargo(BuildingRegistration? building)
        {
            return Box(building, out _, out _);
        }

        public static ShelfSeat Seat(BuildingRegistration? building)
        {
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
                reason = "error:" + ex.GetType().Name;
                return false;
            }
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
                        size = new ShelfSpot(0f, 0f, 0f, Mathf.Abs(extent.x), Mathf.Abs(extent.z), center.x, center.z);
                    }
                }
            }
            catch (System.Exception)
            {
                size = null;
            }

            Sizes[item.itemName] = size;
            return size;
        }
    }
}
