"""Hittar och översätter spelets texter i C#-koden.

  python Tools/loc/loc_convert.py scan     listar alla texter som ska översättas (fil:rad  text)
  python Tools/loc/loc_convert.py apply    byter "text" mot Loc.T("text") och $"... {x}" mot Loc.F("... {0}", x)
  python Tools/loc/loc_convert.py table    skriver saknade rader till Assets/_Game/Data/translations.csv

Texter som redan står i Loc.T/Loc.F hoppas över, så skriptet kan köras flera gånger.
"""
import csv
import io
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "Assets" / "_Game" / "Scripts"
TABLE = ROOT / "Assets" / "_Game" / "Data" / "translations.csv"

FILES = [
    "UI/GameUI.cs", "UI/GameHud.cs",
    "Game/WalletService.cs", "Game/InventoryService.cs", "Game/ProgressionService.cs", "Game/WorldService.cs",
    "Game/RenovationService.cs", "Game/GrowthService.cs", "Game/ProductionService.cs", "Game/OrderService.cs",
    "Game/CommunityService.cs", "Game/DailyRewardService.cs", "Game/GameSession.cs",
    "Game/Retention/QuestService.cs", "Game/Retention/DailyRewardService.cs", "Game/NotificationPlanner.cs",
    "World/WorldInteraction.cs",
]

# Rader där strängar aldrig ska översättas.
SKIP_LINE = re.compile(
    r"Debug\.Log|Analytics\.Event|AddToClassList|EnableInClassList|RemoveFromClassList|PropertyToID|"
    r"\.Q\(|name = \"|Hex\(|OpenURL|ToString\(\"|StartsWith\(|EndsWith\(|Contains\(\"|Replace\(\"\{namn\}\"|"
    r"case \"|== \"|!= \"|focus ==|Loc\.(T|F)\(|Subscribe|\[Tooltip|\[Header|///|^\s*//|"
    r"PlayerPrefs|Path\.Combine|ScreenCapture|\"#\"|Layer\(\"|Panel\(\"|Dot\(\"|CreateBubble|\"_"
)

# Ord som räknas som text trots att de saknar mellanslag.
SINGLE_WORDS = {
    "Bygg", "Måla", "Klar", "Tavlan", "Foto", "Lager", "Sälj", "Meny", "Hämta", "Skörda", "Renovera",
    "Renovera!", "Flytta", "Rotera", "Bekräfta", "Okej!", "Toppen!", "Senare", "Original", "Automatisk",
    "Låg", "Mellan", "Hög", "Språk", "Grafik", "Recept", "Fasad", "Målarfärg", "Nytt:", "Gratis",
    "mynt", "diamanter", "Laddar ...", "Grannskapstavlan", "Beställningar", "Förrådet", "Inställningar",
    "Laga", "Dekorera", "Granne", "granne", "Leverera", "Släng", "Bygg", "Färdigrenoverat!", "Byggnader",
    "Blommor", "Dekor", "Staket", "Kvarter", "Växter", "Träd", "Mark", "Redo", "Lås", "Hämta", "Debug",
}

STRING_RE = re.compile(r'(\$?)"((?:[^"\\]|\\.)*)"')


def is_text(s: str) -> bool:
    if not s or not re.search(r"[A-Za-zÅÄÖåäö]", s):
        return False
    if s in SINGLE_WORDS:
        return True
    if re.fullmatch(r"[a-z0-9_\-:.]+", s):  # id, klassnamn, format
        return False
    if re.fullmatch(r"[A-Z][a-z]+\d*_[A-Za-z_]+", s):  # Phase1_Repaired, Stage0_Sprout
        return False
    return " " in s or re.search(r"[ÅÄÖåäö]", s) is not None


def parse_interpolated(body: str):
    """Delar upp innehållet i $"..." i mall + uttryck. Returnerar (mall, [uttryck]) eller None."""
    template, args = [], []
    i = 0
    while i < len(body):
        c = body[i]
        if c == "{" and body[i:i + 2] == "{{":
            template.append("{{"); i += 2; continue
        if c == "}" and body[i:i + 2] == "}}":
            template.append("}}"); i += 2; continue
        if c == "{":
            depth, j, in_str = 0, i + 1, False
            while j < len(body):
                ch = body[j]
                if in_str:
                    if ch == "\\": j += 2; continue
                    if ch == '"': in_str = False
                elif ch == '"': in_str = True
                elif ch in "([{": depth += 1
                elif ch in ")]}":
                    if depth == 0 and ch == "}": break
                    depth -= 1
                j += 1
            hole = body[i + 1:j]
            # formatspecifikator: ':' på nivå 0, utanför strängar
            d, k, cut, in_s = 0, 0, None, False
            while k < len(hole):
                ch = hole[k]
                if in_s:
                    if ch == "\\": k += 2; continue
                    if ch == '"': in_s = False
                elif ch == '"': in_s = True
                elif ch in "([{": d += 1
                elif ch in ")]}": d -= 1
                elif ch == ":" and d == 0: cut = k; break
                k += 1
            expr, fmt = (hole[:cut], hole[cut:]) if cut is not None else (hole, "")
            template.append("{" + str(len(args)) + fmt + "}")
            args.append(expr.strip())
            i = j + 1
            continue
        template.append(c)
        i += 1
    return "".join(template), args


def literal_spans(line: str):
    """Strängliteraler på raden (start, slut, interpolerad, innehåll), utan att gå in i hål."""
    out, i = [], 0
    while i < len(line):
        if line.startswith("//", i):
            break
        interp = line.startswith('$"', i)
        if line[i] == '"' or interp:
            start = i
            j = i + (2 if interp else 1)
            depth = 0
            while j < len(line):
                ch = line[j]
                if ch == "\\":
                    j += 2; continue
                if interp and ch == "{":
                    if line[j:j + 2] == "{{": j += 2; continue
                    depth += 1
                elif interp and ch == "}" and depth > 0:
                    depth -= 1
                elif ch == '"' and depth == 0:
                    break
                elif ch == '"' and depth > 0:
                    # sträng inne i ett hål: hoppa över den
                    k = j + 1
                    while k < len(line) and line[k] != '"':
                        k += 2 if line[k] == "\\" else 1
                    j = k
                j += 1
            body = line[start + (2 if interp else 1):j]
            out.append((start, j + 1, interp, body))
            i = j + 1
            continue
        i += 1
    return out


def template_of(interp, body):
    if not interp:
        return body, []
    return parse_interpolated(body)


def visible_text(interp, body):
    t, _ = template_of(interp, body)
    return re.sub(r"\{\d+(:[^}]*)?\}", "", t)


def candidates():
    for rel in FILES:
        path = SCRIPTS / rel
        if not path.exists():
            continue
        for n, line in enumerate(io.open(path, encoding="utf-8-sig").read().split("\n"), 1):
            if SKIP_LINE.search(line):
                continue
            for start, end, interp, body in literal_spans(line):
                if is_text(visible_text(interp, body).strip() if interp else body):
                    yield rel, n, start, end, interp, body


def scan():
    for rel, n, _, _, interp, body in candidates():
        t, args = template_of(interp, body)
        print(f"{rel}:{n}  {t}" + (f"   <- {', '.join(args)}" if args else ""))


def apply():
    by_file = {}
    for rel, n, start, end, interp, body in candidates():
        by_file.setdefault(rel, []).append((n, start, end, interp, body))
    for rel, items in by_file.items():
        path = SCRIPTS / rel
        lines = io.open(path, encoding="utf-8-sig").read().split("\n")
        for n, start, end, interp, body in sorted(items, key=lambda x: (x[0], -x[1])):
            line = lines[n - 1]
            if interp:
                t, args = parse_interpolated(body)
                repl = f'Loc.F("{t}", {", ".join(args)})' if args else f'Loc.T("{t}")'
            else:
                repl = f'Loc.T("{body}")'
            lines[n - 1] = line[:start] + repl + line[end:]
        text = "\n".join(lines)
        if "using NG.Core;" not in text:
            text = text.replace("using System", "using NG.Core;\nusing System", 1) if text.startswith("using System") \
                else "using NG.Core;\n" + text
        io.open(path, "w", encoding="utf-8").write(text)
        print(f"{rel}: {len(items)} texter")


def table():
    existing = {}
    if TABLE.exists():
        for row in csv.DictReader(l for l in io.open(TABLE, encoding="utf-8") if not l.startswith("#")):
            existing[row["sv"]] = row.get("en", "")
    found = []
    pat = re.compile(r'Loc\.[TF]\("((?:[^"\\]|\\.)*)"')
    for rel in FILES:
        path = SCRIPTS / rel
        if path.exists():
            for m in pat.finditer(io.open(path, encoding="utf-8-sig").read()):
                if m.group(1) not in found:
                    found.append(m.group(1))
    missing = [s for s in found if s not in existing]
    for s in missing:
        print(s)
    print(f"{len(found)} texter i koden, {len(missing)} saknar översättning")


if __name__ == "__main__":
    {"scan": scan, "apply": apply, "table": table}[sys.argv[1] if len(sys.argv) > 1 else "scan"]()
