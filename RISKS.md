## Risk register (top 5)

This file is intentionally short. Keep only the highest leverage, current risks.

- **Risk**: Merge-ready gates drift from reality (agents think “green” without running the real gate)  
  **Impact**: High  
  **Likelihood**: Med  
  **Trigger**: PRs merge without the documented Tier 1 command; flaky CI ignored  
  **Mitigation**: keep `AGENT_HANDOFF.md` + `TEST_PLAN.md` aligned; enforce handoff checklist  
  **Rollback**: revert the merge; restore last known-good tag/commit

- **Risk**: The calls that close, buy, or rewrite deliveries are not pinned
  **Impact**: High
  **Likelihood**: Med
  **Trigger**: Story **6.1** or Epic 7 starts before the on-screen check or `doc/requirements/game-api.md`
  **Mitigation**: **6.1** proves the F9 window. **6.2** records the calls before any button. The Epic 5 locks stay pure
  **Rollback**: reload the save. Revert the story commit

- **Risk**: Context bloat causes agent drift / incorrect assumptions  
  **Impact**: Med  
  **Likelihood**: High  
  **Trigger**: handoff notes exceed ~500 words; repeated long logs in chat; conflicting “truth” sources  
  **Mitigation**: use session-summarizer; keep durable truth in tracked docs; bootstrap with minimal context  
  **Rollback**: create a compressed handoff note; re-establish source-of-truth docs; re-scope work
