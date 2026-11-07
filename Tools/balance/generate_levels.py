"""Genererar Assets/_Game/Data/levels.csv.

Nivå 0 är tutorialen (rensa ogräs, vattna gräs). De första nivåerna följer designen:
  Nivå 1 vid 50 XP totalt, nivå 2 vid 150, nivå 3 vid 300, nivå 4 vid 550, nivå 5 vid 900.
Därefter: XP till nästa nivå = round(0.55 * (50 * L^1.55 + 25 * L)), så kurvan fortsätter mjukt.
Belöning: mynt = 50 + 30*L, diamanter = 2 (+3 var 5:e nivå).

Upplåsningar styrs INTE här utan av kolumnen unlock i content.csv, recipes.csv och facades.csv.

Körs:  python Tools/balance/generate_levels.py [högsta_nivå]
"""
import csv
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets" / "_Game" / "Data" / "levels.csv"

# Totala XP-trösklar från designen: nivå -> total XP för att nå den.
DESIGN_TOTALS = {1: 50, 2: 150, 3: 300, 4: 550, 5: 900}


def xp_to_next(level: int) -> int:
    if level + 1 in DESIGN_TOTALS:
        return DESIGN_TOTALS[level + 1] - DESIGN_TOTALS.get(level, 0)
    return round(0.55 * (50 * level ** 1.55 + 25 * level))


def main():
    top = int(sys.argv[1]) if len(sys.argv) > 1 else 30
    OUT.parent.mkdir(parents=True, exist_ok=True)
    with OUT.open("w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["level", "xp_to_next", "coin_reward", "gem_reward", "unlock_ids"])
        for lvl in range(0, top + 1):
            gems = 0 if lvl == 0 else 2 + (3 if lvl % 5 == 0 else 0)
            coins = 0 if lvl == 0 else 50 + 30 * lvl
            w.writerow([lvl, xp_to_next(lvl), coins, gems, ""])
    print(f"Skrev {OUT} (nivå 0-{top})")


if __name__ == "__main__":
    main()
