"""Street and number join to a business and a family."""

from __future__ import annotations

import importlib.util
from pathlib import Path

import pytest

REPO = Path(__file__).resolve().parents[1]
CLOTHING = REPO / "tests" / "fixtures" / "portfolio_clothing.bin"
KABOBS = REPO / "tests" / "fixtures" / "portfolio_kabobs.bin"
EMPTY = REPO / "tests" / "fixtures" / "portfolio_empty.bin"
LEDGER_PATH = REPO / "script" / "statement_ledger.py"


def _load(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


ledger = _load("statement_ledger_portfolio", LEDGER_PATH)


def test_PortfolioFixture_locks_business_and_family():
    data = CLOTHING.read_bytes()
    rows = ledger.buildings_between(data, 0, len(data))
    assert len(rows) == 1
    assert rows[0]["street"] == "ba:street_firstavenue"
    assert rows[0]["number"] == 38
    assert rows[0]["business"] == "Chaotic Clothing #3"
    assert rows[0]["family"] == "durables"


def test_PortfolioFixture_locks_a_food_family():
    data = KABOBS.read_bytes()
    rows = ledger.buildings_between(data, 0, len(data))
    assert len(rows) == 1
    assert rows[0]["street"] == "ba:street_firstavenue"
    assert rows[0]["number"] == 5
    assert rows[0]["business"] == "Chaotic Kabobs #5"
    assert rows[0]["family"] == "food"


def test_PortfolioEmpty_is_not_a_business():
    data = EMPTY.read_bytes()
    rows = ledger.buildings_between(data, 0, len(data))
    assert rows == []


def test_PortfolioDays_average_tco_over_days_present():
    statements = [
        {
            "street": "ba:street_firstavenue",
            "number": 38,
            "goods": 10,
            "wages": 1,
            "rent": 2,
            "marketing": 3,
            "theft": 1000,
        },
        {
            "street": "ba:street_firstavenue",
            "number": 38,
            "goods": 30,
            "wages": 1,
            "rent": 2,
            "marketing": 3,
            "theft": 1000,
        },
        {
            "street": "ba:street_fifthavenue",
            "number": 37,
            "goods": 4,
            "wages": 0,
            "rent": 0,
            "marketing": 0,
            "theft": 50,
        },
        {
            "street": "ba:street_nowhere",
            "number": 1,
            "goods": 9,
            "wages": 9,
            "rent": 9,
            "marketing": 9,
            "theft": 0,
        },
    ]
    buildings = [
        {
            "street": "ba:street_firstavenue",
            "number": 38,
            "business": "Chaotic Clothing #3",
            "family": "durables",
        },
        {
            "street": "ba:street_fifthavenue",
            "number": 37,
            "business": "Chaotic Hair #8",
            "family": "service",
        },
    ]
    rows = ledger.portfolio_rows(statements, buildings)
    by_business = {row["business"]: row for row in rows}
    assert set(by_business) == {"Chaotic Clothing #3", "Chaotic Hair #8"}
    assert by_business["Chaotic Clothing #3"]["tco"] == pytest.approx(26)
    assert by_business["Chaotic Hair #8"]["tco"] == pytest.approx(4)
    assert by_business["Chaotic Clothing #3"]["family"] == "durables"
