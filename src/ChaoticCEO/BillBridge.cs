using System.Collections.Generic;

namespace ChaoticCEO
{
    public static class BillBridge
    {
        public static bool Live(BuildingRegistration registration, PricePick quote)
        {
            if (registration == null || quote == null || !quote.Chosen || quote.Item.Length == 0 || quote.Price <= 0m)
            {
                return false;
            }

            try
            {
                if (registration.retailPrices == null)
                {
                    registration.retailPrices = new List<RetailPrice>();
                }

                RetailPrice? row = null;
                foreach (var price in registration.retailPrices)
                {
                    if (price != null && price.itemName == quote.Item)
                    {
                        row = price;
                        break;
                    }
                }

                if (row == null)
                {
                    row = new RetailPrice
                    {
                        itemName = quote.Item,
                    };
                    registration.retailPrices.Add(row);
                }

                row.price = (float)quote.Price;
                SaveGameManager.MarkChange();
                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }
    }
}
