# Project status

**Human-readable current state.** Keep in sync with [AGENT_HANDOFF.md](../AGENT_HANDOFF.md) → *Current state* when milestones ship.

**Last updated:** 2026-11-08

---

## Summary

**BigAmbitionStratMod** 1.15.4 — Story **8.4** seeded noise shipped. Next is **8.5**. Public **EPIC** for Epic 8 is `1.15` after Epic 9’s `1.14`. **7.13** stays stashed until before **10.2**. The rules are in [requirements/strategy.md](requirements/strategy.md). `origin` is this repo.

---

## Active branch

| Branch | Role |
|--------|------|
| **`main`** | Integration branch. Last land `ebf3e0a`, version 1.15.4. Story 8.4 shipped. |
| **`feature/8.4-seeded-noise`** | Story 8.4 seeded noise. Version 1.15.4. Human PASS 2026-11-08. Kept after CMPH. |
| **`feature/8.3-calibration`** | Story 8.3 calibration. Version 1.15.3. Human PASS 2026-11-08. Kept after CMPH. |
| **`feature/8.2-day-stepper`** | Story 8.2 day stepper. Version 1.15.2. Human PASS 2026-11-08. Kept after CMPH. |
| **`feature/8.1-demand-curves`** | Story 8.1 demand and cost curves. Version 1.15.1. Human PASS 2026-11-08. Kept after CMPH. |
| **`feature/9.6-playbook-f9`** | Story 9.6 F9 playbook next step. Version 1.14.6. Human PASS 2026-11-08. Kept after CMPH. |
| **`feature/9.5-managers-money`** | Story 9.5 managers, money, real estate, close, run. Version 1.14.5. Human PASS 2026-11-08. Kept after CMPH. |
| **`feature/9.4-supply-trucks`** | Story 9.4 supply, trucks, warehouses. Version 1.14.4. Human PASS 2026-11-08. Kept after CMPH. |
| **`feature/9.3-layout-prices`** | Story 9.3 golden layout and prices. Version 1.14.3. Human PASS 2026-11-08. Kept after CMPH. |
| **`feature/9.2-open-where`** | Story 9.2 Open/Where locks. Version 1.14.2. Human PASS 2026-11-08. Kept after CMPH. |
| **`feature/9.1-stage-decide`** | Story 9.1 Stage.Decide locks. Version 1.14.1. Human PASS 2026-11-08. Kept after CMPH. |
| **`feature/13.2-capacity-fixtures`** | Capacity fixtures (13.2 remainder). Version 1.13.8. Human PASS 2026-11-08. Kept after CMPH. |
| **`feature/13.6-marketing-rival`** | Story 13.6 marketing start/stop and rival decline. Version 1.13.7. Human PASS 2026-11-08. Kept after CMPH. |
| **`feature/13.5-time-advance`** | Story 13.5 time advance with player needs. Version 1.13.6. Human PASS 2026-11-08. Kept after CMPH. |
| **`feature/13.4-hire-loan-taxes`** | Story 13.4 hire, loan, demand, taxes. Version 1.13.5. Human PASS 2026-11-08. Kept after CMPH. |
| **`feature/13.3-wholesaler-contract`** | Story 13.3 wholesaler contract. Version 1.13.4. Human PASS 2026-11-07 (shelf stock waived). Kept after CMPH. |
| **`feature/13.2-store-ops`** | Story 13.2 store ops (cleaner). Version 1.13.3.9. Human PASS 2026-11-07. Kept after CMPH. |
| **`feature/13.2-starter-layout`** | Story 13.2 layout and hours. Version 1.13.2. Human PASS 2026-11-07. Kept after this CMPH. |
| **`feature/13.1-rent-building`** | Story 13.1. Version 1.13.1. Human PASS 2026-11-07. Kept after this CMPH. |
| **`feature/12.4-telemetry`** | Story 12.4. Version 1.12.4. Human PASS 2026-11-07. Kept after this CMPH. |
| **`fix/daily-gain-sign`** | Daily gain sign. Version 1.12.3.1. Human PASS 2026-11-06. Kept after this CMPH. |
| **`feature/12.3-f9-controls`** | Story 12.3. Version 1.12.3. Human PASS 2026-11-06. Kept after this CMPH. |
| **`feature/12.2-f9-health`** | Story 12.2. Version 1.12.2. Human PASS 2026-11-06. Kept after this CMPH. |
| **`feature/12.1-f9-top-bar`** | Story 12.1. Version 1.12.1. Human PASS 2026-11-06. Kept after this CMPH. |
| **`feature/11.5-daily-audit`** | Story 11.5. Version 1.11.9. Human PASS 2026-11-06. Kept after this CMPH. |
| **`feature/11.4-restock-warehouses`** | Story 11.4. Version 1.11.8.1. Human PASS 2026-11-05. |
| **`feature/11.3-assign-plans`** | Story 11.3. Version 1.11.7. Human PASS 2026-11-05. Kept after CMPH. |
| **`feature/11.2-connect-stores`** | Story 11.2. Version 1.11.6. Human PASS 2026-11-05. Kept after this CMPH. |
| **`feature/11.1-fill-slots`** | Story 11.1. Version 1.11.5. Human PASS 2026-11-05. Kept after this CMPH. |
| **`feature/11.0-record-calls`** | Story 11.0. Kept after this CMPH. Version 1.11.4 |
| **`feature/11-needs-held`** | Epic 11 needs hold. Version 1.11.3. Human PASS. Kept after this CMPH. |
| **`feature/11-forecast-burn`** | Epic 11 live burn and armed connect. Version 1.11.2.1. Human PASS. Kept after this CMPH. |
| **`feature/supply-check`** | Epic 11 forecast dry run. Version 1.11.1. Kept after this CMPH. |
| **`feature/7.8-redo-logistics`** | Story 7.8. Kept after this CMPH. Version 1.7.3.2 |
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

**Last land:** `9e5ad40` on `main`. Version 1.13.6. Branch `feature/13.5-time-advance` kept. Repo: https://github.com/pbuckles22/BigAmbitionStratMod.

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

- Capacity fixtures Human PASS 2026-11-08 on 1.13.8. Epic **13** complete. Next: story **9.1** when asked. **7.13** stays stashed.
- Variants: merge upstream when AgenticTemplate skills/rules change

---

## Reading order for contributors

See [CONTRIBUTING.md](../CONTRIBUTING.md).
