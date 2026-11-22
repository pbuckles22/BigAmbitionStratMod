using System;
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
            ApiNote? api,
            string? businessType = null)
        {
            var call = api == null ? "" : api.Call("clone");
            var known = businessType ?? "";
            if (known == "ba:businesstype_empty")
            {
                known = "";
            }

            if (known.Length > 0)
            {
                var modeLayout = Modes.FitOut(known);
                if (alreadyOpen || call.Length == 0 || modeLayout.Length == 0)
                {
                    return new StarterPick(known, "", "", false, call, 0, 0);
                }

                return new StarterPick(
                    known,
                    modeLayout,
                    StoreName(known),
                    true,
                    call,
                    8,
                    16);
            }

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

            var layout = Modes.FitOut(winner.BusinessType);
            if (layout.Length == 0)
            {
                layout = winner.Layout;
            }

            return new StarterPick(
                winner.BusinessType,
                layout,
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

        public static string BusinessName(string? businessType, string? neighborhood, string? street, int number)
        {
            var baseName = StoreName(businessType ?? "");
            if (baseName.Length == 0)
            {
                return "";
            }

            var place = PlaceToken(street, number);
            var initials = Initials(neighborhood);
            if (place.Length == 0 && initials.Length == 0)
            {
                return baseName;
            }

            if (place.Length == 0)
            {
                return baseName + " " + initials;
            }

            if (initials.Length == 0)
            {
                return baseName + " " + place;
            }

            return baseName + " " + place + " " + initials;
        }

        public static IReadOnlyList<NamedStore> Distinct(IReadOnlyList<NamedStore>? stores)
        {
            if (stores == null || stores.Count == 0)
            {
                return Array.Empty<NamedStore>();
            }

            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var store in stores)
            {
                var name = store == null ? "" : store.Name;
                if (name.Length == 0)
                {
                    continue;
                }

                counts[name] = counts.TryGetValue(name, out var seen) ? seen + 1 : 1;
            }

            var renamed = new List<NamedStore>(stores.Count);
            foreach (var store in stores)
            {
                if (store == null)
                {
                    renamed.Add(new NamedStore("", 0, "", "", ""));
                    continue;
                }

                if (store.Name.Length > 0 && counts.TryGetValue(store.Name, out var seen) && seen > 1)
                {
                    var next = BusinessName(store.BusinessType, store.Neighborhood, store.Street, store.Number);
                    renamed.Add(next.Length == 0 ? store : store.WithName(next));
                    continue;
                }

                renamed.Add(store);
            }

            return renamed;
        }

        public static string RecordName(string localAppData, int day, string? street, int number, string? name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "";
            }

            var line = "store.named version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + (street ?? "")
                + " number=" + number.ToString(CultureInfo.InvariantCulture)
                + " name=" + name.Replace(" ", "_");
            return AppendOnce(localAppData, line);
        }

        public static string Initials(string? neighborhood)
        {
            var display = DisplayNeighborhood(neighborhood);
            if (display.Length == 0)
            {
                return "";
            }

            var letters = "";
            foreach (var word in display.Split(' '))
            {
                if (word.Length == 0)
                {
                    continue;
                }

                letters += char.ToUpperInvariant(word[0]);
            }

            return letters;
        }

        private static string DisplayNeighborhood(string? neighborhood)
        {
            var text = (neighborhood ?? "").Trim();
            if (text.StartsWith("ba:", StringComparison.Ordinal))
            {
                text = text.Substring(3);
            }

            const string prefix = "neighborhood_";
            if (text.StartsWith(prefix, StringComparison.Ordinal))
            {
                text = text.Substring(prefix.Length);
            }

            text = text.Replace('_', ' ').Trim();
            var key = text.Replace(" ", "").Replace("'", "");
            if (key.Equals("garmentdistrict", StringComparison.OrdinalIgnoreCase))
            {
                return "Garment District";
            }

            if (key.Equals("hellskitchen", StringComparison.OrdinalIgnoreCase))
            {
                return "Hell's Kitchen";
            }

            if (key.Equals("murrayhill", StringComparison.OrdinalIgnoreCase))
            {
                return "Murray Hill";
            }

            if (key.Equals("midtown", StringComparison.OrdinalIgnoreCase))
            {
                return "Midtown";
            }

            if (key.Equals("lowermanhattan", StringComparison.OrdinalIgnoreCase))
            {
                return "Lower Manhattan";
            }

            if (key.Equals("industrycity", StringComparison.OrdinalIgnoreCase))
            {
                return "Industry City";
            }

            if (key.Equals("thehamptons", StringComparison.OrdinalIgnoreCase))
            {
                return "The Hamptons";
            }

            if (key.Equals("global", StringComparison.OrdinalIgnoreCase))
            {
                return "Global";
            }

            return text;
        }

        private static string PlaceToken(string? street, int number)
        {
            if (number <= 0)
            {
                return "";
            }

            var id = (street ?? "").Trim().ToLowerInvariant();
            const string streetPrefix = "ba:street_";
            if (id.StartsWith(streetPrefix, StringComparison.Ordinal))
            {
                id = id.Substring(streetPrefix.Length);
            }

            var kind = "";
            if (id.EndsWith("avenue", StringComparison.Ordinal))
            {
                kind = "AV";
                id = id.Substring(0, id.Length - "avenue".Length);
            }
            else if (id.EndsWith("street", StringComparison.Ordinal))
            {
                kind = "ST";
                id = id.Substring(0, id.Length - "street".Length);
            }
            else if (id.EndsWith("boulevard", StringComparison.Ordinal))
            {
                kind = "BL";
                id = id.Substring(0, id.Length - "boulevard".Length);
            }

            var digit = Ordinal(id);
            if (digit.Length == 0 || kind.Length == 0)
            {
                return number.ToString(CultureInfo.InvariantCulture);
            }

            return digit + kind + number.ToString(CultureInfo.InvariantCulture);
        }

        private static string Ordinal(string street)
        {
            if (street.StartsWith("twelfth", StringComparison.Ordinal)) return "12";
            if (street.StartsWith("eleventh", StringComparison.Ordinal)) return "11";
            if (street.StartsWith("tenth", StringComparison.Ordinal)) return "10";
            if (street.StartsWith("ninth", StringComparison.Ordinal)) return "9";
            if (street.StartsWith("eighth", StringComparison.Ordinal)) return "8";
            if (street.StartsWith("seventh", StringComparison.Ordinal)) return "7";
            if (street.StartsWith("sixth", StringComparison.Ordinal)) return "6";
            if (street.StartsWith("fifth", StringComparison.Ordinal)) return "5";
            if (street.StartsWith("fourth", StringComparison.Ordinal)) return "4";
            if (street.StartsWith("third", StringComparison.Ordinal)) return "3";
            if (street.StartsWith("second", StringComparison.Ordinal)) return "2";
            if (street.StartsWith("first", StringComparison.Ordinal)) return "1";
            return "";
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

            if (businessType == "ba:businesstype_hairdresser")
            {
                return "Starter Hair";
            }

            if (businessType == "ba:businesstype_lawfirm")
            {
                return "Starter Law";
            }

            if (businessType == "ba:businesstype_webdevelopmentagency")
            {
                return "Starter Web";
            }

            if (businessType == "ba:businesstype_graphicsdesignagency")
            {
                return "Starter Graphics";
            }

            if (businessType == "ba:businesstype_gym")
            {
                return "Starter Gym";
            }

            return "";
        }

        public static bool Hold(StarterPick? pick)
        {
            return pick == null || (!pick.Loaded && pick.BusinessType.Length == 0);
        }

        public static bool Settle(StarterWatch? watch, object? save, StarterPick? pick)
        {
            if (watch == null || Hold(pick))
            {
                return false;
            }

            watch.Mark(save);
            return true;
        }

        private static StarterPick None()
        {
            return new StarterPick("", "", "", false, "", 0, 0);
        }
    }

    public sealed class NamedStore
    {
        public NamedStore(string street, int number, string neighborhood, string businessType, string name)
        {
            Street = street ?? "";
            Number = number;
            Neighborhood = neighborhood ?? "";
            BusinessType = businessType ?? "";
            Name = name ?? "";
        }

        public string Street { get; }

        public int Number { get; }

        public string Neighborhood { get; }

        public string BusinessType { get; }

        public string Name { get; }

        public NamedStore WithName(string name)
        {
            return new NamedStore(Street, Number, Neighborhood, BusinessType, name);
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
