"""One delivery plan becomes a warehouse and a destination."""

from __future__ import annotations

import importlib.util
import struct
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
CONTRACT = REPO / "tests" / "fixtures" / "routes_contract.bin"
LEDGER_PATH = REPO / "script" / "statement_ledger.py"


def _load(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


ledger = _load("statement_ledger_routes", LEDGER_PATH)


def _key(name: str, type_byte: int = 0x01) -> bytes:
    return bytes([type_byte, 0x01]) + struct.pack("<i", len(name)) + name.encode("utf-16le")


def _text(name: str) -> bytes:
    return bytes([0x01]) + struct.pack("<i", len(name)) + name.encode("utf-16le")


def _warehouse_fixture() -> bytes:
    parts = [
        _key("StreetName"),
        _text("ba:street_fourthstreet"),
        _key("StreetNumber", 0x17),
        struct.pack("<i", 46),
        _key("BusinessName"),
        _text("W1"),
        _key("businessTypeName"),
        _text("ba:businesstype_warehouse"),
        _key("cargoInstances"),
        _key("itemName"),
        _text("ba:itemname_sodacan"),
        _key("itemName"),
        _text("ba:itemname_sodacan"),
        _key("deliveryTransactions"),
        _key("itemName"),
        _text("ba:itemname_burger"),
    ]
    return b"".join(parts)


def test_RoutesFixture_locks_warehouse_and_destination():
    data = CONTRACT.read_bytes()
    rows = ledger.routes_between(data, 0, len(data))
    assert rows[0]["warehouse"] == "ba:street_firststreet 37"
    assert rows[0]["destination"] == "ba:street_fifthavenue 36"
    assert rows[0]["item"] == "ba:itemname_cheapgift"
    assert rows[0]["day"] == 1016
    assert rows[0]["truck"] == ""


def test_RoutesFixture_locks_each_item_on_the_plan():
    data = CONTRACT.read_bytes()
    rows = ledger.routes_between(data, 0, len(data))
    assert [row["item"] for row in rows] == [
        "ba:itemname_cheapgift",
        "ba:itemname_expensivegift",
        "ba:itemname_sodacan",
        "ba:itemname_paperbag",
    ]
    assert {row["destination"] for row in rows} == {"ba:street_fifthavenue 36"}


def test_RoutesPortfolio_business_wins_for_the_destination():
    data = CONTRACT.read_bytes()
    portfolio = [
        {
            "business": "Chaotic Gifts",
            "street": "ba:street_fifthavenue",
            "number": 36,
        }
    ]
    rows = ledger.routes_between(data, 0, len(data), portfolio)
    assert rows[0]["destination"] == "Chaotic Gifts"
    assert rows[0]["warehouse"] == "ba:street_firststreet 37"


def test_RoutesFixture_closes_before_the_next_item_list():
    data = CONTRACT.read_bytes() + _key("FurnitureDeliveryContracts") + _key("items") + _key("itemName") + _text(
        "ba:itemname_banana"
    )
    rows = ledger.routes_between(data, 0, len(data))
    assert [row["item"] for row in rows] == [
        "ba:itemname_cheapgift",
        "ba:itemname_expensivegift",
        "ba:itemname_sodacan",
        "ba:itemname_paperbag",
    ]


def _plan(warehouse_street: str, warehouse_number: int, dest_street: str, dest_number: int, item: str) -> bytes:
    return b"".join(
        [
            _key("assignedWarehouseAddress"),
            _key("streetName"),
            _text(warehouse_street),
            _key("streetNumber", 0x17),
            struct.pack("<i", warehouse_number),
            _key("deliveryTargetAddress"),
            _key("streetName"),
            _text(dest_street),
            _key("streetNumber", 0x17),
            struct.pack("<i", dest_number),
            _key("stockTargets"),
            _key("itemName"),
            _text(item),
        ]
    )


def test_RoutesPlan_locks_a_delivery_plan_destination():
    data = _plan("ba:street_fourthstreet", 46, "ba:street_firstavenue", 38, "ba:itemname_sodacan")
    rows = ledger.routes_between(data, 0, len(data))
    assert len(rows) == 1
    assert rows[0]["warehouse"] == "ba:street_fourthstreet 46"
    assert rows[0]["destination"] == "ba:street_firstavenue 38"
    assert rows[0]["item"] == "ba:itemname_sodacan"


def test_WarehouseFixture_locks_cargo_and_skips_a_delivery():
    data = _warehouse_fixture()
    rows = ledger.warehouses_between(data, 0, len(data))
    assert rows == [
        {
            "warehouse": "W1",
            "street": "ba:street_fourthstreet",
            "number": 46,
            "item": "ba:itemname_sodacan",
            "amount": 2,
        }
    ]
