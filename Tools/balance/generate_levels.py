"""Genererar Assets/_Game/Data/levels.csv med XP-kurvan från GDD:n.

XP till nästa nivå = round(50 * L^1.55 + 25 * L)
Belöningar: mynt = 50 + 30*L, diamanter = 2 (+3 var 5:e nivå).
Upplåsningar styrs INTE här utan av kolumnen unlock i Assets/_Game/Data/content.csv och recipes.csv.
Kolumnen unlock_ids finns kvar för specialfall (t.ex. händelser) och är tom som standard.

Körs:  python Tools/balance/generate_levels.py [antal_nivåer]
"""
import csv
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets" / "_Game" / "Data" / "levels.csv"

UNLOCKS = {}


def xp_to_next(level: int) -> int:
    return round(50 * level ** 1.55 + 25 * level)


def main():
    count = int(sys.argv[1]) if len(sys.argv) > 1 else 20
    OUT.parent.mkdir(parents=True, exist_ok=True)
    with OUT.open("w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["level", "xp_to_next", "coin_reward", "gem_reward", "unlock_ids"])
        for lvl in range(1, count + 1):
            gems = 2 + (3 if lvl % 5 == 0 else 0)
            w.writerow([lvl, xp_to_next(lvl), 50 + 30 * lvl, gems, ";".join(UNLOCKS.get(lvl, []))])
    print(f"Skrev {OUT} ({count} nivåer)")


if __name__ == "__main__":
    main()
