using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public static class StorageRack
    {
        public const string Item = "ba:itemname_storagerack";

        public const int Price = 1200;

        public const int UnitCap = 200;

        public const decimal RackSpendRoom = 2500m;

        public const decimal RackTriggerRatio = 0.85m;

        public const string HamptonStreet = "ba:street_seventhavenue";

        public const int HamptonNumber = 13;

        public static ShelfPick Order(
            string? street,
            int number,
            int target,
            int fixtureCap,
            decimal spendRoom,
            bool pending,
            bool floorRoom,
            ApiNote? api)
        {
            var call = api == null ? "" : api.Call("storage rack");
            if (fixtureCap <= 0
                || target < fixtureCap * RackTriggerRatio
                || spendRoom < RackSpendRoom
                || pending
                || !floorRoom
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

            return "rack.ordered version=" + LoadLog.Version
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
    }
}
