# PM_PLAN — BigAmbitionStratMod

Private decision tool for one Big Ambitions save. Version scheme `MAJOR.EPIC.COUNTER`. Current public version **1.4.1**. The first ship of a new epic is `1.N.1`. Never go backwards.

## Context

The game is Mono, build **3682**. A mod builds with `dotnet build` against `Big Ambitions_Data\Managed` (`BigAmbitions.dll`, `BigAmbitions.ModAPI.dll`). The pin is `doc/requirements/game-build.md`. Python save tools live in `script/`. The mod, when it exists, lives in `src/`. Evidence for the ledger is `doc/gemini/cache/ledger.log`. The mod writes `%LocalAppData%\ChaoticCEO\ceo.log`. The compile counter is gitignored `build_number.txt`. The mod informational version is `1.4.1+N`.

Epic 1 closed on 17 businesses and 5865 statement rows. `ba:businesstype_liquorstore` is **food**. Do not relabel it inside a routes story. A warehouse story may add a specialty override only with a new test.

One story per branch. Human check after **2.2**, after **3.3**, and on every Epic 5 story. Epics 5 and 6 wait until the one before them works. Monte Carlo is only **6.4–6.6**. The story test gate reuses the save index. Rebuild it when the dropzone `.hsg` changes, when `script/Slice-SaveGame.py` changes, or at epic close.

## Epic 1 — Save ledger

**Status:** complete — 2026-10-28

- **1.1** `[x]` Statement rows with a street. `doc/gemini/cache/ledger/statements.csv` columns: day, street, number, sales, goods, wages, rent, marketing, theft, profit. Chain: `gunzip → index → statement`. Version 1.1.1.
- **1.2** `[x]` Join each statement street and number to BuildingRegistration. `portfolio.csv` is one row per business, with family (food / durables / specialty / service / office / hq / warehouse) and TCO = goods + wages + rent + marketing, averaged over the days present. Chain: `gunzip → index → statement → portfolio`. Version 1.1.2.

Routes, the rank report, and saved-game-to-text wiring were cut from this epic. They are Epic 2.

## Epic 2 — Routes and rank report

**Status:** complete — 2026-10-28

Save-side. No game process.

- **2.1** `[x]` Decode delivery plans to `doc/gemini/cache/ledger/routes.csv` (`warehouse, destination, item, day, truck`) and `warehouses.csv` (what each warehouse holds). Chain: `gunzip → index → statement → portfolio → routes`. Log `routes.written`. Version 1.2.1.
- **2.2** `[x]` `doc/gemini/cache/ledger/REPORT.md`: bottom 10 by average daily profit, top offices, streets with 3+ of one family, warehouses that mix families. Chain: `gunzip → index → statement → portfolio → routes → report`. Log `report.written`. Human check passed. Version 1.2.2.
- **2.3** `[x]` Saved-game-to-text runs the ledger through the report. A current save index stays. Chain: `gunzip → index → statement → portfolio → routes → report` on the cached index. Log `savetext.written`. Version 1.2.2.

## Epic 3 — Read-only mod

**Status:** complete — 2026-10-29

Same CSV schema as the ledger, read from live memory. The chain diffs the mod CSV against the ledger CSV for the same day.

- **3.1** `[x]` Pin Managed DLL names and hashes into `doc/requirements/game-build.md`. Version 1.3.1.
- **3.2** `[x]` Mod skeleton from the official SDK. `dotnet build` installs to `ModsLocal`. Log `mod.loaded` with the game build to `ceo.log`. `dotnet test` and `dotnet build -c Release -p:GameManaged=...` are in AGENT_HANDOFF. Chain: `dll → pin → loaded`. The compile counter starts here. Version 1.3.2.
- **3.3** `[x]` Hotkey dumps yesterday’s `FinancialSummary` to `statements.csv`. Log `csv.written`. Chain: `dll → pin → loaded → csv`. Version 1.3.3.
- **3.4** `[x]` Dump warehouses and delivery plans to `routes.csv` and `warehouses.csv`. A hold is cargo. A placed shelf, lamp, spawner, or delivery spot is not a hold. Chain: `dll → pin → loaded → csv → routes`. Log `routes.written`. Version 1.3.4.
- **3.5** `[x]` Auto dump at day rollover. Log `day.rolled`. Chain: `dll → pin → loaded → csv → routes → day`. Version 1.3.5.

## Epic 4 — Advisor

**Status:** in progress

The human still clicks.

- **4.1** `[x]` Pure C# library, unit-tested: `Rank`, `CloseCandidates`, `WarehouseFamilyMix`, `StreetClusters`. Same rules as 2.2. Chain: `dll → pin → loaded → csv → routes → day → rank`. Version 1.4.1.
- **4.2** `[ ]` Overlay: red rows, mixed-warehouse warnings, and where to open next.
- **4.3** `[ ]` One explain line per recommendation. Log `advice.shown`.

## Epic 5 — Autopilot

**Status:** later

Each action is opt-in, logged, and undone by reload. A wrong soak decision becomes a named Tier 1 test in the Epic 4 library before the next soak.

- **5.1** `[ ]` Retail prices from `ItemSoldPerPriceEntry`.
- **5.2** `[ ]` Delivery plans: one family per warehouse, recurring runs.
- **5.3** `[ ]` Staffing via `HrManagerPlan` / `HeadhunterPlan`.
- **5.4** `[ ]` Clone a chain layout onto a new store when a street cluster qualifies.
- **5.5** `[ ]` Close after N red days. Confirm dialog first, then a toggle.
- **5.6** `[ ]` Buy the building under HQ, then under the food warehouse, then on owned streets.

## Epic 6 — Headless sim and Monte Carlo

**Status:** later

Not started until Epic 5 works. **6.1–6.3** are one deterministic sim. **6.4–6.6** add randomness.

- **6.1** `[ ]` Fit demand and cost curves from the ledger CSVs.
- **6.2** `[ ]` Day stepper using the Epic 4 library. Same `portfolio.csv` shape.
- **6.3** `[ ]` Calibration: a sim from day 1000 lands within tolerance of the real ledger at day 1014.
- **6.4** `[ ]` Seeded demand, rival, and hiring noise. Distribution of net worth at day 365.
- **6.5** `[ ]` Compare policies. Keep the best median with an acceptable worst case.
- **6.6** `[ ]` New-game mode: start fresh, and the autopilot follows the chosen policy.

**Next:** story **4.2** when asked.

Operating model (keep these when you replace this plan): `.cursor/rules/` ship commands (UCPH, CMPH, SWAT), human check, plan-coordinate versioning, long-run harvest (`testing.mdc`), and the chain harness and prefix (`integration-chain.mdc`). Do not delete them to “clean up” a new clone.

Keep this file in sync with AGENT_HANDOFF "Current state" and `doc/requirements/` when you add them.
