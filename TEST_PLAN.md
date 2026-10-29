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

`dotnet test` is the Debug compile. The Release command installs `ChaoticCEO.dll` to ModsLocal. The gitignored counter is `build_number.txt`. The informational version is `1.3.4+N`.

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

`Chain: gunzip → index → statement → portfolio` reads the statement file, joins street and number, and locks Chaotic Clothing #3. `Chain: gunzip → index → statement → portfolio span` locks Chaotic Hair #8, family service, tco 4003.3822. `Chain: gunzip → index → statement → portfolio → routes` reads that portfolio and locks the Fifth Avenue 36 destination. `Chain: gunzip → index → statement → portfolio → routes hold` locks W1 sodacan amount 45. `Chain: gunzip → index → statement → seven-day sales` locks Hair #5 sales at 143251 and Kabobs #5 sales at 72610.35 for days 1007–1013. `Chain: gunzip → index → statement → portfolio → routes → report` locks Chaotic Kabobs #5 at the bottom of the profit table, profit 898.6715. Saved-game-to-text calls that same chain on the real save and locks Chaotic Kabobs #5 in the export. `Chain: gunzip → index → statement → portfolio → routes → report kept` keeps the current index and logs `savetext.written`. `Chain: gunzip → index → statement → portfolio → routes → report mix` locks Chaotic Devs as the top office, Fifth Avenue service count 4, and W6 as food plus service. Story 3.1 does not extend that ledger chain. `Chain: dll → pin` publishes one Managed DLL and locks its sha256. `Chain: dll → pin names` locks `BigAmbitions.dll` to its own hash when `BigAmbitions.ModAPI.dll` is in the same pin. The pin file is `doc/requirements/game-build.md`. It has no ledger evidence line. `Chain: dll → pin → loaded` reads that pin and locks build 3682 on `mod.loaded`. `Chain: dll → pin → loaded distinct` keeps the dll hashes off that line. `Chain: dll → pin → loaded default` calls the load the mod uses and locks the same build under `ChaoticCEO\ceo.log`. `Chain: dll → pin → loaded → csv` calls that load, then dumps yesterday’s statements, and locks `csv.written` for day 1013 at Fourth Avenue 28. `Chain: dll → pin → loaded → csv empty` calls that load when yesterday is missing and locks `csv.written` with `rows=0` and a header-only `statements.csv`. `Chain: dll → pin → loaded → csv → routes` calls that statement dump, then the route dump, and locks `routes.written` for the Fourth Street 46 soda delivery. `Chain: dll → pin → loaded → csv → routes empty` calls that statement dump, then an empty route dump, and locks `routes.written` with `routes=0` and a header-only `routes.csv`. `Chain: dll → pin → loaded → csv → routes shelf` calls that statement dump, counts cargo holds, and locks W1 soda amount 1 with the placed lamp and shelf left out.

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

**Evidence sink:** `doc/gemini/cache/ledger.log`. The mod appends one `mod.loaded` line (`version`, `build`) to `%LocalAppData%\ChaoticCEO\ceo.log`. The hotkey dump appends one `csv.written` line (`version`, `rows`, `day`, `street`, `number`, `profit`) to that same log and writes `%LocalAppData%\ChaoticCEO\statements.csv`. The same key then appends one `routes.written` line (`version`, `routes`, `warehouses`, `warehouse`, `destination`, `item`, `day`) and writes `routes.csv` and `warehouses.csv` beside it. Statement export appends one `statements.written` line (version, row count, first street, profit). Portfolio export appends one `portfolio.written` line (version, row count, business, family, street, number, tco). Route export appends one `routes.written` line (version, route count, warehouse count, portfolio count, warehouse, destination, item, day). Report export appends one `report.written` line (version, bottom business, profit, office count, street count, mix count). Saved-game-to-text appends one `savetext.written` line (version, bottom business).

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
