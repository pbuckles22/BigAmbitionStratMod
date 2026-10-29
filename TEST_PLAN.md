# Test plan (TEST_PLAN.md)

Define **Tier 1** (fast feedback: unit, headless, or local) and **Tier 2** (integration, browser, device, or E2E) for your stack. Replace the placeholders below.

---

## Tier 1: Fast feedback

```bash
python -m pytest -q
python -m py_compile script/Export-SaveGameText.py script/Slice-SaveGame.py script/statement_ledger.py
```

---

## Integration chain (1..X together)

Headless tests that run shipped behaviors **together**, through real interfaces. The new chain test’s first line is `Chain: first → … → this`. It walks from the first shipped behavior through this one, and each step receives what the previous step produced. The harness loads the production module and asserts the value the next stage reads. Isolated unit red/green does not replace this. Document the command (it is usually the Tier 1 suite, once the chain tests live there). Rule: [.cursor/rules/integration-chain.mdc](.cursor/rules/integration-chain.mdc) → *Prefix*.

```bash
python -m pytest -q tests/test_statement_street.py tests/test_portfolio_join.py tests/test_routes.py tests/test_report.py
```

The chain reads the build 3682 `.hsg` in `doc/gemini/dropzone/`. Evidence lines `statements.written`, `portfolio.written`, `routes.written`, and `report.written` are appended to `doc/gemini/cache/ledger.log`.

`Chain: gunzip → index → statement → portfolio` reads the statement file, joins street and number, and locks Chaotic Clothing #3. `Chain: gunzip → index → statement → portfolio span` locks Chaotic Hair #8, family service, tco 4003.3822. `Chain: gunzip → index → statement → portfolio → routes` reads that portfolio and locks the Fifth Avenue 36 destination. `Chain: gunzip → index → statement → portfolio → routes hold` locks W1 sodacan amount 45. `Chain: gunzip → index → statement → seven-day sales` locks Hair #5 sales at 143251 and Kabobs #5 sales at 72610.35 for days 1007–1013. `Chain: gunzip → index → statement → portfolio → routes → report` locks Chaotic Kabobs #5 at the bottom of the profit table, profit 898.6715. `Chain: gunzip → index → statement → portfolio → routes → report mix` locks Chaotic Devs as the top office, Fifth Avenue service count 4, and W6 as food plus service.

---

## Tier 2: Integration / E2E

Use when behavior spans a real runtime (browser, device, network, or native APIs).

```bash
# Example: npm run test:e2e
# Example: playwright test
```

---

**Handoff:** Document the exact commands you use for coverage in AGENT_HANDOFF.md so agents can run them consistently.

**Compile counter:** If Tier 1 **compiles**, count every compile (test/Debug **and** Release). Non-compiling stacks skip. See `.cursor/rules/testing.mdc`.

**Evidence sink:** `doc/gemini/cache/ledger.log`. Statement export appends one `statements.written` line (version, row count, first street, profit). Portfolio export appends one `portfolio.written` line (version, row count, business, family, street, number, tco). Route export appends one `routes.written` line (version, route count, warehouse count, portfolio count, warehouse, destination, item, day). Report export appends one `report.written` line (version, bottom business, profit, office count, street count, mix count).

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
