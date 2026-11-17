using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BigAmbitions.Items;
using Entities;

namespace ChaoticCEO
{
    public static class LiveDump
    {
        public static string Dump(string localAppData)
        {
            var current = SaveGameManager.Current;
            var line = StatementDump.Write(
                localAppData,
                current == null ? 0 : current.Day,
                Days(current));
            RouteDump.Write(localAppData, Plans(current), Holds(current));
            return line;
        }

        public static bool InCity()
        {
            return SaveGameManager.Current != null;
        }

        public static string BeginGame(string localAppData)
        {
            return BeginGame(localAppData, SaveGameManager.Current);
        }

        public static string BeginGame(string localAppData, object? save)
        {
            var opened = GameLog.Bind(localAppData, GameLog.Key(save));
            if (opened.Length == 0)
            {
                return "";
            }

            _loanScanned = false;
            _loanLogged = false;
            StorageShelfBridge.ResetSurvey();
            Stall.Clear();
            return opened;
        }

        public static IReadOnlyList<OverlayRow> OverlayRows()
        {
            var current = SaveGameManager.Current;
            return Overlay.Build(Days(current), Portfolio(current), Holds(current));
        }

        public static CityBoard Board()
        {
            var current = SaveGameManager.Current;
            return Roadshow.Compose(
                Days(current),
                Plans(current),
                Holds(current),
                Asks(current),
                Portfolio(current),
                Shut(current));
        }

        public static IReadOnlyList<BuildingAsk> Asks(GameInstance? current)
        {
            return Roadshow.Prices(SaleAsks(current), EstateAsks(current));
        }

        private static void ReadStores(GameInstance? current, List<GoodsNeed> needs, List<DailyUnits> burn)
        {
            if (current == null || current.BuildingRegistrations == null)
            {
                return;
            }

            foreach (var building in current.BuildingRegistrations)
            {
                ReadStore(needs, burn, building);
            }
        }

        private static void ReadStore(List<GoodsNeed> needs, List<DailyUnits> burn, BuildingRegistration building)
        {
            if (building == null || string.IsNullOrEmpty(building.StreetName) || building.StreetNumber <= 0)
            {
                return;
            }

            var place = building.StreetName + " " + building.StreetNumber.ToString(CultureInfo.InvariantCulture);
            var items = new List<string>();
            if (building.retailPrices != null)
            {
                foreach (var price in building.retailPrices)
                {
                    if (price != null && !string.IsNullOrEmpty(price.itemName))
                    {
                        items.Add(price.itemName);
                    }
                }
            }

            var ticks = new List<SaleTick>();
            if (building.orderHistory != null)
            {
                foreach (var entry in building.orderHistory)
                {
                    if (entry == null || entry.itemSales == null)
                    {
                        continue;
                    }

                    foreach (var sale in entry.itemSales)
                    {
                        if (sale == null || string.IsNullOrEmpty(sale.itemName))
                        {
                            continue;
                        }

                        ticks.Add(new SaleTick(sale.itemName, entry.dayNumber, sale.amountSold));
                    }
                }
            }

            var store = new StoreRecord(
                building.BusinessName,
                building.RentedByPlayer,
                building.BuildingOwnedByPlayer,
                building.businessOwnerRivalId,
                building.businessTypeName,
                place);
            Supply.FromBuilding(needs, burn, store, items, ticks);
        }

        public static void HoldNeeds(NeedsWatch watch, string localAppData)
        {
            var current = SaveGameManager.Current;
            if (watch == null || current == null || current.gameVariables == null)
            {
                return;
            }

            var now = new PlayerNeeds(
                current.Energy,
                current.Hunger,
                current.Happiness,
                current.gameVariables.disableEnergy,
                current.gameVariables.disableHappiness);
            var held = watch.Tick(localAppData, current, current.Day, now);
            if (held == null)
            {
                return;
            }

            current.gameVariables.disableEnergy = held.EnergyOff;
            current.gameVariables.disableHappiness = held.HappinessOff;
            current.Energy = held.Energy;
            current.Hunger = held.Hunger;
            current.Happiness = held.Happiness;
        }

        public static void FillSlots(SlotWatch watch, string localAppData)
        {
            if (watch == null || string.IsNullOrEmpty(localAppData))
            {
                return;
            }

            var current = SaveGameManager.Current;
            if (current == null)
            {
                return;
            }

            // The line names Order and AssignVehicle. This pause does not call them.
            watch.Tick(
                localAppData,
                current,
                current.Day,
                GameApi.Live(),
                Garage(current),
                Drivers(current));
        }

        public static void ConnectStores(ConnectWatch watch, string localAppData)
        {
            if (watch == null || string.IsNullOrEmpty(localAppData))
            {
                return;
            }

            var current = SaveGameManager.Current;
            if (current == null)
            {
                return;
            }

            var needs = new List<GoodsNeed>();
            var sales = new List<DailyUnits>();
            ReadStores(current, needs, sales);
            // The lines name AddDestination and Remove. This pause does not call them.
            watch.Tick(
                localAppData,
                current,
                current.Day,
                GameApi.Live(),
                Plans(current),
                Holds(current),
                needs,
                sales);
        }

        public static void AssignPlans(PlanWatch watch, string localAppData)
        {
            if (watch == null || string.IsNullOrEmpty(localAppData))
            {
                return;
            }

            var current = SaveGameManager.Current;
            if (current == null)
            {
                return;
            }

            // The lines name AddPlan and AssignEmployee. This pause does not call them.
            watch.Tick(
                localAppData,
                current,
                current.Day,
                GameApi.Live(),
                PricingPlan(current),
                Seats(current),
                Hired(current));
        }

        private static bool PricingPlan(GameInstance current)
        {
            return current.pricingManagerPlans != null && current.pricingManagerPlans.Count > 0;
        }

        private static List<HqSeat> Seats(GameInstance current)
        {
            var seats = new List<HqSeat>();
            if (current.logisticsManagerPlans != null)
            {
                foreach (var plan in current.logisticsManagerPlans)
                {
                    AddSeat(seats, "logistics", plan == null ? "" : plan.assignedEmployeeId);
                }
            }

            if (current.hrManagerPlans != null)
            {
                foreach (var plan in current.hrManagerPlans)
                {
                    AddSeat(seats, "hr", plan == null ? "" : plan.assignedEmployeeId);
                }
            }

            if (current.headhunterPlans != null)
            {
                foreach (var plan in current.headhunterPlans)
                {
                    AddSeat(seats, "headhunter", plan == null ? "" : plan.assignedEmployeeId);
                }
            }

            if (current.importPartnerships != null)
            {
                foreach (var partnership in current.importPartnerships)
                {
                    AddSeat(seats, "purchasing", partnership == null ? "" : partnership.employeeInstanceId);
                }
            }

            return seats;
        }

        private static void AddSeat(List<HqSeat> seats, string role, string? employee)
        {
            var id = employee ?? "";
            seats.Add(new HqSeat(role, id.Length > 0, id));
        }

        private static List<HiredHand> Hired(GameInstance current)
        {
            var hired = new List<HiredHand>();
            if (current.EmployeeInstances == null)
            {
                return hired;
            }

            var skills = new[]
            {
                HqPlans.LogisticsSkill,
                HqPlans.HrSkill,
                HqPlans.PurchasingSkill,
                HqPlans.HeadhunterSkill,
            };
            foreach (var employee in current.EmployeeInstances)
            {
                if (employee == null || string.IsNullOrEmpty(employee.id))
                {
                    continue;
                }

                foreach (var skill in skills)
                {
                    if (employee.HasSkill(skill))
                    {
                        hired.Add(new HiredHand(employee.id, skill, employee.IsCandidate));
                    }
                }
            }

            return hired;
        }

        public static void RestockWarehouses(RestockWatch watch, string localAppData)
        {
            if (watch == null || string.IsNullOrEmpty(localAppData))
            {
                return;
            }

            var current = SaveGameManager.Current;
            if (current == null)
            {
                return;
            }

            var sales = new List<DailyUnits>();
            ReadStores(current, new List<GoodsNeed>(), sales);
            var shelves = new Dictionary<string, int>();
            var holds = Holds(current, shelves);
            var plans = Plans(current);
            var ordered = OrderedWarehouses(current, holds);
            var arranged = Restock.Arrange(holds, shelves);
            var demanded = Restock.WithBurn(arranged.Homes, plans, sales, ordered);
            // Truck loads are not read yet. Overflow only trips on ordered-and-short until that read exists.
            var overflow = Restock.Overflow(demanded, null);
            // The lines name the drain, overflow, and order. This pause does not call the game.
            var displays = DisplayStock.Morning(current.Day, WholesalerBridge.SellingLines());
            watch.Tick(
                localAppData,
                current,
                current.Day,
                GameApi.Live(),
                overflow.Homes,
                arranged.Drains,
                overflow.Claims,
                displays.Ordered);
        }

        public static void AuditDay(string localAppData)
        {
            if (string.IsNullOrEmpty(localAppData))
            {
                return;
            }

            var current = SaveGameManager.Current;
            if (current == null)
            {
                return;
            }

            var world = AuditWorldFrom(current);
            var guard = Guard(current);
            Audit.Check(
                localAppData,
                current.Day,
                GameApi.Live(),
                world,
                guard,
                heal => LiveHeal(localAppData, heal));
        }

        public static AutopilotDay RunAutopilot(string localAppData)
        {
            if (string.IsNullOrEmpty(localAppData))
            {
                return AutopilotDay.Skipped("");
            }

            var current = SaveGameManager.Current;
            if (current == null)
            {
                return AutopilotDay.Skipped("");
            }

            var world = AuditWorldFrom(current);
            var guard = Guard(current);
            var wealth = Helpers.PlayerHelper.GetPersonalWealth();
            var ownedHealth = OwnedStoreHealth(current);
            foreach (var row in ownedHealth)
            {
                StoreHealth.Record(localAppData, row);
                if (Autopilot.Enabled)
                {
                    Stall.Track(localAppData, row, current.Day);
                }
            }

            var day = WinRun.Day(
                localAppData,
                current.Day,
                ToMoney(wealth.CurrentWealth),
                GameApi.Live(),
                world,
                guard,
                heal => LiveHeal(localAppData, heal),
                NextPlaybook(localAppData),
                TimeAdvanceBridge.Read(),
                HudLine(),
                Health.Latest(localAppData),
                timeBridge: TimeAdvanceBridge.Defer,
                actionBridge: (step, call) => ExecutePlaybook(localAppData, step, call));
            return day.Loop;
        }

        private static bool ExecutePlaybook(string localAppData, PlaybookStep step, string call)
        {
            if (step == null || !step.Chosen || Autopilot.DryRun)
            {
                return true;
            }

            if (step.Kind == "where")
            {
                if (!SplitPlace(step.Detail, out var street, out var number))
                {
                    return false;
                }

                var rent = new BuildingRent(street, number, true, call);
                if (!RentBridge.Live(rent))
                {
                    return false;
                }

                var current = SaveGameManager.Current;
                Rent.Record(localAppData, rent, current == null ? 0 : current.Day);
                return true;
            }

            if (step.Kind == "run")
            {
                return ExecuteRun(localAppData, step.Detail);
            }

            // open / money / others: log through Autopilot; expand bridges as Stage 1 needs them.
            return true;
        }

        private static bool ExecuteRun(string localAppData, string action)
        {
            var current = SaveGameManager.Current;
            if (current == null)
            {
                return false;
            }

            switch (action)
            {
                case "tax":
                {
                    var due = TaxBillBridge.Due();
                    var paid = TaxBill.Pay(due.current, due.back, GameApi.Live());
                    if (!paid.Paid || !TaxBillBridge.Live(paid))
                    {
                        return false;
                    }

                    TaxBill.Record(localAppData, paid, current.Day);
                    return true;
                }
                case "demand":
                {
                    var any = false;
                    foreach (var row in StaffDemandBridge.Due())
                    {
                        var pick = StaffDemand.Clear(
                            row.unmetDays,
                            row.demand,
                            row.employeeId,
                            GameApi.Live());
                        if (!pick.Cleared || !StaffDemandBridge.Live(pick))
                        {
                            continue;
                        }

                        StaffDemand.Record(localAppData, pick, current.Day);
                        any = true;
                    }

                    return any;
                }
                case "marketing":
                {
                    if (!LastRent(localAppData, out var street, out var number))
                    {
                        return false;
                    }

                    var started = Marketing.Start(
                        Marketing.StarterCost,
                        Marketing.StarterGain,
                        MarketingBridge.HasCampaign(street, number),
                        street,
                        number,
                        Marketing.StarterType,
                        GameApi.Live());
                    if (!started.Started || !MarketingBridge.Start(started))
                    {
                        return false;
                    }

                    Marketing.RecordStart(localAppData, started, current.Day);
                    return true;
                }
                case "decline":
                {
                    var pending = RivalTakeoverBridge.Pending();
                    if (pending == null)
                    {
                        return false;
                    }

                    var declined = RivalTakeover.Decline(
                        pending.Value.offer,
                        pending.Value.dailyProfit,
                        pending.Value.street,
                        pending.Value.number,
                        alreadyDeclined: false,
                        GameApi.Live());
                    if (!declined.Declined)
                    {
                        return false;
                    }

                    RivalTakeoverBridge.Decline(declined);
                    RivalTakeover.Record(localAppData, declined, current.Day);
                    return true;
                }
                default:
                    return true;
            }
        }

        private static bool SplitPlace(string place, out string street, out int number)
        {
            street = "";
            number = 0;
            if (string.IsNullOrEmpty(place))
            {
                return false;
            }

            var at = place.LastIndexOf(' ');
            if (at <= 0 || at >= place.Length - 1)
            {
                return false;
            }

            street = place.Substring(0, at);
            return int.TryParse(place.Substring(at + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
                && street.Length > 0
                && number > 0;
        }

        private static AuditWorld AuditWorldFrom(GameInstance current)
        {
            var needs = new List<GoodsNeed>();
            var sales = new List<DailyUnits>();
            ReadStores(current, needs, sales);
            var shelves = new Dictionary<string, int>();
            var holds = Holds(current, shelves);
            var plans = Plans(current);
            var ordered = OrderedWarehouses(current, holds);
            var arranged = Restock.Arrange(holds, shelves);
            var demanded = Restock.WithBurn(arranged.Homes, plans, sales, ordered);
            var overflow = Restock.Overflow(demanded, null);
            RegisterLists(current, out var stores, out var staffed);
            return new AuditWorld(
                plans,
                holds,
                needs,
                sales,
                Garage(current),
                Drivers(current),
                overflow.Homes,
                PricingPlan(current),
                Seats(current),
                Hired(current),
                stores,
                staffed);
        }

        private static void RegisterLists(
            GameInstance current,
            out List<string> stores,
            out List<string> staffed)
        {
            stores = new List<string>();
            staffed = new List<string>();
            if (current?.BuildingRegistrations == null)
            {
                return;
            }

            foreach (var building in current.BuildingRegistrations)
            {
                if (building == null
                    || !building.RentedByPlayer
                    || string.IsNullOrEmpty(building.StreetName)
                    || building.StreetNumber <= 0
                    || !GoodsStore(building)
                    || !CashierBridge.HasRegister(building))
                {
                    continue;
                }

                var place = building.StreetName
                    + " "
                    + building.StreetNumber.ToString(CultureInfo.InvariantCulture);
                stores.Add(place);
                if (SenseStaff(building).RegisterStaffed)
                {
                    staffed.Add(place);
                }
            }
        }

        private static bool GoodsStore(BuildingRegistration building)
        {
            var family = StoreFamily.For(building.businessTypeName ?? "");
            return family == "specialty"
                || family == "food"
                || family == "durables"
                || family == "service";
        }

        public static string? TryRent(RentWatch watch, string localAppData)
        {
            if (watch == null || string.IsNullOrEmpty(localAppData) || !InCity())
            {
                return null;
            }

            var current = SaveGameManager.Current;
            if (current == null || !watch.Due(current))
            {
                return null;
            }

            if (PlayerRents(current))
            {
                watch.Mark(current);
                var skipped = "building.skipped version=" + LoadLog.Version
                    + " day=" + current.Day.ToString(CultureInfo.InvariantCulture)
                    + " reason=rented";
                File.AppendAllText(LoadLog.CeoLogPath(localAppData), skipped + System.Environment.NewLine);
                return skipped;
            }

            var wealth = Helpers.PlayerHelper.GetPersonalWealth();
            var chosen = Rent.Choose(OpenLots(current), ToMoney(wealth.cash), anyRented: false, GameApi.Live());
            if (!chosen.Rented)
            {
                return null;
            }

            watch.Mark(current);
            if (!RentBridge.Live(chosen))
            {
                return Rent.Miss(localAppData, chosen, current.Day);
            }

            return Rent.Record(localAppData, chosen, current.Day);
        }

        public static string? TryLayout(StarterWatch watch, string localAppData)
        {
            if (watch == null || string.IsNullOrEmpty(localAppData) || !InCity())
            {
                return null;
            }

            var current = SaveGameManager.Current;
            if (current == null || !watch.Due(current))
            {
                return null;
            }

            if (PlayerRents(current))
            {
                watch.Mark(current);
                return Starter.Skip(localAppData, current.Day);
            }

            if (!LastRent(localAppData, out var street, out var number))
            {
                return null;
            }

            var registration = FindLot(current, street, number);
            if (registration == null || !StarterBridge.Inside(new BuildingRent(street, number, true, "")))
            {
                return null;
            }

            var rented = new BuildingRent(street, number, true, GameApi.Live().Call("clone"));
            StarterPick chosen;
            try
            {
                chosen = Starter.Choose(
                    StarterBridge.Offers(registration),
                    alreadyOpen: false,
                    GameApi.Live(),
                    registration.businessTypeName);
            }
            catch (System.Exception)
            {
                return null;
            }

            if (!Starter.Settle(watch, current, chosen))
            {
                return null;
            }

            if (!chosen.Loaded)
            {
                return Starter.Miss(localAppData, chosen, rented, current.Day);
            }

            if (!StarterBridge.Live(registration, chosen))
            {
                return Starter.Miss(localAppData, chosen, rented, current.Day);
            }

            QuoteBill(localAppData, registration, chosen, current.Day);
            return Starter.Record(localAppData, chosen, rented, current.Day);
        }

        public static string? TryOps(StoreOpsWatch watch, string localAppData)
        {
            if (watch == null || string.IsNullOrEmpty(localAppData) || !InCity())
            {
                return null;
            }

            var current = SaveGameManager.Current;
            if (current != null && watch.VerifyDue())
            {
                var kept = FindRented(current, watch.VerifyStreet, watch.VerifyNumber);
                var shift = false;
                var person = false;
                var type = "";
                var uniform = "";
                if (kept != null)
                {
                    StoreOpsBridge.Verify(kept, out shift, out person, out type, out uniform);
                }

                return StoreOps.Verify(
                    localAppData,
                    watch.VerifyPick,
                    new BuildingRent(watch.VerifyStreet, watch.VerifyNumber, true, ""),
                    watch.VerifyDay,
                    shift,
                    person,
                    type,
                    uniform);
            }

            if (current == null || !watch.Due(current))
            {
                return null;
            }

            if (!LastLayout(localAppData, out var street, out var number, out var skipped))
            {
                return null;
            }

            if (skipped)
            {
                watch.Mark(current);
                return StoreOps.Skip(localAppData, current.Day);
            }

            var registration = FindRented(current, street, number);
            if (registration == null)
            {
                return null;
            }

            var rented = new BuildingRent(street, number, true, "");
            if (!StoreOpsBridge.HasItems(registration))
            {
                watch.Note();
                if (watch.Waiting())
                {
                    return null;
                }

                watch.Mark(current);
                return StoreOps.Miss(localAppData, StoreOps.Assign(null, null, null), rented, current.Day, "noitems");
            }

            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, GameApi.Live());
            var assigned = StoreOps.Assign(
                chosen,
                StoreOps.Opening(),
                StoreOpsBridge.Fixtures(registration),
                GameApi.Live());
            if (!assigned.Assigned)
            {
                watch.Mark(current);
                return StoreOps.Miss(localAppData, assigned, rented, current.Day, "empty");
            }

            var live = StoreOpsBridge.Live(registration, assigned, out var reason, out var placed);
            StoreOps.CleanerLine(localAppData, rented, current.Day, placed);
            if (!live)
            {
                if (StoreOps.Retry(reason))
                {
                    watch.Note();
                    if (watch.Waiting())
                    {
                        return null;
                    }
                }

                watch.Mark(current);
                return StoreOps.Miss(localAppData, assigned, rented, current.Day, reason);
            }

            watch.Mark(current);
            if (assigned.Cleaner.Length > 0)
            {
                watch.Arm(street, number, current.Day, StoreOps.VerifyFrames, assigned);
            }

            return StoreOps.Record(localAppData, assigned, rented, current.Day);
        }

        public static string? TryCapacity(CapacityWatch watch, string localAppData)
        {
            if (watch == null || string.IsNullOrEmpty(localAppData) || !InCity())
            {
                return null;
            }

            var current = SaveGameManager.Current;
            if (current == null || !watch.Due(current))
            {
                return null;
            }

            if (!LastOps(localAppData, out var street, out var number, out var skipped))
            {
                return null;
            }

            if (skipped)
            {
                watch.Mark(current);
                return null;
            }

            var registration = FindRented(current, street, number);
            if (registration == null)
            {
                return null;
            }

            var rented = new BuildingRent(street, number, true, "");
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, GameApi.Live());
            var already = CapacityBridge.AlreadyMatched(localAppData, street, number);
            var matched = Capacity.Match(rented, chosen, already, GameApi.Live());
            if (!matched.Matched)
            {
                watch.Mark(current);
                return "";
            }

            if (!CapacityBridge.Live(registration, matched))
            {
                watch.Mark(current);
                return Capacity.Miss(localAppData, matched, current.Day);
            }

            watch.Mark(current);
            return Capacity.Record(localAppData, matched, current.Day);
        }

        public static string? TryWholesaler(WholesalerWatch watch, string localAppData)
        {
            if (watch == null || string.IsNullOrEmpty(localAppData) || !InCity())
            {
                return null;
            }

            var current = SaveGameManager.Current;
            if (current == null || !watch.Due(current))
            {
                return null;
            }

            if (!LastOps(localAppData, out var street, out var number, out var skipped))
            {
                return null;
            }

            if (skipped)
            {
                watch.Mark(current);
                return null;
            }

            var registration = FindRented(current, street, number);
            if (registration == null)
            {
                return null;
            }

            var rented = new BuildingRent(street, number, true, "");
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, GameApi.Live());
            var already = WholesalerBridge.HasContract(street, number);
            var signed = Wholesaler.Sign(rented, chosen, Wholesaler.StageOne(), already, GameApi.Live());
            if (!signed.Signed)
            {
                watch.Mark(current);
                return "";
            }

            watch.Mark(current);
            if (!WholesalerBridge.Live(
                signed,
                Wholesaler.ContractType(registration.businessTypeName, chosen.BusinessType)))
            {
                return Wholesaler.Miss(localAppData, signed, current.Day);
            }

            return Wholesaler.Record(localAppData, signed, current.Day);
        }

        public static string? TryCashier(CashierWatch watch, string localAppData)
        {
            if (watch == null || string.IsNullOrEmpty(localAppData) || !InCity())
            {
                return null;
            }

            var current = SaveGameManager.Current;
            if (current == null || !watch.Due(current))
            {
                return null;
            }

            if (!LastContract(localAppData, out var street, out var number, out var skipped))
            {
                return null;
            }

            if (skipped)
            {
                watch.Mark(current);
                return null;
            }

            var registration = FindRented(current, street, number);
            if (registration == null)
            {
                return null;
            }

            var rented = new BuildingRent(street, number, true, "");
            var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, GameApi.Live());
            var signed = Wholesaler.Sign(
                rented,
                chosen,
                Wholesaler.StageOne(),
                alreadySigned: false,
                GameApi.Live());
            var already = CashierBridge.HasCashier(registration);
            var hired = Cashier.Hire(rented, chosen, signed, already, GameApi.Live());
            if (!hired.Hired)
            {
                watch.Mark(current);
                return "";
            }

            if (!CashierBridge.Live(registration, hired, out var reason))
            {
                if (Cashier.Retry(reason))
                {
                    watch.Note();
                    if (watch.Waiting())
                    {
                        return null;
                    }
                }

                watch.Mark(current);
                return Cashier.Miss(localAppData, hired, current.Day, reason);
            }

            watch.Mark(current);
            return Cashier.Record(localAppData, hired, current.Day);
        }

        public static string? TryFitOut(string localAppData)
        {
            if (string.IsNullOrEmpty(localAppData) || !InCity())
            {
                return null;
            }

            var current = SaveGameManager.Current;
            if (current?.BuildingRegistrations == null)
            {
                return null;
            }

            BuildingRegistration? target = null;
            string place = "";
            FitOutProgress? progress = null;
            foreach (var building in current.BuildingRegistrations)
            {
                if (building == null
                    || !building.RentedByPlayer
                    || string.IsNullOrEmpty(building.StreetName)
                    || building.StreetNumber <= 0)
                {
                    continue;
                }

                var family = StoreFamily.For(building.businessTypeName ?? "");
                var goods = family == "specialty"
                    || family == "food"
                    || family == "durables"
                    || family == "service";
                var emptyLot = string.IsNullOrEmpty(building.businessTypeName)
                    && string.IsNullOrEmpty(building.BusinessName);
                if (!goods && Modes.FitOut(building.businessTypeName).Length == 0 && !emptyLot)
                {
                    continue;
                }

                var row = FitProgress(localAppData, building);
                if (row.LayoutLoaded
                    && row.OpsAssigned
                    && row.CapacityMatched
                    && row.ContractSigned
                    && row.CashierHired)
                {
                    continue;
                }

                target = building;
                place = building.StreetName
                    + " "
                    + building.StreetNumber.ToString(CultureInfo.InvariantCulture);
                progress = row;
                break;
            }

            if (target == null || progress == null || place.Length == 0)
            {
                return null;
            }

            var priors = new List<StoreHealth>();
            foreach (var health in OwnedStoreHealth(current))
            {
                if (health != null && health.Place != place)
                {
                    priors.Add(health);
                }
            }

            var pick = FitOut.Decide(priors, place, progress);
            if (!pick.Chosen)
            {
                return null;
            }

            var line = RunFitOutStep(localAppData, current, target, place, pick);
            if (line == null)
            {
                return null;
            }

            FitOut.Record(localAppData, pick, current.Day);
            return line;
        }

        public static string? TryLoan(BankLoanWatch watch, string localAppData)
        {
            if (watch == null || string.IsNullOrEmpty(localAppData) || !InCity())
            {
                return null;
            }

            var current = SaveGameManager.Current;
            if (current == null || !watch.Due(current))
            {
                return null;
            }

            if (!LastCashier(localAppData, out _, out _, out var skipped))
            {
                return null;
            }

            if (skipped)
            {
                watch.Mark(current);
                return null;
            }

            var profit = Starter.StageOne()[0].DailyProfit;
            var already = BankLoanBridge.HasLoan();
            var taken = BankLoan.Take(profit, BankLoanBridge.Offers(profit), already, GameApi.Live());
            watch.Mark(current);
            if (!taken.Taken)
            {
                return "";
            }

            if (!BankLoanBridge.Take(taken))
            {
                return "";
            }

            return BankLoan.RecordTake(localAppData, taken, current.Day);
        }

        public static string? TryDemand(StaffDemandWatch watch, string localAppData)
        {
            if (watch == null || string.IsNullOrEmpty(localAppData) || !InCity())
            {
                return null;
            }

            var current = SaveGameManager.Current;
            if (current == null || !watch.Due(current))
            {
                return null;
            }

            DemandPick? cleared = null;
            foreach (var row in StaffDemandBridge.Due())
            {
                var pick = StaffDemand.Clear(row.unmetDays, row.demand, row.employeeId, GameApi.Live());
                if (!pick.Cleared)
                {
                    continue;
                }

                if (!StaffDemandBridge.Live(pick))
                {
                    continue;
                }

                cleared = pick;
                break;
            }

            watch.Mark(current);
            if (cleared == null)
            {
                return "";
            }

            return StaffDemand.Record(localAppData, cleared, current.Day);
        }

        public static string? TryTax(TaxBillWatch watch, string localAppData)
        {
            if (watch == null || string.IsNullOrEmpty(localAppData) || !InCity())
            {
                return null;
            }

            var current = SaveGameManager.Current;
            if (current == null || !watch.Due(current))
            {
                return null;
            }

            var due = TaxBillBridge.Due();
            var paid = TaxBill.Pay(due.current, due.back, GameApi.Live());
            watch.Mark(current);
            if (!paid.Paid)
            {
                return "";
            }

            if (!TaxBillBridge.Live(paid))
            {
                return "";
            }

            return TaxBill.Record(localAppData, paid, current.Day);
        }

        public static string? TryMarketing(MarketingWatch watch, TimeAdvanceWatch time, string localAppData)
        {
            if (watch == null || string.IsNullOrEmpty(localAppData) || !InCity())
            {
                return null;
            }

            var current = SaveGameManager.Current;
            if (current == null)
            {
                return null;
            }

            if (!HasLoan(localAppData))
            {
                return null;
            }

            if (!LastRent(localAppData, out var street, out var number))
            {
                return null;
            }

            if (watch.StartDue(current))
            {
                var already = MarketingBridge.HasCampaign(street, number);
                var started = Marketing.Start(
                    Marketing.StarterCost,
                    Marketing.StarterGain,
                    already,
                    street,
                    number,
                    Marketing.StarterType,
                    GameApi.Live());
                if (!started.Started || !MarketingBridge.Start(started))
                {
                    if (already)
                    {
                        watch.MarkStarted(current);
                        return "";
                    }

                    return null;
                }

                watch.MarkStarted(current);
                return Marketing.RecordStart(localAppData, started, current.Day);
            }

            var weekDone = time != null && (time.Finished(current) || time.Completed >= TimeAdvance.WeekDays);
            if (!watch.StopDue(current, weekDone))
            {
                return null;
            }

            var stopped = Marketing.Stop(
                alreadyActive: MarketingBridge.HasCampaign(street, number) || watch.Active(current),
                street,
                number,
                GameApi.Live());
            watch.MarkStopped(current);
            if (!stopped.Stopped)
            {
                return "";
            }

            if (!MarketingBridge.Stop(stopped))
            {
                return "";
            }

            return Marketing.RecordStop(localAppData, stopped, current.Day);
        }

        public static string? TryTakeover(MarketingWatch marketing, RivalTakeoverWatch watch, string localAppData)
        {
            if (watch == null || string.IsNullOrEmpty(localAppData) || !InCity())
            {
                return null;
            }

            var current = SaveGameManager.Current;
            if (current == null || !watch.Due(current) || !HasLoan(localAppData))
            {
                return null;
            }

            string street;
            int number;
            decimal offer;
            decimal profit;
            var pending = RivalTakeoverBridge.Pending();
            if (pending != null)
            {
                street = pending.Value.street;
                number = pending.Value.number;
                offer = pending.Value.offer;
                profit = pending.Value.dailyProfit;
            }
            else if (marketing != null
                && !marketing.Active(current)
                && !marketing.StartDue(current)
                && LastRent(localAppData, out street, out number))
            {
                offer = 1000m;
                profit = 10m;
            }
            else
            {
                return null;
            }

            var declined = RivalTakeover.Decline(
                offer,
                profit,
                street,
                number,
                alreadyDeclined: false,
                GameApi.Live());
            watch.Mark(current);
            if (!declined.Declined)
            {
                return "";
            }

            RivalTakeoverBridge.Decline(declined);
            return RivalTakeover.Record(localAppData, declined, current.Day);
        }

        public static string? TryTime(TimeAdvanceWatch watch, string localAppData)
        {
            if (watch == null || string.IsNullOrEmpty(localAppData) || !InCity())
            {
                return null;
            }

            var current = SaveGameManager.Current;
            // Keep the watch due after WeekDays. The clock is activity speed, and it stays on across days.
            if (current == null || !TimeAdvance.KeepSkipping(watch, current))
            {
                return null;
            }

            // A loaded save is a new object. A wait aimed at the previous save's next day must not block start.
            if (watch.Bind(current))
            {
                _loanScanned = false;
            }

            watch.Arrive(current.Day);

            var needs = TimeAdvanceBridge.Read();
            if (TimeAdvance.BelowFloor(needs))
            {
                var care = TimeAdvance.Care(needs, GameApi.Live());
                if (!care.Due || !TimeAdvanceBridge.Care(care))
                {
                    return null;
                }

                return TimeAdvance.RecordCare(localAppData, care, current.Day);
            }

            var allowed = Autopilot.Enabled && ReadyToSkip(localAppData, current.Day);
            var action = TimeAdvanceBridge.Sync(localAppData, allowed, "frame");
            if ((action == "start" || TimeAdvanceBridge.Skipping()) && !watch.Waiting(current.Day))
            {
                watch.WaitFor(current.Day + 1);
            }

            return action == "hold" ? null : action;
        }

        private static bool _loanScanned;
        private static bool _loanLogged;

        private static bool ReadyToSkip(string localAppData, int day)
        {
            _ = localAppData;
            return TimeAdvance.ReadyToSkip(saveHasLoan: false, logRecorded: false, day);
        }

        private static bool HasLoan(string localAppData)
        {
            if (BankLoanBridge.HasLoan())
            {
                return true;
            }

            if (_loanScanned)
            {
                return _loanLogged;
            }

            _loanScanned = true;
            var path = LoadLog.CeoLogPath(localAppData);
            if (File.Exists(path))
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    if (line.StartsWith("loan.taken ", System.StringComparison.Ordinal))
                    {
                        _loanLogged = true;
                        break;
                    }
                }
            }

            return _loanLogged;
        }

        private static bool LastContract(string localAppData, out string street, out int number, out bool skipped)
        {
            return CeoLedger.TryContract(localAppData, out street, out number, out skipped);
        }

        private static bool LastCashier(string localAppData, out string street, out int number, out bool skipped)
        {
            return CeoLedger.TryCashier(localAppData, out street, out number, out skipped);
        }

        private static bool LastOps(string localAppData, out string street, out int number, out bool skipped)
        {
            return CeoLedger.TryOps(localAppData, out street, out number, out skipped);
        }

        private static bool LastLayout(string localAppData, out string street, out int number, out bool skipped)
        {
            return CeoLedger.TryLayout(localAppData, out street, out number, out skipped);
        }

        private static BuildingRegistration? FindRented(GameInstance current, string street, int number)
        {
            if (current.BuildingRegistrations == null)
            {
                return null;
            }

            foreach (var building in current.BuildingRegistrations)
            {
                var registration = building as BuildingRegistration;
                if (registration != null
                    && registration.StreetName == street
                    && registration.StreetNumber == number
                    && registration.RentedByPlayer)
                {
                    return registration;
                }
            }

            return null;
        }

        private static bool LastRent(string localAppData, out string street, out int number)
        {
            return CeoLedger.TryRent(localAppData, out street, out number);
        }

        private static BuildingRegistration? FindLot(GameInstance current, string street, int number)
        {
            if (current.BuildingRegistrations == null)
            {
                return null;
            }

            foreach (var building in current.BuildingRegistrations)
            {
                var registration = building as BuildingRegistration;
                if (registration != null
                    && registration.StreetName == street
                    && registration.StreetNumber == number
                    && registration.RentedByPlayer
                    && string.IsNullOrEmpty(registration.BusinessName))
                {
                    return registration;
                }
            }

            return null;
        }

        private static void QuoteBill(string localAppData, BuildingRegistration registration, StarterPick chosen, int day)
        {
            var quote = Modes.Standard(chosen.BusinessType);
            if (!quote.Chosen)
            {
                return;
            }

            BillBridge.Live(registration, quote);
            Modes.Record(localAppData, quote, day);
        }

        private static FitOutProgress FitProgress(string localAppData, BuildingRegistration building)
        {
            var street = building.StreetName ?? "";
            var number = building.StreetNumber;
            var layout = StoreOpsBridge.HasItems(building)
                || !string.IsNullOrEmpty(building.BusinessName);
            var ops = StoreOpsBridge.HasCleaner(building);
            var capacity = CapacityBridge.AlreadyMatched(localAppData, street, number);
            var contract = !Modes.SignsContract(building.businessTypeName)
                || WholesalerBridge.HasContract(street, number);
            var cashier = CashierBridge.HasCashier(building);
            return new FitOutProgress(layout, ops, capacity, contract, cashier);
        }

        private static string? RunFitOutStep(
            string localAppData,
            GameInstance current,
            BuildingRegistration registration,
            string place,
            FitOutPick pick)
        {
            if (!SplitPlace(place, out var street, out var number))
            {
                return null;
            }

            var rented = new BuildingRent(street, number, true, "");
            switch (pick.Step)
            {
                case "layout":
                {
                    if (!StarterBridge.Inside(rented))
                    {
                        return null;
                    }

                    StarterPick chosen;
                    try
                    {
                        chosen = Starter.Choose(
                            StarterBridge.Offers(registration),
                            alreadyOpen: false,
                            GameApi.Live(),
                            registration.businessTypeName);
                    }
                    catch (Exception)
                    {
                        return null;
                    }

                    if (Starter.Hold(chosen))
                    {
                        return null;
                    }

                    if (!chosen.Loaded)
                    {
                        return Starter.Miss(localAppData, chosen, rented, current.Day);
                    }

                    if (!StarterBridge.Live(registration, chosen))
                    {
                        return Starter.Miss(localAppData, chosen, rented, current.Day);
                    }

                    QuoteBill(localAppData, registration, chosen, current.Day);
                    return Starter.Record(localAppData, chosen, rented, current.Day);
                }

                case "ops":
                {
                    if (!StoreOpsBridge.HasItems(registration))
                    {
                        return null;
                    }

                    var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, GameApi.Live());
                    var assigned = StoreOps.Assign(
                        chosen,
                        StoreOps.Opening(),
                        StoreOpsBridge.Fixtures(registration),
                        GameApi.Live());
                    if (!assigned.Assigned)
                    {
                        return StoreOps.Miss(localAppData, assigned, rented, current.Day, "empty");
                    }

                    if (!StoreOpsBridge.Live(registration, assigned, out var reason, out var placed))
                    {
                        return StoreOps.Miss(localAppData, assigned, rented, current.Day, reason);
                    }

                    StoreOps.CleanerLine(localAppData, rented, current.Day, placed);
                    return StoreOps.Record(localAppData, assigned, rented, current.Day);
                }

                case "capacity":
                {
                    var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, GameApi.Live());
                    var already = CapacityBridge.AlreadyMatched(localAppData, street, number);
                    var matched = Capacity.Match(rented, chosen, already, GameApi.Live());
                    if (!matched.Matched)
                    {
                        return "";
                    }

                    if (!CapacityBridge.Live(registration, matched))
                    {
                        return Capacity.Miss(localAppData, matched, current.Day);
                    }

                    return Capacity.Record(localAppData, matched, current.Day);
                }

                case "contract":
                {
                    var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, GameApi.Live());
                    var already = WholesalerBridge.HasContract(street, number);
                    var signed = Wholesaler.Sign(rented, chosen, Wholesaler.StageOne(), already, GameApi.Live());
                    if (!signed.Signed)
                    {
                        return "";
                    }

                    if (!WholesalerBridge.Live(
                        signed,
                        Wholesaler.ContractType(registration.businessTypeName, chosen.BusinessType)))
                    {
                        return Wholesaler.Miss(localAppData, signed, current.Day);
                    }

                    return Wholesaler.Record(localAppData, signed, current.Day);
                }

                case "cashier":
                {
                    var chosen = Starter.Choose(Starter.StageOne(), alreadyOpen: false, GameApi.Live());
                    var signed = Wholesaler.Sign(
                        rented,
                        chosen,
                        Wholesaler.StageOne(),
                        alreadySigned: false,
                        GameApi.Live());
                    var already = CashierBridge.HasCashier(registration);
                    var hired = Cashier.Hire(rented, chosen, signed, already, GameApi.Live());
                    if (!hired.Hired)
                    {
                        return "";
                    }

                    if (!CashierBridge.Live(registration, hired, out var reason))
                    {
                        return Cashier.Miss(localAppData, hired, current.Day, reason);
                    }

                    return Cashier.Record(localAppData, hired, current.Day);
                }

                default:
                    return null;
            }
        }

        private static bool PlayerRents(GameInstance current)
        {
            if (current.BuildingRegistrations == null)
            {
                return false;
            }

            foreach (var building in current.BuildingRegistrations)
            {
                var registration = building as BuildingRegistration;
                if (registration != null
                    && registration.RentedByPlayer
                    && !string.IsNullOrEmpty(registration.BusinessName))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<RentLot> OpenLots(GameInstance current)
        {
            var lots = new List<RentLot>();
            if (current.BuildingRegistrations == null)
            {
                return lots;
            }

            foreach (var building in current.BuildingRegistrations)
            {
                var registration = building as BuildingRegistration;
                if (registration == null || !registration.AvailableForRent || registration.RentedByPlayer)
                {
                    continue;
                }

                lots.Add(new RentLot(
                    registration.StreetName ?? "",
                    registration.StreetNumber,
                    ToMoney(registration.RentPerDay)));
            }

            return lots;
        }

        public static void WriteTelemetry(string localAppData)
        {
            if (string.IsNullOrEmpty(localAppData) || !InCity())
            {
                return;
            }

            var hud = HudLine();
            Telemetry.Record(
                localAppData,
                hud,
                Health.Latest(localAppData),
                Controls.Row(Autopilot.Enabled, Autopilot.DryRun));
            var current = SaveGameManager.Current;
            if (current != null)
            {
                var wealth = Helpers.PlayerHelper.GetPersonalWealth();
                Win.Record(
                    LoadLog.CeoLogPath(localAppData),
                    current.Day,
                    ToMoney(wealth.CurrentWealth));
            }
        }

        private static HealFate LiveHeal(string localAppData, AuditHeal heal)
        {
            if (heal == null)
            {
                return HealFate.Skip;
            }

            if (heal.Kind == "hire")
            {
                return ReplaceCashier(localAppData, heal) ? HealFate.Done : HealFate.Failed;
            }

            if (heal.Kind != "connect")
            {
                return HealFate.Skip;
            }

            return AuditBridge.Live(heal) ? HealFate.Done : HealFate.Failed;
        }

        private static bool ReplaceCashier(string localAppData, AuditHeal heal)
        {
            var current = SaveGameManager.Current;
            if (current == null || !SplitPlace(heal.Store, out var street, out var number))
            {
                return false;
            }

            var registration = FindOwned(current, street, number);
            if (registration == null)
            {
                return false;
            }

            if (SenseStaff(registration).RegisterStaffed)
            {
                return true;
            }

            CashierBridge.Departure(registration, out var resigned, out var retired, out var poached);
            var gap = Cashier.Cover(heal.Store, registerStaffed: false, resigned, retired, poached, GameApi.Live());
            if (!gap.Hire)
            {
                return false;
            }

            if (!CashierBridge.Fill(registration, gap.Pick, out var reason))
            {
                Cashier.Missed(localAppData, gap.Pick, current.Day, reason);
                return false;
            }

            Cashier.Replaced(localAppData, gap.Pick, current.Day, gap.Reason);
            return true;
        }

        public static string? TryRestaff(CashierWatch watch, string localAppData)
        {
            if (watch == null || string.IsNullOrEmpty(localAppData) || !InCity() || Autopilot.DryRun)
            {
                return null;
            }

            var manager = InstanceBehavior<BuildingManager>.Instance;
            var registration = manager == null ? null : manager.buildingRegistration;
            if (registration == null || !registration.RentedByPlayer || !GoodsStore(registration))
            {
                watch.ClearFill();
                return null;
            }

            var place = (registration.StreetName ?? "")
                + " "
                + registration.StreetNumber.ToString(CultureInfo.InvariantCulture);
            if (watch.Filled(place) || !watch.RestaffAgain(place))
            {
                return null;
            }

            if (CashierBridge.HasCashier(registration))
            {
                watch.Fill(place);
                return null;
            }

            if (HeadcountBridge.Blocks(Cashier.Skill))
            {
                var kept = new CashierPick(
                    true,
                    Cashier.RoleName,
                    Cashier.Skill,
                    Cashier.GiftType,
                    registration.StreetName ?? "",
                    registration.StreetNumber,
                    "",
                    "");
                CashierBridge.Fill(registration, kept, out _);
                watch.Fill(place);
                return null;
            }

            var current = SaveGameManager.Current;
            CashierBridge.Departure(registration, out var resigned, out var retired, out var poached);
            var gap = Cashier.Cover(
                place,
                registerStaffed: false,
                resigned,
                retired,
                poached,
                GameApi.Live());
            if (!gap.Hire || current == null)
            {
                return null;
            }

            if (!CashierBridge.Fill(registration, gap.Pick, out var reason))
            {
                if (Cashier.Retry(reason))
                {
                    watch.RestaffNote();
                    return null;
                }

                watch.Fill(place);
                return Cashier.Missed(localAppData, gap.Pick, current.Day, reason);
            }

            watch.Fill(place);
            return Cashier.Replaced(localAppData, gap.Pick, current.Day, gap.Reason);
        }

        public static void ReleaseCleaner(CashierWatch watch, MorningWatch morning)
        {
            var current = SaveGameManager.Current;
            if (watch == null || morning == null || current == null || morning.Day == 0 || current.Day == morning.Day)
            {
                return;
            }

            watch.ClearFill();
        }

        public static string? TryCleaner(CashierWatch watch, string localAppData)
        {
            if (watch == null || string.IsNullOrEmpty(localAppData) || !InCity() || Autopilot.DryRun)
            {
                return null;
            }

            var manager = InstanceBehavior<BuildingManager>.Instance;
            var registration = manager == null ? null : manager.buildingRegistration;
            if (registration == null || !registration.RentedByPlayer || !GoodsStore(registration))
            {
                watch.ClearFill();
                return null;
            }

            var place = (registration.StreetName ?? "")
                + " "
                + registration.StreetNumber.ToString(CultureInfo.InvariantCulture);
            if (watch.Filled(place) || !watch.RestaffAgain(place))
            {
                return null;
            }

            var shiftStaffed = StoreStaff.FromShifts(
                ReadStaffShifts(registration),
                registration.warnedLastHourAboutNoEmployee).CleanerStaffed;
            if (Cleaner.OnStation(shiftStaffed, StoreOpsBridge.HasCleaner(registration)))
            {
                watch.Fill(place);
                return null;
            }

            if (HeadcountBridge.Blocks(Cleaner.Skill))
            {
                var seated = new StoreOpsPick(true, Cleaner.RoleName, "", false, "", "", "");
                StoreOpsBridge.Live(registration, seated, out _, out _);
                watch.Fill(place);
                return null;
            }

            var current = SaveGameManager.Current;
            CashierBridge.Departure(registration, out var resigned, out var retired, out var poached);
            var gap = Cleaner.Cover(place, cleanerStaffed: false, resigned, retired, poached, GameApi.Live());
            if (!gap.Hire || current == null)
            {
                return null;
            }

            var ops = new StoreOpsPick(true, gap.Pick.Role, "", false, gap.Pick.StationCall, "", "");
            if (!StoreOpsBridge.Live(registration, ops, out var reason, out _))
            {
                if (Cashier.Retry(reason))
                {
                    watch.RestaffNote();
                    return null;
                }

                watch.Fill(place);
                return Cleaner.Missed(localAppData, gap.Pick, current.Day, reason);
            }

            watch.Fill(place);
            return Cleaner.Replaced(localAppData, gap.Pick, current.Day, gap.Reason);
        }

        public static int TryCover(MorningWatch watch, string localAppData)
        {
            if (watch == null || string.IsNullOrEmpty(localAppData) || !InCity())
            {
                return 0;
            }

            var current = SaveGameManager.Current;
            if (current == null)
            {
                return 0;
            }

            var day = Morning.CoverDay(null, Autopilot.Enabled, current.Day, watch.Day);
            if (day == 0)
            {
                return 0;
            }

            var changed = WholesalerBridge.Maintain(localAppData, day);
            changed += WholesalerBridge.StockDisplays(localAppData, day);
            watch.Mark(day);
            return changed;
        }

        public static int TrySurvey(string localAppData)
        {
            if (string.IsNullOrEmpty(localAppData) || !InCity())
            {
                return 0;
            }

            var current = SaveGameManager.Current;
            if (current?.BuildingRegistrations == null)
            {
                return 0;
            }

            var written = 0;
            foreach (var building in current.BuildingRegistrations)
            {
                if (building == null || !building.RentedByPlayer)
                {
                    continue;
                }

                written += StorageShelfBridge.Survey(localAppData, building, current.Day);
            }

            return written;
        }

        private static BuildingRegistration? FindOwned(GameInstance current, string street, int number)
        {
            if (current?.BuildingRegistrations == null)
            {
                return null;
            }

            foreach (var building in current.BuildingRegistrations)
            {
                if (building != null
                    && building.RentedByPlayer
                    && building.StreetName == street
                    && building.StreetNumber == number)
                {
                    return building;
                }
            }

            return null;
        }

        private static HashSet<string> OrderedWarehouses(GameInstance current, IReadOnlyList<WarehouseHold> holds)
        {
            var byPlace = new Dictionary<string, string>();
            foreach (var hold in holds)
            {
                if (hold == null || hold.Warehouse.Length == 0)
                {
                    continue;
                }

                var place = hold.Street + " " + hold.Number.ToString(CultureInfo.InvariantCulture);
                if (!byPlace.ContainsKey(place))
                {
                    byPlace[place] = hold.Warehouse;
                }
            }

            var ordered = new HashSet<string>();
            if (current.importPartnerships == null)
            {
                return ordered;
            }

            foreach (var partnership in current.importPartnerships)
            {
                if (partnership == null || partnership.products == null)
                {
                    continue;
                }

                foreach (var product in partnership.products)
                {
                    if (product == null || product.assignedWarehouse == null)
                    {
                        continue;
                    }

                    var place = Place(product.assignedWarehouse);
                    if (byPlace.TryGetValue(place, out var warehouse))
                    {
                        ordered.Add(warehouse);
                    }
                }
            }

            return ordered;
        }

        private static List<GarageSlot> Garage(GameInstance current)
        {
            var slots = new List<GarageSlot>();
            if (current.BuildingRegistrations == null)
            {
                return slots;
            }

            foreach (var building in current.BuildingRegistrations)
            {
                var warehouse = building as Warehouse;
                if (warehouse == null
                    || warehouse.businessTypeName != "ba:businesstype_warehouse"
                    || string.IsNullOrEmpty(warehouse.BusinessName)
                    || warehouse.vehicleSlots == null)
                {
                    continue;
                }

                var index = 1;
                foreach (var slot in warehouse.vehicleSlots)
                {
                    if (slot != null)
                    {
                        var filled = !string.IsNullOrEmpty(slot.vehicleInstanceId);
                        slots.Add(new GarageSlot(warehouse.BusinessName, index, filled, slot.employeeDriverId ?? ""));
                    }

                    index++;
                }
            }

            return slots;
        }

        private static List<string> Drivers(GameInstance current)
        {
            var taken = new HashSet<string>();
            if (current.BuildingRegistrations != null)
            {
                foreach (var building in current.BuildingRegistrations)
                {
                    var warehouse = building as Warehouse;
                    if (warehouse == null || warehouse.vehicleSlots == null)
                    {
                        continue;
                    }

                    foreach (var slot in warehouse.vehicleSlots)
                    {
                        if (slot != null && !string.IsNullOrEmpty(slot.employeeDriverId))
                        {
                            taken.Add(slot.employeeDriverId);
                        }
                    }
                }
            }

            var free = new List<string>();
            if (current.EmployeeInstances == null)
            {
                return free;
            }

            foreach (var employee in current.EmployeeInstances)
            {
                if (employee == null
                    || employee.IsCandidate
                    || string.IsNullOrEmpty(employee.id)
                    || taken.Contains(employee.id)
                    || !employee.HasSkill(Slots.DriverSkill))
                {
                    continue;
                }

                free.Add(employee.id);
            }

            free.Sort(System.StringComparer.Ordinal);
            return free;
        }

        public static string? Roll(DayWatch watch, string localAppData)
        {
            if (watch == null || string.IsNullOrEmpty(localAppData))
            {
                return null;
            }

            var current = SaveGameManager.Current;
            if (current == null || !watch.Due(current.Day))
            {
                return null;
            }

            var needs = new List<GoodsNeed>();
            var sales = new List<DailyUnits>();
            ReadStores(current, needs, sales);
            var parts = ReadCost(current);
            DailyCost.Record(localAppData, DailyCost.Line(current.Day, Cash(current), parts));
            return watch.Tick(
                localAppData,
                current.Day,
                Days(current),
                Plans(current),
                Holds(current),
                needs,
                sales);
        }

        public static IReadOnlyList<FinancialDay> Days(GameInstance? current)
        {
            if (current == null || current.financialSummaries == null)
            {
                return new FinancialDay[0];
            }

            var days = new List<FinancialDay>();
            foreach (var summary in current.financialSummaries)
            {
                days.Add(ToDay(summary));
            }

            return days;
        }

        private static FinancialDay ToDay(FinancialSummary summary)
        {
            if (summary == null)
            {
                return new FinancialDay(0, new StatementRow[0]);
            }

            var rows = new List<StatementRow>();
            if (summary.businessIncomeStatements != null)
            {
                foreach (var business in summary.businessIncomeStatements)
                {
                    if (business == null)
                    {
                        continue;
                    }

                    var address = business.Address;
                    var street = address == null || address.streetName == null ? "" : address.streetName;
                    var number = address == null ? 0 : address.streetNumber;
                    rows.Add(new StatementRow(
                        street,
                        number,
                        business.TotalSales,
                        business.TotalResources,
                        business.SalaryExpenses,
                        business.RentExpenses,
                        business.MarketingExpenses,
                        business.Theft,
                        business.TotalProfit));
                }
            }

            return new FinancialDay(summary.dayNumber, rows);
        }

        private static List<DeliveryStop> Plans(GameInstance? current)
        {
            var routes = new List<DeliveryStop>();
            if (current == null)
            {
                return routes;
            }

            if (current.DeliveryContracts != null)
            {
                foreach (var contract in current.DeliveryContracts)
                {
                    AddContract(routes, contract);
                }
            }

            if (current.logisticsManagerPlans != null)
            {
                foreach (var plan in current.logisticsManagerPlans)
                {
                    AddPlan(routes, plan);
                }
            }

            return routes;
        }

        private static void AddContract(List<DeliveryStop> routes, DeliveryContract contract)
        {
            if (contract == null || contract.items == null)
            {
                return;
            }

            var warehouse = Place(contract.wholesaleAddress);
            var destination = Place(contract.businessAddress);
            foreach (var item in contract.items)
            {
                if (item == null)
                {
                    continue;
                }

                routes.Add(new DeliveryStop(
                    warehouse,
                    destination,
                    item.itemName,
                    contract.nextDeliveryDay,
                    ""));
            }
        }

        private static void AddPlan(List<DeliveryStop> routes, Buildings.Office.Headquarters.LogisticsManagerPlan plan)
        {
            if (plan == null || plan.destinations == null)
            {
                return;
            }

            var warehouse = Place(plan.targetAddress);
            foreach (var destination in plan.destinations)
            {
                if (destination == null || destination.stockTargets == null)
                {
                    continue;
                }

                var label = Place(destination.deliveryTargetAddress);
                foreach (var target in destination.stockTargets)
                {
                    if (target == null)
                    {
                        continue;
                    }

                    routes.Add(new DeliveryStop(warehouse, label, target.itemName, null, ""));
                }
            }
        }

        public static IReadOnlyList<PortfolioBusiness> Shut(GameInstance? current)
        {
            var rows = new List<PortfolioBusiness>();
            if (current == null || current.BuildingRegistrations == null)
            {
                return rows;
            }

            var buildings = new List<(string Street, int Number, bool Named)>();
            foreach (var building in current.BuildingRegistrations)
            {
                if (building == null)
                {
                    continue;
                }

                buildings.Add((
                    building.StreetName ?? "",
                    building.StreetNumber,
                    !string.IsNullOrEmpty(building.BusinessName)));
            }

            return WithoutABusiness(buildings);
        }

        public static IReadOnlyList<PortfolioBusiness> WithoutABusiness(
            IReadOnlyList<(string Street, int Number, bool Named)>? buildings)
        {
            var rows = new List<PortfolioBusiness>();
            var named = new Dictionary<(string Street, int Number), bool>();
            if (buildings != null)
            {
                foreach (var building in buildings)
                {
                    if (string.IsNullOrEmpty(building.Street) || building.Number <= 0)
                    {
                        continue;
                    }

                    var key = (building.Street, building.Number);
                    if (!named.ContainsKey(key))
                    {
                        named[key] = building.Named;
                    }
                    else if (building.Named)
                    {
                        named[key] = true;
                    }
                }
            }

            foreach (var pair in named)
            {
                if (pair.Value)
                {
                    continue;
                }

                rows.Add(new PortfolioBusiness("", "", pair.Key.Street, pair.Key.Number));
            }

            return rows;
        }

        public static IReadOnlyList<PortfolioBusiness> Portfolio(GameInstance? current)
        {
            var rows = new List<PortfolioBusiness>();
            if (current == null || current.BuildingRegistrations == null)
            {
                return rows;
            }

            foreach (var building in current.BuildingRegistrations)
            {
                AddBusiness(rows, building);
            }

            return rows;
        }

        public static List<WarehouseHold> Holds(GameInstance? current, Dictionary<string, int>? shelves = null)
        {
            var rows = new List<WarehouseHold>();
            if (current == null || current.BuildingRegistrations == null)
            {
                return rows;
            }

            foreach (var building in current.BuildingRegistrations)
            {
                AddWarehouse(rows, building, shelves);
            }

            return rows;
        }

        private static List<BuildingAsk> SaleAsks(GameInstance? current)
        {
            var rows = new List<BuildingAsk>();
            if (current == null || current.buildingsForSale == null)
            {
                return rows;
            }

            foreach (var sale in current.buildingsForSale)
            {
                if (sale == null || sale.address == null)
                {
                    continue;
                }

                rows.Add(new BuildingAsk(
                    sale.address.streetName ?? "",
                    sale.address.streetNumber,
                    sale.buildingPrice));
            }

            return rows;
        }

        private static List<BuildingAsk> EstateAsks(GameInstance? current)
        {
            var rows = new List<BuildingAsk>();
            if (current == null || current.realEstate == null)
            {
                return rows;
            }

            foreach (var estate in current.realEstate)
            {
                if (estate == null || estate.address == null)
                {
                    continue;
                }

                rows.Add(new BuildingAsk(
                    estate.address.streetName ?? "",
                    estate.address.streetNumber,
                    estate.purchasePrice));
            }

            return rows;
        }

        private static void AddBusiness(List<PortfolioBusiness> rows, BuildingRegistration building)
        {
            if (building == null)
            {
                return;
            }

            var family = StoreFamily.For(building.businessTypeName);
            if (family == null || string.IsNullOrEmpty(building.BusinessName) || string.IsNullOrEmpty(building.StreetName))
            {
                return;
            }

            rows.Add(new PortfolioBusiness(
                building.BusinessName,
                family,
                building.StreetName,
                building.StreetNumber));
        }

        private static void AddWarehouse(
            List<WarehouseHold> rows,
            BuildingRegistration building,
            Dictionary<string, int>? shelves)
        {
            if (building == null || building.businessTypeName != "ba:businesstype_warehouse")
            {
                return;
            }

            if (string.IsNullOrEmpty(building.BusinessName) || string.IsNullOrEmpty(building.StreetName))
            {
                return;
            }

            if (building.itemInstances == null)
            {
                return;
            }

            var placed = new List<string>();
            var cargo = new List<string>();
            foreach (var item in building.itemInstances.Values)
            {
                CollectCargo(item, placed, cargo);
            }

            if (shelves != null)
            {
                var count = RouteDump.PlacedShelves(placed);
                if (count > 0)
                {
                    shelves[building.BusinessName] = count;
                }
            }

            rows.AddRange(RouteDump.CountHolds(
                building.BusinessName,
                building.StreetName,
                building.StreetNumber,
                placed,
                cargo));
        }

        private static void CollectCargo(ItemInstance item, List<string> placed, List<string> cargo)
        {
            if (item == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(item.itemName))
            {
                placed.Add(item.itemName);
            }

            if (item.cargoInstances == null)
            {
                return;
            }

            foreach (var box in item.cargoInstances)
            {
                if (box == null)
                {
                    continue;
                }

                cargo.Add(box.itemName);
                if (box.nestedCargoInstances == null)
                {
                    continue;
                }

                foreach (var nested in box.nestedCargoInstances)
                {
                    if (nested == null)
                    {
                        continue;
                    }

                    cargo.Add(nested.itemName);
                }
            }
        }

        public static string HudLine()
        {
            var current = SaveGameManager.Current;
            if (current == null)
            {
                return TopBar.Line(0, 0m, 0m, 0m, 1, Autopilot.Enabled, 0);
            }

            var wealth = Helpers.PlayerHelper.GetPersonalWealth();
            var cash = ToMoney(wealth.cash);
            var net = ToMoney(wealth.CurrentWealth);
            var needs = new List<GoodsNeed>();
            var sales = new List<DailyUnits>();
            ReadStores(current, needs, sales);
            var stage = Stage.Read(current.Day, cash, needs, Plans(current), Holds(current), Garage(current));
            var business = TopBar.Yesterday(Days(current), current.Day);
            var profit = TopBar.ShownProfit(DayTotal(current, business), business);
            return TopBar.Line(current.Day, cash, net, profit, stage, Autopilot.Enabled, ReadSpeed());
        }

        public static PlaybookStep NextPlaybook(string? localAppData = null)
        {
            var current = SaveGameManager.Current;
            if (current == null)
            {
                return PlaybookStep.None();
            }

            var wealth = Helpers.PlayerHelper.GetPersonalWealth();
            var cash = ToMoney(wealth.cash);
            var needs = new List<GoodsNeed>();
            var sales = new List<DailyUnits>();
            ReadStores(current, needs, sales);
            var plans = Plans(current);
            var holds = Holds(current);
            var slots = Garage(current);
            var stage = Stage.Read(current.Day, cash, needs, plans, holds, slots);
            var opens = Open.StageOneCandidates(
                Starter.StageOne(),
                OwnedByFamily(current),
                WinRun.PendingOpen);
            var health = OwnedStoreHealth(current);
            var sites = SiteCandidates(current);
            var business = TopBar.Yesterday(Days(current), current.Day);
            var profit = Money.ProjectedProfit(TopBar.ShownProfit(DayTotal(current, business), business));
            var cost = Operating(current);
            var money = Money.Decide(
                stage,
                cash,
                cost,
                profit,
                offers: BankLoanBridge.Offers(profit),
                loan: null,
                bestPaybackRate: 0m);
            var run = Run.Decide(LiveRunFacts(current, localAppData));
            return WinRun.Next(
                stage,
                opens,
                WinRun.PendingOpen,
                sites,
                cash,
                Money.Reserve(cost),
                money,
                run,
                health);
        }

        private static RunFacts LiveRunFacts(GameInstance current, string? localAppData)
        {
            var unmetDays = 0;
            foreach (var row in StaffDemandBridge.Due())
            {
                if (row.unmetDays > unmetDays)
                {
                    unmetDays = row.unmetDays;
                }
            }

            var tax = TaxBillBridge.Due();
            var pending = RivalTakeoverBridge.Pending();
            var marketingCost = 0m;
            var marketingGain = 0m;
            if (!string.IsNullOrEmpty(localAppData)
                && LastRent(localAppData, out var street, out var number)
                && !MarketingBridge.HasCampaign(street, number))
            {
                marketingCost = Marketing.StarterCost;
                marketingGain = Marketing.StarterGain;
            }

            return new RunFacts(
                current.Energy,
                current.Hunger,
                current.Happiness,
                unmetDays,
                employeeState: "",
                hasDirt: false,
                theft: 0m,
                sales: 0m,
                unmetCustomerDemand: false,
                marketingCost,
                marketingGain,
                taxDue: tax.current + tax.back,
                takeoverOffer: pending == null ? 0m : pending.Value.offer,
                takeoverDailyProfit: pending == null ? 0m : pending.Value.dailyProfit);
        }

        private static IReadOnlyList<StoreHealth> OwnedStoreHealth(GameInstance current)
        {
            var rows = new List<StoreHealth>();
            if (current.BuildingRegistrations == null)
            {
                return rows;
            }

            foreach (var building in current.BuildingRegistrations)
            {
                if (building == null
                    || !building.RentedByPlayer
                    || string.IsNullOrEmpty(building.businessTypeName)
                    || string.IsNullOrEmpty(building.StreetName)
                    || building.StreetNumber <= 0)
                {
                    continue;
                }

                var family = StoreFamily.For(building.businessTypeName);
                var mode = Modes.Of(building.businessTypeName);
                var goods = family == "specialty" || family == "food" || family == "durables" || family == "service";
                if (!goods && mode != BusinessMode.Workstation)
                {
                    continue;
                }

                var place = building.StreetName
                    + " "
                    + building.StreetNumber.ToString(CultureInfo.InvariantCulture);
                var cargo = FixtureCargo(building);
                var needed = Wholesaler.Catalog(building.businessTypeName);
                var staff = SenseStaff(building);
                var todayOrder = OrderDaySales(building, current.Day);
                var priorOrder = OrderDaySales(building, current.Day - 1);
                var sales = StoreHealth.CompletedDaySales(
                    todayOrder.Customers,
                    todayOrder.Revenue,
                    priorOrder.Customers,
                    priorOrder.Revenue);
                var statementProfits = StoreHealth.StatementProfits(
                    Days(current),
                    building.StreetName,
                    building.StreetNumber,
                    current.Day);
                var incomeProfits = OpenDayProfits(building);
                var rawProfits = statementProfits.Count > 0 ? statementProfits : incomeProfits;
                var profits = StoreHealth.DropIncompleteTail(rawProfits, sales.Customers);
                var stations = Modes.Read(building.businessTypeName, PlacedNames(building));
                rows.Add(
                    StoreHealth.Assess(
                        new StoreFacts(
                            place,
                            building.temporarilyClosed,
                            needed,
                            cargo,
                            staff.RegisterStaffed,
                            staff.CleanerStaffed,
                            staff.Satisfaction,
                            scheduledOpen: true,
                            dailyCustomers: sales.Customers,
                            dailyRevenue: sales.Revenue,
                            recentOpenDayProfits: profits,
                            businessType: building.businessTypeName,
                            stylingStations: stations.Styling,
                            completeWorkstations: stations.Workstations)));
            }

            return rows;
        }

        private static StaffSense SenseStaff(BuildingRegistration building)
        {
            var fromShifts = StoreStaff.FromShifts(
                ReadStaffShifts(building),
                building.warnedLastHourAboutNoEmployee);
            var register = fromShifts.RegisterStaffed || CashierBridge.HasCashier(building);
            var cleaner = fromShifts.CleanerStaffed || StoreOpsBridge.HasCleaner(building);
            var satisfaction = register && cleaner && !building.warnedLastHourAboutNoEmployee
                ? 100f
                : 0f;
            return StoreStaff.Sense(
                register,
                cleaner,
                satisfaction,
                employeeHeadcount: building.aiEmployees == null ? 0 : building.aiEmployees.Count);
        }

        private static IReadOnlyList<StaffShift> ReadStaffShifts(BuildingRegistration building)
        {
            var rows = new List<StaffShift>();
            if (building?.scheduleDays == null)
            {
                return rows;
            }

            var cleanerStations = new HashSet<string>(StringComparer.Ordinal);
            if (building.itemInstances != null)
            {
                foreach (var item in building.itemInstances.Values)
                {
                    if (item != null
                        && item.itemName == "ba:itemname_cleaningstation"
                        && !string.IsNullOrEmpty(item.id))
                    {
                        cleanerStations.Add(item.id);
                    }
                }
            }

            foreach (var day in building.scheduleDays)
            {
                if (day?.workShifts == null)
                {
                    continue;
                }

                foreach (var shift in day.workShifts)
                {
                    if (shift == null || string.IsNullOrEmpty(shift.employeeId))
                    {
                        continue;
                    }

                    var itemId = shift.itemInstanceId ?? "";
                    if (shift.type == WorkShiftType.Cleaning || cleanerStations.Contains(itemId))
                    {
                        rows.Add(new StaffShift(shift.employeeId, itemId, StaffShift.Cleaner));
                    }
                    else
                    {
                        rows.Add(new StaffShift(shift.employeeId, itemId, StaffShift.Register));
                    }
                }
            }

            return rows;
        }

        private static IReadOnlyList<string> PlacedNames(BuildingRegistration building)
        {
            var names = new List<string>();
            if (building.itemInstances == null)
            {
                return names;
            }

            foreach (var item in building.itemInstances.Values)
            {
                if (item != null && !string.IsNullOrEmpty(item.itemName))
                {
                    names.Add(item.itemName);
                }
            }

            return names;
        }

        private static IReadOnlyDictionary<string, int> FixtureCargo(BuildingRegistration building)
        {
            var rows = new List<CargoAmount>();
            if (building.itemInstances == null)
            {
                return StoreHealth.CountCargoAmounts(rows);
            }

            foreach (var item in building.itemInstances.Values)
            {
                CollectCargoAmounts(item, rows);
            }

            return StoreHealth.CountCargoAmounts(rows);
        }

        private static void CollectCargoAmounts(ItemInstance item, List<CargoAmount> rows)
        {
            if (item?.cargoInstances == null)
            {
                return;
            }

            foreach (var box in item.cargoInstances)
            {
                if (box == null)
                {
                    continue;
                }

                rows.Add(new CargoAmount(box.itemName, box.amount));
                if (box.nestedCargoInstances == null)
                {
                    continue;
                }

                foreach (var nested in box.nestedCargoInstances)
                {
                    if (nested == null)
                    {
                        continue;
                    }

                    rows.Add(new CargoAmount(nested.itemName, nested.amount));
                }
            }
        }

        private static DaySales OrderDaySales(BuildingRegistration building, int day)
        {
            if (building?.orderHistory == null || day <= 0)
            {
                return StoreHealth.OrderDaySales(0, 0m);
            }

            foreach (var entry in building.orderHistory)
            {
                if (entry == null || entry.dayNumber != day)
                {
                    continue;
                }

                return StoreHealth.OrderDaySales(entry.totalCustomers, (decimal)entry.totalRevenue);
            }

            return StoreHealth.OrderDaySales(0, 0m);
        }

        private static IReadOnlyList<decimal> OpenDayProfits(BuildingRegistration building)
        {
            if (building.dailyIncomes == null || building.dailyIncomes.Count == 0)
            {
                return Array.Empty<decimal>();
            }

            var rows = new List<decimal>(building.dailyIncomes.Count);
            foreach (var income in building.dailyIncomes)
            {
                rows.Add((decimal)income);
            }

            return rows;
        }

        private static IReadOnlyDictionary<string, int> OwnedByFamily(GameInstance current)
        {
            var owned = new Dictionary<string, int>(StringComparer.Ordinal);
            if (current.BuildingRegistrations == null)
            {
                return owned;
            }

            foreach (var building in current.BuildingRegistrations)
            {
                if (building == null || !building.RentedByPlayer || string.IsNullOrEmpty(building.businessTypeName))
                {
                    continue;
                }

                var family = StoreFamily.For(building.businessTypeName);
                if (string.IsNullOrEmpty(family))
                {
                    continue;
                }

                owned.TryGetValue(family, out var count);
                owned[family] = count + 1;
            }

            return owned;
        }

        private static IReadOnlyList<SiteCandidate> SiteCandidates(GameInstance current)
        {
            var rows = new List<SiteCandidate>();
            foreach (var lot in OpenLots(current))
            {
                if (lot == null || lot.Street.Length == 0 || lot.Number <= 0)
                {
                    continue;
                }

                rows.Add(new SiteCandidate(
                    lot.Street,
                    lot.Number,
                    familyOnStreet: 0,
                    traffic: 20m,
                    dailyRent: lot.DailyRent,
                    cost: 0m));
            }

            return rows;
        }

        private static decimal DayTotal(GameInstance current, decimal business)
        {
            if (current.financialSummaries == null)
            {
                return business;
            }

            foreach (var summary in current.financialSummaries)
            {
                if (summary != null && summary.dayNumber == current.Day - 1)
                {
                    return ToMoney(summary.totalProfit);
                }
            }

            return business;
        }

        private static int ReadSpeed()
        {
            var clock = UnityEngine.Object.FindObjectOfType<GameSpeedController>();
            if (clock == null)
            {
                return TopBar.Clock(paused: false, rate: null);
            }

            var now = clock.playerGameSpeed;
            var paused = clock.Paused || now.paused;
            int? rate = paused ? null : System.Convert.ToInt32(now.speed);
            return TopBar.Clock(paused, rate);
        }

        public static decimal Operating(GameInstance? current)
        {
            return ReadCost(current).Daily;
        }

        private static AuditGuard Guard(GameInstance current)
        {
            return new AuditGuard(
                Autopilot.Enabled,
                Autopilot.DryRun,
                Cash(current),
                Operating(current),
                truckCost: 0,
                LoadLog.PinnedBuild);
        }

        private static CostParts ReadCost(GameInstance? current)
        {
            if (current == null)
            {
                return DailyCost.Read(null, 0, 0m, 0m);
            }

            return DailyCost.Read(
                Days(current),
                current.Day,
                YesterdayAmount(current, summary => summary.totalLoanExpenses),
                truckCost: 0m,
                YesterdayAmount(current, summary => summary.totalUnassignedStaffWages));
        }

        private static decimal Cash(GameInstance current)
        {
            return ToMoney(Helpers.PlayerHelper.GetPersonalWealth().cash);
        }

        private static decimal YesterdayAmount(GameInstance current, Func<FinancialSummary, float> read)
        {
            if (current.financialSummaries == null)
            {
                return 0m;
            }

            var yesterday = current.Day - 1;
            foreach (var summary in current.financialSummaries)
            {
                if (summary != null && summary.dayNumber == yesterday)
                {
                    return (decimal)read(summary);
                }
            }

            return 0m;
        }

        private static decimal ToMoney(object value)
        {
            return System.Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }

        private static string Place(Address address)
        {
            if (address == null)
            {
                return "";
            }

            var street = address.streetName ?? "";
            return street + " " + address.streetNumber.ToString(CultureInfo.InvariantCulture);
        }
    }
}
