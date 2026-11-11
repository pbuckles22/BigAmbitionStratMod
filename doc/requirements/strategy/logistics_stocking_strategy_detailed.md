# Logistics & Stocking Strategy — Stage 1 Door Delivery to Stage 4 Rebalancing (Exhaustive Engineering Spec)

Stage 1 line sizing in this note is superseded by [logistics_stocking_strategy_1_5x_buffer_2_open_day_moving_average.md](logistics_stocking_strategy_1_5x_buffer_2_open_day_moving_average.md). Use that note for burn, target stock, the line amount, and the delivery gap. Warehouse handover, importer prime, and residual cross-family supply below stay in force where they do not set a line amount.

## 1. Executive Logistics Path (Day 0 $\rightarrow$ Stage 4 Sequence)

* **Day 1 (Store #1 Initial Contract Seed):**
  * Gate contract creation on `StoreOpsBridge.HasItems(registration) == true` (never sign a contract on an unfurnished bare lot).
  * Sign one Wholesaler `DeliveryContract` (`Dialogs.WholesaleStoreManagerDialog.StartContract`) from `ba:street_firststreet 37` to Store #1 covering all $5$ `GiftShopWholesalers` items (`ba:itemname_cheapgift`, `ba:itemname_cheapflower`, `ba:itemname_sodacan`, `ba:itemname_paperbag`, `ba:itemname_umbrella`).
  * Size the Day-1 lines and every later morning from [logistics_stocking_strategy_1_5x_buffer_2_open_day_moving_average.md](logistics_stocking_strategy_1_5x_buffer_2_open_day_moving_average.md). A closed day does not push the truck.
* **Days 2+ (Daily Closed-Loop Reforecast on `day.rolled`):**
  * Every morning on `day.rolled`, run `WholesalerBridge.Maintain` for each store with an active `DeliveryContract`, using that same note.
  * `WholesalerBridge.Maintain` evaluates each contract against that store’s own `orderHistory` and `itemInstances`.
* **Multi-Store Stage 1 (Stores #2 and #3):**
  * Each new store $N \in \{2, 3\}$ receives its own `DeliveryContract` only after its `layout`, `ops`, and `capacity` fit-out steps complete.
  * `WholesalerBridge.Maintain` evaluates each `DeliveryContract` strictly against `contract.businessAddress`'s own `BuildingRegistration` (`orderHistory` and `itemInstances`), never copying Store #1's burn onto Store #2.
* **Stage 2 Entry (Single-Family Warehouse #1 + Importer Prime):**
  * At `cash >= Stage2Cash` ($\$150,000$) and `HealthyGoodsStores >= Stage2Stores` ($3$):
    1. Rent Warehouse #1 (`shelves > 0`) dedicated to the single primary family (`specialty`).
    2. Buy Freight Truck #1 + Driver #1 into Slot 1, then Truck #2 + Driver #2 into Slot 2 (`GarageSlots = 2`).
    3. Purchasing Agent orders **RefillDays** $= 7$ days of `specialty` family burn (`(7 * FamilyBurn) - WarehouseOnHand`) from an Importer (`ChangeAssignedWarehouse` + `StartOrder`), using the measured burn of owned wholesaler stores of that family for the initial prime order.
* **Stage 2 Zero-Downtime Handover (`StoreConnect`):**
  * Keep Store #1–3 Wholesaler `DeliveryContract` lines active while Warehouse #1 is priming.
  * Once Warehouse #1 physical stock covers $\ge$ **RefillDays** ($7$) of burn at initial Stage-2 handover ($\ge 1$ day of burn for subsequent single-store connects), `StoreConnect.Connect` adds the store to Warehouse #1's `LogisticsManagerPlan` (`AddDestination`) with per-item `ItemAmountTarget` $= \text{TargetStock}(s, i)$ and removes **only** the warehouse-held items (`cheapgift`, `cheapflower`, `umbrella`, and `paperbag` once stocked) from the store's `DeliveryContract`.
  * **Residual Cross-Family Rule:** Because `ba:itemname_sodacan` is `food` (`Advisor.CargoFamily("ba:itemname_sodacan") == "food"`), it is **never** placed in the `specialty` warehouse. Its line remains on the Gift Shop's Wholesaler `DeliveryContract` (and continues to be resized daily by `Maintain`) until a dedicated `food` warehouse opens in Stage 3.
* **Stage 3–4 (Multi-Warehouse Scaling, Truck Load Valve & Mixed-Warehouse Drain):**
  * Maintain $1$ retail family per warehouse and $2$ freight trucks per warehouse.
  * When filled trucks exceed **TruckBusy** $= 85\%$, `Open.Decide` pivots new openings to `service`/`office` businesses and `Warehouses.Decide` claims a 2nd warehouse for that family (`action="second", reason="trucks"`).
  * If a warehouse has `Ordered == true` and still falls under `RefillDays * Daily`, `Warehouses.Decide` claims a 2nd warehouse (`action="second", reason="short"`).
  * If a loaded save contains a mixed warehouse, `Restock.Arrange` keeps the majority family, emits `warehouse.drain` to route minority-family imports and store destinations to the minority family's home warehouse, and lets leftover minority stock sell down to $0$.

---

## 2. Build 3682 Data Model: How Fixtures, Cargo & Sales Live on `BuildingRegistration`

To implement shelf-headroom capping and measured burn without guessing unverified APIs, use the exact fields already referenced in `LiveDump.cs`:

1. **Physical Stock on Hand (`OnHand`):**
   * On `BuildingRegistration`, `building.itemInstances` (`Dictionary<string, ItemInstance>`) holds all placed furniture/fixtures in the building (`Product Panel`, `Shelf`, `Drinks Fridge`, `Flower Fridge`, `CashRegisterController`, `ClosedCardboardBox` in storage).
   * Each fixture `ItemInstance` has `item.cargoInstances` (`List<CargoInstance>`), and each `CargoInstance` has `box.itemName` (`string`), `box.amount` (`int`), and `box.nestedCargoInstances`.
   * `LiveDump.FixtureCargo(building)` (lines 865–904) already sums `box.amount` (and `nested.amount`) per `itemName` into an `IReadOnlyDictionary<string, int>` via `StoreHealth.CountCargoAmounts(rows)`.
2. **Fixture Storage Capacity (`FixtureCap`):**
   * Each display fixture in `GiftShopWholesalers` holds a finite number of units (a small Product Panel / Shelf / Fridge holds $100$ units of a single item; the Cash Register holds $200\text{–}250$ Paper Bags).
   * When reading `building.itemInstances.Values`, count how many display fixtures/slots carry `itemName` (or use **DefaultFixtureCap** $= 100$ per sellable item and **PaperBagFixtureCap** $= 200$ for `ba:itemname_paperbag` on a starter small store, scaling by fixture count when multiple shelves hold the same item).
3. **Completed Open-Day Unit Sales (`orderHistory`):**
   * `building.orderHistory` is a `List<OrderHistoryEntry>`, where each `entry` has `entry.dayNumber` (`int`) and `entry.itemSales` (`List<ItemSaleEntry>` with `sale.itemName` and `sale.amountSold`).
   * Filter `building.orderHistory` to completed days `entry.dayNumber < current.Day` where the store was scheduled open (or `entry.itemSales.Sum(s => s.amountSold) > 0`), take the last **BurnLookbackDays** $= 3$ open days, and average `sale.amountSold` per `itemName`.

---

## 3. Per-Stage Logistics Playbook (Stages 1–4)

| Stage | Enter Gate | Supply Mode | Sizing Formula | Cadence Rule | Daily Monitor Hook (`day.rolled`) | Exit Gate |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Stage 1** | Store $N$ has placed fixtures (`HasItems == true`) | Wholesaler `DeliveryContract` to store door | `LineAmount = clamp(TargetStock - OnHand, MinReplenishAmount, ShelfHeadroom)` | Pull `nextDeliveryDay = day + 1` if any item `OnHand < MeasuredBurn * 2` & `Headroom >= 10`; else `day + 2` | `WholesalerBridge.Maintain` updates `item.amount` (up or down) and `nextDeliveryDay` per store | $3$ Ready stores + $\$150\text{k}$ cash |
| **Stage 2** | `cash >= $150k` & `3` Ready stores | Single-family WH #1 (`specialty`) + residual wholesaler (`sodacan`) | **Importer:** `(7 * FamilyBurn) - WH_OnHand`. **Plan Target:** `TargetStock(store, item)` | Daily truck runs on `LogisticsManagerPlan`; Importer orders when `WH_OnHand < 7d` | Prime WH #1 to $\ge 7\text{d}$ stock $\rightarrow$ `StoreConnect` adds plan stop & drops held contract items only | `plans >= 1` & `5` supplied Ready stores |
| **Stage 3** | `plans >= 1` & `5` supplied stores | 1 WH per goods family, 2 trucks per WH | Same `RefillDays = 7` Importer formula + per-store `StockTarget` | Daily truck runs; `TruckBusy > 85%` triggers service/office opens & 2nd WH | `Slots.Fill` $\rightarrow$ `StoreConnect` $\rightarrow$ `Restock.Arrange` (drain mixed) $\rightarrow$ `Restock.Overflow` $\rightarrow$ `Restock.Order` | `cash >= $2M`, `supplied == goods`, `emptySlots == 0` |
| **Stage 4** | `cash >= $2M`, `supplied == goods`, `emptySlots == 0` | All goods families on single-family WHs | Same, rebalanced as HR training increases store sales velocity | Continuous daily runs | Full `Audit.Check` + `SupplyNight` + residual `Maintain` | Continuous past $\$100\text{M}$ |

---

## 4. Master Logistics Decision Tables

Tables 4A and 4B below are the retired 3-day cover. Do not use them for a line amount. The 1.5× note is the Stage 1 rule.

### Table 4A — Wholesaler Line Sizing: Seed Burn vs. Measured Burn vs. Shelf Headroom

| Store History & Stock State | `MeasuredBurn` Input | `TargetStock` ($\text{Burn} \times 2$, floor $50$) | `ShelfHeadroom` ($\text{Cap} - \text{OnHand}$) | Resulting `LineAmount` Written to `DeliveryContractItem.amount` |
| :--- | :--- | :--- | :--- | :--- |
| **Day 1 (New Store, $0$ open days, `OnHand = 0`, `Cap = 100`)** | `SeedBurn = 25` | $\max(50, 25 \times 2) = 50$ | $100 - 0 = 100$ | $\min(100, \max(10, 50 - 0)) = \mathbf{50}$ ($100$ for `paperbag`) |
| **Open Store, `MeasuredBurn = 30`, `OnHand = 15`, `Cap = 100`** | $30$/day | $\max(50, 30 \times 2) = 60$ | $100 - 15 = 85$ | $\min(85, \max(10, 60 - 15)) = \mathbf{45}$ (tops shelf up to $60$, zero overflow!) |
| **Open Store, `MeasuredBurn = 60`, `OnHand = 10`, `Cap = 100` (High Burn > Half Cap)** | $60$/day | $\min(100, \max(50, 120)) = 100$ | $100 - 10 = 90$ | $\min(90, \max(10, 100 - 10)) = \mathbf{90}$ (fills fixture to $100/100$ without overflowing!) |
| **Open Store, Shelves Almost Full (`MeasuredBurn = 20`, `OnHand = 92`, `Cap = 100`)** | $20$/day | $\max(50, 20 \times 2) = 50$ | $100 - 92 = 8$ ($< \text{MinReplenish } 10$) | Clamp to **`MinReplenishAmount = 10`** AND **defer `nextDeliveryDay` to `day + 2`** so it does not deliver tomorrow! |
| **Open Store, Zero Sales Item (`Sold = 0`, `OnHand = 40`, `Cap = 100`)** | `MinBurnFloor = 5` | $\max(25, 5 \times 2) = 25$ | $100 - 40 = 60$ | `Deficit = max(0, 25 - 40) = 0` $\rightarrow$ hold line at `MinReplenishAmount = 10` and defer delivery if all other items are covered. |

### Table 4B — When to Reschedule `nextDeliveryDay` vs. Defer / Leave Cadence

| Store Stock Condition Across All Contract Items | `anyItemNeedsDelivery` Predicate | `nextDeliveryDay` Action | Log Line |
| :--- | :--- | :--- | :--- |
| **At least 1 item has `OnHand < MeasuredBurn * CoverOpenDays` AND `ShelfHeadroom >= MinReplenishAmount` ($10$) AND `Deficit >= MinReplenishAmount`** | `true` | If `nextDeliveryDay != currentDay + 1`, set **`nextDeliveryDay = currentDay + 1`** | `contract.rescheduled ... from=<old> next=<day+1>` |
| **Every item has `OnHand >= MeasuredBurn * CoverOpenDays` ($\ge 2$ days of stock) OR every needy item has `ShelfHeadroom < MinReplenishAmount` ($10$)** | `false` | If `nextDeliveryDay <= currentDay + 1`, push **`nextDeliveryDay = currentDay + HoldDeliveryDays`** ($2$) | `contract.rescheduled ... from=<old> next=<day+2>` |
| **`nextDeliveryDay` already matches the target day** | Either | **No change** (do not mark save or append log) | *(silent)* |

### Table 4C — When to Raise, Lower, or Hold Contract Line Amounts

| Comparison Between `item.amount` and `want = LineAmount(...)` | Action | Why It Matters |
| :--- | :--- | :--- |
| `item.amount < want` | **Raise:** `item.amount = want` | Prevents mid-week stockouts when sales accelerate. |
| `item.amount > want` | **Lower:** `item.amount = want` | **Eliminates Day-36 NY Distro box overflow:** `WholesalerBridge.EnsureAmounts` previously had `if (item.amount < want)` and never lowered `140`/`280`! |
| `item.amount == want` | **Hold:** no mutation | Zero log noise on steady days. |

### Table 4D — Stage 2+ Wholesaler Keep vs. Drop Matrix (Per Item)

| Item (`itemName`) | `Advisor.CargoFamily(itemName)` | Warehouse #1 (`specialty`) Stock | `StoreConnect` Action |
| :--- | :--- | :--- | :--- |
| `ba:itemname_cheapgift` | `"specialty"` | $\ge 7 \times \text{Burn}$ (handover) / $\ge 1 \times \text{Burn}$ (connect) | Add to `W1` `LogisticsManagerPlan`; **drop from `DeliveryContract`**. |
| `ba:itemname_cheapflower` | `"specialty"` | $\ge 7 \times \text{Burn}$ (handover) / $\ge 1 \times \text{Burn}$ (connect) | Add to `W1` `LogisticsManagerPlan`; **drop from `DeliveryContract`**. |
| `ba:itemname_umbrella` | `"specialty"` | $\ge 7 \times \text{Burn}$ (handover) / $\ge 1 \times \text{Burn}$ (connect) | Add to `W1` `LogisticsManagerPlan`; **drop from `DeliveryContract`**. |
| `ba:itemname_paperbag` | `null` (`IgnoredCargo`) | $\ge 7 \times \text{Burn}$ in `W1` | Add to `W1` `LogisticsManagerPlan`; **drop from `DeliveryContract`** once `W1` holds `paperbag`. |
| `ba:itemname_sodacan` | `"food"` | $0$ in `W1` (single-family `specialty`) | **Keep on `DeliveryContract`** (`Drop == ""`); never add to `W1` plan. |

### Table 4E — First Warehouse Family, Mixed Drain, and 2nd Warehouse / Truck Triggers

| Trigger | Pure Predicate | Decision Output |
| :--- | :--- | :--- |
| **First Warehouse Family** | Primary family of Stage-1 stores (`specialty`) | `Warehouses.Decide("specialty", enteringFamily: true, ...)` $\rightarrow$ `action="home", reason="enter"`. |
| **Empty Garage Slot on Delivering WH** | `slot != null && !slot.Filled && delivering.Contains(slot.Warehouse) && cash >= reserve + truckCost` | `Trucks.Decide` $\rightarrow$ `Chosen=true, Warehouse=W, Slot=1..2`. |
| **2nd Warehouse — Truck Overload** | `trucksBusy == true` (`truckLoadFraction > 0.85m`) AND empty shelved WH exists | `Warehouses.Decide` $\rightarrow$ `action="second", reason="trucks"`. |
| **2nd Warehouse — Ordered & Still Short** | `home.Ordered && home.Daily > 0 && home.OnHand < 7 * home.Daily` AND empty shelved WH exists | `Warehouses.Decide` $\rightarrow$ `action="second", reason="short"`. |
| **Normal Refill Dip (Unordered)** | `!home.Ordered && home.OnHand < 7 * home.Daily` | `Warehouses.Decide` $\rightarrow$ `None`; `Restock.Order` places Importer order for `(7 * Daily) - OnHand`. |

### Table 4F — Importer Order Formula (`Restock.Order`)

| Warehouse Level Input | Condition | Order Amount (`RestockOrder.Amount`) | Recorded Calls |
| :--- | :--- | :--- | :--- |
| `WarehouseLevel(W, family, onHand, daily, ordered: false)` | `daily > 0 && onHand < 7 * daily` | `(7 * daily) - onHand` | `Assign = ChangeAssignedWarehouse`, `Start = PurchasingAgentPlanUI.StartOrder` |
| `ordered == true` OR `daily <= 0` OR `onHand >= 7 * daily` | Covered or already in flight | $0$ (no order) | None |

### Table 4G — Under-Supplied Store Remedies (`G2` / `Supply.List` / `Stall`)

| Stage & Symptom | Predicate | Remedy Action |
| :--- | :--- | :--- |
| **Stage 1 — Missing item on `DeliveryContract`** | `!Wholesaler.Covers(neededItems, contractItems)` | Add missing item(s) to `contract.items` at `LineAmount` and set `nextDeliveryDay = day + 1`. |
| **Stage 1 — `OnHand(item) == 0` with active contract** | `OnHand == 0` | `Maintain` sets `item.amount = LineAmount(0, MeasuredBurn, FixtureCap)` and `nextDeliveryDay = day + 1`. |
| **Stage 2+ — `gap.Reason == "noplan"` with hub** | `gap.Warehouse.Length > 0` | `Audit` / `StoreConnect` runs `connect` (`AddDestination` + drop held contract item). |
| **Stage 2+ — `gap.Reason == "short"` or `"dry"`** | Hub stock $< 7 \times \text{Burn}$ | `Restock.Order` triggers Importer refill; `Slots.Fill` fills empty truck slots. |
| **Stuck $\ge 3$ days (`Stall.StuckDays`)** | `Stall.Decide(health, stuckDays, tried).Pause == true` | Pause Autopilot with `autopilot.paused ... reason=stuck gate=g2 place=<store>`. |

---

## 5. Complete C# Implementation (`Wholesaler.cs` & `WholesalerBridge.cs` Additions)

Add these pure methods and constants to `src/ChaoticCEO/Wholesaler.cs` so every formula in Section 4 is directly unit-tested:

```csharp
        public const int BurnLookbackDays = 3;

        public const int SeedBurn = 25;

        public const int MinBurnFloor = 5;

        public const int MinReplenishAmount = 10;

        public const int HoldDeliveryDays = 2;

        public const int DefaultFixtureCap = 100;

        public const int PaperBagFixtureCap = 200;

        public static int FixtureCapacity(string? item, int liveFixtureCap = 0)
        {
            if (liveFixtureCap > 0)
            {
                return liveFixtureCap;
            }

            return item == PaperBag ? PaperBagFixtureCap : DefaultFixtureCap;
        }

        public static int ShelfHeadroom(int onHand, int fixtureCap)
        {
            var cap = fixtureCap > 0 ? fixtureCap : DefaultFixtureCap;
            var free = cap - (onHand > 0 ? onHand : 0);
            return free > 0 ? free : 0;
        }

        public static int MeasuredBurn(IReadOnlyList<int>? recentOpenDayUnits)
        {
            if (recentOpenDayUnits == null || recentOpenDayUnits.Count == 0)
            {
                return SeedBurn;
            }

            var take = recentOpenDayUnits.Count < BurnLookbackDays
                ? recentOpenDayUnits.Count
                : BurnLookbackDays;
            var sum = 0;
            for (var i = recentOpenDayUnits.Count - take; i < recentOpenDayUnits.Count; i++)
            {
                var u = recentOpenDayUnits[i];
                if (u > 0)
                {
                    sum += u;
                }
            }

            var avg = (int)Math.Ceiling((double)sum / take);
            return avg < MinBurnFloor ? MinBurnFloor : avg;
        }

        public static int LineAmount(
            string? item,
            int onHand,
            int dailyBurn,
            int fixtureCap = 0)
        {
            var cap = FixtureCapacity(item, fixtureCap);
            var headroom = ShelfHeadroom(onHand, cap);
            if (headroom < MinReplenishAmount)
            {
                return MinReplenishAmount;
            }

            var burn = dailyBurn > 0 ? dailyBurn : SeedBurn;
            var minFloor = item == PaperBag ? MinContractAmount * 2 : MinContractAmount;
            if (burn < SeedBurn)
            {
                minFloor = burn * CoverOpenDays < MinReplenishAmount
                    ? MinReplenishAmount
                    : burn * CoverOpenDays;
            }

            var rawTarget = burn * CoverOpenDays;
            if (rawTarget < minFloor)
            {
                rawTarget = minFloor;
            }

            var target = rawTarget > cap ? cap : rawTarget;
            var deficit = target - (onHand > 0 ? onHand : 0);
            if (deficit <= 0)
            {
                return MinReplenishAmount;
            }

            var desired = deficit < MinReplenishAmount ? MinReplenishAmount : deficit;
            return desired > headroom ? headroom : desired;
        }

        public static bool NeedsDeliveryTomorrow(
            int onHand,
            int dailyBurn,
            int fixtureCap = 0,
            string? item = null)
        {
            var cap = FixtureCapacity(item, fixtureCap);
            var headroom = ShelfHeadroom(onHand, cap);
            if (headroom < MinReplenishAmount)
            {
                return false;
            }

            var burn = dailyBurn > 0 ? dailyBurn : SeedBurn;
            var coverTarget = burn * CoverOpenDays;
            if (coverTarget > cap)
            {
                coverTarget = cap;
            }

            return onHand < coverTarget && (coverTarget - onHand) >= MinReplenishAmount;
        }
```

### Updated `WholesalerBridge.EnsureAmounts` (Raises AND Lowers `item.amount`)

In `src/ChaoticCEO/WholesalerBridge.cs`, replace `if (item.amount < want)` with `if (item.amount != want)` using per-store `OnHand` and `MeasuredBurn`:

```csharp
        private static bool EnsureAmounts(
            DeliveryContract contract,
            IReadOnlyDictionary<string, int> cargoOnHand,
            IReadOnlyDictionary<string, int> burnByItem)
        {
            if (contract.items == null)
            {
                return false;
            }

            var touched = false;
            foreach (var item in contract.items)
            {
                if (item == null || string.IsNullOrEmpty(item.itemName))
                {
                    continue;
                }

                cargoOnHand.TryGetValue(item.itemName, out var onHand);
                if (!burnByItem.TryGetValue(item.itemName, out var burn) || burn <= 0)
                {
                    burn = Wholesaler.SeedBurn;
                }

                var want = Wholesaler.LineAmount(item.itemName, onHand, burn);
                if (item.amount != want)
                {
                    item.amount = want;
                    touched = true;
                }
            }

            return touched;
        }
```

---

## 6. Copy-Pasteable Tier-1 xUnit Locks (`src/ChaoticCEO.Tests/WholesalerTests.cs`)

```csharp
    [Fact]
    public void Wholesaler_line_amount_caps_delivery_by_shelf_headroom_and_lowers_when_stocked()
    {
        // Harvest: day-36 NY Distro overflow — Maintain kept 140/280 even when shelves held 80+ units.
        Assert.Equal(100, Wholesaler.FixtureCapacity("ba:itemname_cheapgift"));
        Assert.Equal(200, Wholesaler.FixtureCapacity("ba:itemname_paperbag"));

        // Burn 30/day -> target 60. With 15 on hand and cap 100 -> deficit 45, headroom 85 -> LineAmount 45.
        Assert.Equal(45, Wholesaler.LineAmount("ba:itemname_cheapgift", onHand: 15, dailyBurn: 30, fixtureCap: 100));

        // High onHand (85/100) -> headroom is only 15 -> LineAmount never exceeds 15 (never 140!).
        Assert.True(Wholesaler.LineAmount("ba:itemname_cheapgift", onHand: 85, dailyBurn: 70, fixtureCap: 100) <= 15);

        // Full shelf (95/100, headroom 5 < MinReplenishAmount 10) -> does NOT pull delivery to tomorrow.
        Assert.False(Wholesaler.NeedsDeliveryTomorrow(onHand: 95, dailyBurn: 30, fixtureCap: 100, item: "ba:itemname_cheapgift"));

        // Low shelf (20/100 < 2 * 30 = 60, headroom 80 >= 10) -> pulls delivery to tomorrow.
        Assert.True(Wholesaler.NeedsDeliveryTomorrow(onHand: 20, dailyBurn: 30, fixtureCap: 100, item: "ba:itemname_cheapgift"));
    }

    [Fact]
    public void Wholesaler_measured_burn_averages_last_three_open_days_with_floor()
    {
        Assert.Equal(Wholesaler.SeedBurn, Wholesaler.MeasuredBurn(Array.Empty<int>()));
        Assert.Equal(Wholesaler.MinBurnFloor, Wholesaler.MeasuredBurn(new[] { 0, 0, 0 }));
        Assert.Equal(20, Wholesaler.MeasuredBurn(new[] { 10, 20, 30 }));
        // Only last 3 open days (20, 30, 40) count -> avg 30.
        Assert.Equal(30, Wholesaler.MeasuredBurn(new[] { 100, 20, 30, 40 }));
    }
```