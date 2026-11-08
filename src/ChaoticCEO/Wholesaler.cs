using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class WholesalerOffer
    {
        public WholesalerOffer(string businessType, string wholesale)
        {
            BusinessType = businessType ?? "";
            Wholesale = wholesale ?? "";
        }

        public string BusinessType { get; }

        public string Wholesale { get; }
    }

    public sealed class WholesalerPick
    {
        public WholesalerPick(string wholesale, string street, int number, bool signed, string call)
        {
            Wholesale = wholesale ?? "";
            Street = street ?? "";
            Number = number;
            Signed = signed;
            Call = call ?? "";
        }

        public string Wholesale { get; }

        public string Street { get; }

        public int Number { get; }

        public bool Signed { get; }

        public string Call { get; }
    }

    public static class Wholesaler
    {
        public static IReadOnlyList<WholesalerOffer> StageOne()
        {
            return new[]
            {
                new WholesalerOffer("ba:businesstype_giftshop", "ba:street_firststreet 37"),
            };
        }

        public static WholesalerPick Sign(
            BuildingRent? rent,
            StarterPick? layout,
            IReadOnlyList<WholesalerOffer>? offers,
            bool alreadySigned,
            ApiNote? api)
        {
            var call = api == null ? "" : api.Call("wholesaler contract");
            if (alreadySigned
                || rent == null
                || !rent.Rented
                || rent.Street.Length == 0
                || rent.Number <= 0
                || layout == null
                || !layout.Loaded
                || offers == null
                || call.Length == 0)
            {
                return None();
            }

            WholesalerOffer? winner = null;
            foreach (var offer in offers)
            {
                if (offer == null
                    || offer.BusinessType.Length == 0
                    || offer.Wholesale.Length == 0
                    || offer.BusinessType != layout.BusinessType)
                {
                    continue;
                }

                winner = offer;
            }

            if (winner == null)
            {
                return None();
            }

            return new WholesalerPick(winner.Wholesale, rent.Street, rent.Number, true, call);
        }

        public static string Record(string localAppData, WholesalerPick? pick, int day)
        {
            if (pick == null || !pick.Signed)
            {
                return "";
            }

            var line = "contract.signed version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + pick.Street
                + " number=" + pick.Number.ToString(CultureInfo.InvariantCulture)
                + " wholesale=" + pick.Wholesale
                + " call=" + pick.Call;
            var path = LoadLog.CeoLogPath(localAppData ?? "");
            if (File.Exists(path))
            {
                foreach (var existing in File.ReadAllLines(path))
                {
                    if (existing == line)
                    {
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
            return line;
        }

        public static string Miss(string localAppData, WholesalerPick pick, int day)
        {
            var line = "contract.missed version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + pick.Street
                + " number=" + pick.Number.ToString(CultureInfo.InvariantCulture)
                + " wholesale=" + pick.Wholesale
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

        private static WholesalerPick None()
        {
            return new WholesalerPick("", "", 0, false, "");
        }
    }

    public sealed class WholesalerWatch
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
