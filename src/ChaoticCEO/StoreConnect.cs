using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class StoreLink
    {
        public StoreLink(string store, string warehouse, string item, string add, string drop)
        {
            Store = store ?? "";
            Warehouse = warehouse ?? "";
            Item = item ?? "";
            Add = add ?? "";
            Drop = drop ?? "";
        }

        public string Store { get; }

        public string Warehouse { get; }

        public string Item { get; }

        public string Add { get; }

        public string Drop { get; }
    }

    public static class StoreConnect
    {
        public static IReadOnlyList<StoreLink> Connect(
            ApiNote? api,
            IReadOnlyList<DeliveryStop>? stops,
            IReadOnlyList<WarehouseHold>? holds,
            IReadOnlyList<GoodsNeed>? needs,
            IReadOnlyList<DailyUnits>? sales)
        {
            var add = api == null ? "" : api.Call("delivery");
            var drop = api == null ? "" : api.Call("wholesaler contract drop");
            var links = new List<StoreLink>();
            if (add.Length == 0 || drop.Length == 0)
            {
                return links;
            }

            var routes = stops ?? Array.Empty<DeliveryStop>();
            foreach (var gap in Supply.List(routes, holds, needs, sales).Unsupplied)
            {
                if (gap == null || gap.Reason != "noplan" || gap.Warehouse.Length == 0)
                {
                    continue;
                }

                links.Add(new StoreLink(
                    gap.Store,
                    gap.Warehouse,
                    gap.Item,
                    add,
                    Dated(routes, gap.Store, gap.Item) ? drop : ""));
            }

            return links;
        }

        public static IReadOnlyList<DeliveryStop> Apply(
            IReadOnlyList<DeliveryStop>? stops,
            IReadOnlyList<StoreLink>? links)
        {
            var routes = stops ?? Array.Empty<DeliveryStop>();
            var actions = links ?? Array.Empty<StoreLink>();
            var dropping = new HashSet<string>();
            foreach (var link in actions)
            {
                if (link != null && link.Drop.Length > 0)
                {
                    dropping.Add(Key(link.Store, link.Item));
                }
            }

            var next = new List<DeliveryStop>();
            foreach (var stop in routes)
            {
                if (stop == null)
                {
                    continue;
                }

                if (stop.Day != null && dropping.Contains(Key(stop.Destination, stop.Item)))
                {
                    continue;
                }

                next.Add(stop);
            }

            foreach (var link in actions)
            {
                if (link == null || link.Add.Length == 0 || link.Warehouse.Length == 0)
                {
                    continue;
                }

                if (!Planned(next, link))
                {
                    next.Add(new DeliveryStop(link.Warehouse, link.Store, link.Item, null, ""));
                }
            }

            return next;
        }

        public static string Line(int day, StoreLink link)
        {
            return "store.connected version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " store=" + link.Store
                + " warehouse=" + link.Warehouse
                + " item=" + link.Item
                + " add=" + link.Add
                + " drop=" + link.Drop;
        }

        private static bool Dated(IReadOnlyList<DeliveryStop> stops, string store, string item)
        {
            foreach (var stop in stops)
            {
                if (stop != null && stop.Day != null && stop.Destination == store && stop.Item == item)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Planned(List<DeliveryStop> stops, StoreLink link)
        {
            foreach (var stop in stops)
            {
                if (stop.Day == null
                    && stop.Warehouse == link.Warehouse
                    && stop.Destination == link.Store
                    && stop.Item == link.Item)
                {
                    return true;
                }
            }

            return false;
        }

        private static string Key(string store, string item)
        {
            return store + "\n" + item;
        }
    }

    public sealed class ConnectWatch
    {
        private object? _seen;

        public IReadOnlyList<string> Tick(
            string localAppData,
            object? save,
            int day,
            ApiNote? api,
            IReadOnlyList<DeliveryStop>? stops,
            IReadOnlyList<WarehouseHold>? holds,
            IReadOnlyList<GoodsNeed>? needs,
            IReadOnlyList<DailyUnits>? sales)
        {
            if (save == null)
            {
                return Array.Empty<string>();
            }

            var links = StoreConnect.Connect(api, stops, holds, needs, sales);
            if (links.Count > 0)
            {
                RouteDump.WriteRoutes(localAppData, StoreConnect.Apply(stops, links));
            }

            if (ReferenceEquals(_seen, save))
            {
                return Array.Empty<string>();
            }

            _seen = save;
            var lines = new List<string>();
            foreach (var link in links)
            {
                lines.Add(StoreConnect.Line(day, link));
            }

            if (lines.Count == 0)
            {
                return lines;
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
