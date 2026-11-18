using System.Collections.Generic;
using System.Globalization;

namespace ChaoticCEO
{
    public sealed class GarageCargo
    {
        public GarageCargo(int carried, int capacity, bool filled)
        {
            Carried = carried;
            Capacity = capacity;
            Filled = filled;
        }

        public int Carried { get; }

        public int Capacity { get; }

        public bool Filled { get; }
    }

    public static class TruckLoad
    {
        public static decimal Fraction(IReadOnlyList<GarageCargo>? slots)
        {
            return Measure(slots).Fraction;
        }

        public static string Line(int day, IReadOnlyList<GarageCargo>? slots)
        {
            var measure = Measure(slots);
            return "truck.load version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " carried=" + measure.Carried.ToString(CultureInfo.InvariantCulture)
                + " capacity=" + measure.Capacity.ToString(CultureInfo.InvariantCulture)
                + " fraction=" + Format(measure.Fraction);
        }

        private static LoadMeasure Measure(IReadOnlyList<GarageCargo>? slots)
        {
            long carried = 0;
            long capacity = 0;
            if (slots != null)
            {
                foreach (var slot in slots)
                {
                    if (slot == null || !slot.Filled || slot.Capacity <= 0)
                    {
                        continue;
                    }

                    capacity += slot.Capacity;
                    if (slot.Carried > 0)
                    {
                        carried += slot.Carried;
                    }
                }
            }

            var fraction = capacity <= 0 ? 0m : (decimal)carried / capacity;
            return new LoadMeasure(carried, capacity, fraction);
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

        private readonly struct LoadMeasure
        {
            public LoadMeasure(long carried, long capacity, decimal fraction)
            {
                Carried = carried;
                Capacity = capacity;
                Fraction = fraction;
            }

            public long Carried { get; }

            public long Capacity { get; }

            public decimal Fraction { get; }
        }
    }
}
