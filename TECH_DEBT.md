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

- `Telemetry.Record` reads the whole `ceo.log` on each day roll to find that day's summary and heals. Revisit when a day roll gets slow.
- The live audit calls the game for store connects only. Slot, order, drain, and plan heals are counted and skipped so a missing game call does not pause the day. Cash and the build pin are not read from the save, so those two summary fields stay 0. Revisit when story **10.2** runs the rest of the heals.
- The Epic 3 header says the mod CSV is diffed against the ledger CSV for the same day. Stories 3.3–3.5 lock the mod dump. The human check compared counts. Revisit if a later epic needs that diff as a test.
- `Close.Lock` only scores the layout street and family. `Buy.Lock` only runs after a close. Leave both locks. The close score, the payback, and the roadmap are separate decisions. The roadmap does not call them.
- Store opening does not seed `uniformsBySkill` or wait on `UniformLockerController`. The seed helpers stay in `StoreOps` / `StoreOpsBridge.Uniforms`. Revisit when a story needs customer uniform demand or a cashier in uniform.
- `CapacityBridge.AlreadyMatched` is untested on the production path; tests plant `alreadyMatched` instead. `TryCapacity` retries every frame with no evidence when `Live` fails. Revisit when capacity miss diagnosis hurts.

---

## ROI rubric (quick)

Score each: Impact (0–2) + Frequency (0–2) + RiskReduction (0–2) + Effort (0–2, reverse scale). Sort descending.

