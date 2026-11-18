using System.Collections.Generic;
using System.Globalization;

namespace ChaoticCEO
{
    public sealed class BuildingYield
    {
        public BuildingYield(string street, int number, decimal price, decimal dailyRent)
        {
            Street = street ?? "";
            Number = number;
            Price = price;
            DailyRent = dailyRent;
        }

        public string Street { get; }

        public int Number { get; }

        public decimal Price { get; }

        public decimal DailyRent { get; }
    }

    public sealed class PaybackBest
    {
        public PaybackBest(string street, int number, decimal rate, bool chosen)
        {
            Street = street ?? "";
            Number = number;
            Rate = rate;
            Chosen = chosen;
        }

        public string Street { get; }

        public int Number { get; }

        public decimal Rate { get; }

        public bool Chosen { get; }

        public static PaybackBest None()
        {
            return new PaybackBest("", 0, 0m, false);
        }
    }

    public static class PaybackRate
    {
        public static PaybackBest Best(IReadOnlyList<BuildingYield>? buildings)
        {
            if (buildings == null || buildings.Count == 0)
            {
                return PaybackBest.None();
            }

            BuildingYield? winner = null;
            var rate = 0m;
            foreach (var building in buildings)
            {
                if (building == null
                    || building.Street.Length == 0
                    || building.Number <= 0
                    || building.Price <= 0m
                    || building.DailyRent <= 0m)
                {
                    continue;
                }

                var next = building.DailyRent / building.Price;
                if (winner == null || next >= rate)
                {
                    winner = building;
                    rate = next;
                }
            }

            if (winner == null)
            {
                return PaybackBest.None();
            }

            return new PaybackBest(winner.Street, winner.Number, rate, true);
        }

        public static string Line(int day, PaybackBest? best)
        {
            var picked = best ?? PaybackBest.None();
            return "payback.best version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " rate=" + Format(picked.Rate)
                + " street=" + picked.Street
                + " number=" + picked.Number.ToString(CultureInfo.InvariantCulture);
        }

        private static string Format(decimal value)
        {
            var rounded = decimal.Round(value, 4, System.MidpointRounding.ToEven);
            if (rounded == decimal.Truncate(rounded))
            {
                return ((long)rounded).ToString(CultureInfo.InvariantCulture);
            }

            return rounded.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}
