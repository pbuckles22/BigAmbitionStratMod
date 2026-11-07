"""Progress report: one telemetry row per day becomes one report line."""

from __future__ import annotations

import importlib.util
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
PROGRESS_PATH = REPO / "script" / "Report-Progress.py"


def _load(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


progress = _load("report_progress", PROGRESS_PATH)

HEADER = (
    "day,cash,netWorth,profit,stage,unconnected,emptySlots,dry,mixed,unstaffed,"
    "missingPlans,cashBelowReserve,pinMismatch,healed,actions"
)


def _csv(*rows: str) -> str:
    return HEADER + "\n" + "\n".join(rows) + "\n"


def test_progress_report_lists_each_day():
    text = progress.render_progress(
        progress.read_telemetry_text(
            _csv(
                "1016,1000,10,7,1,2,1,0,0,0,0,0,0,1,connect",
                "1017,1100,20,0,1,1,1,0,0,0,0,0,0,0,",
                "1018,1200,30,-41,2,0,0,0,0,0,0,0,0,0,",
            )
        )
    )
    assert "Latest day 1018." in text
    assert "Cash 1200." in text
    assert "Net worth 30." in text
    assert "Profit -41." in text
    assert "Stage 2." in text
    assert "unconnected 0. empty slots 0. dry 0. mixed 0. unstaffed 0. missing plans 0. cash below reserve 0. pin mismatch 0. healed 0." in text
    assert text.count("\n| 101") == 3
    assert "| 1016 | 1000 | 10 | 7 | 1 | 1 | connect |" in text
    assert "| 1018 | 1200 | 30 | -41 | 2 | 0 |  |" in text


def test_progress_report_keeps_a_loss():
    text = progress.render_progress(
        progress.read_telemetry_text(_csv("1016,10,20,-41,1,0,0,0,0,0,0,0,0,0,"))
    )
    assert "Profit -41." in text
    assert "| 1016 | 10 | 20 | -41 | 1 | 0 |  |" in text
    assert "+41" not in text


def test_progress_report_with_no_days_names_none(tmp_path: Path):
    csv_path = tmp_path / "telemetry.csv"
    report_path = tmp_path / "progress.md"
    log_path = tmp_path / "ceo.log"
    csv_path.write_text(HEADER + "\n", encoding="utf-8")

    count = progress.write_progress(csv_path, report_path, log_path)
    text = report_path.read_text(encoding="utf-8")
    log = log_path.read_text(encoding="utf-8")

    assert count == 0
    assert "No days yet." in text
    assert log == f"progress.written version={progress.VERSION} days=0 latest= profit=\n"


def test_progress_write_locks_the_latest_profit(tmp_path: Path):
    """The written report uses the last telemetry row."""
    csv_path = tmp_path / "telemetry.csv"
    report_path = tmp_path / "progress.md"
    log_path = tmp_path / "ceo.log"
    csv_path.write_text(
        "\ufeff"
        + _csv(
            "1016,1000,10,7,1,9,8,3,1,4,5,6,0,1,connect",
            "1017,11,21,4,2,0,0,0,0,0,0,0,0,0,",
        ),
        encoding="utf-8",
    )

    count = progress.write_progress(csv_path, report_path, log_path)
    rows = progress.read_telemetry(csv_path)
    text = report_path.read_text(encoding="utf-8")
    log = log_path.read_text(encoding="utf-8")

    assert count == 2
    assert len(rows) == 2
    assert "Latest day 1017." in text
    assert f"Profit {rows[-1]['profit']}." in text
    assert "| 1016 | 1000 | 10 | 7 | 1 | 1 | connect |" in text
    assert (
        f"progress.written version={progress.VERSION} days=2 latest=1017 profit=4\n" == log
    )
