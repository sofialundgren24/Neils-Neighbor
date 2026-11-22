"""Listar svenska texter i koden som ännu inte går via Loc.T / Loc.F.

  python Tools/loc/find_unwrapped.py
"""
import glob
import io
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
os.chdir(os.path.join(ROOT, "Assets", "_Game", "Scripts"))

STRING = re.compile(r'(\$?)"((?:[^"\\]|\\.)*)"')
SKIP = re.compile(r"Debug\.Log|///|^\s*//|Tooltip|Header|Analytics|Exception")
files = glob.glob("Game/**/*.cs", recursive=True) + ["UI/GameUI.cs"] + glob.glob("World/*.cs")
for f in files:
    for n, line in enumerate(io.open(f, encoding="utf-8-sig").read().split("\n"), 1):
        if SKIP.search(line):
            continue
        for m in STRING.finditer(line):
            s = m.group(2)
            before = line[:m.start()]
            if before.endswith("Loc.T(") or before.endswith("Loc.F("):
                continue
            if re.search(r"[A-Za-zÅÄÖåäö]{2,}", s) and (" " in s or re.search(r"[ÅÄÖåäö]", s)):
                print(f"{f}:{n}: {line.strip()[:160]}")
                break
