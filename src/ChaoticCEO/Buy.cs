using System.Collections.Generic;

namespace ChaoticCEO
{
    public sealed class BuildingLot
    {
        public BuildingLot(string place, string street, int number)
        {
            Place = place ?? "";
            Street = street ?? "";
            Number = number;
        }

        public string Place { get; }

        public string Street { get; }

        public int Number { get; }
    }

    public sealed class BuildingBuy
    {
        public BuildingBuy(string place, string street, int number, bool bought)
        {
            Place = place ?? "";
            Street = street ?? "";
            Number = number;
            Bought = bought;
        }

        public string Place { get; }

        public string Street { get; }

        public int Number { get; }

        public bool Bought { get; }
    }

    public static class Buy
    {
        public static BuildingBuy Lock(StoreClose? closed, IReadOnlyList<BuildingLot>? lots)
        {
            if (closed == null || !closed.Closed || closed.Street.Length == 0 || lots == null)
            {
                return None();
            }

            BuildingLot? winner = null;
            var bestRank = int.MaxValue;
            foreach (var lot in lots)
            {
                if (!TryRank(closed, lot, out var rank))
                {
                    continue;
                }

                if (winner == null || rank <= bestRank)
                {
                    winner = lot;
                    bestRank = rank;
                }
            }

            if (winner == null)
            {
                return None();
            }

            return new BuildingBuy(winner.Place, winner.Street, winner.Number, true);
        }

        private static bool TryRank(StoreClose closed, BuildingLot? lot, out int rank)
        {
            rank = int.MaxValue;
            if (lot == null || lot.Street.Length == 0 || lot.Number <= 0)
            {
                return false;
            }

            if (lot.Place == "hq")
            {
                rank = 0;
                return true;
            }

            if (lot.Place == "food")
            {
                rank = 1;
                return true;
            }

            if (lot.Place == "street" && lot.Street == closed.Street)
            {
                rank = 2;
                return true;
            }

            return false;
        }

        private static BuildingBuy None()
        {
            return new BuildingBuy("", "", 0, false);
        }
    }
}
