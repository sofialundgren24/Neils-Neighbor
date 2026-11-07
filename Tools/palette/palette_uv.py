"""Gemensam hjälpare: färgnamn -> UV-koordinat i T_Palette_256.png.

Används av Blender-skripten. UV har origo nere till vänster (Blender/Unity),
bildens rad 0 ligger överst, därav 1 - v.
"""
import json
from pathlib import Path

_DATA = json.loads(Path(__file__).with_name("palette.json").read_text(encoding="utf-8"))
_GRID = _DATA["gridSize"]
_INDEX = {c["name"]: i for i, c in enumerate(_DATA["colors"])}


def uv_for(name: str, along: float = 0.5):
    """UV i rutans mitt. `along` (0..1) väljer höjd i rutans gradient (1 = ljusast)."""
    if name not in _INDEX:
        raise KeyError(f"Okänd palettfärg '{name}'. Finns: {sorted(_INDEX)}")
    i = _INDEX[name]
    col, row = i % _GRID, i // _GRID
    u = (col + 0.5) / _GRID
    # Håll oss 3 px från rutkanten (av 16) så att mipmaps inte blöder in grannfärger.
    inset = 3 / 16
    t = inset + (1 - 2 * inset) * along
    v = 1 - (row + 1 - t) / _GRID
    return (u, v)


def names():
    return list(_INDEX)
