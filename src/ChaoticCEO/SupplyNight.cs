using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class NightReport
    {
        public NightReport(SupplyList forecast, SupplyList recheck)
        {
            Forecast = forecast;
            Recheck = recheck;
        }

        public SupplyList Forecast { get; }

        public SupplyList Recheck { get; }
    }

    public static class SupplyNight
    {
        public const int HealCap = 10;

        public static string SnapshotPath(string localAppData)
        {
            return Path.Combine(localAppData ?? "", LoadLog.ModId, "snapshots.log");
        }

        public static NightReport Run(
            string localAppData,
            int day,
            IReadOnlyList<GoodsNeed>? needs,
            IReadOnlyList<DailyUnits>? sales,
            bool dryRun)
        {
            var routesPath = RouteDump.RoutesPath(localAppData);
            var holdsPath = RouteDump.WarehousesPath(localAppData);
            var forecast = Supply.FromDump(routesPath, holdsPath, needs, sales);
            LogForecast(localAppData, day, forecast);
            var before = File.Exists(routesPath) ? File.ReadAllText(routesPath) : "";
            Take(localAppData, before);
            Connect(localAppData, day, routesPath, holdsPath, forecast, dryRun);
            var recheck = Supply.FromDump(routesPath, holdsPath, needs, sales);
            LogRecheck(localAppData, day, forecast, recheck);
            return new NightReport(forecast, recheck);
        }

        private static void Connect(
            string localAppData,
            int day,
            string routesPath,
            string holdsPath,
            SupplyList forecast,
            bool dryRun)
        {
            var stops = new List<DeliveryStop>(Supply.Stops(routesPath));
            var added = 0;
            var changed = false;
            foreach (var gap in forecast.Unsupplied)
            {
                if (gap == null || gap.Reason != "noplan" || gap.Warehouse.Length == 0)
                {
                    continue;
                }

                if (added >= HealCap)
                {
                    break;
                }

                var amount = gap.Target > 0 ? gap.Target / Supply.RefillDays : 1;
                LogMove(localAppData, day, gap, amount, dryRun);
                added++;
                if (dryRun)
                {
                    continue;
                }

                stops.Add(new DeliveryStop(gap.Warehouse, gap.Store, gap.Item, null, ""));
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            RouteDump.Write(localAppData, stops, Supply.Stock(holdsPath));
        }

        private static void Take(string localAppData, string routesCsv)
        {
            var path = SnapshotPath(localAppData);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var line = "snapshot.taken version=" + LoadLog.Version;
            File.AppendAllText(path, line + System.Environment.NewLine + routesCsv);
        }

        private static void LogForecast(string localAppData, int day, SupplyList forecast)
        {
            var gaps = 0;
            foreach (var gap in forecast.Unsupplied)
            {
                if (gap == null)
                {
                    continue;
                }

                gaps++;
                Append(
                    localAppData,
                    "supply.forecast version=" + LoadLog.Version
                    + " day=" + day.ToString(CultureInfo.InvariantCulture)
                    + " store=" + gap.Store
                    + " warehouse=" + gap.Warehouse
                    + " item=" + gap.Item
                    + " reason=" + gap.Reason
                    + " buffer=" + gap.Target.ToString(CultureInfo.InvariantCulture));
            }

            Append(
                localAppData,
                "supply.summary version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " gaps=" + gaps.ToString(CultureInfo.InvariantCulture));
        }

        private static void LogRecheck(string localAppData, int day, SupplyList forecast, SupplyList recheck)
        {
            foreach (var gap in forecast.Unsupplied)
            {
                if (gap == null || gap.Reason != "noplan")
                {
                    continue;
                }

                Append(
                    localAppData,
                    "supply.rechecked version=" + LoadLog.Version
                    + " day=" + day.ToString(CultureInfo.InvariantCulture)
                    + " store=" + gap.Store
                    + " warehouse=" + gap.Warehouse
                    + " item=" + gap.Item
                    + " reason=" + Reason(recheck, gap));
            }
        }

        private static void LogMove(string localAppData, int day, SupplyGap gap, int amount, bool dryRun)
        {
            Append(
                localAppData,
                "supply.move version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " store=" + gap.Store
                + " warehouse=" + gap.Warehouse
                + " item=" + gap.Item
                + " amount=" + amount.ToString(CultureInfo.InvariantCulture)
                + " dry=" + (dryRun ? "true" : "false"));
        }

        private static string Reason(SupplyList list, SupplyGap gap)
        {
            foreach (var row in list.Unsupplied)
            {
                if (row != null && row.Store == gap.Store && row.Item == gap.Item)
                {
                    return row.Reason;
                }
            }

            foreach (var store in list.Wholesaler)
            {
                if (store == gap.Store)
                {
                    return "wholesaler";
                }
            }

            return "supplied";
        }

        private static void Append(string localAppData, string line)
        {
            var path = LoadLog.CeoLogPath(localAppData ?? "");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + System.Environment.NewLine);
        }
    }
}
