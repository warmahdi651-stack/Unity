#!/usr/bin/env python3
"""Convert a (Kenney-style, single-buffer) GLB into OBJ + MTL.

Usage: glb2obj.py <in.glb> <out.obj> <mtl-name> <texture-file-name>
Handles float positions/normals/uvs and u16/u32 indices, multiple
primitives/materials.  Materials with a base color texture all map to the
single supplied texture file; others become flat Kd colours.
"""
import json, struct, sys

def main():
    src, out_obj, mtl_name, tex_name = sys.argv[1:5]
    data = open(src, 'rb').read()
    assert data[:4] == b'glTF'
    clen = struct.unpack_from('<I', data, 12)[0]
    gltf = json.loads(data[20:20 + clen])
    bin_off = 20 + clen + 8
    blob = data[bin_off:]

    def buf_view(bv):
        off = bv.get('byteOffset', 0)
        return blob[off:off + bv['byteLength']], bv.get('byteStride', 0)

    def accessor(acc):
        bv = gltf['bufferViews'][acc['bufferView']]
        raw, stride = buf_view(bv)
        off = acc.get('byteOffset', 0)
        n = acc['count']
        ct = acc['componentType']
        t = acc['type']
        comps = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4}[t]
        sizes = {5120: 1, 5121: 1, 5122: 2, 5123: 2, 5125: 4, 5126: 4}
        cs = sizes[ct]
        fmt = {1: 'b', 2: 'h', 4: 'i', 5126: 'f'}[cs] if ct != 5121 else 'B'
        if ct == 5121: fmt = 'B'
        elif ct == 5120: fmt = 'b'
        elif ct == 5122: fmt = 'h'
        elif ct == 5123: fmt = 'H'
        elif ct == 5125: fmt = 'I'
        else: fmt = 'f'
        elem = cs * comps
        stride = stride or elem
        out = []
        for i in range(n):
            base = off + i * stride
            vals = struct.unpack_from('<' + fmt * comps, raw, base)
            out.append(vals if comps > 1 else vals[0])
        return out

    meshes = gltf.get('meshes', [])
    mats = gltf.get('materials', [])
    mat_info = []
    for m in mats:
        pbr = m.get('pbrMetallicRoughness', {})
        fac = pbr.get('baseColorFactor', [1, 1, 1, 1])
        textured = 'baseColorTexture' in pbr
        mat_info.append((fac, textured))

    obj_lines = ['mtllib ' + mtl_name]
    mtl_lines = []
    seen_mats = set()
    v_off = 1
    vt_off = 1
    vn_off = 1

    for mesh in meshes:
        for prim in mesh['primitives']:
            mi = prim.get('material', 0)
            fac, textured = mat_info[mi] if mi < len(mat_info) else ((1, 1, 1, 1), False)
            mname = 'm%d' % mi
            if mname not in seen_mats:
                seen_mats.add(mname)
                mtl_lines.append('newmtl ' + mname)
                if textured:
                    mtl_lines += ['Kd 1 1 1', 'map_Kd ' + tex_name]
                else:
                    mtl_lines += ['Kd %.3f %.3f %.3f' % (fac[0], fac[1], fac[2]),
                                  'd %.2f' % fac[3]]
                mtl_lines.append('')
            pos = accessor(gltf['accessors'][prim['attributes']['POSITION']])
            uv = accessor(gltf['accessors'][prim['attributes']['TEXCOORD_0']]) if 'TEXCOORD_0' in prim['attributes'] else None
            nor = accessor(gltf['accessors'][prim['attributes']['NORMAL']]) if 'NORMAL' in prim['attributes'] else None
            idx = accessor(gltf['accessors'][prim['indices']]) if 'indices' in prim else list(range(len(pos)))

            obj_lines.append('usemtl ' + mname)
            for p in pos:
                obj_lines.append('v %.4f %.4f %.4f' % (p[0], p[1], p[2]))
            if uv:
                for t in uv:
                    obj_lines.append('vt %.4f %.4f' % (t[0], 1.0 - t[1]))
            if nor:
                for nrm in nor:
                    obj_lines.append('vn %.4f %.4f %.4f' % (nrm[0], nrm[1], nrm[2]))
            for i in range(0, len(idx), 3):
                a, b, c = idx[i] + v_off, idx[i + 1] + v_off, idx[i + 2] + v_off
                if uv and nor:
                    f = lambda k: '%d/%d/%d' % (k, k - v_off + vt_off, k - v_off + vn_off)
                elif uv:
                    f = lambda k: '%d/%d' % (k, k - v_off + vt_off)
                else:
                    f = lambda k: '%d' % k
                obj_lines.append('f %s %s %s' % (f(a), f(b), f(c)))
            v_off += len(pos)
            if uv: vt_off += len(uv)
            if nor: vn_off += len(nor)

    open(out_obj, 'w').write('\n'.join(obj_lines) + '\n')
    open(out_obj[:-4] + '.mtl', 'w').write('\n'.join(mtl_lines) + '\n')
    print('converted', src, '->', out_obj, '(%d verts)' % (v_off - 1))

if __name__ == '__main__':
    main()
