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

- `StageSim.cs` (~780 lines) embeds a second headless empire (`World`) beside `DayStep` / `Calibrate`. It drives Epic 9 decides but not close, run, audit, marketing, or time-advance. Split `World` or share one step kernel before Epic **10** growth makes the two paths drift.
- `StageSim.Tune` only scores caller-supplied `StageThresholdOption` bags. `strategy.md` still says tune every **named** threshold with a Tier 1 lock per tuned value; chains only exercise **Stage2Cash** climb vs tight. Add an owner-facing search/grid or narrow the rulebook text before **10.1** applies tuned numbers live.

## Accept for now

(Isolated + workaround + revisit trigger.)

- `Telemetry.Record` reads the whole `ceo.log` on each day roll to find that day's summary and heals. Revisit when a day roll gets slow.
- The live audit calls the game for store connects only. Slot, order, drain, and plan heals are counted and skipped so a missing game call does not pause the day. Cash and the build pin are not read from the save, so those two summary fields stay 0. Revisit when story **10.2** runs the rest of the heals.
- The Epic 3 header says the mod CSV is diffed against the ledger CSV for the same day. Stories 3.3–3.5 lock the mod dump. The human check compared counts. Revisit if a later epic needs that diff as a test.
- `Close.Lock` only scores the layout street and family. `Buy.Lock` only runs after a close. Leave both locks. The close score, the payback, and the roadmap are separate decisions. The roadmap does not call them.
- Store opening does not seed `uniformsBySkill` or wait on `UniformLockerController`. The seed helpers stay in `StoreOps` / `StoreOpsBridge.Uniforms`. Revisit when a story needs customer uniform demand or a cashier in uniform.
- `CapacityBridge.AlreadyMatched` is untested on the production path; tests plant `alreadyMatched` instead. `TryCapacity` retries every frame with no evidence when `Live` fails. Revisit when capacity miss diagnosis hurts.
- `Stage.Decide` treats `suppliedStores == goodsStores` as true when both are 0, so cash ≥ **Stage4Cash** with zero stores and zero empty slots returns Stage 4 (locked in `StageTests`). Revisit before **10.2** if a brand-new or fully liquidated save should stay Stage 1 Bootstrap.
- Staff plan write (**7.9**) is still *Unverified* in `game-api.md`, and cost-center / FixDays / RedDays close filters (**7.13**) are stashed. Finish **7.13** + **7.9**, or fold **7.13**’s pure filter into **9.5**, before the live **10.2** Autopilot loop.
- `LiveDump.NextPlaybook` only feeds open, money, and run into `Playbook.Decide` (other picks stay `None()`). Enough for a new-game F9 step. Epic **8** stage-sim is headless only. Revisit when **10.2** needs managers/supply/close on the live board.
- `StageSim.World` stubs wholesaler supply (`Contracted` without full refill/contract drop), fixed wage/rent in `DailyCost`, and `ClosePick.None()` / `RunPick.None()` every day. Fine for **8.6** median/worst scoring. Revisit when the live autopilot must match strategy.md stages 3–4 and continuous play.

---

## ROI rubric (quick)

Score each: Impact (0–2) + Frequency (0–2) + RiskReduction (0–2) + Effort (0–2, reverse scale). Sort descending.

