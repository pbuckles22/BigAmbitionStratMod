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

    public static class Layout
    {
        private const int ClusterMinimum = 3;

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

        private static LayoutClone None()
        {
            return new LayoutClone("", "", "", 0, 0);
        }
    }
}
