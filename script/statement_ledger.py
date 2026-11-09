#!/usr/bin/env python3
"""Turn a financial summary into statement rows, a portfolio, routes, and a rank report.

  python script/statement_ledger.py

Reuses a current save index, writes
doc/gemini/cache/ledger/statements.csv, joins each street and number to
BuildingRegistration for portfolio.csv, writes routes.csv and warehouses.csv,
then writes REPORT.md. One line per file is appended to doc/gemini/cache/ledger.log.
"""

from __future__ import annotations

import csv
import importlib.util
import sqlite3
import struct
import sys
from pathlib import Path

VERSION = "1.2.2"

FAMILIES = {
    "ba:businesstype_fastfoodrestaurant": "food",
    "ba:businesstype_coffeeshop": "food",
    "ba:businesstype_fruitandvegetablestore": "food",
    "ba:businesstype_liquorstore": "food",
    "ba:businesstype_clothingstore": "durables",
    "ba:businesstype_electronicsstore": "durables",
    "ba:businesstype_furniturestore": "durables",
    "ba:businesstype_cardealership": "durables",
    "ba:businesstype_florist": "specialty",
    "ba:businesstype_hairdresser": "service",
    "ba:businesstype_gym": "service",
    "ba:businesstype_nightclub": "service",
    "ba:businesstype_travelagency": "service",
    "ba:businesstype_eventplanningagency": "service",
    "ba:businesstype_lawfirm": "office",
    "ba:businesstype_recruitmentagency": "office",
    "ba:businesstype_webdevelopmentagency": "office",
    "ba:businesstype_wholesalestore": "warehouse",
    "ba:businesstype_truckgarage": "warehouse",
}

FLOAT_FIELDS = {
    "TotalSales": "sales",
    "TotalResources": "goods",
    "SalaryExpenses": "wages",
    "RentExpenses": "rent",
    "MarketingExpenses": "marketing",
    "Theft": "theft",
    "TotalProfit": "profit",
}

COLUMNS = (
    "day",
    "street",
    "number",
    "sales",
    "goods",
    "wages",
    "rent",
    "marketing",
    "theft",
    "profit",
)

PORTFOLIO_COLUMNS = ("business", "family", "street", "number", "tco")

ROUTE_COLUMNS = ("warehouse", "destination", "item", "day", "truck")

WAREHOUSE_COLUMNS = ("warehouse", "street", "number", "item", "amount")

IGNORED_CARGO = {
    "ba:itemname_ceilinglampwarehouse",
    "ba:itemname_closedcardboardbox",
    "ba:itemname_deliveryspot",
    "ba:itemname_flatbedspawner",
    "ba:itemname_handtruckspawner",
    "ba:itemname_palletshelf",
    "ba:itemname_paperbag",
}

_ITEM_GROUPS = {
    "food": {
        "apple",
        "banana",
        "beer",
        "bottleofwine",
        "burger",
        "carrot",
        "cigar",
        "cigarette",
        "croissant",
        "cupcake",
        "cupofcoffee",
        "donut",
        "frenchfries",
        "freshfood",
        "frozenfood",
        "hotdog",
        "icecream",
        "kabob",
        "lettuce",
        "margarita",
        "martini",
        "pear",
        "pizza",
        "salad",
        "sodacan",
        "tomato",
        "whisky",
    },
    "durables": {
        "cheapjewelry",
        "classiccheapfemaleclothing",
        "classiccheapmaleclothing",
        "classicexpensivefemaleclothing",
        "classicexpensivemaleclothing",
        "earbuds01",
        "expensivejewelry",
        "headphones01",
        "moderncheapfemaleclothing",
        "moderncheapmaleclothing",
        "modernexpensivefemaleclothing",
        "modernexpensivemaleclothing",
        "smartphone1",
        "smartphone2",
        "smartwatch1",
        "smartwatch2",
    },
    "specialty": {
        "cheapflower",
        "cheapgift",
        "expensiveflower",
        "expensivegift",
        "umbrella",
        "limitededitionbook",
        "motivationalbook",
        "novel",
        "picturebook",
        "technicalmanual",
        "youngnovel",
    },
    "service": {"haircareproduct"},
}

ITEM_FAMILIES = {
    f"ba:itemname_{name}": family
    for family, names in _ITEM_GROUPS.items()
    for name in names
}

WAREHOUSE_ADDRESS = {"assignedWarehouseAddress", "wholesaleAddress", "targetAddress"}

DESTINATION_ADDRESS = {"deliveryTargetAddress", "businessAddress", "destinationAddress"}

ITEM_FIELDS = {
    "itemName",
    "amount",
    "boxes",
    "amountOrderedLastWeek",
    "amountOrderedThisWeek",
    "targetAmount",
}


def _slice():
    path = Path(__file__).with_name("Slice-SaveGame.py")
    spec = importlib.util.spec_from_file_location("slice_save_game", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


SLICE = _slice()


def iter_keys(data: bytes, start: int, end: int):
    idx = start
    read_string = SLICE.read_string
    while idx < end:
        i = data.find(b"\x01", idx, end)
        if i < 0:
            return
        got = read_string(data, i)
        if got is None:
            idx = i + 1
            continue
        text, key_end = got
        if key_end > end:
            return
        type_byte = data[i - 1] if i else 0
        yield i, type_byte, text, key_end
        idx = key_end


def _float_at(data: bytes, type_byte: int, key_end: int) -> float | None:
    if type_byte not in SLICE.FLOAT_TYPES or key_end + 4 > len(data):
        return None
    return struct.unpack_from("<f", data, key_end)[0]


def _int_at(data: bytes, type_byte: int, key_end: int) -> int | None:
    if type_byte not in SLICE.INT_TYPES or key_end + 4 > len(data):
        return None
    return struct.unpack_from("<i", data, key_end)[0]


def _blank_fields() -> dict:
    return {name: None for name in COLUMNS if name != "day"}


def rows_between(data: bytes, start: int, end: int) -> list[dict]:
    """Walk keys in order. Bytes after TotalProfit become that row's street."""
    day = None
    fields = _blank_fields()
    rows: list[dict] = []
    expect_street_at = None
    keys = list(iter_keys(data, start, end))
    for index, item in enumerate(keys):
        pos, type_byte, text, key_end = item
        nxt = keys[index + 1] if index + 1 < len(keys) else None
        if expect_street_at is not None and pos == expect_street_at:
            fields["street"] = text
            expect_street_at = None
            continue
        expect_street_at = None
        if (
            text == "dayNumber"
            and nxt is not None
            and nxt[2] == "businessIncomeStatements"
            and nxt[0] == key_end + 5
        ):
            value = _int_at(data, type_byte, key_end)
            if value is not None:
                day = value
            continue
        if text in FLOAT_FIELDS:
            value = _float_at(data, type_byte, key_end)
            if value is not None:
                fields[FLOAT_FIELDS[text]] = value
            continue
        if text == "streetName":
            expect_street_at = key_end
            continue
        if text != "streetNumber":
            continue
        number = _int_at(data, type_byte, key_end)
        if number is None or fields["street"] is None or fields["profit"] is None:
            fields = _blank_fields()
            continue
        row = dict(fields)
        row["day"] = day
        row["number"] = number
        rows.append(row)
        fields = _blank_fields()
    return rows


def family_for(business_type: str) -> str | None:
    return FAMILIES.get(business_type)


def buildings_between(data: bytes, start: int, end: int) -> list[dict]:
    """Walk BuildingRegistration. Street and number become a business and a family."""
    street = None
    number = None
    business = None
    expect = None
    rows: list[dict] = []
    for pos, type_byte, text, key_end in iter_keys(data, start, end):
        if expect is not None and pos == expect[1]:
            kind = expect[0]
            expect = None
            if kind == "street":
                street = text
            elif kind == "business":
                business = text
            elif kind == "type":
                family = family_for(text)
                if street is not None and number is not None and business and family:
                    rows.append(
                        {
                            "street": street,
                            "number": number,
                            "business": business,
                            "family": family,
                        }
                    )
                street = None
                number = None
                business = None
            continue
        expect = None
        if text == "StreetName":
            street = None
            number = None
            business = None
            expect = ("street", key_end)
            continue
        if text == "StreetNumber":
            number = _int_at(data, type_byte, key_end)
            continue
        if text == "BusinessName":
            expect = ("business", key_end)
            continue
        if text == "businessTypeName":
            expect = ("type", key_end)
    return rows


def _money(row: dict, name: str) -> float:
    value = row.get(name)
    if value is None or value == "":
        return 0.0
    return float(value)


def portfolio_rows(statements: list[dict], buildings: list[dict]) -> list[dict]:
    """One row per joined business. TCO is goods + wages + rent + marketing, averaged on days present."""
    index = {}
    for building in buildings:
        if not building.get("business") or not building.get("family"):
            continue
        index[(building["street"], int(building["number"]))] = building
    buckets: dict[tuple, list[float]] = {}
    for row in statements:
        key = (row["street"], int(row["number"]))
        building = index.get(key)
        if building is None:
            continue
        cost = (
            _money(row, "goods")
            + _money(row, "wages")
            + _money(row, "rent")
            + _money(row, "marketing")
        )
        buckets.setdefault(key, []).append(cost)
    rows = []
    for key in sorted(buckets):
        costs = buckets[key]
        building = index[key]
        rows.append(
            {
                "business": building["business"],
                "family": building["family"],
                "street": key[0],
                "number": key[1],
                "tco": sum(costs) / len(costs),
            }
        )
    return rows


def _label(street: str, number: int, portfolio: list[dict] | None) -> str:
    if portfolio:
        for row in portfolio:
            try:
                same_number = int(row["number"]) == int(number)
            except (TypeError, ValueError, KeyError):
                continue
            if row.get("street") == street and same_number and row.get("business"):
                return str(row["business"])
    return f"{street} {number}"


def _keeps_item_list(text: str) -> bool:
    if text in ITEM_FIELDS:
        return True
    if text.startswith("System.Collections"):
        return True
    return "DeliveryContractItem" in text or "ItemAmount" in text


def routes_between(
    data: bytes, start: int, end: int, portfolio: list[dict] | None = None
) -> list[dict]:
    """One delivery plan becomes rows of warehouse, destination, item, day, and truck."""
    warehouse = None
    destination = None
    day = None
    truck = ""
    role = None
    pending_street = None
    expect_street = None
    expect_item = None
    expect_truck = None
    in_items = False
    rows: list[dict] = []
    for pos, type_byte, text, key_end in iter_keys(data, start, end):
        if expect_street is not None and pos == expect_street:
            pending_street = text
            expect_street = None
            continue
        if expect_item is not None and pos == expect_item:
            expect_item = None
            if warehouse is not None and destination is not None and in_items:
                rows.append(
                    {
                        "warehouse": warehouse,
                        "destination": destination,
                        "item": text,
                        "day": day,
                        "truck": truck,
                    }
                )
            continue
        if expect_truck is not None and pos == expect_truck:
            truck = text
            expect_truck = None
            continue
        if role and text in ("streetName", "StreetName"):
            expect_street = key_end
            continue
        if role and pending_street is not None and text in ("streetNumber", "StreetNumber"):
            number = _int_at(data, type_byte, key_end)
            if number is not None:
                label = _label(pending_street, number, portfolio)
                if role == "warehouse":
                    warehouse = label
                else:
                    destination = label
            pending_street = None
            role = None
            continue
        if in_items and not _keeps_item_list(text):
            in_items = False
            warehouse = None
            destination = None
            day = None
            truck = ""
        if text in WAREHOUSE_ADDRESS:
            role = "warehouse"
            pending_street = None
            continue
        if text in DESTINATION_ADDRESS:
            role = "destination"
            pending_street = None
            continue
        if text in ("nextDeliveryDay", "dayOfDelivery"):
            value = _int_at(data, type_byte, key_end)
            if value is not None:
                day = value
            continue
        if text == "vehicleTypeName":
            expect_truck = key_end
            continue
        if text in ("items", "stockTargets"):
            in_items = True
            continue
        if text == "itemName" and in_items:
            expect_item = key_end
    return rows


def warehouses_between(data: bytes, start: int, end: int) -> list[dict]:
    """Cargo on a warehouse building. A delivery item is not stock."""
    street = None
    number = None
    business = None
    is_warehouse = False
    in_cargo = False
    expect = None
    counts: dict[tuple, int] = {}
    order: list[tuple] = []
    for pos, type_byte, text, key_end in iter_keys(data, start, end):
        if expect is not None and pos == expect[1]:
            kind = expect[0]
            expect = None
            if kind == "street":
                street = text
            elif kind == "business":
                business = text
            elif kind == "type":
                is_warehouse = text == "ba:businesstype_warehouse"
            elif kind == "item" and in_cargo and is_warehouse and business and street and number is not None:
                key = (business, street, number, text)
                if key not in counts:
                    order.append(key)
                    counts[key] = 0
                counts[key] += 1
            continue
        expect = None
        if text == "StreetName":
            street = None
            number = None
            business = None
            is_warehouse = False
            in_cargo = False
            expect = ("street", key_end)
            continue
        if text == "StreetNumber":
            number = _int_at(data, type_byte, key_end)
            continue
        if text == "BusinessName":
            expect = ("business", key_end)
            continue
        if text == "businessTypeName":
            expect = ("type", key_end)
            continue
        if text == "cargoInstances" and is_warehouse:
            in_cargo = True
            continue
        if text == "deliveryTransactions":
            in_cargo = False
            continue
        if text == "itemName" and in_cargo:
            expect = ("item", key_end)
    rows = []
    for business_name, street_name, street_number, item in order:
        rows.append(
            {
                "warehouse": business_name,
                "street": street_name,
                "number": street_number,
                "item": item,
                "amount": counts[(business_name, street_name, street_number, item)],
            }
        )
    return rows


def format_number(value) -> str:
    if value is None:
        return ""
    if isinstance(value, int) and not isinstance(value, bool):
        return str(value)
    rounded = round(float(value), 4)
    return f"{rounded:.4f}".rstrip("0").rstrip(".")


def _csv_row(row: dict) -> dict:
    written = {}
    for name in COLUMNS:
        value = row.get(name)
        if name == "street":
            written[name] = "" if value is None else str(value)
        else:
            written[name] = format_number(value)
    return written


def _region_bounds(name: str) -> tuple[int, int]:
    db = sqlite3.connect(SLICE.CACHE / "save-index.sqlite")
    try:
        found = db.execute("SELECT start, end FROM regions WHERE name = ?", (name,)).fetchone()
    finally:
        db.close()
    if found is None:
        raise SystemExit(f"Index has no region {name}.")
    return int(found[0]), int(found[1])


def write_statements(save: Path, csv_path: Path, log_path: Path) -> int:
    SLICE.ensure_index(save)
    data = (SLICE.CACHE / "save.bin").read_bytes()
    start, end = _region_bounds("financials")
    rows = rows_between(data, start, end)
    csv_path.parent.mkdir(parents=True, exist_ok=True)
    with csv_path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=COLUMNS, lineterminator="\n")
        writer.writeheader()
        for row in rows:
            writer.writerow(_csv_row(row))
    first = rows[0] if rows else {}
    log_path.parent.mkdir(parents=True, exist_ok=True)
    line = (
        f"statements.written version={VERSION} rows={len(rows)} "
        f"day={format_number(first.get('day'))} street={first.get('street')} "
        f"number={format_number(first.get('number'))} profit={format_number(first.get('profit'))}\n"
    )
    with log_path.open("a", encoding="utf-8") as handle:
        handle.write(line)
    return len(rows)


def _read_statements(csv_path: Path) -> list[dict]:
    with csv_path.open(encoding="utf-8", newline="") as handle:
        return list(csv.DictReader(handle))


def _portfolio_csv_row(row: dict) -> dict:
    return {
        "business": row["business"],
        "family": row["family"],
        "street": row["street"],
        "number": format_number(row["number"]),
        "tco": format_number(row["tco"]),
    }


def write_portfolio(statements_csv: Path, csv_path: Path, log_path: Path) -> int:
    data = (SLICE.CACHE / "save.bin").read_bytes()
    start, end = _region_bounds("buildings")
    buildings = buildings_between(data, start, end)
    statements = _read_statements(statements_csv)
    rows = portfolio_rows(statements, buildings)
    csv_path.parent.mkdir(parents=True, exist_ok=True)
    with csv_path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=PORTFOLIO_COLUMNS, lineterminator="\n")
        writer.writeheader()
        for row in rows:
            writer.writerow(_portfolio_csv_row(row))
    first = statements[0] if statements else {}
    joined = next(
        (
            row
            for row in rows
            if row["street"] == first.get("street") and str(row["number"]) == str(first.get("number"))
        ),
        rows[0] if rows else {},
    )
    log_path.parent.mkdir(parents=True, exist_ok=True)
    line = (
        f"portfolio.written version={VERSION} rows={len(rows)} "
        f"business={joined.get('business')} family={joined.get('family')} "
        f"street={joined.get('street')} number={format_number(joined.get('number'))} "
        f"tco={format_number(joined.get('tco'))}\n"
    )
    with log_path.open("a", encoding="utf-8") as handle:
        handle.write(line)
    return len(rows)


def _route_csv_row(row: dict) -> dict:
    return {
        "warehouse": row["warehouse"],
        "destination": row["destination"],
        "item": row["item"],
        "day": format_number(row.get("day")),
        "truck": row.get("truck") or "",
    }


def _warehouse_csv_row(row: dict) -> dict:
    return {
        "warehouse": row["warehouse"],
        "street": row["street"],
        "number": format_number(row["number"]),
        "item": row["item"],
        "amount": format_number(row["amount"]),
    }


def write_routes(
    portfolio_csv: Path, routes_csv: Path, warehouses_csv: Path, log_path: Path
) -> int:
    portfolio = _read_statements(portfolio_csv)
    data = (SLICE.CACHE / "save.bin").read_bytes()
    logistics_start, _logistics_end = _region_bounds("logistics")
    _financials_start, financials_end = _region_bounds("financials")
    routes = routes_between(data, logistics_start, financials_end, portfolio)
    warehouse_start, warehouse_end = _region_bounds("warehouses")
    held = warehouses_between(data, warehouse_start, warehouse_end)
    routes_csv.parent.mkdir(parents=True, exist_ok=True)
    with routes_csv.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=ROUTE_COLUMNS, lineterminator="\n")
        writer.writeheader()
        for row in routes:
            writer.writerow(_route_csv_row(row))
    with warehouses_csv.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=WAREHOUSE_COLUMNS, lineterminator="\n")
        writer.writeheader()
        for row in held:
            writer.writerow(_warehouse_csv_row(row))
    first = routes[0] if routes else {}
    log_path.parent.mkdir(parents=True, exist_ok=True)
    line = (
        f"routes.written version={VERSION} routes={len(routes)} warehouses={len(held)} "
        f"portfolio={len(portfolio)} warehouse={first.get('warehouse')} "
        f"destination={first.get('destination')} item={first.get('item')} "
        f"day={format_number(first.get('day'))}\n"
    )
    with log_path.open("a", encoding="utf-8") as handle:
        handle.write(line)
    return len(routes)


def sales_between(statements: list[dict], street: str, number: int, first_day: int, last_day: int) -> float:
    """Sum sales for one address. Days outside the window stay out. Profit is not sales."""
    total = 0.0
    address = int(number)
    for row in statements:
        try:
            row_number = int(row["number"])
            day = int(row["day"])
            sales = float(row["sales"])
        except (TypeError, ValueError, KeyError):
            continue
        if row.get("street") != street or row_number != address:
            continue
        if first_day <= day <= last_day:
            total += sales
    return total


def business_profits(statements: list[dict], portfolio: list[dict]) -> list[dict]:
    """Average daily profit over the days that business is present."""
    index = {}
    for building in portfolio:
        if not building.get("business"):
            continue
        try:
            number = int(building["number"])
        except (TypeError, ValueError, KeyError):
            continue
        index[(building.get("street"), number)] = building
    buckets: dict[tuple, list[float]] = {}
    for row in statements:
        try:
            number = int(row["number"])
            profit = float(row["profit"])
        except (TypeError, ValueError, KeyError):
            continue
        key = (row.get("street"), number)
        if key not in index:
            continue
        buckets.setdefault(key, []).append(profit)
    rows = []
    for key, values in buckets.items():
        building = index[key]
        rows.append(
            {
                "business": building["business"],
                "family": building.get("family") or "",
                "street": key[0],
                "number": key[1],
                "profit": sum(values) / len(values),
            }
        )
    return rows


def bottom_by_profit(rows: list[dict], limit: int = 10) -> list[dict]:
    """Lowest profit last. The same profit keeps the later business name at the bottom."""
    ordered = sorted(rows, key=lambda row: (-float(row["profit"]), str(row["business"])))
    if len(ordered) <= limit:
        return ordered
    return ordered[-limit:]


def top_offices(rows: list[dict]) -> list[dict]:
    """Office family only. Highest profit is the first row."""
    offices = [row for row in rows if row.get("family") == "office"]
    return sorted(offices, key=lambda row: (-float(row["profit"]), str(row["business"])))


def street_clusters(portfolio: list[dict], minimum: int = 3) -> list[dict]:
    """Streets with at least `minimum` businesses of one family."""
    counts: dict[tuple, int] = {}
    for row in portfolio:
        street = row.get("street")
        family = row.get("family")
        if not street or not family:
            continue
        key = (street, family)
        counts[key] = counts.get(key, 0) + 1
    clusters = [
        {"street": street, "family": family, "count": count}
        for (street, family), count in counts.items()
        if count >= minimum
    ]
    return sorted(clusters, key=lambda row: (row["street"], row["family"]))


def item_family(item: str) -> str | None:
    """Retail family for a cargo item. Warehouse gear and bags are not a family."""
    if not item or item in IGNORED_CARGO:
        return None
    return ITEM_FAMILIES.get(item, "unknown")


def _warehouse_sort_key(name: str) -> tuple:
    digits = "".join(ch for ch in name if ch.isdigit())
    prefix = "".join(ch for ch in name if not ch.isdigit())
    return (prefix, int(digits) if digits else 0, name)


def warehouse_mixes(rows: list[dict]) -> list[dict]:
    """A warehouse mixes when its cargo covers two or more retail families."""
    groups: dict[tuple, set[str]] = {}
    for row in rows:
        family = item_family(str(row.get("item") or ""))
        if family is None:
            continue
        try:
            number = int(row["number"])
        except (TypeError, ValueError, KeyError):
            continue
        key = (str(row.get("warehouse") or ""), str(row.get("street") or ""), number)
        groups.setdefault(key, set()).add(family)
    mixed = []
    for (warehouse, street, number), families in groups.items():
        if len(families) < 2:
            continue
        mixed.append(
            {
                "warehouse": warehouse,
                "street": street,
                "number": number,
                "families": ", ".join(sorted(families)),
            }
        )
    return sorted(
        mixed,
        key=lambda row: (_warehouse_sort_key(row["warehouse"]), row["street"], row["number"]),
    )


def _section_table(title: str, headers: tuple[str, ...], rows: list[tuple]) -> list[str]:
    lines = [f"## {title}", ""]
    if not rows:
        lines.extend(["None.", ""])
        return lines
    lines.append("| " + " | ".join(headers) + " |")
    lines.append("| " + " | ".join("---" for _ in headers) + " |")
    for row in rows:
        lines.append("| " + " | ".join(str(cell) for cell in row) + " |")
    lines.append("")
    return lines


def render_report(profits: list[dict], portfolio: list[dict], warehouses: list[dict]) -> str:
    """Bottom 10 by profit, top offices, street clusters, and mixed warehouses."""
    lines = ["# Rank report", ""]
    lines.extend(
        _section_table(
            "Bottom 10 by profit",
            ("business", "family", "street", "number", "profit"),
            [
                (
                    row["business"],
                    row["family"],
                    row["street"],
                    format_number(row["number"]),
                    format_number(row["profit"]),
                )
                for row in bottom_by_profit(profits)
            ],
        )
    )
    lines.extend(
        _section_table(
            "Top offices",
            ("business", "family", "street", "number", "profit"),
            [
                (
                    row["business"],
                    row["family"],
                    row["street"],
                    format_number(row["number"]),
                    format_number(row["profit"]),
                )
                for row in top_offices(profits)
            ],
        )
    )
    lines.extend(
        _section_table(
            "Streets with 3+ of one family",
            ("street", "family", "count"),
            [
                (row["street"], row["family"], format_number(row["count"]))
                for row in street_clusters(portfolio)
            ],
        )
    )
    lines.extend(
        _section_table(
            "Warehouses that mix families",
            ("warehouse", "street", "number", "families"),
            [
                (
                    row["warehouse"],
                    row["street"],
                    format_number(row["number"]),
                    row["families"],
                )
                for row in warehouse_mixes(warehouses)
            ],
        )
    )
    return "\n".join(lines).rstrip() + "\n"


def write_report(
    statements_csv: Path,
    portfolio_csv: Path,
    warehouses_csv: Path,
    report_path: Path,
    log_path: Path,
) -> int:
    statements = _read_statements(statements_csv)
    portfolio = _read_statements(portfolio_csv)
    warehouses = _read_statements(warehouses_csv)
    profits = business_profits(statements, portfolio)
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(render_report(profits, portfolio, warehouses), encoding="utf-8")
    bottom = bottom_by_profit(profits)
    bottom_row = bottom[-1] if bottom else {}
    log_path.parent.mkdir(parents=True, exist_ok=True)
    line = (
        f"report.written version={VERSION} bottom={bottom_row.get('business', '')} "
        f"profit={format_number(bottom_row.get('profit'))} "
        f"offices={len(top_offices(profits))} streets={len(street_clusters(portfolio))} "
        f"mixes={len(warehouse_mixes(warehouses))}\n"
    )
    with log_path.open("a", encoding="utf-8") as handle:
        handle.write(line)
    return len(bottom)


def run(save: Path | None = None) -> str:
    """Statements, portfolio, routes, then the rank report. Returns the report text."""
    save = save or SLICE.default_save()
    ledger_dir = SLICE.CACHE / "ledger"
    statements_path = ledger_dir / "statements.csv"
    portfolio_path = ledger_dir / "portfolio.csv"
    routes_path = ledger_dir / "routes.csv"
    warehouses_path = ledger_dir / "warehouses.csv"
    report_path = ledger_dir / "REPORT.md"
    log_path = SLICE.CACHE / "ledger.log"
    count = write_statements(save, statements_path, log_path)
    businesses = write_portfolio(statements_path, portfolio_path, log_path)
    routes = write_routes(portfolio_path, routes_path, warehouses_path, log_path)
    write_report(statements_path, portfolio_path, warehouses_path, report_path, log_path)
    print(f"version {VERSION}")
    print(f"Wrote {count} rows to {statements_path}")
    print(f"Wrote {businesses} rows to {portfolio_path}")
    print(f"Wrote {routes} rows to {routes_path}")
    print(f"Wrote warehouses to {warehouses_path}")
    print(f"Wrote report to {report_path}")
    return report_path.read_text(encoding="utf-8")


def main(argv: list[str] | None = None) -> int:
    del argv
    run()
    return 0


if __name__ == "__main__":
    sys.exit(main())
