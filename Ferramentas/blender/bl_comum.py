"""Funções comuns aos scripts do Blender do Pátroclo (rodam dentro do Blender 5.x, sem janela)."""
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector

AQUI = os.path.dirname(os.path.abspath(__file__))
PROJETO = os.path.abspath(os.path.join(AQUI, "..", ".."))
ARTE = os.path.join(PROJETO, "Arte", "Patroclo")
BLEND = os.path.join(ARTE, "Patroclo.blend")
BASE = os.path.join(ARTE, "troca", "base")
PACOTE = os.path.join(PROJETO, "Assets", "Troia", "Personagens", "Patroclo", "Fonte", "patroclo_blender")
TEXTURAS = os.path.join(PROJETO, "Assets", "Troia", "Personagens", "Patroclo", "Texturas")

if AQUI not in sys.path:
    sys.path.insert(0, AQUI)


def args():
    return sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def hexa(h, a=1.0):
    """Cor #RRGGBB (sRGB) em linear, como o Blender espera."""
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c) + (a,)


def colecao(nome, pai=None):
    c = bpy.data.collections.get(nome)
    if c is None:
        c = bpy.data.collections.new(nome)
        (pai or bpy.context.scene.collection).children.link(c)
    return c


def mover_para(obj, col):
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    col.objects.link(obj)


def ativo(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.hide_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def np_verts(me):
    a = np.empty(len(me.vertices) * 3, dtype=np.float32)
    me.vertices.foreach_get("co", a)
    return a.reshape(-1, 3).astype(np.float64)


def np_tris(me):
    me.calc_loop_triangles()
    a = np.empty(len(me.loop_triangles) * 3, dtype=np.int32)
    me.loop_triangles.foreach_get("vertices", a)
    return a.reshape(-1, 3)


def esqueleto():
    return bpy.data.objects["Esqueleto"]


def salvar(caminho=BLEND):
    os.makedirs(os.path.dirname(caminho), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=caminho, compress=True, relative_remap=True)
    print("[blender] salvo", caminho, "%.1f MB" % (os.path.getsize(caminho) / 1e6))


# ---------------------------------------------------------------------------------------------
# Poses do painel do Unity (PatrocloDesign): rotações locais em graus, com os eixos do Unity.
# No repouso todos os ossos do Unity têm rotação zero e eixos iguais aos do mundo.
# ---------------------------------------------------------------------------------------------
POSE_BRACOS_BAIXOS = {"upperarm01.L": (0, 0, 33), "upperarm01.R": (0, 0, -33)}
POSE_NEUTRA = {
    "upperarm01.R": (30.6, 3.5, -5.6), "lowerarm01.R": (-42.6, -66.5, 34.2), "lowerarm02.R": (-4.2, 4.5, -6.5),
    "wrist.R": (-31.6, -34.1, -16.4), "upperarm01.L": (0, 0, 33), "lowerarm01.L": (0, 0, 0),
    "upperleg01.R": (0, 0, -2.5), "upperleg01.L": (0, 0, 2.5), "foot.R": (0, 0, 2.5), "foot.L": (0, 0, -2.5),
    "neck01": (-2, 4, 0), "head": (-2, 4, 0),
}
_C3 = np.array([[-1.0, 0, 0], [0, 0, -1], [0, 1, 0]])


def _euler_unity(e):
    """Quaternion.Euler(x, y, z) do Unity como matriz 3x3 (gira em Z, depois X, depois Y), já nos eixos do Blender."""
    x, y, z = np.radians(e)
    rx = np.array([[1, 0, 0], [0, np.cos(x), -np.sin(x)], [0, np.sin(x), np.cos(x)]])
    ry = np.array([[np.cos(y), 0, np.sin(y)], [0, 1, 0], [-np.sin(y), 0, np.cos(y)]])
    rz = np.array([[np.cos(z), -np.sin(z), 0], [np.sin(z), np.cos(z), 0], [0, 0, 1]])
    return _C3 @ (ry @ rx @ rz) @ _C3.T


def posar(tabela=None, arm=None):
    """Aplica no esqueleto uma pose escrita como no Unity (dict osso -> graus). None ou {} volta ao repouso."""
    arm = arm or esqueleto()
    tabela = tabela or {}
    feito = {}
    for pb in arm.pose.bones:  # a lista vem com os pais antes dos filhos
        b = pb.bone
        rest = np.array(b.matrix_local)
        head = rest[:3, 3]
        rl = _euler_unity(tabela[b.name]) if b.name in tabela else np.eye(3)
        if b.parent is not None:
            prw, phw, phead = feito[b.parent.name]
            rw = prw @ rl
            hw = phw + prw @ (head - phead)
        else:
            rw, hw = rl, head.copy()
        feito[b.name] = (rw, hw, head)
        t = np.eye(4)
        t[:3, :3] = rw
        t[:3, 3] = hw - rw @ head
        pose = t @ rest
        if b.parent is not None:
            prest = np.array(b.parent.matrix_local)
            prw, phw, phead = feito[b.parent.name]
            pt = np.eye(4)
            pt[:3, :3] = prw
            pt[:3, 3] = phw - prw @ phead
            basis = np.linalg.inv(rest) @ prest @ np.linalg.inv(pt @ prest) @ pose
        else:
            basis = np.linalg.inv(rest) @ pose
        pb.matrix_basis = Matrix(basis.tolist())
    bpy.context.view_layer.update()


def malha_avaliada(obj):
    """Cópia da malha do objeto com modificadores e pose aplicados, em coordenadas do mundo."""
    deps = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(deps)
    me = bpy.data.meshes.new_from_object(ev, depsgraph=deps)
    me.transform(obj.matrix_world)
    return me


def material_tex(nome, cor="#FFFFFF", albedo=None, normal=None, mascara=None, alfa=False, metal=0.0, rug=0.6, forca_normal=1.0):
    """Material Principled para a prévia no Blender: cor x textura, normal e máscara (R metal, A lisura) opcionais."""
    m = bpy.data.materials.get(nome) or bpy.data.materials.new(nome)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    p = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(p.outputs["BSDF"], out.inputs["Surface"])
    p.inputs["Metallic"].default_value = metal
    p.inputs["Roughness"].default_value = rug
    p.inputs["Base Color"].default_value = hexa(cor)
    if albedo and os.path.exists(albedo):
        t = nt.nodes.new("ShaderNodeTexImage")
        t.image = bpy.data.images.load(albedo, check_existing=True)
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = 'RGBA'
        mix.blend_type = 'MULTIPLY'
        mix.inputs[0].default_value = 1.0
        nt.links.new(t.outputs["Color"], mix.inputs[6])
        mix.inputs[7].default_value = hexa(cor)
        nt.links.new(mix.outputs[2], p.inputs["Base Color"])
        if alfa:
            nt.links.new(t.outputs["Alpha"], p.inputs["Alpha"])
    if normal and os.path.exists(normal):
        t = nt.nodes.new("ShaderNodeTexImage")
        t.image = bpy.data.images.load(normal, check_existing=True)
        t.image.colorspace_settings.name = 'Non-Color'
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nm.inputs["Strength"].default_value = forca_normal
        nt.links.new(t.outputs["Color"], nm.inputs["Color"])
        nt.links.new(nm.outputs["Normal"], p.inputs["Normal"])
    if mascara and os.path.exists(mascara):
        t = nt.nodes.new("ShaderNodeTexImage")
        t.image = bpy.data.images.load(mascara, check_existing=True)
        t.image.colorspace_settings.name = 'Non-Color'
        sep = nt.nodes.new("ShaderNodeSeparateColor")
        nt.links.new(t.outputs["Color"], sep.inputs["Color"])
        nt.links.new(sep.outputs["Red"], p.inputs["Metallic"])
        inv = nt.nodes.new("ShaderNodeMath")
        inv.operation = 'SUBTRACT'
        inv.inputs[0].default_value = 1.0
        nt.links.new(t.outputs["Alpha"], inv.inputs[1])
        nt.links.new(inv.outputs[0], p.inputs["Roughness"])
    m.diffuse_color = hexa(cor)
    return m


def novo_objeto(nome, verts, faces, colecao_nome, malha_unity=None, caminho=None, exportar=True):
    """Cria (ou recria) um objeto de malha; se já existir um com o mesmo nome, o antigo é apagado."""
    velho = bpy.data.objects.get(nome)
    if velho is not None:
        bpy.data.objects.remove(velho, do_unlink=True)
    me = bpy.data.meshes.new(malha_unity or nome)
    me.from_pydata([tuple(v) for v in verts], [], [tuple(int(i) for i in f) for f in faces])
    me.update()
    obj = bpy.data.objects.new(nome, me)
    colecao(colecao_nome).objects.link(obj)
    arm = bpy.data.objects.get("Esqueleto")
    if arm is not None:
        obj.parent = arm
        md = obj.modifiers.new("Esqueleto", 'ARMATURE')
        md.object = arm
    obj["troia_malha"] = malha_unity or nome
    obj["troia_caminho"] = caminho or nome
    obj["troia_rigida"] = False
    obj["troia_exportar"] = bool(exportar)
    return obj


def aposentar(nome):
    """A peça procedural que vai ser substituída fica guardada, escondida e fora da exportação."""
    o = bpy.data.objects.get(nome)
    if o is None or o.get("troia_procedural"):
        return o
    o.name = nome + "_Procedural"
    o["troia_procedural"] = True
    o["troia_exportar"] = False
    o.hide_viewport = o.hide_render = True
    mover_para(o, colecao("Procedural"))
    return o


def pesos_por_osso(obj, pesos):
    """pesos: dict nome_do_osso -> array (n_vertices) de pesos; cria os grupos de vértices."""
    for nome, w in pesos.items():
        idx = np.nonzero(w > 1e-4)[0]
        if len(idx) == 0:
            continue
        vg = obj.vertex_groups.get(nome) or obj.vertex_groups.new(name=nome)
        for i, x in zip(idx.tolist(), w[idx].tolist()):
            vg.add((i,), x, 'REPLACE')


def bvh_de(nomes):
    """Árvore de colisão com as malhas (já na pose atual, em coordenadas do mundo) dos objetos dados."""
    from mathutils.bvhtree import BVHTree
    vs, fs, base = [], [], 0
    for n in nomes:
        o = bpy.data.objects.get(n)
        if o is None:
            continue
        me = malha_avaliada(o)
        co = np_verts(me)
        tr = np_tris(me)
        vs.append(co)
        fs.append(tr + base)
        base += len(co)
        bpy.data.meshes.remove(me)
    if not vs:
        return None
    return BVHTree.FromPolygons(np.concatenate(vs).tolist(), np.concatenate(fs).tolist())
