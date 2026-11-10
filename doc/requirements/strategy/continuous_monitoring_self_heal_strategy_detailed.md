# Continuous Monitoring & Self-Heal Strategy — Day 1 to Stage 4 Compounding (Exhaustive Engineering Spec)

## 1. Executive Monitor Path (Always-On Closed-Loop Architecture)

The continuous monitor is the **Andon Cord** of the autopilot. It guarantees that the empire never expands on top of broken stores, never rents a 4th or 15th bare lot while Store #2 is still unfurnished, never floods Store #1 with oversized wholesaler deliveries, and never spends below the 7-day cash reserve.

* **Layer 0 — Per-Frame Guardrails (`ChaoticCeoMod.OnFrame`):**
  1. **F10 Emergency Stop:** If F10 is pressed while `Autopilot.Enabled == true`, immediately call `Controls.Pause()`, log `autopilot.paused version=... reason=f10`, and halt all autonomous actions and time skips.
  2. **Player Needs Hold (`LiveDump.HoldNeeds`):** Keep Energy, Hunger, and Happiness at $100$ (`Needs.Full`) and disable energy/happiness drain on `current.gameVariables`.
  3. **Day-1 New-Save Bootstrap:** Run Store #1's initial setup hooks once on Day 1, then hand over $100\%$ of multi-day operations to the `day.rolled` closed-loop sequence.
* **Layer 1 — `day.rolled` Sensors & Reforecast (Runs FIRST Every Morning):**
  1. Dump yesterday's `statements.csv`, `routes.csv`, and `warehouses.csv` (`DayRoll.Apply`) and run `SupplyNight.Run`.
  2. Run `WholesalerBridge.Maintain` across all active `DeliveryContract` entries: compute 3-day `MeasuredBurn`, cap `item.amount` by `ShelfHeadroom` (both raising and lowering), and pull or defer `nextDeliveryDay`.
  3. Run `LiveDump.TryFitOut` on any bare player-rented retail lot (`!FitProgress(b).CashierHired`) **remotely** against its `BuildingRegistration` without requiring the player avatar to stand inside the building.
* **Layer 2 — Store Readiness Gate (G1–G5) & Stall Detector (Runs SECOND Every Morning):**
  1. Assess every fitted goods store via `StoreHealth.Assess` using **completed open days** from `Days(current)` / `orderHistory` (ignoring midnight $00\text{:}00$ zeros and scheduled closed Sundays).
  2. Count `BareRentedLots` (player-rented retail lots where `!FitProgress(b).CashierHired`).
  3. Set `ExpansionAllowed = StoreHealthGate.ExpansionAllowed(ownedHealth, bareRentedLots)` $\rightarrow$ `false` whenever **any** owned store has `!Ready` **or** `bareRentedLots >= BareLotMax` ($1$).
  4. Run `Stall.Decide`: if any store fails the same G1–G5 gate for $\ge$ **StuckDays** ($3$) open days (or a bare lot makes $0$ fit-out progress for $\ge$ **FitOutStuckDays** $= 3$ days), execute one untried remedy (`reopen`, `restock`, `restaff`, `prices`, `fitout`) and wait **ProfitProofDays** ($3$); if all remedies for that gate are exhausted, pause with `autopilot.paused ... reason=stuck gate=<g> place=<store>`.
* **Layer 3 — Empire Audit & Self-Heal (`Audit.Check` — Runs THIRD Every Morning):**
  1. Populate `AuditGuard` with **real** `cash` (`PlayerHelper.GetPersonalWealth().cash`), **real** `dailyCost` (yesterday's total rent + wages + marketing + loan payments), and `truckCost`.
  2. Count all $8$ existing audit metrics (`unconnected`, `emptySlots`, `dry`, `mixed`, `unstaffed`, `missingPlans`, `cashBelowReserve`, `pinMismatch`) plus the operational/drift monitors (`bareRentedLots`, `contractOversize`, `stockReds`, `stage1OverCap`, `unpaidTax`, `unmetDemand`, `marketingOverspend`).
  3. Write `snapshot.taken` to `snapshots.log` before any heal, then execute up to **HealCap** $= 10$ heals inside **DaySpend** ($25\%$ above **ReserveDays** $= 7$). Pause with `autopilot.paused ... reason=failures` if **HealStrikeMax** $= 3$ heals fail in one day.
* **Layer 4 — Stage Playbook & Time Advance (`WinRun.Day` — Runs LAST):**
  1. Run `WinRun.Next` / `Playbook.Decide` (with `open` and `where` strictly gated on `ExpansionAllowed == true` and `stage == 1 ? goodsStoreCount < Stage2Stores : true`).
  2. Execute the chosen playbook step via `ExecutePlaybook`, check `Win.Record` at **WinNetWorth** ($\$100\text{M}$), upsert `telemetry.csv`, and call `TimeAdvanceBridge.Skip` (`FastForward` + `SkipTime("1d")`).

---

## 2. How to Fix the Remote Store $N$ Fit-Out Blocker (`15` Bare Lots, `1` Layout)

In the `1.16.7.5` soak, Store #2 was rented via `BuildingCommands.Command_RentBuilding`, but its fit-out never completed because `LiveDump.RunFitOutStep` called scene-dependent methods that only work when the player avatar is physically inside the building.

### 2.1 The 3 Off-Scene Blockers in `LiveDump.RunFitOutStep` & Their Exact Fixes

| Fit-Out Step | Current Off-Scene Blocker in `LiveDump.cs` / Bridges | Exact Fix for Remote `day.rolled` Execution |
| :--- | :--- | :--- |
| **Step 1: `layout`** | `LiveDump.RunFitOutStep` line 696 has `if (!StarterBridge.Inside(rented)) return null;`, and `StarterBridge.Live` calls `BusinessLayoutSetHelper.LoadBusinessLayoutSet(pick.Layout)` (which loads into `BuildingManager.Instance.buildingRegistration`—the building the player is currently standing in!). | **1)** Remove the `!StarterBridge.Inside(rented)` abort in `RunFitOutStep` when `StarterBridge.Inside(rented) == false` by either: **(a)** loading the layout set data (`BusinessLayoutSetHelper.GetOrLoadBusinessLayoutSet(pick.BusinessType, new BuildingSizeInfo(registration), pick.Layout, false)`) and applying its `itemInstances` / `retailPrices` directly onto `registration`, OR **(b)** temporarily binding `BuildingManager.Instance.buildingRegistration = registration` during `LoadBusinessLayoutSet(pick.Layout)` and restoring it in a `finally` block! |
| **Step 2: `ops` (Cleaner)** | `StoreOpsBridge.Live` looks for an active `CleaningStationController` MonoBehaviour in the loaded 3D scene (`Object.FindObjectsOfType<CleaningStationController>()`). When the player is outside Store #2, no 3D controller exists! | **Headless Schedule Fallback (`StoreOpsBridge.LiveRemote`):** When `Find<CleaningStationController>` returns `null` (player is outside the building), find the `ItemInstance` in `registration.itemInstances.Values` where `item.itemName == "ba:itemname_cleaningstation"`. Create/hire the AI cleaner via `EmployeeHelper.CreateAIEmployeeInstance("ba:skill_cleaning")` + `EmployeeHelper.HireCandidate(hired)`, add the `WorkShiftType.Cleaning` (`8–16`) shift to `registration.scheduleDays` with `itemInstanceId = cleaningItem.id`, and call `hired.UpdateWeeklyHoursAndDays(registration.scheduleDays)`! Because `LiveDump.SenseStaff` already reads `registration.scheduleDays` via `StoreStaff.FromShifts`, `g3` (`CleanerStaffed`) immediately turns `true`! |
| **Step 5: `cashier`** | `CashierBridge.Live` looks for an active `CashRegisterController` MonoBehaviour in the 3D scene (`Object.FindObjectsOfType<CashRegisterController>()`). When the player is outside Store #2, it returns `reason = "nostation"`. | **Headless Schedule Fallback (`CashierBridge.LiveRemote`):** When `Station(registration)` returns `null` (player is outside the building), find the cash register `ItemInstance` in `registration.itemInstances.Values` (e.g., `item.itemName.Contains("cashregister")` or `checkoutcounter`). Hire the AI cashier via `EmployeeHelper.CreateAIEmployeeInstance(Cashier.Skill)` + `EmployeeHelper.HireCandidate(hired)`, add the `WorkShiftType.Default` (`8–16`) shift to `registration.scheduleDays` with `itemInstanceId = registerItem.id`, and call `hired.UpdateWeeklyHoursAndDays(registration.scheduleDays)`! |

### 2.2 Locking the Bare-Lot Gate in `StoreHealthGate.ExpansionAllowed`

In `src/ChaoticCEO/StoreHealth.cs`, extend `StoreHealthGate.ExpansionAllowed` so that **any** bare rented lot (`bareRentedLots >= BareLotMax`) immediately blocks `Open.Decide` and `WinRun.Next`:

```csharp
    public static class StoreHealthGate
    {
        public const int ProfitProofDays = 3;

        public const float SatisfactionFloor = 50f;

        public const int BareLotMax = 1;

        public static bool ExpansionAllowed(
            IReadOnlyList<StoreHealth>? owned,
            int bareRentedLots = 0)
        {
            if (bareRentedLots >= BareLotMax)
            {
                return false;
            }

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
```

And in `LiveDump.GoodsStoreCount(GameInstance current)` (used for the `Stage.Stage2Stores = 3` cap in Stage 1), count **both** fitted goods stores **and** bare player-rented retail lots (`emptyLot == true`), so renting Lot #2 immediately increments `goodsStoreCount` from $1 \rightarrow 2$ and renting Lot #3 increments it to $3$ (capping Stage 1 at $3$ lots total even before Lot #3's layout finishes loading).

---

## 3. Complete Finding Catalog Table (16 Pure Predicates & Heals)

| ID | Finding Name | Pure Detection Predicate | Severity | Heal / Action | Evidence Line (`ceo.log`) | Stage |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **F1** | `pinMismatch` | `Audit.PinMismatch(guard.Build, LoadLog.PinnedBuild) == 1` | **Halt** | Abort all heals and time advance | `autopilot.halted ... reason=pin` | $1\text{–}4$ |
| **F2** | `cashBelowReserve` | `Audit.CashBelowReserve(guard.Cash, guard.DailyCost) == 1` (`cash < dailyCost * 7`) | **Hold Spend** | Set `SpendRoom = 0`; allow $\$0$-cost heals only | `audit.summary ... cashBelowReserve=1` | $1\text{–}4$ |
| **F3** | `bareRentedLots` | `BareRentedLotCount(current) >= BareLotMax` ($1$) | **Heal + Block Open** | Run `TryFitOut` step (`layout`/`ops`/`capacity`/`contract`/`cashier`); block `open`/`where` | `store.fitout ... place=<p> step=<s> chosen=true` | $1\text{–}4$ |
| **F4** | `fitOutStall` | Same bare lot at same `FitOutProgress` step for $\ge$ **FitOutStuckDays** ($3$) | **Pause** | Pause Autopilot (`Controls.Pause()`) | `autopilot.paused ... reason=stuck gate=fitout place=<p>` | $1\text{–}4$ |
| **F5** | `contractOversize` | Active `DeliveryContractItem` has `amount != Wholesaler.LineAmount(...)` or `NeedsReschedule` | **Heal ($\$0$)** | `WholesalerBridge.Maintain` resizes `item.amount` (up or down) and updates `nextDeliveryDay` | `contract.rescheduled ...` / `contract.resized ...` | $1\text{–}4$ |
| **F6** | `stockReds` (G2) | `StoreHealth.Stocked == false` (`!FixtureCargoProof`) | **Heal + Block Open** | Stage 1: `Maintain` adds missing items & pulls delivery to `day + 1`. Stage 2+: `connect` / `order` | `store.health ... g2=false ready=false` | $1\text{–}4$ |
| **F7** | `unstaffed` (G3) | `Audit.Unstaffed(stores, staffed) > 0` or `!StoreHealth.Staffed` | **Heal + Block Open** | Hire & schedule missing Cashier or Cleaner (`HireCandidate` + `AssignEmployee`) | `audit.healed ... action=hire store=<s>` | $1\text{–}4$ |
| **F8** | `readyStall` (G1–G5) | `Stall.Decide(health, stuckDays, tried).Pause == true` ($\ge 3$ days stuck after remedies) | **Pause** | Pause Autopilot (`Controls.Pause()`) | `autopilot.paused ... reason=stuck gate=<g> place=<p>` | $1\text{–}4$ |
| **F9** | `stage1OverCap` | `stage == 1 && goodsStoreCount >= Stage.Stage2Stores` ($3$) | **Auto-Correct** | `WinRun.ClearPending()`; `Open.Decide` returns `None` | *(silent cap enforcement)* | $1$ |
| **F10** | `unconnected` | `Audit.Unconnected(stops, holds, needs, sales) > 0` | **Heal ($\$0$)** | `RedoBridge.Live` adds store to single-family WH plan (`AddDestination`) & drops held contract items | `audit.healed ... action=connect store=<s> warehouse=<w> item=<i>` | $2\text{–}4$ |
| **F11** | `emptySlots` | `Audit.EmptySlots(slots) > 0` | **Heal (1st Spend)** | Order Freight Truck (`Order`), assign to slot (`AssignVehicle`), assign Driver | `audit.healed ... action=slot warehouse=<w> slot=<k>` | $2\text{–}4$ |
| **F12** | `dry` | `Audit.Dry(api, levels) > 0` (`OnHand < 7 * Daily && !Ordered`) | **Heal** | Assign WH to Importer product (`ChangeAssignedWarehouse`) & start order (`StartOrder`) | `audit.healed ... action=order warehouse=<w> family=<f> amount=<a>` | $2\text{–}4$ |
| **F13** | `mixed` | `Audit.Mixed(holds) > 0` ($\ge 2$ families in one WH) | **Heal ($\$0$)** | Route minority family imports & stops to minority home (`ChangeAssignedWarehouse`) | `audit.healed ... action=drain family=<f> from=<w1> to=<w2>` | $2\text{–}4$ |
| **F14** | `missingPlans` | `Audit.MissingPlans(api, pricingPlan, seats, hired) > 0` | **Heal** | Create `PricingManagerPlan` (`AddPlan`) or assign open HQ seat (`AssignEmployee`) | `audit.healed ... action=plan role=<r>` | $2\text{–}4$ |
| **F15** | `unpaidTax` / `unmetDemand` | `TaxBillBridge.Due() > 0` or `StaffDemandBridge.Due()` has `unmetDays >= 3` | **Heal** | Call `PayCurrentTaxes`/`PayBackTaxes` and `EmployeeInstance.GiveBonus` | `tax.paid ...` / `demand.cleared ...` | $1\text{–}4$ |
| **F16** | `marketingOverspend` | Active campaign has `dailyCost >= gain * 0.50m` | **Heal ($\$0$)** | Cancel losing campaign (`CancelAllCampaignsFromAgency`) | `marketing.stopped ... street=<s> number=<n>` | $1\text{–}4$ |

---

## 4. Completed Open-Day `G4` / `G5` Assessment (`StoreHealth.FromCompletedDays`)

To fix the `10.3` / `10.4` morning-zero trap where `LiveDump.OwnedStoreHealth` evaluated `CustomersToday(building, current.Day)` at $00\text{:}00$ midnight (when today's sales are $0$) or included closed Sundays:

### 4.1 Pure Helper to Add to `src/ChaoticCEO/StoreHealth.cs`

```csharp
        public static (int lastCustomers, decimal lastRevenue, IReadOnlyList<decimal> recentProfits) CompletedOpenDays(
            string street,
            int number,
            int currentDay,
            IReadOnlyList<FinancialDay>? days,
            IReadOnlyList<(int day, int units)>? orderDays = null)
        {
            var profits = new List<decimal>();
            decimal lastSales = 0m;
            var lastDay = -1;

            if (days != null && !string.IsNullOrEmpty(street) && number > 0)
            {
                var ordered = new List<FinancialDay>();
                foreach (var d in days)
                {
                    if (d != null && d.Day < currentDay)
                    {
                        ordered.Add(d);
                    }
                }

                ordered.Sort((a, b) => a.Day.CompareTo(b.Day));
                foreach (var day in ordered)
                {
                    if (day.Rows == null)
                    {
                        continue;
                    }

                    foreach (var row in day.Rows)
                    {
                        if (row == null || row.Street != street || row.Number != number)
                        {
                            continue;
                        }

                        // Skip scheduled closed Sundays where sales == 0 and goods == 0.
                        var closedDay = row.Sales <= 0f && row.Goods <= 0f && (day.Day % 7 == 0);
                        if (closedDay)
                        {
                            continue;
                        }

                        profits.Add((decimal)row.Profit);
                        lastSales = (decimal)row.Sales;
                        lastDay = day.Day;
                    }
                }
            }

            var customers = lastSales > 0m ? 1 : 0;
            if (orderDays != null && lastDay > 0)
            {
                foreach (var entry in orderDays)
                {
                    if (entry.day == lastDay && entry.units > 0)
                    {
                        customers = entry.units;
                        break;
                    }
                }
            }

            return (customers, lastSales, profits);
        }
```

---

## 5. Copy-Pasteable Tier-1 xUnit Locks (`src/ChaoticCEO.Tests/StoreHealthTests.cs` & `ScaleTests.cs`)

```csharp
    [Fact]
    public void StoreHealth_completed_open_days_ignore_morning_zero_and_closed_sunday_for_g4_and_g5()
    {
        // Days 5 (Fri), 6 (Sat) profitable; Day 7 (Sun) closed (0 sales); Day 8 (Mon) profitable; Day 9 rolls at 00:00.
        var street = "ba:street_seventhavenue";
        var number = 30;
        var days = new[]
        {
            new FinancialDay(5, new[] { new StatementRow(street, number, 400f, 120f, 100f, 40f, 0f, 0f, 140f) }),
            new FinancialDay(6, new[] { new StatementRow(street, number, 450f, 135f, 100f, 40f, 0f, 0f, 175f) }),
            new FinancialDay(7, new[] { new StatementRow(street, number, 0f, 0f, 0f, 40f, 0f, 0f, -40f) }),
            new FinancialDay(8, new[] { new StatementRow(street, number, 500f, 150f, 100f, 40f, 0f, 0f, 210f) }),
            new FinancialDay(9, new[] { new StatementRow(street, number, 0f, 0f, 0f, 0f, 0f, 0f, 0f) }),
        };

        var completed = StoreHealth.CompletedOpenDays(street, number, currentDay: 9, days);
        Assert.True(completed.lastCustomers > 0);
        Assert.Equal(500m, completed.lastRevenue);
        Assert.Equal(new[] { 140m, 175m, 210m }, completed.recentProfits);

        var health = StoreHealth.Assess(
            new StoreFacts(
                street + " " + number,
                temporarilyClosed: false,
                GiftStock,
                FullCargo(),
                registerStaffed: true,
                cleanerStaffed: true,
                staffSatisfaction: 80f,
                scheduledOpen: true,
                dailyCustomers: completed.lastCustomers,
                dailyRevenue: completed.lastRevenue,
                recentOpenDayProfits: completed.recentProfits));

        Assert.True(health.Selling);
        Assert.True(health.Profitable);
        Assert.True(health.Ready);
    }

    [Fact]
    public void StoreHealthGate_blocks_expansion_whenever_any_bare_rented_lot_exists()
    {
        // Harvest: 1.16.7.5 soak rented 15 bare lots while Store #1 was Ready and Lot #2 had no layout.
        var ready = new[] { StoreHealth.Healthy("ba:street_seventhavenue 30") };

        Assert.True(StoreHealthGate.ExpansionAllowed(ready, bareRentedLots: 0));
        Assert.False(StoreHealthGate.ExpansionAllowed(ready, bareRentedLots: 1));
        Assert.False(StoreHealthGate.ExpansionAllowed(ready, bareRentedLots: 14));
    }
```