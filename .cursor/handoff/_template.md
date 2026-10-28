# Handoff v2 — copy this density

Rules: [`.cursor/rules/handoff-v2.mdc`](../rules/handoff-v2.mdc). **≤ 220 words.** Chat is three lines naming this file. Do not paste this body into chat.

## Worked example

Receiver brief (0.2 closed, 0.3 open)

**Objective:** You will start on story 0.3 when asked.

**Git**

| | |
|--|--|
| **Story** | 0.2 `[x]`; 0.3 `[ ]` |
| **Version** | no bump |
| **On** | `origin/main @ abc1234` |
| **Do not** | re-merge, `git log` |
| **Next** | You will start on story 0.3 when asked |

**Decisions:** Ship commands are UCPH, CMPH, and SWAT.

**In:** story 0.3 when asked. One sentence for the behavior it adds. **Out:** starting it during this pause; production code before the red tests.

**Acceptance (met):** headless.

**Acceptance (not yet):** Red before production code. Unit: `Name_locks_the_value` — one sentence for the lock. Chain: `Chain: first → this` calls the real function and locks the value. Look once for a missing unit and a missing chain.

**Next steps:** 1) Branch `feature/0.3-topic`. 2) Red units. 3) Red chain. 4) One more missing unit and one more missing chain. 5) Then production code.

write thorough tests. red, integration, find missing ones of both kinds.

**Measured:** none this turn

**Filename:** `doc/handoff/0001-HANDOFF-YYYY-MM-DD_HHmm.md`

## Blank

Receiver brief (N.M closed, X.Y open)

**Objective:** You will start on story X.Y when asked.

**Git**

| | |
|--|--|
| **Story** | N.M `[x]`; X.Y `[ ]` |
| **Version** | public version, or `no bump` |
| **On** | `origin/main @ <sha>` or `origin/feature/… @ <sha>` (not merged) |
| **Do not** | re-merge, re-push, `git log` |
| **Next** | You will start on story X.Y when asked |

**Decisions:** (deltas this ship only)

**In:** (the contract this story adds). **Out:** (exclusions, including production code before the red tests).

**Acceptance (met):** headless, or the human-check result.

**Acceptance (not yet):** each red test once. Chain name and the calls. One look for a missing unit and a missing chain.

**Next steps:** 1) Branch. 2) Red units. 3) Red chain. 4) One more missing unit and one more missing chain. 5) Then production code.

write thorough tests. red, integration, find missing ones of both kinds.

**Measured:** counts versus the prior run, or `none this turn`

**Blockers:** omit this heading when there are none.

**Filename:** `doc/handoff/NNNN-HANDOFF-YYYY-MM-DD_HHmm.md`
