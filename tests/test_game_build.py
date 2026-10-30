"""The game-build pin records each Managed DLL name and hash."""

from __future__ import annotations

import importlib.util
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
PIN_PATH = REPO / "doc" / "requirements" / "game-build.md"
GAME_BUILD_PATH = REPO / "script" / "game_build.py"

BIG_AMBITIONS_DLL = "9427561b81aaab2ada09c22a3c98dd9a4017a65225b389513885bf336852e911"
MODAPI_DLL = "0ab90c0473684ce43cbe22c4049eb509373ab0d8a80216bf882a07d50c876983"
IMGUI_DLL = "b9720d33d0709863fcc8b10a4f327e3bbf082486e4b544917393f7ac8a78e460"
SAMPLE_DLL = "016dd39f8914e64478f2016d0e432b2d7de9194a3ad6354c88bb53e931ca12d9"
SAMPLE_API = "14c2529eb4498c5d1ffd6915d05bf58a91bdda796af59f41d480d11c099d0479"
HASH_A = "ca978112ca1bbdcafac231b39a23dc4da786eff8147c4e72b9807785afee48bb"
HASH_B = "3e23e8160039594a33894f6564e1b1348bbd7a0088d42c4acb73eeaed59c009d"


def _load(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


game_build = _load("game_build", GAME_BUILD_PATH)


def test_GameBuild_locks_the_dll_hash():
    pins = game_build.read_pin(PIN_PATH.read_text(encoding="utf-8"))
    row = next(item for item in pins if item["name"] == "BigAmbitions.dll")
    assert row["hash"] == BIG_AMBITIONS_DLL


def test_GameBuild_locks_the_modapi_hash():
    pins = game_build.read_pin(PIN_PATH.read_text(encoding="utf-8"))
    row = next(item for item in pins if item["name"] == "BigAmbitions.ModAPI.dll")
    assert row["hash"] == MODAPI_DLL


def test_GameBuild_locks_the_imgui_hash():
    pins = game_build.read_pin(PIN_PATH.read_text(encoding="utf-8"))
    row = next(item for item in pins if item["name"] == "UnityEngine.IMGUIModule.dll")
    assert row["hash"] == IMGUI_DLL


def test_GameBuild_locks_build_3682():
    assert game_build.read_build(PIN_PATH.read_text(encoding="utf-8")) == "3682"


def test_GameBuild_locks_the_managed_folder():
    text = PIN_PATH.read_text(encoding="utf-8")
    assert game_build.read_managed(text) == "Big Ambitions_Data\\Managed"


def test_GameBuild_a_different_byte_changes_the_hash():
    first = game_build.pin_dll("BigAmbitions.dll", b"a")
    second = game_build.pin_dll("BigAmbitions.dll", b"b")
    assert first["name"] == "BigAmbitions.dll"
    assert first["hash"] == HASH_A
    assert second["hash"] == HASH_B


def test_chain_dll_pin():
    """Chain: dll → pin"""
    text = game_build.publish_pin([("BigAmbitions.dll", b"managed-dll")])
    assert "BigAmbitions.dll" in text
    assert SAMPLE_DLL in text


def test_chain_dll_pin_names_each_hash():
    """Chain: dll → pin names"""
    text = game_build.publish_pin(
        [
            ("BigAmbitions.dll", b"managed-dll"),
            ("BigAmbitions.ModAPI.dll", b"api"),
        ]
    )
    assert f"| BigAmbitions.dll | {SAMPLE_DLL} |" in text
    assert f"| BigAmbitions.ModAPI.dll | {SAMPLE_API} |" in text
    assert SAMPLE_DLL != SAMPLE_API
