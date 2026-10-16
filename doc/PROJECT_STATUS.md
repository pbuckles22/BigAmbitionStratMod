# Project status

**Human-readable current state.** Keep in sync with [AGENT_HANDOFF.md](../AGENT_HANDOFF.md) → *Current state* when milestones ship.

**Last updated:** 2026-10-16

---

## Summary

**AgenticTemplate** — stack-agnostic Cursor rules, skills, handoff protocol, and TDD discipline. No application runtime; bolt-ons add their own stack.

---

## Active branch

| Branch | Role |
|--------|------|
| **`main`** | Integration branch — template baseline |
| **`feature/long-run-harvest`** | Kept after CMPH |
| **`feature/chain-harness`** | Kept after CMPH |

**Last land:** `e4ca9e0` on `main`.

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

---

## Next up (maintainers)

- Consumers: clone → customize AGENT_HANDOFF run/test → add product code under `doc/requirements/`
- Variants: merge upstream when AgenticTemplate skills/rules change

---

## Reading order for contributors

See [CONTRIBUTING.md](../CONTRIBUTING.md).
