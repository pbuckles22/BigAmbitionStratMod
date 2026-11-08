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
            watch.Tick(
                localAppData,
                current,
                current.Day,
                GameApi.Live(),
                overflow.Homes,
                arranged.Drains,
                overflow.Claims);
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
            var world = new AuditWorld(
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
                new string[0],
                new string[0]);
            var guard = new AuditGuard(
                Autopilot.Enabled,
                Autopilot.DryRun,
                cash: 0m,
                dailyCost: 0m,
                truckCost: 0,
                LoadLog.PinnedBuild);
            Audit.Check(localAppData, current.Day, GameApi.Live(), world, guard, LiveHeal);
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
                chosen = Starter.Choose(StarterBridge.Offers(registration), alreadyOpen: false, GameApi.Live());
            }
            catch (System.Exception)
            {
                watch.Mark(current);
                return Starter.Miss(localAppData, Starter.Choose(null, false, null), rented, current.Day);
            }

            if (!chosen.Loaded)
            {
                watch.Mark(current);
                return Starter.Miss(localAppData, chosen, rented, current.Day);
            }

            watch.Mark(current);
            if (!StarterBridge.Live(registration, chosen))
            {
                return Starter.Miss(localAppData, chosen, rented, current.Day);
            }

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
                return null;
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
            if (!WholesalerBridge.Live(signed, chosen.BusinessType))
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
            if (current == null || !watch.Due(current) || watch.Finished(current))
            {
                return null;
            }

            if (!HasLoan(localAppData))
            {
                return null;
            }

            if (watch.Waiting(current.Day))
            {
                return null;
            }

            if (watch.Arrive(current.Day))
            {
                if (watch.Completed >= TimeAdvance.WeekDays)
                {
                    watch.Mark(current);
                }

                return "";
            }

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

            var advanced = TimeAdvance.Skip(needs, alreadyAdvanced: false, GameApi.Live());
            if (!advanced.Advanced || !TimeAdvanceBridge.Skip(advanced))
            {
                watch.Mark(current);
                return "";
            }

            watch.WaitFor(current.Day + 1);
            return TimeAdvance.RecordSkip(localAppData, advanced, current.Day);
        }

        private static bool HasLoan(string localAppData)
        {
            var path = LoadLog.CeoLogPath(localAppData);
            if (!File.Exists(path))
            {
                return false;
            }

            foreach (var line in File.ReadAllLines(path))
            {
                if (line.StartsWith("loan.taken ", System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool LastContract(string localAppData, out string street, out int number, out bool skipped)
        {
            return LastNamed(localAppData, "contract.signed ", out street, out number, out skipped);
        }

        private static bool LastCashier(string localAppData, out string street, out int number, out bool skipped)
        {
            return LastNamed(localAppData, "hire.cashier ", out street, out number, out skipped);
        }

        private static bool LastNamed(
            string localAppData,
            string prefix,
            out string street,
            out int number,
            out bool skipped)
        {
            street = "";
            number = 0;
            skipped = false;
            var path = LoadLog.CeoLogPath(localAppData);
            if (!File.Exists(path))
            {
                return false;
            }

            var found = false;
            foreach (var line in File.ReadAllLines(path))
            {
                if (!line.StartsWith(prefix, System.StringComparison.Ordinal))
                {
                    continue;
                }

                var streetAt = line.IndexOf(" street=", System.StringComparison.Ordinal);
                var numberAt = line.IndexOf(" number=", System.StringComparison.Ordinal);
                if (streetAt < 0 || numberAt <= streetAt)
                {
                    continue;
                }

                var parsedStreet = line.Substring(streetAt + " street=".Length, numberAt - (streetAt + " street=".Length));
                var rest = line.Substring(numberAt + " number=".Length);
                var stop = rest.IndexOf(' ');
                var token = stop < 0 ? rest : rest.Substring(0, stop);
                if (parsedStreet.Length == 0
                    || !int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedNumber)
                    || parsedNumber <= 0)
                {
                    continue;
                }

                street = parsedStreet;
                number = parsedNumber;
                skipped = false;
                found = true;
            }

            return found;
        }

        private static bool LastOps(string localAppData, out string street, out int number, out bool skipped)
        {
            street = "";
            number = 0;
            skipped = false;
            var path = LoadLog.CeoLogPath(localAppData);
            if (!File.Exists(path))
            {
                return false;
            }

            var found = false;
            foreach (var line in File.ReadAllLines(path))
            {
                if (line.StartsWith("ops.skipped ", System.StringComparison.Ordinal))
                {
                    street = "";
                    number = 0;
                    skipped = true;
                    found = true;
                    continue;
                }

                if (!line.StartsWith("ops.assigned ", System.StringComparison.Ordinal))
                {
                    continue;
                }

                var streetAt = line.IndexOf(" street=", System.StringComparison.Ordinal);
                var numberAt = line.IndexOf(" number=", System.StringComparison.Ordinal);
                if (streetAt < 0 || numberAt <= streetAt)
                {
                    continue;
                }

                var parsedStreet = line.Substring(streetAt + " street=".Length, numberAt - (streetAt + " street=".Length));
                var rest = line.Substring(numberAt + " number=".Length);
                var stop = rest.IndexOf(' ');
                var token = stop < 0 ? rest : rest.Substring(0, stop);
                if (parsedStreet.Length == 0
                    || !int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedNumber)
                    || parsedNumber <= 0)
                {
                    continue;
                }

                street = parsedStreet;
                number = parsedNumber;
                skipped = false;
                found = true;
            }

            return found;
        }

        private static bool LastLayout(string localAppData, out string street, out int number, out bool skipped)
        {
            street = "";
            number = 0;
            skipped = false;
            var path = LoadLog.CeoLogPath(localAppData);
            if (!File.Exists(path))
            {
                return false;
            }

            var found = false;
            foreach (var line in File.ReadAllLines(path))
            {
                if (line.StartsWith("layout.skipped ", System.StringComparison.Ordinal))
                {
                    street = "";
                    number = 0;
                    skipped = true;
                    found = true;
                    continue;
                }

                if (!line.StartsWith("layout.loaded ", System.StringComparison.Ordinal))
                {
                    continue;
                }

                var streetAt = line.IndexOf(" street=", System.StringComparison.Ordinal);
                var numberAt = line.IndexOf(" number=", System.StringComparison.Ordinal);
                if (streetAt < 0 || numberAt <= streetAt)
                {
                    continue;
                }

                var parsedStreet = line.Substring(streetAt + " street=".Length, numberAt - (streetAt + " street=".Length));
                var rest = line.Substring(numberAt + " number=".Length);
                var stop = rest.IndexOf(' ');
                var token = stop < 0 ? rest : rest.Substring(0, stop);
                if (parsedStreet.Length == 0
                    || !int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedNumber)
                    || parsedNumber <= 0)
                {
                    continue;
                }

                street = parsedStreet;
                number = parsedNumber;
                skipped = false;
                found = true;
            }

            return found;
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
            street = "";
            number = 0;
            var path = LoadLog.CeoLogPath(localAppData);
            if (!File.Exists(path))
            {
                return false;
            }

            var found = false;
            foreach (var line in File.ReadAllLines(path))
            {
                if (!line.StartsWith("building.rented ", System.StringComparison.Ordinal))
                {
                    continue;
                }

                var streetAt = line.IndexOf(" street=", System.StringComparison.Ordinal);
                var numberAt = line.IndexOf(" number=", System.StringComparison.Ordinal);
                if (streetAt < 0 || numberAt <= streetAt)
                {
                    continue;
                }

                var parsedStreet = line.Substring(streetAt + " street=".Length, numberAt - (streetAt + " street=".Length));
                var rest = line.Substring(numberAt + " number=".Length);
                var stop = rest.IndexOf(' ');
                var token = stop < 0 ? rest : rest.Substring(0, stop);
                if (parsedStreet.Length == 0
                    || !int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedNumber)
                    || parsedNumber <= 0)
                {
                    continue;
                }

                street = parsedStreet;
                number = parsedNumber;
                found = true;
            }

            return found;
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

            Telemetry.Record(
                localAppData,
                HudLine(),
                Health.Latest(localAppData),
                Controls.Row(Autopilot.Enabled, Autopilot.DryRun));
        }

        private static HealFate LiveHeal(AuditHeal heal)
        {
            if (heal == null || heal.Kind != "connect")
            {
                return HealFate.Skip;
            }

            return AuditBridge.Live(heal) ? HealFate.Done : HealFate.Failed;
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
