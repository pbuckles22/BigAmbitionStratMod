#!/usr/bin/env python3
"""Turn a financial summary into statement rows that carry a street.

  python script/statement_ledger.py

Gunzips and indexes the dropzone save, then writes
doc/gemini/cache/ledger/statements.csv. One line is appended to
doc/gemini/cache/ledger.log.
"""

from __future__ import annotations

import csv
import importlib.util
import sqlite3
import struct
import sys
from pathlib import Path

VERSION = "1.1.1"

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
    SLICE.build(save)
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


def main(argv: list[str] | None = None) -> int:
    del argv
    save = SLICE.default_save()
    csv_path = SLICE.CACHE / "ledger" / "statements.csv"
    log_path = SLICE.CACHE / "ledger.log"
    count = write_statements(save, csv_path, log_path)
    print(f"version {VERSION}")
    print(f"Wrote {count} rows to {csv_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
