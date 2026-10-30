using System.Collections.Generic;

namespace ChaoticCEO
{
    public sealed class LedgerDay
    {
        public LedgerDay(string street, int number, string family, int day, float profit)
        {
            Street = street ?? "";
            Number = number;
            Family = family ?? "";
            Day = day;
            Profit = profit;
        }

        public string Street { get; }

        public int Number { get; }

        public string Family { get; }

        public int Day { get; }

        public float Profit { get; }
    }

    public sealed class StoreClose
    {
        public StoreClose(string street, int number, string family, bool closed)
        {
            Street = street ?? "";
            Number = number;
            Family = family ?? "";
            Closed = closed;
        }

        public string Street { get; }

        public int Number { get; }

        public string Family { get; }

        public bool Closed { get; }
    }

    public static class Close
    {
        public static StoreClose Lock(
            LayoutClone? layout,
            IReadOnlyList<LedgerDay>? days,
            int redDays,
            bool confirmed,
            bool toggle)
        {
            if (!confirmed
                || !toggle
                || redDays <= 0
                || days == null
                || layout == null
                || layout.Street.Length == 0
                || layout.Family.Length == 0
                || layout.Blueprint.Length == 0)
            {
                return None();
            }

            var streaks = new Dictionary<int, int>();
            var order = new Dictionary<int, int>();
            var index = 0;
            foreach (var day in days)
            {
                var here = index;
                index++;
                if (day == null || day.Street != layout.Street || day.Family != layout.Family)
                {
                    continue;
                }

                if (day.Profit < 0f)
                {
                    streaks.TryGetValue(day.Number, out var count);
                    streaks[day.Number] = count + 1;
                    order[day.Number] = here;
                }
                else if (streaks.ContainsKey(day.Number))
                {
                    streaks[day.Number] = 0;
                }
            }

            int? winner = null;
            var bestCount = 0;
            var bestOrder = -1;
            foreach (var pair in streaks)
            {
                if (pair.Value < redDays)
                {
                    continue;
                }

                order.TryGetValue(pair.Key, out var seen);
                if (winner == null || pair.Value > bestCount || (pair.Value == bestCount && seen >= bestOrder))
                {
                    winner = pair.Key;
                    bestCount = pair.Value;
                    bestOrder = seen;
                }
            }

            if (winner == null)
            {
                return None();
            }

            return new StoreClose(layout.Street, winner.Value, layout.Family, true);
        }

        private static StoreClose None()
        {
            return new StoreClose("", 0, "", false);
        }
    }
}
