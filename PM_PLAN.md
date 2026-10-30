# PM_PLAN — BigAmbitionStratMod

In-game advisor for one Big Ambitions save, then a new-game guide and autopilot. Version scheme `MAJOR.EPIC.COUNTER`. Current public version **1.5.6**. The first ship of a new epic is `1.N.1`. Never go backwards.

## Context

The game is Mono, build **3682**. A mod builds with `dotnet build` against `Big Ambitions_Data\Managed` (`BigAmbitions.dll`, `BigAmbitions.ModAPI.dll`). The pin is `doc/requirements/game-build.md`. Python save tools live in `script/`. The mod, when it exists, lives in `src/`. Evidence for the ledger is `doc/gemini/cache/ledger.log`. The mod writes `%LocalAppData%\ChaoticCEO\ceo.log`. The compile counter is gitignored `build_number.txt`. The mod informational version is `1.5.6+N`.

Epic 1 closed on 17 businesses and 5865 statement rows. `ba:businesstype_liquorstore` is **food**. Do not relabel it inside a routes story. A warehouse story may add a specialty override only with a new test.

One story per branch. Human check after **2.2**, after **3.3**, and on every story in Epics 5 through 10. Epics 6 through 10 wait until the one before them works. Monte Carlo is only **8.4–8.5**. The story test gate reuses the save index. Rebuild it when the dropzone `.hsg` changes, when `script/Slice-SaveGame.py` changes, or at epic close.

A story that says overlay, panel, button, or in game names what appears on screen. Its human check is that screen or action. `mod.loaded` does not pass it. Dropping the on-screen part takes an explicit OK and an edit here.

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

**Status:** complete — 2026-10-29

Pure decision library. The overlay marks are not drawn. Drawing is story **6.1**.

- **4.1** `[x]` Pure C# library, unit-tested: `Rank`, `CloseCandidates`, `WarehouseFamilyMix`, `StreetClusters`. Same rules as 2.2. Chain: `dll → pin → loaded → csv → routes → day → rank`. Version 1.4.1.
- **4.2** `[x]` Overlay rows: red rows, mixed-warehouse warnings, and where to open next. The marks are not drawn. Chain: `dll → pin → loaded → csv → routes → day → rank → overlay`. Version 1.4.2.
- **4.3** `[x]` One explain line per recommendation. Log `advice.shown`. The running game does not write that line. Chain: `dll → pin → loaded → csv → routes → day → rank → overlay → advice`. Version 1.4.3.

## Epic 5 — Autopilot

**Status:** complete — 2026-10-30

Each action is opt-in, logged, and undone by reload. A wrong soak decision becomes a named Tier 1 test in the Epic 4 library before the next soak.

- **5.1** `[x]` Retail prices from `ItemSoldPerPriceEntry`. The larger amount wins. A tie keeps the later price. Chain: `dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices`. Version 1.5.1.
- **5.2** `[x]` Delivery plans: one family per warehouse, recurring runs. The larger cargo amount wins. A tie keeps the later family. A recurring stop beats a one-day contract. Chain: `dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans`. Version 1.5.2.
- **5.3** `[x]` Staffing via `HrManagerPlan` / `HeadhunterPlan`. One role keeps one plan. The role is the warehouse family from the delivery plan. The larger amount wins. A tie keeps the later plan. Amount 0 is not a plan. An unlimited amount beats a finite count. Chain: `dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing`. Version 1.5.3.
- **5.4** `[x]` Clone a chain layout onto a new store when a street cluster qualifies. One qualifying cluster keeps one clone. The family is the staffing role. The larger interior score wins. A tie keeps the later layout. Score 0 is not a layout. A later cluster without a new store keeps the earlier clone. A count below 3 does not qualify. Chain: `dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout`. Version 1.5.4.
- **5.5** `[x]` Close after N red days. Confirm dialog first, then a toggle. N red days on the layout street and family keep one close. A red day is profit below zero. The longer current streak wins. A tie keeps the later store. A green day breaks the streak. An empty layout keeps no close. Chain: `dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close`. Version 1.5.5.
- **5.6** `[x]` Buy the building under HQ, then under the food warehouse, then on owned streets. One close keeps one buy. HQ wins, then the food warehouse, then a building on the closed street. A tie keeps the later building. An open store keeps no buy. An empty close keeps no buy. The running game does not buy that building. Chain: `dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy`. Version 1.5.6. Human check passed.

## Epic 6 — Visible advisor

**Status:** next

Read-only. This epic draws what **4.2** and **4.3** left as rows. `Close.Lock` and `Buy.Lock` stay as they are.

- **6.1** `[ ]` F9 window in the city. Add `UnityEngine.IMGUIModule`, pin its hash, and draw `Overlay.Build` rows with `Advice.Explain` lines from live state. While open, log `advice.shown` and `panel.opened`. Human check: the rows match the BizMan bottom stores. Chain: `dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel`. Version 1.6.1.
- **6.2** `[ ]` Record which build 3682 calls close a business, buy a building, set a price, and rewrite a delivery plan. The note is `doc/requirements/game-api.md`. Epics 7 and 10 depend on it.
- **6.3** `[ ]` Empire-wide close score. Every store, with no tie to the layout clone. Profit after rent, the red-day streak, and the trend. One explain line per store.
- **6.4** `[ ]` Logistics review. Which warehouse serves which store. Flag mixed families and under-supplied stores. Propose a reassignment.
- **6.5** `[ ]` Where to open next, and which buildings to buy, from rent-versus-buy payback.
- **6.6** `[ ]` Roadmap. An ordered top-N action list with the estimated daily gain for each step.

## Epic 7 — Execute on the current save

**Status:** later

Each command is separate, confirmed, logged, and undone by reload.

- **7.1** `[ ]` Close the selected stores.
- **7.2** `[ ]` Redo logistics.
- **7.3** `[ ]` Apply prices and staffing.
- **7.4** `[ ]` Buy a building.
- **7.5** `[ ]` Clone a layout.
- **7.6** `[ ]` Do it all: run the roadmap in order.

## Epic 8 — Strategy engine

**Status:** later

Not started until Epic 7 works. **8.1–8.3** are one deterministic sim. **8.4–8.5** add randomness. The result re-ranks the **6.6** roadmap.

- **8.1** `[ ]` Fit demand and cost curves from the ledger CSVs.
- **8.2** `[ ]` Day stepper using the Epic 4 library. Same `portfolio.csv` shape.
- **8.3** `[ ]` Calibration: a sim from day 1000 lands within tolerance of the real ledger at day 1014.
- **8.4** `[ ]` Seeded demand, rival, and hiring noise. Distribution of net worth at day 365.
- **8.5** `[ ]` Compare policies. Keep the best median with an acceptable worst case.

## Epic 9 — New-game guide

**Status:** later

- **9.1** `[ ]` Step-by-step playbook from day, cash, and what you own. Shown in the panel. No ledger history required.

## Epic 10 — Watch it win

**Status:** later

- **10.1** `[ ]` Name the win, a net-worth target by a set day.
- **10.2** `[ ]` Automate a fresh game to that win, within the calls **6.2** recorded.

**Next:** story **6.1** when asked. Version 1.6.1. F9 draws the advisor window in the city.

Operating model (keep these when you replace this plan): `.cursor/rules/` ship commands (UCPH, CMPH, SWAT), human check, plan-coordinate versioning, long-run harvest (`testing.mdc`), and the chain harness and prefix (`integration-chain.mdc`). Do not delete them to “clean up” a new clone.

Keep this file in sync with AGENT_HANDOFF "Current state" and `doc/requirements/` when you add them.
