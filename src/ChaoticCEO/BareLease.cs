using System.Collections.Generic;

namespace ChaoticCEO
{
    public sealed class BareLeaseFact
    {
        public BareLeaseFact(bool rented, string? businessType, string? street, int number)
        {
            Rented = rented;
            BusinessType = businessType ?? "";
            Street = street ?? "";
            Number = number;
        }

        public bool Rented { get; }

        public string BusinessType { get; }

        public string Street { get; }

        public int Number { get; }
    }

    public static class BareLease
    {
        public const string EmptyType = "ba:businesstype_empty";

        public static bool IsEmptyType(string? businessType)
        {
            var type = businessType ?? "";
            return type.Length == 0 || type == EmptyType;
        }

        public static bool IsBare(BareLeaseFact? fact)
        {
            return fact != null
                && fact.Rented
                && fact.Street.Length > 0
                && fact.Number > 0
                && IsEmptyType(fact.BusinessType);
        }

        public static int Count(IReadOnlyList<BareLeaseFact>? facts)
        {
            if (facts == null)
            {
                return 0;
            }

            var total = 0;
            foreach (var fact in facts)
            {
                if (IsBare(fact))
                {
                    total++;
                }
            }

            return total;
        }
    }
}
