using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class WarehouseLevel
    {
        public WarehouseLevel(
            string warehouse,
            string family,
            int onHand,
            int daily,
            bool ordered,
            bool mixed = false,
            int shelves = 0)
        {
            Warehouse = warehouse ?? "";
            Family = family ?? "";
            OnHand = onHand;
            Daily = daily;
            Ordered = ordered;
            Mixed = mixed;
            Shelves = shelves;
        }

        public string Warehouse { get; }

        public string Family { get; }

        public int OnHand { get; }

        public int Daily { get; }

        public bool Ordered { get; }

        public bool Mixed { get; }

        public int Shelves { get; }
    }

    public sealed class WarehouseDrain
    {
        public WarehouseDrain(string family, string from, string to, int amount)
        {
            Family = family ?? "";
            From = from ?? "";
            To = to ?? "";
            Amount = amount;
        }

        public string Family { get; }

        public string From { get; }

        public string To { get; }

        public int Amount { get; }
    }

    public sealed class WarehouseOverflow
    {
        public WarehouseOverflow(string family, string home, string second, string reason)
        {
            Family = family ?? "";
            Home = home ?? "";
            Second = second ?? "";
            Reason = reason ?? "";
        }

        public string Family { get; }

        public string Home { get; }

        public string Second { get; }

        public string Reason { get; }
    }

    public sealed class WarehouseArrangement
    {
        public WarehouseArrangement(IReadOnlyList<WarehouseLevel> homes, IReadOnlyList<WarehouseDrain> drains)
        {
            Homes = homes ?? Array.Empty<WarehouseLevel>();
            Drains = drains ?? Array.Empty<WarehouseDrain>();
        }

        public IReadOnlyList<WarehouseLevel> Homes { get; }

        public IReadOnlyList<WarehouseDrain> Drains { get; }
    }

    public sealed class OverflowPlan
    {
        public OverflowPlan(IReadOnlyList<WarehouseLevel> homes, IReadOnlyList<WarehouseOverflow> claims)
        {
            Homes = homes ?? Array.Empty<WarehouseLevel>();
            Claims = claims ?? Array.Empty<WarehouseOverflow>();
        }

        public IReadOnlyList<WarehouseLevel> Homes { get; }

        public IReadOnlyList<WarehouseOverflow> Claims { get; }
    }

    public sealed class RestockOrder
    {
        public RestockOrder(string warehouse, string family, int amount, string assign, string start)
        {
            Warehouse = warehouse ?? "";
            Family = family ?? "";
            Amount = amount;
            Assign = assign ?? "";
            Start = start ?? "";
        }

        public string Warehouse { get; }

        public string Family { get; }

        public int Amount { get; }

        public string Assign { get; }

        public string Start { get; }
    }

    public static class Restock
    {
        private static readonly string[] Families = { "food", "durables", "specialty", "service" };

        public static WarehouseArrangement Arrange(
            IReadOnlyList<WarehouseHold>? holds,
            IReadOnlyDictionary<string, int>? shelves = null)
        {
            var bins = Bins(holds);
            var index = new Dictionary<string, Bin>();
            foreach (var bin in bins)
            {
                index[bin.Name] = bin;
            }

            if (shelves != null)
            {
                foreach (var pair in shelves)
                {
                    if (string.IsNullOrEmpty(pair.Key) || pair.Value <= 0)
                    {
                        continue;
                    }

                    if (!index.TryGetValue(pair.Key, out var bin))
                    {
                        bin = new Bin(pair.Key);
                        index[pair.Key] = bin;
                        bins.Add(bin);
                    }

                    bin.Shelves += pair.Value;
                }
            }
            var homeOf = new Dictionary<string, List<Bin>>();
            foreach (var bin in bins)
            {
                if (!bin.CanHold || bin.Cargo.Count != 1)
                {
                    continue;
                }

                var family = "";
                foreach (var one in bin.Cargo.Keys)
                {
                    family = one;
                }

                bin.Family = family;
                AddHome(homeOf, family, bin);
            }

            var mixed = new List<Bin>();
            foreach (var bin in bins)
            {
                if (bin.CanHold && bin.Family.Length == 0 && bin.Cargo.Count > 1)
                {
                    mixed.Add(bin);
                }
            }

            mixed.Sort((left, right) => right.MajorityAmount.CompareTo(left.MajorityAmount));
            foreach (var bin in mixed)
            {
                if (homeOf.ContainsKey(bin.Majority))
                {
                    continue;
                }

                bin.Family = bin.Majority;
                AddHome(homeOf, bin.Majority, bin);
            }

            foreach (var bin in mixed)
            {
                if (bin.Family.Length == 0)
                {
                    bin.Family = bin.Majority;
                    AddHome(homeOf, bin.Majority, bin);
                }
            }

            var open = new List<Bin>();
            foreach (var bin in bins)
            {
                if (bin.CanHold && bin.Family.Length == 0)
                {
                    open.Add(bin);
                }
            }

            open.Sort((left, right) => WarehouseNumber(left.Name).CompareTo(WarehouseNumber(right.Name)));
            var waiting = Waiting(bins, homeOf);
            var count = open.Count < waiting.Count ? open.Count : waiting.Count;
            for (var i = 0; i < count; i++)
            {
                open[i].Family = waiting[i];
                AddHome(homeOf, waiting[i], open[i]);
            }

            var drains = new List<WarehouseDrain>();
            var ordered = new List<Bin>(bins);
            ordered.Sort((left, right) => WarehouseNumber(left.Name).CompareTo(WarehouseNumber(right.Name)));
            foreach (var bin in ordered)
            {
                foreach (var family in Families)
                {
                    var amount = bin.Amount(family);
                    if (amount <= 0 || bin.Family == family || !homeOf.TryGetValue(family, out var homes))
                    {
                        continue;
                    }

                    var destination = Destination(homes);
                    if (destination.Name == bin.Name)
                    {
                        continue;
                    }

                    drains.Add(new WarehouseDrain(family, bin.Name, destination.Name, amount));
                }
            }

            var levels = new List<WarehouseLevel>();
            foreach (var bin in ordered)
            {
                levels.Add(new WarehouseLevel(
                    bin.Name,
                    bin.Family,
                    bin.Family.Length == 0 ? 0 : bin.Amount(bin.Family),
                    0,
                    false,
                    bin.Cargo.Count > 1,
                    bin.Shelves));
            }

            return new WarehouseArrangement(levels, drains);
        }

        public static OverflowPlan Overflow(
            IReadOnlyList<WarehouseLevel>? homes,
            IReadOnlyDictionary<string, bool>? trucksBusy)
        {
            var rows = new List<WarehouseLevel>();
            var empty = new List<int>();
            if (homes != null)
            {
                foreach (var level in homes)
                {
                    if (level == null)
                    {
                        continue;
                    }

                    rows.Add(level);
                    if (level.Family.Length == 0 && level.Shelves > 0)
                    {
                        empty.Add(rows.Count - 1);
                    }
                }
            }

            empty.Sort((left, right) => WarehouseNumber(rows[left].Warehouse).CompareTo(WarehouseNumber(rows[right].Warehouse)));
            var claims = new List<WarehouseOverflow>();
            var taken = new HashSet<string>();
            var emptyAt = 0;
            foreach (var level in rows.ToArray())
            {
                if (level.Family.Length == 0 || taken.Contains(level.Family))
                {
                    continue;
                }

                var reason = "";
                if (trucksBusy != null && trucksBusy.TryGetValue(level.Family, out var busy) && busy)
                {
                    reason = "trucks";
                }
                else if (level.Ordered && level.Daily > 0 && level.OnHand < Supply.RefillDays * level.Daily)
                {
                    reason = "short";
                }

                if (reason.Length == 0)
                {
                    continue;
                }

                while (emptyAt < empty.Count && rows[empty[emptyAt]].Family.Length > 0)
                {
                    emptyAt++;
                }

                if (emptyAt >= empty.Count)
                {
                    break;
                }

                var index = empty[emptyAt++];
                var open = rows[index];
                rows[index] = new WarehouseLevel(
                    open.Warehouse,
                    level.Family,
                    0,
                    level.Daily,
                    false,
                    false,
                    open.Shelves);
                taken.Add(level.Family);
                claims.Add(new WarehouseOverflow(level.Family, level.Warehouse, open.Warehouse, reason));
            }

            return new OverflowPlan(rows, claims);
        }

        public static int FamilyBurn(
            string family,
            string home,
            IReadOnlyList<DeliveryStop>? stops,
            IReadOnlyList<DailyUnits>? sales,
            bool unconnected = true)
        {
            if (string.IsNullOrEmpty(family) || string.IsNullOrEmpty(home) || sales == null)
            {
                return 0;
            }

            var connected = new HashSet<string>();
            var planned = new HashSet<string>();
            if (stops != null)
            {
                foreach (var stop in stops)
                {
                    if (stop == null || stop.Day != null || stop.Destination.Length == 0)
                    {
                        continue;
                    }

                    planned.Add(stop.Destination);
                    if (stop.Warehouse == home)
                    {
                        connected.Add(stop.Destination);
                    }
                }
            }

            var latest = new Dictionary<string, int>();
            var units = new Dictionary<string, int>();
            foreach (var sale in sales)
            {
                if (sale == null || !sale.Open || sale.Units <= 0)
                {
                    continue;
                }

                if (Advisor.CargoFamily(sale.Item) != family)
                {
                    continue;
                }

                if (!connected.Contains(sale.Store) && (planned.Contains(sale.Store) || !unconnected))
                {
                    continue;
                }

                var key = sale.Store + "\n" + sale.Item;
                if (!latest.TryGetValue(key, out var day) || sale.Day >= day)
                {
                    latest[key] = sale.Day;
                    units[key] = sale.Units;
                }
            }

            var sum = 0;
            foreach (var value in units.Values)
            {
                sum += value;
            }

            return sum;
        }

        public static IReadOnlyList<WarehouseLevel> WithBurn(
            IReadOnlyList<WarehouseLevel>? homes,
            IReadOnlyList<DeliveryStop>? stops,
            IReadOnlyList<DailyUnits>? sales,
            IReadOnlyCollection<string>? ordered)
        {
            var primary = new Dictionary<string, string>();
            if (homes != null)
            {
                foreach (var level in homes)
                {
                    if (level == null || level.Family.Length == 0)
                    {
                        continue;
                    }

                    if (!primary.TryGetValue(level.Family, out var current)
                        || level.OnHand < OnHandOf(homes, current)
                        || (level.OnHand == OnHandOf(homes, current) && WarehouseNumber(level.Warehouse) < WarehouseNumber(current)))
                    {
                        primary[level.Family] = level.Warehouse;
                    }
                }
            }

            var rows = new List<WarehouseLevel>();
            if (homes == null)
            {
                return rows;
            }

            foreach (var level in homes)
            {
                if (level == null)
                {
                    continue;
                }

                var open = level.Family.Length > 0
                    && primary.TryGetValue(level.Family, out var home)
                    && home == level.Warehouse;
                var marked = false;
                if (ordered != null)
                {
                    foreach (var name in ordered)
                    {
                        if (name == level.Warehouse)
                        {
                            marked = true;
                            break;
                        }
                    }
                }

                rows.Add(new WarehouseLevel(
                    level.Warehouse,
                    level.Family,
                    level.OnHand,
                    level.Family.Length == 0
                        ? 0
                        : FamilyBurn(level.Family, level.Warehouse, stops, sales, open),
                    marked,
                    level.Mixed,
                    level.Shelves));
            }

            return rows;
        }

        private static int OnHandOf(IReadOnlyList<WarehouseLevel> homes, string warehouse)
        {
            foreach (var level in homes)
            {
                if (level != null && level.Warehouse == warehouse)
                {
                    return level.OnHand;
                }
            }

            return 0;
        }

        public static IReadOnlyList<RestockOrder> Order(ApiNote? api, IReadOnlyList<WarehouseLevel>? levels)
        {
            var assign = api == null ? "" : api.Call("purchasing plan");
            var start = api == null ? "" : api.Call("importer order");
            var orders = new List<RestockOrder>();
            if (assign.Length == 0 || start.Length == 0 || levels == null)
            {
                return orders;
            }

            foreach (var level in levels)
            {
                if (level == null || level.Warehouse.Length == 0 || level.Family.Length == 0 || level.Ordered)
                {
                    continue;
                }

                if (level.Daily <= 0)
                {
                    continue;
                }

                var cover = Supply.RefillDays * level.Daily;
                if (level.OnHand >= cover)
                {
                    continue;
                }

                orders.Add(new RestockOrder(
                    level.Warehouse,
                    level.Family,
                    cover - level.OnHand,
                    assign,
                    start));
            }

            return orders;
        }

        public static string Line(int day, RestockOrder order)
        {
            return "restock.ordered version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " warehouse=" + order.Warehouse
                + " family=" + order.Family
                + " amount=" + order.Amount.ToString(CultureInfo.InvariantCulture)
                + " assign=" + order.Assign
                + " start=" + order.Start;
        }

        public static string DrainLine(int day, WarehouseDrain drain, string assign)
        {
            return "warehouse.drain version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " family=" + drain.Family
                + " from=" + drain.From
                + " to=" + drain.To
                + " amount=" + drain.Amount.ToString(CultureInfo.InvariantCulture)
                + " assign=" + (assign ?? "");
        }

        public static string OverflowLine(int day, WarehouseOverflow claim)
        {
            return "warehouse.overflow version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " family=" + claim.Family
                + " home=" + claim.Home
                + " second=" + claim.Second
                + " reason=" + claim.Reason;
        }

        public static string Summary(int day, int ordered)
        {
            return "restock.summary version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " ordered=" + ordered.ToString(CultureInfo.InvariantCulture);
        }

        private sealed class Bin
        {
            public Bin(string name)
            {
                Name = name;
            }

            public string Name { get; }

            public Dictionary<string, int> Cargo { get; } = new Dictionary<string, int>();

            public int Shelves { get; set; }

            public string Family { get; set; } = "";

            public string Majority { get; set; } = "";

            public int MajorityAmount { get; set; }

            public bool CanHold => Shelves > 0 || Cargo.Count > 0;

            public int Amount(string family)
            {
                return Cargo.TryGetValue(family, out var amount) ? amount : 0;
            }
        }

        private static List<Bin> Bins(IReadOnlyList<WarehouseHold>? holds)
        {
            var bins = new List<Bin>();
            var index = new Dictionary<string, Bin>();
            if (holds == null)
            {
                return bins;
            }

            foreach (var hold in holds)
            {
                if (hold == null || hold.Warehouse.Length == 0)
                {
                    continue;
                }

                if (!index.TryGetValue(hold.Warehouse, out var bin))
                {
                    bin = new Bin(hold.Warehouse);
                    index[hold.Warehouse] = bin;
                    bins.Add(bin);
                }

                if (hold.Item == "ba:itemname_palletshelf")
                {
                    if (hold.Amount > 0)
                    {
                        bin.Shelves += hold.Amount;
                    }

                    continue;
                }

                var family = Advisor.CargoFamily(hold.Item);
                if (string.IsNullOrEmpty(family) || family == "unknown" || hold.Amount <= 0)
                {
                    continue;
                }

                bin.Cargo[family] = bin.Amount(family) + hold.Amount;
                if (bin.Amount(family) > bin.MajorityAmount
                    || (bin.Amount(family) == bin.MajorityAmount && FamilyRank(family) < FamilyRank(bin.Majority)))
                {
                    bin.Majority = family;
                    bin.MajorityAmount = bin.Amount(family);
                }
            }

            return bins;
        }

        private static void AddHome(Dictionary<string, List<Bin>> homeOf, string family, Bin bin)
        {
            if (!homeOf.TryGetValue(family, out var list))
            {
                list = new List<Bin>();
                homeOf[family] = list;
            }

            list.Add(bin);
        }

        private static List<string> Waiting(IReadOnlyList<Bin> bins, Dictionary<string, List<Bin>> homeOf)
        {
            var totals = new Dictionary<string, int>();
            foreach (var bin in bins)
            {
                foreach (var pair in bin.Cargo)
                {
                    totals[pair.Key] = totals.TryGetValue(pair.Key, out var have) ? have + pair.Value : pair.Value;
                }
            }

            var waiting = new List<string>();
            foreach (var family in Families)
            {
                if (totals.ContainsKey(family) && !homeOf.ContainsKey(family))
                {
                    waiting.Add(family);
                }
            }

            waiting.Sort((left, right) =>
            {
                var byAmount = totals[right].CompareTo(totals[left]);
                return byAmount != 0 ? byAmount : FamilyRank(left).CompareTo(FamilyRank(right));
            });
            return waiting;
        }

        private static Bin Destination(IReadOnlyList<Bin> homes)
        {
            var best = homes[0];
            foreach (var home in homes)
            {
                var amount = home.Amount(home.Family);
                var bestAmount = best.Amount(best.Family);
                if (amount < bestAmount
                    || (amount == bestAmount && WarehouseNumber(home.Name) < WarehouseNumber(best.Name)))
                {
                    best = home;
                }
            }

            return best;
        }

        private static int FamilyRank(string family)
        {
            for (var i = 0; i < Families.Length; i++)
            {
                if (Families[i] == family)
                {
                    return i;
                }
            }

            return Families.Length;
        }

        private static int WarehouseNumber(string name)
        {
            var digits = "";
            foreach (var ch in name)
            {
                if (char.IsDigit(ch))
                {
                    digits += ch;
                }
            }

            return digits.Length == 0 ? 0 : int.Parse(digits, CultureInfo.InvariantCulture);
        }
    }

    public sealed class RestockWatch
    {
        private object? _seen;

        public IReadOnlyList<string> Tick(
            string localAppData,
            object? save,
            int day,
            ApiNote? api,
            IReadOnlyList<WarehouseLevel>? levels,
            IReadOnlyList<WarehouseDrain>? drains = null,
            IReadOnlyList<WarehouseOverflow>? overflows = null,
            int displayOrdered = 0)
        {
            if (save == null || ReferenceEquals(_seen, save))
            {
                return Array.Empty<string>();
            }

            _seen = save;
            var assign = api == null ? "" : api.Call("purchasing plan");
            var orders = Restock.Order(api, levels);
            var lines = new List<string>();
            if (drains != null)
            {
                foreach (var drain in drains)
                {
                    lines.Add(Restock.DrainLine(day, drain, assign));
                }
            }

            if (overflows != null)
            {
                foreach (var claim in overflows)
                {
                    lines.Add(Restock.OverflowLine(day, claim));
                }
            }

            if (orders.Count == 0)
            {
                var count = displayOrdered > 0 ? displayOrdered : 0;
                lines.Add(Restock.Summary(day, count));
            }
            else
            {
                foreach (var order in orders)
                {
                    lines.Add(Restock.Line(day, order));
                }
            }

            var path = LoadLog.CeoLogPath(localAppData ?? "");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, string.Join(Environment.NewLine, lines) + Environment.NewLine);
            return lines;
        }
    }
}
