using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class StoreShutdown
    {
        public StoreShutdown(string street, int number, bool closed, string call)
        {
            Street = street ?? "";
            Number = number;
            Closed = closed;
            Call = call ?? "";
        }

        public string Street { get; }

        public int Number { get; }

        public bool Closed { get; }

        public string Call { get; }
    }

    public static class Shutdown
    {
        public static StoreShutdown Confirm(
            ApiNote? api,
            IReadOnlyList<RoadStep>? steps,
            string? street,
            int number,
            bool confirmed)
        {
            if (!confirmed
                || api == null
                || api.Close.Length == 0
                || steps == null
                || string.IsNullOrEmpty(street)
                || number <= 0)
            {
                return None();
            }

            foreach (var step in steps)
            {
                if (step == null || step.Kind != "close" || step.Street != street || step.Number != number)
                {
                    continue;
                }

                return new StoreShutdown(step.Street, step.Number, true, api.Close);
            }

            return None();
        }

        public static string Record(string localAppData, StoreShutdown? shut)
        {
            if (shut == null || !shut.Closed)
            {
                return "";
            }

            var line = "store.closed version=" + LoadLog.Version
                + " street=" + shut.Street
                + " number=" + shut.Number.ToString(CultureInfo.InvariantCulture)
                + " call=" + shut.Call;
            var path = LoadLog.CeoLogPath(localAppData);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + System.Environment.NewLine);
            return line;
        }

        public static StoreShutdown Reload(StoreShutdown? shut)
        {
            return None();
        }

        private static StoreShutdown None()
        {
            return new StoreShutdown("", 0, false, "");
        }
    }
}
