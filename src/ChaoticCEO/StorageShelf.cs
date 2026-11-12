using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class ShelfPick
    {
        public ShelfPick(
            bool ordered,
            string item,
            int amount,
            int price,
            string street,
            int number,
            string fromStreet,
            int fromNumber,
            string call)
        {
            Ordered = ordered;
            Item = item ?? "";
            Amount = amount;
            Price = price;
            Street = street ?? "";
            Number = number;
            FromStreet = fromStreet ?? "";
            FromNumber = fromNumber;
            Call = call ?? "";
        }

        public bool Ordered { get; }

        public string Item { get; }

        public int Amount { get; }

        public int Price { get; }

        public string Street { get; }

        public int Number { get; }

        public string FromStreet { get; }

        public int FromNumber { get; }

        public string Call { get; }

        public static ShelfPick None()
        {
            return new ShelfPick(false, "", 0, 0, "", 0, "", 0, "");
        }
    }

    public static class StorageShelf
    {
        public const string Item = "ba:itemname_storageshelf";

        public const int Price = 1200;

        public const int Boxes = 16;

        public const string HamptonStreet = "ba:street_seventhavenue";

        public const int HamptonNumber = 13;

        public static ShelfPick Order(
            string? street,
            int number,
            int freeSpace,
            decimal cash,
            bool pending,
            ApiNote? api)
        {
            var call = api == null ? "" : api.Call("storage shelf");
            if (freeSpace > 0
                || cash < Price
                || pending
                || string.IsNullOrEmpty(street)
                || number <= 0
                || call.Length == 0)
            {
                return ShelfPick.None();
            }

            return new ShelfPick(
                true,
                Item,
                1,
                Price,
                street,
                number,
                HamptonStreet,
                HamptonNumber,
                call);
        }

        public static string Line(ShelfPick? pick, int day)
        {
            if (pick == null || !pick.Ordered)
            {
                return "";
            }

            return "shelf.ordered version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + pick.Street
                + " number=" + pick.Number.ToString(CultureInfo.InvariantCulture)
                + " item=" + pick.Item
                + " amount=" + pick.Amount.ToString(CultureInfo.InvariantCulture)
                + " price=" + pick.Price.ToString(CultureInfo.InvariantCulture)
                + " from=" + pick.FromStreet
                + " fromNumber=" + pick.FromNumber.ToString(CultureInfo.InvariantCulture)
                + " call=" + pick.Call;
        }

        public static string Record(string localAppData, ShelfPick? pick, int day)
        {
            var line = Line(pick, day);
            if (line.Length == 0)
            {
                return "";
            }

            var path = LoadLog.CeoLogPath(localAppData ?? "");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + System.Environment.NewLine);
            return line;
        }
    }
}
