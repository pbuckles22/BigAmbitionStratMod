# Agent handoff — Project

## Purpose

BigAmbitionStratMod is a private decision tool for a Big Ambitions save. It keeps the agentic rules and adds save-to-text tools. **Sync:** `upstream` is [AgenticTemplate](https://github.com/pbuckles22/AgenticTemplate). `origin` is this repo. Pull shared rule updates with `git fetch upstream && git merge upstream/main`.

---

## Creating variant templates (e.g. Flutter, React Native, backend)

Use this pattern when you need a **stack-specific** variant that shares the agentic layer but has its own tooling (e.g. [FlutterAgenticTemplate](https://github.com/pbuckles22/FlutterAgenticTemplate)).

### Initial setup (create the variant)

```bash
# 1. Create your variant repo on GitHub, then clone it
git clone https://github.com/YOUR_ORG/YourVariantTemplate.git
cd YourVariantTemplate

# 2. Add AgenticTemplate as upstream remote
git remote add upstream https://github.com/pbuckles22/AgenticTemplate.git
git fetch upstream

# 3. Merge the base template (first time only)
git merge upstream/main --allow-unrelated-histories
# Resolve any conflicts, keeping your stack-specific files

# 4. Push
git push origin main
```

### Syncing updates from AgenticTemplate

When AgenticTemplate gets new skills or enhancements:

```bash
cd YourVariantTemplate
git fetch upstream
git merge upstream/main
# Resolve conflicts — keep stack-specific overrides in:
#   - DEV_GUIDE.md, TEST_TDD.md, DESIGN_SYSTEM.md
#   - always.mdc (project context)
#   - AGENT_HANDOFF.md (run/test commands)
git push origin main
```

### What stays shared vs stack-specific

| Shared (sync from upstream)                | Stack-specific (keep yours)           |
| ------------------------------------------ | ------------------------------------- |
| Most skills (techwriter, tester, code-reviewer, code-quality-gate, tech-lead, etc.) | `DEV_GUIDE.md` (architecture, tooling) |
| Rules (`one-story-one-ship`, `wrap-on-command`, `no-auto-merge-main`, `human-check`, `pm-versioning`, `no-cursor-commit-attribution`, `testing.mdc`, `integration-chain.mdc`, `handoff-v2`, handoff-checklist, epic-close, gemini-handoff) | `TEST_TDD.md` (commands + evidence names) |
| Handoff v2 note                            | `DESIGN_SYSTEM.md` (UI framework)     |
| Operating model skills (green-and-clean, etc.) | `always.mdc` (5–10 project invariant lines only) |
|                                            | `AGENT_HANDOFF.md` (run/test section) |
|                                            | Optional overlay rules (install/smoke paths) |

### Document the upstream in your variant

Add this to your variant's AGENT_HANDOFF.md under Purpose:

```markdown
**Sync:** This repo tracks **AgenticTemplate** as an **upstream remote** for shared skills/rules.
To pull shared updates: `git fetch upstream && git merge upstream/main` (resolve stack-specific conflicts manually).
```

---

## Source of truth

- **Scope / sprints:** [PM_PLAN.md](PM_PLAN.md)
- **Skills:** [.cursor/skills/](.cursor/skills/) — DEV_GUIDE.md, TEST_TDD.md, DESIGN_SYSTEM.md, techwriter, tester, code-reviewer, **code-quality-gate**, **tech-lead**, tech-debt-evaluator, eval-engineer, risk-manager, release-manager, security-reviewer, incident-triager, green-and-clean, context-bootstrapper, session-summarizer, pm-governance, ui-ux, game-readiness, visual-match, **github-feature-workflow**

## Green and clean operating model (how we work)

This template assumes a strict operating model aimed at **green and clean** delivery:

- **Green**: each change is verifiable against explicit acceptance criteria and validated at the appropriate tier.
- **Clean**: context is curated; durable state lives in tracked docs; handoffs are compressed and decision-first.

Skills that enforce this:

- [.cursor/skills/green-and-clean/SKILL.md](.cursor/skills/green-and-clean/SKILL.md)
- [.cursor/skills/context-bootstrapper/SKILL.md](.cursor/skills/context-bootstrapper/SKILL.md)
- [.cursor/skills/session-summarizer/SKILL.md](.cursor/skills/session-summarizer/SKILL.md)
- [.cursor/skills/eval-engineer/SKILL.md](.cursor/skills/eval-engineer/SKILL.md)

## Context hierarchy (what belongs where)

Contributors and agents use **tracked docs** for product truth. See [CONTRIBUTING.md](CONTRIBUTING.md).

- **Level 1:** [CONTRIBUTING.md](CONTRIBUTING.md), [doc/PROJECT_STATUS.md](doc/PROJECT_STATUS.md), `.cursor/rules/always.mdc`, this file
- **Level 2:** [PM_PLAN.md](PM_PLAN.md), [TEST_PLAN.md](TEST_PLAN.md)
- **Level 3:** current task plan + acceptance criteria
- **Level 4 (local session delta):** `doc/handoff/NNNN-HANDOFF-*.md` — gitignored. Chat names that file. It is not product truth. Older notes stay unread.

Token hygiene: prefer Level 1 + Level 2 + current files over transcript dumps.

## Risk discipline

Keep the top risks explicit and current:

- [RISKS.md](RISKS.md) — top 5 only (impact/likelihood/trigger/mitigation/rollback)

## Release / merge discipline

Keep "ship" criteria explicit and boring:

- [RELEASE.md](RELEASE.md) — merge-ready expectations and rollback posture

## Technical debt discipline

Track debt continuously and evaluate ROI:

- [.cursor/skills/tech-debt-evaluator/SKILL.md](.cursor/skills/tech-debt-evaluator/SKILL.md) — produces "Do first" items during handoff
- [TECH_DEBT.md](TECH_DEBT.md) — durable ranked backlog (promote persistent "Do first" items here)

## Incident / debugging discipline

When something breaks, use evidence-driven triage and keep it bounded:

- [.cursor/skills/incident-triager/SKILL.md](.cursor/skills/incident-triager/SKILL.md)
- [INCIDENTS.md](INCIDENTS.md) — what to capture (minimum) for handoff and prevention

## Pod (agents always working)

- **Techwriter:** Use when editing README, AGENT_HANDOFF, or internal docs.
- **Tester:** Black-box tests; run your **documented** test command after changes; keep the suite green. See [TEST_PLAN.md](TEST_PLAN.md).
- **Handoff:** [handoff-v2.mdc](.cursor/rules/handoff-v2.mdc). Chat names the note. The review swarm runs on **SWAT** only.

## Contributor onboarding (norm)

1. [CONTRIBUTING.md](CONTRIBUTING.md)
2. [doc/PROJECT_STATUS.md](doc/PROJECT_STATUS.md)
3. [PM_PLAN.md](PM_PLAN.md)

When shipping: update **PM_PLAN**, **doc/PROJECT_STATUS.md**, and **Current state** below in the same PR.

## Current state

- **Product:** Version 1.12.1. F9 shows a top bar with day, cash, net worth against $100M, yesterday’s profit, stage, autopilot, and speed. A day change still audits the save. 7.13 stays stashed. Ladder: [PM_PLAN.md](PM_PLAN.md).
- **On:** `origin/main` after this land. Stash `7.13 spare cost centers` on `feature/7.13-spare-cost-centers`. https://github.com/pbuckles22/BigAmbitionStratMod
- **Next:** Story 12.2, the F9 health section, when asked.
- **Blockers:** Do not pop 7.13. Do not load the day 1021 save. Do not drop Fifth Avenue 36’s contract before the hub holds that item.

## Run and test

**Document your commands** (examples — replace with yours):

```bash
python -m pytest -q
python -m py_compile script/Export-SaveGameText.py script/Slice-SaveGame.py script/statement_ledger.py script/game_build.py
dotnet test src/ChaoticCEO.Tests/ChaoticCEO.Tests.csproj -p:GameManaged="C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"
dotnet build src/ChaoticCEO/ChaoticCEO.csproj -c Release -p:GameManaged="C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"
```

Run that from the repo root before commit. `dotnet test` is the Debug compile. The Release command is the production compile and installs `ChaoticCEO.dll` to `%USERPROFILE%\AppData\LocalLow\Hovgaard Games\Big Ambitions\ModsLocal\ChaoticCEO\`. The story gate reuses `doc/gemini/cache/save.bin` and `save-index.sqlite` when both are current for the dropzone `.hsg`. Do not read or scan `save.bin` again while that blob is current. A renamed `.hsg` fails the recorded-name check on its own. Read and scan again only when the `.hsg` is newer than the blob, the file name differs, `script/Slice-SaveGame.py` changes, `script/statement_ledger.py` changes, or this is an epic close. `write_statements` calls `ensure_index`, which returns when the index is current. `build()` deletes the sqlite when the index is stale. `write_statements`, `write_portfolio`, and `write_routes` still load the current blob on each call. Do not add another full read. The save cache under `doc/gemini/cache/` is gitignored. Statement export appends `statements.written` to `doc/gemini/cache/ledger.log`. Portfolio export appends `portfolio.written`. Route export appends `routes.written`. Report export appends `report.written`. Saved-game-to-text appends `savetext.written`.

**Compile counter:** The mod compiles. `dotnet test` and the Release `dotnet build` each count when they compile. The gitignored counter is `build_number.txt` (seed `build_number.txt.example`). The binary informational version is `1.11.9+N`. The public version is `1.11.9`. See `.cursor/rules/testing.mdc` and [TEST_TDD.md](.cursor/skills/TEST_TDD.md).

**Evidence sink:** `doc/gemini/cache/ledger.log`. The mod writes `%LocalAppData%\ChaoticCEO\ceo.log`. The hotkey appends one `csv.written` line and writes `%LocalAppData%\ChaoticCEO\statements.csv`, then appends one `routes.written` line and writes `routes.csv` and `warehouses.csv`. A day change appends one `day.rolled` line (`version`, `day`) after that dump, then one `supply.forecast` line per gap (`day`, `store`, `warehouse`, `item`, `reason`, `buffer`), one `supply.summary` (`gaps`), `supply.move` lines (`amount`, `dry`) up to ten (a gap with a blank warehouse gets no move), and one `supply.rechecked` line per `noplan` gap. Before the moves it appends `snapshot.taken` and the route rows to `%LocalAppData%\ChaoticCEO\snapshots.log`. An armed connect appends the store to `routes.csv` and logs `dry=false`. The game save is not written. Statement export appends one `statements.written` line. Portfolio export appends one `portfolio.written` line. Route export appends one `routes.written` line. Report export appends one `report.written` line. Saved-game-to-text appends one `savetext.written` line. The first frame of a loaded save appends one `needs.held` line (`version`, `day`, `energy`, `hunger`, `happiness`, `energyOff`, `happinessOff`). A later frame of that save does not append it again. A day change appends one `slot.filled` line per empty garage slot (`version`, `day`, `warehouse`, `slot`, `order`, `assign`, `driver`). When every slot already has a truck it appends one `slot.summary` line (`bought=0`) and no `slot.filled` line. A later day of that save does not append those lines again. The garage is not changed. A day change that records connects appends one `store.connected` line per unplanned store item (`version`, `day`, `store`, `warehouse`, `item`, `add`, `drop`). `add` is AddDestination. `drop` is Remove when that hub holds the item, and empty when the store has no contract. A planned store has no line. A contract for an item no warehouse holds has no line. A contract stays when the hub holds less than one day of the item. The connect rewrites `routes.csv` and does not write `warehouses.csv`. A later day of that save does not append those lines again. The game is not called. A day change appends one `plan.created` line (`version`, `day`, `create`) when the save has no pricing plan, and one `plan.assigned` line per open headquarters role (`version`, `day`, `role`, `employee`, `assign`). `create` is AddPlan. `assign` is AssignEmployee. A role that already has a person has no line. When the pricing plan exists and every role has a person it appends one `plan.summary` line (`created=0`, `assigned=0`) and no `plan.created` or `plan.assigned` line. A later day of that save does not append those lines again. The game is not called. Opening the F9 window appends one `panel.opened` line (`version`, `rows`) and one `advice.shown` line per row (`version`, `tone`, `business`, `street`, `number`, `explain`). A later frame while it stays open does not append those lines again. A confirmed close appends one `store.closed` line (`version`, `street`, `number`, `call`). A live save with no matching building appends `store.missed` and does not append `store.closed`. Reload does not append another line. A confirmed logistics redo appends one `logistics.redone` line (`version`, `destination`, `from`, `to`, `family`, `call`). A loaded save with no matching plan appends `logistics.missed` and does not append `logistics.redone`. An unconfirmed redo, a different destination, and a reload do not append another line. A confirmed price appends one `price.applied` line (`version`, `item`, `price`, `call`). A confirmed staff plan appends one `staff.applied` line (`version`, `role`, `kind`, `plan`, `amount`). An unconfirmed apply, a different item or plan, and a reload do not append another line. A confirmed building offer appends one `building.offered` line (`version`, `street`, `number`, `call`). An unconfirmed buy, a close step, another number, and a reload do not append another line. A confirmed layout copy appends one `layout.cloned` line (`version`, `street`, `family`, `blueprint`, `source`, `destination`, `call`). An unconfirmed copy, a different street, a different blueprint, a blank family, and a reload do not append another line. Opening the F9 roadmap appends one `roadmap.shown` line (`version`, `steps`, `street`, `number`, `gain`, `move`). A later frame while the window stays open does not append it again. Closing the window does not append it. A day change appends one `audit.summary` (`unconnected`, `emptySlots`, `dry`, `mixed`, `unstaffed`, `missingPlans`, `cashBelowReserve`, `pinMismatch`, `healed`) and one `audit.healed` line per heal (`action`, `store`, `warehouse`, `item`, `call`, `dry`) up to ten. A connect heal calls the logistics destination on the loaded save. A dry run logs `dry=true` and does not call it. F10 appends `autopilot.paused reason=f10` and later days do not heal. Three failed heals append `autopilot.paused reason=failures`. A build pin mismatch appends `autopilot.halted reason=pin` and does not heal. Before the heals it appends `snapshot.taken` to `snapshots.log`. Debugger-only Trace is not evidence.

**Long run:** A batch or soak is not the regression suite. Extract the decision and lock a named Tier 1 test before the next long run (`testing.mdc` → *Long run*).

## Conventions

- Prefer pure functions for business logic where possible.
- **Docs:** Use the **techwriter** skill when editing README, AGENT_HANDOFF, or internal docs.
- **Tests:** Black-box; run your project test command after logic or test changes; keep the suite green (see .cursor/skills/tester/SKILL.md). Prefer writing a failing test before new production code (TDD) where applicable.

---

## Git workflow (how work lands on `main`)

Document **your** team rules here and keep them in sync with what you run locally.

1. **Integration branch:** Usually **`main`**. All shipped product state (PM_PLAN, roadmap checkboxes) should reflect what is merged here.
2. **Feature branches:** One story per branch, `feature/<story-id>-short-topic`. Follow [.cursor/skills/github-feature-workflow/SKILL.md](.cursor/skills/github-feature-workflow/SKILL.md) and [.cursor/rules/wrap-on-command.mdc](.cursor/rules/wrap-on-command.mdc). Push the feature branch after the gate. Merge to `main` only after **CMPH** permission ([.cursor/rules/no-auto-merge-main.mdc](.cursor/rules/no-auto-merge-main.mdc)). **UCPH** does not merge.
3. **Before push / merge-ready:** Run your **full gate** (document it in the **Run and test** section above — e.g. tests + build + integration/E2E). CI should run the headless suite. A build that needs a local device or private binary stays local.
4. **After push — verify CI:** Agents do not get GitHub failure emails. When Actions exist, run `gh run watch --repo OWNER/REPO` (or `gh run list` + `gh run view --log-failed`) before declaring work done on `main`. See [.cursor/skills/github-feature-workflow/SKILL.md](.cursor/skills/github-feature-workflow/SKILL.md).
5. **Pull requests:** **Optional**. Do not invent a PR step.
6. **After merge:** **CMPH** keeps the feature branch. **CMPHD** deletes local and remote after `main` is pushed. Record the land sha in [doc/PROJECT_STATUS.md](doc/PROJECT_STATUS.md). See [doc/git/Feature_Branch_Archaeology.md](doc/git/Feature_Branch_Archaeology.md).

---

## Handoff protocol

On **UCPH**, **CMPH**, or **SWAT**, follow [handoff-v2.mdc](.cursor/rules/handoff-v2.mdc). Chat is three lines naming the note. The note is the only copy of the session delta.

Sync **On**, version, **Next**, and **Blockers** into *Current state* above. Update **PM_PLAN.md** and **[doc/PROJECT_STATUS.md](doc/PROJECT_STATUS.md)** when shipped scope changed. **SWAT** gates stay in [handoff-checklist.mdc](.cursor/rules/handoff-checklist.mdc). Do not run that swarm on UCPH or CMPH.

## Epic close (SWAT)

Do not close an epic because it looks done. Run [.cursor/rules/epic-close.mdc](.cursor/rules/epic-close.mdc) only when they typed **SWAT**, or this **CMPH** is the epic’s last in-scope story. See [.cursor/skills/pm-governance/SKILL.md](.cursor/skills/pm-governance/SKILL.md).

Anything the team must see on GitHub belongs in **PROJECT_STATUS**, **PM_PLAN**, **README**, or the **PR** — not only gitignored handoff files.
