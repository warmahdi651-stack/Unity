#!/usr/bin/env python3
"""Compose Kenney parts (converted OBJs) into merged building OBJs.

Reads part OBJs from /tmp/obj, writes merged OBJs + MTLs to the target
folder.  All textured parts map to one of the three Kenney colormaps;
procedural boxes (doors, gold nuggets, poles) use flat materials.
"""
import math, os, sys

SRC = '/tmp/obj'

def load_obj(path):
    v, vt, vn, groups = [], [], [], []
    cur = None
    for line in open(path):
        t = line.split()
        if not t:
            continue
        if t[0] == 'v':
            v.append(tuple(float(x) for x in t[1:4]))
        elif t[0] == 'vt':
            vt.append(tuple(float(x) for x in t[1:3]))
        elif t[0] == 'vn':
            vn.append(tuple(float(x) for x in t[1:4]))
        elif t[0] == 'usemtl':
            cur = {'mtl': t[1], 'faces': []}
            groups.append(cur)
        elif t[0] == 'f':
            if cur is None:
                cur = {'mtl': 'm0', 'faces': []}
                groups.append(cur)
            ids = []
            for tok in t[1:]:
                a, b, c = (tok.split('/') + ['1', '1'])[:3]
                ids.append((int(a), int(b), int(c)))
            cur['faces'].append(ids)
    return v, vt, vn, groups

class Builder:
    def __init__(self):
        self.v, self.vt, self.vn, self.out = [], [], [], []
        self.mats = {}

    def mat(self, name, kind, tex=None, kd=None):
        self.mats[name] = (kind, tex, kd)

    def _xform(self, p, rot, sc, tr):
        x, y, z = p[0] * sc[0], p[1] * sc[1], p[2] * sc[2]
        if rot:
            c, s = math.cos(rot), math.sin(rot)
            x, z = x * c + z * s, -x * s + z * c
        return (x + tr[0], y + tr[1], z + tr[2])

    def part(self, name, tr, rot=0.0, sc=(1, 1, 1), matmap=None):
        v, vt, vn, groups = load_obj(os.path.join(SRC, name + '.obj'))
        base = len(self.v)
        for p in v:
            self.v.append(self._xform(p, rot, sc, tr))
        for t in vt:
            self.vt.append(t)
        for n in vn:
            c, s = math.cos(rot), math.sin(rot)
            nx, nz = n[0] * c + n[2] * s, -n[0] * s + n[2] * c
            self.vn.append((nx, n[1], nz))
        for g in groups:
            m = g['mtl']
            if matmap and m in matmap:
                m = matmap[m]
            self.out.append(('usemtl ' + m, None))
            for f in g['faces']:
                toks = []
                for (a, b, cc) in f:
                    toks.append('%d/%d/%d' % (a + base, b, cc + base))
                self.out.append(('f ' + ' '.join(toks), None))

    def box(self, cx, cy, cz, sx, sy, sz, m):
        base = len(self.v)
        nbase = len(self.vn)
        hx, hy, hz = sx / 2, sy / 2, sz / 2
        for dx in (-1, 1):
            for dy in (-1, 1):
                for dz in (-1, 1):
                    self.v.append((cx + dx * hx, cy + dy * hy, cz + dz * hz))
        self.vn.append((0, 0, 1))
        self.vn.append((0, 0, -1))
        self.vn.append((1, 0, 0))
        self.vn.append((-1, 0, 0))
        self.vn.append((0, 1, 0))
        self.vn.append((0, -1, 0))
        quads = [(0, 1, 3, 2, 5), (4, 6, 7, 5, 4), (0, 2, 6, 4, 2),
                 (1, 5, 7, 3, 3), (0, 4, 5, 1, 0), (2, 3, 7, 6, 1)]
        # corners order: 0(-,-,-) 1(-,-,+) 2(-,+,-) 3(-,+,+) 4(+,-,-) 5(+,-,+) 6(+,+,-) 7(+,+,+)
        faces = [((0, 1, 5, 4), 5), ((2, 6, 7, 3), 4), ((0, 4, 6, 2), 3),
                 ((1, 3, 7, 5), 2), ((4, 5, 7, 6), 0), ((0, 2, 3, 1), 1)]
        self.out.append(('usemtl ' + m, None))
        for quad, ni in faces:
            a, b, c, d = [base + q + 1 for q in quad]
            nn = nbase + ni + 1
            self.out.append(('f %d//%d %d//%d %d//%d' % (a, nn, b, nn, c, nn), None))
            self.out.append(('f %d//%d %d//%d %d//%d' % (a, nn, c, nn, d, nn), None))

    def write(self, path, mtl_name):
        with open(path, 'w') as f:
            f.write('mtllib ' + mtl_name + '\n')
            for p in self.v:
                f.write('v %.4f %.4f %.4f\n' % p)
            f.write('vt 0 0\n')
            for t in self.vt:
                f.write('vt %.4f %.4f\n' % t)
            for n in self.vn:
                f.write('vn %.4f %.4f %.4f\n' % n)
            for line, _ in self.out:
                f.write(line + '\n')
        with open(path[:-4] + '.mtl', 'w') as f:
            for name, (kind, tex, kd) in sorted(self.mats.items()):
                f.write('newmtl ' + name + '\n')
                if kind == 'tex':
                    f.write('Kd 1 1 1\nmap_Kd ' + tex + '\n')
                else:
                    f.write('Kd %.3f %.3f %.3f\n' % kd)
                f.write('\n')

R90 = math.pi / 2

def house(b, w, d, h, wood, roof='gable'):
    wall = 'wall-wood-block' if wood else 'wall-block'
    for x in range(w):
        for z in range(d):
            if not (x in (0, w - 1) or z in (0, d - 1)):
                continue
            for y in range(h):
                if z == d - 1 and x == w // 2 and y == 0:
                    continue  # door slot
                b.part(wall, (x + 0.5, y + 0.5, z + 0.5), matmap={'m0': 'tex'})
    # door
    b.box(w / 2.0, 0.7, d - 0.5, 0.9, 1.4, 0.18, 'wood')
    # roof grid
    if roof == 'gable':
        for x in range(w):
            for z in range(d):
                b.part('roof-gable', (x + 0.5, h + 0.25, z + 0.5), matmap={'m0': 'tex'})
    else:
        b.part('roof-point', (w / 2.0, h, d / 2.0), sc=(w * 0.95, 2.0, d * 0.95), matmap={'m0': 'tex'})

def main():
    dst = sys.argv[1]
    os.makedirs(dst, exist_ok=True)

    # ---- town hall ----
    b = Builder()
    b.mat('tex', 'tex', 'colormap_ft.png'); b.mat('wood', 'flat', kd=(0.35, 0.24, 0.14)); b.mat('gold', 'flat', kd=(1.0, 0.8, 0.2))
    house(b, 5, 5, 2, False)
    for (px, pz) in [(0.3, 0.3), (4.7, 0.3), (0.3, 4.7), (4.7, 4.7)]:
        b.part('pillar-stone', (px, 2.0, pz), sc=(2.2, 4.0, 2.2), matmap={'m0': 'tex'})
    b.part('banner-red', (1.7, 1.2, 5.05), rot=R90, matmap={'m0': 'tex'})
    b.part('banner-red', (3.3, 1.2, 5.05), rot=R90, matmap={'m0': 'tex'})
    b.part('chimney', (4.2, 2.6, 1.0), matmap={'m0': 'tex'})
    b.part('flag', (2.5, 3.2, 2.5), matmap={'m0': 'tex'})
    b.write(os.path.join(dst, 'town_hall.obj'), 'town_hall.mtl')

    # ---- barracks ----
    b = Builder()
    b.mat('tex', 'tex', 'colormap_ft.png'); b.mat('wood', 'flat', kd=(0.35, 0.24, 0.14))
    house(b, 4, 4, 2, True)
    b.part('banner-red', (2.0, 1.2, 4.05), rot=R90, matmap={'m0': 'tex'})
    b.write(os.path.join(dst, 'barracks.obj'), 'barracks.mtl')

    # ---- farm ----
    b = Builder()
    b.mat('tex', 'tex', 'colormap_ft.png'); b.mat('wood', 'flat', kd=(0.35, 0.24, 0.14))
    house(b, 3, 3, 1, True)
    b.part('stall', (1.0, 1.0, 3.3), matmap={'m0': 'tex'})
    b.write(os.path.join(dst, 'farm.obj'), 'farm.mtl')

    # ---- lumber mill ----
    b = Builder()
    b.mat('tex', 'tex', 'colormap_ft.png'); b.mat('wood', 'flat', kd=(0.35, 0.24, 0.14))
    house(b, 4, 3, 1, True)
    b.part('stall', (3.2, 0.0, 3.4), matmap={'m0': 'tex'})
    b.part('windmill', (0.8, 2.6, 0.8), sc=(0.8, 0.8, 0.8), matmap={'m0': 'tex'})
    b.write(os.path.join(dst, 'lumber_mill.obj'), 'lumber_mill.mtl')

    # ---- blacksmith ----
    b = Builder()
    b.mat('tex', 'tex', 'colormap_ft.png'); b.mat('wood', 'flat', kd=(0.35, 0.24, 0.14))
    house(b, 3, 3, 2, False)
    b.part('chimney', (2.4, 2.6, 0.8), matmap={'m0': 'tex'})
    b.write(os.path.join(dst, 'blacksmith.obj'), 'blacksmith.mtl')

    # ---- altar (open platform with pillars + flags) ----
    b = Builder()
    b.mat('tex', 'tex', 'colormap_ft.png'); b.mat('wood', 'flat', kd=(0.35, 0.24, 0.14)); b.mat('gold', 'flat', kd=(1.0, 0.8, 0.2))
    for x in range(3):
        for z in range(3):
            b.part('wall-block', (x + 0.5, 0.5, z + 0.5), matmap={'m0': 'tex'})
    for (px, pz) in [(0.3, 0.3), (2.7, 0.3), (0.3, 2.7), (2.7, 2.7)]:
        b.part('pillar-stone', (px, 1.0, pz), sc=(2.0, 2.4, 2.0), matmap={'m0': 'tex'})
    b.box(1.5, 1.35, 1.5, 0.7, 0.5, 0.7, 'gold')
    b.part('flag', (1.5, 1.6, 1.5), matmap={'m0': 'tex'})
    b.write(os.path.join(dst, 'altar.obj'), 'altar.mtl')

    # ---- arcane sanctum ----
    b = Builder()
    b.mat('tex', 'tex', 'colormap_ft.png'); b.mat('wood', 'flat', kd=(0.35, 0.24, 0.14))
    house(b, 3, 3, 2, False, roof='point')
    b.part('flag', (1.5, 4.0, 1.5), matmap={'m0': 'tex'})
    b.write(os.path.join(dst, 'sanctum.obj'), 'sanctum.mtl')

    # ---- guard tower ----
    b = Builder()
    b.mat('tex', 'tex', 'colormap_ft.png'); b.mat('wood', 'flat', kd=(0.35, 0.24, 0.14))
    house(b, 2, 2, 4, False, roof='point')
    b.part('flag', (1.0, 5.6, 1.0), matmap={'m0': 'tex'})
    b.write(os.path.join(dst, 'tower.obj'), 'tower.mtl')

    # ---- gold mine ----
    b = Builder()
    b.mat('tex', 'tex', 'colormap_ft.png'); b.mat('gold', 'flat', kd=(1.0, 0.8, 0.2))
    b.part('rock-large', (0, 0, 0), sc=(1.7, 1.5, 1.7), matmap={'m0': 'tex'})
    b.part('rock-small', (-1.4, 0, 0.8), rot=0.7, matmap={'m0': 'tex'})
    b.part('rock-small', (1.3, 0, -0.9), rot=2.1, sc=(0.8, 0.8, 0.8), matmap={'m0': 'tex'})
    b.box(0.4, 1.3, 0.5, 0.45, 0.45, 0.45, 'gold')
    b.box(-0.7, 0.9, -0.5, 0.35, 0.35, 0.35, 'gold')
    b.box(0.9, 0.8, 0.9, 0.3, 0.3, 0.3, 'gold')
    b.write(os.path.join(dst, 'goldmine.obj'), 'goldmine.mtl')

    print('composed buildings ->', dst)

if __name__ == '__main__':
    main()
