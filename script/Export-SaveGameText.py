#!/usr/bin/env python3
"""Turn a Big Ambitions .hsg into a short markdown text file.

Say "saved game to text". This script is what that runs.

  python script/Export-SaveGameText.py
  python script/Export-SaveGameText.py "path/to/save.hsg"
  python script/Export-SaveGameText.py "path/to/save.hsg" -o doc/gemini/empire_summary.md

Default input is the only .hsg in doc/gemini/dropzone/.
Default output is doc/gemini/empire_summary.md (gitignored).

The save is gzip. World item instances are skipped. The markdown lists
candidate Money / NetWorth / Energy / Happiness readings, serialized C#
type names, and the first unique non-item strings.
"""

from __future__ import annotations

import argparse
import gzip
import re
import struct
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_DROPZONE = REPO_ROOT / "doc" / "gemini" / "dropzone"
DEFAULT_OUTPUT = REPO_ROOT / "doc" / "gemini" / "empire_summary.md"

IGNORE_KEYS = {
    "itemName",
    "position",
    "rotation",
    "yRotation",
    "stackedItems",
    "streetName",
    "streetNumber",
    "instanceQuantity",
    "paid",
    "temporary",
    "packedItemName",
    "cargoIds",
    "pricePerUnitOnPurchaseTime",
    "dirtSpotsThatAffects",
    "parentId",
    "shelfItem",
    "pricePerUnitOnPurchase",
    "stock",
    "cargoParentId",
    "customPositions",
    "customColors",
    "linkedItemName",
    "isSecured",
    "itemControllerId",
    "distancesToItems",
    "comparer",
    "worldSpaceTextValue",
    "stateIndex",
    "dirtAffectedCells",
    "channel",
    "color",
    "childId",
    "attachmentIndex",
}

B64_RE = re.compile(r"^[A-Za-z0-9+/]{20,24}={0,2}$")
UTF16_RUN = re.compile(b"(?:[\x20-\x7e]\x00){4,}")
UTF16_TYPE = re.compile(b"(?:[\x20-\x7e]\x00){6,}")
PLAYER_KEYS = ("Money", "NetWorth", "Energy", "Happiness")


def resolve_save(explicit: str | None) -> Path:
    if explicit:
        path = Path(explicit)
        if not path.is_file():
            raise SystemExit(f"Save not found: {path}")
        return path

    saves = sorted(DEFAULT_DROPZONE.glob("*.hsg"))
    if len(saves) == 1:
        return saves[0]
    if not saves:
        raise SystemExit(
            f"No .hsg in {DEFAULT_DROPZONE}. Pass a path, or drop one file there."
        )
    names = "\n".join(f"  {p.name}" for p in saves)
    raise SystemExit(f"More than one .hsg in {DEFAULT_DROPZONE}. Pass a path:\n{names}")


def read_values_after(data: bytes, key: str, max_hits: int = 3) -> list[tuple[int, str, dict]]:
    pattern = key.encode("utf-16le")
    results = []
    pos = 0
    while len(results) < max_hits:
        idx = data.find(pattern, pos)
        if idx == -1:
            break
        end = idx + len(pattern)
        chunk = data[end : end + 16]
        vals: dict[str, float | int] = {}
        for offset in range(0, 9):
            if offset + 4 > len(chunk):
                break
            f_val = struct.unpack_from("<f", chunk, offset)[0]
            i_val = struct.unpack_from("<i", chunk, offset)[0]
            if 1.0 < abs(f_val) < 1e10:
                vals[f"f32@{offset}"] = round(f_val, 2)
            if 0 < i_val < 1_000_000_000:
                vals[f"i32@{offset}"] = i_val
        results.append((idx, chunk.hex(), vals))
        pos = end
    return results


def collect(data: bytes) -> tuple[set[str], list[tuple[int, str]]]:
    type_defs: set[str] = set()
    for match in UTF16_TYPE.finditer(data):
        text = match.group(0).decode("utf-16le", errors="ignore")
        if "BigAmbitions" in text or "System.Collections" in text or "Manager" in text:
            type_defs.add(text)

    unique: list[tuple[int, str]] = []
    seen: set[str] = set()
    for match in UTF16_RUN.finditer(data):
        text = match.group(0).decode("utf-16le", errors="ignore").strip()
        if text in IGNORE_KEYS or text[:-1] in IGNORE_KEYS or B64_RE.match(text):
            continue
        if text in seen:
            continue
        seen.add(text)
        unique.append((match.start(), text))
    return type_defs, unique


def write_summary(save: Path, data: bytes, out_path: Path, type_defs: set[str], unique: list[tuple[int, str]]) -> None:
    out_path.parent.mkdir(parents=True, exist_ok=True)
    with out_path.open("w", encoding="utf-8", newline="\n") as out:
        out.write("# Empire Binary Extraction\n\n")
        out.write(f"Source: `{save.name}` ({save.stat().st_size} bytes on disk, {len(data)} bytes decompressed).\n\n")
        out.write("Numbers next to Money and NetWorth are candidate readings in the 16 bytes after each key. The first hit is the player header. Later hits are other fields that contain the same word.\n\n")

        out.write("## 1. Top-Level Player Metrics (Money & NetWorth)\n")
        for key in PLAYER_KEYS:
            out.write(f"### `{key}`\n")
            for offset, hex_bytes, decoded in read_values_after(data, key):
                out.write(f"- Offset `{offset}` | Hex: `{hex_bytes}` | `{decoded}`\n")

        out.write("\n## 2. C# Classes Serialized in Save\n")
        for name in sorted(type_defs):
            out.write(f"- `{name}`\n")

        shown = min(len(unique), 2500)
        out.write(f"\n## 3. Unique Non-Item Strings & Keys ({len(unique)} total, first {shown})\n")
        for offset, text in unique[:2500]:
            out.write(f"`@{offset}`: {text}\n")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Turn a Big Ambitions .hsg into markdown text.")
    parser.add_argument("save", nargs="?", help="Path to an .hsg. Default: the only file in doc/gemini/dropzone/.")
    parser.add_argument("-o", "--output", default=str(DEFAULT_OUTPUT), help="Markdown output path.")
    args = parser.parse_args(argv)

    save = resolve_save(args.save)
    out_path = Path(args.output)
    if not out_path.is_absolute():
        out_path = REPO_ROOT / out_path

    with gzip.open(save, "rb") as handle:
        data = handle.read()

    type_defs, unique = collect(data)
    write_summary(save, data, out_path, type_defs, unique)
    print(f"Done. {save.name} -> {out_path}")
    print(f"types={len(type_defs)} unique_strings={len(unique)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
