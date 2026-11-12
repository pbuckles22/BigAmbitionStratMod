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

    public sealed class CashierGap
    {
        public CashierGap(bool hire, string reason, CashierPick pick)
        {
            Hire = hire;
            Reason = reason ?? "";
            Pick = pick;
        }

        public bool Hire { get; }

        public string Reason { get; }

        public CashierPick Pick { get; }
    }

    public static class CashierCover
    {
        public static string Reason(bool resigned, bool retired, bool poached)
        {
            if (retired)
            {
                return "retired";
            }

            if (resigned)
            {
                return "quit";
            }

            if (poached)
            {
                return "poached";
            }

            return "absent";
        }
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

        public static CashierGap Cover(
            string? place,
            bool registerStaffed,
            bool resigned,
            bool retired,
            bool poached,
            ApiNote? api)
        {
            var reason = CashierCover.Reason(resigned, retired, poached);
            var call = api == null ? "" : api.Call("hire");
            var stationCall = api == null ? "" : api.Call("cashier");
            if (registerStaffed
                || !Split(place, out var street, out var number)
                || call.Length == 0
                || stationCall.Length == 0)
            {
                return new CashierGap(false, "", None());
            }

            return new CashierGap(
                true,
                reason,
                new CashierPick(true, RoleName, Skill, GiftType, street, number, call, stationCall));
        }

        public static string Replaced(string localAppData, CashierPick? pick, int day, string? reason)
        {
            if (pick == null || !pick.Hired)
            {
                return "";
            }

            var why = string.IsNullOrEmpty(reason) ? "absent" : reason;
            var line = "cashier.replaced version=" + LoadLog.Version
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

        public static string Missed(string localAppData, CashierPick? pick, int day, string? reason)
        {
            if (pick == null || pick.Street.Length == 0 || pick.Number <= 0)
            {
                return "";
            }

            var line = "cashier.missed version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + pick.Street
                + " number=" + pick.Number.ToString(CultureInfo.InvariantCulture)
                + " reason=" + (string.IsNullOrEmpty(reason) ? "absent" : reason);
            return AppendOnce(localAppData, line);
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

        private static bool Split(string? place, out string street, out int number)
        {
            street = "";
            number = 0;
            if (string.IsNullOrEmpty(place))
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

        private static CashierPick None()
        {
            return new CashierPick(false, "", "", "", "", 0, "", "");
        }
    }

    public sealed class CashierWatch
    {
        private object? _seen;
        private int _tries;
        private string _place = "";
        private bool _done;
        private int _restaffTries;

        public bool Due(object? save)
        {
            return save != null && !ReferenceEquals(_seen, save);
        }

        public bool Filled(string? place)
        {
            return _done && place != null && place.Length > 0 && _place == place;
        }

        public void Fill(string? place)
        {
            _place = place ?? "";
            _done = _place.Length > 0;
            _restaffTries = 0;
        }

        public void ClearFill()
        {
            _place = "";
            _done = false;
            _restaffTries = 0;
        }

        public bool RestaffAgain(string? place)
        {
            if (string.IsNullOrEmpty(place) || Filled(place))
            {
                return false;
            }

            if (_place != place)
            {
                _place = place;
                _done = false;
                _restaffTries = 0;
            }

            return _restaffTries < 300;
        }

        public void RestaffNote()
        {
            _restaffTries++;
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
