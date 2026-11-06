using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public enum HealFate
    {
        Done,
        Failed,
        Skip,
    }

    public static class Autopilot
    {
        public static bool Enabled { get; set; } = true;

        public static bool DryRun { get; set; }
    }

    public sealed class AuditCounts
    {
        public AuditCounts(
            int unconnected,
            int emptySlots,
            int dry,
            int mixed,
            int unstaffed,
            int missingPlans,
            int cashBelowReserve,
            int pinMismatch)
        {
            Unconnected = unconnected;
            EmptySlots = emptySlots;
            Dry = dry;
            Mixed = mixed;
            Unstaffed = unstaffed;
            MissingPlans = missingPlans;
            CashBelowReserve = cashBelowReserve;
            PinMismatch = pinMismatch;
        }

        public int Unconnected { get; }

        public int EmptySlots { get; }

        public int Dry { get; }

        public int Mixed { get; }

        public int Unstaffed { get; }

        public int MissingPlans { get; }

        public int CashBelowReserve { get; }

        public int PinMismatch { get; }
    }

    public sealed class AuditGuard
    {
        public AuditGuard(bool enabled, bool dryRun, decimal cash, decimal dailyCost, int truckCost, string build)
        {
            Enabled = enabled;
            DryRun = dryRun;
            Cash = cash;
            DailyCost = dailyCost < 0 ? 0 : dailyCost;
            TruckCost = truckCost < 0 ? 0 : truckCost;
            Build = build ?? "";
        }

        public bool Enabled { get; }

        public bool DryRun { get; }

        public decimal Cash { get; }

        public decimal DailyCost { get; }

        public int TruckCost { get; }

        public string Build { get; }
    }

    public sealed class AuditHeal
    {
        public AuditHeal(
            string kind,
            string call,
            int cost = 0,
            string store = "",
            string warehouse = "",
            string item = "",
            int slot = 0,
            string order = "",
            string assign = "",
            string driver = "",
            string family = "",
            string from = "",
            string to = "",
            int amount = 0,
            string drop = "",
            string role = "")
        {
            Kind = kind ?? "";
            Call = call ?? "";
            Cost = cost;
            Store = store ?? "";
            Warehouse = warehouse ?? "";
            Item = item ?? "";
            Slot = slot;
            Order = order ?? "";
            Assign = assign ?? "";
            Driver = driver ?? "";
            Family = family ?? "";
            From = from ?? "";
            To = to ?? "";
            Amount = amount;
            Drop = drop ?? "";
            Role = role ?? "";
        }

        public string Kind { get; }

        public string Call { get; }

        public int Cost { get; }

        public string Store { get; }

        public string Warehouse { get; }

        public string Item { get; }

        public int Slot { get; }

        public string Order { get; }

        public string Assign { get; }

        public string Driver { get; }

        public string Family { get; }

        public string From { get; }

        public string To { get; }

        public int Amount { get; }

        public string Drop { get; }

        public string Role { get; }
    }

    public sealed class AuditWorld
    {
        public AuditWorld(
            IReadOnlyList<DeliveryStop>? stops,
            IReadOnlyList<WarehouseHold>? holds,
            IReadOnlyList<GoodsNeed>? needs,
            IReadOnlyList<DailyUnits>? sales,
            IReadOnlyList<GarageSlot>? slots,
            IReadOnlyList<string>? drivers,
            IReadOnlyList<WarehouseLevel>? levels,
            bool pricingPlan,
            IReadOnlyList<HqSeat>? seats,
            IReadOnlyList<HiredHand>? hired,
            IReadOnlyList<string>? stores,
            IReadOnlyList<string>? staffed)
        {
            Stops = stops ?? Array.Empty<DeliveryStop>();
            Holds = holds ?? Array.Empty<WarehouseHold>();
            Needs = needs ?? Array.Empty<GoodsNeed>();
            Sales = sales ?? Array.Empty<DailyUnits>();
            Slots = slots ?? Array.Empty<GarageSlot>();
            Drivers = drivers ?? Array.Empty<string>();
            Levels = levels ?? Array.Empty<WarehouseLevel>();
            PricingPlan = pricingPlan;
            Seats = seats ?? Array.Empty<HqSeat>();
            Hired = hired ?? Array.Empty<HiredHand>();
            Stores = stores ?? Array.Empty<string>();
            Staffed = staffed ?? Array.Empty<string>();
        }

        public IReadOnlyList<DeliveryStop> Stops { get; }

        public IReadOnlyList<WarehouseHold> Holds { get; }

        public IReadOnlyList<GoodsNeed> Needs { get; }

        public IReadOnlyList<DailyUnits> Sales { get; }

        public IReadOnlyList<GarageSlot> Slots { get; }

        public IReadOnlyList<string> Drivers { get; }

        public IReadOnlyList<WarehouseLevel> Levels { get; }

        public bool PricingPlan { get; }

        public IReadOnlyList<HqSeat> Seats { get; }

        public IReadOnlyList<HiredHand> Hired { get; }

        public IReadOnlyList<string> Stores { get; }

        public IReadOnlyList<string> Staffed { get; }

        public AuditWorld WithStops(IReadOnlyList<DeliveryStop>? stops)
        {
            return new AuditWorld(
                stops,
                Holds,
                Needs,
                Sales,
                Slots,
                Drivers,
                Levels,
                PricingPlan,
                Seats,
                Hired,
                Stores,
                Staffed);
        }
    }

    public sealed class AuditReport
    {
        public AuditReport(IReadOnlyList<string> lines, IReadOnlyList<DeliveryStop> stops)
        {
            Lines = lines ?? Array.Empty<string>();
            Stops = stops ?? Array.Empty<DeliveryStop>();
        }

        public IReadOnlyList<string> Lines { get; }

        public IReadOnlyList<DeliveryStop> Stops { get; }
    }

    public static class AuditBridge
    {
        public static bool Live(AuditHeal? heal)
        {
            if (heal == null || heal.Kind != "connect" || heal.Store.Length == 0 || heal.Warehouse.Length == 0)
            {
                return false;
            }

            try
            {
                var family = Advisor.CargoFamily(heal.Item) ?? "";
                var moved = new StoreDelivery(heal.Store, "", heal.Warehouse, family, true, heal.Call);
                return RedoBridge.Live(moved);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public static class Audit
    {
        public const int HealCap = 10;

        public const int HealStrikeMax = 3;

        public const int ReserveDays = 7;

        public static int Unconnected(
            IReadOnlyList<DeliveryStop>? stops,
            IReadOnlyList<WarehouseHold>? holds,
            IReadOnlyList<GoodsNeed>? needs,
            IReadOnlyList<DailyUnits>? sales)
        {
            var stores = new HashSet<string>();
            foreach (var link in StoreConnect.Connect(GameApi.Live(), stops, holds, needs, sales))
            {
                if (link != null && link.Store.Length > 0)
                {
                    stores.Add(link.Store);
                }
            }

            return stores.Count;
        }

        public static int EmptySlots(IReadOnlyList<GarageSlot>? slots)
        {
            var count = 0;
            if (slots == null)
            {
                return count;
            }

            foreach (var slot in slots)
            {
                if (slot != null && !slot.Filled && slot.Warehouse.Length > 0 && slot.Index > 0)
                {
                    count++;
                }
            }

            return count;
        }

        public static int Dry(ApiNote? api, IReadOnlyList<WarehouseLevel>? levels)
        {
            return Restock.Order(api, levels).Count;
        }

        public static int Mixed(IReadOnlyList<WarehouseHold>? holds)
        {
            return Advisor.WarehouseFamilyMix(holds ?? Array.Empty<WarehouseHold>()).Count;
        }

        public static int Unstaffed(IReadOnlyList<string>? stores, IReadOnlyList<string>? staffed)
        {
            var onDuty = new HashSet<string>();
            if (staffed != null)
            {
                foreach (var name in staffed)
                {
                    if (!string.IsNullOrEmpty(name))
                    {
                        onDuty.Add(name);
                    }
                }
            }

            var seen = new HashSet<string>();
            var count = 0;
            if (stores == null)
            {
                return count;
            }

            foreach (var store in stores)
            {
                if (string.IsNullOrEmpty(store) || !seen.Add(store) || onDuty.Contains(store))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        public static int MissingPlans(
            ApiNote? api,
            bool pricingPlan,
            IReadOnlyList<HqSeat>? seats,
            IReadOnlyList<HiredHand>? hired)
        {
            return HqPlans.Assign(api, pricingPlan, seats, hired).Count;
        }

        public static int CashBelowReserve(decimal cash, decimal dailyCost)
        {
            if (dailyCost <= 0)
            {
                return 0;
            }

            return cash < dailyCost * ReserveDays ? 1 : 0;
        }

        public static int PinMismatch(string? running, string? pinned)
        {
            if (string.IsNullOrEmpty(running) || string.IsNullOrEmpty(pinned))
            {
                return 0;
            }

            return running == pinned ? 0 : 1;
        }

        public static string Summary(int day, AuditCounts counts, int healed)
        {
            return "audit.summary version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " unconnected=" + counts.Unconnected.ToString(CultureInfo.InvariantCulture)
                + " emptySlots=" + counts.EmptySlots.ToString(CultureInfo.InvariantCulture)
                + " dry=" + counts.Dry.ToString(CultureInfo.InvariantCulture)
                + " mixed=" + counts.Mixed.ToString(CultureInfo.InvariantCulture)
                + " unstaffed=" + counts.Unstaffed.ToString(CultureInfo.InvariantCulture)
                + " missingPlans=" + counts.MissingPlans.ToString(CultureInfo.InvariantCulture)
                + " cashBelowReserve=" + counts.CashBelowReserve.ToString(CultureInfo.InvariantCulture)
                + " pinMismatch=" + counts.PinMismatch.ToString(CultureInfo.InvariantCulture)
                + " healed=" + healed.ToString(CultureInfo.InvariantCulture);
        }

        public static AuditReport Check(
            string localAppData,
            int day,
            ApiNote? api,
            AuditWorld world,
            AuditGuard guard,
            Func<AuditHeal, HealFate>? bridge)
        {
            world = world ?? new AuditWorld(
                null, null, null, null, null, null, null, true, null, null, null, null);
            guard = guard ?? new AuditGuard(true, false, 0, 0, 0, LoadLog.PinnedBuild);
            var stops = new List<DeliveryStop>(world.Stops);
            var slots = new List<GarageSlot>(world.Slots);
            var levels = new List<WarehouseLevel>(world.Levels);
            var pricing = world.PricingPlan;
            var seats = new List<HqSeat>(world.Seats);
            var staffed = new List<string>(world.Staffed);
            var lines = new List<string>();
            if (!guard.Enabled)
            {
                lines.Add(Summary(day, Count(api, stops, world, slots, levels, pricing, seats, staffed, guard), 0));
                Write(localAppData, lines);
                return new AuditReport(lines, stops);
            }

            if (PinMismatch(guard.Build, LoadLog.PinnedBuild) == 1)
            {
                lines.Add(Note(day, "autopilot.halted", "reason=pin"));
                lines.Add(Summary(day, Count(api, stops, world, slots, levels, pricing, seats, staffed, guard), 0));
                Write(localAppData, lines);
                return new AuditReport(lines, stops);
            }

            var selected = Select(api, world, stops, slots, levels, pricing, seats, staffed, guard);
            if (selected.Count > 0)
            {
                Snapshot(localAppData, stops);
            }

            var healed = 0;
            var failures = 0;
            foreach (var heal in selected)
            {
                if (guard.DryRun)
                {
                    Apply(heal, stops, slots, levels, ref pricing, seats, staffed);
                    lines.Add(Healed(day, heal, dry: true));
                    healed++;
                    continue;
                }

                var fate = bridge == null ? HealFate.Skip : bridge(heal);
                if (fate == HealFate.Skip)
                {
                    continue;
                }

                if (fate != HealFate.Done)
                {
                    failures++;
                    if (failures >= HealStrikeMax)
                    {
                        lines.Add(Note(day, "autopilot.paused", "reason=failures"));
                        break;
                    }

                    continue;
                }

                Apply(heal, stops, slots, levels, ref pricing, seats, staffed);
                lines.Add(Healed(day, heal, dry: false));
                healed++;
            }

            lines.Add(Summary(day, Count(api, stops, world, slots, levels, pricing, seats, staffed, guard), healed));
            Write(localAppData, lines);
            return new AuditReport(lines, stops);
        }

        private static List<AuditHeal> Select(
            ApiNote? api,
            AuditWorld world,
            IReadOnlyList<DeliveryStop> stops,
            IReadOnlyList<GarageSlot> slots,
            IReadOnlyList<WarehouseLevel> levels,
            bool pricing,
            IReadOnlyList<HqSeat> seats,
            IReadOnlyList<string> staffed,
            AuditGuard guard)
        {
            var chosen = new List<AuditHeal>();
            var room = SpendRoom(guard.Cash, guard.DailyCost);
            var reserve = guard.DailyCost * ReserveDays;
            foreach (var heal in Candidates(api, world, stops, slots, levels, pricing, seats, staffed, guard.TruckCost))
            {
                if (chosen.Count >= HealCap)
                {
                    break;
                }

                if (heal.Cost > 0 && (guard.Cash < reserve || heal.Cost > room))
                {
                    continue;
                }

                chosen.Add(heal);
                if (heal.Cost > 0)
                {
                    room -= heal.Cost;
                }
            }

            return chosen;
        }

        private static IEnumerable<AuditHeal> Candidates(
            ApiNote? api,
            AuditWorld world,
            IReadOnlyList<DeliveryStop> stops,
            IReadOnlyList<GarageSlot> slots,
            IReadOnlyList<WarehouseLevel> levels,
            bool pricing,
            IReadOnlyList<HqSeat> seats,
            IReadOnlyList<string> staffed,
            int truckCost)
        {
            foreach (var link in StoreConnect.Connect(api, stops, world.Holds, world.Needs, world.Sales))
            {
                if (link == null)
                {
                    continue;
                }

                yield return new AuditHeal(
                    "connect",
                    link.Add,
                    store: link.Store,
                    warehouse: link.Warehouse,
                    item: link.Item,
                    drop: link.Drop);
            }

            foreach (var fill in Slots.Fill(api, slots, world.Drivers))
            {
                yield return new AuditHeal(
                    "slot",
                    fill.Order,
                    cost: truckCost,
                    warehouse: fill.Warehouse,
                    slot: fill.Index,
                    order: fill.Order,
                    assign: fill.Assign,
                    driver: fill.Driver);
            }

            foreach (var order in Restock.Order(api, levels))
            {
                yield return new AuditHeal(
                    "order",
                    order.Start,
                    warehouse: order.Warehouse,
                    family: order.Family,
                    amount: order.Amount,
                    assign: order.Assign);
            }

            foreach (var drain in Restock.Arrange(world.Holds).Drains)
            {
                var assign = api == null ? "" : api.Call("purchasing plan");
                yield return new AuditHeal(
                    "drain",
                    assign,
                    family: drain.Family,
                    from: drain.From,
                    to: drain.To,
                    amount: drain.Amount,
                    assign: assign);
            }

            var hired = new HashSet<string>(staffed);
            foreach (var store in world.Stores)
            {
                if (string.IsNullOrEmpty(store) || !hired.Add(store))
                {
                    continue;
                }

                var call = api == null ? "" : api.Call("hire");
                if (call.Length == 0)
                {
                    continue;
                }

                yield return new AuditHeal("hire", call, store: store);
            }

            foreach (var move in HqPlans.Assign(api, pricing, seats, world.Hired))
            {
                yield return new AuditHeal(
                    "plan",
                    move.Call,
                    store: move.Employee,
                    role: move.Role,
                    assign: move.Kind);
            }
        }

        private static void Apply(
            AuditHeal heal,
            List<DeliveryStop> stops,
            List<GarageSlot> slots,
            List<WarehouseLevel> levels,
            ref bool pricing,
            List<HqSeat> seats,
            List<string> staffed)
        {
            if (heal.Kind == "connect")
            {
                var next = StoreConnect.Apply(
                    stops,
                    new[] { new StoreLink(heal.Store, heal.Warehouse, heal.Item, heal.Call, heal.Drop) });
                stops.Clear();
                stops.AddRange(next);
                return;
            }

            if (heal.Kind == "slot")
            {
                for (var index = 0; index < slots.Count; index++)
                {
                    var slot = slots[index];
                    if (slot.Warehouse == heal.Warehouse && slot.Index == heal.Slot)
                    {
                        slots[index] = new GarageSlot(slot.Warehouse, slot.Index, filled: true, heal.Driver);
                    }
                }

                return;
            }

            if (heal.Kind == "order")
            {
                for (var index = 0; index < levels.Count; index++)
                {
                    var level = levels[index];
                    if (level.Warehouse == heal.Warehouse && level.Family == heal.Family)
                    {
                        levels[index] = new WarehouseLevel(
                            level.Warehouse,
                            level.Family,
                            level.OnHand,
                            level.Daily,
                            ordered: true,
                            level.Mixed,
                            level.Shelves);
                    }
                }

                return;
            }

            if (heal.Kind == "hire" && heal.Store.Length > 0)
            {
                staffed.Add(heal.Store);
                return;
            }

            if (heal.Kind != "plan")
            {
                return;
            }

            if (heal.Assign == "create")
            {
                pricing = true;
                return;
            }

            for (var index = 0; index < seats.Count; index++)
            {
                if (seats[index].Role == heal.Role)
                {
                    seats[index] = new HqSeat(heal.Role, filled: true, heal.Store);
                }
            }
        }

        private static AuditCounts Count(
            ApiNote? api,
            IReadOnlyList<DeliveryStop> stops,
            AuditWorld world,
            IReadOnlyList<GarageSlot> slots,
            IReadOnlyList<WarehouseLevel> levels,
            bool pricing,
            IReadOnlyList<HqSeat> seats,
            IReadOnlyList<string> staffed,
            AuditGuard guard)
        {
            return new AuditCounts(
                Unconnected(stops, world.Holds, world.Needs, world.Sales),
                EmptySlots(slots),
                Dry(api, levels),
                Mixed(world.Holds),
                Unstaffed(world.Stores, staffed),
                MissingPlans(api, pricing, seats, world.Hired),
                CashBelowReserve(guard.Cash, guard.DailyCost),
                PinMismatch(guard.Build, LoadLog.PinnedBuild));
        }

        private static int SpendRoom(decimal cash, decimal dailyCost)
        {
            var above = cash - (dailyCost * ReserveDays);
            if (above <= 0)
            {
                return 0;
            }

            return (int)decimal.Floor(above * 0.25m);
        }

        private static string Healed(int day, AuditHeal heal, bool dry)
        {
            var body = "action=" + heal.Kind;
            if (heal.Kind == "connect")
            {
                body += " store=" + heal.Store
                    + " warehouse=" + heal.Warehouse
                    + " item=" + heal.Item
                    + " call=" + heal.Call;
            }
            else if (heal.Kind == "slot")
            {
                body += " warehouse=" + heal.Warehouse
                    + " slot=" + heal.Slot.ToString(CultureInfo.InvariantCulture)
                    + " order=" + heal.Order
                    + " assign=" + heal.Assign
                    + " driver=" + heal.Driver;
            }
            else if (heal.Kind == "order")
            {
                body += " warehouse=" + heal.Warehouse
                    + " family=" + heal.Family
                    + " amount=" + heal.Amount.ToString(CultureInfo.InvariantCulture)
                    + " call=" + heal.Call;
            }
            else if (heal.Kind == "drain")
            {
                body += " family=" + heal.Family
                    + " from=" + heal.From
                    + " to=" + heal.To
                    + " amount=" + heal.Amount.ToString(CultureInfo.InvariantCulture)
                    + " call=" + heal.Call;
            }
            else if (heal.Kind == "hire")
            {
                body += " store=" + heal.Store + " call=" + heal.Call;
            }
            else
            {
                body += " role=" + heal.Role + " call=" + heal.Call;
            }

            return "audit.healed version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " " + body
                + " dry=" + (dry ? "true" : "false");
        }

        private static string Note(int day, string name, string reason)
        {
            return name + " version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " " + reason;
        }

        private static void Snapshot(string localAppData, IReadOnlyList<DeliveryStop> stops)
        {
            var path = SupplyNight.SnapshotPath(localAppData ?? "");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var text = "snapshot.taken version=" + LoadLog.Version + Environment.NewLine;
            foreach (var stop in stops)
            {
                if (stop == null)
                {
                    continue;
                }

                text += stop.Warehouse + "\t" + stop.Destination + "\t" + stop.Item + Environment.NewLine;
            }

            File.AppendAllText(path, text);
        }

        private static void Write(string localAppData, IReadOnlyList<string> lines)
        {
            if (lines.Count == 0)
            {
                return;
            }

            var path = LoadLog.CeoLogPath(localAppData ?? "");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, string.Join(Environment.NewLine, lines) + Environment.NewLine);
        }
    }
}
