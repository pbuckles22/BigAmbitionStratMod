"""Statement rows: bytes after TotalProfit become a street."""

from __future__ import annotations

import importlib.util
from pathlib import Path

import pytest

REPO = Path(__file__).resolve().parents[1]
FIXTURE = REPO / "tests" / "fixtures" / "statement_after_totalprofit.bin"
BROADWAY = REPO / "tests" / "fixtures" / "statement_broadway.bin"
LEDGER_PATH = REPO / "script" / "statement_ledger.py"


def _load(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


ledger = _load("statement_ledger", LEDGER_PATH)
slice_save = _load("slice_save", REPO / "script" / "Slice-SaveGame.py")


def test_StatementFixture_locks_profit_and_street():
    data = FIXTURE.read_bytes()
    rows = ledger.rows_between(data, 0, len(data))
    assert len(rows) == 1
    assert rows[0]["street"] == "ba:street_firstavenue"
    assert rows[0]["number"] == 38
    assert rows[0]["profit"] == pytest.approx(69652.21875)


def test_StatementFixture_locks_a_longer_street_name():
    data = BROADWAY.read_bytes()
    rows = ledger.rows_between(data, 0, len(data))
    assert len(rows) == 1
    assert rows[0]["street"] == "ba:street_broadwaystreet"
    assert rows[0]["number"] == 3
    assert rows[0]["profit"] == pytest.approx(-12097.953125)


@pytest.fixture(scope="module")
def statement_csv(tmp_path_factory):
    folder = tmp_path_factory.mktemp("ledger")
    csv_path = folder / "statements.csv"
    log_path = folder / "ledger.log"
    ledger.write_statements(slice_save.default_save(), csv_path, log_path)
    return csv_path, log_path


def _rows(csv_path: Path) -> list[dict[str, str]]:
    import csv

    with csv_path.open(encoding="utf-8", newline="") as handle:
        return list(csv.DictReader(handle))


def test_chain_gunzip_index_statement(statement_csv):
    """Chain: gunzip → index → statement"""
    csv_path, log_path = statement_csv
    rows = _rows(csv_path)
    row = rows[0]
    assert row["street"] == "ba:street_firstavenue"
    assert int(row["number"]) == 38
    assert float(row["profit"]) == pytest.approx(69652.21875)
    assert int(row["day"]) == 953
    assert float(row["sales"]) == pytest.approx(85570)
    text = log_path.read_text(encoding="utf-8")
    assert "statements.written" in text
    assert "ba:street_firstavenue" in text


def test_chain_gunzip_index_next_statement(statement_csv):
    """Chain: gunzip → index → next statement"""
    csv_path, _log_path = statement_csv
    rows = _rows(csv_path)
    row = rows[1]
    assert row["street"] == "ba:street_firstavenue"
    assert int(row["number"]) == 5
    assert float(row["profit"]) == pytest.approx(-131)
    assert float(row["sales"]) == pytest.approx(0)
    assert int(row["day"]) == 953
    rolled = next(index for index, item in enumerate(rows) if int(item["day"]) == 954)
    assert int(rows[rolled - 1]["day"]) == 953
