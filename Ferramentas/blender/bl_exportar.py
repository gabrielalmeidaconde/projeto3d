"""Grava no pacote que o Unity lê (Fonte/patroclo_blender.json + .bin) as peças marcadas para exportar.

Uso: blender -b Arte/Patroclo/Patroclo.blend -P bl_exportar.py
     (ou, com o arquivo aberto no Blender: aba Scripting > abrir este arquivo > Run Script)

Só saem os objetos com a propriedade "troia_exportar" ligada (Propriedades do objeto > Custom Properties).
Cada um substitui, no Unity, a peça procedural com o mesmo nome de malha ("troia_malha"). Os modificadores
são aplicados na exportação; o esqueleto é posto em repouso.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import numpy as np

import bl_comum as bc
import troca


def tangentes(pos, nor, uv, tri):
    """Tangentes por vértice (método de Lengyel), com o sinal em w como o Unity usa."""
    p0, p1, p2 = pos[tri[:, 0]], pos[tri[:, 1]], pos[tri[:, 2]]
    w0, w1, w2 = uv[tri[:, 0]], uv[tri[:, 1]], uv[tri[:, 2]]
    e1, e2 = p1 - p0, p2 - p0
    s1, s2 = w1[:, 0] - w0[:, 0], w2[:, 0] - w0[:, 0]
    t1, t2 = w1[:, 1] - w0[:, 1], w2[:, 1] - w0[:, 1]
    den = s1 * t2 - s2 * t1
    r = np.where(np.abs(den) > 1e-20, 1.0 / np.where(den == 0, 1, den), 0.0)[:, None]
    sd = (e1 * t2[:, None] - e2 * t1[:, None]) * r
    td = (e2 * s1[:, None] - e1 * s2[:, None]) * r
    ts = np.zeros_like(pos)
    tt = np.zeros_like(pos)
    for k in range(3):
        np.add.at(ts, tri[:, k], sd)
        np.add.at(tt, tri[:, k], td)
    t = ts - nor * np.einsum('ij,ij->i', nor, ts)[:, None]
    ln = np.linalg.norm(t, axis=1)
    ruim = ln < 1e-12
    # vértice sem UV útil: qualquer direção perpendicular à normal
    alt = np.cross(nor, np.where(np.abs(nor[:, [0]]) < 0.9, [[1.0, 0, 0]], [[0, 1.0, 0]]))
    t = np.where(ruim[:, None], alt, t)
    t /= np.linalg.norm(t, axis=1, keepdims=True) + 1e-20
    w = np.where(np.einsum('ij,ij->i', np.cross(nor, t), tt) < 0.0, -1.0, 1.0)
    return np.concatenate([t, w[:, None]], 1)


def malha_do_objeto(obj, ossos, deps):
    ev = obj.evaluated_get(deps)
    me = bpy.data.meshes.new_from_object(ev, depsgraph=deps)
    me.calc_loop_triangles()
    nl, nt = len(me.loops), len(me.loop_triangles)
    lv = np.empty(nl, dtype=np.int32)
    me.loops.foreach_get("vertex_index", lv)
    tl = np.empty(nt * 3, dtype=np.int32)
    me.loop_triangles.foreach_get("loops", tl)
    tm = np.empty(nt, dtype=np.int32)
    me.loop_triangles.foreach_get("material_index", tm)
    co = bc.np_verts(me)
    cn = np.empty(nl * 3, dtype=np.float32)
    me.corner_normals.foreach_get("vector", cn)
    cn = cn.reshape(-1, 3).astype(np.float64)

    def canto(nome, tam, campo):
        a = me.attributes.get(nome)
        if a is None or a.domain != 'CORNER':
            return None
        x = np.empty(nl * tam, dtype=np.float32)
        a.data.foreach_get(campo, x)
        return x.reshape(-1, tam).astype(np.float64)
    uv_mapa = canto("UVMapa", 2, "vector")
    uv_metro = canto("UVMetro", 2, "vector")
    uv0, uv1 = (uv_mapa, uv_metro) if uv_mapa is not None else (uv_metro, None)
    cor = canto("Mascaras", 4, "color")
    # um vértice do Unity para cada combinação diferente de (vértice, normal, uv, cor)
    cols = [lv[:, None].astype(np.int64), np.round(cn * 2e4).astype(np.int64)]
    if uv0 is not None:
        cols.append(np.round(uv0 * 1e5).astype(np.int64))
    if uv1 is not None:
        cols.append(np.round(uv1 * 1e5).astype(np.int64))
    if cor is not None:
        cols.append(np.round(cor * 255).astype(np.int64))
    key = np.concatenate(cols, 1)
    _, first, inv = np.unique(key, axis=0, return_index=True, return_inverse=True)
    inv = inv.ravel()
    vi = lv[first]
    pos = troca.b2u(co[vi])
    nor = troca.b2u(cn[first])
    nor /= np.linalg.norm(nor, axis=1, keepdims=True) + 1e-20
    tri = inv[tl].reshape(-1, 3)[:, [0, 2, 1]]
    m = dict(nome=obj["troia_malha"], rigida=bool(obj.get("troia_rigida", False)), pos=pos, nor=nor, instancias=[])
    if uv0 is not None:
        m["uv0"] = uv0[first]
        m["tan"] = tangentes(pos, nor, m["uv0"], tri)
    if uv1 is not None:
        m["uv1"] = uv1[first]
    if cor is not None:
        m["cor"] = np.clip(np.round(cor[first] * 255), 0, 255).astype(np.uint8)
    m["subs"] = []
    for k in np.unique(tm):
        mat = me.materials[k] if k < len(me.materials) else None
        m["subs"].append(dict(material=mat.name if mat is not None else "", tri=tri[tm == k].astype(np.int32)))
    if not m["rigida"]:
        idx = {n: i for i, n in enumerate(ossos)}
        grupos = [idx.get(g.name, -1) for g in obj.vertex_groups]
        bi = np.zeros((len(me.vertices), 4), dtype=np.int32)
        bw = np.zeros((len(me.vertices), 4), dtype=np.float32)
        sem = 0
        for v in me.vertices:
            ws = sorted(((g.weight, grupos[g.group]) for g in v.groups if grupos[g.group] >= 0 and g.weight > 1e-5), reverse=True)[:4]
            if not ws:
                sem += 1
                ws = [(1.0, idx.get("root", 0))]
            tot = sum(w for w, _ in ws)
            for k, (w, b) in enumerate(ws):
                bi[v.index, k] = b
                bw[v.index, k] = w / tot
        if sem:
            print("[exportar] AVISO: %s tem %d vértices sem peso (foram presos ao osso root)" % (obj.name, sem))
        m["bi"], m["bw"] = bi[vi], bw[vi]
    ruim = int((~np.isfinite(pos)).any(1).sum())
    if ruim:
        print("[exportar] AVISO: %s tem %d vértices inválidos" % (obj.name, ruim))
    bpy.data.meshes.remove(me)
    return m


def main():
    arm = bc.esqueleto()
    ossos = list(arm["troia_ossos"])
    antes = arm.data.pose_position
    arm.data.pose_position = 'REST'
    # objeto escondido não tem os modificadores calculados: mostra todos os que vão sair e esconde de novo no fim
    lista = [o for o in sorted(bpy.data.objects, key=lambda o: o.name) if o.type == 'MESH' and o.get("troia_exportar")]
    escondidos = [(o, o.hide_viewport, o.hide_get()) for o in lista]
    for o in lista:
        o.hide_viewport = False
        o.hide_set(False)
    bpy.context.view_layer.update()
    deps = bpy.context.evaluated_depsgraph_get()
    malhas, vistos = [], set()
    for obj in lista:
        if obj.get("troia_malha") in vistos:
            continue
        vistos.add(obj["troia_malha"])
        m = malha_do_objeto(obj, ossos, deps)
        malhas.append(m)
        print("[exportar] %-32s %7d vértices %7d triângulos  %s" % (m["nome"], len(m["pos"]), sum(len(s["tri"]) for s in m["subs"]),
                                                                    ", ".join(s["material"] for s in m["subs"])))
    arm.data.pose_position = antes
    for o, hv, hs in escondidos:
        o.hide_viewport = hv
        o.hide_set(hs)
    troca.escrever(bc.PACOTE, [dict(nome=n, pai=-1, cabeca=[0, 0, 0], cauda=[0, 0, 0]) for n in ossos], malhas)
    print("[exportar] %d peças em %s.bin (%.1f MB)" % (len(malhas), bc.PACOTE, os.path.getsize(bc.PACOTE + ".bin") / 1e6))


main()
