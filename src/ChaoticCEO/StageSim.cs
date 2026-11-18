using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ChaoticCEO
{
    public sealed class StageSimCatalog
    {
        public StageSimCatalog(
            IReadOnlyList<OpenCandidate>? opens,
            IReadOnlyList<SiteCandidate>? sites,
            IReadOnlyList<LoanOffer>? loans = null,
            IReadOnlyList<EstateCandidate>? estates = null,
            string? item = null,
            string? warehouseName = null,
            decimal truckCost = 1000m)
        {
            Opens = opens ?? Array.Empty<OpenCandidate>();
            Sites = sites ?? Array.Empty<SiteCandidate>();
            Loans = loans ?? Array.Empty<LoanOffer>();
            Estates = estates ?? Array.Empty<EstateCandidate>();
            Item = item ?? "ba:itemname_cheapgift";
            WarehouseName = warehouseName ?? "W1";
            TruckCost = truckCost < 0m ? 0m : truckCost;
        }

        public IReadOnlyList<OpenCandidate> Opens { get; }

        public IReadOnlyList<SiteCandidate> Sites { get; }

        public IReadOnlyList<LoanOffer> Loans { get; }

        public IReadOnlyList<EstateCandidate> Estates { get; }

        public string Item { get; }

        public string WarehouseName { get; }

        public decimal TruckCost { get; }
    }

    public sealed class StageSimTrial
    {
        public StageSimTrial(
            decimal netWorth,
            decimal cash,
            int stage,
            int goodsStores,
            int suppliedStores,
            int warehouses,
            int emptySlots,
            int plans,
            int closed = 0,
            string? lastClose = null,
            decimal dailyCost = 0m)
        {
            NetWorth = netWorth;
            Cash = cash;
            Stage = stage;
            GoodsStores = goodsStores;
            SuppliedStores = suppliedStores;
            Warehouses = warehouses;
            EmptySlots = emptySlots;
            Plans = plans;
            Closed = closed;
            LastClose = lastClose ?? "";
            DailyCost = dailyCost;
        }

        public decimal NetWorth { get; }

        public decimal Cash { get; }

        public int Stage { get; }

        public int GoodsStores { get; }

        public int SuppliedStores { get; }

        public int Warehouses { get; }

        public int EmptySlots { get; }

        public int Plans { get; }

        public int Closed { get; }

        public string LastClose { get; }

        public decimal DailyCost { get; }
    }

    public sealed class StageDay
    {
        public StageDay(
            int today,
            decimal cash,
            IReadOnlyList<FinancialDay>? days,
            string? kind,
            int suppliedDays,
            int staffedDays,
            int redProfitDays,
            bool supplied,
            bool staffed,
            decimal billedCurrentTax = 0m,
            decimal billedBackTax = 0m,
            int daysUntilDue = 0,
            RunFacts? run = null,
            int stage = 1,
            string? store = null,
            string? item = null,
            bool askSupply = false,
            IReadOnlyList<WarehouseHold>? holds = null,
            IReadOnlyList<DeliveryStop>? stops = null,
            decimal loanPayments = 0m,
            decimal truckCost = 0m)
        {
            Today = today;
            Cash = cash;
            Days = days ?? Array.Empty<FinancialDay>();
            Kind = kind ?? "";
            SuppliedDays = suppliedDays;
            StaffedDays = staffedDays;
            RedProfitDays = redProfitDays;
            Supplied = supplied;
            Staffed = staffed;
            BilledCurrentTax = billedCurrentTax;
            BilledBackTax = billedBackTax;
            DaysUntilDue = daysUntilDue;
            Run = run;
            Stage = stage;
            Store = store ?? "";
            Item = item ?? "";
            AskSupply = askSupply;
            Holds = holds ?? Array.Empty<WarehouseHold>();
            Stops = stops ?? Array.Empty<DeliveryStop>();
            LoanPayments = loanPayments;
            TruckCost = truckCost;
        }

        public int Today { get; }

        public decimal Cash { get; }

        public IReadOnlyList<FinancialDay> Days { get; }

        public string Kind { get; }

        public int SuppliedDays { get; }

        public int StaffedDays { get; }

        public int RedProfitDays { get; }

        public bool Supplied { get; }

        public bool Staffed { get; }

        public decimal BilledCurrentTax { get; }

        public decimal BilledBackTax { get; }

        public int DaysUntilDue { get; }

        public RunFacts? Run { get; }

        public int Stage { get; }

        public string Store { get; }

        public string Item { get; }

        public bool AskSupply { get; }

        public IReadOnlyList<WarehouseHold> Holds { get; }

        public IReadOnlyList<DeliveryStop> Stops { get; }

        public decimal LoanPayments { get; }

        public decimal TruckCost { get; }
    }

    public sealed class StageMorning
    {
        public StageMorning(
            PlaybookStep step,
            decimal dailyCost,
            ClosePick close,
            TaxPaymentPick tax,
            SupplyPick supply,
            RunPick run)
        {
            Step = step ?? PlaybookStep.None();
            DailyCost = dailyCost;
            Close = close ?? ClosePick.None();
            Tax = tax ?? TaxPaymentPick.None();
            Supply = supply ?? SupplyPick.None();
            Run = run ?? RunPick.None();
        }

        public PlaybookStep Step { get; }

        public decimal DailyCost { get; }

        public ClosePick Close { get; }

        public TaxPaymentPick Tax { get; }

        public SupplyPick Supply { get; }

        public RunPick Run { get; }
    }

    public sealed class StageSimResult
    {
        public StageSimResult(
            int horizonDays,
            IReadOnlyList<decimal> netWorths,
            decimal median,
            decimal worst,
            decimal best,
            bool winAtMedian,
            bool winAtWorst,
            StageThresholds thresholds)
        {
            HorizonDays = horizonDays;
            NetWorths = netWorths ?? Array.Empty<decimal>();
            Median = median;
            Worst = worst;
            Best = best;
            WinAtMedian = winAtMedian;
            WinAtWorst = winAtWorst;
            Thresholds = thresholds ?? StageThresholds.Starting();
        }

        public int HorizonDays { get; }

        public IReadOnlyList<decimal> NetWorths { get; }

        public int Trials => NetWorths.Count;

        public decimal Median { get; }

        public decimal Worst { get; }

        public decimal Best { get; }

        public bool WinAtMedian { get; }

        public bool WinAtWorst { get; }

        public StageThresholds Thresholds { get; }

        public string Name { get; private set; } = "";

        public StageSimResult WithName(string name)
        {
            Name = name ?? "";
            return this;
        }

        public static StageSimResult Empty(int horizonDays, StageThresholds? thresholds = null)
        {
            var gates = thresholds ?? StageThresholds.Starting();
            var days = horizonDays > 0 ? horizonDays : gates.SimHorizonDays;
            return new StageSimResult(days, Array.Empty<decimal>(), 0m, 0m, 0m, false, false, gates);
        }
    }

    public sealed class StageThresholdOption
    {
        public StageThresholdOption(string name, StageThresholds thresholds)
        {
            Name = name ?? "";
            Thresholds = thresholds ?? StageThresholds.Starting();
        }

        public string Name { get; }

        public StageThresholds Thresholds { get; }
    }

    public static class StageSim
    {
        public static StageSimTrial Trial(
            decimal startCash,
            decimal startNetWorth,
            StageSimCatalog? catalog,
            int seed,
            StageThresholds? thresholds = null,
            int horizonDays = -1)
        {
            var gates = thresholds ?? StageThresholds.Starting();
            var days = horizonDays > 0 ? horizonDays : gates.SimHorizonDays;
            if (catalog == null || days <= 0)
            {
                return new StageSimTrial(startNetWorth, startCash, 1, 0, 0, 0, 0, 0);
            }

            var state = new World(startCash, startNetWorth, catalog, gates);
            for (var day = 0; day < days; day++)
            {
                state.Step(day, seed);
            }

            return state.Snapshot();
        }

        public static StageSimResult Run(
            decimal startCash,
            decimal startNetWorth,
            StageSimCatalog? catalog,
            IReadOnlyList<int>? seeds,
            StageThresholds? thresholds = null,
            int horizonDays = -1)
        {
            var gates = thresholds ?? StageThresholds.Starting();
            var days = horizonDays > 0 ? horizonDays : gates.SimHorizonDays;
            if (catalog == null || seeds == null || seeds.Count == 0 || days <= 0)
            {
                return StageSimResult.Empty(days, gates);
            }

            var worths = seeds
                .Select(seed => Trial(startCash, startNetWorth, catalog, seed, gates, days).NetWorth)
                .OrderBy(worth => worth)
                .ToList();
            var median = worths[worths.Count / 2];
            var worst = worths[0];
            var best = worths[worths.Count - 1];
            return new StageSimResult(
                days,
                worths,
                median,
                worst,
                best,
                median >= gates.WinNetWorth,
                worst >= gates.WinNetWorth,
                gates);
        }

        public static StageSimResult Tune(
            IReadOnlyList<StageThresholdOption>? options,
            decimal startCash,
            decimal startNetWorth,
            StageSimCatalog? catalog,
            IReadOnlyList<int>? seeds,
            int horizonDays = -1)
        {
            if (options == null || options.Count == 0 || catalog == null || seeds == null || seeds.Count == 0)
            {
                return StageSimResult.Empty(horizonDays > 0 ? horizonDays : Noise.SimHorizonDays);
            }

            StageSimResult? winner = null;
            foreach (var option in options)
            {
                if (option == null || option.Name.Length == 0)
                {
                    continue;
                }

                var run = Run(startCash, startNetWorth, catalog, seeds, option.Thresholds, horizonDays);
                if (run.Trials == 0)
                {
                    continue;
                }

                if (!Policy.Acceptable(run.Worst, run.Median))
                {
                    continue;
                }

                if (winner == null
                    || run.WinAtMedian && !winner.WinAtMedian
                    || run.WinAtMedian == winner.WinAtMedian && run.Median > winner.Median)
                {
                    winner = run.WithName(option.Name);
                }
            }

            return winner ?? StageSimResult.Empty(horizonDays > 0 ? horizonDays : Noise.SimHorizonDays);
        }

        public static StageMorning Morning(
            StageDay? day,
            OpenPick? open = null,
            SitePick? where = null,
            LayoutPick? layout = null,
            PricePick? prices = null,
            TruckPick? trucks = null,
            WarehousePick? warehouses = null,
            ManagerPick? managers = null,
            MoneyPick? money = null,
            EstatePick? estate = null)
        {
            if (day == null)
            {
                return new StageMorning(
                    PlaybookStep.None(),
                    0m,
                    ClosePick.None(),
                    TaxPaymentPick.None(),
                    SupplyPick.None(),
                    RunPick.None());
            }

            var cost = DailyCost.From(day.Days, day.Today, day.LoanPayments, day.TruckCost);
            var close = Close.Decide(
                day.Kind,
                day.SuppliedDays,
                day.StaffedDays,
                day.RedProfitDays,
                day.Supplied,
                day.Staffed);
            var tax = TaxPolicy.DecidePayment(
                day.Cash,
                cost,
                day.BilledCurrentTax,
                day.BilledBackTax,
                day.DaysUntilDue);
            var run = global::ChaoticCEO.Run.Decide(day.Run);
            var supply = day.AskSupply
                ? Supply.Decide(day.Stage, day.Store, day.Item, day.Holds, day.Stops)
                : SupplyPick.None();
            var step = Playbook.Decide(
                open,
                where,
                layout,
                prices,
                supply,
                trucks,
                warehouses,
                managers,
                money,
                estate,
                close,
                run,
                tax);
            return new StageMorning(step, cost, close, tax, supply, run);
        }

        private sealed class Store
        {
            public Store(string street, int number, string type, string family, string kind, decimal dailyProfit, decimal rent)
            {
                Street = street;
                Number = number;
                Type = type;
                Family = family;
                Kind = kind;
                DailyProfit = dailyProfit;
                Rent = rent;
                Key = street + " " + number.ToString(CultureInfo.InvariantCulture);
            }

            public string Street { get; }

            public int Number { get; }

            public string Type { get; }

            public string Family { get; }

            public string Kind { get; }

            public decimal DailyProfit { get; }

            public decimal Rent { get; }

            public string Key { get; }

            public int SuppliedDays { get; set; }

            public int StaffedDays { get; set; }

            public int RedDays { get; set; }

            public bool Staffed { get; set; }

            public bool Supplied { get; set; }

            public bool Contracted { get; set; }

            public bool LayoutDone { get; set; }

            public bool Priced { get; set; }
        }

        private sealed class World
        {
            private readonly StageSimCatalog _catalog;
            private readonly StageThresholds _gates;
            private readonly List<Store> _stores = new List<Store>();
            private readonly HashSet<string> _usedSites = new HashSet<string>(StringComparer.Ordinal);
            private readonly List<WarehouseHold> _holds = new List<WarehouseHold>();
            private readonly List<DeliveryStop> _stops = new List<DeliveryStop>();
            private readonly List<GarageSlot> _slots = new List<GarageSlot>();
            private readonly List<WarehouseLevel> _homes = new List<WarehouseLevel>();
            private readonly List<FinancialDay> _ledger = new List<FinancialDay>();
            private string _pendingOpen = "";
            private string _pendingFamily = "";
            private string _pendingKind = "";
            private decimal _pendingProfit;
            private bool _pricingPlan;
            private bool _loanTaken;
            private decimal _cash;
            private decimal _netWorth;
            private int _closed;
            private string _lastClose = "";
            private decimal _dailyCost;
            private Store? _focus;

            public World(decimal cash, decimal netWorth, StageSimCatalog catalog, StageThresholds gates)
            {
                _cash = cash;
                _netWorth = netWorth;
                _catalog = catalog;
                _gates = gates;
                _homes.Add(new WarehouseLevel(catalog.WarehouseName, "", 0, 0, false, shelves: 10));
            }

            public void Step(int day, int seed)
            {
                var today = day + 1;
                TickStores();
                _focus = FocusStore();
                var stage = CurrentStage(day);
                var dailyCost = DailyCost.From(_ledger, today, 0m, 0m);
                _dailyCost = dailyCost;
                var reserve = dailyCost * _gates.ReserveDays;
                var truckLoad = TruckLoadFraction();
                var open = Open.Decide(stage, OpenCandidates(), truckLoad);
                var where = _pendingOpen.Length > 0
                    ? Where.Decide(AvailableSites(), _cash, reserve)
                    : SitePick.None();
                var layoutStore = _stores.FirstOrDefault(store => !store.LayoutDone);
                var layout = layoutStore == null
                    ? LayoutPick.None()
                    : Layout.Decide(
                        stage,
                        layoutStore.Type,
                        "small",
                        Array.Empty<OwnedLayout>(),
                        Starter.StageOne());
                var priceStore = _stores.FirstOrDefault(store => store.LayoutDone && !store.Priced);
                var price = priceStore == null
                    ? PricePick.None()
                    : Prices.Decide(
                        _catalog.Item,
                        currentPrice: 10m,
                        defaultPrice: 10m,
                        wholesale: 4m,
                        priorUnits: 8,
                        currentUnits: 8,
                        satisfaction: _gates.PriceSatisfied,
                        trials: new[]
                        {
                            new ItemSoldPerPriceEntry(_catalog.Item, 12f, 8),
                        });
                var delivering = _holds
                    .Select(hold => hold.Warehouse)
                    .Where(name => name.Length > 0)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                var trucks = Trucks.Decide(_slots, delivering, _cash, reserve, _catalog.TruckCost);
                var family = _stores.Count > 0 ? _stores[0].Family : "";
                var warehouses = Warehouses.Decide(
                    family,
                    enteringFamily: family.Length > 0 && !_holds.Any(hold => hold.Warehouse.Length > 0),
                    hasStockWithoutHome: false,
                    _homes,
                    trucksBusy: truckLoad > _gates.TruckBusy);
                IReadOnlyList<string> warehouseNames = delivering.Count > 0
                    ? delivering
                    : _holds
                        .Select(hold => hold.Warehouse)
                        .Where(name => name.Length > 0)
                        .Distinct(StringComparer.Ordinal)
                        .ToList();
                var managers = Managers.Decide(
                    stage,
                    warehouseNames,
                    employees: _stores.Count * 2,
                    openRoles: Math.Max(0, 2 - (_pricingPlan ? 1 : 0)),
                    seats: Array.Empty<ManagerSeat>(),
                    pricingPlanExists: _pricingPlan);
                var money = Money.Decide(
                    stage,
                    _cash,
                    dailyCost,
                    projectedDailyProfit: _stores.Sum(store => store.DailyProfit),
                    offers: _loanTaken ? Array.Empty<LoanOffer>() : _catalog.Loans,
                    loan: null,
                    bestPaybackRate: 0.1m);
                var estate = RealEstate.Decide(stage, _catalog.Estates);
                var morning = Morning(
                    new StageDay(
                        today,
                        _cash,
                        _ledger,
                        _focus?.Kind ?? "",
                        _focus?.SuppliedDays ?? 0,
                        _focus?.StaffedDays ?? 0,
                        _focus?.RedDays ?? 0,
                        _focus?.Supplied ?? false,
                        _focus?.Staffed ?? false,
                        stage: stage,
                        store: _focus?.Key ?? "",
                        item: _catalog.Item,
                        askSupply: _focus != null && !_focus.Supplied,
                        holds: _holds,
                        stops: _stops),
                    open,
                    where,
                    layout,
                    price,
                    trucks,
                    warehouses,
                    managers,
                    money,
                    estate);
                _dailyCost = morning.DailyCost;
                Apply(morning, open, where, layout, price, trucks, warehouses, managers, money, estate, reserve);
                Earn(seed, day);
                Remember(today);
            }

            public StageSimTrial Snapshot()
            {
                var stage = CurrentStage(0);
                return new StageSimTrial(
                    _netWorth,
                    _cash,
                    stage,
                    _stores.Count,
                    _stores.Count(store => store.Supplied),
                    WarehouseCount(),
                    EmptySlots(),
                    PlanCount(),
                    _closed,
                    _lastClose,
                    _dailyCost);
            }

            private void Apply(
                StageMorning morning,
                OpenPick open,
                SitePick where,
                LayoutPick layout,
                PricePick price,
                TruckPick trucks,
                WarehousePick warehouses,
                ManagerPick managers,
                MoneyPick money,
                EstatePick estate,
                decimal reserve)
            {
                var step = morning.Step;
                var supply = morning.Supply;
                if (!step.Chosen)
                {
                    return;
                }

                switch (step.Kind)
                {
                    case "open":
                        if (open.Chosen)
                        {
                            var candidate = OpenCandidates().FirstOrDefault(item => item.Type == open.Type);
                            _pendingOpen = open.Type;
                            _pendingFamily = candidate?.Family ?? "specialty";
                            _pendingKind = candidate?.Kind ?? "goods";
                            _pendingProfit = candidate?.DailyProfit ?? 0m;
                        }

                        break;
                    case "where":
                        if (where.Chosen && _pendingOpen.Length > 0)
                        {
                            var site = AvailableSites()
                                .FirstOrDefault(item => item.Street == where.Street && item.Number == where.Number);
                            if (site != null && _cash >= site.Cost + reserve)
                            {
                                _cash -= site.Cost;
                                _netWorth -= site.Cost;
                                _stores.Add(new Store(
                                    site.Street,
                                    site.Number,
                                    _pendingOpen,
                                    _pendingFamily,
                                    _pendingKind.Length > 0 ? _pendingKind : "goods",
                                    _pendingProfit,
                                    site.DailyRent));
                                _usedSites.Add(SiteKey(site));
                                _pendingOpen = "";
                                _pendingFamily = "";
                                _pendingKind = "";
                                _pendingProfit = 0m;
                            }
                        }

                        break;
                    case "layout":
                        if (layout.Chosen)
                        {
                            var target = _stores.FirstOrDefault(store => !store.LayoutDone);
                            if (target != null)
                            {
                                target.LayoutDone = true;
                            }
                        }

                        break;
                    case "prices":
                        if (price.Chosen)
                        {
                            var target = _stores.FirstOrDefault(store => store.LayoutDone && !store.Priced);
                            if (target != null)
                            {
                                target.Priced = true;
                            }
                        }

                        break;
                    case "supply":
                        if (supply.Chosen)
                        {
                            var target = _stores.FirstOrDefault(store => !store.Supplied);
                            if (target == null)
                            {
                                break;
                            }

                            if (supply.Source == "warehouse" && supply.Warehouse.Length > 0)
                            {
                                target.Supplied = true;
                                target.Contracted = true;
                                _stops.Add(new DeliveryStop(
                                    supply.Warehouse,
                                    target.Key,
                                    _catalog.Item,
                                    day: null,
                                    truck: ""));
                            }
                            else if (supply.Source == "wholesaler")
                            {
                                target.Supplied = true;
                                target.Contracted = true;
                            }
                        }

                        break;
                    case "close":
                        ApplyClose(supply);
                        break;
                    case "tax":
                        if (morning.Tax.Chosen && morning.Tax.Amount > 0m && _cash >= morning.Tax.Amount)
                        {
                            _cash -= morning.Tax.Amount;
                            _netWorth -= morning.Tax.Amount;
                        }

                        break;
                    case "trucks":
                        if (trucks.Chosen && _cash >= reserve + _catalog.TruckCost)
                        {
                            for (var i = 0; i < _slots.Count; i++)
                            {
                                var slot = _slots[i];
                                if (!slot.Filled
                                    && slot.Warehouse == trucks.Warehouse
                                    && slot.Index == trucks.Slot)
                                {
                                    _slots[i] = new GarageSlot(slot.Warehouse, slot.Index, filled: true);
                                    _cash -= _catalog.TruckCost;
                                    _netWorth -= _catalog.TruckCost;
                                    break;
                                }
                            }
                        }

                        break;
                    case "warehouses":
                        if (warehouses.Chosen && warehouses.Action == "home")
                        {
                            for (var i = 0; i < _homes.Count; i++)
                            {
                                if (_homes[i].Warehouse == warehouses.Warehouse && _homes[i].Family.Length == 0)
                                {
                                    _homes[i] = new WarehouseLevel(
                                        warehouses.Warehouse,
                                        warehouses.Family,
                                        onHand: 100,
                                        daily: 10,
                                        ordered: false,
                                        shelves: _homes[i].Shelves);
                                    _holds.Add(new WarehouseHold(
                                        warehouses.Warehouse,
                                        "ba:street_warehouse",
                                        1,
                                        _catalog.Item,
                                        100));
                                    _slots.Add(new GarageSlot(warehouses.Warehouse, 1, filled: false));
                                    _slots.Add(new GarageSlot(warehouses.Warehouse, 2, filled: false));
                                    break;
                                }
                            }
                        }

                        break;
                    case "managers":
                        if (managers.Chosen)
                        {
                            _pricingPlan = true;
                        }

                        break;
                    case "money":
                        if (money.Chosen && money.Action == "loan" && money.Amount > 0m)
                        {
                            _cash += money.Amount;
                            _netWorth += money.Amount;
                            _loanTaken = true;
                        }

                        break;
                    case "realestate":
                        if (estate.Chosen)
                        {
                            var building = _catalog.Estates.FirstOrDefault(
                                item => item != null
                                    && item.Street == estate.Street
                                    && item.Number == estate.Number);
                            if (building != null && _cash >= building.Price + reserve)
                            {
                                _cash -= building.Price;
                                _netWorth -= building.Price;
                            }
                        }

                        break;
                }
            }

            private void Earn(int seed, int day)
            {
                var demand = 1m + (2m * Unit(seed, day, 1) - 1m) * _gates.DemandNoise;
                var rival = Unit(seed, day, 2) * _gates.RivalNoise;
                if (demand < 0m)
                {
                    demand = 0m;
                }

                if (rival < 0m)
                {
                    rival = 0m;
                }

                if (rival > 1m)
                {
                    rival = 1m;
                }

                decimal profit = 0m;
                foreach (var store in _stores)
                {
                    profit += store.DailyProfit * demand * (1m - rival);
                }

                _cash += profit;
                _netWorth += profit;
            }

            private int CurrentStage(int day)
            {
                return Stage.Decide(
                    day,
                    _cash,
                    _stores.Count,
                    _stores.Count(store => store.Supplied),
                    WarehouseCount(),
                    EmptySlots(),
                    PlanCount(),
                    _gates);
            }

            private List<OpenCandidate> OpenCandidates()
            {
                if (_pendingOpen.Length > 0)
                {
                    return new List<OpenCandidate>();
                }

                var stage = CurrentStage(0);
                if (stage <= 1 && _stores.Count >= _gates.Stage2Stores)
                {
                    return new List<OpenCandidate>();
                }

                var count = Math.Min(_catalog.Opens.Count, _catalog.Sites.Count);
                var list = new List<OpenCandidate>();
                for (var i = 0; i < count; i++)
                {
                    var site = _catalog.Sites[i];
                    if (_usedSites.Contains(SiteKey(site)))
                    {
                        continue;
                    }

                    var source = _catalog.Opens[i];
                    if (source == null || source.Type.Length == 0)
                    {
                        continue;
                    }

                    var owned = _stores.Count(store => store.Family == source.Family);
                    var hasHome = _holds.Count > 0 && _homes.Any(home => home.Family == source.Family);
                    list.Add(new OpenCandidate(
                        source.Type,
                        source.Kind,
                        source.DailyProfit,
                        source.WholesalerSells,
                        source.Family,
                        owned,
                        hasHome));
                }

                return list;
            }

            private List<SiteCandidate> AvailableSites()
            {
                var count = Math.Min(_catalog.Opens.Count, _catalog.Sites.Count);
                var list = new List<SiteCandidate>();
                for (var i = 0; i < count; i++)
                {
                    var site = _catalog.Sites[i];
                    if (site == null || site.Street.Length == 0 || _usedSites.Contains(SiteKey(site)))
                    {
                        continue;
                    }

                    list.Add(site);
                }

                return list;
            }

            private static decimal OperatingProfit(Store store)
            {
                var net = store.DailyProfit - store.Rent;
                return net;
            }

            private void TickStores()
            {
                foreach (var store in _stores)
                {
                    if (store.Supplied)
                    {
                        store.SuppliedDays++;
                    }

                    if (store.Staffed)
                    {
                        store.StaffedDays++;
                    }

                    if (store.Staffed && OperatingProfit(store) < 0m)
                    {
                        store.RedDays++;
                    }
                }
            }

            private Store? FocusStore()
            {
                Store? connect = null;
                Store? hire = null;
                Store? shutdown = null;
                foreach (var store in _stores)
                {
                    var pick = Close.Decide(
                        store.Kind,
                        store.SuppliedDays,
                        store.StaffedDays,
                        store.RedDays,
                        store.Supplied,
                        store.Staffed);
                    if (!pick.Chosen)
                    {
                        continue;
                    }

                    if (pick.Action == "close")
                    {
                        shutdown = store;
                    }
                    else if (pick.Action == "hire" && hire == null)
                    {
                        hire = store;
                    }
                    else if (pick.Action == "connect" && connect == null)
                    {
                        connect = store;
                    }
                }

                return shutdown ?? hire ?? connect;
            }

            private void ApplyClose(SupplyPick supply)
            {
                if (_focus == null)
                {
                    return;
                }

                var pick = Close.Decide(
                    _focus.Kind,
                    _focus.SuppliedDays,
                    _focus.StaffedDays,
                    _focus.RedDays,
                    _focus.Supplied,
                    _focus.Staffed);
                if (!pick.Chosen)
                {
                    return;
                }

                if (pick.Action == "connect")
                {
                    if (supply.Chosen && supply.Source == "warehouse" && supply.Warehouse.Length > 0)
                    {
                        _focus.Supplied = true;
                        _focus.Contracted = true;
                        _stops.Add(new DeliveryStop(
                            supply.Warehouse,
                            _focus.Key,
                            _catalog.Item,
                            day: null,
                            truck: ""));
                    }
                    else
                    {
                        _focus.Supplied = true;
                        _focus.Contracted = true;
                    }

                    return;
                }

                if (pick.Action == "hire")
                {
                    _focus.Staffed = true;
                    return;
                }

                if (pick.Action == "close")
                {
                    _stores.Remove(_focus);
                    _closed++;
                    _lastClose = pick.Action;
                }
            }

            private void Remember(int today)
            {
                var rows = new List<StatementRow>();
                foreach (var store in _stores)
                {
                    rows.Add(new StatementRow(
                        store.Street,
                        store.Number,
                        0f,
                        0f,
                        0f,
                        (float)store.Rent,
                        0f,
                        0f,
                        (float)OperatingProfit(store)));
                }

                _ledger.Add(new FinancialDay(today, rows));
            }

            private decimal TruckLoadFraction()
            {
                if (_slots.Count == 0)
                {
                    return 0m;
                }

                var filled = _slots.Count(slot => slot.Filled);
                return (decimal)filled / _slots.Count;
            }

            private int WarehouseCount()
            {
                return _holds
                    .Select(hold => hold.Warehouse)
                    .Where(name => name.Length > 0)
                    .Distinct(StringComparer.Ordinal)
                    .Count();
            }

            private int EmptySlots()
            {
                return _slots.Count(slot => !slot.Filled);
            }

            private int PlanCount()
            {
                return _homes.Count(home => home.Family.Length > 0);
            }

            private static string SiteKey(SiteCandidate site)
            {
                return site.Street + " " + site.Number.ToString(CultureInfo.InvariantCulture);
            }

            private static decimal Unit(int seed, int day, int channel)
            {
                unchecked
                {
                    var x = (uint)seed * 747796405u + (uint)day * 2891336453u + (uint)channel * 2246822519u;
                    x ^= x >> 16;
                    x *= 2246822519u;
                    x ^= x >> 13;
                    return (x & 0xFFFFFFu) / 16777216m;
                }
            }
        }
    }
}
