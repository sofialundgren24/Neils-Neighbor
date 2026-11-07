"""Genererar Assets/_Game/Data/start_layout.csv: den nedgångna startgatan.

Fem orenoverade hus på rad, en gata framför, och grå trädgårdar (buskar, staket, brevlådor,
vissna tulpaner, lyktor, träd) som får färg när närmaste hus är färdigrenoverat.
Koordinater är celler à 0,5 m på kartan (96 x 96). Startområdet är x 16-79, y 20-67.

Körs:  python Tools/content/generate_start_layout.py
Redigera gärna CSV:en direkt i stället om du vill flytta enskilda saker.
"""
import csv
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets" / "_Game" / "Data" / "start_layout.csv"

HOUSES = ["house_cottage", "house_townhouse", "house_bungalow", "house_villa", "house_cabin"]
HOUSE_X = [18, 31, 44, 57, 70]
HOUSE_Y = 44
ROAD_Y = 36


def main():
    rows = []
    add = lambda id_, x, y, decayed=1, rot=0: rows.append([id_, x, y, rot, decayed])

    # Gatan (inte grå: den lagas inte, husen gör det).
    for x in range(16, 80, 4):
        add("road_asphalt", x, ROAD_Y, decayed=0)

    for i, (house, hx) in enumerate(zip(HOUSES, HOUSE_X)):
        add(house, hx, HOUSE_Y)                       # orenoverat hus
        add("bush_round", hx, 40)                     # buskar vid tomtgränsen
        add("bush_round", hx + 8, 40)
        add("path_stone", hx + 4, 40)                 # gång fram till dörren
        add("path_stone", hx + 4, 42)
        add("decor_mailbox", hx + 7, 41)
        add("fence_white_picket", hx, 43)             # staket på var sida om gången
        add("fence_white_picket", hx + 6, 43)
        add("flower_tulip_yellow", hx + 2, 41)        # vissna tulpaner
        add("flower_tulip_yellow", hx + 3, 41)
        if i > 0:
            add("decor_lamp", hx - 2, 41)              # lykta mellan tomterna
            add("bush_round", hx - 3, 46)              # buske mellan husen
        # Träd bakom husen
        add("tree_oak" if i % 2 == 0 else "tree_birch", hx + 3, 56)

    OUT.parent.mkdir(parents=True, exist_ok=True)
    with OUT.open("w", newline="", encoding="utf-8") as f:
        f.write("# Genererad av Tools/content/generate_start_layout.py. decayed = 1: grå från start (hus = orenoverat).\n")
        w = csv.writer(f)
        w.writerow(["id", "x", "y", "rot", "decayed"])
        w.writerows(rows)
    print(f"Skrev {OUT} ({len(rows)} objekt)")


if __name__ == "__main__":
    main()
