using System.Collections.Generic;

namespace ChaoticCEO
{
    public sealed class SiteCandidate
    {
        public SiteCandidate(
            string street,
            int number,
            int familyOnStreet,
            decimal traffic,
            decimal dailyRent,
            decimal cost,
            string neighborhood = "",
            int businessesInNeighborhood = 0)
        {
            Street = street ?? "";
            Number = number;
            FamilyOnStreet = familyOnStreet;
            Traffic = traffic;
            DailyRent = dailyRent;
            Cost = cost;
            Neighborhood = neighborhood ?? "";
            BusinessesInNeighborhood = businessesInNeighborhood;
        }

        public string Street { get; }

        public int Number { get; }

        public int FamilyOnStreet { get; }

        public decimal Traffic { get; }

        public decimal DailyRent { get; }

        public decimal Cost { get; }

        public string Neighborhood { get; }

        public int BusinessesInNeighborhood { get; }
    }

    public sealed class SitePick
    {
        public SitePick(string street, int number, bool chosen)
        {
            Street = street ?? "";
            Number = number;
            Chosen = chosen;
        }

        public string Street { get; }

        public int Number { get; }

        public bool Chosen { get; }

        public static SitePick None()
        {
            return new SitePick("", 0, false);
        }
    }

    public static class Where
    {
        public const int ClusterMin = 2;

        public const int WholesalerDistrictMax = 3;

        public const int PreGoldDistrictMax = 7;

        public static int Cap(bool importerRunning, bool goldOn)
        {
            if (!importerRunning)
            {
                return WholesalerDistrictMax;
            }

            if (!goldOn)
            {
                return PreGoldDistrictMax;
            }

            return int.MaxValue;
        }

        public static SitePick Decide(
            IReadOnlyList<SiteCandidate>? sites,
            decimal cash,
            decimal reserve,
            int districtCap)
        {
            if (sites == null || sites.Count == 0)
            {
                return SitePick.None();
            }

            var open = new List<SiteCandidate>();
            foreach (var site in sites)
            {
                if (site == null || site.BusinessesInNeighborhood >= districtCap)
                {
                    continue;
                }

                open.Add(site);
            }

            return Decide(open, cash, reserve);
        }

        public static SitePick Decide(
            IReadOnlyList<SiteCandidate>? sites,
            decimal cash,
            decimal reserve)
        {
            if (sites == null || sites.Count == 0)
            {
                return SitePick.None();
            }

            var room = cash - reserve;
            SiteCandidate? winner = null;
            var winnerCluster = false;
            var winnerRatio = 0m;
            foreach (var site in sites)
            {
                if (site == null
                    || site.Street.Length == 0
                    || site.Number <= 0
                    || site.DailyRent <= 0m
                    || site.Cost > room)
                {
                    continue;
                }

                var cluster = site.FamilyOnStreet >= ClusterMin;
                var ratio = site.Traffic / site.DailyRent;
                if (winner == null)
                {
                    winner = site;
                    winnerCluster = cluster;
                    winnerRatio = ratio;
                    continue;
                }

                if (cluster && !winnerCluster)
                {
                    winner = site;
                    winnerCluster = true;
                    winnerRatio = ratio;
                    continue;
                }

                if (!cluster && winnerCluster)
                {
                    continue;
                }

                if (ratio >= winnerRatio)
                {
                    winner = site;
                    winnerRatio = ratio;
                }
            }

            return winner == null
                ? SitePick.None()
                : new SitePick(winner.Street, winner.Number, true);
        }
    }
}
