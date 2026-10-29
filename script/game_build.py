#!/usr/bin/env python3
"""Pin Managed DLL names and hashes for one game build."""

from __future__ import annotations

import hashlib

BUILD = "3682"
MANAGED = "Big Ambitions_Data\\Managed"


def pin_dll(name: str, data: bytes) -> dict:
    return {"name": name, "hash": hashlib.sha256(data).hexdigest()}


def render_pin(rows: list[dict]) -> str:
    lines = [
        "# Game build",
        "",
        f"Build {BUILD}.",
        "",
        f"Managed folder: `{MANAGED}`.",
        "",
        "| name | sha256 |",
        "| --- | --- |",
    ]
    for row in rows:
        lines.append(f"| {row['name']} | {row['hash']} |")
    lines.append("")
    return "\n".join(lines)


def publish_pin(files: list[tuple[str, bytes]]) -> str:
    return render_pin([pin_dll(name, data) for name, data in files])


def read_build(text: str) -> str:
    for line in text.splitlines():
        if line.startswith("Build "):
            return line.removeprefix("Build ").strip().rstrip(".")
    raise ValueError("build missing")


def read_managed(text: str) -> str:
    marker = "Managed folder: `"
    for line in text.splitlines():
        if line.startswith(marker):
            return line[len(marker) :].split("`", 1)[0]
    raise ValueError("managed folder missing")


def read_pin(text: str) -> list[dict]:
    rows = []
    for line in text.splitlines():
        if not line.startswith("| "):
            continue
        cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
        if len(cells) != 2 or not cells[0].endswith(".dll"):
            continue
        rows.append({"name": cells[0], "hash": cells[1]})
    return rows
