using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class DemandPick
    {
        public DemandPick(bool cleared, string demand, string employeeId, string call)
        {
            Cleared = cleared;
            Demand = demand ?? "";
            EmployeeId = employeeId ?? "";
            Call = call ?? "";
        }

        public bool Cleared { get; }

        public string Demand { get; }

        public string EmployeeId { get; }

        public string Call { get; }
    }

    public static class StaffDemand
    {
        public const int DemandDays = 3;

        public static DemandPick Clear(int unmetDays, string? demandName, string? employeeId, ApiNote? api)
        {
            var call = api == null ? "" : api.Call("employee demand");
            var demand = demandName ?? "";
            var employee = employeeId ?? "";
            if (unmetDays < DemandDays || demand.Length == 0 || employee.Length == 0 || call.Length == 0)
            {
                return new DemandPick(false, "", "", "");
            }

            return new DemandPick(true, demand, employee, call);
        }

        public static string Record(string localAppData, DemandPick? pick, int day)
        {
            if (pick == null || !pick.Cleared)
            {
                return "";
            }

            var line = "demand.cleared version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " employee=" + pick.EmployeeId
                + " demand=" + pick.Demand
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
    }

    public sealed class StaffDemandWatch
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
