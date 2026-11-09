using System;
using System.Collections.Generic;

namespace ChaoticCEO
{
    public sealed class OpenCandidate
    {
        public OpenCandidate(
            string type,
            string kind,
            decimal dailyProfit,
            bool wholesalerSells,
            string family,
            int ownedOfFamily,
            bool familyHasWarehouse)
        {
            Type = type ?? "";
            Kind = kind ?? "";
            DailyProfit = dailyProfit;
            WholesalerSells = wholesalerSells;
            Family = family ?? "";
            OwnedOfFamily = ownedOfFamily;
            FamilyHasWarehouse = familyHasWarehouse;
        }

        public string Type { get; }

        public string Kind { get; }

        public decimal DailyProfit { get; }

        public bool WholesalerSells { get; }

        public string Family { get; }

        public int OwnedOfFamily { get; }

        public bool FamilyHasWarehouse { get; }
    }

    public sealed class OpenPick
    {
        public OpenPick(string type, bool chosen)
        {
            Type = type ?? "";
            Chosen = chosen;
        }

        public string Type { get; }

        public bool Chosen { get; }

        public static OpenPick None()
        {
            return new OpenPick("", false);
        }
    }

    public static class Open
    {
        public const decimal TruckBusy = 0.85m;

        public static bool StageOneType(string? type)
        {
            return type == "ba:businesstype_giftshop"
                || type == "ba:businesstype_florist"
                || type == "ba:businesstype_fastfoodrestaurant";
        }

        public static OpenPick Decide(
            int stage,
            IReadOnlyList<OpenCandidate>? candidates,
            decimal truckLoadFraction,
            IReadOnlyList<StoreHealth>? ownedHealth = null)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return OpenPick.None();
            }

            if (!StoreHealthGate.ExpansionAllowed(ownedHealth))
            {
                return OpenPick.None();
            }

            var busy = truckLoadFraction > TruckBusy;
            OpenCandidate? winner = null;
            foreach (var candidate in candidates)
            {
                if (!Eligible(stage, candidate, busy))
                {
                    continue;
                }

                if (winner == null || candidate.DailyProfit >= winner.DailyProfit)
                {
                    winner = candidate;
                }
            }

            return winner == null ? OpenPick.None() : new OpenPick(winner.Type, true);
        }

        public static IReadOnlyList<OpenCandidate> StageOneCandidates(
            IReadOnlyList<StarterOffer>? offers,
            IReadOnlyDictionary<string, int>? ownedByFamily,
            string? pendingOpen)
        {
            if (!string.IsNullOrEmpty(pendingOpen))
            {
                return Array.Empty<OpenCandidate>();
            }

            if (offers == null || offers.Count == 0)
            {
                return Array.Empty<OpenCandidate>();
            }

            var rows = new List<OpenCandidate>();
            foreach (var offer in offers)
            {
                if (offer == null || offer.BusinessType.Length == 0)
                {
                    continue;
                }

                var family = StoreFamily.For(offer.BusinessType) ?? "";
                var owned = 0;
                if (ownedByFamily != null && family.Length > 0)
                {
                    ownedByFamily.TryGetValue(family, out owned);
                }

                rows.Add(
                    new OpenCandidate(
                        offer.BusinessType,
                        "goods",
                        offer.DailyProfit,
                        wholesalerSells: true,
                        family,
                        owned,
                        familyHasWarehouse: false));
            }

            return rows;
        }

        private static bool Eligible(int stage, OpenCandidate? candidate, bool busy)
        {
            if (candidate == null
                || candidate.Type.Length == 0
                || candidate.Kind.Length == 0
                || candidate.DailyProfit < 0m)
            {
                return false;
            }

            if (busy)
            {
                return candidate.Kind == "service" || candidate.Kind == "office";
            }

            if (stage <= 1)
            {
                return StageOneType(candidate.Type) && candidate.WholesalerSells;
            }

            if (stage >= 3
                && candidate.Kind == "goods"
                && candidate.OwnedOfFamily >= 1
                && !candidate.FamilyHasWarehouse)
            {
                return false;
            }

            return true;
        }
    }
}
