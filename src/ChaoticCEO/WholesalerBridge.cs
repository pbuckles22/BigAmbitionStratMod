using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BigAmbitions.Items;
using Entities;
using Player.SaveSystem;

namespace ChaoticCEO
{
    public static class WholesalerBridge
    {
        public static bool Live(WholesalerPick? pick, string businessType)
        {
            if (pick == null || !pick.Signed || pick.Wholesale.Length == 0 || pick.Street.Length == 0 || pick.Number <= 0)
            {
                return false;
            }

            if (!Split(pick.Wholesale, out var wholesaleStreet, out var wholesaleNumber))
            {
                return false;
            }

            var current = SaveGameManager.Current;
            if (current == null)
            {
                return false;
            }

            try
            {
                if (current.DeliveryContracts == null)
                {
                    current.DeliveryContracts = new List<DeliveryContract>();
                }

                foreach (var existing in current.DeliveryContracts)
                {
                    if (existing?.businessAddress == null)
                    {
                        continue;
                    }

                    if (existing.businessAddress.streetName == pick.Street
                        && existing.businessAddress.streetNumber == pick.Number)
                    {
                        return true;
                    }
                }

                var contract = new DeliveryContract
                {
                    enabled = true,
                    isUrgentOrder = false,
                    repeatingOrder = true,
                    nextDeliveryDay = Wholesaler.RescheduleDay(current.Day),
                    wholesaleAddress = new Address(wholesaleStreet, wholesaleNumber),
                    businessAddress = new Address(pick.Street, pick.Number),
                    deliveryFee = 0f,
                    items = Items(businessType),
                };
                current.DeliveryContracts.Add(contract);
                SaveGameManager.MarkChange();
                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        public static bool HasContract(string street, int number)
        {
            var current = SaveGameManager.Current;
            if (current?.DeliveryContracts == null || street.Length == 0 || number <= 0)
            {
                return false;
            }

            foreach (var contract in current.DeliveryContracts)
            {
                if (contract?.businessAddress == null)
                {
                    continue;
                }

                if (contract.businessAddress.streetName == street
                    && contract.businessAddress.streetNumber == number)
                {
                    return true;
                }
            }

            return false;
        }

        public static int Maintain(string? localAppData, int day)
        {
            var current = SaveGameManager.Current;
            if (current?.DeliveryContracts == null || day <= 0)
            {
                return 0;
            }

            var changed = 0;
            var cash = Cash();
            foreach (var contract in current.DeliveryContracts)
            {
                if (contract == null || contract.businessAddress == null || !contract.enabled)
                {
                    continue;
                }

                var building = BuildingFor(
                    current,
                    contract.businessAddress.streetName,
                    contract.businessAddress.streetNumber);
                var touched = ApplyCover(localAppData, day, contract, building, cash);

                if (touched)
                {
                    changed++;
                }
            }

            if (changed > 0)
            {
                SaveGameManager.MarkChange();
            }

            return changed;
        }

        private static bool ApplyCover(
            string? localAppData,
            int day,
            DeliveryContract contract,
            BuildingRegistration? building,
            decimal cash)
        {
            if (contract.items == null || contract.businessAddress == null)
            {
                return false;
            }

            var touched = false;
            var spacesNeeded = 0;
            var displayCap = 0;
            var rackTarget = 0;
            var rackCap = 0;
            CoverPlan? replacement = null;
            var stocks = new List<ShelfStock>();
            var shelves = Shelves(building);
            foreach (var item in contract.items)
            {
                if (item == null || string.IsNullOrEmpty(item.itemName))
                {
                    continue;
                }

                var onHand = UnitsOnHand(building, item.itemName);
                var sellingCap = Wholesaler.FixtureCapacity(item.itemName, shelves, sellingOnly: true);
                var plan = Wholesaler.Cover(
                    item.itemName,
                    day,
                    contract.nextDeliveryDay,
                    Sales(building, item.itemName),
                    onHand,
                    sellingCap,
                    Wholesaler.RetailStands(item.itemName));
                var gap = Wholesaler.ShortLine(
                    contract.businessAddress.streetName
                        + " "
                        + contract.businessAddress.streetNumber.ToString(CultureInfo.InvariantCulture),
                    item.itemName,
                    item.amount,
                    plan.Need);
                if (gap != null && !string.IsNullOrEmpty(localAppData))
                {
                    Wholesaler.RecordShort(
                        localAppData,
                        gap,
                        day,
                        contract.businessAddress.streetName,
                        contract.businessAddress.streetNumber,
                        item.amount);
                }
                if (!string.IsNullOrEmpty(localAppData))
                {
                    Wholesaler.RecordCover(
                        localAppData,
                        plan,
                        day,
                        contract.businessAddress.streetName,
                        contract.businessAddress.streetNumber);
                }

                if (item.amount != plan.Need)
                {
                    item.amount = plan.Need;
                    touched = true;
                }

                // Shelf buy: leftover + inbound vs placed capacity. Never use "display looks full."
                // Paper bags live on the register, not storage shelves.
                if (item.itemName != Wholesaler.PaperBag)
                {
                    var units = StorageShelf.SpacesNeeded(onHand, plan.Need);
                    spacesNeeded += units;
                    displayCap += sellingCap;
                    stocks.Add(new ShelfStock(
                        item.itemName,
                        units,
                        StorageShelfBridge.DisplayCapacity(building, item.itemName),
                        StorageShelfBridge.BoxSize(item.itemName)));
                }

                if (sellingCap > 0
                    && plan.Target >= sellingCap * StorageRack.RackTriggerRatio
                    && (rackCap == 0 || plan.Target > rackTarget))
                {
                    rackTarget = plan.Target;
                    rackCap = sellingCap;
                }

                if (plan.Replace)
                {
                    replacement = plan;
                }
            }

            var boxesNeeded = StorageShelf.BoxesNeeded(stocks);
            RecordBoxes(localAppData, day, contract, building, stocks, boxesNeeded);
            if (AttendShelf(localAppData, day, contract, building, spacesNeeded, displayCap, boxesNeeded, cash))
            {
                touched = true;
            }

            if (AttendRack(localAppData, day, contract, building, rackTarget, rackCap, cash))
            {
                touched = true;
            }

            if (replacement != null && contract.nextDeliveryDay != replacement.NextDay)
            {
                var from = contract.nextDeliveryDay;
                contract.nextDeliveryDay = replacement.NextDay;
                touched = true;
                RecordReschedule(localAppData, day, contract, from);
            }

            return touched;
        }

        private static bool AttendShelf(
            string? localAppData,
            int day,
            DeliveryContract contract,
            BuildingRegistration? building,
            int spacesNeeded,
            int displayCap,
            int boxesNeeded,
            decimal cash)
        {
            var current = SaveGameManager.Current;
            if (current == null || contract.businessAddress == null || building == null)
            {
                return false;
            }

            var street = contract.businessAddress.streetName;
            var number = contract.businessAddress.streetNumber;
            var pendingAmount = PendingShelfAmount(current, street, number);
            var pending = ShelfPending(current, street, number);
            var placedCap = displayCap
                + EmptySellingCapacity(building)
                + (StorageShelfBridge.PlacedCount(building) * StorageShelf.UnitCapacity);
            var need = StorageShelf.NeedShelves(boxesNeeded, spacesNeeded, placedCap);
            var spareSeats = StorageShelfBridge.CanSpare(building, need);
            var keep = StorageShelf.KeepTarget(need, spareSeats);
            var cargo = StorageShelfBridge.BoxedCount(building);
            var placedCount = StorageShelfBridge.PlacedCount(building);
            var have = StorageShelf.Have(placedCount, cargo, pendingAmount);
            RecordKeep(localAppData, day, street, number, boxesNeeded, need, keep, placedCount, cargo, pendingAmount, have, spareSeats);

            var packed = StorageShelfBridge.PackForReseat(building, keep, out var packedRows);
            foreach (var row in packedRows)
            {
                RecordPacked(localAppData, day, street, number, row.x, row.z, row.yaw, row.reason);
            }

            var placedDone = 0;
            for (var tries = 0; tries < StorageShelf.Boxes; tries++)
            {
                if (StorageShelfBridge.PlacedCount(building) >= keep)
                {
                    break;
                }

                if (!StorageShelfBridge.InCargo(building))
                {
                    break;
                }

                var seat = StorageShelfBridge.Seat(building);
                var attend = StorageShelf.Attend(
                    street,
                    number,
                    shelvesNeeded: 0,
                    inCargo: true,
                    pending: false,
                    cash,
                    seat,
                    GameApi.Live());
                if (attend.Action != ShelfAction.Place)
                {
                    Note(localAppData, attend, day);
                    break;
                }

                if (!StorageShelfBridge.Place(building, attend.Seat, out var reason))
                {
                    attend = new ShelfAttend(ShelfAction.Hold, street, number, true, attend.Seat, ShelfPick.None(), "", reason);
                    Note(localAppData, attend, day);
                    break;
                }

                placedDone++;
                Note(localAppData, attend, day);
            }

            cargo = StorageShelfBridge.BoxedCount(building);
            placedCount = StorageShelfBridge.PlacedCount(building);
            have = StorageShelf.Have(placedCount, cargo, pendingAmount);
            var excess = StorageShelf.Excess(have, keep);
            var sellPlan = StorageShelf.DecideSell(
                excess,
                cargo,
                StorageShelfBridge.EmptyPlacedCount(building),
                StorageShelfBridge.StockedPlacedCount(building),
                placedCount,
                keep);
            if (sellPlan.Cargo > 0)
            {
                var sold = StorageShelfBridge.SellCargo(building, sellPlan.Cargo, out var price);
                if (sold > 0)
                {
                    have = StorageShelf.Have(
                        StorageShelfBridge.PlacedCount(building),
                        StorageShelfBridge.BoxedCount(building),
                        pendingAmount);
                    RecordSold(
                        localAppData,
                        day,
                        street,
                        number,
                        "cargo",
                        sold,
                        price,
                        keep,
                        have,
                        "ICargoHolder.RemoveFromCargo+GameManager.ChangeMoneySafe");
                }
            }

            if (sellPlan.EmptyPlaced > 0)
            {
                var sold = StorageShelfBridge.SellEmptyPlaced(building, sellPlan.EmptyPlaced, out var price);
                if (sold > 0)
                {
                    have = StorageShelf.Have(
                        StorageShelfBridge.PlacedCount(building),
                        StorageShelfBridge.BoxedCount(building),
                        pendingAmount);
                    RecordSold(
                        localAppData,
                        day,
                        street,
                        number,
                        "placed",
                        sold,
                        price,
                        keep,
                        have,
                        "BuildingRegistration.RemoveItemInstanceFromBuilding+GameManager.ChangeMoneySafe");
                }
            }

            if (sellPlan.HoldStocked)
            {
                var hold = new ShelfAttend(
                    ShelfAction.Hold,
                    street,
                    number,
                    false,
                    ShelfSeat.None("stocked"),
                    ShelfPick.None(),
                    "",
                    "stocked");
                Note(localAppData, hold, day);
            }

            cargo = StorageShelfBridge.BoxedCount(building);
            placedCount = StorageShelfBridge.PlacedCount(building);
            have = StorageShelf.Have(placedCount, cargo, pendingAmount);
            var orderAmount = StorageShelf.OrderForKeep(keep, have);
            if (orderAmount <= 0 || pending)
            {
                return placedDone > 0 || packed > 0;
            }

            var pick = StorageShelf.Order(street, number, orderAmount, cash, pending, inCargo: false, seat: true, GameApi.Live());
            if (!pick.Ordered)
            {
                var seat = StorageShelfBridge.Seat(building);
                if (!seat.Found)
                {
                    Note(
                        localAppData,
                        new ShelfAttend(ShelfAction.Hold, street, number, false, seat, ShelfPick.None(), "", seat.Reason),
                        day);
                }

                return placedDone > 0 || packed > 0;
            }

            var orderAttend = new ShelfAttend(
                ShelfAction.Order,
                street,
                number,
                false,
                StorageShelfBridge.Seat(building),
                pick,
                pick.Call,
                "");
            Note(localAppData, orderAttend, day);
            return OrderShelf(localAppData, day, current, pick) || placedDone > 0 || packed > 0;
        }

        private static void RecordBoxes(
            string? localAppData,
            int day,
            DeliveryContract contract,
            BuildingRegistration? building,
            IReadOnlyList<ShelfStock> stocks,
            int boxesNeeded)
        {
            if (string.IsNullOrEmpty(localAppData) || building == null || contract.businessAddress == null || stocks.Count == 0)
            {
                return;
            }

            var street = contract.businessAddress.streetName;
            var number = contract.businessAddress.streetNumber;
            var path = LoadLog.CeoLogPath(localAppData!);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            foreach (var stock in stocks)
            {
                File.AppendAllText(path, StorageShelf.BoxLine(stock, day, street, number) + System.Environment.NewLine);
            }

            var needShelves = StorageShelf.NeedShelves(boxesNeeded, spacesNeeded: 0, placedCap: 0);
            var keep = StorageShelf.KeepTarget(needShelves, StorageShelfBridge.CanSpare(building, needShelves));
            var need = StorageShelf.NeedLine(
                day,
                street,
                number,
                boxesNeeded,
                StorageShelfBridge.PlacedCount(building),
                StorageShelfBridge.UsedSlots(building),
                keep);
            File.AppendAllText(path, need + System.Environment.NewLine);
        }

        private static void RecordKeep(
            string? localAppData,
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
            bool spareSeats)
        {
            if (string.IsNullOrEmpty(localAppData))
            {
                return;
            }

            var path = LoadLog.CeoLogPath(localAppData!);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(
                path,
                StorageShelf.KeepLine(day, street, number, boxes, need, keep, placed, cargo, pending, have, spareSeats)
                    + System.Environment.NewLine);
        }

        private static void RecordSold(
            string? localAppData,
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
            if (string.IsNullOrEmpty(localAppData) || amount <= 0)
            {
                return;
            }

            var path = LoadLog.CeoLogPath(localAppData!);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(
                path,
                StorageShelf.SoldLine(day, street, number, where, amount, price, keep, haveAfter, call)
                    + System.Environment.NewLine);
        }

        private static void RecordPacked(
            string? localAppData,
            int day,
            string? street,
            int number,
            float x,
            float z,
            float yaw,
            string reason)
        {
            if (string.IsNullOrEmpty(localAppData))
            {
                return;
            }

            var path = LoadLog.CeoLogPath(localAppData!);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(
                path,
                StorageShelf.PackedLine(day, street, number, x, z, yaw, reason) + System.Environment.NewLine);
        }

        private static void Note(string? localAppData, ShelfAttend attend, int day)
        {
            if (!string.IsNullOrEmpty(localAppData))
            {
                StorageShelf.RecordAttend(localAppData, attend, day);
            }
        }

        private static bool OrderShelf(string? localAppData, int day, GameInstance current, ShelfPick pick)
        {
            return OrderFurniture(localAppData, day, current, pick, StorageShelf.Record);
        }

        private static bool OrderFurniture(
            string? localAppData,
            int day,
            GameInstance current,
            ShelfPick pick,
            System.Func<string, ShelfPick?, int, string> record)
        {
            if (!pick.Ordered)
            {
                return false;
            }

            if (current.FurnitureDeliveryContracts == null)
            {
                current.FurnitureDeliveryContracts = new List<FurnitureDeliveryContract>();
            }

            var row = new FurnitureDeliveryItem
            {
                itemName = pick.Item,
                amount = pick.Amount,
                pricePerUnit = pick.Price,
            };
            current.FurnitureDeliveryContracts.Add(new FurnitureDeliveryContract
            {
                fromAddress = new Address(pick.FromStreet, pick.FromNumber),
                toAddress = new Address(pick.Street, pick.Number),
                itemsToDeliver = new List<FurnitureDeliveryItem> { row },
                dayOfDelivery = day + 1,
                hourOfDelivery = 8,
                deliveryFee = 0f,
            });
            SaveGameManager.MarkChange();
            if (!string.IsNullOrEmpty(localAppData))
            {
                record(localAppData, pick, day);
            }

            return true;
        }

        private static bool AttendRack(
            string? localAppData,
            int day,
            DeliveryContract contract,
            BuildingRegistration? building,
            int target,
            int fixtureCap,
            decimal cash)
        {
            var current = SaveGameManager.Current;
            if (current == null || contract.businessAddress == null || fixtureCap <= 0 || target <= 0)
            {
                return false;
            }

            var street = contract.businessAddress.streetName;
            var number = contract.businessAddress.streetNumber;
            var pending = FurniturePending(current, street, number, StorageRack.Item);
            var floorRoom = StorageShelfBridge.Seat(building).Found;
            var spendRoom = Money.SpendRoom(cash, dailyCost: 0m);
            var pick = StorageRack.Order(
                street,
                number,
                target,
                fixtureCap,
                spendRoom,
                pending,
                floorRoom,
                GameApi.Live());
            if (!pick.Ordered)
            {
                return false;
            }

            return OrderFurniture(localAppData, day, current, pick, StorageRack.Record);
        }

        private static bool ShelfPending(GameInstance current, string street, int number)
        {
            return PendingShelfAmount(current, street, number) > 0;
        }

        private static int PendingShelfAmount(GameInstance current, string street, int number)
        {
            return FurniturePendingAmount(current, street, number, StorageShelf.Item);
        }

        private static bool FurniturePending(GameInstance current, string street, int number, string itemName)
        {
            return FurniturePendingAmount(current, street, number, itemName) > 0;
        }

        private static int FurniturePendingAmount(GameInstance current, string street, int number, string itemName)
        {
            if (current.FurnitureDeliveryContracts == null)
            {
                return 0;
            }

            var total = 0;
            foreach (var contract in current.FurnitureDeliveryContracts)
            {
                if (contract?.toAddress == null
                    || contract.toAddress.streetName != street
                    || contract.toAddress.streetNumber != number
                    || contract.itemsToDeliver == null)
                {
                    continue;
                }

                foreach (var item in contract.itemsToDeliver)
                {
                    if (item != null && item.itemName == itemName && item.amount > 0)
                    {
                        total += item.amount;
                    }
                }
            }

            return total;
        }

        private static decimal Cash()
        {
            try
            {
                var wealth = Helpers.PlayerHelper.GetPersonalWealth();
                return System.Convert.ToDecimal(wealth.cash, CultureInfo.InvariantCulture);
            }
            catch (System.Exception)
            {
                return 0m;
            }
        }

        private static BuildingRegistration? BuildingFor(GameInstance current, string street, int number)
        {
            if (current.BuildingRegistrations == null || street.Length == 0 || number <= 0)
            {
                return null;
            }

            foreach (var building in current.BuildingRegistrations)
            {
                if (building != null
                    && building.StreetName == street
                    && building.StreetNumber == number)
                {
                    return building;
                }
            }

            return null;
        }

        private static IReadOnlyList<ShelfFixture> Shelves(BuildingRegistration? building)
        {
            var rows = new List<ShelfFixture>();
            if (building?.itemInstances == null)
            {
                return rows;
            }

            foreach (var placed in building.itemInstances.Values)
            {
                if (placed == null || !string.IsNullOrEmpty(placed.parentId))
                {
                    continue;
                }

                var sold = placed.playerItemPurchaserSettings == null
                    ? ""
                    : placed.playerItemPurchaserSettings.itemName;
                if (string.IsNullOrEmpty(sold))
                {
                    sold = FirstCargo(placed);
                }

                if (string.IsNullOrEmpty(sold))
                {
                    continue;
                }

                rows.Add(new ShelfFixture(placed.itemName, sold, Wholesaler.Hold(placed.itemName, sold)));
            }

            return rows;
        }

        /// <summary>
        /// Unassigned selling fixtures (no purchaser item / no cargo yet). After opening every box
        /// they would otherwise vanish from displayCap and force a fake shelf shortfall.
        /// </summary>
        private static int EmptySellingCapacity(BuildingRegistration? building)
        {
            if (building?.itemInstances == null)
            {
                return 0;
            }

            var total = 0;
            foreach (var placed in building.itemInstances.Values)
            {
                if (placed == null || !string.IsNullOrEmpty(placed.parentId))
                {
                    continue;
                }

                if (Wholesaler.IsStorageFixture(placed.itemName)
                    || placed.itemName.IndexOf("cashregister", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                var sold = placed.playerItemPurchaserSettings == null
                    ? ""
                    : placed.playerItemPurchaserSettings.itemName;
                if (string.IsNullOrEmpty(sold))
                {
                    sold = FirstCargo(placed);
                }

                if (!string.IsNullOrEmpty(sold))
                {
                    continue;
                }

                if (!IsSellingFurniture(placed.itemName))
                {
                    continue;
                }

                total += Wholesaler.StandCap;
            }

            return total;
        }

        private static bool IsSellingFurniture(string? fixture)
        {
            if (string.IsNullOrEmpty(fixture))
            {
                return false;
            }

            return fixture.IndexOf("productpanel", System.StringComparison.OrdinalIgnoreCase) >= 0
                || fixture.IndexOf("fridge", System.StringComparison.OrdinalIgnoreCase) >= 0
                || fixture.IndexOf("display", System.StringComparison.OrdinalIgnoreCase) >= 0
                || fixture.IndexOf("counter", System.StringComparison.OrdinalIgnoreCase) >= 0
                || fixture.IndexOf("rack", System.StringComparison.OrdinalIgnoreCase) >= 0
                    && fixture.IndexOf("storage", System.StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static string FirstCargo(ItemInstance placed)
        {
            if (placed.cargoInstances == null)
            {
                return "";
            }

            foreach (var box in placed.cargoInstances)
            {
                if (box != null && !string.IsNullOrEmpty(box.itemName))
                {
                    return box.itemName;
                }

                if (box?.nestedCargoInstances == null)
                {
                    continue;
                }

                foreach (var nested in box.nestedCargoInstances)
                {
                    if (nested != null && !string.IsNullOrEmpty(nested.itemName))
                    {
                        return nested.itemName;
                    }
                }
            }

            return "";
        }

        private static int UnitsOnHand(
            BuildingRegistration? building,
            string item,
            bool sellingOnly = false)
        {
            if (building?.itemInstances == null || item.Length == 0)
            {
                return 0;
            }

            var total = 0;
            foreach (var placed in building.itemInstances.Values)
            {
                if (placed?.cargoInstances == null)
                {
                    continue;
                }

                if (sellingOnly && Wholesaler.IsStorageFixture(placed.itemName))
                {
                    continue;
                }

                foreach (var box in placed.cargoInstances)
                {
                    if (box == null)
                    {
                        continue;
                    }

                    if (box.itemName == item)
                    {
                        total += box.amount;
                    }

                    if (box.nestedCargoInstances == null)
                    {
                        continue;
                    }

                    foreach (var nested in box.nestedCargoInstances)
                    {
                        if (nested != null && nested.itemName == item)
                        {
                            total += nested.amount;
                        }
                    }
                }
            }

            return total;
        }

        private static List<CoverSale> Sales(BuildingRegistration? building, string item)
        {
            var rows = new List<CoverSale>();
            if (building?.orderHistory == null)
            {
                return rows;
            }

            foreach (var entry in building.orderHistory)
            {
                if (entry == null)
                {
                    continue;
                }

                var sold = 0;
                var any = false;
                if (entry.itemSales != null)
                {
                    foreach (var sale in entry.itemSales)
                    {
                        if (sale == null)
                        {
                            continue;
                        }

                        if (sale.amountSold > 0)
                        {
                            any = true;
                        }

                        if (sale.itemName == item)
                        {
                            sold += sale.amountSold;
                        }
                    }
                }

                rows.Add(new CoverSale(
                    entry.dayNumber,
                    sold,
                    entry.totalCustomers > 0 || any,
                    entry.totalCustomers));
            }

            return rows;
        }

        private static void RecordReschedule(
            string? localAppData,
            int day,
            DeliveryContract contract,
            int fromDay)
        {
            if (string.IsNullOrEmpty(localAppData) || contract.businessAddress == null)
            {
                return;
            }

            var line = "contract.rescheduled version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + contract.businessAddress.streetName
                + " number=" + contract.businessAddress.streetNumber.ToString(CultureInfo.InvariantCulture)
                + " from=" + fromDay.ToString(CultureInfo.InvariantCulture)
                + " next=" + contract.nextDeliveryDay.ToString(CultureInfo.InvariantCulture);
            var path = LoadLog.CeoLogPath(localAppData);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + System.Environment.NewLine);
        }

        private static List<DeliveryContractItem> Items(string businessType)
        {
            var items = new List<DeliveryContractItem>();
            foreach (var name in Catalog(businessType))
            {
                var opening = Wholesaler.Cover(name, currentDay: 1, nextDeliveryDay: 2, days: null);
                items.Add(new DeliveryContractItem
                {
                    itemName = name,
                    amount = opening.Need,
                });
            }

            return items;
        }

        private static IReadOnlyList<string> Catalog(string businessType)
        {
            return Wholesaler.Catalog(businessType);
        }

        private static bool Split(string place, out string street, out int number)
        {
            street = "";
            number = 0;
            if (string.IsNullOrEmpty(place))
            {
                return false;
            }

            var at = place.LastIndexOf(' ');
            if (at <= 0 || at >= place.Length - 1)
            {
                return false;
            }

            street = place.Substring(0, at);
            return int.TryParse(place.Substring(at + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
                && street.Length > 0
                && number > 0;
        }
    }
}
