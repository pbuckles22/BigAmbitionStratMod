using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
                var touched = ApplyCover(localAppData, day, contract, building);

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
            BuildingRegistration? building)
        {
            if (contract.items == null || contract.businessAddress == null)
            {
                return false;
            }

            var touched = false;
            CoverPlan? replacement = null;
            foreach (var item in contract.items)
            {
                if (item == null || string.IsNullOrEmpty(item.itemName))
                {
                    continue;
                }

                var plan = Wholesaler.Cover(
                    item.itemName,
                    day,
                    contract.nextDeliveryDay,
                    Sales(building, item.itemName),
                    UnitsOnHand(building, item.itemName),
                    stands: Wholesaler.RetailStands(item.itemName));
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

                if (plan.Replace)
                {
                    replacement = plan;
                }
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

        private static int UnitsOnHand(BuildingRegistration? building, string item)
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
