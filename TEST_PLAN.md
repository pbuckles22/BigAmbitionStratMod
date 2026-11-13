# Test plan (TEST_PLAN.md)

Define **Tier 1** (fast feedback: unit, headless, or local) and **Tier 2** (integration, browser, device, or E2E) for your stack. Replace the placeholders below.

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

Tier 1: `StageSimTests` locks **StageThresholds.Starting** against every named production const, `Stage.Decide` with thresholds, stage-1 open cap at **Stage2Stores**, `Trial` / `Run` win flags against **WinNetWorth**, empty inputs, and `Tune` (climb over tight, median tie, blank skip). Chain `… → policy → stage-sim` reports win flags after calibrate/noise/policy; FitFile chain tunes climb over tight. Command: the Tier 1 `dotnet test` suite above.

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

Tier 1: `Cover_cuts_the_line_to_free_space` and `Cover_keeps_a_line_that_fits_and_a_space_under_the_minimum`. Chain: `on hand → free space → line` and `on hand → register → free space → line`. A known fixture cuts `need` to the units it can still hold. `cap` on `contract.cover` is that free space. Stock already over the fixture is `need=0`, so a full shelf is not offered a delivery. A paper bag’s space is one cash register (1000) for Hold/FixtureCapacity only; Cover does not clip paper to that free space. A goods shelf is 100. A line that fits is left as it is. A free space under 10 stays that space. An unread fixture, capacity 0, does not cut the line. A full fixture orders one storage shelf (`ba:itemname_storageshelf`, 16 boxes, $1,200) from Hampton Supplies at 13 7th Avenue. A second order waits while that delivery is still open. Cash under $1,200 orders nothing.

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

Tier 1: `Cover_day_1575_register_1000_keeps_the_display_above_half`, `Cover_day_1571_paper_keeps_the_full_line_past_register_free_space`. Chain: `open days → burn → register floor → full line → playbook`. RegisterCap is 1000. Paper need is target − onHand with cap=0. Goods still cut to free space.

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
