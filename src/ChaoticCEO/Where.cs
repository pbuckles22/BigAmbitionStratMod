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
            decimal cost)
        {
            Street = street ?? "";
            Number = number;
            FamilyOnStreet = familyOnStreet;
            Traffic = traffic;
            DailyRent = dailyRent;
            Cost = cost;
        }

        public string Street { get; }

        public int Number { get; }

        public int FamilyOnStreet { get; }

        public decimal Traffic { get; }

        public decimal DailyRent { get; }

        public decimal Cost { get; }
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
