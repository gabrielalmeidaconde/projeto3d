"""Monta o arquivo de arte do Pátroclo no Blender a partir da base exportada pelo Unity.

Uso: blender -b --factory-startup -P bl_montar.py [-- saida.blend]

Cria o esqueleto (ossos do MakeHuman), cada peça como um objeto com grupos de vértices (pesos) e
modificador de armadura, as peças rígidas (espada, escudos, adaga) presas ao osso, e materiais simples.
Guarda em cada objeto de onde ele veio (troia_malha, troia_caminho...) para o caminho de volta.
"""
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import numpy as np
from mathutils import Matrix, Vector

import bl_comum as bc
import troca

T0 = time.time()

# cor de base (sRGB), metal e rugosidade de cada material do Unity, para a prévia no Blender
MATERIAIS = {
    "M_Pat_Bronze": ("#A26A3A", 1.0, 0.36), "M_Pat_BronzeEscuro": ("#5E3A1E", 1.0, 0.5), "M_Pat_Ouro": ("#F2C65C", 1.0, 0.3),
    "M_Pat_Lamina": ("#D8AA5F", 1.0, 0.25), "M_Pat_Crista": ("#B01C22", 0.0, 0.7), "M_Pat_Capa": ("#8E1A1E", 0.0, 0.85),
    "M_Pat_Linho": ("#D6C6A2", 0.0, 0.85), "M_Pat_Couro": ("#5A3A26", 0.0, 0.6), "M_Pat_Emblema": ("#6E1015", 0.2, 0.55),
    "M_Pat_Madeira": ("#6B5038", 0.0, 0.75), "M_Pat_Corte": ("#140D0A", 0.0, 0.95), "M_Pat_Pele": ("#C89678", 0.0, 0.5),
    "M_Pat_Olhos": ("#E8E4E0", 0.0, 0.15), "M_Pat_Sobrancelhas": ("#2A1A12", 0.0, 0.7), "M_Pat_Cilios": ("#1A100C", 0.0, 0.7),
    "M_Pat_Cabelo": ("#3A2416", 0.0, 0.6), "M_Pat_Marca": ("#141414", 0.0, 0.4),
}


def material(nome):
    m = bpy.data.materials.get(nome)
    if m is not None:
        return m
    m = bpy.data.materials.new(nome)
    cor, metal, rug = MATERIAIS.get(nome, ("#808080", 0.0, 0.6))
    m.use_nodes = True
    p = m.node_tree.nodes.get("Principled BSDF")
    p.inputs["Base Color"].default_value = bc.hexa(cor)
    p.inputs["Metallic"].default_value = metal
    p.inputs["Roughness"].default_value = rug
    m.diffuse_color = bc.hexa(cor)
    m.metallic = metal
    m.roughness = rug
    return m


def criar_esqueleto(ossos):
    arm = bpy.data.armatures.new("Esqueleto")
    obj = bpy.data.objects.new("Esqueleto", arm)
    bc.colecao("Esqueleto").objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode='EDIT')
    ebs = []
    for b in ossos:
        eb = arm.edit_bones.new(b["nome"])
        h = Vector(troca.u2b(b["cabeca"]))
        t = Vector(troca.u2b(b["cauda"]))
        if (t - h).length < 0.004:
            t = h + Vector((0, 0, 0.02))
        eb.head, eb.tail = h, t
        ebs.append(eb)
    for b, eb in zip(ossos, ebs):
        if b["pai"] >= 0:
            eb.parent = ebs[b["pai"]]
    bpy.ops.object.mode_set(mode='OBJECT')
    arm.display_type = 'STICK'
    obj.show_in_front = False
    nomes = [b.name for b in arm.bones]
    faltam = [b["nome"] for b in ossos if b["nome"] not in nomes]
    if faltam:
        print("[blender] AVISO: ossos que o Blender renomeou ou removeu:", faltam[:10])
    return obj


def soldar(pos, nor):
    """Vértices repetidos do Unity (costuras de UV e quinas) viram um só; lados opostos de folha dupla ficam separados."""
    key = np.round(pos * 2e5).astype(np.int64)
    _, first, inv = np.unique(key, axis=0, return_index=True, return_inverse=True)
    inv = inv.ravel()
    ref = nor[first][inv]
    oposto = (np.einsum('ij,ij->i', nor, ref) < -0.5).astype(np.int64)
    _, first2, inv2 = np.unique(inv * 2 + oposto, return_index=True, return_inverse=True)
    return first2, inv2.ravel()


def criar_malha(m):
    pos = troca.u2b(m["pos"])
    nor = troca.u2b(m["nor"])
    first, inv = soldar(pos, nor)
    tris = np.concatenate([s["tri"] for s in m["subs"]])[:, [0, 2, 1]]
    mat_idx = np.concatenate([np.full(len(s["tri"]), k, dtype=np.int32) for k, s in enumerate(m["subs"])])
    faces = inv[tris]
    ok = (faces[:, 0] != faces[:, 1]) & (faces[:, 1] != faces[:, 2]) & (faces[:, 0] != faces[:, 2])
    tris, faces, mat_idx = tris[ok], faces[ok], mat_idx[ok]
    me = bpy.data.meshes.new(m["nome"])
    me.from_pydata(pos[first].tolist(), [], faces.tolist())
    assert len(me.polygons) == len(faces) and len(me.loops) == len(faces) * 3, m["nome"]
    me.polygons.foreach_set("material_index", mat_idx)
    for s in m["subs"]:
        me.materials.append(material(s["material"]))
    cantos = tris.ravel()
    if m["uv0"] is not None:
        uv = me.attributes.new("UVMetro", 'FLOAT2', 'CORNER')
        uv.data.foreach_set("vector", m["uv0"][cantos].astype(np.float32).ravel())
    if m["cor"] is not None:
        # máscaras assadas pelo Unity (R oclusão, G 1-sujeira, B 1-sangue): números puros, não cor
        ca = me.attributes.new("Mascaras", 'FLOAT_COLOR', 'CORNER')
        ca.data.foreach_set("color", (m["cor"][cantos].astype(np.float32) / 255.0).ravel())
    me.shade_smooth()
    me.update()
    me.normals_split_custom_set(nor[cantos].tolist())
    me.update()
    return me, first


def pesos(obj, m, first, ossos):
    bi, bw = m["bi"][first], m["bw"][first]
    usados = np.unique(bi[bw > 0])
    grupos = {int(b): obj.vertex_groups.new(name=ossos[int(b)]["nome"]) for b in usados}
    vs, ks = np.nonzero(bw > 0)
    for v, b, w in zip(vs.tolist(), bi[vs, ks].tolist(), bw[vs, ks].tolist()):
        grupos[b].add((v,), w, 'ADD')


def onde(caminho, nome_malha):
    """Coleção e nome do objeto a partir do caminho no Unity."""
    partes = caminho.split("/")
    if nome_malha.startswith("MH_"):
        return "Corpo", partes[-1]
    if partes[0] in ("Armadura", "Roupa"):
        if len(partes) > 2 and partes[1] == "Elmo":
            return "Elmo", partes[-1]
        return partes[0], partes[-1]
    # peça rígida pendurada num osso: .../<osso>/<grupo>/<peça>
    grupo = partes[-2] if len(partes) >= 2 else "Outros"
    return "Armas", f"{grupo}_{partes[-1]}" if grupo.startswith("Escudo") else partes[-1]


def main():
    saida = bc.args()[0] if bc.args() else bc.BLEND
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for c in list(bpy.data.collections):
        bpy.data.collections.remove(c)
    pk = troca.ler(bc.BASE)
    ossos = pk["ossos"]
    arm = criar_esqueleto(ossos)
    arm["troia_ossos"] = [b["nome"] for b in ossos]
    tot_v = tot_t = 0
    for m in pk["malhas"]:
        me, first = criar_malha(m)
        tot_v += len(me.vertices)
        tot_t += len(me.polygons)
        for k, inst in enumerate(m["instancias"]):
            col, nome = onde(inst["caminho"], m["nome"])
            obj = bpy.data.objects.new(nome, me)
            bc.colecao(col).objects.link(obj)
            obj["troia_malha"] = m["nome"]
            obj["troia_caminho"] = inst["caminho"]
            obj["troia_rigida"] = bool(m["rigida"])
            obj["troia_exportar"] = False
            if m["rigida"]:
                # objeto preso ao osso, com a malha no espaço local dele
                mw = Matrix(troca.mat_u2b(inst["matriz"]).tolist())
                obj["troia_osso"] = inst["osso"]
                obj.parent = arm
                if inst["osso"] and inst["osso"] in arm.data.bones:
                    b = arm.data.bones[inst["osso"]]
                    obj.parent_type = 'BONE'
                    obj.parent_bone = inst["osso"]
                    pai = arm.matrix_world @ b.matrix_local @ Matrix.Translation((0, b.length, 0))
                    obj.matrix_parent_inverse = pai.inverted()
                obj.matrix_basis = mw
            else:
                obj.parent = arm
                if k == 0:
                    pesos(obj, m, first, ossos)
                md = obj.modifiers.new("Esqueleto", 'ARMATURE')
                md.object = arm
        print("[blender] %-34s %7d v %7d t  %s" % (m["nome"], len(me.vertices), len(me.polygons), "rígida" if m["rigida"] else ""))
    # o corpo "vestido" (sem os polígonos escondidos) é derivado no Unity: no Blender só atrapalha
    for o in bpy.data.objects:
        if o.get("troia_malha") == "MH_Corpo_Vestido" or o.get("troia_malha") == "Pat_Quiton_Solto":
            o.hide_viewport = o.hide_render = True
    sc = bpy.context.scene
    sc.unit_settings.system = 'METRIC'
    sc.frame_set(1)
    # vista 3D em modo material e com o personagem enquadrado, para quem abrir o arquivo
    for scr in bpy.data.screens:
        for a in scr.areas:
            if a.type == 'VIEW_3D':
                for sp in a.spaces:
                    if sp.type == 'VIEW_3D':
                        sp.shading.type = 'SOLID'
                        sp.shading.color_type = 'MATERIAL'
                        sp.clip_start = 0.01
    bc.salvar(saida)
    print("[blender] montado: %d malhas, %d vértices, %d triângulos em %.1f s" % (len(pk["malhas"]), tot_v, tot_t, time.time() - T0))


main()
