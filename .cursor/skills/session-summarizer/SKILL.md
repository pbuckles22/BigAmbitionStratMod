---
name: session-summarizer
description: Leaving-agent protocol. Writes one short handoff note and a three-line chat pointer. Use on UCPH, CMPH, or session end.
---

# Session Summarizer — Leaving Agent Protocol

Use this skill when ending a session, handing work to a new agent, or closing **UCPH** / **CMPH**.

Follow [handoff-v2.mdc](../../rules/handoff-v2.mdc). That file is the rule. This skill does not restate it.

1. Write `doc/handoff/NNNN-HANDOFF-YYYY-MM-DD_HHmm.md` from [`.cursor/handoff/_template.md`](../../handoff/_template.md). **≤ 220 words.** One file. New `NNNN`.
2. Chat is the three lines in the rule. Do not paste the note.
3. Sync **On**, version, **Next**, and **Blockers** into `AGENT_HANDOFF.md` → *Current state*.

Do not run the review swarm on UCPH or CMPH ([wrap-on-command.mdc](../../rules/wrap-on-command.mdc)). Gates stay on SWAT ([handoff-checklist.mdc](../../rules/handoff-checklist.mdc)).

## Exit check

- The note is ≤ 220 words and each fact appears once.
- Chat is the three-line pointer, and `NNNN` is unused.
- *Current state* matches **On**, **Next**, and **Blockers**.
- Counts in **Measured** are real.
