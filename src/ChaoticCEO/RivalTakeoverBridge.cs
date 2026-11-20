using System;
using Entities;
using Helpers;

namespace ChaoticCEO
{
    public static class RivalTakeoverBridge
    {
        public static (string street, int number, decimal offer, decimal dailyProfit)? Pending()
        {
            var current = SaveGameManager.Current;
            if (current?.buildingsForSale == null || current.BuildingRegistrations == null)
            {
                return null;
            }

            foreach (var sale in current.buildingsForSale)
            {
                if (sale?.address == null)
                {
                    continue;
                }

                var registration = BuildingHelper.GetBuildingRegistration(sale.address);
                if (registration == null || !registration.BuildingOwnedByPlayer)
                {
                    continue;
                }

                var profit = DailyProfit(registration);
                if (profit <= 0m || sale.buildingPrice <= 0f)
                {
                    continue;
                }

                return (
                    sale.address.streetName ?? "",
                    sale.address.streetNumber,
                    (decimal)sale.buildingPrice,
                    profit);
            }

            return null;
        }

        public static bool Decline(TakeoverDeclinePick? pick)
        {
            if (pick == null || !pick.Declined || pick.Street.Length == 0 || pick.Number <= 0)
            {
                return false;
            }

            var current = SaveGameManager.Current;
            if (current?.buildingsForSale == null)
            {
                return false;
            }

            try
            {
                var removed = current.buildingsForSale.RemoveAll(
                    sale => sale?.address != null
                        && sale.address.streetName == pick.Street
                        && sale.address.streetNumber == pick.Number);
                if (removed <= 0)
                {
                    return false;
                }

                SaveGameManager.MarkChange();
                return true;
            }
            catch (Exception ex)
            {
                CrashTrace.Seen(ex, "RivalTakeoverBridge.Decline.1", "");
                return false;
            }
        }

        private static decimal DailyProfit(BuildingRegistration registration)
        {
            if (registration == null)
            {
                return 0m;
            }

            foreach (var offer in Starter.StageOne())
            {
                if (offer.BusinessType == registration.businessTypeName)
                {
                    return offer.DailyProfit;
                }
            }

            return Starter.StageOne()[0].DailyProfit;
        }
    }
}
