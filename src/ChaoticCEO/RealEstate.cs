using System.Collections.Generic;

namespace ChaoticCEO
{
    public sealed class EstateCandidate
    {
        public EstateCandidate(string street, int number, string kind, decimal price, decimal dailyRent)
        {
            Street = street ?? "";
            Number = number;
            Kind = kind ?? "";
            Price = price;
            DailyRent = dailyRent;
        }

        public string Street { get; }

        public int Number { get; }

        public string Kind { get; }

        public decimal Price { get; }

        public decimal DailyRent { get; }
    }

    public sealed class EstatePick
    {
        public EstatePick(string street, int number, string kind, decimal days, bool chosen)
        {
            Street = street ?? "";
            Number = number;
            Kind = kind ?? "";
            Days = days;
            Chosen = chosen;
        }

        public string Street { get; }

        public int Number { get; }

        public string Kind { get; }

        public decimal Days { get; }

        public bool Chosen { get; }

        public static EstatePick None()
        {
            return new EstatePick("", 0, "", 0m, false);
        }
    }

    public static class RealEstate
    {
        public const int PaybackMax = 365;

        public static EstatePick Decide(int stage, IReadOnlyList<EstateCandidate>? buildings)
        {
            if (stage < 4 || buildings == null)
            {
                return EstatePick.None();
            }

            EstateCandidate? winner = null;
            var winnerDays = 0m;
            foreach (var building in buildings)
            {
                if (building == null
                    || building.Street.Length == 0
                    || building.Kind.Length == 0
                    || building.DailyRent <= 0m
                    || building.Price < 0m)
                {
                    continue;
                }

                var days = building.Price / building.DailyRent;
                if (days > PaybackMax)
                {
                    continue;
                }

                if (winner == null
                    || KindRank(building.Kind) < KindRank(winner.Kind)
                    || (KindRank(building.Kind) == KindRank(winner.Kind) && days <= winnerDays))
                {
                    winner = building;
                    winnerDays = days;
                }
            }

            if (winner == null || KindRank(winner.Kind) > 3)
            {
                return EstatePick.None();
            }

            return new EstatePick(winner.Street, winner.Number, winner.Kind, winnerDays, true);
        }

        private static int KindRank(string kind)
        {
            if (kind == "headquarters")
            {
                return 1;
            }

            if (kind == "warehouse")
            {
                return 2;
            }

            if (kind == "retail")
            {
                return 3;
            }

            return 99;
        }
    }
}
