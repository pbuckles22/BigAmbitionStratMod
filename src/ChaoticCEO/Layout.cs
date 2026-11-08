using System.Collections.Generic;

namespace ChaoticCEO
{
    public sealed class ChainLayout
    {
        public ChainLayout(string street, string family, int number, string blueprint, int score, bool isNew)
        {
            Street = street ?? "";
            Family = family ?? "";
            Number = number;
            Blueprint = blueprint ?? "";
            Score = score;
            IsNew = isNew;
        }

        public string Street { get; }

        public string Family { get; }

        public int Number { get; }

        public string Blueprint { get; }

        public int Score { get; }

        public bool IsNew { get; }
    }

    public sealed class LayoutClone
    {
        public LayoutClone(string street, string family, string blueprint, int source, int destination)
        {
            Street = street ?? "";
            Family = family ?? "";
            Blueprint = blueprint ?? "";
            Source = source;
            Destination = destination;
        }

        public string Street { get; }

        public string Family { get; }

        public string Blueprint { get; }

        public int Source { get; }

        public int Destination { get; }
    }

    public sealed class OwnedLayout
    {
        public OwnedLayout(string type, string size, string blueprint, int score)
        {
            Type = type ?? "";
            Size = size ?? "";
            Blueprint = blueprint ?? "";
            Score = score;
        }

        public string Type { get; }

        public string Size { get; }

        public string Blueprint { get; }

        public int Score { get; }
    }

    public sealed class LayoutPick
    {
        public LayoutPick(string blueprint, bool chosen)
        {
            Blueprint = blueprint ?? "";
            Chosen = chosen;
        }

        public string Blueprint { get; }

        public bool Chosen { get; }

        public static LayoutPick None()
        {
            return new LayoutPick("", false);
        }
    }

    public static class Layout
    {
        private const int ClusterMinimum = 3;

        public static LayoutPick Decide(
            int stage,
            string? type,
            string? size,
            IReadOnlyList<OwnedLayout>? owned,
            IReadOnlyList<StarterOffer>? starters)
        {
            var business = type ?? "";
            var building = size ?? "";
            if (business.Length == 0 || building.Length == 0)
            {
                return LayoutPick.None();
            }

            if (stage >= 2)
            {
                OwnedLayout? winner = null;
                if (owned != null)
                {
                    foreach (var layout in owned)
                    {
                        if (layout == null
                            || layout.Type != business
                            || layout.Size != building
                            || layout.Blueprint.Length == 0
                            || layout.Score <= 0)
                        {
                            continue;
                        }

                        if (winner == null || layout.Score >= winner.Score)
                        {
                            winner = layout;
                        }
                    }
                }

                if (winner != null)
                {
                    return new LayoutPick(winner.Blueprint, true);
                }
            }

            return StarterFor(business, starters);
        }

        public static LayoutClone Lock(
            StaffPlan? staff,
            IReadOnlyList<StreetCluster>? clusters,
            IReadOnlyList<ChainLayout>? layouts)
        {
            var role = staff == null ? "" : staff.Role ?? "";
            if (role.Length == 0 || clusters == null || layouts == null)
            {
                return None();
            }

            LayoutClone? winner = null;
            foreach (var candidate in clusters)
            {
                if (candidate == null
                    || candidate.Count < ClusterMinimum
                    || candidate.Family != role
                    || candidate.Street.Length == 0)
                {
                    continue;
                }

                var clone = Clone(role, candidate, layouts);
                if (clone != null)
                {
                    winner = clone;
                }
            }

            return winner ?? None();
        }

        private static LayoutClone? Clone(string role, StreetCluster cluster, IReadOnlyList<ChainLayout> layouts)
        {
            ChainLayout? source = null;
            ChainLayout? destination = null;
            foreach (var layout in layouts)
            {
                if (layout == null || layout.Street != cluster.Street || layout.Family != role)
                {
                    continue;
                }

                if (layout.IsNew)
                {
                    destination = layout;
                    continue;
                }

                if (layout.Blueprint.Length == 0 || layout.Score <= 0)
                {
                    continue;
                }

                if (source == null || layout.Score >= source.Score)
                {
                    source = layout;
                }
            }

            if (source == null || destination == null)
            {
                return null;
            }

            return new LayoutClone(
                cluster.Street,
                role,
                source.Blueprint,
                source.Number,
                destination.Number);
        }

        private static LayoutPick StarterFor(string type, IReadOnlyList<StarterOffer>? starters)
        {
            if (starters == null)
            {
                return LayoutPick.None();
            }

            foreach (var offer in starters)
            {
                if (offer == null
                    || offer.BusinessType != type
                    || offer.Layout.Length == 0)
                {
                    continue;
                }

                return new LayoutPick(offer.Layout, true);
            }

            return LayoutPick.None();
        }

        private static LayoutClone None()
        {
            return new LayoutClone("", "", "", 0, 0);
        }
    }
}
