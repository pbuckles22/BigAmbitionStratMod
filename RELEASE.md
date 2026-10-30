## Release / merge discipline (lightweight)

Keep releases/merges boring and reversible.

### Merge-ready (minimum)

Document your real gate in `AGENT_HANDOFF.md` and `TEST_PLAN.md`, then treat it as mandatory:

- Tier 1 is green (fast feedback)
- If the stack compiles: every documented compile ran (test/Debug **and** Release) and the local +BUILD counter incremented; skip the counter when nothing compiles
- Tier 2 is run when behavior demands integration/E2E validation
- A long-run find is locked in Tier 1 before the next batch or soak (`testing.mdc` → *Long run*)
- The chain suite loads the production module and locks the value the next stage reads (`integration-chain.mdc` → *Harness*). The new chain walks `Chain: first → … → this` (*Prefix*).
- Tracked docs updated when workflow/expectations change
- Rollback path is clear (a revert commit is usually sufficient)

### Version

Scheme: `MAJOR.EPIC.COUNTER` plus optional `.FIX`. The counter is ships inside the epic, not the story id. Never go backwards. See [doc/VERSIONING.md](doc/VERSIONING.md).

Current public version: **1.5.2**. Ledger log lines in `script/statement_ledger.py` stay **1.2.2**.

This product cuts epic releases: yes, a GitHub Release on SWAT after that close is on `main`. Do not upload to a store until a product doc says it is distributable. Local `+BUILD` stays gitignored in `build_number.txt`. The mod informational version is `1.5.2+N`.

### Rollback

- Prefer a single revert commit per change
- If a change affects your “stable” line, revert immediately and re-run the required validation tier(s)
