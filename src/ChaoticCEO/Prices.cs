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

    public sealed class PricePick
    {
        public PricePick(string item, decimal price, bool chosen)
        {
            Item = item ?? "";
            Price = price;
            Chosen = chosen;
        }

        public string Item { get; }

        public decimal Price { get; }

        public bool Chosen { get; }

        public static PricePick None()
        {
            return new PricePick("", 0m, false);
        }
    }

    public static class Prices
    {
        public const decimal PriceStep = 0.05m;

        public const decimal PriceSatisfied = 0.75m;

        public static PricePick Decide(
            string? item,
            decimal currentPrice,
            decimal defaultPrice,
            decimal wholesale,
            int priorUnits,
            int currentUnits,
            decimal satisfaction,
            IReadOnlyList<ItemSoldPerPriceEntry>? trials)
        {
            var name = item ?? "";
            if (name.Length == 0 || currentPrice <= 0m)
            {
                return PricePick.None();
            }

            if (defaultPrice > 0m && currentPrice == defaultPrice)
            {
                return Raise(name, currentPrice);
            }

            if (currentUnits >= priorUnits && satisfaction >= PriceSatisfied)
            {
                return Raise(name, currentPrice);
            }

            var stepped = StepBack(name, defaultPrice, wholesale, trials);
            if (stepped != null)
            {
                return stepped;
            }

            return new PricePick(name, currentPrice, true);
        }

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

        private static PricePick Raise(string item, decimal currentPrice)
        {
            return new PricePick(item, currentPrice * (1m + PriceStep), true);
        }

        private static PricePick? StepBack(
            string item,
            decimal defaultPrice,
            decimal wholesale,
            IReadOnlyList<ItemSoldPerPriceEntry>? trials)
        {
            if (trials == null)
            {
                return null;
            }

            ItemSoldPerPriceEntry? winner = null;
            var winnerGain = 0m;
            foreach (var entry in trials)
            {
                if (entry == null
                    || entry.Item != item
                    || entry.Amount <= 0
                    || entry.Price <= 0f)
                {
                    continue;
                }

                var price = (decimal)entry.Price;
                if (defaultPrice > 0m && price == defaultPrice)
                {
                    continue;
                }

                var gain = (price - wholesale) * entry.Amount;
                if (winner == null || gain >= winnerGain)
                {
                    winner = entry;
                    winnerGain = gain;
                }
            }

            return winner == null
                ? null
                : new PricePick(winner.Item, (decimal)winner.Price, true);
        }
    }
}
