"""Saved-game-to-text names the bottom business from the rank report."""

from __future__ import annotations

import importlib.util
from pathlib import Path

import pytest

REPO = Path(__file__).resolve().parents[1]
EXPORT_PATH = REPO / "script" / "Export-SaveGameText.py"
SLICE_PATH = REPO / "script" / "Slice-SaveGame.py"

KABOBS_REPORT = """# Rank report

## Bottom 10 by profit

| business | family | street | number | profit |
| --- | --- | --- | --- | --- |
| Chaotic Hair #5 | service | ba:street_fifthavenue | 38 | 14484.6098 |
| Chaotic Kabobs #5 | food | ba:street_firstavenue | 5 | 898.6715 |

## Top offices

None.
"""

LAST_ROW_REPORT = """# Rank report

## Bottom 10 by profit

| business | family | street | number | profit |
| --- | --- | --- | --- | --- |
| Chaotic Hair #5 | service | ba:street_fifthavenue | 38 | 10 |
| Only Shop | food | ba:street_a | 1 | 1 |

## Top offices

None.
"""

EMPTY_REPORT = """# Rank report

## Bottom 10 by profit

None.

## Top offices

None.
"""


def _load(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


export = _load("export_save_text", EXPORT_PATH)
slice_save = _load("slice_save_text", SLICE_PATH)
ledger = _load("statement_ledger_save_text", REPO / "script" / "statement_ledger.py")


def test_IndexReuse_keeps_the_sqlite(tmp_path):
    save = slice_save.default_save()
    db_path = slice_save.CACHE / "save-index.sqlite"
    assert slice_save.index_is_current(save)
    before = db_path.stat().st_mtime_ns
    ledger.write_statements(save, tmp_path / "statements.csv", tmp_path / "ledger.log")
    assert db_path.is_file()
    assert db_path.stat().st_mtime_ns == before


def test_IndexReuse_a_different_save_is_not_current(tmp_path):
    other = tmp_path / "other.hsg"
    other.write_bytes(b"not a save")
    assert slice_save.index_is_current(other) is False


def test_BlobReuse_a_renamed_save_is_not_the_cached_blob():
    assert (
        slice_save.reuse_blob(
            "Recover #1 (Modded).hsg",
            "New chaotic Save Game.hsg",
            10.0,
            10.0,
        )
        is False
    )


def test_BlobReuse_an_older_blob_is_not_reused():
    assert (
        slice_save.reuse_blob(
            "Recover #1 (Modded).hsg",
            "Recover #1 (Modded).hsg",
            9.0,
            10.0,
        )
        is False
    )


def test_BlobReuse_keeps_the_same_save_when_the_blob_is_newer():
    assert (
        slice_save.reuse_blob(
            "Recover #1 (Modded).hsg",
            "Recover #1 (Modded).hsg",
            10.0,
            10.0,
        )
        is True
    )


def test_SaveText_locks_the_bottom_business():
    text = export.render_export("save.hsg", KABOBS_REPORT)
    assert "Bottom business: Chaotic Kabobs #5" in text


def test_SaveText_names_the_last_row():
    text = export.render_export("save.hsg", LAST_ROW_REPORT)
    assert "Bottom business: Only Shop" in text
    assert "Bottom business: Chaotic Hair #5" not in text


def test_SaveText_an_empty_table_names_nobody():
    text = export.render_export("save.hsg", EMPTY_REPORT)
    assert "Bottom business: none" in text
    assert "Kabobs" not in text


@pytest.fixture(scope="module")
def exported(tmp_path_factory):
    save = slice_save.default_save()
    db_path = slice_save.CACHE / "save-index.sqlite"
    before = db_path.stat().st_mtime_ns
    folder = tmp_path_factory.mktemp("savetext")
    out = folder / "empire_summary.md"
    export.main([str(save), "-o", str(out)])
    after = db_path.stat().st_mtime_ns
    report = (slice_save.CACHE / "ledger" / "REPORT.md").read_text(encoding="utf-8")
    log_text = (slice_save.CACHE / "ledger.log").read_text(encoding="utf-8")
    return out, report, before, after, log_text


def test_chain_export_locks_the_bottom_business(exported):
    """Chain: gunzip → index → statement → portfolio → routes → report"""
    out, report, _before, _after, _log_text = exported
    summary = out.read_text(encoding="utf-8")
    assert export.bottom_business(report) == "Chaotic Kabobs #5"
    assert "Bottom business: Chaotic Kabobs #5" in summary


def test_chain_export_keeps_the_index(exported):
    """Chain: gunzip → index → statement → portfolio → routes → report kept"""
    _out, _report, before, after, log_text = exported
    assert after == before
    assert "savetext.written version=1.2.2 bottom=Chaotic Kabobs #5" in log_text
