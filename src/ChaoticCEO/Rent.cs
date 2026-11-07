using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class RentLot
    {
        public RentLot(string street, int number, decimal dailyRent, bool available = true)
        {
            Street = street ?? "";
            Number = number;
            DailyRent = dailyRent;
            Available = available;
        }

        public string Street { get; }

        public int Number { get; }

        public decimal DailyRent { get; }

        public bool Available { get; }
    }

    public sealed class BuildingRent
    {
        public BuildingRent(string street, int number, bool rented, string call)
        {
            Street = street ?? "";
            Number = number;
            Rented = rented;
            Call = call ?? "";
        }

        public string Street { get; }

        public int Number { get; }

        public bool Rented { get; }

        public string Call { get; }
    }

    public static class Rent
    {
        public static BuildingRent Choose(
            IReadOnlyList<RentLot>? lots,
            decimal cash,
            bool anyRented,
            ApiNote? api)
        {
            var call = api == null ? "" : api.Call("rent");
            if (anyRented || lots == null || call.Length == 0)
            {
                return None();
            }

            RentLot? winner = null;
            foreach (var lot in lots)
            {
                if (lot == null
                    || !lot.Available
                    || lot.Street.Length == 0
                    || lot.Number <= 0
                    || lot.DailyRent < 0m
                    || lot.DailyRent > cash)
                {
                    continue;
                }

                if (winner == null || lot.DailyRent <= winner.DailyRent)
                {
                    winner = lot;
                }
            }

            if (winner == null)
            {
                return None();
            }

            return new BuildingRent(winner.Street, winner.Number, true, call);
        }

        public static string Record(string localAppData, BuildingRent? rent, int day)
        {
            if (rent == null || !rent.Rented)
            {
                return "";
            }

            var line = "building.rented version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + rent.Street
                + " number=" + rent.Number.ToString(CultureInfo.InvariantCulture)
                + " call=" + rent.Call;
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

        public static string Miss(string localAppData, BuildingRent rent, int day)
        {
            var line = "building.missed version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + rent.Street
                + " number=" + rent.Number.ToString(CultureInfo.InvariantCulture)
                + " call=" + rent.Call;
            var path = LoadLog.CeoLogPath(localAppData ?? "");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + System.Environment.NewLine);
            return line;
        }

        private static BuildingRent None()
        {
            return new BuildingRent("", 0, false, "");
        }
    }

    public sealed class RentWatch
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
