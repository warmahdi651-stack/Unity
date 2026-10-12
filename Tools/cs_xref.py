"""Cross-reference check: static/enum member access and struct-initialiser fields.

Reports (a) Type.Member where Member is not static on Type, (b) Type.Member that
does not exist at all, and (c) `new Type { field = ... }` where field is not a
member of Type.  Requires cs_symbols.py to have been run first.
"""
import os, re, json, sys
HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(HERE)
os.chdir(PROJECT)
S = json.load(open(os.environ.get('RTS_SYMBOLS', '/tmp/symbols.json')))
declared = S['declared']; members = S['members']; statics = S['statics']

files = []
for root, dirs, fs in os.walk('Assets'):
    for f in sorted(fs):
        if f.endswith('.cs'):
            files.append(os.path.join(root, f))

IGNORE = set("""Time Mathf Vector2 Vector3 Quaternion Color Color32 GameObject Object Debug Application
Random Resources LayerMask Screen Input Physics Shader Material Mesh Texture2D FilterMode TextureWrapMode
TextureFormat RenderMode CameraClearFlags LightType LightShadows FogMode RenderSettings ShadowCastingMode
BlendMode IndexFormat Camera Light Canvas CanvasScaler GraphicRaycaster EventSystem PointerEventData
RaycastResult RectTransform RectTransformUtility Image Text RawImage Button Outline Rect Font FontStyle
TextAnchor HorizontalWrapMode VerticalWrapMode WaitForSeconds List Dictionary Math System Action
KeyValuePair IEnumerator MonoBehaviour Component Transform Renderer Collider SphereCollider BoxCollider
MeshFilter MeshRenderer Ray RaycastHit Vector2Int Vector4 Bounds EventSystems UnityEngine RtsWarcraft UI
Rendering StandaloneInputModule ScaleMode FillMethod Type AmbientMode String Gizmos GUI GUILayout Not
IComparer StringComparison IEnumerator""".split())

problems = []

# 1) Type.Member  (static/enum access)
pat = re.compile(r'\b([A-Z][A-Za-z0-9_]*)\.([A-Za-z_][A-Za-z0-9_]*)')
for f in files:
    for i, line in enumerate(open(f, encoding='utf8'), 1):
        s = line.split('//')[0]
        for m in pat.finditer(s):
            t, mem = m.group(1), m.group(2)
            if t in IGNORE or t not in declared:
                continue
            if mem in statics.get(t, []):
                continue
            if mem in members.get(t, []):
                problems.append((f, i, "INSTANCE-AS-STATIC", "%s.%s (member is not static)" % (t, mem)))
            else:
                problems.append((f, i, "NO-MEMBER", "%s.%s does not exist" % (t, mem)))

# 2) struct/object initializer fields
init_pat = re.compile(r'new\s+([A-Z][A-Za-z0-9_]*)\s*(?:<[^>]*>)?\s*\{')
for f in files:
    lines = open(f, encoding='utf8').read().split('\n')
    for i, line in enumerate(lines):
        s = line.split('//')[0]
        for m in init_pat.finditer(s):
            t = m.group(1)
            if t in ('List', 'Dictionary') or t not in members:
                continue
            for j in range(i, min(i + 90, len(lines))):
                body = lines[j].split('//')[0]
                if j > i and body.strip().startswith('};'):
                    break
                for fm in re.finditer(r'(?:^|[,{])\s*([a-zA-Z_][A-Za-z0-9_]*)\s*=[^=]', body):
                    fld = fm.group(1)
                    if fld in members[t]:
                        continue
                    problems.append((f, j + 1, "BAD-INIT", "%s { %s = ... } is not a member of %s" % (t, fld, t)))

seen = set(); out = []
for pr in problems:
    k = (pr[0], pr[1], pr[3])
    if k in seen:
        continue
    seen.add(k); out.append(pr)

print("cross-reference findings:", len(out))
for f, i, kind, msg in out[:80]:
    print("  %s:%d  [%s] %s" % (f, i, kind, msg))


# ===========================================================================
#  pass 3 - array rank check:  declared  byte[,] x  must never be read as
#  x[i], and declared byte[] y must never be read as y[a, b].
# ===========================================================================

def top_level_commas(expr):
    depth = 0
    n = 0
    for ch in expr:
        if ch in '([{':
            depth += 1
        elif ch in ')]}':
            depth -= 1
        elif ch == ',' and depth == 0:
            n += 1
    return n

def bracket_expr(src, idx):
    """Return the text between the brackets starting at src[idx]=='['."""
    depth = 0
    for j in range(idx, len(src)):
        if src[j] == '[':
            depth += 1
        elif src[j] == ']':
            depth -= 1
            if depth == 0:
                return src[idx + 1:j]
    return ''

array_ranks = {}
for f in files:
    src = open(f, encoding='utf8').read()
    for m in re.finditer(r'(\[(?:,\s*)*\])\s*([a-z_][A-Za-z0-9_]*)\s*(?:=|;|,|\))', src):
        # inner text of the declaration brackets: '[]' -> rank 1, '[,]' -> 2
        rank = top_level_commas(m.group(1)[1:-1]) + 1
        array_ranks.setdefault(m.group(2), rank)

rank_problems = []
for f in files:
    src = open(f, encoding='utf8').read()
    lines = src.split('\n')
    pos = 0
    for i, line in enumerate(lines, 1):
        for m in re.finditer(r'(?<![A-Za-z0-9_\.])([a-z_][A-Za-z0-9_]*)\[', line):
            name = m.group(1)
            if name not in array_ranks:
                continue
            expr = bracket_expr(line, m.end() - 1)
            if expr == '':
                continue
            used = top_level_commas(expr) + 1
            if used != array_ranks[name]:
                rank_problems.append((f, i, "RANK", "%s declared rank %d but indexed with %d" % (name, array_ranks[name], used)))

# ===========================================================================
#  pass 4 - List<T> generic misuse:  new List<A>(varOfListB)  and
#  listOfA.Add(listOrWrongTypedThing) where types are both known and differ.
# ===========================================================================

list_types = {}
for f in files:
    src = open(f, encoding='utf8').read()
    for m in re.finditer(r'\bList<([A-Za-z0-9_]+)>\s+([a-z_][A-Za-z0-9_]*)', src):
        list_types.setdefault(m.group(2), m.group(1))

generic_problems = []
for f in files:
    src = open(f, encoding='utf8').read()
    lines = src.split('\n')
    for i, line in enumerate(lines, 1):
        for m in re.finditer(r'new\s+List<([A-Za-z0-9_]+)>\s*\(\s*([a-z_][A-Za-z0-9_]*)\s*\)', line):
            elem, var = m.group(1), m.group(2)
            have = list_types.get(var)
            if have and have != elem:
                generic_problems.append((f, i, "LIST-CTOR", "new List<%s>(%s) but %s is List<%s>" % (elem, var, var, have)))

all_problems = out + rank_problems + generic_problems
print("array-rank findings:", len(rank_problems))
for f, i, kind, msg in rank_problems:
    print("  %s:%d  [%s] %s" % (f, i, kind, msg))
print("list-generic findings:", len(generic_problems))
for f, i, kind, msg in generic_problems:
    print("  %s:%d  [%s] %s" % (f, i, kind, msg))

sys.exit(1 if all_problems else 0)
