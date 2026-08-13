## Release / merge discipline (lightweight)

Keep releases/merges boring and reversible.

### Merge-ready (minimum)

Document your real gate in `AGENT_HANDOFF.md` and `TEST_PLAN.md`, then treat it as mandatory:

- Tier 1 is green (fast feedback)
- If the stack compiles: every documented compile ran (test/Debug **and** Release) and the local +BUILD counter incremented; skip the counter when nothing compiles
- Tier 2 is run when behavior demands integration/E2E validation
- Tracked docs updated when workflow/expectations change
- Rollback path is clear (a revert commit is usually sufficient)

### Rollback

- Prefer a single revert commit per change
- If a change affects your “stable” line, revert immediately and re-run the required validation tier(s)
