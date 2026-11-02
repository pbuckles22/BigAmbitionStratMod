# Project status

**Human-readable current state.** Keep in sync with [AGENT_HANDOFF.md](../AGENT_HANDOFF.md) → *Current state* when milestones ship.

**Last updated:** 2026-11-02

---

## Summary

**BigAmbitionStratMod** 1.7.3.2 — Epic 7 is in progress. F9 shows the current roadmap and one logistics move. Confirm on a close step shuts that store. Confirm on the logistics move sends that store to the new warehouse. A boot starts a fresh `ceo.log`. `history.log` keeps one line per roadmap change. Story **7.7** passed. Story **7.8** is on `feature/7.8-redo-logistics` and is waiting on the headquarters logistics-plan check. After that check, finish Epic 7 as **7.13**, then **7.9** through **7.12**. Then Epics 11, 12, 13, 9, 8, and 10. Crossing $100M logs `win.reached` once and play continues. Day 365 is the Epic 8 scoring window only. The rules are in [requirements/strategy.md](requirements/strategy.md). F8 still dumps yesterday’s statements and the route CSVs. `origin` is this repo.

---

## Active branch

| Branch | Role |
|--------|------|
| **`main`** | Integration branch. Last land `db096d4`, version 1.7.2 |
| **`feature/7.8-redo-logistics`** | Story 7.8. Redo button on the logistics move. Version 1.7.3.2. Not merged. |
| **`feature/7.7-close-store`** | Story 7.7. Kept after this CMPH. Version 1.7.2 |
| **`feature/7.6-roadmap-panel`** | Story 7.6. Kept after this CMPH. Version 1.7.1.2 |
| **`feature/7.5-clone-layout`** | Story 7.5. Kept after this CMPH |
| **`feature/7.4-buy-building`** | Story 7.4. Kept after this CMPH |
| **`feature/7.3-apply-prices`** | Story 7.3. Kept after this CMPH |
| **`feature/7.2-redo-logistics`** | Story 7.2. Kept after this CMPH |
| **`feature/7.1-close-stores`** | Story 7.1. Kept after this CMPH |
| **`feature/6.6-roadmap`** | Story 6.6. Kept after this CMPH |
| **`feature/6.5-rent-payback`** | Story 6.5. Kept after this CMPH |
| **`feature/6.4-logistics-review`** | Story 6.4. Kept after this CMPH |
| **`feature/6.3-close-score`** | Story 6.3. Kept after this CMPH |
| **`feature/6.2-game-api`** | Story 6.2. Kept after this CMPH |
| **`feature/6.1-f9-panel`** | Story 6.1. Kept after this CMPH |
| **`feature/5.6-buy`** | Story 5.6. Kept after this CMPH |
| **`feature/5.5-close`** | Story 5.5. Kept after this CMPH |
| **`feature/5.4-layout`** | Story 5.4. Kept after this CMPH |
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

**Last land:** `db096d4` on `main`. Version 1.7.2. Branch `feature/7.7-close-store` kept. Repo: https://github.com/pbuckles22/BigAmbitionStratMod.

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

- Story **7.8** human check. BizMan shows the new warehouse on that store. Then **7.13**, **7.9**, **7.10**, **7.11**, **7.12**, then Epics 11, 12, 13, 9, 8, 10.
- Variants: merge upstream when AgenticTemplate skills/rules change

---

## Reading order for contributors

See [CONTRIBUTING.md](../CONTRIBUTING.md).
