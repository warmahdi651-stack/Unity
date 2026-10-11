"""Extract a symbol table (types, members, statics) from every C# file using tree-sitter.

Used by cs_xref.py. Requires:  pip install tree_sitter tree_sitter_c_sharp
"""
import os, re, json, sys
import tree_sitter_c_sharp as tscs
from tree_sitter import Language, Parser

lang = Language(tscs.language())
p = Parser(lang)

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.join(os.path.dirname(HERE), 'Assets')

files = []
for root, dirs, fs in os.walk(ROOT):
    for f in sorted(fs):
        if f.endswith('.cs'):
            files.append(os.path.join(root, f))

TYPE_NODES = ('class_declaration', 'struct_declaration', 'interface_declaration',
              'enum_declaration', 'record_declaration')
MEMBER_NODES = ('method_declaration', 'property_declaration', 'field_declaration',
                'event_declaration', 'constructor_declaration', 'indexer_declaration',
                'delegate_declaration', 'operator_declaration', 'conversion_operator_declaration')

declared = {}
members = {}
statics = {}
kinds = {}


def name_of(node):
    """Correct name for a declaration node."""
    t = node.type
    if t == 'method_declaration':
        for i, c in enumerate(node.children):
            if c.type == 'parameter_list':
                for j in range(i - 1, -1, -1):
                    if node.children[j].type in ('identifier', 'generic_name'):
                        return node.children[j].text.decode().split('<')[0]
                break
    if t in ('property_declaration', 'indexer_declaration', 'event_declaration'):
        for c in reversed(node.children):
            if c.type == 'identifier':
                return c.text.decode()
        for c in node.children:
            if c.type == 'identifier':
                return c.text.decode()
    if t in ('class_declaration', 'struct_declaration', 'interface_declaration',
             'enum_declaration', 'record_declaration', 'constructor_declaration',
             'delegate_declaration', 'enum_member_declaration'):
        for c in node.children:
            if c.type == 'identifier':
                return c.text.decode()
    if t == 'field_declaration':
        return None
    return None


def field_names(node):
    out = []
    stack = [node]
    while stack:
        n = stack.pop()
        if n.type == 'variable_declarator':
            for c in n.children:
                if c.type == 'identifier':
                    out.append(c.text.decode())
                    break
        else:
            stack.extend(n.children)
    return out


def is_static(node):
    head = node.text.decode().split('{')[0].split(';')[0]
    return re.search(r'\b(static|const)\b', head) is not None


def add(owner, nm, st, kind):
    if owner is None or nm is None:
        return
    members.setdefault(owner, set()).add(nm)
    kinds[(owner, nm)] = kind
    if st:
        statics.setdefault(owner, set()).add(nm)


def walk_body(body, owner):
    for ch in body.children:
        if ch.type in TYPE_NODES:
            nm = name_of(ch)
            if nm:
                declared.setdefault(nm, 'nested')
                b = None
                for c in ch.children:
                    if c.type in ('declaration_list', 'enum_member_declaration_list'):
                        b = c
                if b is not None:
                    walk_body(b, nm)
            walk_body(ch, owner)          # members declared with the outer type too
        elif ch.type == 'enum_member_declaration':
            add(owner, name_of(ch), True, 'enum_member')
        elif ch.type in MEMBER_NODES:
            st = is_static(ch)
            if ch.type == 'field_declaration':
                for fn in field_names(ch):
                    add(owner, fn, st, 'field')
            else:
                add(owner, name_of(ch), st, ch.type)
            walk_body(ch, owner)          # nested/local declarations (rare)
        elif hasattr(ch, 'children'):
            walk_body(ch, owner)


for f in files:
    src = open(f, 'rb').read()
    tree = p.parse(src)
    stack = [tree.root_node]
    while stack:
        n = stack.pop()
        if n.type in TYPE_NODES:
            nm = name_of(n)
            if nm:
                declared.setdefault(nm, f)
            for c in n.children:
                if c.type in ('declaration_list', 'enum_member_declaration_list'):
                    walk_body(c, nm)
        else:
            stack.extend(n.children)

OUT = os.environ.get('RTS_SYMBOLS', '/tmp/symbols.json')
json.dump({
    'declared': declared,
    'members': {k: sorted(v) for k, v in members.items()},
    'statics': {k: sorted(v) for k, v in statics.items()},
    'kinds': {'%s::%s' % k: v for k, v in kinds.items()},
}, open(OUT, 'w'))

print('types:', len(declared))
for t in ['GameData', 'GameMap', 'GameManager', 'Order', 'UnitDef', 'AbilityDef', 'BuildingDef', 'WorldFactory']:
    print(' ', t, '->', len(members.get(t, [])), 'members;',
          len(statics.get(t, [])), 'static:', sorted(statics.get(t, []))[:8])
