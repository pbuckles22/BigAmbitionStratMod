# PM_PLAN — BigAmbitionStratMod

In-game advisor for one Big Ambitions save, then a new-game guide and autopilot. Version scheme `MAJOR.EPIC.COUNTER`. Current public version **1.7.2**. The first ship of a new epic is `1.N.1`. Never go backwards.

## Context

The game is Mono, build **3682**. A mod builds with `dotnet build` against `Big Ambitions_Data\Managed` (`BigAmbitions.dll`, `BigAmbitions.ModAPI.dll`). The pin is `doc/requirements/game-build.md`. Python save tools live in `script/`. The mod, when it exists, lives in `src/`. Evidence for the ledger is `doc/gemini/cache/ledger.log`. The mod writes `%LocalAppData%\ChaoticCEO\ceo.log`. The compile counter is gitignored `build_number.txt`. The mod informational version is `1.7.2+N`. The draw reference is `UnityEngine.IMGUIModule`.

Epic 1 closed on 17 businesses and 5865 statement rows. `ba:businesstype_liquorstore` is **food**. Do not relabel it inside a routes story. A warehouse story may add a specialty override only with a new test.

One story per branch. Human check after **2.2**, after **3.3**, and on every story in Epics 5 through 10. Epics 6 through 10 wait until the one before them works. Monte Carlo is only **8.4–8.5**. The story test gate reuses the save index. Rebuild it when the dropzone `.hsg` changes, when `script/Slice-SaveGame.py` changes, or at epic close.

A story that says overlay, panel, button, or in game names what appears on screen. Its human check is that screen or action. `mod.loaded` does not pass it. Dropping the on-screen part takes an explicit OK and an edit here. From Epic 7 on, a story is done in game when F9 shows it, or when the save changes. A headless story has to say so. "The running game does not call X" is not an exit.

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

**Status:** complete — 2026-10-30. Decision library. F9 still drew the 4.2 overlay until story **7.6**. `Close.Lock` and `Buy.Lock` stay as they are.

- **6.1** `[x]` F9 window in the city. Add `UnityEngine.IMGUIModule`, pin its hash, and draw `Overlay.Build` rows with `Advice.Explain` lines from live state. While open, log `advice.shown` and `panel.opened`. The window is a dark panel with light text. A scroll over it does not zoom the city. Human check passed. Chain: `dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel`. Version 1.6.1.2.
- **6.2** `[x]` Record which build 3682 calls close a business, buy a building, set a price, and rewrite a delivery plan. The note is `doc/requirements/game-api.md`. Close is `Helpers.BusinessHelper.ShutdownBusiness`. Buy is `BizManPresentation.SendBuyBuildingOffer`. Price is `Buildings.Office.Headquarters.PricingManagerPlan.SetPrice`. Delivery is `UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination`. The running game does not call them. Chain: `dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api`. Version 1.6.1.2.
- **6.3** `[x]` Empire-wide close score. Every store, with no tie to the layout clone. Profit after rent, the red-day streak, and the trend. One explain line per store. The running game does not score a store. Chain: `dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score`. Version 1.6.1.2.
- **6.4** `[x]` Logistics review. Which warehouse serves which store. One service per store and cargo family. A recurring stop beats a dated stop. The larger cargo amount wins, a tie keeps the later stop, and amounts of the same family add. Amount 0 is empty. A lamp or a bag is not a family. A mixed warehouse is flagged and is not a move target. An under-supplied store has no positive cargo of that family. One reassignment moves the later short store onto the single-family warehouse with the larger stock. A tie keeps the later warehouse. The close-score line does not choose the store. The running game does not apply the move. Chain: `dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics`. Version 1.6.1.2.
- **6.5** `[x]` Where to open next, and which buildings to buy, from rent-versus-buy payback. Payback days are the purchase price divided by the latest daily rent for that address. The shorter payback is the one street to open and the one building to buy. A tie keeps the later building. A zero or negative price or rent is not a buy. A blank street or a number of 0 is not a buy. A later day supplies the rent, and a later row on that day supplies it. A later zero rent clears an earlier rent. The logistics move does not choose the building. The running game does not buy that building. Chain: `dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback`. Version 1.6.1.2. Human check passed.
- **6.6** `[x]` Roadmap. An ordered top-N action list. The larger daily gain is first. A close step is a scored store whose latest profit is below zero, and the gain is that loss. An open step is the payback building, and the gain is the latest rent. A tie keeps the later step. A repeated score line stays one close. A later day supplies the profit and the rent, and a later row on that day supplies them. A later zero profit clears the close. A later zero rent clears the open. The logistics move is not a step. The running game does not run the list. Chain: `dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap`. Version 1.6.1.2. Headless.

## Epic 7 — Execute on the current save

**Status:** in progress

7.1–7.5 are the headless confirm and record library. None of them calls the game or draws a button. From 7.6 on, the human check is the F9 window or a change on the save.

7.7–7.12 share this exit. The click goes through a confirm step. It calls the build 3682 method from `doc/requirements/game-api.md` on `SaveGameManager.Current`. It writes the existing `Record` line to `ceo.log`. `Confirm` decides which row becomes which call, and a headless test locks that decision. The game call sits behind a thin bridge. Reload undo means loading the last game save. The mod never saves.

- **7.1** `[x]` Close the selected stores. One confirmed close shuts that store and logs `store.closed`. Each close is separate. An unconfirmed close, an open step, another number, and a store outside the list stay open. A blank close call stays open. Reload clears the close and leaves the statement file. The running game does not call ShutdownBusiness. Chain: `dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown`. Version 1.6.1.2. Headless.
- **7.2** `[x]` Redo logistics. One confirmed redo moves that store and logs `logistics.redone`. Each redo is separate. An unconfirmed redo, a different destination, a close street, and a store that is not the move stay put. A review with no move stays put. A blank delivery call stays put. Reload clears the redo and leaves the route file. The running game does not call AddDestination. Chain: `dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown → delivery`. Version 1.6.1.2. Headless. Human check passed.
- **7.3** `[x]` Apply prices and staffing. One confirmed price sets that item and logs `price.applied`. One confirmed staff plan keeps that plan and logs `staff.applied`. Each apply is separate. An unconfirmed price, a different item, a zero price, and a blank price call stay unset. An unconfirmed plan, a different plan, an empty plan, and an unknown kind stay unstaffed. A blank price call does not block the staff plan. Reload clears both and leaves the statement file and the route file. The running game does not call SetPrice. Chain: `dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown → delivery → apply`. Version 1.6.1.2. Headless. Human check passed.
- **7.4** `[x]` Buy a building. One confirmed buy offers that building and logs `building.offered`. Each buy is separate. An unconfirmed buy, a close step, another number, a building outside the list, and a blank buy call stay unbought. A blank price call still offers the building. A close on that address does not block the open. Reload clears the offer and leaves the statement file and the route file. The running game does not call SendBuyBuildingOffer. Chain: `dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown → delivery → apply → offer`. Version 1.6.1.2. Headless. Human check passed.
- **7.5** `[x]` Clone a layout. One confirmed copy clones that layout and logs `layout.cloned`. Each copy is separate. An unconfirmed copy, a different street, a different blueprint, a blank family, a missing source, a missing destination, and a blank clone call stay uncopied. A buy on that street does not replace the blueprint. A blank buy call still copies the layout. Reload clears the copy and leaves the statement file and the route file. The running game does not call LoadBusinessLayoutSet. Chain: `dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown → delivery → apply → offer → clone`. Version 1.6.1.2. Headless. Human check passed.
- **7.6** `[x]` F9 shows the roadmap from live state. `LiveDump.Board` reads statements, routes, warehouse holds, and building prices. A for-sale price keeps the address. An owned `purchasePrice` fills an address that is not for sale. A zero price is dropped. It runs `CloseScore.Score`, then `Logistics.Review`, then `Payback.Decide`, then `Roadmap.Order` with the top 10. The window draws those steps, then the logistics move on its own line. The old overlay stays out of the window. Opening it logs `roadmap.shown` and, the first time that list changes, one `roadmap.changed` line in `history.log`. A later open of the same list does not append history. A boot replaces `ceo.log` with the new `mod.loaded` line and keeps changes in `history.log`, capped at 200 lines. Version 1.7.1.2. Human check passed. Chain: `dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown → delivery → apply → offer → clone → shown`.
- **7.7** `[x]` Close button on each close step. Close arms a red Confirm and a Cancel. Cancel leaves the store. Confirm calls `Helpers.BusinessHelper.ShutdownBusiness` on the registration at that street and number that still has a business name, sets `AvailableForRent`, and refreshes the street sign. A building with no business name at that address leaves the close list. An address that is only absent from the family portfolio stays. A found close logs `store.closed`. A live save with no matching building logs `store.missed` and does not log `store.closed`. Reload of the last game save is the undo. The mod never saves. Version 1.7.2. Human check passed. Chain: `dll → pin → loaded → csv → routes → day → rank → overlay → advice → prices → plans → staffing → layout → close → buy → panel → api → score → logistics → payback → roadmap → shutdown → delivery → apply → offer → clone → shown → closed → gone`.
- **7.8** `[ ]` Redo button on the logistics move. It calls `LogisticsManagerPlanUI.AddDestination`, or a non-UI plan call found in this story and written into `doc/requirements/game-api.md`. Human check: BizMan shows the new warehouse on that store. Version 1.7.3.
- **7.9** `[ ]` Price button and staff button. Price calls `PricingManagerPlan.SetPrice`. Find the `HrManagerPlan` / `HeadhunterPlan` staff call, record it in `doc/requirements/game-api.md`, and call it. Human check: the item price and the staff plan change in game. Version 1.7.4.
- **7.10** `[ ]` Buy button on each open step. It calls `BizManPresentation.SendBuyBuildingOffer`. Human check: the offer or the purchase shows in game and `building.offered` is in `ceo.log`. Version 1.7.5.
- **7.11** `[ ]` Clone button when a cluster qualifies. It calls `BusinessLayoutSetHelper.LoadBusinessLayoutSet`. Human check: the destination store has the source layout. Version 1.7.6.
- **7.12** `[ ]` Execute all. One confirm runs the roadmap steps in order through the 7.7–7.11 calls. A failed step stops the run and is logged. Log `roadmap.executed` with counts. Human check: every listed step happened in game, in that order. Version 1.7.7.

## Epic 8 — Strategy engine

**Status:** later

Not started until **7.12** passes its in-game check. **8.1–8.3** are one deterministic sim. **8.4–8.5** add randomness. The result re-ranks the **7.6** roadmap that F9 shows.

- **8.1** `[ ]` Fit demand and cost curves from the ledger CSVs.
- **8.2** `[ ]` Day stepper using the Epic 4 library. Same `portfolio.csv` shape.
- **8.3** `[ ]` Calibration: a sim from day 1000 lands within tolerance of the real ledger at day 1014.
- **8.4** `[ ]` Seeded demand, rival, and hiring noise. Distribution of net worth at day 365.
- **8.5** `[ ]` Compare policies. Keep the best median with an acceptable worst case.

## Epic 9 — New-game guide

**Status:** later

- **9.1** `[ ]` Step-by-step playbook from day, cash, and what you own. Shown in the panel. No ledger history required. Human check is F9 on a new game.

## Epic 10 — Watch it win

**Status:** later

- **10.1** `[ ]` Name the win, a net-worth target by a set day.
- **10.2** `[ ]` Automate a fresh game to that win, through the 7.7–7.11 game calls.

**Next:** story **7.8** when asked. A redo button on the logistics move calls `LogisticsManagerPlanUI.AddDestination`, or a non-UI plan call found in that story and written into `doc/requirements/game-api.md`. Version 1.7.3.

Operating model (keep these when you replace this plan): `.cursor/rules/` ship commands (UCPH, CMPH, SWAT), human check, plan-coordinate versioning, long-run harvest (`testing.mdc`), and the chain harness and prefix (`integration-chain.mdc`). Do not delete them to “clean up” a new clone.

Keep this file in sync with AGENT_HANDOFF "Current state" and `doc/requirements/` when you add them.
