using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class CapacityPick
    {
        public CapacityPick(bool matched, string street, int number, string call)
        {
            Matched = matched;
            Street = street ?? "";
            Number = number;
            Call = call ?? "";
        }

        public bool Matched { get; }

        public string Street { get; }

        public int Number { get; }

        public string Call { get; }
    }

    public static class Capacity
    {
        public static CapacityPick Match(
            BuildingRent? rent,
            StarterPick? layout,
            bool alreadyMatched,
            ApiNote? api)
        {
            var call = api == null ? "" : api.Call("capacity fixtures");
            if (alreadyMatched
                || rent == null
                || !rent.Rented
                || rent.Street.Length == 0
                || rent.Number <= 0
                || layout == null
                || !layout.Loaded
                || call.Length == 0)
            {
                return None();
            }

            return new CapacityPick(true, rent.Street, rent.Number, call);
        }

        public static string Record(string localAppData, CapacityPick? pick, int day)
        {
            if (pick == null || !pick.Matched)
            {
                return "";
            }

            var line = "capacity.matched version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + pick.Street
                + " number=" + pick.Number.ToString(CultureInfo.InvariantCulture)
                + " call=" + pick.Call;
            return AppendOnce(localAppData, line);
        }

        public static string Miss(string localAppData, CapacityPick? pick, int day)
        {
            if (pick == null || !pick.Matched)
            {
                return "";
            }

            var line = "capacity.missed version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + pick.Street
                + " number=" + pick.Number.ToString(CultureInfo.InvariantCulture)
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

        private static CapacityPick None()
        {
            return new CapacityPick(false, "", 0, "");
        }
    }

    public sealed class CapacityWatch
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
