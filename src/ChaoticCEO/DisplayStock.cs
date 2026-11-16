using System.Collections.Generic;
using System.Globalization;

namespace ChaoticCEO
{
    public sealed class DisplayLine
    {
        public DisplayLine(string? item, int onDisplay)
        {
            Item = item ?? "";
            OnDisplay = onDisplay < 0 ? 0 : onDisplay;
        }

        public string Item { get; }

        public int OnDisplay { get; }
    }

    public sealed class DisplayShop
    {
        public DisplayShop(int day, IReadOnlyList<DisplayLine>? lines)
        {
            Day = day;
            Lines = lines ?? new DisplayLine[0];
        }

        public int Day { get; }

        public IReadOnlyList<DisplayLine> Lines { get; }
    }

    public sealed class DisplayMorning
    {
        public DisplayMorning(
            int day,
            int ordered,
            IReadOnlyDictionary<string, int>? onDisplay,
            string? line)
        {
            Day = day;
            Ordered = ordered < 0 ? 0 : ordered;
            OnDisplay = onDisplay ?? new Dictionary<string, int>();
            Line = line ?? "";
        }

        public int Day { get; }

        public int Ordered { get; }

        public IReadOnlyDictionary<string, int> OnDisplay { get; }

        public string Line { get; }
    }

    public static class DisplayStock
    {
        public static DisplayShop Open()
        {
            var lines = new List<DisplayLine>();
            foreach (var item in Wholesaler.GiftShopWholesalersStock())
            {
                lines.Add(new DisplayLine(item, 0));
            }

            return new DisplayShop(1, lines);
        }

        public static DisplayShop Roll(DisplayShop? shop, string? dayRolled)
        {
            var current = shop ?? Open();
            var day = DayOf(dayRolled);
            if (day <= current.Day)
            {
                return current;
            }

            return new DisplayShop(day, current.Lines);
        }

        public static DisplayMorning Morning(DisplayShop? shop)
        {
            var current = shop ?? Open();
            return Morning(current.Day, current.Lines);
        }

        public static DisplayMorning Morning(int day, IReadOnlyList<DisplayLine>? lines)
        {
            var stock = new Dictionary<string, int>();
            var ordered = 0;
            if (day > 0 && lines != null)
            {
                foreach (var line in lines)
                {
                    if (line == null || line.Item.Length == 0 || line.Item == StorageShelf.Item)
                    {
                        continue;
                    }

                    if (line.OnDisplay > 0)
                    {
                        stock[line.Item] = line.OnDisplay;
                        continue;
                    }

                    ordered++;
                    stock[line.Item] = 1;
                }
            }

            return new DisplayMorning(day, ordered, stock, Restock.Summary(day, ordered));
        }

        public static string Stocked(int day, string? street, int number, string? item, int amount)
        {
            return "display.stocked version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + (street ?? "")
                + " number=" + number.ToString(CultureInfo.InvariantCulture)
                + " item=" + (item ?? "")
                + " amount=" + amount.ToString(CultureInfo.InvariantCulture);
        }

        public static StoreHealth Gate(DisplayShop? shop)
        {
            var current = shop ?? Open();
            return Assess(Map(current.Lines), Names(current.Lines));
        }

        public static StoreHealth Gate(DisplayMorning? morning)
        {
            if (morning == null)
            {
                return Gate(Open());
            }

            var names = new List<string>();
            foreach (var pair in morning.OnDisplay)
            {
                if (pair.Key.Length == 0 || pair.Key == StorageShelf.Item)
                {
                    continue;
                }

                names.Add(pair.Key);
            }

            return Assess(morning.OnDisplay, names);
        }

        private static StoreHealth Assess(
            IReadOnlyDictionary<string, int> onDisplay,
            IReadOnlyList<string> needed)
        {
            return StoreHealth.Assess(
                new StoreFacts(
                    "ba:street_seventhavenue 22",
                    temporarilyClosed: false,
                    needed,
                    onDisplay,
                    registerStaffed: true,
                    cleanerStaffed: true,
                    staffSatisfaction: 100f,
                    scheduledOpen: false,
                    dailyCustomers: 0,
                    dailyRevenue: 0m,
                    recentOpenDayProfits: new decimal[0]));
        }

        private static Dictionary<string, int> Map(IReadOnlyList<DisplayLine> lines)
        {
            var stock = new Dictionary<string, int>();
            foreach (var line in lines)
            {
                if (line == null || line.Item.Length == 0 || line.Item == StorageShelf.Item)
                {
                    continue;
                }

                stock[line.Item] = line.OnDisplay;
            }

            return stock;
        }

        private static List<string> Names(IReadOnlyList<DisplayLine> lines)
        {
            var names = new List<string>();
            foreach (var line in lines)
            {
                if (line == null || line.Item.Length == 0 || line.Item == StorageShelf.Item)
                {
                    continue;
                }

                names.Add(line.Item);
            }

            return names;
        }

        private static int DayOf(string? line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return 0;
            }

            const string key = "day=";
            var at = line.LastIndexOf(key, System.StringComparison.Ordinal);
            if (at < 0)
            {
                return 0;
            }

            var text = line.Substring(at + key.Length);
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var day)
                ? day
                : 0;
        }
    }
}
