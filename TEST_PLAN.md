# Test plan (TEST_PLAN.md)

Define **Tier 1** (fast feedback: unit, headless, or local) and **Tier 2** (integration, browser, device, or E2E) for your stack. Replace the placeholders below.

---

## Tier 1: Fast feedback

```bash
# Example: npm test
# Example: pytest -q
```

---

## Tier 2: Integration / E2E

Use when behavior spans a real runtime (browser, device, network, or native APIs).

```bash
# Example: npm run test:e2e
# Example: playwright test
```

---

**Handoff:** Document the exact commands you use for coverage in AGENT_HANDOFF.md so agents can run them consistently.

**Compile counter:** If Tier 1 **compiles**, count every compile (test/Debug **and** Release). Non-compiling stacks skip. See `.cursor/rules/testing.mdc`.

**Evidence sink:** If the product has a runtime, document the **file path** for named evidence lines here (human opens it; agent Reads it). Debugger-only Trace is not evidence. This template has no runtime — skip.

**Long run:** A batch or soak is not Tier 1. When it trips a gate, extract that decision and add a named Tier 1 test before the next long run. See `.cursor/rules/testing.mdc`.
