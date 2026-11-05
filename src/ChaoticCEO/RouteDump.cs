using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ChaoticCEO
{
    public sealed class DeliveryStop
    {
        public DeliveryStop(string warehouse, string destination, string item, int? day, string truck)
        {
            Warehouse = warehouse ?? "";
            Destination = destination ?? "";
            Item = item ?? "";
            Day = day;
            Truck = truck ?? "";
        }

        public string Warehouse { get; }

        public string Destination { get; }

        public string Item { get; }

        public int? Day { get; }

        public string Truck { get; }
    }

    public sealed class WarehouseHold
    {
        public WarehouseHold(string warehouse, string street, int number, string item, int amount)
        {
            Warehouse = warehouse ?? "";
            Street = street ?? "";
            Number = number;
            Item = item ?? "";
            Amount = amount;
        }

        public string Warehouse { get; }

        public string Street { get; }

        public int Number { get; }

        public string Item { get; }

        public int Amount { get; }
    }

    public static class RouteDump
    {
        public static string RoutesPath(string localAppData)
        {
            return Path.Combine(localAppData, LoadLog.ModId, "routes.csv");
        }

        public static string WarehousesPath(string localAppData)
        {
            return Path.Combine(localAppData, LoadLog.ModId, "warehouses.csv");
        }

        public static IReadOnlyList<WarehouseHold> CountHolds(
            string warehouse,
            string street,
            int number,
            IReadOnlyList<string> placedItems,
            IReadOnlyList<string> cargoItems)
        {
            var counts = new Dictionary<string, int>();
            var order = new List<string>();
            if (cargoItems != null)
            {
                foreach (var name in cargoItems)
                {
                    var item = name ?? "";
                    if (!counts.ContainsKey(item))
                    {
                        order.Add(item);
                        counts[item] = 0;
                    }

                    counts[item]++;
                }
            }

            _ = placedItems;

            var rows = new List<WarehouseHold>();
            foreach (var item in order)
            {
                rows.Add(new WarehouseHold(warehouse, street, number, item, counts[item]));
            }

            return rows;
        }

        public static string Write(
            string localAppData,
            IReadOnlyList<DeliveryStop> routes,
            IReadOnlyList<WarehouseHold> holds)
        {
            var plans = routes ?? Array.Empty<DeliveryStop>();
            var stock = holds ?? Array.Empty<WarehouseHold>();
            var routesPath = RoutesPath(localAppData);
            var directory = Path.GetDirectoryName(routesPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(routesPath, RoutesCsv(plans));
            File.WriteAllText(WarehousesPath(localAppData), HoldsCsv(stock));
            var line = Line(plans, stock);
            File.AppendAllText(LoadLog.CeoLogPath(localAppData), line + Environment.NewLine);
            return line;
        }

        public static void WriteRoutes(string localAppData, IReadOnlyList<DeliveryStop> routes)
        {
            var plans = routes ?? Array.Empty<DeliveryStop>();
            var routesPath = RoutesPath(localAppData ?? "");
            var directory = Path.GetDirectoryName(routesPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(routesPath, RoutesCsv(plans));
        }

        private static string RoutesCsv(IReadOnlyList<DeliveryStop> routes)
        {
            var text = new StringBuilder();
            text.Append("warehouse,destination,item,day,truck\n");
            foreach (var route in routes)
            {
                if (route == null)
                {
                    continue;
                }

                text.Append(route.Warehouse);
                text.Append(',');
                text.Append(route.Destination);
                text.Append(',');
                text.Append(route.Item);
                text.Append(',');
                text.Append(Day(route.Day));
                text.Append(',');
                text.Append(route.Truck);
                text.Append('\n');
            }

            return text.ToString();
        }

        private static string HoldsCsv(IReadOnlyList<WarehouseHold> holds)
        {
            var text = new StringBuilder();
            text.Append("warehouse,street,number,item,amount\n");
            foreach (var hold in holds)
            {
                if (hold == null)
                {
                    continue;
                }

                text.Append(hold.Warehouse);
                text.Append(',');
                text.Append(hold.Street);
                text.Append(',');
                text.Append(hold.Number.ToString(CultureInfo.InvariantCulture));
                text.Append(',');
                text.Append(hold.Item);
                text.Append(',');
                text.Append(hold.Amount.ToString(CultureInfo.InvariantCulture));
                text.Append('\n');
            }

            return text.ToString();
        }

        private static string Line(IReadOnlyList<DeliveryStop> routes, IReadOnlyList<WarehouseHold> holds)
        {
            DeliveryStop? first = null;
            var routeCount = 0;
            foreach (var route in routes)
            {
                if (route == null)
                {
                    continue;
                }

                if (first == null)
                {
                    first = route;
                }

                routeCount++;
            }

            var holdCount = 0;
            foreach (var hold in holds)
            {
                if (hold != null)
                {
                    holdCount++;
                }
            }

            return "routes.written version=" + LoadLog.Version
                + " routes=" + routeCount.ToString(CultureInfo.InvariantCulture)
                + " warehouses=" + holdCount.ToString(CultureInfo.InvariantCulture)
                + " warehouse=" + (first == null ? "" : first.Warehouse)
                + " destination=" + (first == null ? "" : first.Destination)
                + " item=" + (first == null ? "" : first.Item)
                + " day=" + (first == null ? "" : Day(first.Day));
        }

        private static string Day(int? day)
        {
            if (day == null)
            {
                return "";
            }

            return day.Value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
