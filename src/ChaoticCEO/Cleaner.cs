using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class CleanerPick
    {
        public CleanerPick(
            bool hired,
            string role,
            string skill,
            string street,
            int number,
            string call,
            string stationCall)
        {
            Hired = hired;
            Role = role ?? "";
            Skill = skill ?? "";
            Street = street ?? "";
            Number = number;
            Call = call ?? "";
            StationCall = stationCall ?? "";
        }

        public bool Hired { get; }

        public string Role { get; }

        public string Skill { get; }

        public string Street { get; }

        public int Number { get; }

        public string Call { get; }

        public string StationCall { get; }
    }

    public sealed class CleanerGap
    {
        public CleanerGap(bool hire, string reason, CleanerPick pick)
        {
            Hire = hire;
            Reason = reason ?? "";
            Pick = pick;
        }

        public bool Hire { get; }

        public string Reason { get; }

        public CleanerPick Pick { get; }
    }

    public static class Cleaner
    {
        public const string Skill = "ba:skill_cleaning";

        public const string RoleName = "Cleaner";

        public static CleanerGap Cover(
            string? place,
            bool cleanerStaffed,
            bool resigned,
            bool retired,
            bool poached,
            ApiNote? api)
        {
            var reason = CashierCover.Reason(resigned, retired, poached);
            var call = api == null ? "" : api.Call("hire");
            var stationCall = api == null ? "" : api.Call("cleaner");
            if (cleanerStaffed
                || !Split(place, out var street, out var number)
                || call.Length == 0
                || stationCall.Length == 0)
            {
                return new CleanerGap(false, "", None());
            }

            return new CleanerGap(
                true,
                reason,
                new CleanerPick(true, RoleName, Skill, street, number, call, stationCall));
        }

        public static string Replaced(string localAppData, CleanerPick? pick, int day, string? reason)
        {
            if (pick == null || !pick.Hired)
            {
                return "";
            }

            var why = string.IsNullOrEmpty(reason) ? "absent" : reason;
            var line = "cleaner.replaced version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + pick.Street
                + " number=" + pick.Number.ToString(CultureInfo.InvariantCulture)
                + " reason=" + why
                + " role=" + pick.Role
                + " skill=" + pick.Skill
                + " call=" + pick.Call
                + " stationCall=" + pick.StationCall;
            return AppendOnce(localAppData, line);
        }

        public static string Missed(string localAppData, CleanerPick? pick, int day, string? reason)
        {
            if (pick == null || pick.Street.Length == 0 || pick.Number <= 0)
            {
                return "";
            }

            var line = "cleaner.missed version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + pick.Street
                + " number=" + pick.Number.ToString(CultureInfo.InvariantCulture)
                + " reason=" + (string.IsNullOrEmpty(reason) ? "absent" : reason);
            return AppendOnce(localAppData, line);
        }

        private static bool Split(string? place, out string street, out int number)
        {
            street = "";
            number = 0;
            if (string.IsNullOrWhiteSpace(place))
            {
                return false;
            }

            var at = place.LastIndexOf(' ');
            if (at <= 0 || at >= place.Length - 1)
            {
                return false;
            }

            street = place.Substring(0, at);
            return int.TryParse(
                    place.Substring(at + 1),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out number)
                && street.Length > 0
                && number > 0;
        }

        private static CleanerPick None()
        {
            return new CleanerPick(false, "", "", "", 0, "", "");
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
