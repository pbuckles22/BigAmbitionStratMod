using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class GoodsNeed
    {
        public GoodsNeed(string store, string item)
        {
            Store = store ?? "";
            Item = item ?? "";
        }

        public string Store { get; }

        public string Item { get; }
    }

    public sealed class DailyUnits
    {
        public DailyUnits(string store, string item, int day, int units, bool open = true)
        {
            Store = store ?? "";
            Item = item ?? "";
            Day = day;
            Units = units;
            Open = open;
        }

        public string Store { get; }

        public string Item { get; }

        public int Day { get; }

        public int Units { get; }

        public bool Open { get; }
    }

    public sealed class SaleTick
    {
        public SaleTick(string item, int day, int units)
        {
            Item = item ?? "";
            Day = day;
            Units = units;
        }

        public string Item { get; }

        public int Day { get; }

        public int Units { get; }
    }

    public sealed class StoreRecord
    {
        public StoreRecord(
            string? businessName,
            bool rentedByPlayer,
            bool ownedByPlayer,
            string? rivalId,
            string? businessType,
            string? place)
        {
            BusinessName = businessName ?? "";
            RentedByPlayer = rentedByPlayer;
            OwnedByPlayer = ownedByPlayer;
            RivalId = rivalId ?? "";
            BusinessType = businessType ?? "";
            Place = place ?? "";
        }

        public string BusinessName { get; }

        public bool RentedByPlayer { get; }

        public bool OwnedByPlayer { get; }

        public string RivalId { get; }

        public string BusinessType { get; }

        public string Place { get; }
    }

    public sealed class SupplyGap
    {
        public SupplyGap(string store, string warehouse, string item, string reason, int target = 0)
        {
            Store = store ?? "";
            Warehouse = warehouse ?? "";
            Item = item ?? "";
            Reason = reason ?? "";
            Target = target;
        }

        public string Store { get; }

        public string Warehouse { get; }

        public string Item { get; }

        public string Reason { get; }

        public int Target { get; }
    }

    public sealed class SupplyList
    {
        public SupplyList(IReadOnlyList<string> wholesaler, IReadOnlyList<SupplyGap> unsupplied)
        {
            Wholesaler = wholesaler ?? Array.Empty<string>();
            Unsupplied = unsupplied ?? Array.Empty<SupplyGap>();
        }

        public IReadOnlyList<string> Wholesaler { get; }

        public IReadOnlyList<SupplyGap> Unsupplied { get; }
    }

    public static class Supply
    {
        public const int RefillDays = 7;

        public static SupplyList List(
            IReadOnlyList<DeliveryStop>? stops,
            IReadOnlyList<WarehouseHold>? holds,
            IReadOnlyList<GoodsNeed>? needs,
            IReadOnlyList<DailyUnits>? days)
        {
            var routes = stops ?? Array.Empty<DeliveryStop>();
            var stock = holds ?? Array.Empty<WarehouseHold>();
            var wanted = needs ?? Array.Empty<GoodsNeed>();
            var history = days ?? Array.Empty<DailyUnits>();
            var wholesaler = new List<string>();
            var gaps = new List<SupplyGap>();
            foreach (var need in wanted)
            {
                if (need == null || need.Store.Length == 0 || need.Item.Length == 0)
                {
                    continue;
                }

                var verdict = Decide(need, routes, stock, wanted, history);
                if (verdict.Kind == "wholesaler")
                {
                    if (!wholesaler.Contains(need.Store))
                    {
                        wholesaler.Add(need.Store);
                    }

                    continue;
                }

                if (verdict.Kind == "supplied")
                {
                    continue;
                }

                gaps.Add(new SupplyGap(need.Store, verdict.Warehouse, need.Item, verdict.Kind, verdict.Target));
            }

            return new SupplyList(wholesaler, gaps);
        }

        public static SupplyList FromDump(
            string? routesCsv,
            string? warehousesCsv,
            IReadOnlyList<GoodsNeed>? needs,
            IReadOnlyList<DailyUnits>? days)
        {
            return List(ReadStops(routesCsv), ReadHolds(warehousesCsv), needs, days);
        }

        public static IReadOnlyList<DeliveryStop> Stops(string? path)
        {
            return ReadStops(path);
        }

        public static IReadOnlyList<WarehouseHold> Stock(string? path)
        {
            return ReadHolds(path);
        }

        public static bool PlayerStore(string? businessName, bool rentedByPlayer, bool ownedByPlayer, string? rivalId)
        {
            if (string.IsNullOrEmpty(businessName) || !string.IsNullOrEmpty(rivalId))
            {
                return false;
            }

            return rentedByPlayer || ownedByPlayer;
        }

        public static void FromBuilding(
            List<GoodsNeed> needs,
            List<DailyUnits> burn,
            StoreRecord? store,
            IReadOnlyList<string>? items,
            IReadOnlyList<SaleTick>? ticks)
        {
            if (store == null
                || !PlayerStore(store.BusinessName, store.RentedByPlayer, store.OwnedByPlayer, store.RivalId))
            {
                return;
            }

            FromStore(needs, burn, store.BusinessType, store.Place, items, ticks);
        }

        public static void FromStore(
            List<GoodsNeed> needs,
            List<DailyUnits> burn,
            string? businessType,
            string? place,
            IReadOnlyList<string>? items,
            IReadOnlyList<SaleTick>? ticks)
        {
            if (needs == null || burn == null || string.IsNullOrEmpty(place))
            {
                return;
            }

            var sold = new List<string>();
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (Sellable(item) && !sold.Contains(item))
                    {
                        sold.Add(item);
                    }
                }
            }

            if (businessType == "ba:businesstype_hairdresser" && !sold.Contains("ba:itemname_haircareproduct"))
            {
                sold.Add("ba:itemname_haircareproduct");
            }

            foreach (var item in sold)
            {
                var have = false;
                foreach (var need in needs)
                {
                    if (need != null && need.Store == place && need.Item == item)
                    {
                        have = true;
                        break;
                    }
                }

                if (!have)
                {
                    needs.Add(new GoodsNeed(place, item));
                }
            }

            if (ticks == null)
            {
                return;
            }

            foreach (var tick in ticks)
            {
                if (tick == null || tick.Units <= 0 || !sold.Contains(tick.Item))
                {
                    continue;
                }

                burn.Add(new DailyUnits(place, tick.Item, tick.Day, tick.Units));
            }
        }

        public static void Collect(List<GoodsNeed> needs, string? businessType, string? place)
        {
            if (needs == null || string.IsNullOrEmpty(businessType) || string.IsNullOrEmpty(place))
            {
                return;
            }

            if (businessType == "ba:businesstype_hairdresser")
            {
                needs.Add(new GoodsNeed(place, "ba:itemname_haircareproduct"));
                return;
            }

            var family = StoreFamily.For(businessType);
            if (family != "food" && family != "durables" && family != "specialty")
            {
                return;
            }

            foreach (var item in Advisor.Items(family))
            {
                needs.Add(new GoodsNeed(place, item));
            }
        }

        private static Verdict Decide(
            GoodsNeed need,
            IReadOnlyList<DeliveryStop> stops,
            IReadOnlyList<WarehouseHold> holds,
            IReadOnlyList<GoodsNeed> needs,
            IReadOnlyList<DailyUnits> days)
        {
            var piles = Piles(holds, need.Item);
            var positive = new List<Pile>();
            foreach (var pile in piles)
            {
                if (pile.Amount > 0)
                {
                    positive.Add(pile);
                }
            }

            var connections = new List<Pile>();
            foreach (var pile in positive)
            {
                if (Connected(need.Store, need.Item, pile, stops))
                {
                    connections.Add(pile);
                }
            }

            var burn = Burn(need.Store, need.Item, days);
            var target = HoldTarget(burn);
            var hub = SingleFamilyHub(need.Item, holds);
            if (connections.Count > 0)
            {
                foreach (var pile in connections)
                {
                    if (Covers(pile, need.Item, stops, needs, days))
                    {
                        return new Verdict("supplied", pile.Name, target);
                    }
                }

                return new Verdict("short", Best(connections).Name, target);
            }

            var oneDay = burn > 0 ? burn : 1;
            if (Dated(need, stops))
            {
                if (hub != null && hub.Amount >= oneDay)
                {
                    return new Verdict("noplan", hub.Name, target);
                }

                return new Verdict("wholesaler", "", 0);
            }

            if (hub != null)
            {
                return new Verdict("noplan", hub.Name, target);
            }

            var dry = PlanWarehouse(need, piles, stops);
            if (dry != null)
            {
                return new Verdict("dry", dry, target);
            }

            return new Verdict("noplan", "", target);
        }

        private static bool Sellable(string? item)
        {
            if (string.IsNullOrEmpty(item) || item == "ba:itemname_haircareproduct")
            {
                return item == "ba:itemname_haircareproduct";
            }

            var family = Advisor.CargoFamily(item);
            return family == "food" || family == "durables" || family == "specialty";
        }

        private static int Burn(string store, string item, IReadOnlyList<DailyUnits> days)
        {
            var burn = 0;
            var latest = int.MinValue;
            foreach (var day in days)
            {
                if (day == null || day.Store != store || day.Item != item || !day.Open || day.Units <= 0)
                {
                    continue;
                }

                if (day.Day >= latest)
                {
                    latest = day.Day;
                    burn = day.Units;
                }
            }

            return burn;
        }

        private static int HoldTarget(int burn)
        {
            if (burn <= 0)
            {
                return 0;
            }

            return RefillDays * burn;
        }

        private static HubPick? SingleFamilyHub(string item, IReadOnlyList<WarehouseHold> holds)
        {
            var family = Advisor.CargoFamily(item);
            if (string.IsNullOrEmpty(family) || family == "unknown")
            {
                return null;
            }

            var families = new Dictionary<string, HashSet<string>>();
            var amounts = new Dictionary<string, int>();
            var order = new List<string>();
            foreach (var hold in holds)
            {
                if (hold == null || hold.Amount <= 0)
                {
                    continue;
                }

                var cargo = Advisor.CargoFamily(hold.Item);
                if (string.IsNullOrEmpty(cargo))
                {
                    continue;
                }

                var name = hold.Warehouse.Length > 0
                    ? hold.Warehouse
                    : hold.Street + " " + hold.Number.ToString(CultureInfo.InvariantCulture);
                if (!families.TryGetValue(name, out var set))
                {
                    set = new HashSet<string>();
                    families[name] = set;
                    order.Add(name);
                }

                set.Add(cargo);
                if (hold.Item != item)
                {
                    continue;
                }

                amounts[name] = amounts.TryGetValue(name, out var have) ? have + hold.Amount : hold.Amount;
            }

            HubPick? best = null;
            foreach (var name in order)
            {
                if (!amounts.TryGetValue(name, out var amount) || amount <= 0)
                {
                    continue;
                }

                if (families[name].Count != 1 || !families[name].Contains(family))
                {
                    continue;
                }

                if (best == null || amount >= best.Amount)
                {
                    best = new HubPick(name, amount);
                }
            }

            return best;
        }

        private static bool Covers(
            Pile pile,
            string item,
            IReadOnlyList<DeliveryStop> stops,
            IReadOnlyList<GoodsNeed> needs,
            IReadOnlyList<DailyUnits> days)
        {
            long required = 0;
            var any = false;
            foreach (var need in needs)
            {
                if (need == null || need.Item != item || need.Store.Length == 0)
                {
                    continue;
                }

                if (!Connected(need.Store, item, pile, stops))
                {
                    continue;
                }

                any = true;
                var burn = Burn(need.Store, item, days);
                if (burn <= 0)
                {
                    return false;
                }

                required += (long)RefillDays * burn;
            }

            return any && pile.Amount >= required;
        }

        private static bool Connected(string store, string item, Pile pile, IReadOnlyList<DeliveryStop> stops)
        {
            foreach (var stop in stops)
            {
                if (stop == null || stop.Day != null || stop.Destination != store || stop.Item != item)
                {
                    continue;
                }

                if (pile.Matches(stop.Warehouse))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Dated(GoodsNeed need, IReadOnlyList<DeliveryStop> stops)
        {
            foreach (var stop in stops)
            {
                if (stop != null
                    && stop.Day != null
                    && stop.Destination == need.Store
                    && stop.Item == need.Item)
                {
                    return true;
                }
            }

            return false;
        }

        private static string? PlanWarehouse(GoodsNeed need, List<Pile> piles, IReadOnlyList<DeliveryStop> stops)
        {
            string? name = null;
            foreach (var stop in stops)
            {
                if (stop == null || stop.Day != null || stop.Destination != need.Store || stop.Item != need.Item)
                {
                    continue;
                }

                name = stop.Warehouse;
                foreach (var pile in piles)
                {
                    if (pile.Matches(stop.Warehouse))
                    {
                        name = pile.Name;
                    }
                }
            }

            return name;
        }

        private static Pile Best(List<Pile> piles)
        {
            var best = piles[0];
            for (var i = 1; i < piles.Count; i++)
            {
                if (piles[i].Amount >= best.Amount)
                {
                    best = piles[i];
                }
            }

            return best;
        }

        private static List<Pile> Piles(IReadOnlyList<WarehouseHold> holds, string item)
        {
            var piles = new List<Pile>();
            var index = new Dictionary<string, int>();
            foreach (var hold in holds)
            {
                if (hold == null || hold.Item != item)
                {
                    continue;
                }

                var address = hold.Street + " " + hold.Number.ToString(CultureInfo.InvariantCulture);
                var name = hold.Warehouse.Length > 0 ? hold.Warehouse : address;
                if (!index.TryGetValue(name, out var at))
                {
                    index[name] = piles.Count;
                    piles.Add(new Pile(name, address, hold.Amount));
                    continue;
                }

                piles[at].Add(address, hold.Amount);
            }

            return piles;
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

        private sealed class Pile
        {
            private readonly List<string> addresses = new List<string>();

            public Pile(string name, string address, int amount)
            {
                Name = name;
                Amount = amount;
                addresses.Add(address);
            }

            public string Name { get; }

            public int Amount { get; private set; }

            public void Add(string address, int amount)
            {
                Amount += amount;
                if (!addresses.Contains(address))
                {
                    addresses.Add(address);
                }
            }

            public bool Matches(string warehouse)
            {
                if (warehouse == Name)
                {
                    return true;
                }

                foreach (var address in addresses)
                {
                    if (warehouse == address)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private sealed class HubPick
        {
            public HubPick(string name, int amount)
            {
                Name = name ?? "";
                Amount = amount;
            }

            public string Name { get; }

            public int Amount { get; }
        }

        private readonly struct Verdict
        {
            public Verdict(string kind, string warehouse, int target)
            {
                Kind = kind;
                Warehouse = warehouse ?? "";
                Target = target;
            }

            public string Kind { get; }

            public string Warehouse { get; }

            public int Target { get; }
        }
    }
}
