# Overall Growth Strategy — Day 0 to Stage 4 Compounding (Exhaustive Engineering Spec)

## 1. Executive Path (Day 0 $\rightarrow$ Stage 4 Master Sequence)

* **Day 1 (Store #1 Bootstrap — Story `10.3`):**
  1. Freeze and refill player Energy, Hunger, and Happiness at $100$ (`Needs.Hold`; care via sleep/eat if any need $\le$ **NeedFloor** $= 20\%$).
  2. Rent the lowest-rent available retail lot (`Rent.Choose` $\rightarrow$ `BuildingCommands.Command_RentBuilding`).
  3. Load starter layout `GiftShopWholesalers` (`Starter.Choose` $\rightarrow$ `LoadBusinessLayoutSet`), set hours `8–16` Monday–Saturday (Sunday closed), and clear `temporarilyClosed`.
  4. Assign Cleaner (`CleaningStationController.AssignEmployee` + `WorkShiftType.Cleaning`) and recalculate customer capacity from placed fixtures (`BusinessHelper.UpdateCustomerCapacity`).
  5. Sign recurring Wholesaler `DeliveryContract` (`WholesaleStoreManagerDialog.StartContract`) to `ba:street_firststreet 37` covering all $5$ layout items (`ba:itemname_cheapgift`, `ba:itemname_cheapflower`, `ba:itemname_sodacan`, `ba:itemname_paperbag`, `ba:itemname_umbrella`) at initial **SeedBurn** $= 25 \times$ **CoverOpenDays** $= 2 \implies 50$ units ($100$ `paperbag`), scheduled for `nextDeliveryDay = currentDay + 1`.
  6. Hire and schedule Cashier (`EmployeeHelper.HireCandidate` + `CashRegisterController.AssignEmployee` on `SelfServiceEmployee` station).
  7. Raise every item's retail price off default by **PriceStep** $= +5\%$ (`Prices.Decide`, respecting `MinViablePrice` for `specialty`/`food` in Stage 1).
* **Days 2–5 (Store #1 Stabilization, Closed-Loop Reforecast & Ready Proof — Story `10.3`):**
  1. On each `day.rolled`, run `WholesalerBridge.Maintain`: reforecast each item's `MeasuredBurn` from the last **BurnLookbackDays** $= 3$ completed open days (`MinBurnFloor = 5`), cap `item.amount` by **ShelfHeadroom** ($\text{FixtureCap} - \text{OnHand}$), and pull `nextDeliveryDay` to `day + 1` only when `OnHand < MeasuredBurn * CoverOpenDays` (otherwise defer to `day + HoldDeliveryDays` $= 2$).
  2. Evaluate `StoreHealth.Assess` (G1 Open, G2 Stocked, G3 Staffed, G4 Selling, G5 Profitable) from **completed open days** (ignoring morning $00\text{:}00$ zeros and scheduled closed Sundays).
  3. Step retail prices up $+5\%$/day in local monopoly neighborhoods or undercut local neighborhood rivals down to `MinViablePrice`.
  4. Repay any initial $\$500$ bootstrap loan once `Available(cash, dailyCost) > RepayFloor` ($\$5,000$) while `!Ready`.
  5. Unlock expansion (`StoreHealthGate.ExpansionAllowed == true`) only after Store #1 logs `ready=true` across **ProfitProofDays** $= 3$ completed open days and `BareRentedLotCount == 0`.
* **Days 6–25 (Stage 1 Scale — Stores #2 and #3 — Story `10.4`):**
  1. When `ExpansionAllowed == true`, `BareRentedLotCount == 0`, and `goodsStoreCount < Stage2Stores` ($3$), `Open.Decide` picks the highest-profit Stage-1 starter type (`ba:businesstype_giftshop`, `ba:businesstype_florist`, or `ba:businesstype_fastfoodrestaurant`).
  2. `Where.Decide` selects a layout-compatible retail lot: a street with $\ge$ **ClusterMin** $= 2$ owned stores of that family wins; otherwise max `TrafficIndex / DailyRent` with `Cost <= cash - Reserve`.
  3. **Financial Discipline:** If `Available(cash, dailyCost) < Stage1StoreNeed` ($\$15,000$), check whether organic profit covers the shortfall within **SelfFundDays** $= 5$ days (`Shortfall <= NetDailyProfit * 5`). If yes, wait on profits; if no (and a valid site exists), borrow one $\$5\text{k}/\$1\text{k}$ chunk under **LoanCover** ($50\%$).
  4. **Remote Fit-Out (`TryFitOut`):** On `day.rolled`, fit out Store #2 directly on its `BuildingRegistration` (`layout` $\rightarrow$ `ops` $\rightarrow$ `capacity` $\rightarrow$ `contract` $\rightarrow$ `cashier`) without requiring the player camera inside Store #2. Block all further `open`/`where` actions while Store #2 is fitting out (`BareRentedLotCount >= 1`) or stabilizing (`!Ready`).
  5. Repeat for Store #3 until **HealthyGoodsStores** $= 3$ (`ready=true` on all $3$).
* **Days 26–45 (Stage 1 $\rightarrow$ Stage 2 De-Leveraging & Cash Accumulation):**
  1. At `goodsStoreCount == Stage2Stores` ($3$), `Open.Decide` returns `None` and clears any `PendingOpen`.
  2. `Money.Decide` sweeps cash above `Reserve + RepayFloor` ($\$5,000$) to pay off all remaining Stage-1 bank loans, then compounds debt-free cash to **Stage2Cash** $= \$150,000$.
* **Stage 2 (First HQ, Single-Family Warehouse #1 & Zero-Downtime Handover — Story `10.5`):**
  1. At `cash >= Stage2Cash` ($\$150,000$) and `HealthyGoodsStores >= Stage2Stores` ($3$), `Stage.Decide` advances to Stage $2$.
  2. Rent HQ and create `PricingManagerPlan` (`PricingManagersPlanList.AddPlan`).
  3. Rent Warehouse #1 (`shelves > 0`) as a **single-family** hub for the primary Stage-1 family (`specialty`).
  4. Buy Freight Truck #1 (`ContractVehicleForSale.Order`) + Driver #1 (`VehicleSlot.AssignVehicle`) into Slot 1, then fill Slot 2 (`GarageSlots = 2`) as first spend after reserve.
  5. Hire and assign baseline HQ seats: $1$ Logistics Manager, $1$ Purchasing Agent, $1$ HR Manager, $1$ Headhunter (`HrManagerPlan.AssignEmployee`).
  6. Purchasing Agent orders **RefillDays** $= 7$ days of family burn (`(7 * FamilyBurn) - OnHand`) from an Importer (`ChangeAssignedWarehouse` + `StartOrder`).
  7. **Zero-Downtime Handover:** Keep Store #1–3 Wholesaler `DeliveryContract` lines active until Warehouse #1 physical stock reaches $\ge$ **RefillDays** ($7$) of burn. Then attach Stores #1–3 to `LogisticsManagerPlan` (`AddDestination`) and drop **only** the `specialty` + `paperbag` lines from their `DeliveryContract` (`DeliveryContract.Remove`), leaving cross-family `ba:itemname_sodacan` (`food`) on its residual wholesaler contract.
* **Stage 3 (Scale to 5+ Supplied Stores, Clusters, Service/Office Pivot & Multi-Warehouse — Story `10.6`):**
  1. Expand to $\ge$ **Stage3Supplied** $= 5$ warehouse-supplied stores (`Stage.Decide == 3`).
  2. Clone golden layouts (`Layout.Decide` picks highest interior score for that type and size).
  3. Prefer **ClusterMin** $= 2$ streets (`Where.Decide`) and price per neighborhood (`Prices.Decide` withStage-2 Importer margin floors: $50\%$ `food`, $55\%$ `specialty`, $45\%$ `durables`).
  4. When filled warehouse trucks exceed **TruckBusy** $= 85\%$ load, `Open.Decide` pivots to `service` or `office` businesses (`law`, `web`, `graphics`, `hair`, `gym`) which require $0$ trucks.
  5. Open a **2nd warehouse** for the same family only when both trucks exceed **TruckBusy** ($85\%$) OR `Ordered == true && OnHand < RefillDays * Daily`. Open a new single-family warehouse when entering a 2nd goods family (`food` or `durables`).
  6. Scale managers by span: $1$ Logistics + $1$ Purchasing per warehouse, $1$ HR per **HrSpan** $= 25$ employees, $1$ Headhunter per **HuntSpan** $= 5$ open roles.
  7. Shut down a losing goods store (`ShutdownBusiness`) only after $\ge$ **FixDays** $= 7$ supplied+staffed days and $\ge$ **RedDays** $= 7$ consecutive losing days.
* **Stage 4 (Real Estate Compounding to `win.reached` & Continuous Play — Story `10.7`):**
  1. At `cash >= Stage4Cash` ($\$2,000,000$), `suppliedStores == goodsStores`, and `emptySlots == 0`, `Stage.Decide` enters Stage $4$.
  2. Buy rented buildings (`BizManPresentation.SendBuyBuildingOffer`) with payback `Price / DailyRent <= PaybackMax` ($365$ days) in strict order: `headquarters` $\rightarrow$ `warehouse` $\rightarrow$ `retail` (shortest payback first).
  3. Train employees to $100\%$ skill (`HrManagerPlan.TrainEmployees`).
  4. Cross **WinNetWorth** $= \$100,000,000$ $\rightarrow$ log `win.reached` once (`Win.Record`) $\rightarrow$ continue compounding in Stage 4 until Autopilot is toggled off or F10 is pressed.

---

## 2. Detailed Per-Stage Playbook (Stages 1–4)

| Dimension | Stage 1: Bootstrap (`10.3`–`10.4`) | Stage 2: First HQ & Warehouse (`10.5`) | Stage 3: Cluster & Fleet Scale (`10.6`) | Stage 4: Real Estate Compounding (`10.7`) |
| :--- | :--- | :--- | :--- | :--- |
| **Enter Gate** | New game (`day >= 1`) | `cash >= $150,000` AND `HealthyGoodsStores >= 3` | `plans >= 1` live WH plan AND `suppliedStores >= 5` | `cash >= $2,000,000` AND `suppliedStores == goodsStores` AND `emptySlots == 0` |
| **Rent vs. Buy** | **Rent only** (`<= 3` small retail lots; `BareRentedLotCount == 0`) | **Rent only** (1 HQ, 1 shelved WH, retail lots #4–#5 after handover) | **Rent only** (cluster retail/service/office lots + new single-family WHs) | **Buy owned/rented buildings** (`Payback <= 365d`: HQ $\rightarrow$ WH $\rightarrow$ Retail) + Rent new expansion lots |
| **What to Open (`Open.Decide`)** | Wholesaler starter goods only: `giftshop` ($\$40$), `fastfoodrestaurant` ($\$30$), `florist` ($\$25$) | Same primary family (`specialty`) for Stores #4–#5 to reach `5` supplied stores | Highest profit type; if `truckLoad > 85%`, open `service`/`office` (`law`, `web`, `hair`, `gym`); 2nd store of a goods family requires a WH for that family | Same as Stage 3 |
| **Where to Open (`Where.Decide`)** | Compatible retail lot (`GetOrLoadBusinessLayoutSet != null`); `ClusterMin = 2` street wins, else max `Traffic / DailyRent` | Same (`ClusterMin = 2` street first, else max `Traffic / DailyRent`, `Cost <= cash - Reserve`) | Same (`ClusterMin = 2` street cluster priority) | Same |
| **Layout (`Layout.Decide`)** | Starter layout (`GiftShopWholesalers`, `FloristWholesalers`, `FastFoodKabob`) | Owned layout of same type & size with highest interior score (`Score > 0`), else starter layout | Same (never clone across different building sizes) | Same |
| **Pricing (`Prices.Decide`)** | Leave default (`+5%`); step up $+5\%$/day while units hold & sat $\ge 75\%$; floor at Stage-1 `MinViablePrice` ($35\%\text{–}40\%$ margin) | Create `PricingManagerPlan` (grouped by neighborhood or written per store); Stage-2 Importer `MinViablePrice` ($45\%\text{–}55\%$ margin); undercut neighborhood rivals by $2\%$ when above floor | Same neighborhood-scoped pricing + rival undercut | Same |
| **Supply Mode (`Supply.Decide`)** | Wholesaler `DeliveryContract` per store; daily `Maintain` reforecasts `LineAmount` from 3d measured burn capped by `ShelfHeadroom` | Single-family WH #1 (`specialty` + `paperbag`); Importer orders `7d` burn; zero-downtime handover drops held items only (`sodacan` stays on wholesaler) | 1 family per WH; drain mixed WHs; Importer `RefillDays = 7` | Same, with `suppliedStores == goodsStores` enforced |
| **HQ & Agents (`Managers.Decide`)** | None | `PricingPlan` first $\rightarrow$ $1$ Logistics, $1$ Purchasing, $1$ HR, $1$ Headhunter | $1$ Logistics + $1$ Purchasing per WH; $1$ HR per $25$ staff; $1$ Headhunter per $5$ open roles | Same + `TrainEmployees` to $100\%$ skill |
| **Warehouse & Trucks (`Trucks` / `Warehouses`)** | None | 1 shelved WH; fill Slot 1 then Slot 2 (`2` Freight Trucks + `2` Drivers) | 2 trucks/WH (1st spend after reserve); 2nd WH only on `trucks > 85%` or `Ordered && OnHand < 7d` | Same (`emptySlots == 0` required) |
| **Money Posture (`Money.Decide`)** | `Reserve = 7d * DailyCost`; borrow $\$5\text{k}/\$1\text{k}$ chunks only on actionable shortfall $> 5\text{d}$ profit under $50\%$ `LoanCover`; repay above `Reserve + $5k` when not expanding | No bootstrap loans; repay any remaining debt when `InterestRate > bestPaybackRate`; spend $\le 25\%$ above reserve | Self-funded inside `DaySpend = 25%` above `ReserveDays = 7` | Spend $\le 25\%$ above reserve on real estate & new stores |
| **Closing (`Close.Decide`)** | Never close before `FixDays = 7` (supplied+staffed) + `RedDays = 7` (profit $< 0$) | Same; never close HQ or Warehouse | Same; never close HQ, Warehouse, Office, or Service | Same |
| **Exit Gate** | `HealthyGoodsStores >= 3` (`ready=true` on all $3$, `bareLots == 0`) AND `cash >= $150,000` | `plans >= 1` AND `suppliedStores >= 5` (all Ready) | `cash >= $2,000,000` AND `suppliedStores == goodsStores` AND `emptySlots == 0` | Continuous (`win.reached` logged once at $\$100\text{M}$) |

---

## 3. Master Decision Tables (All 6 Core Decisions)

### Table 3A — Rent vs. Buy Decision Matrix

| Building Kind | Stage 1 | Stage 2 | Stage 3 | Stage 4 (`cash >= $2M`, `supplied == goods`, `emptySlots == 0`) |
| :--- | :--- | :--- | :--- | :--- |
| **Headquarters (`headquarters`)** | Forbidden | **Rent 1 HQ** on Stage 2 entry | Keep rented HQ | **Buy Priority #1 (`KindRank = 1`):** Buy if `Price / DailyRent <= 365` and `Price <= cash - Reserve` |
| **Warehouse (`warehouse`)** | Forbidden | **Rent Warehouse #1** (`shelves > 0`) | **Rent** on new family entry (`enter`/`stock`) or overflow (`trucks`/`short`) | **Buy Priority #2 (`KindRank = 2`):** Buy by shortest payback $\le 365\text{d}$ after HQ is owned |
| **Retail / Service / Office (`retail`)** | **Rent** Stores #1–#3 (one at a time; `bareLots == 0`, prior stores `Ready`) | **Rent** Stores #4–#5 after WH #1 handover | **Rent** on cluster streets (`ClusterMin = 2`) or max `Traffic / DailyRent` | **Buy Priority #3 (`KindRank = 3`):** Buy by shortest payback $\le 365\text{d}$; continue **Renting** new expansion lots |

### Table 3B — Wholesaler Door Delivery vs. Warehouse Logistics Handover Matrix

| Stage | Store Item Family | Warehouse On-Hand Stock (`OnHand(W, item)`) | Active Plan Stop (`PlannedWarehouse`) | `Supply.Decide` Output | Contract & Plan Execution |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Stage 1** | Any (`specialty`, `food`, `paperbag`) | $0$ (No warehouse) | `""` | `source="wholesaler", DropContract=false` | Sign & maintain Wholesaler `DeliveryContract` (`Maintain` resizes to `LineAmount` daily). |
| **Stage 2+** | Primary WH Family (`specialty` + `paperbag`) | $< \text{RefillDays} \times \text{StoreBurn}$ (Priming) | `""` | `source="wholesaler", DropContract=false` | **Hold Handover:** Keep Wholesaler `DeliveryContract` active while Purchasing Agent primes Warehouse #1. |
| **Stage 2+** | Primary WH Family (`specialty` + `paperbag`) | $\ge \text{RefillDays} \times \text{StoreBurn}$ (Primed) | `"W1"` (or attaching now) | `source="warehouse", warehouse="W1", DropContract=true` | **Execute Handover:** Call `AddDestination` on `W1` `LogisticsManagerPlan` and remove covered items from `DeliveryContract` (`DeliveryContract.Remove`). |
| **Stage 2+** | Cross-Family Residual (`ba:itemname_sodacan` = `food` in Gift Shop) | $0$ (No `food` warehouse yet) | `""` | `source="wholesaler", DropContract=false` | **Residual Door Delivery:** Keep `sodacan` on the store's `DeliveryContract`; never add `food` to `specialty` Warehouse #1. |
| **Stage 3+** | Secondary Family (`food` / `durables`) after `W2` opens & primes | $\ge \text{RefillDays} \times \text{StoreBurn}$ in `W2` | `"W2"` | `source="warehouse", warehouse="W2", DropContract=true` | Move `food` stores (and residual `sodacan` lines) onto `W2` `LogisticsManagerPlan` and drop remaining wholesaler contracts. |

### Table 3C — First Warehouse Family Choice & Mixed vs. Single-Family Resolution

| Scenario | Detection Predicate | Mandatory Action |
| :--- | :--- | :--- |
| **Selecting Family for Warehouse #1 (Stage 2 Entry)** | Count owned Stage-1 stores by `StoreFamily.For(type)` | Pick the family with the most Stage-1 stores (`specialty` for Gift Shop/Florist). Warehouse #1 is **100% single-family** (`specialty` + non-family `paperbag`). |
| **Bot-Created Warehouses (Stages 2–4)** | `Warehouses.Decide(family, ...)` | Every warehouse is assigned **one** retail family (`food`, `durables`, `specialty`, or `service`). Never assign two retail families to the same warehouse. |
| **Pre-Existing Mixed Warehouse on a Loaded Save** | `Advisor.WarehouseFamilyMix(holds)` returns $\ge 2$ families on Warehouse $W$ | **1)** `Warehouses.KeepFamily` retains the family with the largest unit count (ties keep later/rank order). **2)** `Restock.Arrange` emits `warehouse.drain` (`ChangeAssignedWarehouse`) to direct new imports of the minority family to its own home warehouse. **3)** `Logistics.Review` / `StoreConnect` routes minority-family store destinations to the minority home. **4)** Mixed warehouse sells down leftover minority boxes to $0$ (no game API moves boxes between warehouses). |

### Table 3D — Upgrade / Second Truck / Second Warehouse Triggers

| Trigger | Exact Condition | Action & Priority |
| :--- | :--- | :--- |
| **1. Empty Garage Slot 1 or Slot 2 on Delivering Warehouse** | `delivering.Contains(slot.Warehouse) && !slot.Filled && cash >= Reserve + truckCost` | **#1 Spend After Reserve (`Trucks.Decide`):** Order Freight Truck (`ContractVehicleForSale.Order`), assign to slot (`VehicleSlot.AssignVehicle`), and assign Driver (`ba:skill_deliverydriver`). |
| **2. Normal Warehouse Stock Dip (Unordered)** | `home.OnHand < RefillDays * home.Daily && !home.Ordered` | **Place Importer Order (`Restock.Order`):** Order `(7 * Daily) - OnHand` via Purchasing Agent (`StartOrder`). **Do NOT open a 2nd warehouse.** |
| **3. Warehouse Stock Short While Order Is Already Active** | `home.OnHand < RefillDays * home.Daily && home.Ordered && home.Daily > 0` | **Open 2nd Warehouse for Same Family (`Warehouses.Decide` $\rightarrow$ `action="second", reason="short"`):** Claim next empty warehouse with pallet shelves (`shelves > 0`). |
| **4. Both Garage Slots Filled & Trucks Overloaded** | `FilledSlots(W) == 2 && truckLoadFraction > TruckBusy` ($0.85$) | **Dual Action:** **1)** `Open.Decide` switches next business opening to `service`/`office` (`law`, `web`, `hair`, `gym`). **2)** `Warehouses.Decide` claims a 2nd shelved warehouse for that family (`action="second", reason="trucks"`). |
| **5. Entering a New Goods Family in Stage 3+** | `enteringFamily == true` (or `hasStockWithoutHome == true`) AND no warehouse holds `family` | **Open Home Warehouse (`Warehouses.Decide` $\rightarrow$ `action="home", reason="enter"`):** Claim next empty shelved warehouse (`shelves > 0`) before opening a 2nd store of that family. |

### Table 3E — HQ Agent Hiring & Scaling Order (`Managers.Decide` / `HqPlans.Assign`)

| Order | Role (`ManagerPick.Role`) | Action | Trigger Predicate (`Stage >= 2`) | Target Count Formula | Recorded Game API Call |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **1** | `pricing` | `plan` | `!pricingPlanExists` | $1$ plan on HQ | `PricingManagersPlanList.AddPlan` |
| **2** | `logistics` | `hire` / `assign` | Any warehouse $W \in \text{warehouses}$ has `!HasSeat(seats, "logistics", W)` | $1$ per warehouse | `EmployeeHelper.HireCandidate` + `HrManagerPlan.AssignEmployee` |
| **3** | `purchasing` | `hire` / `assign` | Any warehouse $W \in \text{warehouses}$ has `!HasSeat(seats, "purchasing", W)` | $1$ per warehouse | `EmployeeHelper.HireCandidate` + `HrManagerPlan.AssignEmployee` |
| **4** | `hr` | `hire` / `assign` | `CountSeats(seats, "hr") < max(1, ceil(employees / HrSpan))` | $1$ per **HrSpan** ($25$) employees | `EmployeeHelper.HireCandidate` + `HrManagerPlan.AssignEmployee` |
| **5** | `headhunter` | `hire` / `assign` | `CountSeats(seats, "headhunter") < max(1, ceil(openRoles / HuntSpan))` | $1$ per **HuntSpan** ($5$) open roles | `EmployeeHelper.HireCandidate` + `HrManagerPlan.AssignEmployee` |

### Table 3F — Store Closing & Contract Termination Rules (`Close.Decide` / `StoreConnect`)

| Entity | Condition | Action |
| :--- | :--- | :--- |
| **Cost Centers & Non-Goods (`warehouse`, `headquarters`, `office`, `service`)** | `kind != "goods"` | **Never close** (`ClosePick.None()`). |
| **Unsupplied Goods Store** | `kind == "goods" && !supplied` | Return `ClosePick("connect", true)` — heal supply first; do not advance close timer. |
| **Unstaffed Goods Store** | `kind == "goods" && supplied && !staffed` | Return `ClosePick("hire", true)` — hire staff first; do not advance close timer. |
| **Supplied & Staffed Goods Store Within Grace Window** | `supplied && staffed && (suppliedDays < 7 || staffedDays < 7 || redProfitDays < 7)` | Return `ClosePick.None()` — keep open; allow price/marketing tuning to recover profit. |
| **Chronic Losing Goods Store** | `supplied && staffed && suppliedDays >= 7 && staffedDays >= 7 && redProfitDays >= 7` | Return `ClosePick("close", true)` $\rightarrow$ write `snapshot.taken` $\rightarrow$ call `BusinessHelper.ShutdownBusiness`. |
| **Wholesaler Contract Line (`DeliveryContractItem`)** | Stage $2+$ AND store is connected to warehouse $W$ for item $I$ AND `OnHand(W, I) >= max(1, StoreBurn(I))` | Remove item $I$ from `contract.items`; if `contract.items.Count == 0`, call `DeliveryContract.Remove`. |

---

## 4. Complete Named Threshold Registry

| Threshold Name | Value | Owning File | Testable Contract |
| :--- | :--- | :--- | :--- |
| **WinNetWorth** | $\$100,000,000$ | `TopBar.cs` / `Win.cs` | First crossing logs `win.reached` once; Autopilot stays on in Stage 4 |
| **SimHorizonDays** | $365$ | `Noise.cs` | Epic 8 Monte Carlo scoring window |
| **CalibrateTol** | $0.20$ | `Calibrate.cs` | Max relative empire profit error at Day 1014 |
| **DemandNoise** / **RivalNoise** / **HireNoise** | $0.10$ / $0.05$ / $0.10$ | `Noise.cs` | Monte Carlo sales, rival, and wage variance bounds |
| **PolicyWorstTol** | $0.98$ | `Policy.cs` | Policy worst trial must be $\ge 98\%$ of policy median |
| **Stage2Cash** / **Stage2Stores** | $\$150,000$ / $3$ | `Stage.cs` | Cash and healthy goods store gates to enter Stage 2 |
| **Stage3Supplied** | $5$ | `Stage.cs` | Supplied goods stores on a live warehouse plan to enter Stage 3 |
| **Stage4Cash** | $\$2,000,000$ | `Stage.cs` | Cash gate (with `supplied == goods` and `emptySlots == 0`) for Stage 4 |
| **ProfitProofDays** / **SatisfactionFloor** / **StuckDays** | $3$ / $50\%$ / $3$ | `StoreHealth.cs` / `Stall.cs` | G5 rolling open-day profit window, G3 staff satisfaction floor, and stall remedy window |
| **BareLotMax** / **FitOutStuckDays** | $1$ / $3$ | `FitOut.cs` / `Stall.cs` | Max bare rented lots before blocking `open`/`where`, and days without fit-out progress before pause |
| **CoverOpenDays** / **BurnLookbackDays** / **SeedBurn** / **MinBurnFloor** | $2$ / $3$ / $25$ / $5$ | `Wholesaler.cs` | Stage-1 delivery cover days, open-day burn lookback, Day-1 seed burn, and zero-sale burn floor |
| **DefaultFixtureCap** / **PaperBagFixtureCap** / **MinReplenishAmount** / **HoldDeliveryDays** | $100$ / $200$ / $10$ / $2$ | `Wholesaler.cs` | Shelf capacity caps (`ShelfHeadroom`), minimum delivery batch, and cadence deferral days |
| **RefillDays** / **GarageSlots** / **TruckBusy** | $7$ / $2$ / $0.85$ | `Supply.cs` / `Trucks.cs` / `Open.cs` | Warehouse import cover days, trucks per warehouse, and truck load threshold for service/office pivot |
| **ClusterMin** | $2$ | `Where.cs` | Same-family stores on a street to win site selection |
| **PriceStep** / **PriceSatisfied** / **UndercutStep** | $0.05$ / $0.75$ / $0.02$ | `Prices.cs` | Daily price step ($+5\%$), satisfaction floor ($75\%$), and local neighborhood rival undercut step ($2\%$) |
| **Stage1FoodMinMargin** / **Stage1SpecialtyMinMargin** / **Stage1DurablesMinMargin** | $0.35$ / $0.40$ / $0.30$ | `Prices.cs` | Stage-1 Wholesaler minimum viable gross margin floors by family |
| **Stage2FoodMinMargin** / **Stage2SpecialtyMinMargin** / **Stage2DurablesMinMargin** | $0.50$ / $0.55$ / $0.45$ | `Prices.cs` | Stage-2+ Importer minimum viable gross margin floors by family |
| **HrSpan** / **HuntSpan** | $25$ / $5$ | `Managers.cs` | Employees per HR Manager and open roles per Headhunter |
| **ReserveDays** / **DaySpend** / **LoanCover** | $7$ / $0.25$ / $0.50$ | `Money.cs` / `BankLoan.cs` | 7-day cash reserve, $25\%$ daily spend cap above reserve, and $50\%$ profit debt-service cover cap |
| **Stage1StoreNeed** / **SelfFundDays** / **RepayFloor** / **MinRepayStep** | $\$15,000$ / $5$ / $\$5,000$ / $\$1,000$ | `Money.cs` | Store opening working capital target, 5-day organic self-funding patience window, and loan repayment float/step |
| **PaybackMax** / **FixDays** / **RedDays** | $365$ / $7$ / $7$ | `RealEstate.cs` / `Close.cs` | Max building payback days, and supplied+staffed / red-profit days before closing a store |
| **NeedFloor** / **DemandDays** / **TheftMax** / **MarketingCover** | $20\%$ / $3$ / $0.01$ / $0.50$ | `Run.cs` | Player need floor, staff demand days, guard theft threshold ($1\%$), and marketing cost cover ($50\%$) |
| **HealCap** / **HealStrikeMax** | $10$ / $3$ | `Audit.cs` | Max daily self-heal actions and max failed heals before pausing |

---

## 5. Conflicts & Gaps Resolved (One Testable Sentence Each)

1. **Cross-Family `sodacan` in Gift Shops at Stage 2:** Stage 2's single-family `specialty` warehouse connects only `specialty` items (`cheapgift`, `cheapflower`, `umbrella`) plus `paperbag`, while `ba:itemname_sodacan` (`food`) remains on its Wholesaler `DeliveryContract` until a `food` warehouse opens in Stage 3.
2. **Unrented `RentPerDay == 0` Breaking `Where.Decide` (`where=0`):** `LiveDump.SiteCandidates` resolves positive asking rent and `TrafficIndex` and filters to lots where `BusinessLayoutSetHelper.GetOrLoadBusinessLayoutSet` supports the target business type.
3. **Loan Stacking When `sites.Count == 0` or Shortfall Is Self-Fundable:** `Money.Decide` sets `plannedSpend = 0m` when `sites.Count == 0` or `bareRentedLots > 0`, refuses to borrow when `Shortfall <= NetDailyProfit * SelfFundDays` ($5$), and actively repays debt above `Reserve + RepayFloor` ($\$5,000$) when not expanding.
4. **Day-36 Wholesaler Box Overflow (`Maintain` Never Lowering Amounts):** `WholesalerBridge.Maintain` resizes `item.amount` both up and down to `min(ShelfHeadroom, max(MinReplenishAmount, TargetStock - OnHand))` and defers `nextDeliveryDay` by **HoldDeliveryDays** ($2$) when shelves already hold $\ge 2$ days of measured burn.
5. **15 Bare Rented Lots With Only 1 Layout (`StarterBridge.Inside` Gate):** `StoreHealthGate.ExpansionAllowed` blocks `open` and `where` whenever `BareRentedLotCount >= BareLotMax` ($1$), and `TryFitOut` loads layouts and assigns shifts directly on the target `BuildingRegistration` on `day.rolled` without requiring the player camera inside Store $N$.
6. **Global `PricingManagerPlan` Overwriting Neighborhood Prices:** Prices are evaluated per `(store, item, neighborhood)` against local neighborhood competitors and family margin floors (`MinViablePrice`), never forcing a single price across different districts.

---

## 6. Out of Scope

* **No Runtime LLM or Reinforcement Learning:** All decisions are pure, deterministic C# methods locked by Tier-1 xUnit tests.
* **No Walking, Driving, or Hand-Carting:** All actions use recorded game-state and command methods in `doc/requirements/game-api.md`.
* **No Unverified Game Calls:** Unverified calls stay in discovery stories until verified in Build 3682.