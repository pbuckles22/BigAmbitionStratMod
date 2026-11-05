using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public static class DayRoll
    {
        public static bool ShouldDump(int? seen, int day)
        {
            return seen.HasValue && seen.Value != day;
        }

        public static string? Apply(
            string localAppData,
            int? seen,
            int day,
            IReadOnlyList<FinancialDay> days,
            IReadOnlyList<DeliveryStop> routes,
            IReadOnlyList<WarehouseHold> holds,
            IReadOnlyList<GoodsNeed>? needs = null,
            IReadOnlyList<DailyUnits>? sales = null)
        {
            if (!ShouldDump(seen, day))
            {
                return null;
            }

            StatementDump.Write(localAppData, day, days);
            RouteDump.Write(localAppData, routes, holds);
            var line = Line(day);
            File.AppendAllText(LoadLog.CeoLogPath(localAppData), line + System.Environment.NewLine);
            if (needs != null)
            {
                SupplyNight.Run(localAppData, day, needs, sales, dryRun: true);
            }

            return line;
        }

        private static string Line(int day)
        {
            return "day.rolled version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class DayWatch
    {
        private int? _seen;

        public bool Due(int day)
        {
            if (!_seen.HasValue)
            {
                _seen = day;
                return false;
            }

            return _seen.Value != day;
        }

        public string? Tick(
            string localAppData,
            int day,
            IReadOnlyList<FinancialDay> days,
            IReadOnlyList<DeliveryStop> routes,
            IReadOnlyList<WarehouseHold> holds,
            IReadOnlyList<GoodsNeed>? needs = null,
            IReadOnlyList<DailyUnits>? sales = null)
        {
            var line = DayRoll.Apply(localAppData, _seen, day, days, routes, holds, needs, sales);
            _seen = day;
            return line;
        }
    }
}
