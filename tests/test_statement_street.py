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


def test_chain_gunzip_index_statement_portfolio(statement_csv, tmp_path):
    """Chain: gunzip → index → statement → portfolio"""
    csv_path, log_path = statement_csv
    statements = _rows(csv_path)
    portfolio_path = tmp_path / "portfolio.csv"
    ledger.write_portfolio(csv_path, portfolio_path, log_path)
    portfolio = _rows(portfolio_path)
    first = statements[0]
    match = next(
        row
        for row in portfolio
        if row["street"] == first["street"] and row["number"] == first["number"]
    )
    assert match["business"] == "Chaotic Clothing #3"
    text = log_path.read_text(encoding="utf-8")
    assert "portfolio.written" in text
    assert "Chaotic Clothing #3" in text


def test_chain_gunzip_index_statement_portfolio_routes(statement_csv, tmp_path):
    """Chain: gunzip → index → statement → portfolio → routes"""
    csv_path, log_path = statement_csv
    portfolio_path = tmp_path / "portfolio.csv"
    routes_path = tmp_path / "routes.csv"
    warehouses_path = tmp_path / "warehouses.csv"
    ledger.write_portfolio(csv_path, portfolio_path, log_path)
    ledger.write_routes(portfolio_path, routes_path, warehouses_path, log_path)
    routes = _rows(routes_path)
    assert len(routes) == 4
    match = next(row for row in routes if row["item"] == "ba:itemname_cheapgift")
    assert match["warehouse"] == "ba:street_firststreet 37"
    assert match["destination"] == "ba:street_fifthavenue 36"
    assert match["day"] == "1016"
    text = log_path.read_text(encoding="utf-8")
    assert "routes.written" in text
    assert "portfolio=17" in text


def test_chain_gunzip_index_statement_portfolio_routes_hold(statement_csv, tmp_path):
    """Chain: gunzip → index → statement → portfolio → routes hold"""
    csv_path, log_path = statement_csv
    portfolio_path = tmp_path / "portfolio.csv"
    routes_path = tmp_path / "routes.csv"
    warehouses_path = tmp_path / "warehouses.csv"
    ledger.write_portfolio(csv_path, portfolio_path, log_path)
    ledger.write_routes(portfolio_path, routes_path, warehouses_path, log_path)
    held = _rows(warehouses_path)
    match = next(
        row
        for row in held
        if row["warehouse"] == "W1" and row["item"] == "ba:itemname_sodacan"
    )
    assert match["street"] == "ba:street_fourthstreet"
    assert match["number"] == "46"
    assert match["amount"] == "45"


def test_chain_gunzip_index_statement_portfolio_span(statement_csv, tmp_path):
    """Chain: gunzip → index → statement → portfolio span"""
    csv_path, log_path = statement_csv
    portfolio_path = tmp_path / "portfolio.csv"
    ledger.write_portfolio(csv_path, portfolio_path, log_path)
    portfolio = _rows(portfolio_path)
    match = next(row for row in portfolio if row["business"] == "Chaotic Hair #8")
    assert match["family"] == "service"
    assert match["street"] == "ba:street_fifthavenue"
    assert int(match["number"]) == 37
    assert float(match["tco"]) == pytest.approx(4003.3822)


def _section(text: str, start: str, end: str) -> str:
    body = text.split(f"## {start}", 1)[1]
    if end == "":
        return body
    return body.split(f"## {end}", 1)[0]


def _table_rows(section: str) -> list[list[str]]:
    rows = []
    for line in section.splitlines():
        if not line.startswith("| "):
            continue
        cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
        if not cells or cells[0].startswith("-") or cells[0] in {"business", "street", "warehouse"}:
            continue
        rows.append(cells)
    return rows


def test_chain_gunzip_index_statement_seven_day_sales(statement_csv):
    """Chain: gunzip → index → statement → seven-day sales"""
    csv_path, _log_path = statement_csv
    statements = _rows(csv_path)
    hair = ledger.sales_between(statements, "ba:street_fifthavenue", 38, 1007, 1013)
    kabobs = ledger.sales_between(statements, "ba:street_firstavenue", 5, 1007, 1013)
    assert hair == pytest.approx(143251)
    assert kabobs == pytest.approx(72610.35)


def test_chain_gunzip_index_statement_portfolio_routes_report(statement_csv, tmp_path):
    """Chain: gunzip → index → statement → portfolio → routes → report"""
    csv_path, log_path = statement_csv
    portfolio_path = tmp_path / "portfolio.csv"
    routes_path = tmp_path / "routes.csv"
    warehouses_path = tmp_path / "warehouses.csv"
    report_path = tmp_path / "REPORT.md"
    ledger.write_portfolio(csv_path, portfolio_path, log_path)
    ledger.write_routes(portfolio_path, routes_path, warehouses_path, log_path)
    ledger.write_report(csv_path, portfolio_path, warehouses_path, report_path, log_path)
    text = report_path.read_text(encoding="utf-8")
    rows = _table_rows(_section(text, "Bottom 10 by profit", "Top offices"))
    assert rows[-1][0] == "Chaotic Kabobs #5"
    assert rows[-1][4] == "898.6715"
    logged = log_path.read_text(encoding="utf-8")
    assert "report.written" in logged
    assert "bottom=Chaotic Kabobs #5" in logged
    assert "profit=898.6715" in logged


def test_chain_gunzip_index_statement_portfolio_routes_report_mix(statement_csv, tmp_path):
    """Chain: gunzip → index → statement → portfolio → routes → report mix"""
    csv_path, log_path = statement_csv
    portfolio_path = tmp_path / "portfolio.csv"
    routes_path = tmp_path / "routes.csv"
    warehouses_path = tmp_path / "warehouses.csv"
    report_path = tmp_path / "REPORT.md"
    ledger.write_portfolio(csv_path, portfolio_path, log_path)
    ledger.write_routes(portfolio_path, routes_path, warehouses_path, log_path)
    ledger.write_report(csv_path, portfolio_path, warehouses_path, report_path, log_path)
    text = report_path.read_text(encoding="utf-8")
    offices = _table_rows(_section(text, "Top offices", "Streets with 3+ of one family"))
    assert offices[0][0] == "Chaotic Devs"
    streets = _table_rows(_section(text, "Streets with 3+ of one family", "Warehouses that mix families"))
    assert ["ba:street_fifthavenue", "service", "4"] in streets
    assert ["ba:street_fourthavenue", "food", "3"] in streets
    assert all(row[0] != "ba:street_firstavenue" or row[1] != "durables" for row in streets)
    mixes = _table_rows(_section(text, "Warehouses that mix families", ""))
    w6 = next(row for row in mixes if row[0] == "W6")
    assert w6[1] == "ba:street_fifthavenue"
    assert w6[2] == "43"
    assert w6[3] == "food, service"
    assert all(row[0] != "W1" for row in mixes)
