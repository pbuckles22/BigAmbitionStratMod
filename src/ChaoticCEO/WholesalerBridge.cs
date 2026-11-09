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

                var touched = false;
                if (Wholesaler.NeedsReschedule(day, contract.nextDeliveryDay))
                {
                    var from = contract.nextDeliveryDay;
                    contract.nextDeliveryDay = Wholesaler.RescheduleDay(day);
                    touched = true;
                    RecordReschedule(localAppData, day, contract, from);
                }

                if (EnsureAmounts(contract))
                {
                    touched = true;
                }

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

        private static bool EnsureAmounts(DeliveryContract contract)
        {
            if (contract.items == null)
            {
                return false;
            }

            var touched = false;
            foreach (var item in contract.items)
            {
                if (item == null || string.IsNullOrEmpty(item.itemName))
                {
                    continue;
                }

                var want = Wholesaler.ItemAmount(item.itemName, Wholesaler.StageOneDailyBurn);
                if (item.amount < want)
                {
                    item.amount = want;
                    touched = true;
                }
            }

            return touched;
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
                items.Add(new DeliveryContractItem
                {
                    itemName = name,
                    amount = Wholesaler.ItemAmount(name, Wholesaler.StageOneDailyBurn),
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
