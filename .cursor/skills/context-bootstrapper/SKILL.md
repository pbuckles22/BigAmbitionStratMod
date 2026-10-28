---
name: context-bootstrapper
description: Receiving-agent protocol. Reads the named handoff note once and stops at its Next step. Use when starting work, resuming, or when context is bloated.
---

# Context Bootstrapper — Receiving Agent Protocol

Use this skill when starting work on this repo, resuming after a break, or when context feels bloated.

Goal: the next step in the latest note, and no second brief.

## Read

1. `.cursor/rules/always.mdc` (already in context when it is always-on).
2. `AGENT_HANDOFF.md` → *Current state* only, then *Run and test* if you will run something. Skip the rest.
3. `PM_PLAN.md` for the story named in **Next**. Skip other epics.
4. The handoff file the chat names. If the chat has no path, open the highest `NNNN` in `doc/handoff/` only.

Read that note once. Do not open older notes. Do not open a `.cursor/handoff/` twin. If the chat already pasted a full legacy brief, that paste is the note — do not also open the file.

Then read only the files you will edit.

## Act

Trust **On**. Start the story in **Next** only when the human asks. The first edits are the red tests under **Acceptance (not yet)**. Watch them fail before production code.

`git status` is for a dirty tree before an edit. Re-check git when **On** is missing, says not merged, or the user asked whether it landed.

If *Current state* and the note disagree on **On**, the tree you have checked out wins. Ask.

Do not echo the brief. Do not write a new handoff unless this turn is UCPH, CMPH, or SWAT.
