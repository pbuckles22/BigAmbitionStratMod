# Test plan (TEST_PLAN.md)

## Course gate (1.18.5.2)

Where: a save with one open store and a second rented lot whose type is `ba:businesstype_empty` (BizMan: Empty).

1. Restart so `mod.loaded version=1.18.5.2` is first in `%LocalAppData%\ChaoticCEO\ceo.log`.
2. Let the next day roll.
3. **PASS if** that address has `course.needed` `course=market-demands`, or `block.named` `slug=expansion-blocked` when Market Demands is already completed and the first store is not Ready. **FAIL if** the lot stays `kind=other` and neither line is there.
4. Version marker: public `1.18.5.2`.

Measured: `dotnet test` 1278 passed. Release build installed `1.18.5.2`.

Human PASS 2026-11-20 on 1.18.5.2. Days 37–62 each logged `course.needed` `course=market-demands` `street=ba:street_ninthavenue` `number=19`. The lot stayed `kind=other`, rent 87, profit −87. Day 61 retail profit 1489.785. Cash 61796.51 → 63728.51.

## Rack brake and goods reconcile (1.18.4)

A storage rack is not ordered when `shelf.need` is `enough`, even if Cover’s target is at least 85% of the selling fixture. Owned racks plus pending racks are capped at 1. Open furniture contracts append `furniture.open` (`from`, `to`, item, deliver day, price). A blank-street wallet move of $1200 takes the next open furniture contract’s street. On a delivery day, `goods.delivered` or `goods.missed` names the Cover need and the on-hand delta. A same-day `replace` that has not landed keeps the scheduled truck for one delivery cycle (`RiseDays` = 2).

Tier 1: `GoodsReconcileTests`. Unit: enough shelves block the rack, the owned+pending cap, on-hand rises within 2 days after a large replace, a $1200 blank move takes the furniture street, a smaller blank move does not, and the same-day hold. Chain: `cover need → delivery land → rack gate`, `cover need → delivery hold → goods land`, and `cover need → on-hand rose → delivery release`. Command: the Tier 1 `dotnet test` suite.

Human check: city view, after a restart onto this build. One morning on the gift store is enough.

1. Restart Big Ambitions so this build loads.
2. Continue the gift-store save and let one morning run.
3. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
4. **PASS if** `mod.loaded version=1.18.4`, a `shelf.need` line with `verdict=enough` is not followed by a new `rack.ordered` that morning, and any open furniture contract has a `furniture.open` line with `from` and `to`. **FAIL if** the version is still `1.18.3`, or `verdict=enough` and a new `rack.ordered` share that day.
5. Evidence: `shelf.need` (`verdict`), `rack.ordered` (absent when enough), `furniture.open` (`from`, `to`, `price`), and on a delivery day `goods.delivered` or `goods.missed` (`need`, `delta`).
6. Version marker: public `1.18.4`.

Measured: `dotnet test` 1267 passed. Release build succeeded and installed. Public `1.18.4`.

Human PASS 2026-11-20 on 1.18.4 at 15 7th Avenue. `mod.loaded version=1.18.4 build=3682`. Days 6–9 `shelf.need verdict=enough` and no `rack.ordered`. Day 6 `contract.rescheduled from=8 next=6`. Day 8 five `goods.missed` `delta=0` (gift before=65 after=65 need=233). Day 9 gift onHand 250. Day 8 `spend.move` deliverycontract -$2340.99 already had `ba:street_seventhavenue` 15. No `furniture.open`.

## Named block (1.18.3)

A silent refusal on the rent, fit-out, or ops path appends one `block.named` line to `%LocalAppData%\ChaoticCEO\ceo.log`. The line names `where`, `slug`, day, street, and number. `Inside` false is `slug=not-inside`. Other silent returns on that path use `no-rent`, `no-lot`, `no-layout`, `no-pick`, `no-items`, `waiting`, `bad-place`, or `ops-station-not-initialized-frame0`. A rented lot with a blank business type stays in store health and counts as `bareLeases` on `audit.summary`.

Tier 1: `NamedBlockTests`. Unit: the not-inside line, each slug, the Frame-0 retry slug, bare-lease count, and the record write. Chain: `load → morning steps → named block` and the fit-out/ops share of that line. Command: the Tier 1 `dotnet test` suite.

Human check: city view, after a restart onto this build. One morning is enough. Stay outside a second lot if the morning tries fit-out there.

1. Restart Big Ambitions so this build loads.
2. Continue a save (or a new game) and let one morning run while not standing inside a bare rented lot the morning tries to open.
3. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
4. **PASS if** `mod.loaded version=1.18.3` and a fit-out or layout refusal writes `block.named` with `slug=not-inside` (or another named slug on that path). **FAIL if** the version is still `1.18.2`, or a refused layout leaves no `block.named` line.
5. Evidence: `block.named` (`where`, `slug`, street, number).
6. Version marker: public `1.18.3`.

Human PASS 2026-11-20 on 1.18.3. `mod.loaded version=1.18.3 build=3682`. Day 1 `block.named where=layout slug=not-inside street=ba:street_seventhavenue number=15` (twice).

Measured: `dotnet test` 1257 passed. Release build succeeded. Public `1.18.3`.

## Named spend (1.18.2)

On each day change, the closed day appends named money lines to `%LocalAppData%\ChaoticCEO\ceo.log`. `spend.store` is one business: `kind` is `retail`, `service`, or `cost`, and `check` is sales − goods − ongoing. `ongoing` is wages + rent + marketing + theft + license. `gap` on that line is the game profit minus `check`. `spend.item` names each sale and each goods purchase. `spend.roll` sums profit by retail, service, cost, and other. `spend.home` and `spend.property` name each address. `spend.empire` is business + real estate − residential + loan + parking + unassigned + salary + insurance + headhunter. Interest is on that line and is not in `check`. `spend.move` is one transaction from that day, with its type, categories, address, and kind. `spend.cash` is the wallet: `delta` is close − open, and `gap` is delta − the sum of those moves. The first roll of a session has `open=missing`. A closed day with no statement appends `spend.blocked reason=no_summary`.

Tier 1: `SpendMathTests`. Unit: the day-64 gift shop and warehouse, interest left out of the total, license and theft inside ongoing, a profit gap, moves that cover the wallet, a missing open, and the kind split. Chain: `statements → daily cost → spend kind → named spend → cash gap` and `store family → spend kind → retail and cost → empire check`. Command: the Tier 1 `dotnet test` suite.

Human check: city view, after a load of this build. Let the day change twice (a short skip is enough).

1. Restart Big Ambitions so this build loads. Continue the save and let two mornings pass.
2. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
3. Find `mod.loaded version=1.18.2`, then the second `day.rolled` block.
4. **PASS if** that block has `spend.store` with `kind=retail` for the gift shop, `spend.roll`, `spend.empire`, and `spend.cash` whose `delta` is close − open. A dollar that is not a store, a home, a property, or a move shows up as `gap` on `spend.cash` or `spend.empire`. **FAIL if** the version is still `1.18.1`, or the only money lines are `cost.read` and `csv.written`.
5. Evidence: `spend.store`, `spend.item`, `spend.roll`, `spend.empire`, `spend.move`, `spend.cash`.
6. Version marker: public `1.18.2`.

The first morning of the session has `spend.cash` `open=missing`. The wallet math starts on the second morning.

Human PASS 2026-11-20 on 1.18.2 at 15 7th Avenue. Loaded day 64, F6 on day 69. Gift shop `kind=retail`. Empire gap 0 on days 64–68. Cash gap 0 on days 65, 66, and 68, and −0.003 on day 67. Day 64 moves −8516.834 against a wallet drop of 8516.829 from the prior session’s open 12300.51. Eight delivery contracts of 1200 have no street.

Measured: `dotnet test` 1251 passed (prior 1240). Release `1.18.2+643`.

## Crash trail (1.18.1)

A thrown morning step, and a bridge catch that used to return with no line, appends one `crash.caught` line to `%LocalAppData%\ChaoticCEO\ceo.log`. The line names `where`, the exception type, `slug=UNHANDLED`, the day, `payload`, `trail`, `message`, and `stack`. The same exception waits until the next day. The first tick appends one `trace.armed` line. Crossing the win appends one `win.trail` line with the same steps. The tick continues after the catch.

Tier 1: `CrashTraceTests`. Unit: the caught line names the exception, the payload, and the steps. The same exception logs once until the day changes. Chain: `load → morning steps → crash` and `load → morning steps → win`. Command: the Tier 1 `dotnet test` suite.

Human check: city view, after a load. `mod.loaded version=1.18.1` and one `trace.armed version=1.18.1`. If a step throws, one `crash.caught` line with `type`, `trail`, and `payload`. PASS if both boot lines are present and a thrown step names itself. FAIL if the log stays on `1.17.23` or a thrown step leaves no `crash.caught` line.

## One stage sim (1.17.23)

The stage sim steps close and tax through the live decides. Cost is yesterday’s wages and rent from the day step. A wholesaler contract counts as supplied. Close wins over tax. Headless. No game boot.

Tier 1: `StageSimMorningTests`. Unit: the sim close matches `Close.Decide`. Chain: `day step → live decides → close → tax`. Command: the Tier 1 `dotnet test` suite.

Measured: `dotnet test` 1235 passed (prior 1225).

---

## Truck load (1.17.22)

Truck load is the cargo on filled garage slots divided by those trucks’ capacity. Above **TruckBusy** (85%) the next open is a service or office. Repay uses the best daily rent ÷ price among buildings for sale. A loan is repaid only when its interest is higher than that rate. Dirt, theft, sales, and unmet customer demand come from the rented building. Cleaner, guard, or customer is the run pick only when that reading says so.

Tier 1: `TruckLoadTests`. Chains: `garage → truck load → open`, `buildings → payback → repay`, and `building → run facts → remedy`. Command: the Tier 1 `dotnet test` suite.

Human version smoke:

1. Restart Big Ambitions so this build loads. Continue the 15 7th Avenue save and let one morning roll.
2. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
3. Read `mod.loaded`, then that morning’s `truck.load`, `payback.best`, and `building.pulse`.
4. **PASS if** the version is `1.17.22` and those three lines are present. On a gift shop with no filled truck, `fraction=0` and the morning does not open a service. **FAIL if** the version is still `1.17.21` or any of the three lines is missing.
5. Evidence: `mod.loaded`. `truck.load`. `payback.best`. `building.pulse`.
6. Version marker: public `1.17.22`.

Measured 2026-11-17 at 15 7th Avenue on the truck-load build: `mod.loaded version=1.17.21 build=3682`. Days 22–57 each wrote the three lines. `fraction=0`. `rate=0`. `dirt=1` `theft=0` `unmet=0`. Day 56: 96 customers, revenue 3066, profit 2183.67, cash 13830. Day 57 cash 530. Human PASS. The land number is 1.17.22 because 1.17.21 is the display-green ship.

---

## Display stays green (1.17.21)

A selling display stays at or above 2/3 of its capacity for the whole day, open or closed. Below 1/3 is red. The band between is yellow. The 4 AM count is that display. The order leaves at least one day of sales above the green line. A count already under the line, or one that would fall under it before the truck, is pulled to today. The buy line is not cut to free space.

Tier 1: `Cover_day_58_yellow_gift_shelf_pulls_the_truck_to_today`, `Cover_a_green_count_that_would_cross_the_line_pulls_the_truck_to_today`. Chain: `open days → burn → green line → truck today → playbook`. Command: the Tier 1 `dotnet test` suite.

Human version smoke:

1. Restart Big Ambitions so this build loads. Continue the 15 7th Avenue save. The cheap-gift rounded shelf is already yellow at 152/250. This morning cannot be painted green until the truck arrives.
2. Let today run. After the delivery, check the cheap-gift shelf, then check it again at the next 4 AM, while the shop is still closed. Look at flowers, soda, umbrellas, and the register bags the same way.
3. Open `%LocalAppData%\ChaoticCEO\ceo.log`. Read the first line and the cheap-gift `contract.cover` row for today.
4. **PASS if** the first line is `mod.loaded version=1.17.21 build=3682`, today's cheap-gift row has `replace=true` and `next` equal to today, and after that delivery the shelf stays at or above 2/3 full through the next 4 AM. **FAIL if** the version is still `1.17.20` or missing, the truck stays on a later day, or the shelf is yellow or red again at the next 4 AM after a normal sales day.
5. Evidence: `mod.loaded`. `contract.cover` for `ba:itemname_cheapgift` with `replace=true` and `next` equal to the current day.
6. Version marker: public `1.17.21`.

Measured 2026-11-17 at 15 7th Avenue: `mod.loaded version=1.17.21 build=3682`. Day 59 cheap gift `replace=true` `next=59`, rescheduled from day 64. Day 60 profit 1888.64. Day 61 profit 1988.74, cash 8049. Day 62 profit 2092.88, cash 9457, cheap gift onHand 266, `replace=false`, `next=64`. Human PASS.

---

## Tax reserve (1.17.20)

The reserve is 7 days of operating cost plus billed and unbilled tax. Unbilled tax is 25% of profit minus deductible capex. Back taxes are paid first, and that pay stops at 3 days of operating cost. A partial pay under $500 is not sent. A current bill that is not due within 2 days stays above the 7-day reserve. In the 5 days before an assessment, a deductible buy (truck, pallet shelf, or Stage 4 building) may spend 60% above the operating reserve. Loan principal is not that deduction. F9 shows the tax pick ahead of an open or a loan repay. Sleep and a close still come first. This slice does not read an assessment calendar from the save, so the morning does not raise a truck or shelf order into that 60% window yet.

Tier 1: `TaxPolicyTests`. Chain: `profit − capex → tax reserve → pay back taxes → shield spend`. Command: the Tier 1 `dotnet test` suite.

Human version smoke:

1. Restart Big Ambitions so this build loads. Continue the current save, or start a new game. Open the city, then press F9.
2. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
3. Read the first line, then look for `tax.paid` or `tax.held`. On F9, read the `Next` line. A save with no tax bill keeps the usual next step. Do not look for a truck or a shelf purchase jumping to 60% spend. That window is not on the morning yet.
4. **PASS if** the first line is `mod.loaded version=1.17.20 build=3682`. When back taxes are paid, cash left is at least 3 days of operating cost, or the log has `tax.held reason=survival` and no `tax.paid`. **FAIL if** the version is still `1.17.19` or missing, or a `tax.paid` back amount leaves less than 3 days of operating cost.
5. Evidence: `mod.loaded`. `tax.paid` or `tax.held` when a bill is due. `playbook.shown kind=tax` when F9 is opened on a payable bill.
6. Version marker: public `1.17.20`.

Human PASS 2026-11-16 on 1.17.20. New game at 15 7th Avenue. `mod.loaded version=1.17.20 build=3682`. `layout.loaded` `GiftShopWholesalers`. Day 2 profit 615. Day 3: 47 customers, revenue 968, profit 1129, ready, cash 11319. Day 22 cash 6917, profit -100. No `tax.paid` and no `tax.held` through day 22.

---

## District pace (1.17.19)

A wholesaler neighborhood holds 3 businesses. Store 4 goes to another neighborhood. After that family's importer is running, the cap is 7 until Gold insurance is on, then the cap is off. Gold without an importer stays at 3. A full neighborhood is not a fallback when the other lot is over the cash reserve. This slice does not move a store in the city and does not turn Gold on. The morning still rents and fits the same lot.

Tier 1: `DistrictPaceTests`. Chain: `importer flag → cap → where`. Command: the Tier 1 `dotnet test` suite.

Human version smoke (install changed; the city does not show the district pick yet):

1. Restart Big Ambitions so this build loads. Continue the current save, or start a new game.
2. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
3. Do not look for a fourth store moving to another neighborhood. That pick is not on the morning yet.
4. **PASS if** the first line is `mod.loaded version=1.17.19 build=3682`. **FAIL if** the version is still `1.17.18` or missing.
5. Evidence: `mod.loaded`.
6. Version marker: public `1.17.19`.

Human PASS 2026-11-16 on 1.17.19. New game at 15 7th Avenue. `mod.loaded version=1.17.19 build=3682`. `layout.loaded` `GiftShopWholesalers`. Day 2 profit 1024. Day 3: 63 customers, revenue 1524, profit 1223, ready. Day 4: 67 customers, revenue 1791, cash 2766, ready.

---

## Layout size and SKU lock (1.17.18)

`Layout.Decide` keeps an A1 blueprint off a B1. A size with no golden uses the starter file for that size code: C2 gift is `GiftShopImporters`, B1 gift is none. An electronics store stays closed while the factory pipeline is off. Phones, earbuds, headphones, and the smartwatch are not ordered, including the game ids `headphones01`, `smartphone2`, `smartwatch1`, and `smartwatch2`. This slice does not rent a B1 and does not open an electronics store on screen. An A1 gift shop still loads `GiftShopWholesalers`.

Tier 1: `LayoutSizeTests`. Chains: `size code → layout`, `sku scope → open`, and `open → size code → layout`. Command: the Tier 1 `dotnet test` suite.

Human version smoke (install changed; the city still fits the A1 gift shop):

1. Restart Big Ambitions so this build loads. Continue the current save, or start a new game.
2. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
3. If a store is fitted, find its `layout.loaded` line. Do not look for a B1 building or an electronics store.
4. **PASS if** the first line is `mod.loaded version=1.17.18 build=3682`. If a layout loaded, it is `GiftShopWholesalers` on the small lot. **FAIL if** the version is still `1.17.17` or missing, or a fitted gift shop loads a different blueprint.
5. Evidence: `mod.loaded`. `layout.loaded` only when a store is fitted.
6. Version marker: public `1.17.18`.

Human PASS 2026-11-16 on 1.17.18. New game at 30 7th Avenue. `mod.loaded version=1.17.18 build=3682`. `layout.loaded` `GiftShopWholesalers`. Day 2 profit 894. Day 4: 65 customers, revenue 1703, profit 1162. Day 5: 69 customers, revenue 1733, profit 1182. Ready. Cash on day 5 was 3434.

---

## Close list (1.17.17)

A warehouse, a headquarters, and an office never close. A goods store with no delivery connects, and one with no staff hires. **FixDays** and **RedDays** still gate a losing goods store. The playbook close is that pick. This slice does not change the F9 list. The morning does not feed a live store into the pick yet.

Tier 1: `CloseListTests`. Chain: `type → close filter → playbook close`. A gift shop that has been delivered and staffed for **FixDays** with **RedDays** of losses is `Next close close`, and that pick beats an open. An unstaffed clothing store is a hire. Command: the Tier 1 `dotnet test` suite.

Human version smoke (install changed; F9 close rows are unchanged):

1. Restart Big Ambitions so this build loads. Any save or a new game is fine.
2. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
3. Do nothing else for this check. Do not look for a store to close.
4. **PASS if** the first line is `mod.loaded version=1.17.17 build=3682`. **FAIL if** the version is still `1.17.16.2` or missing.
5. Evidence: that `mod.loaded` line.
6. Version marker: public `1.17.17`.

Human PASS 2026-11-16 on 1.17.17. `mod.loaded version=1.17.17 build=3682`. New game at 26 7th Avenue, stopped day 6. Day 3 ready after one selling day (profit 717). Days 4 and 6 named a $40,000 loan. No `loan.taken`. One store rented.

---

## Three business modes (1.17.16.2)

Human PASS 2026-11-16. New game at 30 7th Avenue: `layout.loaded` `GiftShopWholesalers`, no `bill.quoted`. Days 2–5 profit 879, 1196, 956, 1182 (customers 51, 65, 56, 70; revenue 1329, 1755, 1427, 1733). Same band as the 1.17.14 new-game day 4 (profit 1179). Days 1–7 keep the affordable lot and the Stage 1 loan. Driveability, marketing, and size wait.

Retail G2 stays shelves. A hairdresser’s G2 is hair-care product and a styling station. An office or a gym passes G2 on complete workstations and signs no contract. Fit-out uses that mode’s starter. The bill is labor and overhead over (1 − margin), written onto that mode’s fee when the layout loads. A gift shop stays `GiftShopWholesalers` and is not quoted a fee. A stage 1 open cannot be a hairdresser, an office, or a gym. An empty lot (`ba:businesstype_empty`) loads the gift-shop starter. `1.17.16.1` missed that lot at 22 7th Avenue and did not load a layout.

Tier 1: `ModesTests`, `StarterTests`. Chain: `open → mode → G2 → fit-out → bill`. The empty-lot case is `An_empty_lot_loads_the_gift_shop_starter`.

Human check. Continue the current save, or start a new game. This is still the first gift shop. It is not a second store, and it is not a hairdresser, office, or gym open.

1. Restart Big Ambitions so this build loads. Continue the save, or start a new game, and let the mod fit the first store.
2. Open `%LocalAppData%\ChaoticCEO\ceo.log`. Find `mod.loaded` and a `layout.loaded` line for that store.
3. Confirm the layout name and that the log has no `bill.quoted` line.
4. **PASS if** `mod.loaded version=1.17.16.2` and `layout.loaded` contains `layout=GiftShopWholesalers`, and there is no `bill.quoted` line. **FAIL if** the version is still `1.17.16.1`, the only layout line is `layout.missed` for `ba:businesstype_empty`, or `bill.quoted` appears on that gift shop.
5. Evidence: `mod.loaded`, `layout.loaded` (`layout=`). Version marker: public `1.17.16.2`.

---

## Cash, cost, and stage (1.17.15)

Human PASS 2026-11-15. Day 2 at 26 7th Avenue: cash=15000 daily=0 reserve=0 cashBelowReserve=0. Day 1 profit=0.

Yesterday’s rent, wages, marketing, loan payments, unassigned wages, and truck cost set the 7-day reserve. A zero profit stays zero. No goods stores stays Stage 1. Tax and the truck-load fraction are later stories.

Tier 1: `DailyCostTests`, `StageTests`. Chains: `statements → daily cost → reserve → money`, `statements → daily cost → reserve → audit`, and `zero stores → Stage 1`. The day-2 pass locks `cost.read` for cash 15000 and a zero reserve, and `audit.summary` with `cashBelowReserve=0`. Cash under the reserve writes `cashBelowReserve=1`.

Human check. Continue the current save and let one morning roll. This check is the cost line and the reserve flag. It does not check tax or truck load.

1. Restart Big Ambitions so this build loads. Continue the save at 22 7th Avenue and let the clock roll one morning.
2. Open `%LocalAppData%\ChaoticCEO\ceo.log`. Find `mod.loaded` and the new `cost.read` line for that day. If autopilot is on, also find that day’s `audit.summary`.
3. Compare `cost.read` with yesterday: `daily` is wages + rent + marketing + loan + truck + unassigned. `reserve` is 7 × `daily`. `truck` is 0 until a daily truck cost exists on the save.
4. **PASS if** `mod.loaded version=1.17.15`, `cost.read` shows those parts, `reserve` is 7 × `daily`, and `cashBelowReserve` is 1 only when `cash` is below `reserve`. **FAIL if** the version is still `1.17.14`, there is no `cost.read` line, `reserve` is 0 while `daily` is above 0, or `cashBelowReserve` is 1 while `cash` is at least `reserve`.
5. Evidence: `cost.read` (`daily`, `reserve`, `cash`). `audit.summary` (`cashBelowReserve`) when autopilot is on. Version marker: public `1.17.15`.

---

Define **Tier 1** (fast feedback: unit, headless, or local) and **Tier 2** (integration, browser, device, or E2E) for your stack. Replace the placeholders below.

---

## Empty displays (1.17.14)

A new game’s selling displays have stock by day 4. `g2` is true. The morning order is `restock.summary` with `ordered` above 0. A storage shelf is not one of those displays.

Tier 1: `DisplayStockTests`. Chain: `new game → day roll → display stock → g2`.

Human check. A new gift shop, played through day 4. This check is the selling displays. It does not check the storage pod.

1. Restart Big Ambitions so this build loads. You are in a new game, at the gift shop on 22 7th Avenue, on the morning of day 4.
2. The shelves, flower stand, fridge, and register should be holding product. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
3. Let the new game run from the first morning through day 4. Look at the selling fixtures, then find `mod.loaded`, `restock.summary`, and `store.health` for day 4.
4. **PASS if** `mod.loaded version=1.17.14`, a `restock.summary` line has `ordered` above 0, and day 4 `store.health` has `g2=true` while those selling fixtures are not bare. **FAIL if** the version is still `1.17.13`, the only `restock.summary` says `ordered=0`, day 4 has `g2=false`, or the selling fixtures are still empty.
5. Evidence: `restock.summary` (`ordered`). `store.health` (`g2`). `display.stocked` when a box was moved from storage onto a display. Version marker: public `1.17.14`.

---

## Game log (1.17.13)

A new game does not read the previous game’s `ceo.log`. Each game keeps its own file. `ceo.log` is only the game now loaded. The previous game is copied to `%LocalAppData%\ChaoticCEO\games\<n>\ceo.log` when you start another game without quitting, and again on the next boot. The ledger then starts empty. A second look at the same save object does not split the log.

Tier 1: `GameLogTests`. Chain: `rent → layout skip → game switch → new layout gate`.

Human check. Continue a save, then start a new game without quitting.

1. Restart Big Ambitions so this build loads. Confirm `mod.loaded version=1.17.13`.
2. Continue 22 7th Avenue and let one morning finish.
3. Start a new game without quitting.
4. **PASS if** `%LocalAppData%\ChaoticCEO\ceo.log` opens with `mod.loaded` and `game.opened`, has no lines from the continued save, and that save is still in `games\<n>\ceo.log`. The new game then logs `building.rented` and `layout.loaded` with no `ops.skipped` before that `layout.loaded`. **FAIL if** `ceo.log` still contains the continued save, or `ops.skipped reason=open` is written before this game’s `layout.loaded`.
5. Evidence: `game.opened`, then `building.rented`, then `layout.loaded`. Version marker: public `1.17.13`.

This check is the log split. Stock on the selling displays by day 4 is still **14.20**.

---

## Overhire (1.17.12)

My Employees shows one cashier and one cleaner. A `HireCandidate` row at $0/hr still counts. Extra cleaners are not hired, and extras already on the list are dropped. One scheduled cleaner is kept over a later $0/hr row. A later $0/hr row is kept only when nobody is on a shift. Other skills stay.

Tier 1: `HeadcountTests`. Chain: `morning → one cashier → one cleaner`.

Human check. Gift shop at 22 7th Avenue, My Employees.

1. Restart Big Ambitions so this build loads. Continue the same save. Confirm `mod.loaded version=1.17.12`.
2. Let one morning finish. Open My Employees.
3. **PASS if** the list has one cashier and one cleaner. **FAIL if** a second cleaner is listed, including a row at $0/hr.
4. Evidence: `headcount.held` with `cashiers=1 cleaners=1`. Version marker: public `1.17.12`.

The wage on that one cleaner can still read $0/hr. Changing the wage is **14.10**.

---

## Staff hold cheat (1.17.11.12)

The parked employee hold is on, on the same tick as the player needs hold. Satisfaction stays at 100, an open complaint is cleared, the next complaint is a game year out, a rival poach is cancelled, and age resets to 60 at 65. A sick day on the calendar, due or later, becomes never, and an absence from a call that already landed is cleared. Walking off a shift for another reason is not held. Switch: `StaffHold.Enabled`.

Tier 1: `StaffHoldTests`. Chain: `day roll → staff hold → leaving reason → register cover (no hire)`.

Human check. Gift shop at 22 7th Avenue.

1. Restart Big Ambitions so this build loads. Continue the same save. Confirm `mod.loaded version=1.17.11.12`.
2. Let one morning finish. Do not cancel the time skip.
3. **PASS if** `ceo.log` has `staff.held` that morning, with `sick=` on the line, and nobody quits, retires, is poached, or calls in sick. **FAIL if** the version is still `1.17.11.11`, there is no `staff.held`, a cashier or cleaner leaves with `reason=quit`, `reason=retired`, or `reason=poached`, or someone calls in sick.
4. Evidence: `staff.held` (`sick=`). Version marker: public `1.17.11.12`.

The first morning may show `sick` above 0 while scheduled sick days are cleared. A later morning of the same save stays at `sick=0`. Walking off a shift for a reason other than a sick call is outside this cheat.

Human PASS 2026-11-14 on 1.17.11.12 at 22 7th Avenue. Days 1588–1644: 57 `staff.held`, employees=8. Day 1588 `sick=8` and one `cashier.replaced reason=absent`. Days 1589–1644 `sick=0`. No `reason=quit`, `reason=retired`, or `reason=poached`. Day 1643 ready, 75 customers, revenue 2351. Day 1642 profit 1641.70.

Human PASS 2026-11-14 on 1.17.11.11 at 22 7th Avenue. Days 1588–1593: six `staff.held`, employees=8, raised=0, aged=0, complaints=0, poach=0. No `reason=quit`, `reason=retired`, or `reason=poached`. Day 1588 one `cashier.replaced reason=absent`. Days 1592–1593 ready, customers 78 then 76, revenue 2295 then 2228.

---

## Tier 1: Fast feedback

```bash
python -m pytest -q
python -m py_compile script/Export-SaveGameText.py script/Slice-SaveGame.py script/statement_ledger.py script/game_build.py
dotnet test src/ChaoticCEO.Tests/ChaoticCEO.Tests.csproj -p:GameManaged="C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"
dotnet build src/ChaoticCEO/ChaoticCEO.csproj -c Release -p:GameManaged="C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"
```

`dotnet test` is the Debug compile. The Release command installs `ChaoticCEO.dll` to ModsLocal. The gitignored counter is `build_number.txt`. The informational version is `1.14.1+N`.

The story gate reuses `doc/gemini/cache/save.bin` and `save-index.sqlite` when both are current for the dropzone `.hsg`. Do not read or scan `save.bin` again while that blob is current. A renamed `.hsg` fails the recorded-name check on its own. Nobody announces the new name.

Read and scan again only when the `.hsg` is newer than the blob, the file name differs, `script/Slice-SaveGame.py` changes, `script/statement_ledger.py` changes, or this is an epic close:

```bash
python script/Slice-SaveGame.py
python -m pytest -q
```

`write_statements` calls `ensure_index`, which returns when the index is current. `build()` deletes the sqlite when the index is stale. `write_statements`, `write_portfolio`, and `write_routes` still load the current blob on each call. Do not add another full read. Reuse the loaded bytes and the scanned rows for that same blob.

---

## Integration chain (1..X together)

Headless tests that run shipped behaviors **together**, through real interfaces. The new chain test’s first line is `Chain: first → … → this`. It walks from the first shipped behavior through this one, and each step receives what the previous step produced. The harness loads the production module and asserts the value the next stage reads. Isolated unit red/green does not replace this. Document the command (it is usually the Tier 1 suite, once the chain tests live there). Rule: [.cursor/rules/integration-chain.mdc](.cursor/rules/integration-chain.mdc) → *Prefix*.

```bash
python -m pytest -q tests/test_statement_street.py tests/test_portfolio_join.py tests/test_routes.py tests/test_report.py tests/test_save_text.py tests/test_game_build.py
dotnet test src/ChaoticCEO.Tests/ChaoticCEO.Tests.csproj -p:GameManaged="C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"
```

The chain reads the cached index for the build 3682 `.hsg` in `doc/gemini/dropzone/`. It reads `save.bin` again only when that blob is stale: a newer `.hsg`, a different file name, an indexer change, a `statement_ledger.py` change, or an epic close. Evidence lines `statements.written`, `portfolio.written`, `routes.written`, `report.written`, and `savetext.written` are appended to `doc/gemini/cache/ledger.log`.

`Chain: gunzip → index → statement → portfolio` reads the statement file, joins street and number, and locks Chaotic Clothing #3. `Chain: gunzip → index → statement → portfolio span` locks Chaotic Hair #8, family service, tco 4003.3822. `Chain: gunzip → index → statement → portfolio → routes` reads that portfolio and locks the Fifth Avenue 36 destination. `Chain: gunzip → index → statement → portfolio → routes hold` locks W1 sodacan amount 45. `Chain: gunzip → index → statement → seven-day sales` locks Hair #5 sales at 143251 and Kabobs #5 sales at 72610.35 for days 1007–1013. `Chain: gunzip → index → statement → portfolio → routes → report` locks Chaotic Kabobs #5 at the bottom of the profit table, profit 898.6715. Saved-game-to-text calls that same chain on the real save and locks Chaotic Kabobs #5 in the export. `Chain: gunzip → index → statement → portfolio → routes → report kept` keeps the current index and logs `savetext.written`. `Chain: gunzip → index → statement → portfolio → routes → report mix` locks Chaotic Devs as the top office, Fifth Avenue service count 4, and W6 as food plus service. Story 3.1 does not extend that ledger chain. `Chain: dll → pin` publishes one Managed DLL and locks its sha256. `Chain: dll → pin names` locks `BigAmbitions.dll` to its own hash when `BigAmbitions.ModAPI.dll` is in the same pin. The pin file is `doc/requirements/game-build.md`. It has no ledger evidence line. `Chain: dll → pin → loaded` reads that pin and locks build 3682 on `mod.loaded`. `Chain: dll → pin → loaded distinct` keeps the dll hashes off that line. `Chain: dll → pin → loaded default` calls the load the mod uses and locks the same build under `ChaoticCEO\ceo.log`. `Chain: dll → pin → loaded → csv` calls that load, then dumps yesterday’s statements, and locks `csv.written` for day 1013 at Fourth Avenue 28. `Chain: dll → pin → loaded → csv empty` calls that load when yesterday is missing and locks `csv.written` with `rows=0` and a header-only `statements.csv`. `Chain: dll → pin → loaded → csv → routes` calls that statement dump, then the route dump, and locks `routes.written` for the Fourth Street 46 soda delivery. `Chain: dll → pin → loaded → csv → routes empty` calls that statement dump, then an empty route dump, and locks `routes.written` with `routes=0` and a header-only `routes.csv`. `Chain: dll → pin → loaded → csv → routes shelf` calls that statement dump, counts cargo holds, and locks W1 soda amount 1 with the placed lamp and shelf left out. `Chain: dll → pin → loaded → csv → routes → day` calls that route dump, then a day change, and locks `day.rolled` for day 1014. `Chain: dll → pin → loaded → csv → routes → day empty` calls an empty route dump, then that rollover, and locks `day.rolled` with a header-only `routes.csv`. `Chain: dll → pin → loaded → csv → routes → day → next` calls that rollover, then the following day, and locks `day.rolled` for day 1015. `Chain: dll → pin → loaded → csv → routes → day → rank` calls that rollover, reads the statement file it wrote, and locks Fourth Avenue 28 at profit 50687.4805. The soda warehouse is not a mix. `Chain: dll → pin → loaded → csv → routes → day → rank mix` calls that rollover with two cargo families and locks the low-profit street at -40 and W1 as food plus specialty. A placed lamp and a paper bag stay out of the warehouse file. `Chain: dll → pin → loaded → csv → routes → day → rank empty` calls an empty route dump, then that rollover, and locks the same Fourth Avenue profit with no warehouse mix. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay` calls Rank on that statement file and locks Fourth Avenue 28 as the red row at profit 50687.4805. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay mix` calls Rank on the two-street file and locks the low-profit street as the red row and W1 as a food-plus-specialty warning. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice` calls that overlay and locks `advice.shown` for Fourth Avenue 28, explain `Close ba:street_fourthavenue. Profit 50687.4805.` `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice mix` calls that overlay on the two-street file and locks a separate `advice.shown` line for the low-profit close and for W1 mixing food and specialty. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices` calls that advice line and locks the route item's sold price at 2.75. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices route` calls that advice line and keeps that 2.75 when another item sold more. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans` calls that price and locks W1 food as one recurring soda run to First Avenue 38. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans family` calls that soda price and keeps the larger specialty run. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans recurring` calls that soda price and keeps the burger run because a recurring stop beats the priced contract. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing` calls that plan and locks the food role to one HR plan. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing family` calls that plan and keeps the specialty headhunter when food has the larger staff count. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing empty` calls that plan and keeps the food role with no plan when its amount is 0. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout` calls that plan and locks one food blueprint, `soda-line`, onto store 4. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout family` calls that plan and keeps the specialty blueprint `gift-line` when a food blueprint scored higher. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout short` calls that plan and keeps no clone when the street has two businesses. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close` calls that clone and locks store 2 after 3 red days. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close short` calls that empty clone and keeps no close when the same street has 3 red days. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy` calls that close and locks the building under HQ. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy short` calls that empty close and keeps no buy when an HQ building is offered. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy order` calls that close and keeps the later HQ building when the owned street is listed first. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel` calls that buy and locks Fourth Avenue 28, explain `Close ba:street_fourthavenue. Profit 50687.4805.` `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel open` calls that empty buy and still locks that explain, with one `panel.opened` line. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api` reads `doc/requirements/game-api.md` and locks ShutdownBusiness, SendBuyBuildingOffer, SetPrice, and AddDestination. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api empty` keeps those four calls when the buy is empty. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api blank` keeps the calls blank when the panel has no rows. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score` reads the statement stores and locks `Close ba:street_side 9. After rent -30. Streak 3. Trend -20.` plus Fourth Avenue 28 at profit 50687.4805. The layout close stays store 2 on Main and is not one of those lines. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score empty` keeps no explain line when the panel is empty. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score layout` keeps Fourth Avenue 28's explain line when the layout clone is empty. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics` reads the written routes and locks `Reassign ba:street_side 9. From W1. To W2. Family specialty.` `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics supplied` keeps no reassignment when that soda route is stocked. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics empty` keeps no reassignment when the panel is empty, while those same routes would move side 9 under a recorded build. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback` reads the written rent and locks Fourth Avenue 28 at payback 10. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback supplied` locks that building at payback 2 when the soda route stays stocked. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback tie` keeps Later 7 when that payback ties Fourth Avenue 28. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback empty` keeps no building when the panel is empty, while a recorded build still locks Fourth Avenue 28 at payback 10. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap` locks the first action, open Fourth Avenue 28, daily gain 1099. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap loss` locks the side-street close at daily gain 5000. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap tie` keeps the open when that gain ties the close. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap empty` keeps no steps when the panel is empty, while a recorded build still locks the open at daily gain 1099. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap supplied` locks that open at daily gain 1099 when the soda route stays stocked. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown` calls that roadmap and locks the side-street close, `ba:street_side` 9, with one `store.closed` line for `Helpers.BusinessHelper.ShutdownBusiness`. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown separate` locks that side-street close at daily gain 5000, leaves the other store open, leaves the open step unclosed, and a reload clears the close while both stores stay in the statement file. `Chain: dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown → delivery → apply → offer → clone → shown` calls that clone, then the roadmap steps, and locks the first F9 line as open Fourth Avenue 28, daily gain 1099, with the logistics move as the last line and no old red row in the window. `Chain: dump → routes → forecast → snapshot → connect → forecast` reads the route dump, keeps Fifth Avenue 36 on the wholesaler, and after the connect record the salon is no longer `noplan`. `Chain: load → day roll → needs refill` loads a session, rolls the day, and locks hunger back at 100 with one `needs.held` line. `Chain: load → day roll → needs refill → recorded call` reads `game-api.md` after that hold and locks `Buildings.ContractVehicleForSale.Order` for truck buy and `Entities.DeliveryContract.Remove` for the contract drop. An empty panel after the same hold locks those calls blank. `Chain: load → day roll → needs refill → recorded call → slot fill` reads that truck buy after the hold and locks `Buildings.ContractVehicleForSale.Order` and `Entities.VehicleSlot.AssignVehicle` on one `slot.filled` line for the empty slot. `Chain: load → day roll → needs refill → recorded call → slot fill → full warehouse` keeps a warehouse with two filled slots off that line and logs one `slot.summary` with `bought=0`.

---

## Tier 2: Integration / E2E

Use when behavior spans a real runtime (browser, device, network, or native APIs).

```bash
# Example: npm run test:e2e
# Example: playwright test
```

---

**Handoff:** Document the exact commands you use for coverage in AGENT_HANDOFF.md so agents can run them consistently.

**Compile counter:** The mod compiles. Count the Debug `dotnet test` compile and the Release `dotnet build`. The counter is gitignored `build_number.txt`. See `.cursor/rules/testing.mdc`.

**Evidence sink:** `doc/gemini/cache/ledger.log`. The mod appends one `mod.loaded` line (`version`, `build`) to `%LocalAppData%\ChaoticCEO\ceo.log`. The hotkey dump appends one `csv.written` line (`version`, `rows`, `day`, `street`, `number`, `profit`) to that same log and writes `%LocalAppData%\ChaoticCEO\statements.csv`. The same key then appends one `routes.written` line (`version`, `routes`, `warehouses`, `warehouse`, `destination`, `item`, `day`) and writes `routes.csv` and `warehouses.csv` beside it. A day change dumps once and appends one `day.rolled` line (`version`, `day`) after that dump’s `csv.written` and `routes.written`, then one `supply.forecast` line per gap (`day`, `store`, `warehouse`, `item`, `reason`, `buffer`), one `supply.summary` (`gaps`), `supply.move` lines (`amount`, `dry`), and one `supply.rechecked` line per `noplan` gap. `snapshot.taken` and the route rows go to `%LocalAppData%\ChaoticCEO\snapshots.log`. An armed connect appends the store to `routes.csv` and logs `dry=false`. The game save is not written. The first sight of a day does not dump. A later day dumps again. Statement export appends one `statements.written` line (version, row count, first street, profit). Portfolio export appends one `portfolio.written` line (version, row count, business, family, street, number, tco). Route export appends one `routes.written` line (version, route count, warehouse count, portfolio count, warehouse, destination, item, day). Report export appends one `report.written` line (version, bottom business, profit, office count, street count, mix count). Saved-game-to-text appends one `savetext.written` line (version, bottom business). Recording advice appends one `advice.shown` line per recommendation (`version`, `tone`, `business`, `street`, `number`, `explain`) to `ceo.log`. Opening the F9 window appends one `panel.opened` line (`version`, `rows`) and one `advice.shown` line per row. A later frame while the window stays open does not append those lines again. Closing the window does not append them. A confirmed close appends one `store.closed` line (`version`, `street`, `number`, `call`) to `ceo.log`. An unconfirmed close, an open step, and a reload do not append another line. A confirmed logistics redo appends one `logistics.redone` line (`version`, `destination`, `from`, `to`, `family`, `call`). An unconfirmed redo, a different destination, and a reload do not append another line. A confirmed price appends one `price.applied` line (`version`, `item`, `price`, `call`). A confirmed staff plan appends one `staff.applied` line (`version`, `role`, `kind`, `plan`, `amount`). An unconfirmed apply, a different item or plan, and a reload do not append another line. A confirmed building offer appends one `building.offered` line (`version`, `street`, `number`, `call`). An unconfirmed buy, a close step, another number, and a reload do not append another line. A confirmed layout copy appends one `layout.cloned` line (`version`, `street`, `family`, `blueprint`, `source`, `destination`, `call`). An unconfirmed copy, a different street, a different blueprint, a blank family, and a reload do not append another line. Opening the F9 roadmap appends one `roadmap.shown` line (`version`, `steps`, `street`, `number`, `gain`, `move`) to `ceo.log`. A later frame while the window stays open does not append it again. The first frame of a loaded save appends one `needs.held` line (`version`, `day`, `energy`, `hunger`, `happiness`, `energyOff`, `happinessOff`). A later frame of that save does not append it again. A day change that dumps appends one `slot.filled` line per empty garage slot (`version`, `day`, `warehouse`, `slot`, `order`, `assign`, `driver`). A warehouse with no empty slot appends one `slot.summary` line (`version`, `day`, `bought=0`) and no `slot.filled` line. A later day of that save does not append those lines again. The garage is not changed. A day change that records connects appends one `store.connected` line per unplanned store item (`version`, `day`, `store`, `warehouse`, `item`, `add`, `drop`). `add` is AddDestination. `drop` is Remove when that hub holds the item, and empty when the store has no contract. A planned store has no line. A contract for an item no warehouse holds has no line. A contract stays when the hub holds less than one day of the item. The connect rewrites `routes.csv` and does not write `warehouses.csv`. A later day of that save does not append those lines again. The game is not called. The statement file and the route file stay in place.

## Forecast human check

1. Restart Big Ambitions so version 1.11.2.1 loads. You are in the city on the day 1014 save. Do not load the day 1021 save. This check forecasts the items each store sells and writes those connects into `routes.csv`. It does not change the game’s logistics list or drop Fifth Avenue 36.
2. Let one day pass.
3. Open `%LocalAppData%\ChaoticCEO\ceo.log`, `%LocalAppData%\ChaoticCEO\routes.csv`, and `%LocalAppData%\ChaoticCEO\snapshots.log`.
4. **PASS if** Fourth Avenue 28 has one `supply.forecast` for `ba:itemname_haircareproduct` on W11 with `reason=noplan` and a `buffer` taken from that salon’s unit burn, `supply.summary gaps` is in the hundreds (only your own stores), no `supply.move` line has a blank `warehouse=`, more than one store gets a `supply.move ... dry=false` (up to ten), and Fifth Avenue 36’s wholesaler rows are still in `routes.csv`. **FAIL if** the hair line is missing, `gaps` is above 1000, a `supply.move` has `warehouse=` blank, only the salon moves while other lines name a warehouse, or the Fifth Avenue 36 rows are gone.
5. Evidence: `supply.forecast`, `supply.summary`, `supply.move` with `dry=false`, `supply.rechecked`, and `snapshot.taken`. The snapshot is the routes from before the connect.

1.11.2 FAIL 2026-11-05: day 1015 `gaps=1295` across 265 stores; 199 rival or city stores made all 870 `buffer=0` lines. The salon moved (`buffer=5516`, `amount=788`, `dry=false`), then three blank-warehouse moves for Fifth Avenue 54 stopped the night. Fix 1.11.2.1: only player stores, a blank warehouse is no move and no strike, the burn uses the highest day.

Human PASS 2026-11-05 on 1.11.2.1. Day 1015: `gaps=426` (was 1295) across 67 stores (was 265), one `buffer=0` line (Third Avenue 1 soda, priced but unsold), 164 blank-warehouse lines with no move. Fourth Avenue 28 hair on W11 `buffer=5530` (7 × 790). Ten moves, all `dry=false`, across five stores. `routes.csv` 4 → 14 rows, Fifth Avenue 36’s four wholesaler rows kept. Day 1016: the same ten moves; the salon buffer was 1190 (7 × 170). The morning dump reads 4 routes again because the connect is not in the game.
6. Version marker: public `1.11.2.1`. `ceo.log` starts at `mod.loaded version=1.11.2.1 build=3682`.

## Needs human check

1. Restart Big Ambitions so version 1.11.3 loads. You are in the city on the usual save. Do not load the day 1021 save. This check keeps energy, hunger, and happiness full. It does not add a hotkey.
2. Check the three bars. Walk or drive for an in-game hour, then let the day change, and check them again.
3. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
4. **PASS if** all three bars read 100 after load and after the day change, the log has one `needs.held` line, and a later `day.rolled` line. **FAIL if** a bar drops, a low-energy or hunger warning appears, or a second `needs.held` line appears for the same save.
5. Evidence: `needs.held` names the day and the three values from before the fill.
6. Version marker: public `1.11.3`. `ceo.log` starts at `mod.loaded version=1.11.3 build=3682`.

Human PASS 2026-11-05 on 1.11.3. Day 1014 load: energy 70.47, hunger 66.3, happiness 100, both drain settings off. One `needs.held`. Day rolled to 1015 with no second `needs.held`. Same roll: `gaps=426`, Fourth Avenue 28 hair buffer 5516.

## Slot fill human check

1. Restart Big Ambitions so version 1.11.5 loads. You are in the city on the usual save. Do not load the day 1021 save. This check records a freight-truck buy for each empty garage slot. It does not order the truck, assign it, or change the garage.
2. Let one day pass.
3. Open `%LocalAppData%\ChaoticCEO\ceo.log`. Look at the warehouse garages for W7, W8, W9, W13, and W14.
4. **PASS if** `mod.loaded` is version 1.11.5, the new day has one `slot.filled` line per empty slot with `order=Buildings.ContractVehicleForSale.Order` and `assign=Entities.VehicleSlot.AssignVehicle`, a warehouse that already has two trucks has no `slot.filled` line, and those garages look the same as before the day change. **FAIL if** a truck appears in an empty slot, a full warehouse is listed, or the new day has no `slot.filled` line.
5. Evidence: `slot.filled` (`warehouse`, `slot`, `order`, `assign`, `driver`). `slot.summary` with `bought=0` is the line when every slot already has a truck. A later day does not append those lines again.
6. Version marker: public `1.11.5`. `ceo.log` starts at `mod.loaded version=1.11.5 build=3682`.

Human PASS 2026-11-05 on 1.11.5. Day 1014 load: energy 70.47, hunger 66.3, happiness 100. Day 1015: seven `slot.filled` lines, W9 slot 1, W8 slot 1, W14 slots 1 and 2, W13 slots 1 and 2, W7 slot 1. Each names `Buildings.ContractVehicleForSale.Order` and `Entities.VehicleSlot.AssignVehicle`. `driver` is blank. No other warehouse is listed. Day 1016 rolled with no second `slot.filled`. Fourth Avenue 28 hair on W11: buffer 5530 on day 1015, buffer 1190 on day 1016.

## Store connect human check

1. Restart Big Ambitions so version 1.11.6 loads. You are in the city on the usual save. Do not load the day 1021 save. This check records a destination for each goods store whose warehouse holds that family, and records a contract drop when that hub holds the item. It does not call the game. The headquarters logistics list and the garages stay as they are. Fifth Avenue 36 stays on the wholesaler while its hub holds less than one day.
2. Let one day pass.
3. Open `%LocalAppData%\ChaoticCEO\ceo.log` and `%LocalAppData%\ChaoticCEO\routes.csv`.
4. **PASS if** `mod.loaded` is version 1.11.6, the new day has `store.connected` lines whose `add` is `UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination`, a store with no single-family hub has no line, Fifth Avenue 36’s wholesaler rows are still in `routes.csv`, and the headquarters logistics list is unchanged. **FAIL if** the new day has no `store.connected` line, a `drop` is anything other than `Entities.DeliveryContract.Remove` or blank, Fifth Avenue 36’s rows are gone, or a stop or a truck changes in the game.
5. Evidence: `store.connected` (`store`, `warehouse`, `item`, `add`, `drop`). A later day of that save does not append those lines again.
6. Version marker: public `1.11.6`. `ceo.log` starts at `mod.loaded version=1.11.6 build=3682`.

Human PASS 2026-11-05 on 1.11.6. Day 1015: 262 `store.connected` lines. Each `add` is `UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination`. Each `drop` is blank. 164 forecast gaps with a blank warehouse had no `store.connected` line. Fifth Avenue 36 had no connect line and kept four wholesaler rows. `routes.csv` has 266 rows.

## Plan assign human check

1. Restart Big Ambitions so version 1.11.7 loads. You are in the city on the usual save. Do not load the day 1021 save. This check records a pricing plan when none exists, then records one person for each open headquarters role: logistics, HR, purchasing, and headhunter. It does not call the game. BizMan’s plans stay as they are. Fifth Avenue 36 stays on the wholesaler.
2. Let one day pass.
3. Open `%LocalAppData%\ChaoticCEO\ceo.log`. Open BizMan’s headquarters plans.
4. **PASS if** `mod.loaded` is version 1.11.7, the new day has one `plan.created` line whose `create` is `UI.Smartphone.Apps.BizMan.PricingManagers.PricingManagersPlanList.AddPlan`, each `plan.assigned` line names `assign=Buildings.Office.Headquarters.HrManagerPlan.AssignEmployee` and a different role, a role that already has a person has no line, and the headquarters screens look the same as before the day change. **FAIL if** the new day has no `plan.created` line, a role is listed twice, `assign` is any other call, or a person appears on a plan that was empty.
5. Evidence: `plan.created` (`create`). `plan.assigned` (`role`, `employee`, `assign`). `plan.summary` with `created=0 assigned=0` is the line when the pricing plan exists and every role already has a person. A later day of that save does not append those lines again.
6. Version marker: public `1.11.7`. `ceo.log` starts at `mod.loaded version=1.11.7 build=3682`.

Human PASS 2026-11-05 on 1.11.7. Day 1014 load: energy 70.47, hunger 66.3, happiness 100. Day 1015: one `plan.created` (`AddPlan`), `plan.assigned` logistics `Db9eInvmDUm+4AmmCqNhHw==` and headhunter `+m2HNyl0Wku7yH2lq5wf3g==` (`AssignEmployee`). No line for hr or purchasing. Seven `slot.filled`. 262 `store.connected`, each `drop` blank. Day 1016: no second plan, slot, or connect lines. `gaps=426`. Fourth Avenue 28 hair buffer 1169. Morning routes 4, Fifth Avenue 36 kept.

## Daily audit human check

1. Restart Big Ambitions so version 1.11.9 loads. You are in the city on the usual save. Do not load the day 1021 save. This check connects up to 10 unconnected goods stores on the warehouse that holds the family. It does not buy a truck or place an import order. Fifth Avenue 36 stays on the wholesaler. Pressing F10 stops later days from healing.
2. Let one day pass.
3. Open `%LocalAppData%\ChaoticCEO\ceo.log` and `%LocalAppData%\ChaoticCEO\snapshots.log`. In BizMan, open a store named on an `audit.healed` line.
4. **PASS if** `mod.loaded` is version 1.11.9, the new day has one `audit.summary` whose `healed` is from 1 to 10 and whose `unconnected` does not include a store named on that day's `audit.healed` lines, at least one `audit.healed` line is `action=connect` `dry=false` and its `call` is `AddDestination`, that store is a destination on its warehouse plan, and Fifth Avenue 36 still has its wholesaler rows. **FAIL if** the new day has no `audit.summary`, `healed=0`, the connect `call` is not `AddDestination`, `dry=true`, a truck was bought, an import order was placed, or Fifth Avenue 36 lost the wholesaler contract.
5. Evidence: `audit.summary` (`unconnected`, `emptySlots`, `dry`, `mixed`, `unstaffed`, `missingPlans`, `cashBelowReserve`, `pinMismatch`, `healed`). `audit.healed` (`action`, `store`, `warehouse`, `item`, `call`, `dry`). `snapshot.taken` in `snapshots.log` before those heals. `autopilot.paused reason=failures` if three connects fail. `autopilot.paused reason=f10` if F10 was pressed. `autopilot.halted reason=pin` if the build pin does not match.
6. Version marker: public `1.11.9`. `ceo.log` starts at `mod.loaded version=1.11.9 build=3682`.

Human PASS 2026-11-06 on 1.11.9. Day 1015: `audit.summary` `unconnected=61 emptySlots=7 dry=4 mixed=7 unstaffed=0 missingPlans=3 cashBelowReserve=0 pinMismatch=0 healed=10`. Ten `audit.healed` lines, each `action=connect` `dry=false` and `AddDestination`. Fourth Avenue 28 hair to W11. First Avenue 38 used eight clothing items to W9. First Avenue 5 kabob to W7. Fifth Avenue 36 kept four wholesaler rows. `snapshot.taken version=1.11.9`.

## F9 top bar human check

1. Restart Big Ambitions so version 1.12.1 loads. You are in the city on the usual save. Do not load the day 1021 save. This check is the F9 top bar only. It does not add the health counts, Execute buttons, or the autopilot toggle.
2. Open BizMan and note the bank cash and yesterday's store profit. Note the personal wealth figure the game uses against $100,000,000. Note the game speed.
3. Press F9. The ChaoticCEO window opens. The first line is the bar. Press F9 again and it closes.
4. **PASS if** that line names the day, cash, net worth against 100000000, yesterday's profit, stage, autopilot on, and speed, and cash, net worth, profit, and speed match the figures from step 2. **FAIL if** the bar is missing, a figure disagrees with the game, autopilot reads off without F10, or the roadmap list is gone.
5. Evidence: one `bar.shown` line in `%LocalAppData%\ChaoticCEO\ceo.log` with the same day, cash, net, goal, profit, stage, autopilot, and speed. A later frame while the window stays open does not append it again.
6. Version marker: public `1.12.1`. `ceo.log` starts at `mod.loaded version=1.12.1 build=3682`.

Human PASS 2026-11-06 on 1.12.1. Day 1015: `bar.shown` cash 15874940, net worth 378834400 / 100000000, profit 1544496, stage 2, autopilot on, speed 1. The HUD showed $15.8M and +$1.5M on Sunday (Day 1015). The roadmap stayed under the bar.

## F9 health section human check

1. Restart Big Ambitions so version 1.12.2 loads. You are in the city on the usual save. Do not load the day 1021 save. This check is the health line under the top bar. It does not add Execute buttons or the autopilot toggle.
2. Let one day roll if today's `ceo.log` has no `audit.summary` yet. Press F9. The ChaoticCEO window opens. The first line is the bar. The next line is the health section. The roadmap stays under that.
3. Read the latest `audit.summary` in `%LocalAppData%\ChaoticCEO\ceo.log`.
4. **PASS if** the health line shows `Stores supplied unconnected=`, `Slots filled emptySlots=`, `Plans present missingPlans=`, and `Last heal healed=` with the same four numbers as that summary, and the last heal names the last `audit.healed` action for that summary (or `none` when `healed=0`). **FAIL if** the line is missing, a number disagrees with that summary, an older day's heal is shown when `healed=0`, or the roadmap is gone.
5. Evidence: one `health.shown` line with that same text. A later frame while the window stays open does not append it again. `audit.summary` (`unconnected`, `emptySlots`, `missingPlans`, `healed`) and the last `audit.healed` (`action`, `store`, `warehouse`, `item`, `role`).
6. Version marker: public `1.12.2`. `ceo.log` starts at `mod.loaded version=1.12.2 build=3682`.

Human PASS 2026-11-06 on 1.12.2. Day 1016: `health.shown` unconnected=61 emptySlots=7 missingPlans=3 healed=10, last heal connect First Avenue 5 kabob to W7. The latest `audit.summary` matches those four numbers. One `health.shown`. `mod.loaded version=1.12.2 build=3682`. The HUD showed $17.5M and +$1.3M.

## F9 controls human check

1. Restart Big Ambitions so version 1.12.3 loads. You are in the city on the usual save. Do not load the day 1021 save. This check is the F9 controls: Execute on each roadmap step, Execute all, the autopilot toggle, and dry run. It does not run the roadmap steps. That run is story 7.12. It does not start the daily autopilot loop. That loop is story 10.2.
2. Press F9. The first line is the bar. The next line is the health section. Each roadmap step has an Execute button. The bottom row has Autopilot on or off, Dry run on or off, and Execute all.
3. Click the Autopilot button. Press F10 once, with the window open or closed. Click Execute all.
4. **PASS if** the bar flips between Autopilot on and Autopilot off when you click the toggle, F10 leaves the bar on Autopilot off, and the bottom button says Execute all. **FAIL if** the bar stays the same, F10 leaves Autopilot on, Execute all is missing, or Execute all closes a store or changes the save.
5. Evidence: `controls.shown` names Autopilot, Dry run, and Execute all. A later frame with the same row does not append it again. `autopilot.paused reason=f10` after F10. A click on Execute all appends `controls.pressed` `action=execute-all story=7.12`. A click on Execute appends `controls.pressed` `action=execute`.
6. Version marker: public `1.12.3`. `ceo.log` starts at `mod.loaded version=1.12.3 build=3682`.

Human PASS 2026-11-06 on 1.12.3. Day 1016: the bar flipped Autopilot on to off and back, then `autopilot.paused reason=f10` left it off. `controls.pressed action=execute-all story=7.12`. Dry run then read on while autopilot stayed on. Cash 17568900, net worth 380540500 / 100000000, profit 1345948. No store closed.

## F9 daily gain sign

1. Restart Big Ambitions so version 1.12.3.1 loads. You are in the city on the usual save. Do not load the day 1021 save. This check is the number after Daily gain on each roadmap step. It does not change which rows are listed.
2. Press F9. Read a Close line and, if one is on screen, an Open line.
3. **PASS if** a Close line shows `Daily gain` with a minus, and an Open line shows `Daily gain` as the number with no plus. **FAIL if** a Close line shows a positive Daily gain, or an Open line starts that number with a plus.
4. Evidence: `roadmap.shown` `gain=` for a close that leads the list is negative. Second Avenue 10 on day 1015 is `gain=-52733.9531` when it is the first step.
5. Version marker: public `1.12.3.1`. `ceo.log` starts at `mod.loaded version=1.12.3.1 build=3682`.

## Daily telemetry human check

1. Restart Big Ambitions so version 1.12.4 loads. You are in the city on the usual save. Do not load the day 1021 save. This check is one `telemetry.csv` row per day, and the progress report from that file. It does not change the F9 window.
2. Let three days pass.
3. Open `%LocalAppData%\ChaoticCEO\telemetry.csv`. From the repo root run `python script/Report-Progress.py`. Open `%LocalAppData%\ChaoticCEO\progress.md`.
4. **PASS if** those three days are three rows, the report's latest day is the third day, and a negative profit in a row stays a minus in the report. **FAIL if** a day is missing, a day is listed twice, or the report says no days.
5. Evidence: one `telemetry.written` line per new day (`day`, `rows`, `actions`). `progress.written` (`days`, `latest`, `profit`) in `ceo.log`.
6. Version marker: public `1.12.4`. `ceo.log` starts at `mod.loaded version=1.12.4 build=3682`.

Human PASS 2026-11-07 on 1.12.4. Days 1015, 1016, and 1017: one row each, `telemetry.written` rows 1 then 2 then 3. Day 1017 matches `audit.summary`: unconnected 61, empty slots 7, dry 4, mixed 7, missing plans 3, healed 10. Cash 18228400, net worth 381213200, profit 464167. `progress.written days=3 latest=1017 profit=464167`. No day was repeated. `mod.loaded version=1.12.4 build=3682`.

## Rent a building human check

1. Restart Big Ambitions so version 1.13.1 loads. Start a new game and reach the city. Do not load the usual save and do not load the day 1021 save. This check rents one available lot. It does not open a store or change an existing empire.
2. Wait until the city is on screen.
3. Open `%LocalAppData%\ChaoticCEO\ceo.log` and look at the building you now rent.
4. **PASS if** one available lot is rented, `building.rented` names that street and number, and the call is `Buildings.BuildingCommands.Command_RentBuilding`. **FAIL if** no lot is rented, a second lot is rented, or the usual save gains a new rental.
5. Evidence: one `building.rented` line (`day`, `street`, `number`, `call`). A later frame does not append it again. A save that already rents appends `building.skipped reason=rented` and does not append `building.rented`.
6. Version marker: public `1.13.1`. `ceo.log` starts at `mod.loaded version=1.13.1 build=3682`.

Human PASS 2026-11-07 on 1.13.1. New game day 1: one `building.rented` for `ba:street_seventhavenue` number 26, call `Buildings.BuildingCommands.Command_RentBuilding`. The player was inside 26 7th Avenue. The day 1014 save in the same session logged `building.skipped reason=rented` and was not rented. `mod.loaded version=1.13.1 build=3682`.

## Starter layout human check

1. Restart Big Ambitions so version 1.13.2 loads. Start a new game and reach the city. Do not load the usual save and do not load the day 1021 save. This check rents one lot, names it, and loads one starter layout. It does not hire a cleaner or a guard, set uniforms, or sign a wholesaler. It does not change an existing empire.
2. Wait until you are inside the rented building.
3. Open `%LocalAppData%\ChaoticCEO\ceo.log`. Look at the business name and the furniture.
4. **PASS if** the business is Starter Gift, Starter Florist, or Starter Kabob, the hours are 8 to 16, and one `layout.loaded` line names that type, that layout, and `BusinessLayoutSets.BusinessLayoutSetHelper.LoadBusinessLayoutSet`. On a small lot the line is the gift shop `GiftShopWholesalers`. **FAIL if** the building stays empty, a second layout loads, or the usual save gains a store.
5. Evidence: one `layout.loaded` line (`day`, `street`, `number`, `type`, `layout`, `open`, `call`) after `building.rented` for the same street and number. A later frame does not append it again. A save that already has a named business appends `layout.skipped reason=open` and does not append `layout.loaded`.
6. Version marker: public `1.13.2`. `ceo.log` starts at `mod.loaded version=1.13.2 build=3682`.

Human PASS 2026-11-07 on 1.13.2. New game day 1: one `building.rented` for `ba:street_seventhavenue` number 20, then one `layout.loaded` for the same street and number, type `ba:businesstype_giftshop`, layout `GiftShopWholesalers`, `open=8-16`, call `BusinessLayoutSets.BusinessLayoutSetHelper.LoadBusinessLayoutSet`. The player was inside Starter Gift at 20 7th Avenue. The room had the gift-shop furniture. Cash on screen was $15.0K at 09:15. No second layout line. `mod.loaded version=1.13.2 build=3682`.

## Store ops human check

1. Restart Big Ambitions so version 1.13.3.9 loads. Start a new game and reach the city. Do not load the usual save and do not load the day 1021 save. This check rents one lot, loads one starter layout, and places one cleaner. Opening leaves uniforms off. The gift-shop layout has no guard locker, so this check does not assign a guard. It does not sign a wholesaler. Capacity fixtures are not part of this check.
2. Wait until you are inside the starter store, then wait about fifteen seconds.
3. Open `%LocalAppData%\ChaoticCEO\ceo.log`. Count mop people in the store.
4. **PASS if** one `ops.cleaner` line says `placed=true` on the same street and number as `layout.loaded`, one `ops.assigned` line names `cleaner=Cleaner`, `guard=` empty, and `uniforms=false`, and the store shows exactly one cleaner. **FAIL if** `ops.missed`, no `ops.cleaner`, two or more mop people, or a guard is named. The missing-employee alert stays until a cashier is hired (13.4) and does not fail this check. Uniforms are deferred.
5. Evidence: `ops.cleaner` (`employee`, `name`, `station`, `shift`, `person`, `type`, `placed`) then `ops.assigned` (`cleaner`, `guard`, `uniforms=false`, `cleanerCall`). `cleanerCall` is `CleaningStationController.AssignEmployee`. About 300 frames later, one `ops.verified` line (`shift`, `person`, `type`, `uniform`, `kept`).
6. Version marker: public `1.13.3.9`. `ceo.log` starts at `mod.loaded version=1.13.3.9 build=3682`.

Human PASS 2026-11-07 on 1.13.3.8 for the cleaner block: day 1 at 26 7th Avenue, one `ops.cleaner ... placed=true`, employee `MS4xJhJS10KNikV4k41zg==`. Uniforms deferred (`ops.missed reason=nolocker` on that build; opening no longer demands them on 1.13.3.9).

## Capacity fixtures human check

1. Restart Big Ambitions so version 1.13.8 loads. Start a new game and reach the city. Do not load the usual save and do not load the day 1021 save. This check rents one lot, loads one starter layout, places one cleaner, and matches customer capacity to that layout. It does not require a wholesaler, cashier, loan, or marketing.
2. Wait until you are inside the starter store, then wait about fifteen seconds so ops and capacity can run.
3. Open `%LocalAppData%\ChaoticCEO\ceo.log`. Open the store’s customer-capacity panel (or BizMan Insight) and note the capacity number.
4. **PASS if** one `capacity.matched` line names the same street and number as `layout.loaded` and `ops.assigned`, `call=Helpers.BusinessHelper.UpdateCustomerCapacity`, and the store’s customer capacity is greater than zero. **FAIL if** no `capacity.matched`, a second capacity line, or capacity stays at zero with fixtures already placed.
5. Evidence: after `ops.assigned` (or `ops.cleaner`), one `capacity.matched` (`day`, `street`, `number`, `call`). A later frame does not append it again.
6. Version marker: public `1.13.8`. `ceo.log` starts at `mod.loaded version=1.13.8 build=3682`.

Human PASS 2026-11-08 on 1.13.8. Day 1 at 26 7th Avenue: one `capacity.matched` with `call=Helpers.BusinessHelper.UpdateCustomerCapacity` after `ops.assigned` on the same street and number as `layout.loaded`. No second capacity line.

## Story 9.1 — Stage.Decide (headless + version smoke)

Tier 1: `StageTests` locks thresholds, boundaries, precedence, `Stage.Read` filtering, and the intentional `0 == 0` Stage 4 case. Chain test `… → marketing → stop → takeover → stage` locks `Stage.Read` through the real prefix at stage 1. Command: the Tier 1 `dotnet test` suite above.

Human version smoke (install changed; no stage UI yet — that is **9.6**):

1. Restart Big Ambitions so version 1.14.1 loads. Any save or a new game is fine.
2. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
3. Do nothing else for this check.
4. **PASS if** the first line is `mod.loaded version=1.14.1 build=3682`. **FAIL if** the version is still 1.13.8 or missing.
5. Evidence: that `mod.loaded` line.
6. Version marker: public `1.14.1`.

Human PASS 2026-11-08 on 1.14.1. `mod.loaded version=1.14.1 build=3682`. New-game day 1 at 26 7th Avenue ran the Epic 13 chain through `takeover.declined` on day 8.

## Story 9.2 — Open and Where (headless + version smoke)

Tier 1: `OpenTests` locks profit winner, later-type ties, Stage 1 wholesaler starters, **TruckBusy** service/office switch, Stage 3+ warehouse-for-second-store, and blanks. `WhereTests` locks **ClusterMin**, traffic/rent, later-building ties, cash-reserve exclusion, and the cost-equals-room edge. Chain `… → takeover → stage → open → where` locks Stage 1 gift shop onto the rented cluster street; chain `… → stage → open → where` with truck busy locks Stage 2 law firm onto the service cluster. Command: the Tier 1 `dotnet test` suite above.

Human version smoke (install changed; no open/where UI yet — that is **9.6**):

1. Restart Big Ambitions so version 1.14.2 loads. Any save or a new game is fine.
2. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
3. Do nothing else for this check.
4. **PASS if** the first line is `mod.loaded version=1.14.2 build=3682`. **FAIL if** the version is still 1.14.1 or missing.
5. Evidence: that `mod.loaded` line.
6. Version marker: public `1.14.2`.

Human PASS 2026-11-08 on 1.14.2. `mod.loaded version=1.14.2 build=3682`. New-game day 1 at 22 7th Avenue continued the Epic 13 chain through layout and ops.

## Story 9.3 — Golden layout and prices (headless + version smoke)

Tier 1: `GoldenLayoutTests` locks Stage 1 starter, Stage 2+ score winner for type/size, later-layout ties, starter fallback (never another size), score 0 skip, and blank blueprint. `PriceDecideTests` locks **PriceStep**, **PriceSatisfied**, leave-default raise, hold-and-raise, step-back on units or satisfaction, later-price ties, never-default on step-back, and empty-trial keep. Chain `… → open → where → layout` locks Stage 1 gift shop onto `GiftShopWholesalers`; chain `… → layout → prices` locks Stage 2 score golden then a raise. Command: the Tier 1 `dotnet test` suite above.

Human version smoke (install changed; no layout/price UI yet — that is **9.6**):

1. Restart Big Ambitions so version 1.14.3 loads. Any save or a new game is fine.
2. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
3. Do nothing else for this check.
4. **PASS if** the first line is `mod.loaded version=1.14.3 build=3682`. **FAIL if** the version is still 1.14.2 or missing.
5. Evidence: that `mod.loaded` line.
6. Version marker: public `1.14.3`.

Human PASS 2026-11-08 on 1.14.3. `mod.loaded version=1.14.3 build=3682`. New-game day 1 at 22 7th Avenue continued the Epic 13 chain through layout and ops.

## Story 9.4 — Supply, trucks, and warehouses (headless + version smoke)

Tier 1: `SupplyDecideTests` locks Stage 1 wholesaler, Stage 2+ warehouse source and contract drop, recurring-over-dated, refill **RefillDays**, zero-amount non-hub, and recurring without stock. `TruckDecideTests` locks empty delivering-slot spend after reserve, non-delivering skip, and exact reserve+cost. `WarehouseDecideTests` locks enter/stock home, shelves required, trucks/short second warehouse, normal dip no-second, and mixed KeepFamily. Chain `… → layout → prices → supply` locks Stage 2 warehouse source plus truck and second-warehouse picks; chain Stage 1 prefix locks wholesaler source after prices. Command: the Tier 1 `dotnet test` suite above.

Human version smoke (install changed; no supply UI yet — that is **9.6**):

1. Restart Big Ambitions so version 1.14.4 loads. Any save or a new game is fine.
2. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
3. Do nothing else for this check.
4. **PASS if** the first line is `mod.loaded version=1.14.4 build=3682`. **FAIL if** the version is still 1.14.3 or missing.
5. Evidence: that `mod.loaded` line.
6. Version marker: public `1.14.4`.

Human PASS 2026-11-08 on 1.14.4. `mod.loaded version=1.14.4 build=3682`. New-game day 1 at 30 7th Avenue continued the Epic 13 chain through layout and ops.

## Story 9.5 — Managers, money, real estate, close, and run (headless + version smoke)

Tier 1: `ManagerDecideTests` locks pricing-plan-before-hire, per-warehouse logistics/purchasing, **HrSpan** / **HuntSpan**, and unassigned-as-missing. `MoneyDecideTests` locks **ReserveDays**, **DaySpend**, Stage 1 **LoanCover** loan, and interest-vs-payback repay. `RealEstateDecideTests` locks Stage 4 kind order and **PaybackMax**. `CloseDecideTests` locks cost-center never-close, **FixDays** / **RedDays**, and connect-before-hire. `RunDecideTests` locks need floor, staff, store ops, marketing cover, tax, and takeover decline. Chain `… → prices → supply → managers` locks the pricing plan; chain `… → managers → money` locks DaySpend room after Stage 2 logistics. Command: the Tier 1 `dotnet test` suite above.

Human version smoke (install changed; F9 playbook step is **9.6**):

1. Restart Big Ambitions so version 1.14.5 loads. Any save or a new game is fine.
2. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
3. Do nothing else for this check.
4. **PASS if** the first line is `mod.loaded version=1.14.5 build=3682`. **FAIL if** the version is still 1.14.4 or missing.
5. Evidence: that `mod.loaded` line.
6. Version marker: public `1.14.5`.

Human PASS 2026-11-08 on 1.14.5. `mod.loaded version=1.14.5 build=3682`. New-game continued through day 8 with marketing stop and takeover decline on 1.14.5.

## Story 9.6 — F9 playbook next step (headless + F9 on a new save)

Tier 1: `PlaybookTests` locks need-floor over open, open over where/layout/money, managers over money, close over run staff and real estate, trucks over warehouses, blank board → none, `Playbook.Line` kinds, and one `playbook.shown` per step. Chain `… → managers → money → playbook` locks open as the step when growth is live; chain Stage 1 with idle growth locks the loan. Command: the Tier 1 `dotnet test` suite above.

Human check (F9 on a new save):

1. Restart Big Ambitions so version 1.14.6 loads. Start a **new game** and reach the city. Do not load the day 1021 save.
2. Press **F9**. Look at the panel line under the health section. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
3. Do: (1) confirm the top bar and health still draw, (2) read the Next line, (3) check `ceo.log` for one `playbook.shown` line.
4. **PASS if** F9 shows a `Next …` line (on a fresh new game usually `Next open ba:businesstype_giftshop`), `ceo.log` starts with `mod.loaded version=1.14.6 build=3682`, and one `playbook.shown` names that kind and detail. **FAIL if** no Next line, still `1.14.5`, or no `playbook.shown`.
5. Evidence: `playbook.shown version=1.14.6 kind=… detail=…`. A later frame with the same step does not append another line.
6. Version marker: public `1.14.6`.

Human PASS 2026-11-08 on 1.14.6. New-game day 8 at 22 7th Avenue: F9 showed `Next open ba:businesstype_giftshop`; `mod.loaded version=1.14.6 build=3682`; one `playbook.shown kind=open detail=ba:businesstype_giftshop`.

## Story 8.1 — Demand and cost curves (headless + version smoke)

Tier 1: `CurvesTests` locks mean sales and goods-per-sale, fixed-cost averages, zero-sales goods-rate isolation, cost-only stores, street/number order, empty input, `Curves.Project` statement rows, and `Curves.FitFile` ledger columns. Chain `… → playbook → curves` locks fitted demand/cost after the playbook step; chain `… → playbook → curves` via FitFile locks full ledger columns into `Project`. Command: the Tier 1 `dotnet test` suite above.

Human version smoke (install changed; curves are headless):

1. Restart Big Ambitions so version 1.15.1 loads. Any save or a new game is fine.
2. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
3. Do nothing else for this check.
4. **PASS if** the first line is `mod.loaded version=1.15.1 build=3682`. **FAIL if** the version is still 1.14.6 or missing.
5. Evidence: that `mod.loaded` line.
6. Version marker: public `1.15.1`.

Human PASS 2026-11-08 on 1.15.1. `mod.loaded version=1.15.1 build=3682`.

## Story 8.2 — Day stepper (headless + version smoke)

Tier 1: `DayStepTests` locks projected statement rows from curves, multi-day run, portfolio.csv-shaped TCO rows, Advisor.Rank through DayStep, empty input, unmatched-street drop with street/number order, and projected row order. Chain `… → curves → day-step` locks projected rank and TCO after the playbook/curves prefix; chain via FitFile locks ledger columns into a multi-day portfolio TCO. Command: the Tier 1 `dotnet test` suite above.

Human version smoke (install changed; day stepper is headless):

1. Restart Big Ambitions so version 1.15.2 loads. Any save or a new game is fine.
2. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
3. Do nothing else for this check.
4. **PASS if** the first line is `mod.loaded version=1.15.2 build=3682`. **FAIL if** the version is still 1.15.1 or missing.
5. Evidence: that `mod.loaded` line.
6. Version marker: public `1.15.2`.

Human PASS 2026-11-08 on 1.15.2. `mod.loaded version=1.15.2 build=3682`.

## Story 8.3 — Calibration (headless + version smoke)

Tier 1: `CalibrateTests` locks day-1000→1014 within **CalibrateTol**, relative Error/Within, fit-only-at-or-before-start (later days ignored), empire profit over matching stores, empty/missing end day, CheckFile ledger CSV, and near-zero actual floor. Chain `… → day-step → calibrate` locks Within after DayStep; chain via FitFile locks CheckFile Within. Command: the Tier 1 `dotnet test` suite above.

Human version smoke (install changed; calibration is headless):

1. Restart Big Ambitions so version 1.15.3 loads. Any save or a new game is fine.
2. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
3. Do nothing else for this check.
4. **PASS if** the first line is `mod.loaded version=1.15.3 build=3682`. **FAIL if** the version is still 1.15.2 or missing.
5. Evidence: that `mod.loaded` line.
6. Version marker: public `1.15.3`.

Human PASS 2026-11-08 on 1.15.3. `mod.loaded version=1.15.3 build=3682`.

## Story 8.4 — Seeded noise (headless + version smoke)

Tier 1: `NoiseTests` locks seeded `Draw` inside **DemandNoise** / **RivalNoise** / **HireNoise**, `Project` sales/rival/hiring scales, quiet Project matches `Curves.Project`, `Trial` determinism, `Run` net-worth distribution at **SimHorizonDays**, empty inputs, and hiring-only wages. Chain `… → calibrate → noise` locks the distribution after Calibrate; chain via FitFile locks file curves into `Run`. Command: the Tier 1 `dotnet test` suite above.

Human version smoke (install changed; noise is headless):

1. Restart Big Ambitions so version 1.15.4 loads. Any save or a new game is fine.
2. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
3. Do nothing else for this check.
4. **PASS if** the first line is `mod.loaded version=1.15.4 build=3682`. **FAIL if** the version is still 1.15.3 or missing.
5. Evidence: that `mod.loaded` line.
6. Version marker: public `1.15.4`.

Human PASS 2026-11-08 on 1.15.4. `mod.loaded version=1.15.4 build=3682`.

## Story 8.5 — Compare policies (headless + version smoke)

Tier 1: `PolicyTests` locks **PolicyWorstTol**, `Acceptable` against own median, `Compare` picks the higher median when the worst stays acceptable, keeps the stable policy when a higher median breaks the tol, median ties keep the earlier name, empty/null/blank inputs, and blank-name skip. Chain `… → noise → policy` picks close over keep after calibrate/noise; FitFile chain keeps stable over gamble. Command: the Tier 1 `dotnet test` suite above.

Human version smoke (install changed; policy compare is headless):

1. Restart Big Ambitions so version 1.15.5 loads. Any save or a new game is fine.
2. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
3. Do nothing else for this check.
4. **PASS if** the first line is `mod.loaded version=1.15.5 build=3682`. **FAIL if** the version is still 1.15.4 or missing.
5. Evidence: that `mod.loaded` line.
6. Version marker: public `1.15.5`.

Human PASS 2026-11-08 on 1.15.5. `mod.loaded version=1.15.5 build=3682`.

## Story 8.6 — Stage sim (headless + version smoke)

Tier 1: `StageSimTests` locks **StageThresholds.Starting** against every named production const, `Stage.Decide` with thresholds, stage-1 open cap at **Stage2Stores**, `Trial` / `Run` win flags against **WinNetWorth**, empty inputs, and `Tune` (climb over tight, median tie, blank skip). Chain `… → policy → stage-sim` reports win flags after calibrate/noise/policy; FitFile chain tunes climb over tight. **14.13** extends this with `StageSimMorningTests`: `day step → live decides → close → tax`. The climb check uses 90 days. Command: the Tier 1 `dotnet test` suite above.

Human version smoke (install changed; stage sim is headless):

1. Restart Big Ambitions so version 1.15.6 loads. Any save or a new game is fine.
2. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
3. Do nothing else for this check.
4. **PASS if** the first line is `mod.loaded version=1.15.6 build=3682`. **FAIL if** the version is still 1.15.5 or missing.
5. Evidence: that `mod.loaded` line.
6. Version marker: public `1.15.6`.

Human PASS 2026-11-08 on 1.15.6. `mod.loaded version=1.15.6 build=3682`.

## Story 10.1 — Win milestone (headless + version smoke)

Tier 1: `WinTests` locks **Win.Milestone** (default **WinNetWorth**, owner `Approve`), `Crossed`, `Record` once with `win.reached`, Autopilot stays on, blank inputs, and already-logged skip. Chain `… → stage-sim → win` logs once from the stage-sim median; FitFile chain tunes climb then logs. Command: the Tier 1 `dotnet test` suite above.

Human version smoke (install changed; win.reached needs a crossing and is locked headless):

1. Restart Big Ambitions so version 1.16.1 loads. Any save or a new game is fine.
2. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
3. Do nothing else for this check.
4. **PASS if** the first line is `mod.loaded version=1.16.1 build=3682`. **FAIL if** the version is still 1.15.6 or missing.
5. Evidence: that `mod.loaded` line.
6. Version marker: public `1.16.1`.

Human PASS 2026-11-08 on 1.16.1. `mod.loaded version=1.16.1 build=3682`.

## Story 10.2 — Autopilot day loop

Tier 1: `AutopilotTests` locks `Autopilot.Day` (audit → stage action via recorded call → time advance → telemetry) when enabled, skip when cleared / F10, CallKey maps, none-step, blank inputs, and Enabled-flag winner over a planted guard. Chain `… → win → autopilot` runs the real day; a second chain locks F10 skip after win. Command: the Tier 1 `dotnet test` suite above.

Human check:

1. Restart Big Ambitions so version 1.16.2 loads. Start a **new** game and reach the city. Do not load the day 1021 save. Do not continue a save that already says Starter Gift. This check is one autopilot day and a cleared-flag skip. It does not require a full hands-off win run (that is 10.3).
2. Leave Autopilot on. Let one day roll under autopilot. Open `%LocalAppData%\ChaoticCEO\ceo.log` and `%LocalAppData%\ChaoticCEO\telemetry.csv`.
3. Press F10 (or flip Autopilot off). Let the next day arrive.
4. **PASS if** the autopilot day has `autopilot.day … ran=true`, matching `audit.summary` / `telemetry.written` for that day, and the `telemetry.csv` row for that day matches the log; after clear, the next day has `autopilot.day … ran=false` and no new `autopilot.action` / heal for that skipped day. **FAIL if** version is still 1.16.1, the telemetry row disagrees with the day, or a cleared flag still heals or advances the stage action.
5. Evidence: `autopilot.day` (`ran`), `audit.summary`, optional `autopilot.action` (`kind`, `call`), `time.advanced` or live time care, `telemetry.written` (`day`, `autopilot`). After F10: `autopilot.paused reason=f10` then `autopilot.day ran=false`.
6. Version marker: public `1.16.2`. `ceo.log` starts at `mod.loaded version=1.16.2 build=3682`.

Human PASS 2026-11-09 on 1.16.2. New game: `mod.loaded version=1.16.2 build=3682`. Days 2–8 each `autopilot.day ran=true` with matching `audit.summary` and `telemetry.csv` (day 8: cash 14664, net 14199, profit -106, stage 1, missingPlans 1). F10 logged `autopilot.paused reason=f10` and the bar flipped Autopilot off. Live path does not call the day loop when cleared, so no further `autopilot.action` after F10.

## Story 10.3 — Store #1 Ready + stall

Exit is Store #1 `ready=true` × ProfitProofDays and the stall pause — not `win.reached`. Stories **10.4–10.7** get their own human checks when started. Full `$100M` soak is **10.7** only.

Tier 1: `StoreHealthTests` / stall Decide (completed open-day g4/g5; StuckDays → remedy; exhausted → `reason=stuck`); chain through ExpansionAllowed (no open while not Ready). Soak-speed / Resume skip already PASS on 1.16.5.6. Command: the Tier 1 `dotnet test` suite above.

Human check (Ready×3):

1. Restart Big Ambitions so **1.16.6.1** loads. Start a **new** game; Autopilot on.
2. Let Store #1 run until F9 / `ceo.log` show `store.health … ready=true` for **ProfitProofDays** (3) open days. Do not expect Store #2. While cash-blocked for Store #2, days should still advance; tax/demand/marketing/takeover still run when due.
3. **PASS if** `ready=true` holds for those days with non-zero `revenue`/`profits` after sales, no Store #2 while not Ready, and if a gate stalls past **StuckDays** you see `autopilot.paused reason=stuck`. **FAIL if** `ready` stays false with `customers>0 revenue=0`, Store #2 opens while not Ready, or time freezes while waiting on cash.
4. Evidence: `store.health` (`g1`–`g5`, `ready`, `customers`, `revenue`, `profits`), optional `stall.remedy` / `autopilot.paused reason=stuck`, optional `tax.paid` / `marketing.started` / `takeover.declined`.
5. Version marker: public `1.16.6.1`. `ceo.log` starts at `mod.loaded version=1.16.6.1 build=3682`.

Prior soak-speed / Resume skip PASS 2026-11-09 on 1.16.5.6 remains.

## Wholesaler contract human check

1. Restart Big Ambitions so version 1.13.4 loads. Start a new game and reach the city. Do not load the usual save and do not load the day 1021 save. This check rents one lot, loads one starter layout, places one cleaner, and signs a recurring wholesaler `DeliveryContract` for the starter gift shop. It does not hire a cashier. Capacity fixtures are not part of this check.
2. Wait until you are inside the starter store, then wait about fifteen seconds so ops and the contract can run. Skip time to the next morning (sleep or the day change) and walk back into the store.
3. Open `%LocalAppData%\ChaoticCEO\ceo.log`. Look at store shelves for gift stock.
4. **PASS if** one `contract.signed` line names the same street and number as `layout.loaded`, `wholesale=ba:street_firststreet 37`, and `call=Dialogs.WholesaleStoreManagerDialog.StartContract`, and the next morning the store has gift stock on the shelves. **FAIL if** no `contract.signed`, a second contract line, `contract.missed`, or the shelves are empty the next morning.
5. Evidence: after `ops.assigned` (or `ops.cleaner`), one `contract.signed` (`day`, `street`, `number`, `wholesale`, `call`). A later frame does not append it again.
6. Version marker: public `1.13.4`. `ceo.log` starts at `mod.loaded version=1.13.4 build=3682`.

Human PASS 2026-11-07 on 1.13.4 for the contract log: day 1 at 26 7th Avenue, one `contract.signed` with `wholesale=ba:street_firststreet 37` and `call=Dialogs.WholesaleStoreManagerDialog.StartContract` after `ops.assigned`. Next-morning shelf stock waived.

## Hire, loan, demand, and taxes human check

1. Restart Big Ambitions so version 1.13.5 loads. Start a new game and reach the city. Do not load the usual save and do not load the day 1021 save. This check rents one lot, loads one starter layout, places one cleaner, signs a wholesaler contract, hires a cashier, and takes a bank loan. Demand clear and tax pay run when the game already has an unmet demand or a tax balance.
2. Wait until you are inside the starter store and the missing-employee alert is gone.
3. Open `%LocalAppData%\ChaoticCEO\ceo.log`. Check the bank loan list in the phone Econo view.
4. **PASS if** one `hire.cashier` line names `role=Cashier`, `skill=ba:skill_customerservice`, and `call=Helpers.EmployeeHelper.HireCandidate` on the same street and number as `contract.signed`, the register worker is in the store, and one `loan.taken` line names a positive `amount` with `call=Dialogs.BankDialog.LoanRequest`. **FAIL if** `hire.missed`, no cashier line, no loan line, or the missing-employee alert stays after those lines.
5. Evidence: `hire.cashier` (`day`, `street`, `number`, `role`, `skill`, `type`, `call`, `stationCall`) and `loan.taken` (`day`, `amount`, `dailyPayment`, `call`) after `contract.signed`. A later frame does not append them again. `demand.cleared` and `tax.paid` are optional on a brand-new day 1 save.
6. Version marker: public `1.13.5`. `ceo.log` starts at `mod.loaded version=1.13.5 build=3682`.

Human PASS 2026-11-08 on 1.13.5. Day 1 at 15 7th Avenue: one `hire.cashier` with `role=Cashier`, `skill=ba:skill_customerservice`, `type=SelfServiceEmployee`, `call=Helpers.EmployeeHelper.HireCandidate`, `stationCall=Controllers.CashRegisterController.AssignEmployee` after `contract.signed`, and one `loan.taken` with `amount=500`, `dailyPayment=5`, `call=Dialogs.BankDialog.LoanRequest`. Cash on screen was $15.5K. No `hire.missed`. `demand.cleared` and `tax.paid` were not due on that new save.

## Time advance with player needs human check

1. Restart Big Ambitions so version 1.13.6 loads. Start a new game and reach the city. Do not load the usual save and do not load the day 1021 save. This check rents, loads the starter layout, places one cleaner, signs a wholesaler contract, hires a cashier, takes a cover-fit bank loan, then advances time one day at a time for a week. Sleep and food run when energy, hunger, or happiness are at or below NeedFloor (20).
2. Wait until `loan.taken` is in the log, then stay in the city while the week advances. Do not faint and do not go to the hospital.
3. Open `%LocalAppData%\ChaoticCEO\ceo.log`. Note the in-game day when the week finishes.
4. **PASS if** seven `time.advanced` lines each name `span=1d` and `call=Timemachine.TimeMachine.SkipTime` after `loan.taken`, the in-game day is about a week later, and you never fainted or went to the hospital. **FAIL if** fewer than seven advance lines, a faint or hospital trip, or `time.advanced` appears before `loan.taken`.
5. Evidence: `time.advanced` (`day`, `span`, `call`). Optional `time.cared` (`sleep`, `food`, `sleepCall`, `foodCall`) when a need was at or below the floor. A later frame does not append a given advance line again.
6. Version marker: public `1.13.6`. `ceo.log` starts at `mod.loaded version=1.13.6 build=3682`.

Human PASS 2026-11-08 on 1.13.6. Day 1 at 26 7th Avenue: `loan.taken` (`amount=500`, `call=Dialogs.BankDialog.LoanRequest`), then seven `time.advanced` lines (`span=1d`, `call=Timemachine.TimeMachine.SkipTime`) on days 1–7, `day.rolled` through day 8. Screen day 8 at 12:19 AM, cash $15.3K, no faint or hospital. No `time.cared` (needs stayed above NeedFloor).

## Marketing and rival decline human check

1. Restart Big Ambitions so version 1.13.7 loads. Start a new game and reach the city. Do not load the usual save and do not load the day 1021 save. This check rents, loads the starter layout, places one cleaner, signs a wholesaler contract, hires a cashier, takes a cover-fit bank loan, advances time one day at a time for a week, starts a SmallInternet marketing campaign, stops it after the week, and declines a weak rival takeover offer.
2. Wait until `loan.taken` is in the log, then stay in the city through the week. Open BizMan → Marketing on the starter store while the campaign is active, then again after it ends.
3. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
4. **PASS if** one `marketing.started` line names `type=SmallInternet` and `call=Dialogs.MarketingAgencyDialog.OnMarketingSettingsSet` after `loan.taken`, one `marketing.stopped` line names `call=UI.Smartphone.Apps.BizMan.BizManMarketing.CancelAllCampaignsFromAgency` after the week of `time.advanced`, BizMan showed the campaign then cleared it, and one `takeover.declined` line names `call=UI.Smartphone.Apps.BizMan.RealEstateSettings.CancelForSale`. **FAIL if** no start line, no stop line, the campaign never appears or never clears, or no decline line.
5. Evidence: `marketing.started` (`day`, `street`, `number`, `type`, `dailyCost`, `call`), `marketing.stopped` (`day`, `street`, `number`, `call`), `takeover.declined` (`day`, `street`, `number`, `offer`, `dailyProfit`, `call`). A later frame does not append those lines again.
6. Version marker: public `1.13.7`. `ceo.log` starts at `mod.loaded version=1.13.7 build=3682`.

Human PASS 2026-11-08 on 1.13.7. Day 1 at 15 7th Avenue: `marketing.started` (`type=SmallInternet`, `dailyCost=100`, `call=Dialogs.MarketingAgencyDialog.OnMarketingSettingsSet`) after `loan.taken`, seven `time.advanced` (`span=1d`), then on day 8 `marketing.stopped` (`call=UI.Smartphone.Apps.BizMan.BizManMarketing.CancelAllCampaignsFromAgency`) and `takeover.declined` (`offer=1000`, `dailyProfit=10`, `call=UI.Smartphone.Apps.BizMan.RealEstateSettings.CancelForSale`). Screen day 8 Monday 12:43 AM, cash $14.6K; BizMan Marketing showed no active campaigns after the stop.

## Warehouse sort and restock human check

1. Restart Big Ambitions so version 1.11.8.1 loads. You are in the city on the usual save. Do not load the day 1021 save. This check records warehouse homes, drains, overflow, and the first import order. It does not call the game. BizMan’s warehouses and purchasing screens stay as they are. Fifth Avenue 36 stays on the wholesaler.
2. Let one day pass.
3. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
4. **PASS if** `mod.loaded` is version 1.11.8.1, the new day has one `warehouse.drain` for specialty from W9 to W12 whose `assign` is `ChangeAssignedWarehouse`, a `restock.ordered` for W12 specialty with a positive `amount` whose `assign` and `start` are `ChangeAssignedWarehouse` and `StartOrder`, no `warehouse.overflow` unless a home already has an order and is still short, and the warehouses and purchasing screens look the same as before the day change. **FAIL if** the new day has no `warehouse.drain`, a drain or order names any other call, W13 or W14 is claimed without the overflow trigger, or a warehouse or order changes in the game.
5. Evidence: `warehouse.drain` (`family`, `from`, `to`, `amount`, `assign`). `warehouse.overflow` (`family`, `home`, `second`, `reason`) when present. `restock.ordered` (`warehouse`, `family`, `amount`, `assign`, `start`). A later day of that save does not append those lines again.
6. Version marker: public `1.11.8.1`. `ceo.log` starts at `mod.loaded version=1.11.8.1 build=3682`.

Human PASS 2026-11-05 on 1.11.8.1. Day 1015: 10 `warehouse.drain` lines, each `assign` is `ChangeAssignedWarehouse`. Specialty to W12: W3 58, W5 40, W8 166, W9 226, W10 72. Durables to W9: W2 37, W3 134, W8 19. Service to W11: W3 14, W6 87. Four `restock.ordered` lines, each `start` is `StartOrder`: W5 food 542550, W9 durables 80956, W11 service 25297, W12 specialty 32382. No `warehouse.overflow`. `gaps=426`. Fifth Avenue 36 stayed the wholesaler destination. The log ends on day 1015.

Prior PASS 2026-11-05 was the dry run on 1.11.1. Day rolled 1014 to 1015. Fourth Avenue 28 sales 57847, profit 25010.7891. First forecast: `noplan` on W11, `buffer=0`. `gaps=3836`. Four moves, all `dry=true`. Routes stayed the four Fifth Avenue 36 contract rows.

## Story 6.1 human check

1. Restart Big Ambitions so this build loads, then load the empire save. You are in the city, not the main menu. This story draws the advisor window. It does not close a store, buy a building, or change logistics.
2. Open BizMan and note the stores at the bottom of the profit list.
3. Press F9. A window titled ChaoticCEO opens. Press F9 again and it closes.
4. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
5. **PASS if** the red rows are those bottom stores, each with one explain line, the newest `mod.loaded` line is `mod.loaded version=1.6.1.2 build=3682`, and the log has one `panel.opened version=1.6.1.2` line plus an `advice.shown` line for each row from that open. **FAIL if** the window is missing, the text sits on BizMan with no dark panel, scrolling the list also zooms the city, the version is `1.6.1.1`, or those log lines are missing.
6. Evidence: `mod.loaded`, `panel.opened`, and `advice.shown`. The last red row is the worst store. At most ten red rows are shown. Warning and open lines can follow them.
7. Version marker: public `1.6.1.2`. The installed dll informational version is `1.6.1.2+N`. The window is a dark panel with light text.

Human PASS 2026-10-30: the F9 window is readable, and scrolling it does not zoom the city. `mod.loaded version=1.6.1.2 build=3682`.

## Story 7.7 human check

1. Restart Big Ambitions so this build loads, then load the empire save. You are in the city, not the main menu. This story puts a Close button on each close step. It does not redo a delivery, change a price, hire, buy a building, or copy a layout.
2. Press F9. Each close step has a Close button. An open step does not. A logistics line does not.
3. Click Close on one close step. That button is replaced by a red Confirm and a Cancel. Cancel leaves the store open. Confirm closes it. That row leaves the F9 window. The street sign changes. Reload the last save if you want the store back. The mod does not save.
4. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
5. **PASS if** that store is gone from the city and from the F9 window, and `ceo.log` has one `store.closed` line for that street and number. **FAIL if** the store is still open, the row stays on the window, the line is missing, or the log has `store.missed` for that street and number.
6. Evidence: `store.closed version=1.7.2 street=<that street> number=<that number> call=Helpers.BusinessHelper.ShutdownBusiness`. `store.missed` means the building was not found.
7. Version marker: public `1.7.2`. The installed dll informational version is `1.7.2+N`.

Chain: `dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown → delivery → apply → offer → clone → shown → closed → gone`.

Human PASS 2026-10-31. F9 opens and closes. Scroll stays in the window. First open `rows=9`, `steps=8`, Ninth Street 4, gain 3162.0491, `move=1`. Cancel left the row. Confirm removed each close row. Eight `store.closed` lines, no `store.missed`. Next open `rows=1`, `steps=0`, `move=1`.

## Story 7.8 human check

1. Restart Big Ambitions so this build loads, then load the empire save. You are in the city, not the main menu. This story puts a Redo button on the logistics move. It does not change a price, hire, buy a building, or copy a layout.
2. Press F9. The logistics line has a Redo button. A close step still has Close. An open step has neither.
3. Click Redo. That button is replaced by a red Confirm and a Cancel. Cancel leaves the warehouse as it was. Click Redo again, then Confirm.
4. Open the headquarters BizMan, Logistics Managers, and the plan for the warehouse named after “To”. That is not the warehouse Drivers tab and not the store’s Deliveries tab. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
5. **PASS if** that plan lists the store, and `ceo.log` has one `logistics.redone` line for that destination. **FAIL if** the plan does not list the store, the line is missing, or the log has `logistics.missed` for that destination.
6. Evidence: `logistics.redone version=1.7.3.2 destination=<street number> from=<old warehouse> to=<new warehouse> family=<family> call=UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination`. `logistics.missed` with `reason=noplan` means the named warehouse building was not found.
7. Version marker: public `1.7.3.2`. The installed dll informational version is `1.7.3.2+N`. `ceo.log` starts at `mod.loaded version=1.7.3.2 build=3682`.

Chain: `dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown → delivery → apply → offer → clone → shown → closed → gone → redone`.

Human PASS 2026-11-02. `ceo.log` starts at `mod.loaded version=1.7.3.2 build=3682`. One open: `rows=9`, `steps=8`, Ninth Street 4, gain 3162.0491, `move=1`. One `logistics.redone` for Fifth Avenue 36, from First Street 37, to W7, family food. No `logistics.missed`.

## Story 7.6 human check

1. Restart Big Ambitions so this build loads, then load the empire save. You are in the city, not the main menu. This story draws the roadmap. It does not close a store, buy a building, change a price, or move a delivery.
2. Press F9. The ChaoticCEO window opens. The first lines are ranked steps. Each step names a close or an open, a street and number, and a daily gain. A logistics reassignment, when there is one, is its own line under those steps. The old bottom-profit list is still below that.
3. Scroll the list. The city does not zoom. Press F9 again and the window closes.
4. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
5. **PASS if** the window shows the ranked steps and the logistics move, the old Close list is gone, `ceo.log` starts at `mod.loaded version=1.7.1.2 build=3682`, and `history.log` has one `roadmap.changed` line for that list. **FAIL if** the old red Close rows are still in the window, `ceo.log` still starts with an older `mod.loaded`, or a second open of the same list adds another `roadmap.changed` line.
6. Evidence: `mod.loaded` in `ceo.log`. `roadmap.changed` in `history.log` (`steps`, `street`, `number`, `gain`, `move`).
7. Version marker: public `1.7.1.2`. The installed dll informational version is `1.7.1.2+N`. The window is the same dark panel with light text.

Human PASS 2026-10-31. `ceo.log` starts at `mod.loaded version=1.7.1.2 build=3682`. Two opens, `rows=11`, one `roadmap.shown` pair for Tenth Street 13, gain 13771.4512, move=1. `history.log` has one `roadmap.changed` for that list.

## Story 7.5

1. You can stay out of the game. This story does not copy a layout onto a store and does not add a button. It does not run the roadmap.
2. You should see no layout copied onto a building.
3. Do not press a blueprint or layout control. Do not advance the day.
4. **PASS if** no layout was copied onto a store. **FAIL if** a layout was loaded into a building.
5. Evidence: a confirmed copy logs `layout.cloned version=1.6.1.2 street=ba:street_main family=food blueprint=soda-line source=1 destination=4 call=BusinessLayoutSets.BusinessLayoutSetHelper.LoadBusinessLayoutSet`. Play does not append that line. Reload does not append another one. The statement file and the route file stay as they were.
6. Version marker: public `1.6.1.2`. The installed dll informational version is `1.6.1.2+N`.

Chain: `dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown → delivery → apply → offer → clone`.

Human PASS 2026-10-30. Nothing to press in game. One confirmed copy clones that layout and logs `layout.cloned`. Each copy is separate. An unconfirmed copy stays uncopied. A different blueprint stays uncopied. A blank family stays uncopied. A buy on that street does not replace the blueprint. Reload clears the copy and leaves the statement file and the route file. The running game does not call LoadBusinessLayoutSet. Version stays 1.6.1.2.

## Story 7.4

1. You can stay out of the game. This story does not send a purchase offer and does not add a button.
2. You should see no building bought.
3. Do not press a buy control. Do not advance the day.
4. **PASS if** no building was bought. **FAIL if** a purchase offer was sent.
5. Evidence: a confirmed offer logs `building.offered version=1.6.1.2 street=ba:street_fourthavenue number=28 call=BizManPresentation.SendBuyBuildingOffer`. Play does not append that line. Reload does not append another one. The statement file and the route file stay as they were.
6. Version marker: public `1.6.1.2`. The installed dll informational version is `1.6.1.2+N`.

Chain: `dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown → delivery → apply → offer`.

Human PASS 2026-10-30. Nothing to press in game. One confirmed buy offers that building and logs `building.offered`. Each buy is separate. An unconfirmed buy stays unbought. A close step stays unbought. Another number stays unbought. Reload clears the offer and leaves the statement file and the route file. The running game does not call SendBuyBuildingOffer. Version stays 1.6.1.2.

## Story 7.3

1. You can stay out of the game. This story does not set a retail price, does not hire, and does not add a button.
2. You should see no price change and no staffing change on a store.
3. Do not press a pricing or hiring control. Do not advance the day.
4. **PASS if** no retail price and no staff plan changed. **FAIL if** a price was written or a staff plan was hired.
5. Evidence: a confirmed price logs `price.applied version=1.6.1.2 item=ba:itemname_sodacan price=2.75 call=Buildings.Office.Headquarters.PricingManagerPlan.SetPrice`. A confirmed staff plan logs `staff.applied version=1.6.1.2 role=food kind=hr plan=hr-soda amount=40`. Play does not append those lines. Reload does not append another one. The statement file and the route file stay as they were.
6. Version marker: public `1.6.1.2`. The installed dll informational version is `1.6.1.2+N`.

Chain: `dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown → delivery → apply`.

Human PASS 2026-10-30. Nothing to press in game. One confirmed price sets that item and logs `price.applied`. One confirmed staff plan keeps that plan and logs `staff.applied`. Each apply is separate. An unconfirmed price stays unset. A different item stays unset. An unconfirmed plan stays unstaffed. Reload clears both and leaves the statement file and the route file. The running game does not call SetPrice. Version stays 1.6.1.2.

## Story 7.2

1. You can stay out of the game. This story does not rewrite a delivery plan and does not add a button.
2. You should see no warehouse change on a store.
3. Do not press a logistics control. Do not advance the day.
4. **PASS if** no store's warehouse assignment changed. **FAIL if** a delivery plan was rewritten.
5. Evidence: a confirmed redo logs `logistics.redone version=1.6.1.2 destination=ba:street_side 9 from=W1 to=W2 family=specialty call=UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination`. Play does not append that line. Reload does not append another one. The routes file stays as it was.
6. Version marker: public `1.6.1.2`. The installed dll informational version is `1.6.1.2+N`.

Human PASS 2026-10-30. Nothing to press in game. One confirmed redo moves that selected store and logs `logistics.redone`. Each redo is separate. An unconfirmed redo stays put. A different destination stays put. Reload clears the redo and leaves the route file. The running game does not call AddDestination. Version stays 1.6.1.2.

## Story 7.1

Headless. Human PASS 2026-10-30. Nothing to press in game. One confirmed close shuts that selected store and logs `store.closed`. Each close is separate. An unconfirmed close stays open. An open step is not a close. A store outside the list stays open. A blank close call stays open. Reload clears the close and leaves the statement file in place. The running game does not call ShutdownBusiness. Version stays 1.6.1.2.

## Story 6.6

Headless. No play check. An ordered top-N action list. The larger daily gain is first. A close gain is the latest loss. An open gain is the latest rent. A tie keeps the later step. A repeated score line stays one close. The logistics move is not a step. The running game does not run the list. Version stays 1.6.1.2.

## Story 6.5

Headless. Human PASS 2026-10-30. Payback days are the purchase price divided by the latest daily rent. The shorter payback is the one street to open and the one building to buy. A tie keeps the later building. A zero or negative price or rent is not a buy. The logistics move does not choose the building. The running game does not buy that building. Version stays 1.6.1.2.

## Story 6.4

Headless. No play check. One warehouse serves each store for each cargo family. A mixed warehouse is flagged. An under-supplied store is flagged. One reassignment moves the later short store onto the pure warehouse with the larger stock of that family. A tie keeps the later warehouse. A recurring stop beats a dated stop. The close-score line does not choose the store. The running game does not call the recorded methods. Version stays 1.6.1.2.

## Story 6.3

Headless. No play check. Every store gets one explain line from profit after rent, the red-day streak, and the trend. The layout clone does not choose the store. The running game does not call the recorded methods. Version stays 1.6.1.2.

## Story 6.2

Headless. No play check. The note is `doc/requirements/game-api.md`. Close is `Helpers.BusinessHelper.ShutdownBusiness`. Buy is `BizManPresentation.SendBuyBuildingOffer`. Price is `Buildings.Office.Headquarters.PricingManagerPlan.SetPrice`. Delivery is `UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination`.

## Story 5.6 human check

1. Restart Big Ambitions so this build loads, then load the empire save. You are in the city, not the main menu. This story does not buy a building and does not write a new log line during play.
2. Do not press F8. Do not advance the day.
3. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
4. **PASS if** the newest `mod.loaded` line is `mod.loaded version=1.5.6 build=3682`. **FAIL if** that line is missing, the version is `1.5.5`, or `build=` is not `3682`.
5. Evidence: that `mod.loaded` line. Headless tests lock one buy after a close: the building under HQ, then under the food warehouse, then on the closed street. A tie keeps the later building. An open store keeps no buy. You will not see a buy line in this log from play.
6. Version marker: public `1.5.6`. The installed dll informational version is `1.5.6+N`.

Human PASS 2026-10-30: `mod.loaded version=1.5.6 build=3682`. The prior line is `mod.loaded version=1.5.5 build=3682`.

## Story 5.5 human check

1. Restart Big Ambitions so this build loads, then load the empire save. You are in the city, not the main menu. This story does not close a store and does not write a new log line during play.
2. Do not press F8. Do not advance the day.
3. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
4. **PASS if** the newest `mod.loaded` line is `mod.loaded version=1.5.5 build=3682`. **FAIL if** that line is missing, the version is `1.5.4`, or `build=` is not `3682`.
5. Evidence: that `mod.loaded` line. Headless tests lock one close after N red days on the layout street and family. A red day is profit below zero. The longer current streak wins, a tie keeps the later store, a green day breaks the streak, confirm is required, and then the toggle. You will not see a close line in this log from play.
6. Version marker: public `1.5.5`. The installed dll informational version is `1.5.5+N`.

Human PASS 2026-10-30: `mod.loaded version=1.5.5 build=3682`. The prior line is `mod.loaded version=1.5.4 build=3682`.

## Story 5.4 human check

1. Restart Big Ambitions so this build loads, then load the empire save. You are in the city, not the main menu. This story does not copy a store layout and does not write a new log line during play.
2. Do not press F8. Do not advance the day.
3. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
4. **PASS if** the newest `mod.loaded` line is `mod.loaded version=1.5.4 build=3682`. **FAIL if** that line is missing, the version is `1.5.3`, or `build=` is not `3682`.
5. Evidence: that `mod.loaded` line. Headless tests lock one qualifying street cluster to one cloned blueprint. The family is the staffing role. A larger interior score wins, a tie keeps the later layout, score 0 is not a layout, and a later cluster without a new store keeps the earlier clone. You will not see a layout line in this log from play.
6. Version marker: public `1.5.4`. The installed dll informational version is `1.5.4+N`.

Human PASS 2026-10-30: `mod.loaded version=1.5.4 build=3682`. The prior line is `mod.loaded version=1.5.3 build=3682`.

## Story 5.3 human check

1. Restart Big Ambitions so this build loads, then load the empire save. You are in the city, not the main menu. This story does not assign staff and does not write a new log line during play.
2. Do not press F8. Do not advance the day.
3. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
4. **PASS if** the newest `mod.loaded` line is `mod.loaded version=1.5.3 build=3682`. **FAIL if** that line is missing, the version is `1.5.2`, or `build=` is not `3682`.
5. Evidence: that `mod.loaded` line. Headless tests lock one role to one HR or headhunter plan. The role is the warehouse family from the delivery plan. A larger amount wins, a tie keeps the later plan, amount 0 is not a plan, and an unlimited amount beats a finite count. You will not see a staffing line in this log from play.
6. Version marker: public `1.5.3`. The installed dll informational version is `1.5.3+N`.

Human PASS 2026-10-30: `mod.loaded version=1.5.3 build=3682`. The prior line is `mod.loaded version=1.5.2 build=3682`.

## Story 5.2 human check

1. Restart Big Ambitions so this build loads, then load the empire save. You are in the city, not the main menu. This story does not write a delivery plan and does not write a new log line during play.
2. Do not press F8. Do not advance the day.
3. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
4. **PASS if** the newest `mod.loaded` line is `mod.loaded version=1.5.2 build=3682`. **FAIL if** that line is missing, the version is `1.5.1`, or `build=` is not `3682`.
5. Evidence: that `mod.loaded` line. Headless tests lock one warehouse to one family and one recurring delivery. A larger cargo family wins, a tie keeps the later family, and a recurring stop beats a priced one-day contract. You will not see a plan line in this log from play.
6. Version marker: public `1.5.2`. The installed dll informational version is `1.5.2+N`.

Human PASS 2026-10-30: `mod.loaded version=1.5.2 build=3682`. The prior line is `mod.loaded version=1.5.1 build=3682`.

## Story 5.1 human check

1. Restart Big Ambitions so this build loads, then load the empire save. You are in the city, not the main menu. This story does not change a shelf price and does not write a new log line during play.
2. Do not press F8. Do not advance the day.
3. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
4. **PASS if** the newest `mod.loaded` line is `mod.loaded version=1.5.1 build=3682`. **FAIL if** that line is missing, the version is `1.4.3`, or `build=` is not `3682`.
5. Evidence: that `mod.loaded` line. Headless tests lock a soda can sold at 2.75, the larger amount when that item sold at two prices, and the route item when another item sold more.
6. Version marker: public `1.5.1`. The installed dll informational version is `1.5.1+N`.

Human PASS 2026-10-29: `mod.loaded version=1.5.1 build=3682`. The prior line is `mod.loaded version=1.4.3 build=3682`.

## Story 4.3 human check

1. Restart Big Ambitions so this build loads, then load the empire save. You are in the city, not the main menu. This story does not draw the overlay and does not write `advice.shown` during play.
2. Do not press F8. Do not advance the day.
3. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
4. **PASS if** the newest `mod.loaded` line is `mod.loaded version=1.4.3 build=3682`. **FAIL if** that line is missing, the version is `1.4.2`, or `build=` is not `3682`.
5. Evidence: that `mod.loaded` line. Headless tests lock one explain line per recommendation and the `advice.shown` text. You will not see `advice.shown` in this log from play.
6. Version marker: public `1.4.3`. The installed dll informational version is `1.4.3+N`.

Human PASS 2026-10-29: `mod.loaded version=1.4.3 build=3682`. The prior line is `mod.loaded version=1.4.2 build=3682`.

## Story 3.5 human check

1. Restart Big Ambitions so this build loads, then load the empire save. You are in the city, not the main menu. This story dumps when the in-game day changes. Do not press F8.
2. Note the in-game day. Advance exactly one day (sleep until the day number changes).
3. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
4. **PASS if** the log has one new line `day.rolled version=1.3.5 day=<the new day>` and that day appears once. **FAIL if** the line is missing, `day=` is the old day, or the same day is logged twice.
5. Evidence: that `day.rolled` line. The same rollover writes `csv.written` and `routes.written` just above it.
6. Version marker: public `1.3.5`. The dll informational version is `1.3.5+N`.

Human PASS 2026-10-29: `day.rolled version=1.3.5 day=1015` once. Statements `rows=102` `day=1014` profit `24763.5137`. Routes stayed `routes=4` `warehouses=147`. Prior statements were `rows=104` `day=1013` profit `50687.4805`. `DayRolled_defers_capture_until_the_day_changes` locks that the save is walked only after the day changes.

## Story 3.4 human check

1. Load the empire save in Big Ambitions. You are in the city, not the main menu. This story dumps delivery plans and warehouse holds on the same F8 as yesterday’s statements. It does not roll the day.
2. Press **F8** once.
3. Open `%LocalAppData%\ChaoticCEO\routes.csv`, `warehouses.csv`, and `ceo.log`. Route columns are warehouse, destination, item, day, truck. Hold columns are warehouse, street, number, item, amount.
4. **PASS if** `ceo.log` has `routes.written version=1.3.4` after the `csv.written` line from that same press, `routes.csv` contains `ba:street_firststreet 37,ba:street_fifthavenue 36,ba:itemname_cheapgift,1016,`, and `warehouses.csv` contains W1 at `ba:street_fourthstreet` number 46 holding `ba:itemname_sodacan` amount `45`. **FAIL if** either csv is missing, the log has no `routes.written` line, the cheap-gift row is missing, or the sodacan amount is not `45`.
5. The `routes=` and `warehouses=` on that log line match the data-row counts in the two csv files.
6. Version marker: public `1.3.4`. The dll informational version is `1.3.4+N`.

Human PASS 2026-10-29: `routes=4`, `warehouses=147`, cheap-gift day `1016`, W1 sodacan amount `45`. Statements stayed `rows=104` day `1013` profit `50687.4805`. The ledger warehouse file has 204 rows. The 57 extra rows are placed fixtures. `WarehouseHold_skips_a_placed_shelf` locks that decision.

## Story 3.3 human check

1. Load the empire save in Big Ambitions. You are in the city, not the main menu. This story dumps yesterday’s business statements. It does not dump routes or roll the day.
2. Press **F8** once.
3. Open `%LocalAppData%\ChaoticCEO\statements.csv` and `%LocalAppData%\ChaoticCEO\ceo.log`. The csv columns are day, street, number, sales, goods, wages, rent, marketing, theft, profit. Every data row is the day before the in-game day.
4. **PASS if** `ceo.log` contains `csv.written version=1.3.3` and the `day=` on that line is yesterday, and `statements.csv` has that same day and the same row count. **FAIL if** the file is missing, the log has no `csv.written` line, or a row is for today.
5. When the in-game day is 1014, the first data row is `1013,ba:street_fourthavenue,28,65477,3897.2825,9793.2383,1099,0,0,50687.4805` and the log line includes `rows=` matching the csv, `day=1013`, `street=ba:street_fourthavenue`, `number=28`, `profit=50687.4805`.
6. Version marker: public `1.3.3`. The dll informational version is `1.3.3+N`.

## Story 3.2 human check

1. Open `%USERPROFILE%\AppData\LocalLow\Hovgaard Games\Big Ambitions\ModsLocal\ChaoticCEO`. You are looking at the installed mod folder. Do not start the game. This story does not dump statements.
2. The folder contains `ChaoticCEO.dll`.
3. Confirm that file is the one just built.
4. **PASS if** `ChaoticCEO.dll` is in that folder. **FAIL if** the folder or the dll is missing.
5. The dll informational version is `1.3.2+N`. Headless tests lock `mod.loaded version=1.3.2 build=3682`. The game writes that line to `%LocalAppData%\ChaoticCEO\ceo.log` when it loads the mod.
6. Version marker: public `1.3.2`.

## Story 2.2 human check

1. Open `doc/gemini/cache/ledger/REPORT.md` after `python script/statement_ledger.py`. You are on the Bottom 10 table. BizMan is how you check which businesses those rows are. The profit column is the average of daily profit over the days that business appears (61 days for both rows below). A single BizMan day, or a lifetime total, will not match the dollar figure.
2. The first row is Chaotic Hair #5, service, `ba:street_fifthavenue` 38, profit `14484.6098`. The last row is Chaotic Kabobs #5, food, `ba:street_firstavenue` 5, profit `898.6715`.
3. Top offices is Chaotic Devs at `26583.2479`. Fifth Avenue has 4 service businesses. Fourth Avenue has 3 food businesses. W6 at Fifth Avenue 43 lists `food, service`. W1 is not in the mixed-warehouse table.
4. **PASS if** the Bottom 10 table starts with Chaotic Hair #5 at `14484.6098` and ends with Chaotic Kabobs #5 at `898.6715`. **FAIL if** Kabobs is not the last row, or that profit is `8551.19` (latest day) or `54818.96` (lifetime sum).
5. `doc/gemini/cache/ledger.log` contains `report.written` with `version=1.2.2`, `bottom=Chaotic Kabobs #5`, `profit=898.6715`, `offices=1`, `streets=2`, and `mixes=7`.
6. Version marker: `1.2.2` printed by the script and on that log line.

## Story 2.1 human check

1. Open `doc/gemini/cache/ledger/routes.csv` and `warehouses.csv` after `python script/statement_ledger.py`.
2. `routes.csv` columns: warehouse, destination, item, day, truck. Four rows. The warehouse is `ba:street_firststreet 37`. The destination is `ba:street_fifthavenue 36`.
3. Find the cheap-gift row and the W1 row in `warehouses.csv`.
4. **PASS if** the cheap-gift route is day `1016` with an empty truck, and W1 at `ba:street_fourthstreet` number 46 holds `ba:itemname_sodacan` amount `45`. **FAIL if** the route count is not 4, the destination is a food-delivery street, or the sodacan amount is `95`.
5. `doc/gemini/cache/ledger.log` contains `routes.written` with `version=1.2.1`, `routes=4`, and `portfolio=17`.
6. Version marker: `1.2.1` printed by the script and on that log line.

## Story 1.2 human check

1. Open `doc/gemini/cache/ledger/portfolio.csv` after `python script/statement_ledger.py`.
2. One row per joined business. Columns: business, family, street, number, tco.
3. Find `Chaotic Clothing #3` and `Chaotic Hair #8`.
4. **PASS if** Clothing is family `durables` at `ba:street_firstavenue` number 38, and Hair #8 is family `service` at `ba:street_fifthavenue` number 37 with tco `4003.3822`. **FAIL if** either row is missing, the family is wrong, or Hair #8 tco is `1050.0675` (that would be an average over 61 days).
5. `doc/gemini/cache/ledger.log` contains `portfolio.written` with `version=1.1.2` and `business=Chaotic Clothing #3`.
6. Version marker: `1.1.2` printed by the script and on that log line.

## Story 14.2 — Delivery cover

Tier 1: `DeliveryCoverTests`. Chain: `open days → burn → stand floor → line → Maintain → playbook`. Burn is the last two completed open days. A closed day is skipped. No history uses seed 25. Paper bags with no sales use seed 50 on the first drop. A zero sale of that item stays at 5. A gap of 1 is sized as 2 days and the date stays. A gap of 2 or 3 stays when units on hand still cover burn × gap × 1.5. A thinner shelf pulls that truck to tomorrow and sizes it for 2 days. A truck due today stays today and is sized for 2 days. An open day below 5 units is left out when another open day sold at least 5. Target stock is 2 × the daily average, plus a stand floor of 200 on a retail line (2 stands × 100). The window does not multiply the sales piece. A paper bag uses one cash register. The register holds 1000, and that capacity is the bag floor, plus 2 × the daily average. Paper never clips to register free space. The truck-pull check stays burn × gap × 1.5. The line is that target minus units on hand, at least 10. Fixture space does not cut it. `contract.cover` records the open days, the average, the gap, on-hand, the target, and the line. Command: the Tier 1 `dotnet test` suite above.

The build checked is `1.17.1+403`. `1.17.1+399` still adds 450 and multiplies the sales piece by the window.

Human check. This slice is the delivery amount on the store that already has a contract. It does not furnish a second building. Every retail item is treated as 2 stands of 100. A paper bag uses one cash register of 1000.

1. Restart Big Ambitions so that build loads. Continue the current shop or start a **new** game. Autopilot on. Let one morning pass.
2. Open `%LocalAppData%\ChaoticCEO\ceo.log` and find that morning's `contract.cover` lines.
3. **PASS if** `mod.loaded` is `version=1.17.1`, `multiplier=2.0`, a retail `target` is 200 plus 2 × `average`, and the paper-bag `target` is 1000 plus 2 × its `average`. **FAIL if** a retail `target` is still about 500, or a paper-bag `target` is only 2 × its `average` or still uses floor 200.
4. Evidence: `contract.cover`. Umbrella shape: average 25, target 250. Above that target the line drops to 10.
5. Version marker: public `1.17.1`. The installed dll informational version is `1.17.1+403`.

Human PASS 2026-11-10 on 1.17.1 at 22 7th Avenue. Mornings 3–29 stayed above half a fixture. Day 2 bag `target=300` with `multiplier=2.0`.

## Story 14.3 — Goods list, shelf ceiling removed

Tier 1: `ShelfGoodsTests`. Chain: `open type → catalog → line` and `open type → catalog → line → short gap`. A florist or fast-food contract uses that type's list. Hair and office stay on the gift starter. A short wholesaler line is a supply gap. Fixture space does not reduce `need`. An unread fixture adds nothing to capacity.

Human check. This slice is the order on the gift shop at 22 7th Avenue. It does not open a florist.

1. Restart Big Ambitions so 1.17.2.2 loads. Continue the same save. Let one morning pass.
2. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
3. **PASS if** `mod.loaded version=1.17.2.2` and a `contract.cover` line with on-hand below the target has `need` equal to `target` minus `onHand`. **FAIL if** `need` is only the room left on the shelf.
4. Evidence: `contract.cover`. Day 56 cheap gifts were on-hand 184, target 344, and the clipped build ordered 10. This build orders 160.
5. Version marker: public `1.17.2.2`.

Human PASS 2026-11-11 on 1.17.2.2 at 22 7th Avenue. Days 106–111: `mod.loaded version=1.17.2.2`, and every cover with on-hand below the target had `need` equal to `target` minus `onHand`. Morning fills stayed at or above half the last shelf reading. A mid-day yellow on one fixture is not in the log.

## Story 14.15 — Fixture space, then one storage shelf

Tier 1: `Cover_never_clips_the_line_to_free_space` (HARD LOCK). Chain: `on hand → free space → shelf` — Cover.Need stays the full line; free space only gates the storage-shelf order. `cap` on `contract.cover` is always 0. A full selling fixture (`FixtureFull`) orders one storage shelf (`ba:itemname_storageshelf`, 16 boxes, $1,200) from Hampton Supplies at 13 7th Avenue. A second order waits while that delivery is still open. Cash under $1,200 orders nothing.

Human check. The gift shop at 22 7th Avenue, one morning. The shelf arrives as a furniture delivery. Place it in the store when it shows up. This does not hire the cleaner again.

1. Restart Big Ambitions so this build loads. Continue the same save at 22 7th Avenue. Let one morning pass.
2. Open `%LocalAppData%\ChaoticCEO\ceo.log`. Find `mod.loaded` and `shelf.ordered`.
3. **PASS if** `mod.loaded version=1.17.5` and one `shelf.ordered` line names `item=ba:itemname_storageshelf`, `amount=1`, `price=1200`, `number=22`, and `fromNumber=13`. **FAIL if** the version is still 1.17.4, or that morning has no `shelf.ordered` line.
4. Evidence: `shelf.ordered`. Day 1520 covers were `need=0` `cap=0` for every gift item. The day-1495 wholesaler notes are the boxes that did not fit.
5. Version marker: public `1.17.5`.

Human PASS 2026-11-12 on 1.17.5 at 22 7th Avenue. `mod.loaded version=1.17.5 build=3682`. `shelf.ordered` day 1519 and day 1521, `amount=1` `price=1200` `fromNumber=13`. Day 1520 did not order while that delivery was open. Day 1521 paper `onHand=192` `need=8` `cap=8`. Gifts stayed `need=0` `cap=0`. `ready=true`, 73 customers, revenue 2230, profit 1546.88. The shelf was bought and not placed.

## Story 14.16 — A later morning leaves a staffed cleaner

Tier 1: `Cleaner_a_later_morning_leaves_a_staffed_station` and `Cleaner_a_later_morning_hires_an_empty_station`. Chain: `day roll → cleaner on station → no hire` and `day roll → empty station → hire`. A cleaning shift, or a cleaner already on the station, logs nothing on the next morning. An empty station still logs `cleaner.replaced` `reason=absent`. Days 1520 and 1521 are both locked. This does not place the storage shelf and does not add the 85% rack.

Human check. The gift shop at 22 7th Avenue, the next morning, with the cleaner still on the station.

1. Restart Big Ambitions so this build loads. Continue the same save at 22 7th Avenue. Let one morning pass.
2. Open `%LocalAppData%\ChaoticCEO\ceo.log`. Find `mod.loaded` and the newest `cleaner.replaced` line.
3. **PASS if** `mod.loaded version=1.17.6` and that new morning has no `cleaner.replaced` line. **FAIL if** the version is still 1.17.5, or that morning appends `cleaner.replaced` `reason=absent`.
4. Evidence: no new `cleaner.replaced` for the morning under test. Older `cleaner.replaced` lines from days 1514–1521 stay in the file.
5. Version marker: public `1.17.6`.

Human PASS 2026-11-12 on 1.17.6 at 22 7th Avenue. `mod.loaded version=1.17.6 build=3682`. Days 1520–1528: no `cleaner.replaced`, and `g3=true`. Day 1528 ready, 74 customers, revenue 2303, profit 1600.11. Day 1520 still logged `cashier.replaced` `reason=absent`. That hire is outside this story.

## Story 14.9 — Place the cargo shelf in the storage room, touching the row

Tier 1: `Shelf_an_unplaced_shelf_does_not_order_another`, `Shelf_a_full_storage_room_orders_nothing`, the `Seat_*` cases, and the `Attend_*` cases in `StorageShelfTests`. Chain: `day roll → full fixture → shelf in cargo → no order`, `day roll → full fixture → storage room full → hold, no order`, and `day roll → full fixture → order → shelf in cargo → storage seat → placed → next morning seat`. The seat comes from the interior's own placement grid (`GridGenerator` ground cells, wall cells occupied) plus the footprint of every placed item. A seat touches the end of the storage-shelf row, or fills a gap in it, with the same facing. It never crosses a wall, never stands on another item, and never opens a second rank. A shelf in cargo is placed and orders nothing. No seat holds the shelf in cargo and logs `shelf.held`. A full fixture orders only while a seat is open. Nothing is sold. This does not add the 85% rack or the capacity-miss log.

Human check. The gift shop at 22 7th Avenue, the next morning. The Storage Shelf now in cargo moves into the storage room, flush against the end of the existing storage shelves.

1. Restart Big Ambitions so this build loads. Continue the same save. Stand outside the shop (at home is fine) and let one morning pass.
2. Walk into the shop. Open the delivery-spot cargo. Then walk into the storage room and the bathroom.
3. Open `%LocalAppData%\ChaoticCEO\ceo.log` and find `mod.loaded` and the newest `shelf.` line.
4. **PASS if** `mod.loaded version=1.17.8.1`, that morning has `shelf.placed` for `number=22`, cargo has no Storage Shelf, and the new shelf stands in the storage room touching the existing storage shelves with no gap, facing the same way, with nothing in the bathroom or outside. **FAIL if** the version is still 1.17.8, the cargo still shows a Storage Shelf, a shelf stands anywhere but flush against the storage row, or that morning logs `shelf.ordered`.
5. Evidence: `shelf.placed ... x= z= yaw=`. `shelf.held reason=nogrid`, `offgrid`, or `full` names why a shelf stayed in cargo.
6. Version marker: public `1.17.8.1`.

Human PASS 2026-11-12 on 1.17.8.1 at 22 7th Avenue. `mod.loaded version=1.17.8.1 build=3682`. Day 1571 `shelf.held reason=nogrid` (outside). Day 1572 `shelf.placed x=997.50 z=54.50 yaw=90`. Day 1573 `shelf.ordered` after cargo was empty. Day 1575 `shelf.placed x=998.00 z=54.50 yaw=90`. Save shows storage shelves at (997, 54.5) and (997.5, 54.5). No sell. The 85% rack and the capacity-miss log stay open on **14.9**.

**Long run:** A batch or soak is not Tier 1. When it trips a gate, extract that decision and add a named Tier 1 test before the next long run. See `.cursor/rules/testing.mdc`.

## Bugfix — paper register floor 1000 (1.17.8.3)

Tier 1: `Cover_day_1575_register_1000_keeps_the_display_above_half`, `Cover_day_1571_paper_keeps_the_full_line_past_register_free_space`. Chain: `open days → burn → register floor → full line → playbook`. RegisterCap is 1000. Paper and goods need is target − onHand with cap=0. Free space never cuts Cover.

Human PASS 2026-11-12 on 1.17.8.3 at 22 7th Avenue. Day 1575 `target=1166` `need=1050`. Day 1576 `onHand=1166` `target=1166` `need=10`. Days 1577–1579 stayed above 1000 on hand.

## Story 14.9 — 85% rack and capacity-miss log

Tier 1: `Rack_orders_when_spend_room_and_target_reach_eighty_five_percent_of_fixture_space`, `Rack_leaves_the_store_alone_when_spend_room_target_or_floor_block_it`, `Capacity_a_failed_call_logs_one_miss_and_does_not_spam`, `Capacity_miss_skips_an_unmatched_pick`. Chain: `target vs cap → rack`, `spend room → target vs cap → rack`, and `layout → capacity match → miss`. Spend room at least **RackSpendRoom** = $2,500 and target at least **RackTriggerRatio** = 85% of fixture space orders one `ba:itemname_storagerack` from Hampton. A failed capacity call logs one `capacity.missed` line and marks the watch so it does not retry every frame. This does not add a second register.

Human check. The gift shop at 22 7th Avenue, the next morning, with spend room at least $2,500 and a goods target at or above 85% of that item’s fixture space.

1. Restart Big Ambitions so this build loads. Continue the same save at 22 7th Avenue. Let one morning pass.
2. Open `%LocalAppData%\ChaoticCEO\ceo.log`. Find `mod.loaded` and any new `rack.ordered` or `capacity.missed` line.
3. **PASS if** `mod.loaded version=1.17.9`, and either that morning has `rack.ordered` for `item=ba:itemname_storagerack` `number=22`, or no goods target is at 85% yet and the morning still has no frame-spam of `capacity.missed`. **FAIL if** the version is still 1.17.8.3, or `capacity.missed` repeats every frame for the same day.
4. Evidence: at most one `capacity.missed` per day/street/number/call. `rack.ordered ... item=ba:itemname_storagerack` when the 85% trigger fired.
5. Version marker: public `1.17.9`.

Human PASS 2026-11-12 on 1.17.9 at 22 7th Avenue. `mod.loaded version=1.17.9 build=3682`. Day 1579 `rack.ordered` `item=ba:itemname_storagerack` `amount=1` `price=1200` `number=22` `fromNumber=13`. No `capacity.missed` frame spam.

## Bugfix — Cover never clips to free space (1.17.9.1)

Tier 1 HARD LOCK: `Cover_never_clips_the_line_to_free_space`, `Cover_day_1579_soda_keeps_the_full_line_past_storage_free_space`, `Cover_day_1571_paper_keeps_the_full_line_past_register_free_space`. Chain: `storage on hand → empty fridge capacity → full cover line`. Cover.Need is always target − onHand (min 10). `fixtureCap` must not change Need.

## Bugfix — shelf buy uses leftover + inbound (1.17.9.2)

Tier 1: `SpacesNeeded_is_leftover_boxes_plus_boxes_on_the_truck`, `ShelvesToOrder_buys_only_the_shortfall_and_counts_boxed_shelves_first`, `Chain_leftover_plus_inbound_orders_shelves_only_when_placed_capacity_is_short`. Spaces needed = on-hand + inbound. Buy only when that exceeds display capacity + placed shelves × 16. Boxed shelves count as capacity and are placed before any buy.

Human check. Gift shop at 22 7th Avenue with plenty of placed storage shelves and (if any) boxed shelves in cargo.

1. Restart Big Ambitions so this build loads. Continue the same save. Let one morning pass.
2. Open `%LocalAppData%\ChaoticCEO\ceo.log`. Find `mod.loaded` and any `shelf.ordered` / `shelf.placed` for that morning.
3. **PASS if** `mod.loaded version=1.17.9.2`, and with placed storage covering leftover + inbound there is no new `shelf.ordered` (boxed shelves may `shelf.placed` first). **FAIL if** version is still 1.17.9.1, or the morning buys shelves while placed capacity already covers the stock.
4. Evidence: no spurious `shelf.ordered`; optional `shelf.placed` when cargo had a boxed shelf.
5. Version marker: public `1.17.9.2`.

## Bug fix — shelf unit capacity + nested cargo + one-at-a-time (1.17.9.3)

Tier 1: `ShelvesToOrder_does_not_treat_sixteen_box_slots_as_sixteen_retail_units`, `Hold_storage_shelf_uses_unit_capacity_not_box_slot_count`, `BoxedInHolder_counts_nested_cargo_and_skips_placed_shelves_as_holders`. Capacity uses `UnitCapacity` (100), not `Boxes` (16). Nested cargo and pending furniture count as inbound shelves. Buy at most one per morning; place or hold before another buy.

## Bug fix — 14.9 storage layout 1-2-2-1 (1.17.9.7)

Folds the buy-math stop, survey log, pair/wall seat, and room fill into one installable (slices .4–.6 folded; never go back from the live 1.17.9.3).

Tier 1: `Attend_holds_with_hold_spike_and_orders_nothing`, `Seat_after_a_wall_shelf_leaves_a_one_meter_aisle_then_faces_it`, `Seat_after_an_aisle_pair_adds_the_back_to_back_partner`, `Seat_a_full_pair_never_gets_a_third_shelf_touching_along_depth`, `Seat_extends_along_width_after_the_aisle_row_is_full`, `Survey_line_names_the_footprint_and_boxes`, `Layout_constants_match_the_long_face_aisle_stack`. Chains: `day roll → cover → boxed in cargo → hold-spike → no order`, `day roll → short capacity → order → place → next seat`, `day roll → full storage → hold, no order`.

Human check. Gift shop at 22 7th Avenue.

1. Restart Big Ambitions so this build loads. Continue the same save. Confirm `mod.loaded version=1.17.9.7`.
2. First city frame: open `%LocalAppData%\ChaoticCEO\ceo.log` and find `shelf.room` / `shelf.surveyed` (one per standing storage shelf).
3. Sell empty standing shelves that are packed front-to-back if needed, leave cargo shelves, and fast-forward several mornings.
4. **PASS if** cargo shelves `shelf.placed` into wall → aisle → pair (yaws 90/270, ~2.0 m across aisles, ~1.0 m back-to-back), restocker loads boxes onto both faces of a pair, and after the room is full mornings log `shelf.held reason=full` with **no** new `shelf.ordered`. **FAIL if** same-yaw 0.5 m packs return, only one face of a pair gets boxes, or buys continue into cargo after `full`.
5. Evidence: `shelf.surveyed`, `shelf.placed … yaw=`, `shelf.held reason=full`. Version marker: public `1.17.9.7`.

**Human FAIL 2026-11-13** on 1.17.9.7 at 22 7th Avenue (UCPH park, not CMPH). `mod.loaded version=1.17.9.7`. Day 1588 `shelf.surveyed` lines show `width=2.97 depth=0.50` (half-depth still live — Size fix keyed on small width and missed). All surveyed shelves `yaw=90`. Days 1588–1593: `shelf.held` (`nogrid` once, then `full`); **no** `shelf.placed`; **no** `shelf.ordered` (buy-stop worked). Next: fix Size when depth is about 0.5 (or always force FootprintWidth x FootprintDepth for storage shelves), then re-smoke place.

## 14.9 storage keep = need+1, sell excess, reseat (1.17.9.9)

Finding: live Size `width=2.97 depth=0.50` is the full collider. Buy math uses storage boxes = ceil((units after truck − display cap) / boxSize), 16 slots per shelf. Keep target is `need + 1` when the library row still has a spare seat. Excess cargo and empty placed shelves are sold; blocked shelves pack to cargo and reseat into the 1–aisle–2 stack. Staff hold cheat is parked on `feature/staff-hold-cheat`, not this ship.

Tier 1: box-math tests plus `KeepTarget_*`, `Excess_counts_cargo_and_placed_above_keep`, `Order_uses_keep_not_bare_need`, `Sell_prefers_cargo_before_placed`, `Stocked_placed_is_held_not_sold`, `Keep_line_names_*`. Chains: `day roll → cover → box math → keep → sell excess → reseat → attend` (no order when have≥keep; order one toward keep when short).

Human check. Gift shop at 22 7th Avenue (same save; no new game).

1. Restart Big Ambitions so **1.17.9.9** loads. Continue the save. Confirm `mod.loaded version=1.17.9.9`.
2. Open cargo if shelves are piled (e.g. 32). Let one morning roll (F6).
3. Open `%LocalAppData%\ChaoticCEO\ceo.log`.
4. **PASS if** `shelf.keep` shows `need` and `keep=need` or `need+1`; `have` falls to `keep`; `shelf.sold` accounts for excess cargo/placed; remaining placed shelves are aisle-reachable; no new `shelf.ordered` while `have >= keep`. **FAIL if** cargo stays at 32, `keep` is ignored, a fifth+ shelf stays when `need<=1`, or `shelf.need verdict=unknown` / `boxSize=0`.
5. Evidence: `shelf.keep`, `shelf.sold`, `shelf.need`, optional `shelf.packed` / `shelf.held reason=stocked`. Version marker: public `1.17.9.9`.

Not in this slice: staff hold; second register; changing live collider constants.

**Human FAIL 2026-11-13** on 1.17.9.9 at 22 7th Avenue (UCPH park). `mod.loaded version=1.17.9.9`. Day 1588: `need=1 keep=2` correct; `shelf.sold` cargo then `shelf.packed` excess ×2 then sold packed cargo + one placed → `haveAfter=2` on paper, but day 1589+ stays `placed=1` (never reseated the spare). Cover spam during F6 logged dozens of `shelf.keep` with cargo ticking down before the batch sell. New-game did place `keep=2` but aisle not walkable (seat still uses 2×1 constants vs live ~3×0.5). Log shows `cashier.replaced reason=absent` (not cleaner quit). Next: do not sell freshly packed shelves before Place reaches `keep`; lock aisle with live footprint; optional staff-hold branch for resign.

## 14.9 pack-before-sell + live-footprint aisle (1.17.9.10)

Tier 1: `Attend_does_not_sell_packed_cargo_before_place_reaches_keep`, `Attend_sells_excess_cargo_after_place_reaches_keep`, `Seat_accept_uses_live_half_depth_for_aisle_partner`, `ResolveFootprint_keeps_live_storage_shelf_half_depth`. Chains: `keep → pack → place → sell`, `keep → pack (under keep) → place → sell`, `cover → boxed → live-depth aisle seat → place`.

Human check. Gift shop at 22 7th Avenue (same save; optional new-game for aisle).

1. Restart Big Ambitions so **1.17.9.10** loads. Continue the save. Confirm `mod.loaded version=1.17.9.10`.
2. Let one morning roll (F6). Open `%LocalAppData%\ChaoticCEO\ceo.log`.
3. Check `shelf.keep` / `shelf.packed` / `shelf.placed` / `shelf.sold` order for that day, and that `placed` on later mornings stays at `keep`.
4. **PASS if** sell runs only after place reaches `keep` (`placed` stays = `keep`, not 1 while `keep=2`); `shelf.surveyed` keeps ~`width=2.97 depth=0.50`; aisle pair is walkable (restocker stocks both faces). **FAIL if** packed cargo is sold while `placed < keep`, or the 1+1 aisle blocks walking.
5. Evidence: `shelf.keep`, `shelf.packed`, `shelf.placed`, `shelf.sold`, `shelf.surveyed`. Version marker: public `1.17.9.10`.

Not in this slice: staff hold; second register; CMPH of the prior FAIL.

## 14.9 second pod — third shelf against the spare (1.17.11.5)

Tier 1: `Seat_live_pair_puts_the_third_shelf_against_the_spare`, `Seat_a_west_facing_stocked_shelf_keeps_the_third_against_the_spare`. Chain: `cover → place live 1+1 → keep 3 → third against the spare`.

Human check. Gift shop at 22 7th Avenue, the save from the 1.17.11.4 pass. The stocked shelf is near x=996.75 yaw 90. The spare faces it near x=998.00 yaw 270. This morning places a third shelf only when `keep` is 3. A morning that stays at `keep=2` is not a fail of this slice.

1. Restart Big Ambitions so this build loads. Continue the same save. Confirm `mod.loaded version=1.17.11.5`.
2. Let mornings finish until `shelf.keep` shows `keep=3` (need has risen to 2 and a shelf is in cargo or arrives). Do not cancel the time skip.
3. **PASS if** that morning logs `shelf.placed` at about x=999.00 z=55.00 yaw=90, backs touching the spare (about 1.00 m between centers, not 1.25). **FAIL if** the new shelf is behind the stocked one (about x=995.75 yaw=270) or an aisle gap is left on the spare's back.
4. Evidence: `shelf.keep`, `shelf.placed`. Version marker: public `1.17.11.5`.

Not in this slice: the second register, overhire, displays.

## 14.9 facing gap zero uses the surveyed depth (1.17.11.10)

`facingGap` 0 places the long faces together. Centers are one surveyed depth apart (0.50 m on the live shelf, the spare near x=997.25). A number above 0 is still that many shelf bodies, so 1.25 stays the previous layout. The shelf at the width end of the run is not moved. Back-to-back stays one shelf body.

Tier 1: `Day_1606_facing_gap_zero_puts_the_long_faces_together`, `Facing_gap_zero_leaves_the_width_end_one_shelf_apart`. Chain: `cover → shelf gap file → reseat the 1 m spare → facing seat on the surveyed depth`.

Human check. Gift shop at 22 7th Avenue. The stocked shelf is at x=996.75 yaw 90. The spare from 1.17.11.9 is at x=997.75 yaw 270. `shelf-gap.json` stays at `facingGap` 0. This check is the gap between those two long faces. It does not move the shelf at the end of the run.

1. Restart Big Ambitions so this build loads. Continue the same save. Confirm `mod.loaded version=1.17.11.10`.
2. Open `%LocalAppData%\ChaoticCEO\shelf-gap.json` and leave `facingGap` at 0.
3. Let one morning finish. Do not cancel the time skip. Look at the storage row.
4. **PASS if** `shelf.gap` shows `facingGap=0 pitch=0` and the empty spare is seated at about x=997.25 z=55 yaw 270, long faces touching. **FAIL if** the spare stays at about x=997.75, or a shelf at the end of the run is pulled into that gap.
5. Evidence: `shelf.gap`, `shelf.packed`, `shelf.placed`. Version marker: public `1.17.11.10`.

Not in this slice: the back-to-back third shelf. That seat stays one shelf body behind the spare.

Human PASS 2026-11-14 at 22 7th Avenue on 1.17.11.10. The save opened with two shelves already touching. After one was sold, the replacement was bought and seated at the zero gap. Editing `facingGap` to 1 while the game kept running opened the next seat to one shelf body.

## 14.9 need+1 and shipping shelves (1.17.11.9)

Keep is need + 1 when the room has a spare seat. A shelf already in the shipping area counts as owned, is seated before another is bought, and the extras are sold once the floor is at keep. A furniture contract whose delivery day has arrived is delivered on that morning even when the clock is still before 8:00.

Tier 1: `Order_does_not_buy_when_ninety_shipping_shelves_already_cover_keep`, `ContractDue_day_1605_still_owes_the_shelf_ordered_for_day_1602`, `ShippingToSell_waits_until_the_floor_reaches_keep_then_clears_the_pile`. Chain: `cover → box need → keep is need+1 → shipping holds the spare → no order → sell the pile once the floor is at keep`.

Human check. Gift shop at 22 7th Avenue, the day 1605 save. One storage shelf is on the floor. One shelf order has been pending since day 1601. `shelf-gap.json` can stay at `facingGap` 0.

1. Restart Big Ambitions so this build loads. Continue the same save. Confirm `mod.loaded version=1.17.11.9`.
2. Let one morning finish. Do not cancel the time skip. Then look at the storage row and the shipping area.
3. **PASS if** `shelf.keep` shows `need=1 keep=2` (not 3), the pending shelf arrives (`shelf.arrived`) and is placed so `placed=2`, and there is no new `shelf.ordered` while shipping plus placed plus cargo already covers `keep`. **FAIL if** `keep` is 3, the extra shelf stays pending with `cargo=0` and `placed=1`, or another shelf is ordered while shelves are already sitting in shipping.
4. Evidence: `shelf.keep`, `shelf.arrived`, `shelf.placed`, `shelf.ordered`, `shelf.sold where=shipping`. Version marker: public `1.17.11.9`.

Not in this slice: the third shelf (the second pod). Keep stays at need + 1. The `facingGap` file is read (`pitch=1` when the file says 0) and was not part of this pass.

Human PASS 2026-11-14 at 22 7th Avenue on 1.17.11.9. Day 1606: `need=1 keep=2`, `shelf.arrived amount=1`, `shelf.placed x=997.75 z=55.00 yaw=270`. Days 1607–1608: `placed=2`, no new `shelf.ordered`.

## 14.9 shelf gap file (1.17.11.8)

`%LocalAppData%\ChaoticCEO\shelf-gap.json` sets `facingGap`. `0` means the faces touch. Any number above 0 is the center spacing in shelf bodies. `1.25` is the previous layout. The file is created on the first morning if it is missing. A later edit is read on the next morning. An existing file is not overwritten.

Tier 1: `Shelf_gap_file_uses_zero_as_touching_and_one_point_two_five_as_the_old_spacing`, `Shelf_gap_file_is_created_when_missing_and_keeps_a_later_edit`. Chain: `cover → shelf gap file → facing seat`.

Human check. Gift shop at 22 7th Avenue.

1. Restart Big Ambitions so this build loads. Continue the same save. Confirm `mod.loaded version=1.17.11.8`.
2. Open `%LocalAppData%\ChaoticCEO\shelf-gap.json`. Leave `facingGap` at 0, or set it to 1.25 if you want the previous spacing. Save the file.
3. Let one morning finish. Do not cancel the time skip.
4. **PASS if** `shelf.gap` shows the number you saved (`facingGap=0 pitch=1`, or `facingGap=1.25 pitch=1.25`) and the facing shelves sit that far apart (about 1.00 m when the pitch is 1, about 1.25 m when the pitch is 1.25). **FAIL if** `shelf.gap` does not match the file, or the shelves stay at the other spacing.
5. Evidence: `shelf.gap`, `shelf.placed`. Version marker: public `1.17.11.8`.

Not in this slice: collapsing the aisle and back-to-back code into one path.

## 14.9 facing gap zero (1.17.11.7)

`FacingGap` is 0. Facing centers are one shelf body apart, so the faces touch. The aisle-versus-back-to-back split stays until this look is confirmed. An empty spare that still sits a quarter-body away is packed and seated flush.

Tier 1: `Facing_gap_is_zero_so_the_partner_sits_one_body_away`. Chain: `cover → place live 1+1 → facing gap 0 → third against that partner`.

Human check. Gift shop at 22 7th Avenue. The stocked shelf is near x=996.75 yaw 90. The spare from the last build is near x=998.00 yaw 270, a quarter-body of air away.

1. Restart Big Ambitions so this build loads. Continue the same save. Confirm `mod.loaded version=1.17.11.7`.
2. Let one morning finish. Do not cancel the time skip.
3. **PASS if** the spare is packed and the seated shelf is about x=997.75 z=55 yaw 270 (centers 1.00 m apart, faces touching). If `keep=3` and cargo is on hand, the next shelf is about x=998.75 z=55 yaw 90, also touching. **FAIL if** a facing pair stays about 1.25 m apart (the spare left at x=998).
4. Evidence: `shelf.packed`, `shelf.placed`, `shelf.keep`. Version marker: public `1.17.11.7`.

Not in this slice: collapsing the aisle and back-to-back code into one path. That cleanup waits until this layout is confirmed.

## 14.9 second pod — debug keep 3, no resign, no sick (1.17.11.6)

Temporary. `DebugKeep` lifts a positive keep below 3 up to 3 so the second pod can place. Remove it after that shelf is seen. `StaffHold` sets satisfaction to 100, clears the quit warning, clears absent, and sets the next sick day to never. It runs on the same tick as the needs hold.

Tier 1: `DebugKeep_lifts_a_positive_keep_below_three_and_leaves_the_rest`, `Staff_each_fault_is_due`, `Staff_hold_sets_satisfaction_and_clears_sick_and_quit`. Chains: `cover → place live 1+1 → keep 2 → debug keep 3 → third against the spare`, `needs hold → staff hold`.

Human check. Gift shop at 22 7th Avenue. Two shelves are already placed (stocked near x=996.75 yaw 90, spare near x=998.00 yaw 270). Cargo is still on hand from the mornings that sold nothing because keep stayed 2.

1. Restart Big Ambitions so this build loads. Continue the same save. Confirm `mod.loaded version=1.17.11.6`.
2. Let one morning finish. Do not cancel the time skip.
3. **PASS if** that morning logs `shelf.keep` with `keep=3` and `shelf.placed` near x=999.00 z=55.00 yaw=90, and no employee resigns or calls in sick. **FAIL if** keep stays 2, the new shelf is behind the stocked one (about x=995.75 yaw 270), or someone resigns or calls in sick.
4. Evidence: `shelf.keep`, `shelf.placed`, `staff.held`. Version marker: public `1.17.11.6`.

Not in this slice: the second register, overhire, displays. The debug keep comes out after this pod is confirmed.

## 14.9 closer aisle and even sellback (1.17.11.4)

Tier 1: facing centers are `AisleCenterStep` (1.25 shelf bodies). `Buyback(960)` is the purchase price 1200. Chain: `cover → reseat same-yaw spare across the aisle → third is back-to-back` uses that step.

Human check. Gift shop at 22 7th Avenue. The two storage shelves face each other with a wide gap.

1. Restart Big Ambitions so this build loads. Continue the same save. Confirm `mod.loaded version=1.17.11.4`.
2. Let one morning finish. Do not cancel the time skip.
3. **PASS if** the shelves sit closer (centers about 1.25 m apart; the moved shelf is near x=998.00, not 998.75) and any `shelf.sold` price equals `amount * 1200`. **FAIL if** the gap stays at 2 m, or a sold shelf is still priced at 960 each.
4. Evidence: `shelf.packed`, `shelf.placed`, `shelf.sold`. Version marker: public `1.17.11.4`.

## 14.9 reseat the spare across the aisle (1.17.11.3)

Tier 1: `Seat_one_shelf_in_a_wide_room_faces_the_aisle_before_a_side_neighbor`, `Reseat_packs_the_same_yaw_spare_and_leaves_an_aisle_pair`. Chain: `cover → reseat same-yaw spare across the aisle → third is back-to-back`.

Human check. Gift shop at 22 7th Avenue. Two storage shelves, one against the wall with the boxes, the empty one stacked on the same side.

1. Restart Big Ambitions so this build loads. Continue the same save. Confirm `mod.loaded version=1.17.11.3`.
2. Let one morning finish. Do not cancel the time skip. Then open `%LocalAppData%\ChaoticCEO\ceo.log` and look at the two shelves.
3. **PASS if** the empty shelf is packed (`shelf.packed` `reason=pattern`) and placed facing the shelf that already has boxes, with a walkable gap (`shelf.placed`, opposite facing, about 2 m between centers). Both fronts can be walked. **FAIL if** a shelf is still stacked half a meter in front of the wall shelf on the same facing, or the spare sits beside it along the wall.
4. Evidence: `shelf.packed`, `shelf.placed`, `shelf.keep`. Version marker: public `1.17.11.3`. The third shelf, back-to-back with no gap, is not this morning. Keep stays at 2.

## 14.9 place the purchased shelf (1.17.11.2)

Tier 1: `Sell_does_not_drop_the_floor_below_keep_to_cover_a_pending_shelf`, `Order_does_not_buy_when_unplaced_cargo_already_covers_keep`. Chain: `cover → place cargo to keep → sell leaves the floor shelf`.

Human check. Gift shop at 22 7th Avenue, the day 1589 cargo (11 storage shelves, none of that purchase on the floor).

1. Restart Big Ambitions so this build loads. Continue the same save. Confirm `mod.loaded version=1.17.11.2`.
2. Let one morning finish (F6). Do not cancel the time skip. Open the storage cargo, then `%LocalAppData%\ChaoticCEO\ceo.log`.
3. **PASS if** a storage shelf is on the floor (`shelf.placed`) and stays there (`shelf.sold where=placed` does not drop `placed` below `keep`), and there is no new `shelf.ordered` while cargo plus placed plus pending is already at `keep`. **FAIL if** the purchased shelf is still only in cargo, or another shelf is ordered while unplaced shelves already cover `keep`.
4. Evidence: `shelf.placed`, `shelf.sold`, `shelf.keep`, `shelf.ordered`. Version marker: public `1.17.11.2`.

## 14.9 new-game storage aisle (1.17.11.1)

Tier 1: `Seat_live_depth_aisle_gap_is_not_packed`, `KeepTarget_holds_the_empty_plus_one_when_it_blocks_its_own_seat`. Chains: `cover → place live 1+1 → Accessible`, `cover → box need → keep placed +1 → no sell`.

Human check. Gift shop at 22 7th Avenue, the save from the 1.17.11 fail. This check is the empty +1 shelf. It does not check overhire or empty displays.

1. Restart Big Ambitions so this build loads. Continue the same save. Confirm `mod.loaded version=1.17.11.1`.
2. Let one morning roll (F6). Open `%LocalAppData%\ChaoticCEO\ceo.log`.
3. **PASS if** that morning does not sell the empty storage shelf (`shelf.sold where=placed` is absent) and `shelf.keep` shows `placed` staying at 2 when `need=1`. **FAIL if** `shelf.sold where=placed` appears while `need=1` and the shelf count drops to 1.
4. Evidence: `shelf.keep`, `shelf.sold`. Version marker: public `1.17.11.1`.

**Human FAIL 2026-11-13** on 1.17.11 at 22 7th Avenue. Day 1600 `keep=2 placed=2`. Day 1601 `spareSeats=false keep=1 placed=2`, then `shelf.sold where=placed amount=1`. The empty +1 was sold. Day 1602 `placed=1`.

Not in this slice: overhire, empty displays, the second register.
