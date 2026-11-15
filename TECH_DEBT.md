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

- **Aisle (1) is 14.9.3 / 14.9.5**, human PASS 2026-11-13 and 2026-11-14. **14.19** overhire is closed on 1.17.13. A `$0/hr` row still counts as a person; the wage is **14.10**. **14.20** remains: `restock.summary ordered` was still 0 on the new game. The inherited `ops.skipped` is closed. **14.9.6** is closed.
- Day-roll blip: `store.health` can briefly show `customers=0 revenue=0 ready=false` (and a trailing `-100` profit) even after Ready days, then recover. The open **10.4** tree harvests this. Drop this line when that tree commits. It is not an Epic 14 story.
- Morning `g3` stays false after `cashier.replaced`. Day 1497 at 7th Avenue 22 served 82 customers and `g3` was still false. Sales do not flip the register flag. The live shift read is what misses the hired person.
- Day 1 `ops.verified` on **1.16.7.7** logged cleaner `shift=false person=false uniform=nopreset`. Uniforms stay accepted below. The missing cleaner shift belongs to the **10.4** fit-out, not Epic 14.

## Owned by a story

These left the backlog because a story now has the exit.

- Second stage-sim empire, stubbed close and tax: **14.13**, after the new decisions exist and before **10.6**.
- Tune scores **Stage2Cash** only: **14.1** narrows the rulebook. No tune-every-number story.
- Audit cash and daily cost stay 0: **14.4**.
- Skipped warehouse heals (slot, order, drain, plan): **10.5**.
- Playbook only sees open, money, and run: **14.2**, then each later story adds its own pick.
- Zero stores counts as Stage 4: **14.4**.
- Staff-plan write still unverified: **7.9**, and it blocks **14.10**.
- Close list still has warehouses: **14.6** (folds **7.13**).
- Capacity retries with no log line: **14.9**.
- Live contract burn is a planted number: **14.2**. Real fixture cap and the goods list: **14.3**.

## Accept for now

(Isolated + workaround + revisit trigger. No story.)

- `Telemetry.Record` reads the whole `ceo.log` on each day roll to find that day's summary and heals. Revisit when a day roll gets slow.
- The Epic 3 header says the mod CSV is diffed against the ledger CSV for the same day. Stories 3.3–3.5 lock the mod dump. The human check compared counts. Revisit if a later epic needs that diff as a test.
- `Close.Lock` only scores the layout street and family. `Buy.Lock` only runs after a close. Leave both locks. The close score, the payback, and the roadmap are separate decisions. The roadmap does not call them.
- Store opening does not seed `uniformsBySkill` or wait on `UniformLockerController`. The seed helpers stay in `StoreOps` / `StoreOpsBridge.Uniforms`. Revisit when a story needs customer uniform demand or a cashier in uniform.
- The eleven files in `doc/requirements/strategy/` are rules. Do not copy their sample C# into `src/`. `strategy.md` is the sentence that ships.

---

## ROI rubric (quick)

Score each: Impact (0–2) + Frequency (0–2) + RiskReduction (0–2) + Effort (0–2, reverse scale). Sort descending.

