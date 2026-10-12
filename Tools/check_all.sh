#!/usr/bin/env bash
# Full offline verification pass for the RTS-Warcraft project.
#
#   1. cs_symbols.py     - build a symbol table from the C# sources
#   2. tree-sitter parse - every .cs file must parse without ERROR nodes
#   3. cs_xref.py        - static/enum access + struct initialiser cross-check
#   4. validate_project  - .meta/GUID + Unity YAML + scene->script wiring
#
# Unity itself cannot run here, so these are the strongest checks available.
set -uo pipefail

PY=${PYTHON:-python3}
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(dirname "$HERE")"
cd "$ROOT" || exit 2

fail=0

echo "== 1/4  building C# symbol table"
"$PY" Tools/cs_symbols.py || fail=1

echo
echo "== 2/4  parsing every C# file (tree-sitter)"
"$PY" - <<'PYCODE' || fail=1
import os
import tree_sitter_c_sharp as tscs
from tree_sitter import Language, Parser

lang = Language(tscs.language())
parser = Parser(lang)
bad = 0
count = 0
for root, _dirs, files in os.walk('Assets'):
    for name in sorted(files):
        if not name.endswith('.cs'):
            continue
        path = os.path.join(root, name)
        count += 1
        src = open(path, 'rb').read()
        tree = parser.parse(src)
        errors = []
        def walk(node):
            if node.type == 'ERROR' or node.is_missing:
                errors.append(node.start_point[0] + 1)
            for child in node.children:
                walk(child)
        walk(tree.root_node)
        if errors:
            bad += 1
            print("  SYNTAX ERROR %s at lines %s" % (path, errors[:6]))
print("  parsed %d files, %d with syntax errors" % (count, bad))
raise SystemExit(1 if bad else 0)
PYCODE

echo
echo "== 3/4  cross-references"
"$PY" Tools/cs_xref.py || fail=1

echo
echo "== 4/4  Unity project structure"
"$PY" Tools/validate_project.py || fail=1

echo
if [ "$fail" -eq 0 ]; then echo "ALL CHECKS PASSED"; else echo "CHECKS FAILED"; fi
exit $fail
