using System.Collections.Generic;
using System.Globalization;

namespace ChaoticCEO
{
    public sealed class BuildingReading
    {
        public BuildingReading(
            string street,
            int number,
            int dirtSpots,
            decimal theft,
            decimal sales,
            int demandSets,
            int fulfilled)
        {
            Street = street ?? "";
            Number = number;
            DirtSpots = dirtSpots;
            Theft = theft;
            Sales = sales;
            DemandSets = demandSets;
            Fulfilled = fulfilled;
        }

        public string Street { get; }

        public int Number { get; }

        public int DirtSpots { get; }

        public decimal Theft { get; }

        public decimal Sales { get; }

        public int DemandSets { get; }

        public int Fulfilled { get; }
    }

    public sealed class BuildingSignal
    {
        public BuildingSignal(
            string street,
            int number,
            bool hasDirt,
            decimal theft,
            decimal sales,
            bool unmet)
        {
            Street = street ?? "";
            Number = number;
            HasDirt = hasDirt;
            Theft = theft;
            Sales = sales;
            Unmet = unmet;
        }

        public string Street { get; }

        public int Number { get; }

        public bool HasDirt { get; }

        public decimal Theft { get; }

        public decimal Sales { get; }

        public bool Unmet { get; }

        public static BuildingSignal None()
        {
            return new BuildingSignal("", 0, false, 0m, 0m, false);
        }
    }

    public static class BuildingSignalRead
    {
        public static BuildingSignal Read(IReadOnlyList<BuildingReading>? buildings)
        {
            if (buildings == null || buildings.Count == 0)
            {
                return BuildingSignal.None();
            }

            BuildingReading? dirt = null;
            BuildingReading? theft = null;
            BuildingReading? unmet = null;
            foreach (var building in buildings)
            {
                if (building == null)
                {
                    continue;
                }

                if (building.DirtSpots > 0)
                {
                    dirt = building;
                }

                if (building.Sales > 0m && NonNegative(building.Theft) > NonNegative(building.Sales) * Run.TheftMax)
                {
                    theft = building;
                }

                if (building.DemandSets > 0 && building.Fulfilled < building.DemandSets)
                {
                    unmet = building;
                }
            }

            var chosen = dirt ?? theft ?? unmet;
            if (chosen == null)
            {
                return BuildingSignal.None();
            }

            return new BuildingSignal(
                chosen.Street,
                chosen.Number,
                dirt != null,
                NonNegative(chosen.Theft),
                NonNegative(chosen.Sales),
                dirt == null && theft == null && unmet != null);
        }

        public static RunFacts Facts(IReadOnlyList<BuildingReading>? buildings)
        {
            var signal = Read(buildings);
            return new RunFacts(
                100f,
                100f,
                100f,
                0,
                "ok",
                signal.HasDirt,
                signal.Theft,
                signal.Sales,
                signal.Unmet,
                0m,
                0m,
                0m,
                0m,
                0m);
        }

        public static string Line(int day, BuildingSignal? signal)
        {
            var pulse = signal ?? BuildingSignal.None();
            return "building.pulse version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + pulse.Street
                + " number=" + pulse.Number.ToString(CultureInfo.InvariantCulture)
                + " dirt=" + (pulse.HasDirt ? "1" : "0")
                + " theft=" + Format(pulse.Theft)
                + " sales=" + Format(pulse.Sales)
                + " unmet=" + (pulse.Unmet ? "1" : "0");
        }

        private static decimal NonNegative(decimal value)
        {
            return value > 0m ? value : 0m;
        }

        private static string Format(decimal value)
        {
            var rounded = decimal.Round(value, 4, System.MidpointRounding.ToEven);
            if (rounded == decimal.Truncate(rounded))
            {
                return ((long)rounded).ToString(CultureInfo.InvariantCulture);
            }

            return rounded.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}
