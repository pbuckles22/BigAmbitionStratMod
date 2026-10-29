#!/usr/bin/env python3
"""Decompress a Big Ambitions .hsg once and map it into slices.

  python script/Slice-SaveGame.py
  python script/Slice-SaveGame.py --name Money
  python script/Slice-SaveGame.py --region financials

The .hsg is gzip. The decompressed save is about 136 MB. This script writes
that blob to doc/gemini/cache/save.bin a single time, then an index of the
length-prefixed UTF-16 strings. Later lookups read the index, not the .hsg.

Slices are byte ranges (player, buildings, warehouses, people, logistics,
financials). Item instances sit on each building. Permutations are the same
strings written again by region and by topic, so a question opens one small file.
"""

from __future__ import annotations

import argparse
import gzip
import json
import sqlite3
import struct
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
DROPZONE = REPO / "doc" / "gemini" / "dropzone"
CACHE = REPO / "doc" / "gemini" / "cache"

LANDMARKS = (
    ("player", "GameInstance, BigAmbitions"),
    ("buildings", "BuildingRegistration, BigAmbitions"),
    ("warehouses", "Entities.Warehouse, BigAmbitions"),
    ("people", "Entities.EmployeeInstance, BigAmbitions"),
    ("logistics", "Entities.LogisticsManager, BigAmbitions"),
    ("financials", "Entities.FinancialSummary, BigAmbitions"),
)

FLOAT_TYPES = {0x1F}
INT_TYPES = {0x17, 0x2D, 0x01}
BOOL_TYPES = {0x2B}
MONEY_KEYS = {
    "Money",
    "NetWorth",
    "amount",
    "amountSold",
    "total",
    "totalWholesalePrice",
    "customerCapacity",
    "customerService",
    "pricing",
    "cleanliness",
    "facility",
    "overall",
    "trafficIndex",
    "marketing",
}
PORTFOLIO_KEYS = {"BusinessName", "BusinessDescription", "businessTypeName"}
LOGISTICS_KEYS = {
    "deliveryPlans",
    "vehicleTypeName",
    "vehicleInstanceId",
    "cargoIds",
    "Warehouse",
}


def default_save() -> Path:
    saves = sorted(DROPZONE.glob("*.hsg"))
    if len(saves) == 1:
        return saves[0]
    if not saves:
        raise SystemExit(f"No .hsg in {DROPZONE}.")
    names = "\n".join(f"  {p.name}" for p in saves)
    raise SystemExit(f"More than one .hsg in {DROPZONE}. Pass a path:\n{names}")


def recorded_save_name() -> str | None:
    meta = CACHE / "regions.json"
    if not meta.is_file():
        return None
    try:
        name = json.loads(meta.read_text(encoding="utf-8")).get("save")
    except (OSError, json.JSONDecodeError):
        return None
    return name if isinstance(name, str) else None


def reuse_blob(save_name: str, recorded_name: str | None, blob_mtime: float, save_mtime: float) -> bool:
    """Keep save.bin only when it was built from this file and is not older than it."""
    return recorded_name == save_name and blob_mtime >= save_mtime


def load_bin(save: Path) -> tuple[bytes, Path]:
    CACHE.mkdir(parents=True, exist_ok=True)
    blob = CACHE / "save.bin"
    if blob.is_file() and reuse_blob(
        save.name,
        recorded_save_name(),
        blob.stat().st_mtime,
        save.stat().st_mtime,
    ):
        print(f"Using cached {blob} ({blob.stat().st_size} bytes)")
        return blob.read_bytes(), blob
    print(f"Decompressing {save.name} once...")
    data = gzip.open(save, "rb").read()
    blob.write_bytes(data)
    print(f"Wrote {blob} ({len(data)} bytes)")
    return data, blob


def find_regions(data: bytes) -> list[dict]:
    found = []
    for name, marker in LANDMARKS:
        pos = data.find(marker.encode("utf-16le"))
        if pos < 0:
            raise SystemExit(f"Missing landmark {marker}")
        found.append({"name": name, "marker": marker, "start": pos})
    found.sort(key=lambda row: row["start"])
    for i, row in enumerate(found):
        row["end"] = found[i + 1]["start"] if i + 1 < len(found) else len(data)
    found[0]["start"] = 0
    return found


def region_at(regions: list[dict], pos: int) -> str:
    name = regions[0]["name"]
    for row in regions:
        if row["start"] <= pos:
            name = row["name"]
        else:
            break
    return name


def read_string(data: bytes, i: int) -> tuple[str, int] | None:
    if i < 0 or data[i] != 0x01 or i + 5 > len(data):
        return None
    length = struct.unpack_from("<i", data, i + 1)[0]
    if not (1 <= length <= 180):
        return None
    start = i + 5
    end = start + length * 2
    if end > len(data):
        return None
    chars = []
    for c in range(length):
        lo = data[start + c * 2]
        hi = data[start + c * 2 + 1]
        if hi != 0 or lo < 32 or lo > 126:
            return None
        chars.append(chr(lo))
    return "".join(chars), end


def topic_for(key: str) -> str | None:
    if key in PORTFOLIO_KEYS or key.startswith("Chaotic "):
        return "portfolio"
    if key in LOGISTICS_KEYS or "arehouse" in key or "elivery" in key or "ehicle" in key:
        return "logistics"
    if key in MONEY_KEYS or "price" in key.lower() or "rent" in key.lower() or "wage" in key.lower() or "salary" in key.lower():
        return "money"
    return None


def index_is_current(save: Path) -> bool:
    """True when save.bin and the sqlite match this .hsg and are at least as new as it."""
    blob = CACHE / "save.bin"
    db_path = CACHE / "save-index.sqlite"
    meta_path = CACHE / "regions.json"
    if not save.is_file() or not blob.is_file() or not db_path.is_file() or not meta_path.is_file():
        return False
    try:
        recorded = json.loads(meta_path.read_text(encoding="utf-8")).get("save")
    except (OSError, json.JSONDecodeError):
        return False
    if recorded != save.name:
        return False
    blob_mtime = blob.stat().st_mtime
    if blob_mtime < save.stat().st_mtime:
        return False
    return db_path.stat().st_mtime >= blob_mtime


def ensure_index(save: Path) -> None:
    """Keep a current index. Rescan only when the blob or the sqlite is stale."""
    if index_is_current(save):
        return
    build(save)


def build(save: Path) -> None:
    data, blob = load_bin(save)
    db_path = CACHE / "save-index.sqlite"
    if db_path.exists():
        db_path.unlink()
    regions = find_regions(data)
    by_region = CACHE / "by-region"
    by_topic = CACHE / "by-topic"
    by_region.mkdir(parents=True, exist_ok=True)
    by_topic.mkdir(parents=True, exist_ok=True)
    for stale_name in ("world-items-values.tsv", "world-items-numbers.tsv"):
        stale = by_region / stale_name
        if stale.is_file():
            stale.unlink()

    region_files = {
        row["name"]: {
            "values": (by_region / f"{row['name']}-values.tsv").open("w", encoding="utf-8", newline="\n"),
            "numbers": (by_region / f"{row['name']}-numbers.tsv").open("w", encoding="utf-8", newline="\n"),
        }
        for row in regions
    }
    for handle in region_files.values():
        handle["values"].write("key\tvalue\tpos\n")
        handle["numbers"].write("key\ttype\tvalue\tpos\n")
    topics = {
        name: (by_topic / f"{name}.tsv").open("w", encoding="utf-8", newline="\n")
        for name in ("portfolio", "logistics", "money")
    }
    for handle in topics.values():
        handle.write("region\tkey\tvalue\tpos\n")

    db = sqlite3.connect(db_path)
    db.execute("CREATE TABLE strings (pos INTEGER PRIMARY KEY, region TEXT, kind TEXT, type INTEGER, text TEXT, vlen INTEGER, parent TEXT)")
    db.execute("CREATE TABLE regions (name TEXT PRIMARY KEY, start INTEGER, end INTEGER, marker TEXT)")
    db.executemany(
        "INSERT INTO regions (name, start, end, marker) VALUES (?, ?, ?, ?)",
        [(r["name"], r["start"], r["end"], r["marker"]) for r in regions],
    )

    batch: list[tuple] = []
    counts = {row["name"]: 0 for row in regions}
    prev = None
    idx = 0
    accepted = 0

    def flush_batch() -> None:
        if batch:
            db.executemany(
                "INSERT INTO strings (pos, region, kind, type, text, vlen, parent) VALUES (?, ?, ?, ?, ?, ?, ?)",
                batch,
            )
            batch.clear()

    def close_prev(next_pos: int | None) -> None:
        nonlocal prev
        if prev is None:
            return
        if next_pos is None or next_pos <= prev["end"]:
            value = b""
        else:
            value = data[prev["end"] : next_pos - 1]
        prev["vlen"] = len(value)
        decoded = decode_value(prev["type"], value)
        if decoded is not None:
            region_files[prev["region"]]["numbers"].write(
                f"{prev['text']}\t{prev['type']}\t{decoded}\t{prev['pos']}\n"
            )
            topic = topic_for(prev["text"])
            if topic:
                topics[topic].write(f"{prev['region']}\t{prev['text']}\t{decoded}\t{prev['pos']}\n")
        batch.append((prev["pos"], prev["region"], "key", prev["type"], prev["text"], len(value), None))
        counts[prev["region"]] += 1

    while True:
        i = data.find(b"\x01", idx)
        if i < 0:
            break
        got = read_string(data, i)
        if got is None:
            idx = i + 1
            continue
        text, end = got
        region = region_at(regions, i)
        if prev is not None and i == prev["end"]:
            parent = prev["text"]
            batch.append((prev["pos"], prev["region"], "key", prev["type"], parent, 0, None))
            batch.append((i, region, "value", 0, text, 0, parent))
            counts[prev["region"]] += 1
            counts[region] += 1
            region_files[region]["values"].write(f"{parent}\t{text}\t{i}\n")
            topic = topic_for(parent)
            if topic:
                topics[topic].write(f"{region}\t{parent}\t{text}\t{i}\n")
            prev = None
            accepted += 1
            idx = end
            if accepted % 200000 == 0:
                flush_batch()
                db.commit()
                print(f"  indexed {accepted} strings...")
            continue
        close_prev(i)
        prev = {
            "pos": i,
            "end": end,
            "text": text,
            "type": data[i - 1] if i else 0,
            "region": region,
        }
        accepted += 1
        idx = end
        if accepted % 200000 == 0:
            flush_batch()
            db.commit()
            print(f"  indexed {accepted} strings...")

    if prev is not None:
        prev["vlen"] = 0
        batch.append((prev["pos"], prev["region"], "key", prev["type"], prev["text"], 0, None))
        counts[prev["region"]] += 1
    flush_batch()
    db.commit()
    print("Indexing text...")
    db.execute("CREATE INDEX ix_text ON strings(text)")
    db.execute("CREATE INDEX ix_region ON strings(region)")
    db.commit()

    for handles in region_files.values():
        handles["values"].close()
        handles["numbers"].close()
    for handle in topics.values():
        handle.close()

    (CACHE / "regions.json").write_text(json.dumps({"save": save.name, "decompressed": len(data), "blob": str(blob), "regions": regions, "counts": counts}, indent=2), encoding="utf-8")
    write_map(save, len(data), regions, counts, accepted)
    db.close()
    print(f"Done. {accepted} strings. Index: {db_path}")


def decode_value(type_byte: int, value: bytes) -> str | None:
    if type_byte in FLOAT_TYPES and len(value) == 4:
        return str(round(struct.unpack("<f", value)[0], 4))
    if type_byte in INT_TYPES and len(value) == 4:
        return str(struct.unpack("<i", value)[0])
    if type_byte in BOOL_TYPES and len(value) == 1:
        return "1" if value[0] else "0"
    return None


def write_map(save: Path, size: int, regions: list[dict], counts: dict, accepted: int) -> None:
    lines = [
        "# Save map",
        "",
        f"Source `{save.name}` decompresses to {size} bytes at `save.bin`.",
        f"The string index holds {accepted} length-prefixed strings in `save-index.sqlite`.",
        "Do not gunzip the .hsg again. Open a slice below.",
        "",
        "A slice is a byte range of `save.bin`. A permutation is a small TSV of that same index grouped another way.",
        "",
        "## Regions",
        "",
        "| Region | Start | End | Bytes | Strings |",
        "|---|---:|---:|---:|---:|",
    ]
    for row in regions:
        span = row["end"] - row["start"]
        lines.append(
            f"| {row['name']} | {row['start']} | {row['end']} | {span} | {counts.get(row['name'], 0)} |"
        )
    lines += [
        "",
        "## How to read",
        "",
        "- `by-region/<name>-values.tsv` — text sitting on a field (business names, ids).",
        "- `by-region/<name>-numbers.tsv` — ints, floats, and bools whose width is known.",
        "- `by-topic/portfolio.tsv`, `logistics.tsv`, `money.tsv` — the same rows pulled across regions.",
        "- `python script/Slice-SaveGame.py --region financials` — keys in one region, from the index only.",
        "- `python script/Slice-SaveGame.py --name Money` — every Money hit, from the index only.",
        "",
        "Item instances sit on each building. Business, warehouse, staff, route, and statement questions use the regions above.",
        "",
        "A number whose type code is not yet known stays in `save.bin` at the string's `pos`. The index still has the field name and offset, so the next decode seeks there instead of scanning 136 MB.",
        "",
    ]
    (CACHE / "MAP.md").write_text("\n".join(lines), encoding="utf-8")


def query(name: str | None, region: str | None) -> int:
    db_path = CACHE / "save-index.sqlite"
    if not db_path.is_file():
        raise SystemExit("No index yet. Run python script/Slice-SaveGame.py")
    db = sqlite3.connect(db_path)
    if region and not name:
        rows = db.execute(
            "SELECT pos, kind, type, text, parent FROM strings WHERE region = ? ORDER BY pos LIMIT 80",
            (region,),
        ).fetchall()
    elif name and region:
        rows = db.execute(
            "SELECT pos, region, kind, type, text, parent FROM strings WHERE region = ? AND (text = ? OR parent = ?) ORDER BY pos LIMIT 80",
            (region, name, name),
        ).fetchall()
    elif name:
        rows = db.execute(
            "SELECT pos, region, kind, type, text, parent FROM strings WHERE text = ? OR parent = ? ORDER BY pos LIMIT 80",
            (name, name),
        ).fetchall()
    else:
        rows = db.execute("SELECT name, start, end FROM regions ORDER BY start").fetchall()
    for row in rows:
        print("\t".join(str(part) for part in row))
    print(f"{len(rows)} rows")
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Map a Big Ambitions .hsg into slices.")
    parser.add_argument("save", nargs="?", help="Path to an .hsg. Default: the only file in doc/gemini/dropzone/.")
    parser.add_argument("--name", help="Look up a field in the index. Does not read the 136 MB blob.")
    parser.add_argument("--region", help="List strings in one region from the index.")
    args = parser.parse_args(argv)
    if args.name or args.region:
        return query(args.name, args.region)
    save = Path(args.save) if args.save else default_save()
    if not save.is_file():
        raise SystemExit(f"Save not found: {save}")
    build(save)
    return 0


if __name__ == "__main__":
    sys.exit(main())
