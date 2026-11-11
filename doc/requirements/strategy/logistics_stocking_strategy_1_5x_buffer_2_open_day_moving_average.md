# Logistics & Stocking Strategy — Stage 1 Door Delivery to Stage 4 Rebalancing (Exhaustive Engineering Spec)

## 1. Executive Logistics Path & The `1.16.7.7` Forensic Post-Mortem

### 1.1 Why Run `1.16.7.7` Shorted Stock on Day 7 (`3rd Street 35`)
In run `1.16.7.7`, the umbrella order oscillated (`50 -> 16 -> 37 -> 20 -> 35`), and on **Day 7 (Sunday, $0$ customers)** the daily resize crushed every single item down to `10` (including shrinking `ba:itemname_paperbag` from `100 -> 10`) and pushed the Monday Day-8 delivery out to Day 9. Three flaws caused this:
1. **Closed Sunday Zero-Sales Poisoning:** Day 7 was a scheduled closed Sunday (`0` customers). Including a closed day in the burn lookback (or treating a $0$-sale Sunday as a drop in demand) collapsed the calculated need and triggered the delivery deferral rule on Sunday night—leaving Monday morning with nearly empty shelves and no truck until Tuesday (Day 9).
2. **Missing the `1.5x` Safety Buffer Multiplier (`SafetyMultiplier = 1.5m`):** Sizing deliveries to `1.0x` of the delivery gap leaves zero margin for upward sales trends and fails to account for the fact that today's customers burn a full day of stock *before* tomorrow's delivery arrives.
3. **Why "Full Need Without Subtracting `OnHand`" Is Also Fatal:** If a store writes the full gross `TargetStock` (`Avg * GapDays * 1.5`) onto every delivery without subtracting what is already sitting on the shelves (`OnHand`), the unused `0.5x` buffer from each delivery accumulates every $2$ days and overflows the store fixtures within $3$ deliveries. Conversely, subtracting `OnHand` from the `1.5x` `TargetStock` (`NetOrder = TargetStock - OnHand`, clamped to `[MinReplenishAmount, ShelfHeadroom]`) tops the store up to the full `1.5x` buffer on every truck while automatically shrinking the order when sales contract.

---

### 1.2 The 6 Golden Rules of Store Replenishment (Acceptance Criteria Lock)

1. **Rule L1 — Day-1 Initial Delivery Guess (`1.5x` First Drop):**
   * On Day 1 (or any new store with $0$ completed open days of sales history), seed the initial daily burn at **`SeedBurn = 25`** units/day for sellable items and **`PaperBagSeedBurn = 50`** units/day for `ba:itemname_paperbag` (or use `FirstDropAmount(SeedBurn, gapDays)` $= \lceil \text{SeedBurn} \times \text{GapDays} \times 1.5 \rceil$, capped by `FixtureCap`).
   * Gate contract creation strictly on `StoreOpsBridge.HasItems(registration) == true` (never sign a contract on an unfurnished bare lot).
2. **Rule L2 — Strictly Completed OPEN Days in the 2-Day Moving Average (`LookbackOpenDays = 2`):**
   * Every morning on `day.rolled`, inspect `registration.orderHistory` for completed days `entry.dayNumber < currentDay`.
   * **Hard Exclusion of Closed Days:** A day is an **Open Day** for store $s$ if and only if the store was scheduled open on that day (`dayNumber % 7 != 0` on a Mon–Sat schedule) AND total store units sold across all items on that day was $> 0$ (or `CustomersServed > 0`). **Closed Sundays (`0` customers) are completely invisible to the moving average.**
   * Take the last **`LookbackOpenDays = 2`** completed open days ($d_1, d_2$) for item $i$ and compute:
     $$\text{AvgDailyBurn}(s, i) = \max\!\left(\text{MinBurnFloor}\ (5),\ \left\lceil \frac{u(d_1) + u(d_2)}{N_{\text{open}}} \right\rceil\right)$$
     where $N_{\text{open}} \in \{1, 2\}$ is the number of completed open days available so far. (If $N_{\text{open}} == 0$, use `SeedBurn`).
   * **Paper Bag Moving Average Rule (`ba:itemname_paperbag`):** Because `orderHistory` tracks retail *sold* items and customers consume $1$ paper bag per checkout order (or proportional to total sellable units), if `orderHistory` reports $0$ direct sales for `ba:itemname_paperbag`, derive its open-day burn from the store's total customer orders on those open days (or $\max(\text{PaperBagMinBurn}\ (25),\ \lceil 0.5 \times \sum_{\text{sellable}} \text{AvgDailyBurn} \rceil)$) so paper bags **never** drop to `10`!
3. **Rule L3 — The `1.5x` Gross Target Stock Formula (`TargetStock`):**
   * Let $G = \text{ValidGapDays}(\text{gapDays}) \in \{1, 2, 3\}$ be the number of open days between deliveries on the contract (if a contract's gap is $< 1$ or $> 3$, normalize it to **`DefaultGapDays = 2`** and replace the contract).
   * Compute the gross shelf target required to cover $G$ open days with a **$50\%$ surge buffer (`SafetyMultiplier = 1.5m`)**:
     $$\text{TargetStock}(s, i) = \left\lceil \text{AvgDailyBurn}(s, i) \times G \times 1.5 \right\rceil$$
   * **Worked Examples:**
     * Item averaging $40/\text{day}$ on a $1$-day truck ($G=1$): $\text{TargetStock} = \lceil 40 \times 1 \times 1.5 \rceil = \mathbf{60}$.
     * Item averaging $40/\text{day}$ on a $2$-day truck ($G=2$): $\text{TargetStock} = \lceil 40 \times 2 \times 1.5 \rceil = \mathbf{120}$.
     * Item averaging $40/\text{day}$ on a $3$-day truck ($G=3$): $\text{TargetStock} = \lceil 40 \times 3 \times 1.5 \rceil = \mathbf{180}$.
4. **Rule L4 — Net Order Amount = `TargetStock - OnHand`, Capped by `ShelfHeadroom`:**
   * Read physical units currently in the store (`OnHand` = units on display fixtures + units in backroom storage boxes) and total store capacity for that item (`FixtureCap`).
   * Compute available physical space:
     $$\text{ShelfHeadroom}(s, i) = \max\big(0,\ \text{FixtureCap}(s, i) - \text{OnHand}(s, i)\big)$$
   * Subtract `OnHand` from `TargetStock` to get the net replenishment needed (`RawNeed = TargetStock - OnHand`), and clamp into `[MinReplenishAmount (10), ShelfHeadroom]`:
     $$\text{OrderAmount}(s, i) = \min\!\Big(\text{ShelfHeadroom}(s, i),\ \max\big(\text{MinReplenishAmount}\ (10),\ \text{TargetStock}(s, i) - \text{OnHand}(s, i)\big)\Big)$$
   * *(If `ShelfHeadroom < MinReplenishAmount` because the shelf is $> 90\%$ full, set `OrderAmount = MinReplenishAmount` ($10$) so the contract line remains valid).*
5. **Rule L5 — Mandatory Daily Diagnostic Telemetry Log (`stock.math`):**
   * Every single morning on `day.rolled`, for **every store** and **every contract item** (even when the truck does not deliver today or the amount does not change), emit one structured diagnostic line to `ceo.log`:
     ```text
     stock.math version=<v> day=<d> store=<addr> item=<sku> openDay1=<d1> openDay2=<d2> avg=<avg> gapDays=<gap> mult=1.5 target=<target> onHand=<onHand> cap=<cap> headroom=<headroom> rawNeed=<rawNeed> orderAmount=<order> nextDeliveryDay=<nextDay> action=<updated|recreated|held>
     ```
6. **Rule L6 — In-Place Edit vs. Cancel-and-Recreate Delivery Contract (`WholesalerBridge.Maintain`):**
   * On every `day.rolled` inside the continuous self-heal loop:
     * Update `item.amount = OrderAmount` on the active `DeliveryContract`.
     * **Never push `nextDeliveryDay` out on a closed Sunday** or when any item in the store has `OnHand < TargetStock`!
     * If `contract.daysBetweenDeliveries` (or `nextDeliveryDay - currentDay`) is $> \text{MaxGapDays}$ ($3$) or $< 1$, OR if the game engine locks an in-flight delivery from in-place mutation, **cancel (`DeliveryContract.Remove`) and immediately recreate (`StartContract`)** the replacement contract for the normalized gap ($G \in \{1, 2, 3\}$, default $2$) with `item.amount = OrderAmount` and `nextDeliveryDay = currentDay + 1`.

---

## 2. Build 3682 Data Model: Fixtures, Cargo, Open-Day History & Contract Mutation

1. **Physical Stock on Hand (`OnHand`):**
   * On `BuildingRegistration`, `building.itemInstances` (`Dictionary<string, ItemInstance>`) holds all placed fixtures (`Product Panel`, `Shelf`, `Drinks Fridge`, `Flower Fridge`, `CashRegisterController`, `StorageRack`, and `ClosedCardboardBox`).
   * `LiveDump.FixtureCargo(building)` sums `box.amount` (and `nested.amount`) per `itemName` into an `IReadOnlyDictionary<string, int>`.
2. **Fixture Storage Capacity (`FixtureCap`):**
   * Count display shelves + storage rack slots assigned to `itemName` in `building.itemInstances.Values`.
   * Default starter capacities: **`DefaultFixtureCap = 150`** (or $100$ per small shelf + backroom box buffer; $200$ for `ba:itemname_paperbag`), scaling automatically by $+200$ whenever `StoreScale` adds a `ba:itemname_storagerack`.
3. **Extracting the Last 2 Completed Open Days (`ExtractLastTwoOpenDays`):**
   * Iterate `building.orderHistory` sorted by `entry.dayNumber` ascending where `entry.dayNumber < current.Day`.
   * Compute `totalStoreUnitsThatDay = entry.itemSales.Sum(s => s.amountSold)`.
   * **Filter:** If `totalStoreUnitsThatDay <= 0` (e.g., closed Sunday, holiday, or pre-opening day), **skip that `OrderHistoryEntry` completely**.
   * Take the last $2$ surviving open-day entries (`openDay1`, `openDay2`).

---

## 3. Multi-Day Walkthrough: How the New Math Prevents the Day-7 Bug on `3rd Street 35`

Trace an item (`ba:itemname_umbrella`, `FixtureCap = 150`) and `ba:itemname_paperbag` (`FixtureCap = 250`) across Days 1–9 on a 2-day delivery gap ($G = 2$):

| Day (DOW) | Store Status | Last 2 Open Days Umbrella Sales (`d1, d2`) | `AvgDailyBurn` | `Gap` ($G$) | `TargetStock` ($\lceil \text{Avg} \times G \times 1.5 \rceil$) | Morning `OnHand` | `RawNeed` ($\text{Target} - \text{OnHand}$) | `OrderAmount` Written to Truck | `nextDeliveryDay` | Why It Never Shorts or Overflows |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Day 1 (Mon)** | Fit-Out (0 sales yet) | `none` (Seed $= 25$) | $25$ | $2$ | $\lceil 25 \times 2 \times 1.5 \rceil = \mathbf{75}$ | $0$ | $75 - 0 = 75$ | **$75$** (`paperbag`: **$150$**) | **Day 2 (Tue)** | First drop has $1.5\times$ buffer across the 2-day window. |
| **Day 2 (Tue)** | Truck #1 drops $75$; sells $32$ today | `none` (0 completed open days at 00:00) | $25$ | $2$ | $75$ | $75$ (after drop) | $0 \rightarrow$ hold | **$75$** | **Day 4 (Thu)** | Store opens Tuesday with $75$ units on the shelf. |
| **Day 3 (Wed)** | Open Day 2; sells $38$ today | `[32]` ($1$ open day) | $32$ | $2$ | $\lceil 32 \times 2 \times 1.5 \rceil = \mathbf{96}$ | $43$ ($75 - 32$) | $96 - 43 = 53$ | **$53$** | **Day 4 (Thu)** | Resizes Thursday's truck to $53$ so Thursday morning tops up to $96$ (minus Wed sales). |
| **Day 4 (Thu)** | Truck #2 drops; sells $40$ today | `[32, 38]` | $35$ | $2$ | $\lceil 35 \times 2 \times 1.5 \rceil = \mathbf{105}$ | $58$ ($5\text{ left} + 53$) | $105 - 58 = 47$ | **$47$** | **Day 6 (Sat)** | 2-open-day average ($35$) sets Target $= 105$. |
| **Day 5 (Fri)** | Open; sells $42$ today | `[38, 40]` | $39$ | $2$ | $\lceil 39 \times 2 \times 1.5 \rceil = \mathbf{117}$ | $18$ ($58 - 40$) | $117 - 18 = 99$ | **$99$** | **Day 6 (Sat)** | Friday morning sees only $18$ left, so Saturday's truck is raised to **$99$**! |
| **Day 6 (Sat)** | Truck #3 drops $99$; sells $44$ today | `[40, 42]` | $41$ | $2$ | $\lceil 41 \times 2 \times 1.5 \rceil = \mathbf{123}$ | $75$ (after Fri/drop) | $123 - 75 = 48$ | **$48$** | **Day 8 (Mon)** | Saturday ends with $75 - 44 = 31$ units on the shelf. |
| **Day 7 (Sun)** | **CLOSED SUNDAY ($0$ sales)** | **`[42, 44]` (Fri & Sat — Sunday skipped!)** | **$43$** | $2$ | $\lceil 43 \times 2 \times 1.5 \rceil = \mathbf{129}$ | $31$ | $129 - 31 = 98$ | **$98$** (`paperbag`: **$160$**) | **Day 8 (Mon) — NOT pushed!** | **Bug Fixed:** Sunday's $0$ is ignored! Monday's truck stays on Day 8 and brings **$98$** umbrellas + **$160$** bags! |
| **Day 8 (Mon)** | Truck #4 drops $98$ at dawn ($31 + 98 = 129$ on shelf!) | `[42, 44]` (Sun skipped!) | $43$ | $2$ | $\mathbf{129}$ | $129$ | $0 \rightarrow$ resize Tue/Wed | **$43\text{–}86$** | **Day 10 (Wed)** | Monday opens with **$129$ units** on the shelf—zero stockouts! |

---

## 4. Master Logistics Decision Tables

### Table 4A — Daily Replenishment Sizing (`Wholesaler.EvaluateReplenishment`)

| Store History & Stock State | Last 2 Open Days (`d1, d2`) | `AvgDailyBurn` | `GapDays` ($G$) | `TargetStock` ($\lceil \text{Avg} \times G \times 1.5 \rceil$) | `OnHand` | `FixtureCap` (`Headroom`) | Resulting `OrderAmount` Written to Next Delivery |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Day 1 First Drop (Sellable Item, $G=2$)** | None ($0$ open days) | `SeedBurn = 25` | $2$ | $\lceil 25 \times 2 \times 1.5 \rceil = \mathbf{75}$ | $0$ | $150$ ($150$) | **$75$** (or $\lceil 25 \times 1.5 \rceil = 38$ if $G=1$) |
| **Day 1 First Drop (`paperbag`, $G=2$)** | None ($0$ open days) | `PaperBagSeed = 50` | $2$ | $\lceil 50 \times 2 \times 1.5 \rceil = \mathbf{150}$ | $0$ | $250$ ($250$) | **$150$** (never shrinks to $10$ on closed Sunday!) |
| **1-Day Truck ($G=1$), Avg $40$, `OnHand = 15`** | `38, 42` | $40$ | $1$ | $\lceil 40 \times 1 \times 1.5 \rceil = \mathbf{60}$ | $15$ | $150$ ($135$) | $60 - 15 = \mathbf{45}$ |
| **2-Day Truck ($G=2$), Avg $40$, `OnHand = 25`** | `38, 42` | $40$ | $2$ | $\lceil 40 \times 2 \times 1.5 \rceil = \mathbf{120}$ | $25$ | $150$ ($125$) | $120 - 25 = \mathbf{95}$ |
| **3-Day Truck ($G=3$), Avg $40$, `OnHand = 30`** | `38, 42` | $40$ | $3$ | $\lceil 40 \times 3 \times 1.5 \rceil = \mathbf{180}$ | $30$ | $200$ ($170$) | $180 - 30 = \mathbf{150}$ |
| **Sales Contraction ($40 \rightarrow 14$), `OnHand = 30`, $G=2$** | `16, 12` | $14$ | $2$ | $\lceil 14 \times 2 \times 1.5 \rceil = \mathbf{42}$ | $30$ | $150$ ($120$) | $42 - 30 = \mathbf{12}$ (automatically shrinks order so storage doesn't stay full!) |
| **Closed Sunday Roll (`Day 7`), `OnHand = 31`, $G=2$** | `42, 44` (Fri, Sat; Sun ignored) | $43$ | $2$ | $\lceil 43 \times 2 \times 1.5 \rceil = \mathbf{129}$ | $31$ | $150$ ($119$) | $129 - 31 = \mathbf{98}$ (Monday truck is **not** deferred!) |

### Table 4B — Contract Gap Normalization & In-Place Update vs. Cancel/Recreate

| Contract State on `day.rolled` | Gap Check (`gapDays`) | Mutation Action | `ceo.log` Action Token |
| :--- | :--- | :--- | :--- |
| **Valid Gap ($G \in \{1, 2, 3\}$) & Editable in Place** | `1 <= gapDays <= 3` | Set `item.amount = calc.OrderAmount`; if `OnHand < AvgDailyBurn * 1.5` and `nextDeliveryDay > currentDay + 1`, pull `nextDeliveryDay = currentDay + 1`. | `action=updated` (or `action=held` if unchanged) |
| **Invalid Gap ($G < 1$ or $G > 3$) or Locked Contract** | `gapDays < 1 || gapDays > 3` | Normalize gap to **`DefaultGapDays = 2`**; call `DeliveryContract.Remove()` on old contract and `StartContract(...)` with `calc.OrderAmount` and `nextDeliveryDay = currentDay + 1`. | `action=recreated` |
| **Missing SKU on Active Contract** | `!Covers(neededItems, contract.items)` | Add missing `DeliveryContractItem(sku, calc.OrderAmount)` or recreate contract with all required SKUs. | `action=recreated` |

### Table 4C — Stage 2+ Warehouse Handover & Residual Cross-Family Rule

| Item (`itemName`) | `Advisor.CargoFamily(itemName)` | Warehouse #1 (`specialty`) Stock | `StoreConnect` Action |
| :--- | :--- | :--- | :--- |
| `ba:itemname_cheapgift` | `"specialty"` | $\ge 7 \times \text{Burn}$ (handover) / $\ge 1 \times \text{Burn}$ (connect) | Add to `W1` `LogisticsManagerPlan` (`ItemAmountTarget = TargetStock`); **drop from `DeliveryContract`**. |
| `ba:itemname_cheapflower` | `"specialty"` | $\ge 7 \times \text{Burn}$ (handover) / $\ge 1 \times \text{Burn}$ (connect) | Add to `W1` `LogisticsManagerPlan` (`ItemAmountTarget = TargetStock`); **drop from `DeliveryContract`**. |
| `ba:itemname_umbrella` | `"specialty"` | $\ge 7 \times \text{Burn}$ (handover) / $\ge 1 \times \text{Burn}$ (connect) | Add to `W1` `LogisticsManagerPlan` (`ItemAmountTarget = TargetStock`); **drop from `DeliveryContract`**. |
| `ba:itemname_paperbag` | `null` (`IgnoredCargo`) | $\ge 7 \times \text{Burn}$ in `W1` | Add to `W1` `LogisticsManagerPlan`; **drop from `DeliveryContract`** once `W1` holds `paperbag`. |
| `ba:itemname_sodacan` | `"food"` | $0$ in `W1` (single-family `specialty`) | **Keep on `DeliveryContract`** (`Drop == ""`); sized daily by `Wholesaler.EvaluateReplenishment` until a `food` warehouse opens in Stage 3. |

---

## 5. Complete C# Implementation (`src/ChaoticCEO/Wholesaler.cs` & `WholesalerBridge.cs`)

### 5.1 Pure Replenishment & Diagnostic Math (`src/ChaoticCEO/Wholesaler.cs`)

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ChaoticCEO
{
    public sealed class OpenDaySale
    {
        public OpenDaySale(int dayNumber, int storeTotalUnits, int itemUnitsSold)
        {
            DayNumber = dayNumber;
            StoreTotalUnits = storeTotalUnits;
            ItemUnitsSold = itemUnitsSold;
        }

        public int DayNumber { get; }

        /// <summary>Total units sold across ALL items in the store on this day (0 means the store was closed / had 0 customers).</summary>
        public int StoreTotalUnits { get; }

        public int ItemUnitsSold { get; }
    }

    public sealed class ReplenishCalc
    {
        public ReplenishCalc(
            string storeAddress,
            string item,
            int openDay1Sales,
            int openDay2Sales,
            int openDaysCount,
            int avgDailyBurn,
            int gapDays,
            decimal multiplier,
            int targetStock,
            int onHand,
            int fixtureCap,
            int shelfHeadroom,
            int rawNeed,
            int orderAmount)
        {
            StoreAddress = storeAddress ?? "";
            Item = item ?? "";
            OpenDay1Sales = openDay1Sales;
            OpenDay2Sales = openDay2Sales;
            OpenDaysCount = openDaysCount;
            AvgDailyBurn = avgDailyBurn;
            GapDays = gapDays;
            Multiplier = multiplier;
            TargetStock = targetStock;
            OnHand = onHand;
            FixtureCap = fixtureCap;
            ShelfHeadroom = shelfHeadroom;
            RawNeed = rawNeed;
            OrderAmount = orderAmount;
        }

        public string StoreAddress { get; }

        public string Item { get; }

        public int OpenDay1Sales { get; }

        public int OpenDay2Sales { get; }

        public int OpenDaysCount { get; }

        public int AvgDailyBurn { get; }

        public int GapDays { get; }

        public decimal Multiplier { get; }

        public int TargetStock { get; }

        public int OnHand { get; }

        public int FixtureCap { get; }

        public int ShelfHeadroom { get; }

        public int RawNeed { get; }

        public int OrderAmount { get; }

        public string ToLogLine(string version, int currentDay, int nextDeliveryDay, string action)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "stock.math version={0} day={1} store={2} item={3} openDay1={4} openDay2={5} openDays={6} avg={7} gapDays={8} mult={9:0.0} target={10} onHand={11} cap={12} headroom={13} rawNeed={14} orderAmount={15} nextDeliveryDay={16} action={17}",
                version ?? "",
                currentDay,
                StoreAddress,
                Item,
                OpenDay1Sales,
                OpenDay2Sales,
                OpenDaysCount,
                AvgDailyBurn,
                GapDays,
                Multiplier,
                TargetStock,
                OnHand,
                FixtureCap,
                ShelfHeadroom,
                RawNeed,
                OrderAmount,
                nextDeliveryDay,
                string.IsNullOrEmpty(action) ? "held" : action);
        }
    }

    public static partial class Wholesaler
    {
        public const string PaperBag = "ba:itemname_paperbag";

        public const int LookbackOpenDays = 2;

        public const int CoverOpenDays = 2;

        public const int DefaultGapDays = 2;

        public const int MinGapDays = 1;

        public const int MaxGapDays = 3;

        public const decimal SafetyMultiplier = 1.5m;

        public const int SeedBurn = 25;

        public const int PaperBagSeedBurn = 50;

        public const int MinBurnFloor = 5;

        public const int PaperBagMinBurnFloor = 25;

        public const int MinReplenishAmount = 10;

        public const int DefaultFixtureCap = 150;

        public const int PaperBagFixtureCap = 250;

        public static int NormalizeGapDays(int gapDays)
        {
            if (gapDays < MinGapDays || gapDays > MaxGapDays)
            {
                return DefaultGapDays;
            }

            return gapDays;
        }

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

        /// <summary>
        /// Filters raw daily history to strictly completed OPEN days (dayNumber &lt; currentDay and StoreTotalUnits &gt; 0),
        /// completely ignoring closed Sundays or 0-customer days, and returns the last 2 open days of sales.
        /// </summary>
        public static (int openDay1, int openDay2, int openDaysCount) ExtractLastTwoOpenDays(
            int currentDay,
            IReadOnlyList<OpenDaySale>? history,
            bool isPaperBag = false)
        {
            if (history == null || history.Count == 0)
            {
                return (0, 0, 0);
            }

            var openDays = new List<OpenDaySale>();
            foreach (var row in history)
            {
                if (row == null || row.DayNumber >= currentDay)
                {
                    continue;
                }

                // Closed day check: if the entire store sold 0 units (e.g. closed Sunday), skip it!
                if (row.StoreTotalUnits <= 0)
                {
                    continue;
                }

                openDays.Add(row);
            }

            if (openDays.Count == 0)
            {
                return (0, 0, 0);
            }

            openDays.Sort((a, b) => a.DayNumber.CompareTo(b.DayNumber));

            int EffectiveUnits(OpenDaySale s)
            {
                if (!isPaperBag)
                {
                    return s.ItemUnitsSold > 0 ? s.ItemUnitsSold : 0;
                }

                // Paper bags are consumed by customer orders even when itemSales for paperbag is 0.
                if (s.ItemUnitsSold > 0)
                {
                    return s.ItemUnitsSold;
                }

                return (int)Math.Ceiling(s.StoreTotalUnits * 0.50);
            }

            if (openDays.Count == 1)
            {
                var d2 = EffectiveUnits(openDays[0]);
                return (0, d2, 1);
            }

            var prev = EffectiveUnits(openDays[openDays.Count - 2]);
            var last = EffectiveUnits(openDays[openDays.Count - 1]);
            return (prev, last, 2);
        }

        public static int ComputeTwoOpenDayAverage(
            string? item,
            int openDay1Sales,
            int openDay2Sales,
            int openDaysCount)
        {
            var isBag = item == PaperBag;
            if (openDaysCount <= 0)
            {
                return isBag ? PaperBagSeedBurn : SeedBurn;
            }

            var sum = (openDay1Sales > 0 ? openDay1Sales : 0) + (openDay2Sales > 0 ? openDay2Sales : 0);
            var divisor = openDaysCount >= 2 ? 2 : 1;
            var avg = (int)Math.Ceiling((double)sum / divisor);
            var floor = isBag ? PaperBagMinBurnFloor : MinBurnFloor;
            return avg < floor ? floor : avg;
        }

        public static int ComputeTargetStock(int avgDailyBurn, int gapDays)
        {
            var burn = avgDailyBurn > 0 ? avgDailyBurn : SeedBurn;
            var gap = NormalizeGapDays(gapDays);
            return (int)Math.Ceiling((double)(burn * gap * SafetyMultiplier));
        }

        /// <summary>
        /// Evaluates the daily 2-open-day moving average, multiplies by gapDays * 1.5 to get TargetStock,
        /// and subtracts OnHand (clamped to [MinReplenishAmount, ShelfHeadroom]) so shelves never empty and never overflow.
        /// </summary>
        public static ReplenishCalc EvaluateReplenishment(
            string? storeAddress,
            string? item,
            int currentDay,
            int gapDays,
            int onHand,
            int liveFixtureCap,
            IReadOnlyList<OpenDaySale>? history)
        {
            var sku = item ?? "";
            var isBag = sku == PaperBag;
            var (d1, d2, count) = ExtractLastTwoOpenDays(currentDay, history, isBag);
            var avg = ComputeTwoOpenDayAverage(sku, d1, d2, count);
            var validGap = NormalizeGapDays(gapDays);
            var targetStock = ComputeTargetStock(avg, validGap);

            var cap = FixtureCapacity(sku, liveFixtureCap);
            var currentOnHand = onHand > 0 ? onHand : 0;
            var headroom = ShelfHeadroom(currentOnHand, cap);
            var rawNeed = targetStock - currentOnHand;

            int orderAmount;
            if (headroom < MinReplenishAmount)
            {
                orderAmount = MinReplenishAmount;
            }
            else
            {
                var clampedLow = rawNeed < MinReplenishAmount ? MinReplenishAmount : rawNeed;
                orderAmount = clampedLow > headroom ? headroom : clampedLow;
            }

            return new ReplenishCalc(
                storeAddress ?? "",
                sku,
                d1,
                d2,
                count,
                avg,
                validGap,
                SafetyMultiplier,
                targetStock,
                currentOnHand,
                cap,
                headroom,
                rawNeed,
                orderAmount);
        }

        public static int LineAmount(
            string? item,
            int onHand,
            int dailyBurn,
            int fixtureCap = 0,
            int gapDays = DefaultGapDays)
        {
            var sku = item ?? "";
            var isBag = sku == PaperBag;
            var floor = isBag ? PaperBagMinBurnFloor : MinBurnFloor;
            var burn = dailyBurn > 0 ? (dailyBurn < floor ? floor : dailyBurn) : (isBag ? PaperBagSeedBurn : SeedBurn);
            var target = ComputeTargetStock(burn, gapDays);
            var cap = FixtureCapacity(sku, fixtureCap);
            var headroom = ShelfHeadroom(onHand, cap);
            if (headroom < MinReplenishAmount)
            {
                return MinReplenishAmount;
            }

            var rawNeed = target - (onHand > 0 ? onHand : 0);
            var desired = rawNeed < MinReplenishAmount ? MinReplenishAmount : rawNeed;
            return desired > headroom ? headroom : desired;
        }

        public static bool ShouldPullDeliveryToTomorrow(
            int onHand,
            int avgDailyBurn,
            int gapDays = DefaultGapDays)
        {
            var burn = avgDailyBurn > 0 ? avgDailyBurn : SeedBurn;
            // If current stock on hand cannot even cover 1 open day + 50% buffer, pull the truck to tomorrow!
            var oneDaySafe = (int)Math.Ceiling((double)(burn * SafetyMultiplier));
            return onHand < oneDaySafe;
        }
    }
}
```

---

## 6. Copy-Pasteable Tier-1 xUnit Locks (`src/ChaoticCEO.Tests/WholesalerReplenishTests.cs`)

```csharp
using System.Collections.Generic;
using Xunit;

namespace ChaoticCEO.Tests
{
    public sealed class WholesalerReplenishTests
    {
        [Fact]
        public void Wholesaler_evaluate_replenishment_computes_two_open_day_average_times_gap_times_1_5_minus_on_hand()
        {
            // Item averaging 40/day (38 on Day 4, 42 on Day 5) on a 2-day delivery gap:
            // TargetStock = 40 * 2 * 1.5 = 120.
            // With 35 units currently on the shelf (OnHand = 35) and cap 150 (headroom 115):
            // RawNeed = 120 - 35 = 85 -> OrderAmount = 85!
            var history = new[]
            {
                new OpenDaySale(dayNumber: 4, storeTotalUnits: 120, itemUnitsSold: 38),
                new OpenDaySale(dayNumber: 5, storeTotalUnits: 130, itemUnitsSold: 42),
            };

            var calc = Wholesaler.EvaluateReplenishment(
                storeAddress: "3rd Street 35",
                item: "ba:itemname_umbrella",
                currentDay: 6,
                gapDays: 2,
                onHand: 35,
                liveFixtureCap: 150,
                history: history);

            Assert.Equal(38, calc.OpenDay1Sales);
            Assert.Equal(42, calc.OpenDay2Sales);
            Assert.Equal(40, calc.AvgDailyBurn);
            Assert.Equal(2, calc.GapDays);
            Assert.Equal(1.5m, calc.Multiplier);
            Assert.Equal(120, calc.TargetStock);
            Assert.Equal(35, calc.OnHand);
            Assert.Equal(85, calc.RawNeed);
            Assert.Equal(85, calc.OrderAmount);
        }

        [Fact]
        public void Wholesaler_ignores_closed_sunday_zero_sales_and_never_crushes_monday_order_or_paper_bags()
        {
            // Reproduces 1.16.7.7 bug on 3rd Street 35:
            // Day 5 (Fri): 42 umbrellas sold; Day 6 (Sat): 44 umbrellas sold; Day 7 (Sun): CLOSED (0 store units).
            // On Day 8 morning (or Day 7 night), Day 7 MUST be skipped so Avg = (42 + 44) / 2 = 43, Target = 129, Order = 129 - 31 = 98!
            var history = new[]
            {
                new OpenDaySale(dayNumber: 5, storeTotalUnits: 150, itemUnitsSold: 42),
                new OpenDaySale(dayNumber: 6, storeTotalUnits: 160, itemUnitsSold: 44),
                new OpenDaySale(dayNumber: 7, storeTotalUnits: 0, itemUnitsSold: 0), // Closed Sunday!
            };

            var umbrella = Wholesaler.EvaluateReplenishment(
                storeAddress: "3rd Street 35",
                item: "ba:itemname_umbrella",
                currentDay: 8,
                gapDays: 2,
                onHand: 31,
                liveFixtureCap: 150,
                history: history);

            Assert.Equal(42, umbrella.OpenDay1Sales);
            Assert.Equal(44, umbrella.OpenDay2Sales);
            Assert.Equal(43, umbrella.AvgDailyBurn);
            Assert.Equal(129, umbrella.TargetStock);
            Assert.Equal(98, umbrella.OrderAmount);

            // Paper bags on the same store (even if itemUnitsSold == 0 for paperbag, derives from storeTotalUnits * 0.50 -> 75 & 80 -> avg 78 -> Target 234):
            var bags = Wholesaler.EvaluateReplenishment(
                storeAddress: "3rd Street 35",
                item: Wholesaler.PaperBag,
                currentDay: 8,
                gapDays: 2,
                onHand: 60,
                liveFixtureCap: 250,
                history: history);

            Assert.True(bags.OrderAmount > 100);
        }

        [Fact]
        public void Wholesaler_shrinks_order_amount_when_sales_contract_so_storage_does_not_overflow()
        {
            // Sales contract from 40/day down to 14/day (16 and 12). TargetStock on 2-day truck = 14 * 2 * 1.5 = 42.
            // With 30 already on the shelf, OrderAmount shrinks to 42 - 30 = 12!
            var history = new[]
            {
                new OpenDaySale(dayNumber: 8, storeTotalUnits: 60, itemUnitsSold: 16),
                new OpenDaySale(dayNumber: 9, storeTotalUnits: 50, itemUnitsSold: 12),
            };

            var calc = Wholesaler.EvaluateReplenishment(
                storeAddress: "3rd Street 35",
                item: "ba:itemname_umbrella",
                currentDay: 10,
                gapDays: 2,
                onHand: 30,
                liveFixtureCap: 150,
                history: history);

            Assert.Equal(14, calc.AvgDailyBurn);
            Assert.Equal(42, calc.TargetStock);
            Assert.Equal(12, calc.OrderAmount);
        }
    }
}
```