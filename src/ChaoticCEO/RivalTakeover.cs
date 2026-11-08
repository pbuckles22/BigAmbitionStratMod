using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class TakeoverDeclinePick
    {
        public TakeoverDeclinePick(
            bool declined,
            string street,
            int number,
            decimal offerPrice,
            decimal dailyProfit,
            string call)
        {
            Declined = declined;
            Street = street ?? "";
            Number = number;
            OfferPrice = offerPrice;
            DailyProfit = dailyProfit;
            Call = call ?? "";
        }

        public bool Declined { get; }

        public string Street { get; }

        public int Number { get; }

        public decimal OfferPrice { get; }

        public decimal DailyProfit { get; }

        public string Call { get; }
    }

    public static class RivalTakeover
    {
        public const int PaybackMax = 365;

        public static TakeoverDeclinePick Decline(
            decimal offerPrice,
            decimal dailyProfit,
            string? street,
            int number,
            bool alreadyDeclined,
            ApiNote? api)
        {
            var call = api == null ? "" : api.Call("rival takeover decline");
            var place = street ?? "";
            if (alreadyDeclined
                || place.Length == 0
                || number <= 0
                || offerPrice <= 0m
                || dailyProfit <= 0m
                || call.Length == 0
                || BeatsPayback(offerPrice, dailyProfit))
            {
                return None();
            }

            return new TakeoverDeclinePick(true, place, number, offerPrice, dailyProfit, call);
        }

        public static bool BeatsPayback(decimal offerPrice, decimal dailyProfit)
        {
            if (dailyProfit <= 0m || offerPrice <= 0m)
            {
                return false;
            }

            return offerPrice >= dailyProfit * PaybackMax;
        }

        public static string Record(string localAppData, TakeoverDeclinePick? pick, int day)
        {
            if (pick == null || !pick.Declined)
            {
                return "";
            }

            var line = "takeover.declined version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + pick.Street
                + " number=" + pick.Number.ToString(CultureInfo.InvariantCulture)
                + " offer=" + pick.OfferPrice.ToString(CultureInfo.InvariantCulture)
                + " dailyProfit=" + pick.DailyProfit.ToString(CultureInfo.InvariantCulture)
                + " call=" + pick.Call;
            return AppendOnce(localAppData, line);
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

        private static TakeoverDeclinePick None()
        {
            return new TakeoverDeclinePick(false, "", 0, 0m, 0m, "");
        }
    }

    public sealed class RivalTakeoverWatch
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
