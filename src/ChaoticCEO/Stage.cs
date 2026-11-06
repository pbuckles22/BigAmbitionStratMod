using System.Collections.Generic;

namespace ChaoticCEO
{
    public static class Stage
    {
        public const decimal Stage2Cash = 150000m;

        public const int Stage2Stores = 3;

        public const int Stage3Supplied = 5;

        public const decimal Stage4Cash = 2000000m;

        public static int Decide(
            int day,
            decimal cash,
            int goodsStores,
            int suppliedStores,
            int warehouses,
            int emptySlots,
            int plans)
        {
            _ = day;
            _ = warehouses;
            if (cash >= Stage4Cash && suppliedStores == goodsStores && emptySlots == 0)
            {
                return 4;
            }

            if (plans > 0 && suppliedStores >= Stage3Supplied)
            {
                return 3;
            }

            if (cash >= Stage2Cash && goodsStores >= Stage2Stores)
            {
                return 2;
            }

            return 1;
        }

        public static int Read(
            int day,
            decimal cash,
            IReadOnlyList<GoodsNeed>? needs,
            IReadOnlyList<DeliveryStop>? stops,
            IReadOnlyList<WarehouseHold>? holds,
            IReadOnlyList<GarageSlot>? slots)
        {
            var goods = new HashSet<string>();
            if (needs != null)
            {
                foreach (var need in needs)
                {
                    if (need != null && need.Store.Length > 0)
                    {
                        goods.Add(need.Store);
                    }
                }
            }

            var linked = new HashSet<string>();
            var homes = new HashSet<string>();
            if (stops != null)
            {
                foreach (var stop in stops)
                {
                    if (stop == null || stop.Destination.Length == 0)
                    {
                        continue;
                    }

                    if (stop.Warehouse.Length > 0)
                    {
                        homes.Add(stop.Warehouse);
                    }

                    linked.Add(stop.Destination);
                }
            }

            var supplied = 0;
            foreach (var store in goods)
            {
                if (linked.Contains(store))
                {
                    supplied++;
                }
            }

            var warehouses = new HashSet<string>();
            if (holds != null)
            {
                foreach (var hold in holds)
                {
                    if (hold != null && hold.Warehouse.Length > 0)
                    {
                        warehouses.Add(hold.Warehouse);
                    }
                }
            }

            return Decide(day, cash, goods.Count, supplied, warehouses.Count, Audit.EmptySlots(slots), homes.Count);
        }
    }
}
