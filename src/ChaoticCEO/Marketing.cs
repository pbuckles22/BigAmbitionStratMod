using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class MarketingStartPick
    {
        public MarketingStartPick(
            bool started,
            string street,
            int number,
            string type,
            decimal dailyCost,
            string call)
        {
            Started = started;
            Street = street ?? "";
            Number = number;
            Type = type ?? "";
            DailyCost = dailyCost;
            Call = call ?? "";
        }

        public bool Started { get; }

        public string Street { get; }

        public int Number { get; }

        public string Type { get; }

        public decimal DailyCost { get; }

        public string Call { get; }
    }

    public sealed class MarketingStopPick
    {
        public MarketingStopPick(bool stopped, string street, int number, string call)
        {
            Stopped = stopped;
            Street = street ?? "";
            Number = number;
            Call = call ?? "";
        }

        public bool Stopped { get; }

        public string Street { get; }

        public int Number { get; }

        public string Call { get; }
    }

    public static class Marketing
    {
        public const decimal MarketingCover = 0.5m;

        public const string StarterType = "SmallInternet";

        public const decimal StarterCost = 100m;

        public const decimal StarterGain = 250m;

        public static MarketingStartPick Start(
            decimal dailyCost,
            decimal gain,
            bool alreadyActive,
            string? street,
            int number,
            string? type,
            ApiNote? api)
        {
            var call = api == null ? "" : api.Call("marketing start");
            var place = street ?? "";
            var kind = type ?? "";
            if (alreadyActive
                || place.Length == 0
                || number <= 0
                || kind.Length == 0
                || dailyCost <= 0m
                || gain <= 0m
                || call.Length == 0
                || dailyCost >= gain * MarketingCover)
            {
                return NoneStart();
            }

            return new MarketingStartPick(true, place, number, kind, dailyCost, call);
        }

        public static MarketingStopPick Stop(
            bool alreadyActive,
            string? street,
            int number,
            ApiNote? api)
        {
            var call = api == null ? "" : api.Call("marketing stop");
            var place = street ?? "";
            if (!alreadyActive || place.Length == 0 || number <= 0 || call.Length == 0)
            {
                return NoneStop();
            }

            return new MarketingStopPick(true, place, number, call);
        }

        public static string RecordStart(string localAppData, MarketingStartPick? pick, int day)
        {
            if (pick == null || !pick.Started)
            {
                return "";
            }

            var line = "marketing.started version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + pick.Street
                + " number=" + pick.Number.ToString(CultureInfo.InvariantCulture)
                + " type=" + pick.Type
                + " dailyCost=" + pick.DailyCost.ToString(CultureInfo.InvariantCulture)
                + " call=" + pick.Call;
            return AppendOnce(localAppData, line);
        }

        public static string RecordStop(string localAppData, MarketingStopPick? pick, int day)
        {
            if (pick == null || !pick.Stopped)
            {
                return "";
            }

            var line = "marketing.stopped version=" + LoadLog.Version
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

        private static MarketingStartPick NoneStart()
        {
            return new MarketingStartPick(false, "", 0, "", 0m, "");
        }

        private static MarketingStopPick NoneStop()
        {
            return new MarketingStopPick(false, "", 0, "");
        }
    }

    public sealed class MarketingWatch
    {
        private object? _started;
        private object? _stopped;

        public bool StartDue(object? save)
        {
            return save != null && !ReferenceEquals(_started, save);
        }

        public bool StopDue(object? save, bool ready)
        {
            return ready
                && save != null
                && ReferenceEquals(_started, save)
                && !ReferenceEquals(_stopped, save);
        }

        public void MarkStarted(object? save)
        {
            _started = save;
        }

        public void MarkStopped(object? save)
        {
            _stopped = save;
        }

        public bool Active(object? save)
        {
            return save != null
                && ReferenceEquals(_started, save)
                && !ReferenceEquals(_stopped, save);
        }
    }
}
