# Strategy rulebook

The autopilot plays a brand-new game with no hands on the keyboard and keeps running after the milestone. Each rule below is one testable sentence. A rule that names a threshold uses the name in bold. The number after it is the starting value. A story that changes one of those numbers locks that number with a Tier 1 test. Epic 8 does not search every named threshold. The longer notes live in [strategy/](strategy/). Those files are rules. Their sample code is not the mod.

## Win and boundary

- **Win:** net worth of **WinNetWorth** = $100,000,000. The first crossing logs `win.reached` once, with day and net worth, and the autopilot keeps running. An owner-approved target from the Epic 8 stage sim may replace that number. Changing the number needs the owner’s OK and an edit here.
- **Horizon:** **SimHorizonDays** = 365 is the Epic 8 window for scoring policies. It is not an in-game stop.
- **Calibration:** a sim fit at or before day **1000** and stepped to day **1014** stays within **CalibrateTol** = 20% relative empire profit of the real ledger at day 1014.
- **Noise:** seeded **DemandNoise** = 0.10 scales sales, **RivalNoise** = 0.05 is the fraction of sales lost, and **HireNoise** = 0.10 scales wages. The Monte Carlo window is **SimHorizonDays**.
- **Policy:** compare curve sets over **SimHorizonDays**. Keep the best median whose worst stays within **PolicyWorstTol** = 0.98 of that policy’s own median.
- **Stage sim:** run the four stages from day 1 over **SimHorizonDays** through the Epic 9 decides, with the named thresholds. Report whether **WinNetWorth** is reached at the median and at the worst. Tune keeps the best median that still passes **PolicyWorstTol**.
- **Continuous:** After the milestone the autopilot stays in Stage 4, compounding and self-healing, until Autopilot is turned off or F10 is pressed.
- **Boundary:** the mod acts through game-state and command calls only. Those calls rent, load a layout, sign or drop a wholesaler contract, hire, train, meet a demand, write plans, set store ops, take a loan, pay taxes, buy, decline a takeover, and advance time. It does not walk, drive, or push a hand cart.
- **Calls:** a call the autopilot uses is listed in [game-api.md](game-api.md) as recorded. An unverified call is a discovery story, not code.
- **Game updates:** a game build whose Managed DLL hashes differ from [game-build.md](game-build.md) halts the autopilot with `autopilot.halted reason=pin`.

## Stages

The stage is a pure decision, `Stage.Decide(day, cash, goodsStores, suppliedStores, warehouses, emptySlots, plans)`. The autopilot never runs a stage’s actions before that stage’s gate passes. A later stage keeps every rule of the earlier ones unless it says otherwise. Stage 4 passes when cash is at least **Stage4Cash**, at least one goods store is open, supplied stores equal goods stores, and empty slots are 0. Zero goods stores stays Stage 1. Stage 4 does not end at **WinNetWorth**.

| Stage | Gate in | Supply | Stores and layouts | People and fleet |
| --- | --- | --- | --- | --- |
| 1 Bootstrap | New game | Wholesaler `DeliveryContract` to each store | Rent low-rent small lots. Open high-margin goods stores (gift, florist, fast food) from a starter layout | Cashiers hired directly. Loan funds stores 1 to 3 |
| 2 First HQ and warehouse | Cash at least **Stage2Cash** = $150,000 and at least **Stage2Stores** = 3 goods stores | One single-family warehouse. Stores of that family move from contracts to a logistics plan | Clone the best layout of that type and building size. Insurance starts at Bronze | Rent HQ. Logistics manager, purchasing agent, HR manager, headhunter, pricing plan. Truck 1 and driver 1 |
| 3 Scale | A warehouse plan is live and at least **Stage3Supplied** = 5 stores are on it | One family per warehouse, two trucks per warehouse | Street clusters. Services and offices when trucks are busy | Fill every garage slot. Managers scale by ratio |
| 4 Real estate | Cash at least **Stage4Cash** = $2,000,000, at least one goods store, supplied stores equal goods stores, and empty slots are 0. Stays on after **WinNetWorth** | Same, with rebalancing | Buy buildings by payback | Training to full skill |

## 1. What business to open

- The type with the larger expected daily gross profit wins. That profit is the sum over its items of (retail price − wholesale cost) × daily units, minus rent and wages. Ties keep the later type.
- Stage 1 opens only goods types whose stock a wholesaler sells. Gift shop, florist, and fast food are the starting list. An electronics store stays closed while the factory pipeline is off. Factory-locked phones, earbuds, headphones, and the smartwatch are not ordered.
- When filled garage slots carry more than **TruckBusy** = 85% of their daily cargo, the next opening is a service or office business (law, web, graphics, hair, gym). Offices and a gym have no shelves and no delivery contract. A hairdresser stocks `ba:itemname_haircareproduct` only.
- A new goods family needs a warehouse for that family before Stage 3 opens a second store of it.
- **Store readiness before expansion:** do not emit `open` or `where` while `PendingOpen` is set, or while any currently owned business fails the Store Readiness Gate (G1–G5). Store 1 has no prior store to check. Store N+1 waits until stores 1…N are Ready. Stage promotions use the same gate on every owned goods store.

### Store Readiness Gate (G1–G5)

Every owned business must pass all five before expansion or stage promotion:

| Gate | Name | Condition | If failed |
| --- | --- | --- | --- |
| G1 | Open | Not temporarily closed | Re-open, restaff register, or clear the closure cause |
| G2 | Stocked | Goods: every layout sellable item plus `ba:itemname_paperbag` has supply and morning stock > 0. Hairdresser: hair-care product on hand and a styling station. Office or gym: enough complete workstations, never cargo | Widen the order, or finish the workstation |
| G3 | Staffed | Required stations have an employee on shift | Hire or reassign cashier / cleaner |
| G4 | Selling | On scheduled open days: customers > 0 and revenue > 0 | Fix stock/staff first; then prices or marketing |
| G5 | Profitable | Most recent open day profit > 0, and rolling open-day profit over **ProfitProofDays** = 3 days > 0 | Hold expansion; tune contract size and prices |

Stage 1 → 2 also needs **HealthyGoodsStores** ≥ **Stage2Stores**, liquid cash ≥ **Stage2Cash**, and positive empire daily profit large enough to carry HQ + warehouse overhead. Stage 2+ warehouse handover keeps wholesaler contracts live until warehouse stock covers **RefillDays**.

### Live stage exits (Epic 10 ships)

| Story | Done when | Out of scope |
| --- | --- | --- |
| **10.3** | Store #1 G1–G5 true; `ready=true` for **ProfitProofDays**; stall pause when remedies exhaust | Store #2+, warehouse, `$100M` |
| **10.4** | Prior Ready → live rent + fit-out; 3 healthy goods stores. Waits on **14.2**, **14.3**, and **14.4** | HQ / warehouse, offices, tax |
| **10.5** | Stage2Cash + HealthyGoodsStores≥3; warehouse+truck+managers; wholesaler until stock ≥ **RefillDays**; then connect | Clusters / buy |
| **10.6** | ≥5 healthy supplied; **ClusterMin**; **TruckBusy** → service/office; Close after **FixDays**+**RedDays** | Real-estate buy |
| **10.7** | Stage4Cash + all Ready + emptySlots=0; live buy by payback; `win.reached` once | LLM / RL |

### Stall rule

A gate that stays false for **StuckDays** = 3 open days runs that gate’s *If failed* remedy from the G1–G5 table, one remedy at a time, then waits **ProfitProofDays** to measure. When every remedy for that gate has been tried and it is still false, the autopilot logs `autopilot.paused reason=stuck gate=<g> place=<store>` and clears Autopilot. It never holds silently. `store.health` carries the numbers behind g4 and g5 (customers, revenue, recent open-day profits). A stall a human resolves becomes a new remedy line here plus a named Tier 1 test before the next soak.

## 2. Where

- A street with at least **ClusterMin** = 2 stores of that family wins.
- Otherwise the building with the larger traffic index per dollar of daily rent wins.
- A tie keeps the later building.
- A building over the cash reserve is not a choice.
- A neighborhood on wholesaler supply holds at most **WholesalerDistrictMax** = 3 businesses. Store 4 goes to another neighborhood. After that family’s importer is running, the cap is **PreGoldDistrictMax** = 7 until Gold insurance is on, then the cap is off.

## 3. Which layout

- There is one golden layout per business type and building size.
- In Stage 1 it is the starter layout from story 13.2. That layout works on a fresh profile with zero saved layouts.
- From Stage 2 it is the store of that type and size with the larger interior score (`Layout.Lock`). A tie keeps the later layout.
- A size with no golden layout uses the starter layout for that size. An A1 layout is never loaded onto a B1. The key is the building size code.

## 4. Prices

- No item stays at the default price.
- Raise an item by **PriceStep** = 5% while its daily units hold and pricing satisfaction stays at or above **PriceSatisfied** = 75%.
- When units drop or satisfaction falls below that, step back to the price with the larger (price − wholesale cost) × units (`ItemSoldPerPriceEntry`, story 5.1).
- Prices are written through `PricingManagerPlan.SetPrice`.

## 5. Where stock comes from

- **Stage 1:** each goods store has a wholesaler contract for its items. Nothing is hand-carted. Burn is the average units sold on the last **LookbackOpenDays** = 2 completed open days, or **SeedBurn** = 25 when that item has no open day. A closed day is skipped. An open day that sold none of the item stays at **MinBurnFloor** = 5. `ba:itemname_paperbag` with no bag sales uses **PaperBagSeedBurn** = 50 on the first drop, and otherwise the store’s open-day orders. A gap of 1 is sized as **DefaultGapDays** = 2 and the date stays. A gap of 2 or 3 stays when units on hand still cover burn × gap × 1.5. A thinner shelf pulls that truck to tomorrow and sizes it for 2 days. A truck due today stays today and is sized for **DefaultGapDays** = 2. An open day below **MinBurnFloor** is left out when another open day sold at least that many. A missed day is pulled to today. A gap longer than 3 becomes **DefaultGapDays** = 2 and that drop is replaced for the next day. Target stock is the stand floor plus the ceiling of burn × **SalesWindowMultiplier** = 2. The window does not multiply that sales piece. A retail line is **RetailStands** = 2 and **StandCap** = 100, so the floor is 200. One display stand adds no stand floor. A paper bag's stand is one cash register, which holds **RegisterCap** = 200, and that capacity is the bag floor. The truck-pull check stays burn × gap × 1.5. The line is that target minus units on hand, at least **MinReplenishAmount** = 10, and never above the shelf space left. A closed day does not push the truck out. The line is logged as `contract.cover`. Detail: [strategy/logistics_stocking_strategy_1_5x_buffer_2_open_day_moving_average.md](strategy/logistics_stocking_strategy_1_5x_buffer_2_open_day_moving_average.md).
- **Stage 2 on, downstream:** a warehouse supplies every store of its family through one logistics plan with stock targets.
- **Stage 2 on, upstream:** a purchasing agent orders that family from an importer when warehouse stock falls below **RefillDays** = 7 days of the sales that home will serve.
- The first order of a new home uses the sales of that family’s stores that still sit on a wholesaler. After those stores connect, later orders use their connected burn.
- A wholesaler contract stays only for an item no warehouse holds. The contract for an item a warehouse holds is dropped (story 11.0).
- One store and one family have one supplier. A recurring plan stop beats a dated contract (story 6.4).

## 6. Trucks and warehouses

- Every warehouse has two garage slots. Each delivering warehouse has a freight truck and a driver in both.
- An empty slot on a delivering warehouse is the first spend after the cash reserve.
- A warehouse holds one family. Grow one family at a time. A second family stays on the wholesaler until a warehouse the player already owns can be its home.
- A mixed warehouse keeps its largest family. The smaller family’s new imports go to that family’s home. Its stores route to that home. The mixed warehouse sells the leftover down and gets no more orders for that family. There is no call that moves boxes between warehouses.
- A family that already has stock and no home takes the next empty warehouse that has pallet shelves. A building with no pallet shelves is not a home. An empty warehouse with nothing to receive stays empty.
- Open a new warehouse for a family when the empire enters that family. A family already on a home gets a second warehouse of that family only when both of its trucks carry more than **TruckBusy** = 85% of their cargo, or when that home already has an import order and is still under **RefillDays** of stock. A normal refill dip, where the home has no order yet, does not claim a second warehouse.

## 7. Managers

- Stage 2 has one logistics manager, one purchasing agent, one HR manager, one headhunter, and one pricing plan.
- A missing pricing plan is created on the headquarters before a person or a price is assigned to it (story 11.0).
- Staff plan targets are written through the staff-plan call (story 7.9).
- After that:
  - one logistics manager and one purchasing agent per warehouse
  - one HR manager per **HrSpan** = 25 employees, until the game’s HR capacity read replaces that span
  - one headhunter per **HuntSpan** = 5 open roles
- A manager capacity is read from the game, not guessed. Until that read is recorded, **HrSpan** stays 25.
- A plan without an assigned person counts as missing.
- Stage 2 insurance starts at **Bronze**. **Gold** is on before any district reaches **RivalPoachAttackThreshold** = 8 businesses. A health, wage, or hours demand is met by the plan. A bonus does not clear it.

## 8. Money

- Keep a cash reserve of **ReserveDays** = 7 days of total costs, plus billed tax and unbilled tax at **EstimatedTaxRate** = 25% when the save has no brackets. No ordinary action spends that reserve.
- Back taxes are paid first, down to **MinSurvivalDays** = 3 of operating cost. In the **TaxShieldWindowDays** = 5 before an assessment, trucks, pallet shelves, and Stage 4 building buys may spend up to **TaxShieldDaySpend** = 60% above the operating reserve. Loan principal is not a deduction.
- Stage 1 takes the largest loan whose daily repayment is at most **LoanCover** = 50% of projected daily profit.
- Repay a loan when its interest rate beats the best payback rate on offer.
- Each day the autopilot spends at most **DaySpend** = 25% of cash above the reserve.

## 9. Real estate

- From Stage 4, buy by shortest payback (purchase price ÷ daily rent, story 6.5).
- Headquarters come first, then warehouses, then retail.
- A building whose payback is longer than **PaybackMax** = 365 days is not bought.

## 10. Closing

- Warehouses, headquarters, and offices are cost centers. They are never close steps (story **14.6**, which folds 7.13).
- A goods store is a close step only after it has been supplied and staffed for **FixDays** = 7 days and its profit is still below zero for **RedDays** = 7 days.
- An unsupplied store gets connected. An unstaffed store gets hired.

## 11. Running the business

**The player.** During time advance the player sleeps and eats before energy, hunger, or happiness reach **NeedFloor** = 20%.

**Staff.**
- An employee demand that is unmet for **DemandDays** = 3 days is met (story 13.4) or that employee is replaced (story 11.0). Insurance, wages, and hours are not cleared with a bonus.
- A sick, retired, quitting, or poached employee is replaced through the headhunter.
- Training targets full skill (story 11.0).

**Store operation.** Story 13.2 assigns the cleaner, the guard, uniforms, and capacity fixtures.
- A store with dirt spots has a cleaner.
- A store with theft above **TheftMax** = 1% of sales has a guard.
- A customer demand the store can meet (for example uniforms) is met.
- Opening hours cover the busiest hours of that type.
- Customer capacity matches the layout. A capacity miss logs one line. It does not retry every frame. Once spend room is at least **RackSpendRoom** = $2,500, a storage rack is added when target stock reaches **RackTriggerRatio** = 85% of that fixture’s space and the floor has room, including in Stage 1. A second register stays a Stage 2 check at 85% of checkout.

**Market and money.**
- A marketing campaign runs when its daily cost is below **MarketingCover** = 50% of the gain it measures.
- Taxes and back taxes are paid on time (story 13.4).
- A rival takeover offer is logged and declined unless it beats payback (story 13.6).

**Saves.** The game’s autosave keeps the run. The mod does not save.

## Monitoring and self-healing

**Daily audit.** On `day.rolled`, `Audit.Check` logs one `audit.summary` line with these counts:
- unconnected goods stores
- empty garage slots
- dry warehouses
- mixed warehouses
- unstaffed stores
- missing plans
- cash below reserve
- pin mismatch

Each count is a pure function with a Tier 1 test.

**Self-heal.** Each finding maps to one action through the existing confirm, bridge, and record pattern:
- connect the store
- buy a truck and hire a driver
- add an import order
- move the minority family
- hire
- assign the plan

**Guardrails:**
- at most **HealCap** = 10 heal actions a day
- spending inside **DaySpend**
- after **HealStrikeMax** = 3 failed actions in a day, the autopilot pauses with `autopilot.paused reason=failures`
- dry-run mode logs the action without calling the game
- F10 stops the autopilot

**Snapshot.** Before a close or a bulk action, `snapshots.log` gets `snapshot.taken` with the affected rows. Undo is a reload of the last save.

**Watch it:**
- The F9 top bar shows day, cash, net worth against **WinNetWorth**, yesterday’s profit, stage, autopilot on or off, and speed. Crossing **WinNetWorth** does not stop the run.
- The middle shows stores supplied, slots filled, plans present, and the last heal.
- The bottom shows roadmap steps with Execute, Execute all, and the autopilot toggle.
- `telemetry.csv` gets one row per day: day, cash, net worth, profit, stage, health counts, and actions. `script/Report-Progress.py` turns that file into the progress report.

**Harvest.** A wrong heal, a wrong stage, or a bad decision found in play becomes a named Tier 1 test before the next run.
