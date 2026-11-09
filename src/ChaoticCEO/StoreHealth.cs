using System.Collections.Generic;

namespace ChaoticCEO
{
    public sealed class StoreHealth
    {
        public StoreHealth(
            bool open,
            bool stocked,
            bool staffed,
            bool selling,
            bool profitable,
            string place = "")
        {
            Open = open;
            Stocked = stocked;
            Staffed = staffed;
            Selling = selling;
            Profitable = profitable;
            Place = place ?? "";
        }

        public bool Open { get; }

        public bool Stocked { get; }

        public bool Staffed { get; }

        public bool Selling { get; }

        public bool Profitable { get; }

        public string Place { get; }

        public bool Ready => Open && Stocked && Staffed && Selling && Profitable;

        public static StoreHealth Healthy(string place = "")
        {
            return new StoreHealth(true, true, true, true, true, place);
        }
    }

    public static class StoreHealthGate
    {
        public const int ProfitProofDays = 3;

        public static bool ExpansionAllowed(IReadOnlyList<StoreHealth>? owned)
        {
            if (owned == null || owned.Count == 0)
            {
                return true;
            }

            foreach (var store in owned)
            {
                if (store == null || !store.Ready)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
