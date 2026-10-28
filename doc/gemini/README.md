# Gemini drop

Transient pack for an external LLM. **Not canonical.** Live source wins.

Tracked: this README only. Pack files, `dropzone/` contents, and `Gemini_Snapshot.txt` are gitignored.

Rules: [.cursor/rules/gemini-handoff.mdc](../../.cursor/rules/gemini-handoff.mdc).

Full-repo dump: `script/Export-GeminiSnapshot.ps1` (output is gitignored at the repo root).

Save dump: `python script/Export-SaveGameText.py` when they say **saved game to text**. Input is the `.hsg` in `dropzone/`. Output is gitignored `empire_summary.md` in this folder.
