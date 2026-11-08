using System;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class PlaybookStep
    {
        public PlaybookStep(string kind, string detail, bool chosen)
        {
            Kind = kind ?? "";
            Detail = detail ?? "";
            Chosen = chosen;
        }

        public string Kind { get; }

        public string Detail { get; }

        public bool Chosen { get; }

        public static PlaybookStep None()
        {
            return new PlaybookStep("", "", false);
        }
    }

    public static class Playbook
    {
        private static string _seen = "";

        public static PlaybookStep Decide(
            OpenPick? open,
            SitePick? where,
            LayoutPick? layout,
            PricePick? prices,
            SupplyPick? supply,
            TruckPick? trucks,
            WarehousePick? warehouses,
            ManagerPick? managers,
            MoneyPick? money,
            EstatePick? realEstate,
            ClosePick? close,
            RunPick? run)
        {
            if (run != null && run.Chosen && (run.Action == "sleep" || run.Action == "eat"))
            {
                return new PlaybookStep("run", run.Action, true);
            }

            if (close != null && close.Chosen)
            {
                return new PlaybookStep("close", close.Action, true);
            }

            if (open != null && open.Chosen)
            {
                return new PlaybookStep("open", open.Type, true);
            }

            if (where != null && where.Chosen)
            {
                return new PlaybookStep(
                    "where",
                    where.Street + " " + where.Number.ToString(CultureInfo.InvariantCulture),
                    true);
            }

            if (layout != null && layout.Chosen)
            {
                return new PlaybookStep("layout", layout.Blueprint, true);
            }

            if (prices != null && prices.Chosen)
            {
                return new PlaybookStep(
                    "prices",
                    prices.Item + " " + FormatAmount(prices.Price),
                    true);
            }

            if (supply != null && supply.Chosen)
            {
                var detail = supply.Source;
                if (supply.Warehouse.Length > 0)
                {
                    detail += " " + supply.Warehouse;
                }

                return new PlaybookStep("supply", detail, true);
            }

            if (trucks != null && trucks.Chosen)
            {
                return new PlaybookStep(
                    "trucks",
                    trucks.Warehouse + " " + trucks.Slot.ToString(CultureInfo.InvariantCulture),
                    true);
            }

            if (warehouses != null && warehouses.Chosen)
            {
                return new PlaybookStep(
                    "warehouses",
                    warehouses.Action + " " + warehouses.Family + " " + warehouses.Warehouse,
                    true);
            }

            if (managers != null && managers.Chosen)
            {
                var detail = managers.Role + " " + managers.Action;
                if (managers.Warehouse.Length > 0)
                {
                    detail += " " + managers.Warehouse;
                }

                return new PlaybookStep("managers", detail, true);
            }

            if (money != null && money.Chosen)
            {
                var detail = money.Action;
                if (money.Amount > 0m)
                {
                    detail += " " + FormatAmount(money.Amount);
                }

                return new PlaybookStep("money", detail, true);
            }

            if (realEstate != null && realEstate.Chosen)
            {
                return new PlaybookStep(
                    "realestate",
                    realEstate.Kind
                        + " "
                        + realEstate.Street
                        + " "
                        + realEstate.Number.ToString(CultureInfo.InvariantCulture),
                    true);
            }

            if (run != null && run.Chosen)
            {
                return new PlaybookStep("run", run.Action, true);
            }

            return PlaybookStep.None();
        }

        public static string Line(PlaybookStep? step)
        {
            if (step == null || !step.Chosen || step.Kind.Length == 0)
            {
                return "Next none";
            }

            return "Next " + step.Kind + " " + step.Detail;
        }

        public static string Record(string localAppData, PlaybookStep? step)
        {
            var kind = step == null || !step.Chosen ? "" : step.Kind;
            var detail = step == null || !step.Chosen ? "" : step.Detail;
            var shown = "playbook.shown version=" + LoadLog.Version
                + " kind=" + kind
                + " detail=" + detail;
            var key = (localAppData ?? "") + "\n" + shown;
            if (key == _seen)
            {
                return shown;
            }

            _seen = key;
            var path = LoadLog.CeoLogPath(localAppData ?? "");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, shown + Environment.NewLine);
            return shown;
        }

        private static string FormatAmount(decimal amount)
        {
            var rounded = Math.Round(amount, 4, MidpointRounding.ToEven);
            if (rounded == Math.Truncate(rounded))
            {
                return ((long)rounded).ToString(CultureInfo.InvariantCulture);
            }

            return rounded.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}
