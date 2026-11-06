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

`dotnet test` is the Debug compile. The Release command installs `ChaoticCEO.dll` to ModsLocal. The gitignored counter is `build_number.txt`. The informational version is `1.11.9+N`.

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

**Long run:** A batch or soak is not Tier 1. When it trips a gate, extract that decision and add a named Tier 1 test before the next long run. See `.cursor/rules/testing.mdc`.
