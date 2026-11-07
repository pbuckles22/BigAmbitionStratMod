"""Progress report from telemetry.csv. One row per day."""

from __future__ import annotations

import csv
import io
import os
import sys
from pathlib import Path

VERSION = "1.12.4"


def read_telemetry_text(text: str) -> list[dict[str, str]]:
    return list(csv.DictReader(io.StringIO(text)))


def read_telemetry(path: Path) -> list[dict[str, str]]:
    return read_telemetry_text(path.read_text(encoding="utf-8-sig"))


def render_progress(rows: list[dict[str, str]]) -> str:
    if not rows:
        return "# Progress\n\nNo days yet.\n"

    latest = rows[-1]
    lines = [
        "# Progress",
        "",
        (
            f"Latest day {latest.get('day', '')}. "
            f"Cash {latest.get('cash', '')}. "
            f"Net worth {latest.get('netWorth', '')}. "
            f"Profit {latest.get('profit', '')}. "
            f"Stage {latest.get('stage', '')}."
        ),
        "",
        "## Days",
        "",
        "| day | cash | net worth | profit | stage | healed | actions |",
        "| --- | --- | --- | --- | --- | --- | --- |",
    ]
    for row in rows:
        lines.append(
            "| {day} | {cash} | {net} | {profit} | {stage} | {healed} | {actions} |".format(
                day=row.get("day", ""),
                cash=row.get("cash", ""),
                net=row.get("netWorth", ""),
                profit=row.get("profit", ""),
                stage=row.get("stage", ""),
                healed=row.get("healed", ""),
                actions=row.get("actions", ""),
            )
        )
    lines.extend(
        [
            "",
            "## Health",
            "",
            (
                f"unconnected {latest.get('unconnected', '')}. "
                f"empty slots {latest.get('emptySlots', '')}. "
                f"dry {latest.get('dry', '')}. "
                f"mixed {latest.get('mixed', '')}. "
                f"unstaffed {latest.get('unstaffed', '')}. "
                f"missing plans {latest.get('missingPlans', '')}. "
                f"cash below reserve {latest.get('cashBelowReserve', '')}. "
                f"pin mismatch {latest.get('pinMismatch', '')}. "
                f"healed {latest.get('healed', '')}."
            ),
            "",
        ]
    )
    return "\n".join(lines)


def write_progress(csv_path: Path, report_path: Path, log_path: Path) -> int:
    rows = read_telemetry(csv_path)
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(render_progress(rows), encoding="utf-8")
    latest = rows[-1] if rows else {}
    line = (
        f"progress.written version={VERSION} days={len(rows)} "
        f"latest={latest.get('day', '')} profit={latest.get('profit', '')}\n"
    )
    log_path.parent.mkdir(parents=True, exist_ok=True)
    with log_path.open("a", encoding="utf-8") as handle:
        handle.write(line)
    return len(rows)


def main(argv: list[str] | None = None) -> int:
    del argv
    root = Path(os.environ["LOCALAPPDATA"]) / "ChaoticCEO"
    count = write_progress(root / "telemetry.csv", root / "progress.md", root / "ceo.log")
    print(f"version {VERSION}")
    print(f"Wrote {count} days to {root / 'progress.md'}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
