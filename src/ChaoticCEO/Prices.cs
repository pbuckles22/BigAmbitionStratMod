using System.Collections.Generic;

namespace ChaoticCEO
{
    public sealed class ItemSoldPerPriceEntry
    {
        public ItemSoldPerPriceEntry(string item, float price, int amount)
        {
            Item = item ?? "";
            Price = price;
            Amount = amount;
        }

        public string Item { get; }

        public float Price { get; }

        public int Amount { get; }
    }

    public sealed class SoldPrice
    {
        public SoldPrice(string item, float price)
        {
            Item = item ?? "";
            Price = price;
        }

        public string Item { get; }

        public float Price { get; }
    }

    public static class Prices
    {
        public static SoldPrice Lock(string item, IReadOnlyList<ItemSoldPerPriceEntry> entries)
        {
            var name = item ?? "";
            ItemSoldPerPriceEntry? winner = null;
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    if (entry == null || entry.Item != name || entry.Amount <= 0)
                    {
                        continue;
                    }

                    if (winner == null || entry.Amount >= winner.Amount)
                    {
                        winner = entry;
                    }
                }
            }

            if (winner == null)
            {
                return new SoldPrice(name, 0f);
            }

            return new SoldPrice(winner.Item, winner.Price);
        }
    }
}
