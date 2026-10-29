#!/usr/bin/env python3
"""Turn a Big Ambitions .hsg into markdown by running the ledger through the report.

Say "saved game to text". This script is what that runs.

  python script/Export-SaveGameText.py
  python script/Export-SaveGameText.py "path/to/save.hsg"
  python script/Export-SaveGameText.py "path/to/save.hsg" -o doc/gemini/empire_summary.md

Default input is the only .hsg in doc/gemini/dropzone/.
Default output is doc/gemini/empire_summary.md (gitignored).

The export runs statements, portfolio, routes, and the rank report, then
names the bottom business. A current save index is kept.
"""

from __future__ import annotations

import argparse
import importlib.util
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_DROPZONE = REPO_ROOT / "doc" / "gemini" / "dropzone"
DEFAULT_OUTPUT = REPO_ROOT / "doc" / "gemini" / "empire_summary.md"


def resolve_save(explicit: str | None) -> Path:
    if explicit:
        path = Path(explicit)
        if not path.is_file():
            raise SystemExit(f"Save not found: {path}")
        return path

    saves = sorted(DEFAULT_DROPZONE.glob("*.hsg"))
    if len(saves) == 1:
        return saves[0]
    if not saves:
        raise SystemExit(
            f"No .hsg in {DEFAULT_DROPZONE}. Pass a path, or drop one file there."
        )
    names = "\n".join(f"  {p.name}" for p in saves)
    raise SystemExit(f"More than one .hsg in {DEFAULT_DROPZONE}. Pass a path:\n{names}")


def _ledger():
    path = Path(__file__).with_name("statement_ledger.py")
    spec = importlib.util.spec_from_file_location("statement_ledger_export", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def bottom_business(report_text: str) -> str:
    """Last business on the Bottom 10 table. Empty when that table has no rows."""
    marker = "## Bottom 10 by profit"
    if marker not in report_text:
        return ""
    section = report_text.split(marker, 1)[1].split("\n## ", 1)[0]
    names = []
    for line in section.splitlines():
        if not line.startswith("| "):
            continue
        cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
        if not cells or cells[0] == "business" or set(cells[0]) <= {"-"}:
            continue
        names.append(cells[0])
    return names[-1] if names else ""


def render_export(save_name: str, report_text: str) -> str:
    """Saved-game text. The named business is the last row of the rank report."""
    bottom = bottom_business(report_text) or "none"
    body = report_text if report_text.endswith("\n") else report_text + "\n"
    return f"# Saved game\n\nSource: `{save_name}`\n\nBottom business: {bottom}\n\n{body}"


def export_save(save: Path, out_path: Path) -> str:
    """Run the ledger through the report and write the saved-game text."""
    ledger = _ledger()
    report_text = ledger.run(save)
    bottom = bottom_business(report_text)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    with out_path.open("w", encoding="utf-8", newline="\n") as handle:
        handle.write(render_export(save.name, report_text))
    log_path = ledger.SLICE.CACHE / "ledger.log"
    log_path.parent.mkdir(parents=True, exist_ok=True)
    with log_path.open("a", encoding="utf-8") as handle:
        handle.write(f"savetext.written version={ledger.VERSION} bottom={bottom}\n")
    print(f"Done. {save.name} -> {out_path}")
    print(f"bottom={bottom}")
    return bottom


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Run the ledger through the report and write saved-game text.")
    parser.add_argument("save", nargs="?", help="Path to an .hsg. Default: the only file in doc/gemini/dropzone/.")
    parser.add_argument("-o", "--output", default=str(DEFAULT_OUTPUT), help="Markdown output path.")
    args = parser.parse_args(argv)

    save = resolve_save(args.save)
    out_path = Path(args.output)
    if not out_path.is_absolute():
        out_path = REPO_ROOT / out_path
    export_save(save, out_path)
    return 0


if __name__ == "__main__":
    sys.exit(main())
