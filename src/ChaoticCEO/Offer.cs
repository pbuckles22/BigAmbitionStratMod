using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class BuildingOffer
    {
        public BuildingOffer(string street, int number, bool offered, string call)
        {
            Street = street ?? "";
            Number = number;
            Offered = offered;
            Call = call ?? "";
        }

        public string Street { get; }

        public int Number { get; }

        public bool Offered { get; }

        public string Call { get; }
    }

    public static class Offer
    {
        public static BuildingOffer Confirm(
            ApiNote? api,
            IReadOnlyList<RoadStep>? steps,
            string? street,
            int number,
            bool confirmed)
        {
            if (!confirmed
                || api == null
                || api.Buy.Length == 0
                || steps == null
                || string.IsNullOrEmpty(street)
                || number <= 0)
            {
                return None();
            }

            foreach (var step in steps)
            {
                if (step == null || step.Kind != "open" || step.Street != street || step.Number != number)
                {
                    continue;
                }

                return new BuildingOffer(step.Street, step.Number, true, api.Buy);
            }

            return None();
        }

        public static string Record(string localAppData, BuildingOffer? offer)
        {
            if (offer == null || !offer.Offered)
            {
                return "";
            }

            var line = "building.offered version=" + LoadLog.Version
                + " street=" + offer.Street
                + " number=" + offer.Number.ToString(CultureInfo.InvariantCulture)
                + " call=" + offer.Call;
            var path = LoadLog.CeoLogPath(localAppData);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + System.Environment.NewLine);
            return line;
        }

        public static BuildingOffer Reload(BuildingOffer? offer)
        {
            return None();
        }

        private static BuildingOffer None()
        {
            return new BuildingOffer("", 0, false, "");
        }
    }
}
