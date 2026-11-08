using System.Collections.Generic;

namespace ChaoticCEO
{
    public sealed class WarehousePick
    {
        public WarehousePick(string family, string warehouse, string action, string reason, bool chosen)
        {
            Family = family ?? "";
            Warehouse = warehouse ?? "";
            Action = action ?? "";
            Reason = reason ?? "";
            Chosen = chosen;
        }

        public string Family { get; }

        public string Warehouse { get; }

        public string Action { get; }

        public string Reason { get; }

        public bool Chosen { get; }

        public static WarehousePick None()
        {
            return new WarehousePick("", "", "", "", false);
        }
    }

    public static class Warehouses
    {
        public static WarehousePick Decide(
            string? family,
            bool enteringFamily,
            bool hasStockWithoutHome,
            IReadOnlyList<WarehouseLevel>? homes,
            bool trucksBusy)
        {
            var name = family ?? "";
            if (name.Length == 0 || homes == null)
            {
                return WarehousePick.None();
            }

            WarehouseLevel? home = null;
            string empty = "";
            foreach (var level in homes)
            {
                if (level == null || level.Warehouse.Length == 0)
                {
                    continue;
                }

                if (level.Family == name)
                {
                    home = level;
                    continue;
                }

                if (empty.Length == 0
                    && level.Family.Length == 0
                    && level.Shelves > 0)
                {
                    empty = level.Warehouse;
                }
            }

            if (empty.Length == 0)
            {
                return WarehousePick.None();
            }

            if (home == null)
            {
                if (enteringFamily)
                {
                    return new WarehousePick(name, empty, "home", "enter", true);
                }

                if (hasStockWithoutHome)
                {
                    return new WarehousePick(name, empty, "home", "stock", true);
                }

                return WarehousePick.None();
            }

            if (trucksBusy)
            {
                return new WarehousePick(name, empty, "second", "trucks", true);
            }

            if (home.Ordered && home.Daily > 0 && home.OnHand < Supply.RefillDays * home.Daily)
            {
                return new WarehousePick(name, empty, "second", "short", true);
            }

            return WarehousePick.None();
        }

        public static string KeepFamily(IReadOnlyDictionary<string, int>? amounts)
        {
            if (amounts == null || amounts.Count == 0)
            {
                return "";
            }

            string winner = "";
            var best = int.MinValue;
            foreach (var pair in amounts)
            {
                if (string.IsNullOrEmpty(pair.Key) || pair.Value <= 0)
                {
                    continue;
                }

                if (winner.Length == 0 || pair.Value >= best)
                {
                    winner = pair.Key;
                    best = pair.Value;
                }
            }

            return winner;
        }
    }
}
