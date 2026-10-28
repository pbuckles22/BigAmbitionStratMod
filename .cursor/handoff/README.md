# Handoff notes

**Committed in this repo:** `_template.md` and this README — no secrets.

**Session narratives (usually gitignored):**

- **`doc/handoff/NNNN-HANDOFF-YYYY-MM-DD_HHmm.md`** — the note. Shape: [`_template.md`](_template.md). Rule: [`.cursor/rules/handoff-v2.mdc`](../rules/handoff-v2.mdc). **≤ 220 words.**

Chat is three lines naming that file. Do not paste the note. Do not write a second copy here. Gates (PASS/WARN) are SWAT only.

**Naming rules (do not overwrite history):**

- **`NNNN`**: zero-padded monotonic serial (**0001**, **0002**, …) that always increases.
- **`YYYY-MM-DD_HHmm`**: local timestamp in 24h time.
- **Never reuse** an `NNNN` and **never edit** a previous handoff file in place—append a new file.

Older files under `.cursor/handoff/*-handoff-*.md` are history. Do not add new ones.

**Pick the next serial (PowerShell, repo root):**

```powershell
$dir = 'doc/handoff'
$next = 1
Get-ChildItem -LiteralPath $dir -Filter '*-HANDOFF-*.md' -File -ErrorAction SilentlyContinue |
  ForEach-Object {
    if ($_.Name -match '^(\d{4})-HANDOFF-') {
      [int]$n = $matches[1]
      if ($n -ge $next) { $next = $n + 1 }
    }
  }
'{0:0000}-HANDOFF-YYYY-MM-DD_HHmm.md' -f $next
```

See [`.gitignore`](../.gitignore). If you need a **tracked** contributor guide under `doc/handoff/`, add the file and use a `.gitignore` exception (`!doc/handoff/YourFile.md`) so it is not ignored by the `HANDOFF-*` pattern.

**Product state on GitHub:** [doc/PROJECT_STATUS.md](../../doc/PROJECT_STATUS.md), [PM_PLAN.md](../../PM_PLAN.md), [AGENT_HANDOFF.md](../../AGENT_HANDOFF.md) → *Current state*. **Norm:** [CONTRIBUTING.md](../../CONTRIBUTING.md). Promote session decisions to tracked docs before merge.

Ship cadence is `.cursor/rules/wrap-on-command.mdc`. The review swarm (`.cursor/rules/handoff-checklist.mdc`) runs on SWAT, not on every UCPH or CMPH.
