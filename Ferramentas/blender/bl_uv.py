"""Abre em mapa de textura próprio (UV sem repetição) as peças que recebem pintura exclusiva.

Uso: blender -b Arte/Patroclo/Patroclo.blend -P bl_uv.py

Cada conjunto junta partes de várias peças num atlas só. As faces do conjunto trocam o material de textura
repetida pelo material pintado e ganham UV nova no mapa "UVMapa"; o resto da peça continua com a UV em metros
("UVMetro", que nas faces pintadas passa a servir só para a sujeira). O pintor (pintor.py) lê o pacote exportado.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import numpy as np

import bl_comum as bc

# conjunto -> (material pintado, cor de prévia, [(objeto, [materiais de origem], peso de resolução)])
CONJUNTOS = {
    "bronze": ("M_Pat_BronzePintado", "#A26A3A", [
        ("Elmo_Casco", ["M_Pat_Bronze"], 1.5), ("Cnemides", ["M_Pat_Bronze"], 1.0), ("Escudo_Costas_Aspis", ["M_Pat_Bronze"], 0.62),
        ("Disco_Peito", ["M_Pat_Bronze"], 1.6), ("Epomides", ["M_Pat_Bronze"], 1.0), ("Bracadeiras", ["M_Pat_Bronze"], 1.0),
        ("Cinturao", ["M_Pat_Bronze"], 1.0)]),
    "linho": ("M_Pat_LinhoPintado", "#D6C6A2", [
        # só o miolo de linho: a barra carmesim continua com a lã repetida, e a costura entre os dois vira a "beira" do pintor
        ("Corselete", ["M_Pat_Linho"], 1.0), ("Epomides", ["M_Pat_Linho"], 1.0)]),
}


_corpo = None


def faces_de_fora(obj):
    """Máscara das faces que olham para longe da pele mais próxima (o lado de dentro das peças não é pintado)."""
    global _corpo
    if _corpo is None:
        _corpo = bc.bvh_de(["Corpo"])
    me = obj.data
    n = len(me.polygons)
    c = np.empty(n * 3, dtype=np.float32)
    nr = np.empty(n * 3, dtype=np.float32)
    me.polygons.foreach_get("center", c)
    me.polygons.foreach_get("normal", nr)
    c, nr = c.reshape(-1, 3), nr.reshape(-1, 3)
    fora = np.ones(n, dtype=bool)
    from mathutils import Vector
    for i in range(n):
        perto = _corpo.find_nearest(Vector(c[i].tolist()))[0]
        if perto is not None:
            fora[i] = (c[i, 0] - perto.x) * nr[i, 0] + (c[i, 1] - perto.y) * nr[i, 1] + (c[i, 2] - perto.z) * nr[i, 2] > 0.0
    return fora


def preparar(obj, origens, pintado, so_fora=False):
    """Cria o UVMapa (cópia do UVMetro), troca o material das faces do conjunto e devolve a máscara delas."""
    me = obj.data
    if me.attributes.get("UVMapa") is None:
        src = me.attributes["UVMetro"]
        a = np.empty(len(me.loops) * 2, dtype=np.float32)
        src.data.foreach_get("vector", a)
        me.attributes.new("UVMapa", 'FLOAT2', 'CORNER').data.foreach_set("vector", a)
    me.uv_layers.active = me.uv_layers["UVMapa"]
    me.uv_layers["UVMapa"].active_render = True
    idx = [i for i, m in enumerate(me.materials) if m is not None and m.name in origens]
    mi = np.empty(len(me.polygons), dtype=np.int32)
    me.polygons.foreach_get("material_index", mi)
    sel = np.isin(mi, idx)
    if so_fora:
        sel &= faces_de_fora(obj)
    if pintado.name not in [m.name for m in me.materials if m is not None]:
        me.materials.append(pintado)
    novo = [m.name if m is not None else "" for m in me.materials].index(pintado.name)
    mi[sel] = novo
    me.polygons.foreach_set("material_index", mi)
    selecionar(me, sel)
    return sel


def selecionar(me, sel):
    """Deixa selecionadas só as faces dadas (vértices e arestas desmarcados, senão o modo de edição marca tudo)."""
    me.vertices.foreach_set("select", np.zeros(len(me.vertices), dtype=bool))
    me.edges.foreach_set("select", np.zeros(len(me.edges), dtype=bool))
    me.polygons.foreach_set("select", sel)
    me.update()


def uv_das_faces(me, sel):
    lt = np.empty(len(me.polygons), dtype=np.int32)
    ls = np.empty(len(me.polygons), dtype=np.int32)
    me.polygons.foreach_get("loop_total", lt)
    me.polygons.foreach_get("loop_start", ls)
    cantos = np.concatenate([np.arange(s, s + t) for s, t in zip(ls[sel], lt[sel])]) if sel.any() else np.zeros(0, dtype=np.int64)
    return cantos


def conjunto(nome, pintado_nome, cor, pecas, so_fora=False):
    pintado = bpy.data.materials.get(pintado_nome) or bc.material_tex(pintado_nome, cor)
    objs, sels = [], []
    bpy.ops.object.select_all(action='DESELECT')
    for on, origens, peso in pecas:
        o = bpy.data.objects.get(on)
        if o is None:
            print("[uv] AVISO: sem o objeto", on)
            continue
        if o.get("uv_" + nome):
            # já aberto numa execução anterior: as faces pintadas são as do material pintado
            mi = np.empty(len(o.data.polygons), dtype=np.int32)
            o.data.polygons.foreach_get("material_index", mi)
            sel = mi == [m.name if m is not None else "" for m in o.data.materials].index(pintado_nome)
            selecionar(o.data, sel)
            o.data.uv_layers.active = o.data.uv_layers["UVMapa"]
        else:
            sel = preparar(o, origens, pintado, so_fora)
        o.hide_viewport = False
        o.hide_set(False)
        o.select_set(True)
        objs.append((o, peso))
        sels.append(sel)
    bpy.context.view_layer.objects.active = objs[0][0]
    bpy.context.scene.tool_settings.use_uv_select_sync = True
    bpy.context.scene.tool_settings.mesh_select_mode = (False, False, True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66.0), island_margin=0.0, area_weight=0.0, correct_aspect=True, scale_to_bounds=False)
    bpy.ops.uv.average_islands_scale()
    bpy.ops.object.mode_set(mode='OBJECT')
    # peso de resolução por peça (o elmo é visto de perto; o escudo é grande e visto de longe)
    for (o, peso), sel in zip(objs, sels):
        if abs(peso - 1.0) > 1e-3:
            me = o.data
            a = np.empty(len(me.loops) * 2, dtype=np.float32)
            me.attributes["UVMapa"].data.foreach_get("vector", a)
            a = a.reshape(-1, 2)
            c = uv_das_faces(me, sel)
            a[c] *= peso
            me.attributes["UVMapa"].data.foreach_set("vector", a.ravel())
    for (o, _), sel in zip(objs, sels):
        selecionar(o.data, sel)
        o.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.uv.pack_islands(rotate=True, margin=0.0035)
    bpy.ops.object.mode_set(mode='OBJECT')
    # confere: área de UV usada e metros por unidade de UV (para o pintor e para o mapa de detalhe)
    area_uv = area_3d = 0.0
    for (o, peso), sel in zip(objs, sels):
        me = o.data
        me.calc_loop_triangles()
        nt = len(me.loop_triangles)
        tl = np.empty(nt * 3, dtype=np.int32)
        me.loop_triangles.foreach_get("loops", tl)
        tp = np.empty(nt, dtype=np.int32)
        me.loop_triangles.foreach_get("polygon_index", tp)
        lv = np.empty(len(me.loops), dtype=np.int32)
        me.loops.foreach_get("vertex_index", lv)
        uv = np.empty(len(me.loops) * 2, dtype=np.float32)
        me.attributes["UVMapa"].data.foreach_get("vector", uv)
        uv = uv.reshape(-1, 2).astype(np.float64)
        co = bc.np_verts(me)
        t = tl.reshape(-1, 3)[sel[tp]]
        a, b, c = uv[t[:, 0]], uv[t[:, 1]], uv[t[:, 2]]
        auv = 0.5 * np.abs((b[:, 0] - a[:, 0]) * (c[:, 1] - a[:, 1]) - (c[:, 0] - a[:, 0]) * (b[:, 1] - a[:, 1]))
        p0, p1, p2 = co[lv[t[:, 0]]], co[lv[t[:, 1]]], co[lv[t[:, 2]]]
        a3 = 0.5 * np.linalg.norm(np.cross(p1 - p0, p2 - p0), axis=1)
        o["uv_" + nome] = True
        o["uv_escala_" + nome] = float(math.sqrt(a3.sum() / max(auv.sum(), 1e-12)))   # metros por unidade de UV nesta peça
        o["troia_exportar"] = True
        area_uv += auv.sum()
        area_3d += a3.sum()
        print("[uv] %-8s %-22s %6d faces  %.3f m2  %.2f m por UV" % (nome, o.name, int(sel.sum()), a3.sum(), o["uv_escala_" + nome]))
    print("[uv] %s: %.2f m2 em %.0f%% do atlas" % (nome, area_3d, 100 * area_uv))
    return float(math.sqrt(area_3d / max(area_uv, 1e-12)))


def main():
    import json
    escalas = {}
    for nome, (mat, cor, pecas) in CONJUNTOS.items():
        # no linho as peças são sólidas com o mesmo material dos dois lados: só o lado de fora entra no atlas
        escalas[nome] = conjunto(nome, mat, cor, pecas, so_fora=(nome == "linho"))
    # metros por unidade de UV de cada atlas: o Unity usa para repetir o mapa de detalhe (martelado, trama) no tamanho certo
    with open(bc.PACOTE.replace("patroclo_blender", "patroclo_atlas") + ".json", "w", encoding="utf-8") as f:
        json.dump(escalas, f)
    bc.salvar(bpy.data.filepath)


main()
