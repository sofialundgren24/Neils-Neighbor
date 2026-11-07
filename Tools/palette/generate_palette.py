"""Genererar T_Palette_256.png från palette.json.

Varje ruta (16x16 px) är en färg med en mjuk vertikal ljusgradient:
ljusare upptill, mörkare nertill. UV-mappa en yta mot rutans mitt för
"platt" färg, eller sträck den vertikalt inom rutan för en inbyggd gradient.

Körs:  python Tools/palette/generate_palette.py
"""
import colorsys
import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SRC = Path(__file__).with_name("palette.json")
OUT = ROOT / "Assets" / "_Game" / "Art" / "Textures" / "T_Palette_256.png"

GRADIENT = 0.12  # +/- ljushet (HSV value) från rutans topp till botten


def hex_to_rgb(h: str):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


def main():
    data = json.loads(SRC.read_text(encoding="utf-8"))
    grid, cell = data["gridSize"], data["cellPixels"]
    colors = data["colors"]
    if len(colors) > grid * grid:
        raise SystemExit(f"För många färger: {len(colors)} > {grid * grid}")
    names = [c["name"] for c in colors]
    if len(set(names)) != len(names):
        raise SystemExit("Dubbla färgnamn i palette.json")

    size = grid * cell
    img = Image.new("RGB", (size, size), (255, 0, 255))  # magenta = oanvänd ruta
    px = img.load()
    for index, c in enumerate(colors):
        col, row = index % grid, index // grid
        h, s, v = colorsys.rgb_to_hsv(*hex_to_rgb(c["hex"]))
        for y in range(cell):
            t = (y + 0.5) / cell  # 0 = topp, 1 = botten
            vv = max(0.0, min(1.0, v + GRADIENT * (0.5 - t) * 2))
            r, g, b = colorsys.hsv_to_rgb(h, s, vv)
            rgb = (round(r * 255), round(g * 255), round(b * 255))
            for x in range(cell):
                px[col * cell + x, row * cell + y] = rgb
    OUT.parent.mkdir(parents=True, exist_ok=True)
    img.save(OUT)
    print(f"Skrev {OUT} ({len(colors)} färger)")


if __name__ == "__main__":
    main()
