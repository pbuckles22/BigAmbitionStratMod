## Technical debt (tracked backlog)

This is the durable home for technical debt across sessions. Handoff notes can mention debt, but anything that persists should be recorded here.

### Cadence

- **Every handoff**: run the tech-debt-evaluator skill and record “Do first” items in the handoff note.
- **Promote persistent debt**: if a “Do first” item persists across 2+ handoffs (or blocks work), add it here and rank it.

---

## Fix now

(Blocking, unsafe, or no-rollback debt.)

- (none)

## Fix soon

(High ROI; frequent pain; not blocking.)

- (none)

## Accept for now

(Isolated + workaround + revisit trigger.)

- The Epic 3 header says the mod CSV is diffed against the ledger CSV for the same day. Stories 3.3–3.5 lock the mod dump. The human check compared counts. Revisit if a later epic needs that diff as a test.
- `Advice.Record` writes `advice.shown`. The running game does not call it. Revisit at story **6.1**, which draws the window and writes that line.
- `Overlay.Build` returns rows and draws nothing. Revisit at story **6.1**.
- `Close.Lock` only scores the layout street and family. `Buy.Lock` only runs after a close. Leave both locks. New decisions are **6.3** and **6.5**.

---

## ROI rubric (quick)

Score each: Impact (0–2) + Frequency (0–2) + RiskReduction (0–2) + Effort (0–2, reverse scale). Sort descending.

