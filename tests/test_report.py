"""Rank report: the lowest profit is the bottom row."""

from __future__ import annotations

import importlib.util
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
LEDGER_PATH = REPO / "script" / "statement_ledger.py"


def _load(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


ledger = _load("statement_ledger_report", LEDGER_PATH)


def _section(text: str, start: str, end: str) -> str:
    body = text.split(f"## {start}", 1)[1]
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


def _profit(business: str, family: str, street: str, number: int, profit: float) -> dict:
    return {
        "business": business,
        "family": family,
        "street": street,
        "number": number,
        "profit": profit,
    }


def test_BizManGross_locks_seven_day_sales():
    statements = [
        {"street": "ba:street_fifthavenue", "number": 38, "day": 1007, "sales": 100, "profit": 1},
        {"street": "ba:street_fifthavenue", "number": 38, "day": 1008, "sales": 40, "profit": 9},
        {"street": "ba:street_fifthavenue", "number": 38, "day": 1006, "sales": 1000, "profit": 50},
        {"street": "ba:street_firstavenue", "number": 5, "day": 1007, "sales": 7, "profit": 3},
    ]
    total = ledger.sales_between(statements, "ba:street_fifthavenue", 38, 1007, 1013)
    assert total == 140


def test_ReportFixture_locks_the_bottom_profit():
    profits = [
        _profit("High", "food", "ba:street_a", 1, 100),
        _profit("Low", "food", "ba:street_b", 2, -40),
        _profit("Mid", "service", "ba:street_c", 3, 5),
    ]
    text = ledger.render_report(profits, [], [])
    rows = _table_rows(_section(text, "Bottom 10 by profit", "Top offices"))
    assert rows[-1][0] == "Low"
    assert rows[-1][-1] == "-40"
    assert float(rows[0][-1]) > float(rows[-1][-1])


def test_ReportCut_drops_the_best_and_keeps_the_lowest():
    profits = [
        _profit(f"Shop {index}", "food", "ba:street_a", index, index) for index in range(11)
    ]
    text = ledger.render_report(profits, [], [])
    rows = _table_rows(_section(text, "Bottom 10 by profit", "Top offices"))
    assert len(rows) == 10
    assert rows[-1][0] == "Shop 0"
    assert all(row[0] != "Shop 10" for row in rows)


def test_ReportTie_later_name_is_the_bottom_row():
    profits = [
        _profit("Ann", "food", "ba:street_a", 1, -5),
        _profit("Zed", "food", "ba:street_b", 2, -5),
        _profit("Mid", "food", "ba:street_c", 3, 20),
    ]
    text = ledger.render_report(profits, [], [])
    rows = _table_rows(_section(text, "Bottom 10 by profit", "Top offices"))
    assert rows[-1][0] == "Zed"
    assert rows[-2][0] == "Ann"


def test_ReportDays_averages_profit_over_days_present():
    statements = [
        {"street": "ba:street_a", "number": 1, "profit": 0},
        {"street": "ba:street_a", "number": 1, "profit": -80},
        {"street": "ba:street_b", "number": 2, "profit": 100},
    ]
    portfolio = [
        {"business": "Low", "family": "food", "street": "ba:street_a", "number": 1},
        {"business": "High", "family": "food", "street": "ba:street_b", "number": 2},
    ]
    text = ledger.render_report(ledger.business_profits(statements, portfolio), portfolio, [])
    rows = _table_rows(_section(text, "Bottom 10 by profit", "Top offices"))
    assert rows[-1][0] == "Low"
    assert rows[-1][-1] == "-40"


def test_ReportOffices_highest_profit_is_the_top_row():
    profits = [
        _profit("Small Office", "office", "ba:street_a", 1, 10),
        _profit("Big Office", "office", "ba:street_b", 2, 50),
        _profit("Store", "food", "ba:street_c", 3, 1000),
    ]
    text = ledger.render_report(profits, [], [])
    rows = _table_rows(_section(text, "Top offices", "Streets with 3+ of one family"))
    assert [row[0] for row in rows] == ["Big Office", "Small Office"]


def test_ReportStreets_three_of_one_family_qualifies():
    portfolio = [
        {"business": "Food 1", "family": "food", "street": "ba:street_main", "number": 1},
        {"business": "Food 2", "family": "food", "street": "ba:street_main", "number": 2},
        {"business": "Food 3", "family": "food", "street": "ba:street_main", "number": 3},
        {"business": "Side 1", "family": "food", "street": "ba:street_side", "number": 1},
        {"business": "Side 2", "family": "food", "street": "ba:street_side", "number": 2},
        {"business": "Cut", "family": "service", "street": "ba:street_main", "number": 8},
        {"business": "Cut 2", "family": "service", "street": "ba:street_main", "number": 9},
    ]
    text = ledger.render_report([], portfolio, [])
    rows = _table_rows(_section(text, "Streets with 3+ of one family", "Warehouses that mix families"))
    assert rows == [["ba:street_main", "food", "3"]]


def test_ReportJoin_unmatched_statement_is_not_the_bottom_row():
    statements = [
        {"street": "ba:street_a", "number": 1, "profit": -1000},
        {"street": "ba:street_b", "number": 2, "profit": 10},
    ]
    portfolio = [
        {"business": "Known", "family": "food", "street": "ba:street_b", "number": 2},
    ]
    text = ledger.render_report(ledger.business_profits(statements, portfolio), portfolio, [])
    rows = _table_rows(_section(text, "Bottom 10 by profit", "Top offices"))
    assert [row[0] for row in rows] == ["Known"]


def test_ReportStreets_two_families_on_one_street_both_qualify():
    portfolio = []
    for number in range(1, 4):
        portfolio.append(
            {"business": f"Food {number}", "family": "food", "street": "ba:street_main", "number": number}
        )
        portfolio.append(
            {
                "business": f"Service {number}",
                "family": "service",
                "street": "ba:street_main",
                "number": number + 10,
            }
        )
    text = ledger.render_report([], portfolio, [])
    rows = _table_rows(_section(text, "Streets with 3+ of one family", "Warehouses that mix families"))
    assert rows == [
        ["ba:street_main", "food", "3"],
        ["ba:street_main", "service", "3"],
    ]


def test_ReportWarehouse_one_unknown_item_is_not_a_mix():
    held = [
        {"warehouse": "W4", "street": "ba:street_a", "number": 1, "item": "ba:itemname_notinstock"},
    ]
    text = ledger.render_report([], [], held)
    rows = _table_rows(_section(text, "Warehouses that mix families", "\n"))
    assert rows == []


def test_ReportWarehouse_two_families_mix_and_a_lamp_does_not():
    held = [
        {"warehouse": "W1", "street": "ba:street_fourthstreet", "number": 46, "item": "ba:itemname_sodacan"},
        {"warehouse": "W1", "street": "ba:street_fourthstreet", "number": 46, "item": "ba:itemname_paperbag"},
        {
            "warehouse": "W1",
            "street": "ba:street_fourthstreet",
            "number": 46,
            "item": "ba:itemname_ceilinglampwarehouse",
        },
        {"warehouse": "W6", "street": "ba:street_fifthavenue", "number": 43, "item": "ba:itemname_sodacan"},
        {"warehouse": "W6", "street": "ba:street_fifthavenue", "number": 43, "item": "ba:itemname_haircareproduct"},
        {"warehouse": "W2", "street": "ba:street_b", "number": 2, "item": "ba:itemname_burger"},
        {"warehouse": "W2", "street": "ba:street_b", "number": 2, "item": "ba:itemname_cheapgift"},
        {"warehouse": "W10", "street": "ba:street_a", "number": 1, "item": "ba:itemname_burger"},
        {"warehouse": "W10", "street": "ba:street_a", "number": 1, "item": "ba:itemname_notinstock"},
    ]
    text = ledger.render_report([], [], held)
    rows = _table_rows(_section(text, "Warehouses that mix families", "\n"))
    assert [row[0] for row in rows] == ["W2", "W6", "W10"]
    assert rows[0][-1] == "food, specialty"
    assert rows[1][-1] == "food, service"
    assert rows[2][-1] == "food, unknown"
