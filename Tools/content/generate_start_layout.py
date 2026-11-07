"""Genererar Assets/_Game/Data/start_layout.csv: Lindgatan när spelaren kommer dit.

En bortglömd villagata: fem gråa hus på rad, gata och trottoarer, lyktstolpar och stora träd.
Tomterna har trasiga staket, vissna buskar, ogräs och torrt gräs (tutorialen på nivå 0).
Andra sidan gatan är tomma, grå tomter där spelaren bygger Blomsteraffären, Kvartersaffären,
lekparken och skolan.

decayed: 0 = färg, 1 = grått tills närmaste hus är renoverat, 2 = grått tills ALLA hus är klara.
Koordinater är celler à 0,5 m på kartan (96 x 96). Startområdet är x 16-79, y 20-67.

Körs:  python Tools/content/generate_start_layout.py
"""
import csv
import random
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets" / "_Game" / "Data" / "start_layout.csv"
CONTENT = ROOT / "Assets" / "_Game" / "Data" / "content.csv"

HOUSES = ["house_cottage", "house_classic", "house_twostory", "house_corner", "house_bungalow"]
HOUSE_X = [18, 31, 44, 57, 70]
HOUSE_Y = 44
ROAD_Y = 36
BACK_TREES = ["tree_birch", "tree_maple", "tree_oak", "tree_birch", "tree_maple"]


def footprints():
    rows = csv.DictReader(l for l in CONTENT.open(encoding="utf-8") if not l.startswith("#"))
    fp = {r["id"]: (int(r["w"]), int(r["h"])) for r in rows}
    fp["house_cottage"] = (10, 11)  # riktig modell
    return fp


def main():
    rnd = random.Random(7)
    fp = footprints()
    occupied = {}
    rows = []

    def fits(id_, x, y, rot=0):
        w, h = fp[id_]
        if rot % 2: w, h = h, w
        cells = [(i, j) for i in range(x, x + w) for j in range(y, y + h)]
        return all(16 <= i < 80 and 20 <= j < 68 and (i, j) not in occupied for i, j in cells), cells

    def add(id_, x, y, decayed=1, rot=0):
        ok, cells = fits(id_, x, y, rot)
        if not ok:
            raise SystemExit(f"Krock eller utanför: {id_} på ({x},{y})")
        for c in cells: occupied[c] = id_
        rows.append([id_, x, y, rot, decayed])

    def try_add(id_, x, y, decayed=0):
        if fits(id_, x, y)[0]:
            add(id_, x, y, decayed)
            return True
        return False

    # Gatan: asfalt med trottoar på båda sidor. Får färg när hela gatan är klar.
    for x in range(16, 80, 4):
        add("road_asphalt", x, ROAD_Y, decayed=2)
        add("sidewalk", x, ROAD_Y + 4, decayed=2)
        add("sidewalk", x, ROAD_Y - 1, decayed=2)

    # Lyktstolpar och stora gatuträd på södra sidan.
    for x in (20, 36, 52, 68):
        add("decor_lamp_classic", x, ROAD_Y - 2, decayed=2)
    for x in (24, 56):
        add("tree_oak", x, 24, decayed=2)

    for i, (house, hx) in enumerate(zip(HOUSES, HOUSE_X)):
        add(house, hx, HOUSE_Y)                                  # orenoverat hus
        # Framsidan: trasiga staket på var sida om gången, vissna buskar.
        add("fence_white" if i % 2 == 0 else "fence_chainlink", hx, 41)
        add("fence_white" if i % 2 == 0 else "fence_chainlink", hx + 6, 41)
        add("bush_round", hx, 42)
        add("flower_rose_pink", hx + 9, 43)
        # Mellan tomterna: vild häck (stående).
        if i < 4:
            add("hedge_wild", hx + 11, 45, rot=1)
        # Baksidan: ett grått träd.
        add(BACK_TREES[i], hx + 3, 57)

    # Ogräs och torrt gräs (tutorialen): spritt över tomterna, inte på gata eller hus.
    weeds = dry = 0
    attempts = 0
    while (weeds < 24 or dry < 18) and attempts < 5000:
        attempts += 1
        x = rnd.randint(16, 78)
        y = rnd.choice([rnd.randint(41, 43), rnd.randint(55, 66), rnd.randint(20, 33)])
        if weeds < 24 and rnd.random() < 0.55:
            if try_add("clutter_weed", x, y): weeds += 1
        elif dry < 18:
            if try_add("clutter_dry_grass", x, y): dry += 1

    OUT.parent.mkdir(parents=True, exist_ok=True)
    with OUT.open("w", newline="", encoding="utf-8") as f:
        f.write("# Genererad av Tools/content/generate_start_layout.py. decayed: 0 = färg, 1 = grått tills närmaste hus är klart, 2 = tills alla hus är klara.\n")
        w = csv.writer(f)
        w.writerow(["id", "x", "y", "rot", "decayed"])
        w.writerows(rows)
    print(f"Skrev {OUT} ({len(rows)} objekt, {weeds} ogräs, {dry} torra plättar)")


if __name__ == "__main__":
    main()
