using System.Collections.Generic;
using System.Globalization;

namespace ChaoticCEO
{
    public sealed class DeliveryRun
    {
        public DeliveryRun(string warehouse, string family, string destination, string item, int? day)
        {
            Warehouse = warehouse ?? "";
            Family = family ?? "";
            Destination = destination ?? "";
            Item = item ?? "";
            Day = day;
        }

        public string Warehouse { get; }

        public string Family { get; }

        public string Destination { get; }

        public string Item { get; }

        public int? Day { get; }
    }

    public static class Plans
    {
        public static DeliveryRun Lock(
            string warehouse,
            IReadOnlyList<WarehouseHold> holds,
            IReadOnlyList<DeliveryStop> stops,
            SoldPrice? price)
        {
            var name = warehouse ?? "";
            var family = KeepFamily(name, holds, out var addresses);
            var stop = KeepStop(name, addresses, family, stops, price);
            if (stop == null)
            {
                return new DeliveryRun(name, family, "", "", null);
            }

            return new DeliveryRun(name, family, stop.Destination, stop.Item, null);
        }

        private static string KeepFamily(
            string warehouse,
            IReadOnlyList<WarehouseHold> holds,
            out HashSet<string> addresses)
        {
            addresses = new HashSet<string>();
            var totals = new Dictionary<string, int>();
            var lastIndex = new Dictionary<string, int>();
            if (holds == null)
            {
                return "";
            }

            var index = 0;
            foreach (var hold in holds)
            {
                var here = index;
                index++;
                if (hold == null || hold.Warehouse != warehouse)
                {
                    continue;
                }

                addresses.Add(Address(hold.Street, hold.Number));
                if (hold.Amount <= 0)
                {
                    continue;
                }

                var family = Advisor.CargoFamily(hold.Item);
                if (family == null)
                {
                    continue;
                }

                totals[family] = totals.TryGetValue(family, out var sum) ? sum + hold.Amount : hold.Amount;
                lastIndex[family] = here;
            }

            var winner = "";
            var best = -1;
            var bestAt = -1;
            foreach (var pair in totals)
            {
                var at = lastIndex[pair.Key];
                if (pair.Value > best || (pair.Value == best && at > bestAt))
                {
                    best = pair.Value;
                    bestAt = at;
                    winner = pair.Key;
                }
            }

            return winner;
        }

        private static DeliveryStop? KeepStop(
            string warehouse,
            HashSet<string> addresses,
            string family,
            IReadOnlyList<DeliveryStop> stops,
            SoldPrice? price)
        {
            if (family.Length == 0 || stops == null)
            {
                return null;
            }

            var recurring = new List<DeliveryStop>();
            var dated = new List<DeliveryStop>();
            foreach (var stop in stops)
            {
                if (!Belongs(warehouse, addresses, family, stop))
                {
                    continue;
                }

                if (stop.Day == null)
                {
                    recurring.Add(stop);
                }
                else
                {
                    dated.Add(stop);
                }
            }

            var pool = recurring.Count > 0 ? recurring : dated;
            DeliveryStop? last = null;
            DeliveryStop? priced = null;
            var wanted = price == null ? "" : price.Item;
            foreach (var stop in pool)
            {
                last = stop;
                if (wanted.Length > 0 && stop.Item == wanted)
                {
                    priced = stop;
                }
            }

            return priced ?? last;
        }

        private static bool Belongs(
            string warehouse,
            HashSet<string> addresses,
            string family,
            DeliveryStop stop)
        {
            if (stop == null)
            {
                return false;
            }

            if (stop.Warehouse != warehouse && !addresses.Contains(stop.Warehouse))
            {
                return false;
            }

            return Advisor.CargoFamily(stop.Item) == family;
        }

        private static string Address(string street, int number)
        {
            return (street ?? "") + " " + number.ToString(CultureInfo.InvariantCulture);
        }
    }
}
