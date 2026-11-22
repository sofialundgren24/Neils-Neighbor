"""Kompilerar spelets skript med Unitys egen C#-kompilator, utan att öppna Unity.

Används för att hitta kompileringsfel innan man byter till Unity.
Kräver att Unity har öppnat projektet minst en gång (så att .csproj-filerna och Library/ finns).

  python Tools/dev/compile_check.py
"""
import glob
import os
import re
import subprocess
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
UNITY = os.environ.get("UNITY_EDITOR_DATA", r"C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data")


def main():
    os.chdir(ROOT)
    refs, defs = set(), ""
    for proj in ["Assembly-CSharp.csproj", "Assembly-CSharp-Editor.csproj"]:
        text = open(proj, encoding="utf-8").read()
        for m in re.findall(r"<HintPath>([^<]+)</HintPath>", text):
            if not os.path.basename(m).startswith("Assembly-CSharp") and os.path.exists(m):
                refs.add(m)
        defs = re.search(r"<DefineConstants>([^<]*)", text).group(1)

    out_dir = tempfile.mkdtemp()
    lines = ["-nologo", "-target:library", "-langversion:9.0", "-nostdlib+",
             "-nowarn:1701,1702,0649,0169,0414", f"-out:{out_dir}/check.dll", "-define:" + defs]
    seen = set()
    for r in sorted(refs, key=lambda r: ("NetStandard" in r, r)):
        b = os.path.basename(r).lower()
        if b in seen:
            continue
        seen.add(b)
        lines.append(f'-r:"{r}"')
    lines += [f'"{s}"' for s in glob.glob("Assets/_Game/Scripts/**/*.cs", recursive=True)]
    rsp = os.path.join(out_dir, "check.rsp")
    open(rsp, "w", encoding="utf-8").write("\n".join(lines))

    dotnet = os.path.join(UNITY, "DotNetSdk", "dotnet.exe")
    csc = glob.glob(os.path.join(UNITY, "DotNetSdk", "sdk", "*", "Roslyn", "bincore", "csc.dll"))[0]
    result = subprocess.run([dotnet, csc, "@" + rsp], capture_output=True, text=True, encoding="utf-8", errors="replace")
    issues = [l.replace(ROOT + os.sep, "") for l in result.stdout.splitlines()
              if ("error" in l or "warning CS" in l) and "CS2023" not in l]
    print("\n".join(issues[:40]) if issues else "Inga fel.")
    sys.exit(result.returncode)


if __name__ == "__main__":
    main()
