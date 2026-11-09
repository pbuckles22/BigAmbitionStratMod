using System.Collections.Generic;
using System.Globalization;
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
                    nextDeliveryDay = current.Day + 1,
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

        private static List<DeliveryContractItem> Items(string businessType)
        {
            var items = new List<DeliveryContractItem>();
            foreach (var name in Catalog(businessType))
            {
                items.Add(new DeliveryContractItem
                {
                    itemName = name,
                    // Days 3→9 stockout: amount=10 sold out in one open day; size for a week of burn.
                    amount = 50,
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
