#!/usr/bin/env python3
"""
Structural validator for the RTS-Warcraft Unity project.

Unity itself cannot run in this environment, so this script performs the checks
that would otherwise only surface when the editor imports the project:

  1. Every .cs / .shader / .unity asset has a .meta file, and every GUID is
     unique across the project.
  2. Every Unity YAML document (.unity, .asset) parses as YAML and every
     internal {fileID: N} reference resolves to an anchor declared in the file.
  3. MonoBehaviour script references in scenes point at a GUID that belongs to
     an existing .cs.meta, and the serialized fields listed in the scene match
     the public fields of that script.
  4. The scene listed in ProjectSettings/EditorBuildSettings.asset exists.

Usage:  python3 Tools/validate_project.py
Exit code 0 = clean, 1 = problems found.
"""

import os
import re
import sys

try:
    import yaml
except ImportError:  # pragma: no cover
    print("PyYAML is required:  pip install pyyaml")
    sys.exit(2)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# Unity tags documents with things like "--- !u!1 &1000000001"; teach PyYAML to
# treat any unknown tag as a plain mapping.
yaml.SafeLoader.add_multi_constructor(
    "tag:unity3d.com,2011:", lambda loader, suffix, node: loader.construct_mapping(node, deep=True)
)

ASSET_EXTENSIONS = (".cs", ".shader", ".unity", ".asset", ".prefab", ".mat", ".cginc")

GUID_RE = re.compile(r"^guid:\s*([0-9a-f]{32})\s*$", re.MULTILINE)
FILEID_RE = re.compile(r"\{fileID:\s*(-?\d+)(?:,\s*guid:\s*([0-9a-f]{32}))?[^}]*\}")
ANCHOR_RE = re.compile(r"^--- !u!(\d+) &(\d+)", re.MULTILINE)

# Unity's own always-available resources live in the default resources asset.
BUILTIN_GUIDS = {
    "0000000000000000e000000000000000",  # unity default resources
    "0000000000000000f000000000000000",  # unity default extra resources
    "0000000000000000c000000000000000",  # editor resources
    "0000000000000000b000000000000000",  # editor default resources
    "0000000000000000a000000000000000",  # unity builtin extra
    "00000000000000002000000000000000",  # unity default resources (input)
}

problems = []
warnings = []


def fail(msg):
    problems.append(msg)


def warn(msg):
    warnings.append(msg)


# ---------------------------------------------------------------------------
# 1. meta files + GUID uniqueness
# ---------------------------------------------------------------------------

def check_metas():
    guids = {}
    assets = []
    for dirpath, dirnames, filenames in os.walk(os.path.join(ROOT, "Assets")):
        for name in filenames:
            full = os.path.join(dirpath, name)
            rel = os.path.relpath(full, ROOT)
            if not name.endswith(ASSET_EXTENSIONS):
                continue
            if name.endswith(".meta"):
                continue
            assets.append(rel)

    for rel in sorted(assets):
        meta = os.path.join(ROOT, rel + ".meta")
        if not os.path.exists(meta):
            fail("missing .meta file: %s.meta" % rel)
            continue
        text = open(meta, encoding="utf8").read()
        m = GUID_RE.search(text)
        if not m:
            fail("no guid line in %s.meta" % rel)
            continue
        guid = m.group(1)
        if guid in guids:
            fail("duplicate guid %s used by %s and %s" % (guid, guids[guid], rel))
        guids[guid] = rel

    return guids


# ---------------------------------------------------------------------------
# 2. YAML parsing + fileID reference integrity
# ---------------------------------------------------------------------------

def load_unity_yaml(path):
    """Returns (documents, anchors, raw_text)."""
    text = open(path, encoding="utf8").read()
    anchors = {}
    for cls, fid in ANCHOR_RE.findall(text):
        anchors[int(fid)] = int(cls)

    docs = []
    body = re.sub(r"^%.*$", "", text, flags=re.MULTILINE)
    for chunk in body.split("\n--- "):
        chunk = chunk.strip()
        if not chunk:
            continue
        chunk = re.sub(r"^!u!\d+ &\d+\s*\n", "", chunk)
        try:
            docs.append(yaml.safe_load(chunk))
        except Exception as exc:  # noqa: BLE001
            fail("%s: YAML parse error: %s" % (path, exc))
    return docs, anchors, text


def check_yaml_file(path, guids):
    docs, anchors, text = load_unity_yaml(path)

    referenced = set()
    for fid, guid in FILEID_RE.findall(text):
        fid = int(fid)
        if guid:
            # A reference that carries a guid points into another asset, so the
            # local anchor table is not the right place to look for it.
            if guid not in guids and guid not in BUILTIN_GUIDS:
                fail("%s: references unknown asset guid %s" % (path, guid))
            continue
        if fid > 0:
            referenced.add(fid)

    for fid in sorted(referenced):
        if fid not in anchors:
            fail("%s: dangling reference to fileID %d" % (path, fid))

    return docs


# ---------------------------------------------------------------------------
# 3. MonoBehaviour script fields
# ---------------------------------------------------------------------------

def public_fields_of(script_rel):
    """Cheap extraction of the serialized (public, non-static) field names."""
    path = os.path.join(ROOT, script_rel)
    if not os.path.exists(path):
        return None
    text = open(path, encoding="utf8").read()
    fields = set()
    pattern = re.compile(
        r"^\s*(?:\[[^\]]*\]\s*)*public\s+(?!static|const|class|struct|enum|interface|void|"
        r"readonly)([A-Za-z0-9_<>,\[\]\. ]+?)\s+([A-Za-z_][A-Za-z0-9_]*)\s*(?:=[^;]+)?;",
        re.MULTILINE,
    )
    for _typ, name in pattern.findall(text):
        fields.add(name)
    return fields


def check_monobehaviours(path, docs, guids):
    for doc in docs:
        if not isinstance(doc, dict):
            continue
        for key, value in doc.items():
            if key != "MonoBehaviour" or not isinstance(value, dict):
                continue
            script = value.get("m_Script") or {}
            guid = script.get("guid")
            if not guid:
                continue
            script_rel = guids.get(guid)
            if script_rel is None:
                fail("%s: MonoBehaviour m_Script guid %s does not exist" % (path, guid))
                continue

            fields = public_fields_of(script_rel)
            if fields is None:
                warn("%s: script %s not found for field comparison" % (path, script_rel))
                continue

            serialized = set(value.keys()) - {
                "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance",
                "m_PrefabAsset", "m_GameObject", "m_Enabled", "m_EditorHideFlags",
                "m_Script", "m_Name", "m_EditorClassIdentifier",
            }
            for name in sorted(serialized):
                if name not in fields:
                    fail("%s: scene sets '%s' but %s has no such public field"
                         % (path, name, script_rel))


# ---------------------------------------------------------------------------
# 4. build settings
# ---------------------------------------------------------------------------

def check_build_settings():
    path = os.path.join(ROOT, "ProjectSettings", "EditorBuildSettings.asset")
    if not os.path.exists(path):
        warn("no ProjectSettings/EditorBuildSettings.asset (Unity will create one)")
        return
    text = open(path, encoding="utf8").read()
    for scene_path in re.findall(r"path:\s*(Assets/[^\s]+\.unity)", text):
        if not os.path.exists(os.path.join(ROOT, scene_path)):
            fail("EditorBuildSettings lists missing scene %s" % scene_path)


def main():
    print("Validating Unity project at %s" % ROOT)

    guids = check_metas()
    print("  assets with .meta: %d, unique guids: %d" % (len(guids), len(set(guids.values()))))

    yaml_assets = []
    for dirpath, _dirnames, filenames in os.walk(os.path.join(ROOT, "Assets")):
        for name in filenames:
            if name.endswith((".unity", ".asset", ".prefab")):
                yaml_assets.append(os.path.relpath(os.path.join(dirpath, name), ROOT))
    for dirpath, _dirnames, filenames in os.walk(os.path.join(ROOT, "ProjectSettings")):
        for name in filenames:
            if name.endswith(".asset"):
                yaml_assets.append(os.path.relpath(os.path.join(dirpath, name), ROOT))

    for rel in sorted(yaml_assets):
        docs = check_yaml_file(os.path.join(ROOT, rel), guids)
        check_monobehaviours(rel, docs, guids)
    print("  unity yaml documents checked: %d" % len(yaml_assets))

    check_build_settings()

    if warnings:
        print("\nWarnings (%d):" % len(warnings))
        for w in warnings:
            print("  ! %s" % w)

    if problems:
        print("\nPROBLEMS (%d):" % len(problems))
        for p in problems:
            print("  x %s" % p)
        return 1

    print("\nOK - no structural problems found.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
