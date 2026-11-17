using System;
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
            IReadOnlyList<StarterOffer>? starters,
            bool factoryPipeline = false)
        {
            var business = type ?? "";
            var building = size ?? "";
            if (business.Length == 0 || building.Length == 0 || !SkuScope.IsBusinessAllowed(business, factoryPipeline))
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

            return StarterFor(business, building, starters);
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

        private static LayoutPick StarterFor(string type, string size, IReadOnlyList<StarterOffer>? starters)
        {
            if (IsSizeCode(size))
            {
                if (starters != null)
                {
                    foreach (var offer in starters)
                    {
                        if (offer == null
                            || offer.BusinessType != type
                            || offer.Layout.Length == 0
                            || !Fits(offer.Layout, size))
                        {
                            continue;
                        }

                        return new LayoutPick(offer.Layout, true);
                    }
                }

                var builtin = Builtin(type, size);
                if (builtin.Length > 0)
                {
                    return new LayoutPick(builtin, true);
                }

                return LayoutPick.None();
            }

            var mode = Modes.FitOut(type);
            if (mode.Length > 0)
            {
                return new LayoutPick(mode, true);
            }

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

        private static bool IsSizeCode(string size)
        {
            var letter = false;
            var digit = false;
            foreach (var ch in size)
            {
                if (ch >= 'A' && ch <= 'Z')
                {
                    letter = true;
                    continue;
                }

                if (ch >= '0' && ch <= '9')
                {
                    digit = true;
                    continue;
                }

                return false;
            }

            return letter && digit;
        }

        private static string Builtin(string type, string size)
        {
            foreach (var name in Names(type))
            {
                if (Fits(name, size))
                {
                    return name;
                }
            }

            return "";
        }

        private static IEnumerable<string> Names(string type)
        {
            switch (type)
            {
                case "ba:businesstype_giftshop":
                    return new[] { "GiftShopWholesalers", "GiftShopImporters" };
                case "ba:businesstype_florist":
                    return new[] { "FloristWholesalers", "FloristImporters" };
                case "ba:businesstype_fastfoodrestaurant":
                    return new[] { "FastFoodKabob", "FastFoodHotdog", "FastFoodPizza" };
                case "ba:businesstype_hairdresser":
                    return new[] { "HairdresserWholesalers", "HairdresserImporters" };
                case "ba:businesstype_gym":
                    return new[] { "GymWholesalers", "GymImporters" };
                case "ba:businesstype_lawfirm":
                    return new[] { "LawFirm" };
                case "ba:businesstype_webdevelopmentagency":
                    return new[] { "WebDevelopmentAgency" };
                case "ba:businesstype_graphicsdesignagency":
                    return new[] { "GraphicDesigner" };
                case "ba:businesstype_travelagency":
                    return new[] { "TravelAgency" };
                default:
                    return Array.Empty<string>();
            }
        }

        private static bool Fits(string blueprint, string size)
        {
            switch (blueprint)
            {
                case "GiftShopWholesalers":
                case "FloristWholesalers":
                case "FastFoodKabob":
                case "FastFoodHotdog":
                case "HairdresserWholesalers":
                case "GymWholesalers":
                    return size == "A1" || size == "A2" || size == "C1";
                case "GiftShopImporters":
                case "FloristImporters":
                case "HairdresserImporters":
                case "GymImporters":
                    return size == "C1" || size == "C2" || size == "D2";
                case "FastFoodPizza":
                    return size == "C1" || size == "C2" || size == "D2";
                case "LawFirm":
                case "WebDevelopmentAgency":
                case "GraphicDesigner":
                case "TravelAgency":
                    return size == "A3" || size == "C1" || size == "D2" || size == "J1" || size == "K1";
                default:
                    return false;
            }
        }

        private static LayoutClone None()
        {
            return new LayoutClone("", "", "", 0, 0);
        }
    }
}
