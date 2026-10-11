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

sys.exit(1 if out else 0)
