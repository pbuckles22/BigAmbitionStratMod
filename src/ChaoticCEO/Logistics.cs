using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class StoreService
    {
        public StoreService(string destination, string warehouse, string family, string item)
        {
            Destination = destination ?? "";
            Warehouse = warehouse ?? "";
            Family = family ?? "";
            Item = item ?? "";
        }

        public string Destination { get; }

        public string Warehouse { get; }

        public string Family { get; }

        public string Item { get; }
    }

    public sealed class MixedWarehouse
    {
        public MixedWarehouse(string warehouse, string families)
        {
            Warehouse = warehouse ?? "";
            Families = families ?? "";
        }

        public string Warehouse { get; }

        public string Families { get; }
    }

    public sealed class ShortStore
    {
        public ShortStore(string destination, string warehouse, string family)
        {
            Destination = destination ?? "";
            Warehouse = warehouse ?? "";
            Family = family ?? "";
        }

        public string Destination { get; }

        public string Warehouse { get; }

        public string Family { get; }
    }

    public sealed class StoreMove
    {
        public StoreMove(string destination, string from, string to, string family)
        {
            Destination = destination ?? "";
            From = from ?? "";
            To = to ?? "";
            Family = family ?? "";
            Explain = "Reassign " + Destination
                + ". From " + From
                + ". To " + To
                + ". Family " + Family
                + ".";
        }

        public string Destination { get; }

        public string From { get; }

        public string To { get; }

        public string Family { get; }

        public string Explain { get; }
    }

    public sealed class LogisticsReview
    {
        public LogisticsReview(
            IReadOnlyList<StoreService> services,
            IReadOnlyList<MixedWarehouse> mixed,
            IReadOnlyList<ShortStore> shortStores,
            StoreMove? move)
        {
            Services = services ?? Array.Empty<StoreService>();
            Mixed = mixed ?? Array.Empty<MixedWarehouse>();
            Short = shortStores ?? Array.Empty<ShortStore>();
            Move = move;
        }

        public IReadOnlyList<StoreService> Services { get; }

        public IReadOnlyList<MixedWarehouse> Mixed { get; }

        public IReadOnlyList<ShortStore> Short { get; }

        public StoreMove? Move { get; }

        public static LogisticsReview None()
        {
            return new LogisticsReview(
                Array.Empty<StoreService>(),
                Array.Empty<MixedWarehouse>(),
                Array.Empty<ShortStore>(),
                null);
        }
    }

    public static class Logistics
    {
        public static LogisticsReview Review(
            ApiNote? api,
            IReadOnlyList<DeliveryStop>? stops,
            IReadOnlyList<WarehouseHold>? holds)
        {
            if (api == null || api.Build.Length == 0)
            {
                return LogisticsReview.None();
            }

            var stock = holds ?? Array.Empty<WarehouseHold>();
            var routes = stops ?? Array.Empty<DeliveryStop>();
            var mixed = MixedRows(stock, out var mixedNames);
            var services = new List<StoreService>();
            var shorts = new List<ShortStore>();
            foreach (var winner in Winners(routes, stock))
            {
                services.Add(new StoreService(
                    winner.Destination,
                    winner.Warehouse,
                    winner.Family,
                    winner.Item));
                if (winner.Amount <= 0)
                {
                    shorts.Add(new ShortStore(winner.Destination, winner.Warehouse, winner.Family));
                }
            }

            return new LogisticsReview(services, mixed, shorts, OneMove(shorts, stock, mixedNames));
        }

        public static LogisticsReview FromRoutes(
            IReadOnlyList<CloseScoreLine>? scored,
            ApiNote? api,
            string? routesCsv,
            string? warehousesCsv)
        {
            if (scored == null)
            {
                return LogisticsReview.None();
            }

            return Review(api, ReadStops(routesCsv), ReadHolds(warehousesCsv));
        }

        private static List<MixedWarehouse> MixedRows(IReadOnlyList<WarehouseHold> holds, out HashSet<string> names)
        {
            names = new HashSet<string>();
            var rows = new List<MixedWarehouse>();
            foreach (var mix in Advisor.WarehouseFamilyMix(holds))
            {
                names.Add(mix.Warehouse);
                rows.Add(new MixedWarehouse(mix.Warehouse, mix.Families));
            }

            return rows;
        }

        private static List<Candidate> Winners(IReadOnlyList<DeliveryStop> stops, IReadOnlyList<WarehouseHold> holds)
        {
            var groups = new List<Candidate>();
            var index = new Dictionary<(string Destination, string Family), int>();
            for (var i = 0; i < stops.Count; i++)
            {
                var stop = stops[i];
                if (stop == null || stop.Destination.Length == 0)
                {
                    continue;
                }

                var family = Advisor.CargoFamily(stop.Item);
                if (family == null)
                {
                    continue;
                }

                var warehouse = Resolve(stop, holds);
                var candidate = new Candidate(
                    i,
                    stop.Destination,
                    warehouse,
                    family,
                    stop.Item,
                    stop.Day,
                    FamilyAmount(warehouse, family, holds));
                var key = (stop.Destination, family);
                if (!index.TryGetValue(key, out var at))
                {
                    index[key] = groups.Count;
                    groups.Add(candidate);
                    continue;
                }

                groups[at] = Prefer(groups[at], candidate);
            }

            return groups;
        }

        private static Candidate Prefer(Candidate current, Candidate next)
        {
            var currentRecurring = current.Day == null;
            var nextRecurring = next.Day == null;
            if (nextRecurring != currentRecurring)
            {
                return nextRecurring ? next : current;
            }

            if (next.Amount > current.Amount)
            {
                return next;
            }

            if (next.Amount == current.Amount && next.Index > current.Index)
            {
                return next;
            }

            return current;
        }

        private static StoreMove? OneMove(
            List<ShortStore> shorts,
            IReadOnlyList<WarehouseHold> holds,
            HashSet<string> mixedNames)
        {
            for (var i = shorts.Count - 1; i >= 0; i--)
            {
                var home = BestTarget(shorts[i].Family, shorts[i].Warehouse, holds, mixedNames);
                if (home.Length == 0)
                {
                    continue;
                }

                return new StoreMove(shorts[i].Destination, shorts[i].Warehouse, home, shorts[i].Family);
            }

            return null;
        }

        private static string BestTarget(
            string family,
            string current,
            IReadOnlyList<WarehouseHold> holds,
            HashSet<string> mixedNames)
        {
            var order = new List<string>();
            var seen = new HashSet<string>();
            foreach (var hold in holds)
            {
                if (hold == null || hold.Warehouse.Length == 0 || !seen.Add(hold.Warehouse))
                {
                    continue;
                }

                order.Add(hold.Warehouse);
            }

            var best = "";
            var bestAmount = 0;
            var bestAt = -1;
            for (var i = 0; i < order.Count; i++)
            {
                var name = order[i];
                if (name == current || mixedNames.Contains(name))
                {
                    continue;
                }

                var amount = FamilyAmount(name, family, holds);
                if (amount <= 0)
                {
                    continue;
                }

                if (best.Length == 0 || amount > bestAmount || (amount == bestAmount && i > bestAt))
                {
                    best = name;
                    bestAmount = amount;
                    bestAt = i;
                }
            }

            return best;
        }

        private static string Resolve(DeliveryStop stop, IReadOnlyList<WarehouseHold> holds)
        {
            var addressHit = "";
            var nameHit = "";
            foreach (var hold in holds)
            {
                if (hold == null)
                {
                    continue;
                }

                if (addressHit.Length == 0
                    && Address(hold.Street, hold.Number) == stop.Warehouse
                    && hold.Warehouse.Length > 0)
                {
                    addressHit = hold.Warehouse;
                }

                if (nameHit.Length == 0 && hold.Warehouse == stop.Warehouse)
                {
                    nameHit = hold.Warehouse;
                }
            }

            if (addressHit.Length > 0)
            {
                return addressHit;
            }

            if (nameHit.Length > 0)
            {
                return nameHit;
            }

            return stop.Warehouse ?? "";
        }

        private static int FamilyAmount(string warehouse, string family, IReadOnlyList<WarehouseHold> holds)
        {
            var sum = 0;
            foreach (var hold in holds)
            {
                if (hold == null || hold.Warehouse != warehouse || hold.Amount <= 0)
                {
                    continue;
                }

                if (Advisor.CargoFamily(hold.Item) == family)
                {
                    sum += hold.Amount;
                }
            }

            return sum;
        }

        private static string Address(string street, int number)
        {
            return (street ?? "") + " " + number.ToString(CultureInfo.InvariantCulture);
        }

        private static List<DeliveryStop> ReadStops(string? path)
        {
            var stops = new List<DeliveryStop>();
            foreach (var line in Lines(path))
            {
                var cells = line.Split(',');
                if (cells.Length < 3)
                {
                    continue;
                }

                int? day = null;
                if (cells.Length > 3
                    && cells[3].Length > 0
                    && int.TryParse(cells[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    day = parsed;
                }

                var truck = cells.Length > 4 ? cells[4] : "";
                stops.Add(new DeliveryStop(cells[0], cells[1], cells[2], day, truck));
            }

            return stops;
        }

        private static List<WarehouseHold> ReadHolds(string? path)
        {
            var holds = new List<WarehouseHold>();
            foreach (var line in Lines(path))
            {
                var cells = line.Split(',');
                if (cells.Length < 5)
                {
                    continue;
                }

                if (!int.TryParse(cells[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                    || !int.TryParse(cells[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount))
                {
                    continue;
                }

                holds.Add(new WarehouseHold(cells[0], cells[1], number, cells[3], amount));
            }

            return holds;
        }

        private static IEnumerable<string> Lines(string? path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                yield break;
            }

            foreach (var line in File.ReadAllLines(path))
            {
                if (line.Length == 0 || line.StartsWith("warehouse,", StringComparison.Ordinal))
                {
                    continue;
                }

                yield return line;
            }
        }

        private sealed class Candidate
        {
            public Candidate(
                int index,
                string destination,
                string warehouse,
                string family,
                string item,
                int? day,
                int amount)
            {
                Index = index;
                Destination = destination;
                Warehouse = warehouse;
                Family = family;
                Item = item;
                Day = day;
                Amount = amount;
            }

            public int Index { get; }

            public string Destination { get; }

            public string Warehouse { get; }

            public string Family { get; }

            public string Item { get; }

            public int? Day { get; }

            public int Amount { get; }
        }
    }
}
