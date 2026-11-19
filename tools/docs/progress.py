#!/usr/bin/env python3
"""Regenera el progreso del curso y la navegación entre guías.

Fuente única: docs/flow/chapters.json. Un capítulo cuenta como replicado
cuando existe su guía docs/flow/flowN.md. Reescribe solo lo que hay entre
los marcadores <!-- progress:start --> / <!-- progress:end --> (README.md),
<!-- chapters:start --> / <!-- chapters:end --> (docs/flow/README.md) y
<!-- nav:start --> / <!-- nav:end --> (cada docs/flow/flowN.md).

Uso:
  python3 tools/docs/progress.py           # actualiza los archivos
  python3 tools/docs/progress.py --check   # falla si algo está desactualizado
"""
import json
import os
import pathlib
import re
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
FLOW = ROOT / "docs" / "flow"
ORIGINAL = "https://github.com/ElectNewt/Distribt/tree/"


def current_branch():
    # En GitHub Actions el checkout queda en detached HEAD: el workflow pasa la rama aquí.
    if os.environ.get("PROGRESS_BRANCH"):
        return os.environ["PROGRESS_BRANCH"]
    try:
        return subprocess.run(["git", "branch", "--show-current"], cwd=ROOT,
                              capture_output=True, text=True, check=True).stdout.strip()
    except (OSError, subprocess.CalledProcessError):
        return ""


def load():
    chapters = json.loads((FLOW / "chapters.json").read_text(encoding="utf-8"))
    for c in chapters:
        c["guide"] = f"flow{c['flow']}.md"
        c["branch"] = f"features/flow{c['flow']}"
        c["done"] = (FLOW / c["guide"]).exists()
    return chapters


def here(chapters, branch):
    """Capítulo de la rama actual. En integration-flow y main no se marca ninguno,
    para que el marcador no provoque conflictos al mergear cada capítulo."""
    return next((c for c in chapters if c["done"] and c["branch"] == branch), None)


def size(chapter):
    """Cuántos capítulos del curso cubre una entrada ("3–4" son dos)."""
    first, _, last = chapter.partition("–")
    return int(last or first) - int(first) + 1


def progress_block(chapters, branch):
    mark = here(chapters, branch)
    done = sum(size(c["chapter"]) for c in chapters if c["done"])
    total = sum(size(c["chapter"]) for c in chapters)
    lines = ["Cada capítulo se replica en su propia rama `features/flowN` y tiene su guía en `docs/flow`. "
             f"**{done} de {total} capítulos** replicados.", ""]
    for c in chapters:
        line = f"- [{'x' if c['done'] else ' '}] {c['chapter']} · {c['title']}"
        if c["done"]:
            line += f" · [guía](docs/flow/{c['guide']})"
        if c is mark:
            line += " **← esta rama**"
        lines.append(line)
    return "\n".join(lines)


def chapters_block(chapters):
    rows = ["| # | Guía | Rama de Scattered | Rama original | Qué cambia |", "|---|---|---|---|---|"]
    for c in chapters:
        if c["done"]:
            rows.append(f"| {c['chapter']} | [{c['short']}]({c['guide']}) | `{c['branch']}` | "
                        f"[`{c['original']}`]({ORIGINAL}{c['original']}) | {c['summary']} |")
    pending = next((c for c in chapters if not c["done"]), None)
    if pending:
        rows += ["", f"Siguiente: capítulo {pending['chapter']} · {pending['short']} "
                     f"(rama `{pending['branch']}`, original [`{pending['original']}`]({ORIGINAL}{pending['original']}))."]
    return "\n".join(rows)


def nav_block(chapters, i):
    c = chapters[i]
    prev_ = chapters[i - 1] if i > 0 else None
    next_ = chapters[i + 1] if i + 1 < len(chapters) else None
    parts = []
    if prev_:
        parts.append(f"[← Capítulo {prev_['chapter']}]({prev_['guide']})")
    parts.append("[Índice del flujo](README.md)")
    if next_:
        if next_["done"]:
            parts.append(f"[Siguiente: capítulo {next_['chapter']} →]({next_['guide']})")
        else:
            parts.append(f"Siguiente: capítulo {next_['chapter']} (pendiente)")
    steps = []
    for o in chapters:
        if o is c:
            steps.append(f"**{o['chapter']}**")
        elif o["done"]:
            steps.append(f"[{o['chapter']}]({o['guide']})")
        else:
            steps.append(o["chapter"])
    return " · ".join(parts) + "\n\nProgreso: " + " · ".join(steps)


def replace(text, name, body, path):
    pattern = re.compile(rf"(<!-- {name}:start[^>]*-->\n).*?(<!-- {name}:end -->)", re.S)
    if not pattern.search(text):
        sys.exit(f"{path}: faltan los marcadores <!-- {name}:start --> / <!-- {name}:end -->")
    return pattern.sub(lambda m: m.group(1) + body + "\n" + m.group(2), text)


def main():
    check = "--check" in sys.argv
    chapters = load()
    branch = current_branch()
    targets = [(ROOT / "README.md", "progress", progress_block(chapters, branch)),
               (FLOW / "README.md", "chapters", chapters_block(chapters))]
    targets += [(FLOW / c["guide"], "nav", nav_block(chapters, i))
                for i, c in enumerate(chapters) if c["done"]]

    stale = []
    for path, name, body in targets:
        old = path.read_text(encoding="utf-8")
        new = replace(old, name, body, path)
        if new != old:
            stale.append(path.relative_to(ROOT))
            if not check:
                path.write_text(new, encoding="utf-8")

    if check and stale:
        sys.exit("Progreso desactualizado en: " + ", ".join(map(str, stale)) +
                 "\nEjecuta: python3 tools/docs/progress.py")
    for p in stale:
        print(f"actualizado {p}")


if __name__ == "__main__":
    main()
