# PM_PLAN — Project

Replace with your project’s phases and sprints.

## Epic 1 — Save ledger

**Status:** complete — 2026-10-28

- **1.1** `[x]` Statement rows with a street. `doc/gemini/cache/ledger/statements.csv` columns: day, street, number, sales, goods, wages, rent, marketing, theft, profit. Chain: `gunzip → index → statement`. Version 1.1.1.
- **1.2** `[x]` Join each statement street and number to BuildingRegistration. `portfolio.csv` is one row per business, with family (food / durables / specialty / service / office / hq / warehouse) and TCO = goods + wages + rent + marketing, averaged over the days present. Chain: `gunzip → index → statement → portfolio`. Version 1.1.2.
- **Next:** no story until one is named.

Operating model (keep these when you replace this plan): `.cursor/rules/` ship commands (UCPH, CMPH, SWAT), human check, plan-coordinate versioning, long-run harvest (`testing.mdc`), and the chain harness and prefix (`integration-chain.mdc`). Do not delete them to “clean up” a new clone.

Keep this file in sync with AGENT_HANDOFF "Current state" and `doc/requirements/` when you add them.
