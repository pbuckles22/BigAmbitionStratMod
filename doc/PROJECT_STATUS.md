# Project status

**Human-readable current state.** Keep in sync with [AGENT_HANDOFF.md](../AGENT_HANDOFF.md) → *Current state* when milestones ship.

**Last updated:** 2026-10-30

---

## Summary

**BigAmbitionStratMod** 1.5.3 — Story 5.3 is on `main`. One role keeps one HR or headhunter plan. The role is the warehouse family from the delivery plan. The larger amount wins, a tie keeps the later plan, amount 0 is not a plan, and an unlimited amount beats a finite count. The running game does not write that plan. A delivery plan keeps one family on a warehouse and one recurring run. Epic 4 stays closed. F8 still dumps yesterday’s statements and the route CSVs. A day change dumps once and logs `day.rolled`. A current `save.bin` is not read again. Rules come from AgenticTemplate (`upstream`). `origin` is this repo.

---

## Active branch

| Branch | Role |
|--------|------|
| **`main`** | Integration branch. Last land recorded after this CMPH, version 1.5.3 |
| **`feature/5.3-staffing`** | Story 5.3. Kept after this CMPH |
| **`feature/5.2-delivery-plans`** | Story 5.2. Kept after this CMPH |
| **`feature/5.1-retail-prices`** | Story 5.1. Kept after this CMPH |
| **`feature/4.3-advice-line`** | Story 4.3. Kept after this CMPH |
| **`feature/4.2-advisor-overlay`** | Story 4.2. Kept after this CMPH |
| **`feature/4.1-advisor-library`** | Kept after story 4.1 |
| **`feature/3.5-day-rollover`** | Kept after this CMPH |
| **`feature/save-bin-reuse`** | Kept after the save.bin CMPH |
| **`feature/3.4-route-csvs`** | Kept after the 3.4 CMPH |
| **`feature/3.3-hotkey-statements`** | Kept after the 3.3 CMPH |
| **`feature/3.2-mod-skeleton`** | Kept after the 3.2 CMPH |
| **`feature/3.1-game-build`** | Kept after the 3.1 CMPH |
| **`feature/2.3-save-text`** | Kept after the 2.3 CMPH |
| **`feature/2.2-rank-report`** | Kept after the 2.2 CMPH |
| **`feature/2.1-routes`** | Kept after the 2.1 CMPH |
| **`feature/plan-epic-ladder`** | Kept after the ladder CMPH |
| **`feature/1.2-portfolio-join`** | Kept after the 1.2 CMPH |
| **`feature/1.1-statement-street`** | Kept after the 1.1 CMPH |
| **`feature/standup-save-tools`** | Kept after the standup CMPH |
| **`feature/long-run-harvest`** | Kept after CMPH (template history) |
| **`feature/chain-harness`** | Kept after CMPH (template history) |
| **`feature/chain-prefix`** | Kept after CMPH (template history) |
| **`feature/handoff-receiver-framing`** | Kept after CMPH (template history) |

**Last land:** recorded after this CMPH on `main`. Version 1.5.3. Branch `feature/5.3-staffing` kept. Repo: https://github.com/pbuckles22/BigAmbitionStratMod.

---

## Completed (template baseline)

- Agentic rules, skills, handoff protocol
- Governance stubs: PM_PLAN, TEST_PLAN, TECH_DEBT, RISKS, RELEASE, INCIDENTS
- **Contributor onboarding norm:** CONTRIBUTING.md, PROJECT_STATUS, GitHub PR/issue templates
- **Compile counter:** compiling stacks count every merge-ready compile (test/Debug **and** Release) via a local gitignored +BUILD; non-compiling stacks (twitchurl*, DJ tools, docs) skip
- **Evidence sink:** runtimes write named lines to a durable file (TEST_PLAN path); debugger-only Trace is not evidence; this template has no runtime
- **Ship commands:** UCPH parks the feature branch; CMPH lands `main` and keeps the branch; CMPHD deletes; SWAT is the only epic-close swarm
- **Human check:** documented install step, then a plain-language ask; runtime ships wait for PASS
- **Versioning:** plan coordinate `MAJOR.EPIC.COUNTER`; counter is not the story id; never backwards ([VERSIONING.md](VERSIONING.md))
- **Gemini packs:** conceptual vs troubleshooting vs repo-to-text; inbound `dropzone/`; `script/Export-GeminiSnapshot.ps1`
- **Leverage log:** [LEVERAGE.md](LEVERAGE.md) and a blank [LEVERAGE_REGISTER.md](LEVERAGE_REGISTER.md)
- **Long-run harvest:** a batch or soak find becomes a named Tier 1 test of the extracted decision before the next long run (`testing.mdc` → *Long run*)
- **Chain harness:** the chain test loads the production module and locks the value the next stage reads (`integration-chain.mdc` → *Harness*). Its first line is `Chain: first → … → this`, and each step receives what the previous step produced (*Prefix*).
- **Receiver framing:** the brief opens with `You are the agent to execute the below hand off.` **Next** names the story. **Acceptance** and **Next steps** list the unit tests and the chain test written red before production code.

---

## Next up (maintainers)

- Story **5.4** when asked. Clone a chain layout onto a new store when a street cluster qualifies.
- Variants: merge upstream when AgenticTemplate skills/rules change

---

## Reading order for contributors

See [CONTRIBUTING.md](../CONTRIBUTING.md).
