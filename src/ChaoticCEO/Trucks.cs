using System.Collections.Generic;

namespace ChaoticCEO
{
    public sealed class TruckPick
    {
        public TruckPick(string warehouse, int slot, bool chosen)
        {
            Warehouse = warehouse ?? "";
            Slot = slot;
            Chosen = chosen;
        }

        public string Warehouse { get; }

        public int Slot { get; }

        public bool Chosen { get; }

        public static TruckPick None()
        {
            return new TruckPick("", 0, false);
        }
    }

    public static class Trucks
    {
        public const int GarageSlots = 2;

        public static TruckPick Decide(
            IReadOnlyList<GarageSlot>? slots,
            IReadOnlyList<string>? delivering,
            decimal cash,
            decimal reserve,
            decimal truckCost)
        {
            if (slots == null || delivering == null || truckCost < 0m || cash < reserve + truckCost)
            {
                return TruckPick.None();
            }

            var live = new HashSet<string>();
            foreach (var warehouse in delivering)
            {
                if (!string.IsNullOrEmpty(warehouse))
                {
                    live.Add(warehouse);
                }
            }

            if (live.Count == 0)
            {
                return TruckPick.None();
            }

            foreach (var slot in slots)
            {
                if (slot == null
                    || slot.Filled
                    || slot.Warehouse.Length == 0
                    || slot.Index <= 0
                    || !live.Contains(slot.Warehouse))
                {
                    continue;
                }

                return new TruckPick(slot.Warehouse, slot.Index, true);
            }

            return TruckPick.None();
        }
    }
}
