using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class StarterOffer
    {
        public StarterOffer(string businessType, string layout, decimal dailyProfit)
        {
            BusinessType = businessType ?? "";
            Layout = layout ?? "";
            DailyProfit = dailyProfit;
        }

        public string BusinessType { get; }

        public string Layout { get; }

        public decimal DailyProfit { get; }
    }

    public sealed class StarterPick
    {
        public StarterPick(
            string businessType,
            string layout,
            string name,
            bool loaded,
            string call,
            int openFrom,
            int openTo)
        {
            BusinessType = businessType ?? "";
            Layout = layout ?? "";
            Name = name ?? "";
            Loaded = loaded;
            Call = call ?? "";
            OpenFrom = openFrom;
            OpenTo = openTo;
        }

        public string BusinessType { get; }

        public string Layout { get; }

        public string Name { get; }

        public bool Loaded { get; }

        public string Call { get; }

        public int OpenFrom { get; }

        public int OpenTo { get; }
    }

    public static class Starter
    {
        public static IReadOnlyList<StarterOffer> StageOne()
        {
            return new[]
            {
                new StarterOffer("ba:businesstype_giftshop", "GiftShopWholesalers", 40m),
                new StarterOffer("ba:businesstype_florist", "FloristWholesalers", 25m),
                new StarterOffer("ba:businesstype_fastfoodrestaurant", "FastFoodKabob", 30m),
            };
        }

        public static StarterPick Choose(
            IReadOnlyList<StarterOffer>? offers,
            bool alreadyOpen,
            ApiNote? api)
        {
            var call = api == null ? "" : api.Call("clone");
            if (alreadyOpen || offers == null || call.Length == 0)
            {
                return None();
            }

            StarterOffer? winner = null;
            foreach (var offer in offers)
            {
                if (offer == null
                    || !StageOneType(offer.BusinessType)
                    || offer.Layout.Length == 0
                    || offer.DailyProfit < 0m)
                {
                    continue;
                }

                if (winner == null || offer.DailyProfit >= winner.DailyProfit)
                {
                    winner = offer;
                }
            }

            if (winner == null)
            {
                return None();
            }

            return new StarterPick(
                winner.BusinessType,
                winner.Layout,
                StoreName(winner.BusinessType),
                true,
                call,
                8,
                16);
        }

        public static string Record(string localAppData, StarterPick? pick, BuildingRent? rent, int day)
        {
            if (pick == null || !pick.Loaded || rent == null || !rent.Rented)
            {
                return "";
            }

            var line = "layout.loaded version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + rent.Street
                + " number=" + rent.Number.ToString(CultureInfo.InvariantCulture)
                + " type=" + pick.BusinessType
                + " layout=" + pick.Layout
                + " open=" + pick.OpenFrom.ToString(CultureInfo.InvariantCulture)
                + "-" + pick.OpenTo.ToString(CultureInfo.InvariantCulture)
                + " call=" + pick.Call;
            return AppendOnce(localAppData, line);
        }

        public static string Miss(string localAppData, StarterPick pick, BuildingRent rent, int day)
        {
            var line = "layout.missed version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + rent.Street
                + " number=" + rent.Number.ToString(CultureInfo.InvariantCulture)
                + " type=" + pick.BusinessType
                + " layout=" + pick.Layout
                + " call=" + pick.Call;
            var path = LoadLog.CeoLogPath(localAppData ?? "");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + System.Environment.NewLine);
            return line;
        }

        public static string Skip(string localAppData, int day)
        {
            var line = "layout.skipped version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " reason=open";
            var path = LoadLog.CeoLogPath(localAppData ?? "");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + System.Environment.NewLine);
            CeoLedger.Ingest(line);
            return line;
        }

        private static string AppendOnce(string localAppData, string line)
        {
            var path = LoadLog.CeoLogPath(localAppData ?? "");
            if (File.Exists(path))
            {
                foreach (var existing in File.ReadAllLines(path))
                {
                    if (existing == line)
                    {
                        CeoLedger.Ingest(line);
                        return line;
                    }
                }
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + System.Environment.NewLine);
            CeoLedger.Ingest(line);
            return line;
        }

        private static bool StageOneType(string businessType)
        {
            return businessType == "ba:businesstype_giftshop"
                || businessType == "ba:businesstype_florist"
                || businessType == "ba:businesstype_fastfoodrestaurant";
        }

        private static string StoreName(string businessType)
        {
            if (businessType == "ba:businesstype_giftshop")
            {
                return "Starter Gift";
            }

            if (businessType == "ba:businesstype_florist")
            {
                return "Starter Florist";
            }

            if (businessType == "ba:businesstype_fastfoodrestaurant")
            {
                return "Starter Kabob";
            }

            return "";
        }

        private static StarterPick None()
        {
            return new StarterPick("", "", "", false, "", 0, 0);
        }
    }

    public sealed class StarterWatch
    {
        private object? _seen;

        public bool Due(object? save)
        {
            return save != null && !ReferenceEquals(_seen, save);
        }

        public void Mark(object? save)
        {
            _seen = save;
        }
    }
}
