# Agent handoff — Project

## Purpose

This repo is an **agentic template**: Cursor rules, skills, handoff protocol, and testing discipline. **Replace** stack-specific placeholders below with your project's commands (test runner, coverage, integration or E2E).

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
| Rules (`one-story-one-ship`, `wrap-on-command`, `no-auto-merge-main`, `human-check`, `pm-versioning`, `no-cursor-commit-attribution`, `testing.mdc`, handoff-checklist, epic-close, gemini-handoff) | `TEST_TDD.md` (commands + evidence names) |
| Handoff Receiver brief                     | `DESIGN_SYSTEM.md` (UI framework)     |
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
- **Level 4 (optional, local only):** `.cursor/handoff/NNNN-handoff-*.md` — gitignored; never sole source of truth

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
- **Handoff:** **UCPH** and **CMPH** paste a Receiver brief ([.cursor/handoff/_template.md](.cursor/handoff/_template.md)). The review swarm runs on **SWAT** only. See [.cursor/rules/wrap-on-command.mdc](.cursor/rules/wrap-on-command.mdc).

## Contributor onboarding (norm)

1. [CONTRIBUTING.md](CONTRIBUTING.md)
2. [doc/PROJECT_STATUS.md](doc/PROJECT_STATUS.md)
3. [PM_PLAN.md](PM_PLAN.md)

When shipping: update **PM_PLAN**, **doc/PROJECT_STATUS.md**, and **Current state** below in the same PR.

## Current state

- **Template:** Agentic rules and skills in place. Ship cadence is **UCPH** (park the feature branch), **CMPH** (land `main`, keep the branch), **CMPHD** (land and delete), **SWAT** (epic close). Public versions use a plan coordinate and must not go backwards ([doc/VERSIONING.md](doc/VERSIONING.md)). Compiling stacks still count every merge-ready compile (`testing.mdc` → *Compile counter*). Runtimes still write named evidence to a **file** (`testing.mdc` → *Evidence sink*). A batch or soak is not the regression suite: extract the decision and lock a named Tier 1 test before the next long run (`testing.mdc` → *Long run*). A chain test loads the production module and locks the value the next stage reads (`integration-chain.mdc` → *Harness*). Non-compiling / no-runtime stacks skip the counter and the evidence file.
- **Next:** Add your codebase; document run/test commands here and in TEST_PLAN.md. Do not delete the ship-command rules when you replace PM_PLAN.

## Run and test

**Document your commands** (examples — replace with yours):

```bash
# e.g. npm test && npm run build
# e.g. cargo test
# e.g. pytest
```

Replace the block above with your real commands and keep them in sync with TEST_PLAN.md.

**Compile counter:** If those commands **compile**, count every compile (test/Debug and Release are separate). Local gitignored counter + binary `+N` when the toolchain allows. Skip for non-compiling repos (scripts, twitchurl*, DJ tools). See `.cursor/rules/testing.mdc` and [TEST_TDD.md](.cursor/skills/TEST_TDD.md).

**Evidence sink:** If the product has a runtime, named events go in a durable file documented in TEST_PLAN.md. Debugger-only Trace is not evidence. This template has no runtime — skip.

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

When ending a session:

1. On **UCPH** or **CMPH**, paste the Receiver brief in chat and write the same body to the handoff note ([template](.cursor/handoff/_template.md)). Do not run the review swarm.
2. On **SWAT**, run [handoff-checklist.mdc](.cursor/rules/handoff-checklist.mdc) first.
3. Update **PM_PLAN.md** when shipped scope changed.
4. Update **[doc/PROJECT_STATUS.md](doc/PROJECT_STATUS.md)** and **Current state** above (required for contributor-visible changes).

## Epic close (SWAT)

Do not close an epic because it looks done. Run [.cursor/rules/epic-close.mdc](.cursor/rules/epic-close.mdc) only when they typed **SWAT**, or this **CMPH** is the epic’s last in-scope story. See [.cursor/skills/pm-governance/SKILL.md](.cursor/skills/pm-governance/SKILL.md).

Anything the team must see on GitHub belongs in **PROJECT_STATUS**, **PM_PLAN**, **README**, or the **PR** — not only gitignored handoff files.
