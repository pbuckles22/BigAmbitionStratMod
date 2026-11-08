using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class CashierPick
    {
        public CashierPick(
            bool hired,
            string role,
            string skill,
            string type,
            string street,
            int number,
            string call,
            string stationCall)
        {
            Hired = hired;
            Role = role ?? "";
            Skill = skill ?? "";
            Type = type ?? "";
            Street = street ?? "";
            Number = number;
            Call = call ?? "";
            StationCall = stationCall ?? "";
        }

        public bool Hired { get; }

        public string Role { get; }

        public string Skill { get; }

        public string Type { get; }

        public string Street { get; }

        public int Number { get; }

        public string Call { get; }

        public string StationCall { get; }
    }

    public static class Cashier
    {
        public const string Skill = "ba:skill_customerservice";

        public const string RoleName = "Cashier";

        public const string GiftType = "SelfServiceEmployee";

        public static bool Retry(string? reason)
        {
            return reason == "notstarted"
                || reason == "nostation"
                || reason == "nocharacter";
        }

        public static string RegisterType(string? customerType)
        {
            if (customerType == "SelfService" || customerType == "CinemaTheater")
            {
                return GiftType;
            }

            if (customerType == "FullService" || customerType == "Hairdresser")
            {
                return "FullServiceEmployee";
            }

            if (customerType == "Nightclub" || customerType == "Casino")
            {
                return "BarmanEmployee";
            }

            return GiftType;
        }

        public static CashierPick Hire(
            BuildingRent? rent,
            StarterPick? layout,
            WholesalerPick? contract,
            bool alreadyHired,
            ApiNote? api)
        {
            var call = api == null ? "" : api.Call("hire");
            var stationCall = api == null ? "" : api.Call("cashier");
            if (alreadyHired
                || rent == null
                || !rent.Rented
                || rent.Street.Length == 0
                || rent.Number <= 0
                || layout == null
                || !layout.Loaded
                || contract == null
                || !contract.Signed
                || call.Length == 0
                || stationCall.Length == 0)
            {
                return None();
            }

            return new CashierPick(
                true,
                RoleName,
                Skill,
                GiftType,
                rent.Street,
                rent.Number,
                call,
                stationCall);
        }

        public static string Record(string localAppData, CashierPick? pick, int day)
        {
            if (pick == null || !pick.Hired)
            {
                return "";
            }

            var line = "hire.cashier version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + pick.Street
                + " number=" + pick.Number.ToString(CultureInfo.InvariantCulture)
                + " role=" + pick.Role
                + " skill=" + pick.Skill
                + " type=" + pick.Type
                + " call=" + pick.Call
                + " stationCall=" + pick.StationCall;
            return AppendOnce(localAppData, line);
        }

        public static string Miss(string localAppData, CashierPick pick, int day, string reason)
        {
            var line = "hire.missed version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + pick.Street
                + " number=" + pick.Number.ToString(CultureInfo.InvariantCulture)
                + " reason=" + (reason ?? "");
            return Append(localAppData, line);
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

            return Append(localAppData ?? "", line);
        }

        private static string Append(string localAppData, string line)
        {
            var path = LoadLog.CeoLogPath(localAppData ?? "");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + System.Environment.NewLine);
            return line;
        }

        private static CashierPick None()
        {
            return new CashierPick(false, "", "", "", "", 0, "", "");
        }
    }

    public sealed class CashierWatch
    {
        private object? _seen;
        private int _tries;

        public bool Due(object? save)
        {
            return save != null && !ReferenceEquals(_seen, save);
        }

        public bool Waiting()
        {
            return _tries < 45;
        }

        public void Note()
        {
            _tries++;
        }

        public void Mark(object? save)
        {
            _seen = save;
        }
    }
}
