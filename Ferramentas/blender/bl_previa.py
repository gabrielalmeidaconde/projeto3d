"""Renderiza prévias do arquivo aberto (Cycles na placa de vídeo), para conferir o trabalho sem abrir o Blender.

Uso: blender -b Patroclo.blend -P bl_previa.py -- <pasta_saida> [opções] vista1 vista2 ...
  vista: nome pronto (frente, costas, tq, tq_costas, perfil, elmo, torso, pernas, capa)
         ou  nome:ex,ey,ez:tx,ty,tz:lente   (olho e alvo em metros, eixos do Blender)
  opções: --res=LxA  --amostras=N  --esconder=Colecao1,Colecao2  --mostrar=Obj1,Obj2  --barro  --quadro=N
          --pose=neutra|baixos   --ossos='{"upperarm01.L": [0, 0, 33]}'  (graus, como no painel do Unity)
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
from mathutils import Vector

import bl_comum as bc

VISTAS = {
    "frente": ((0, -3.5, 1.0), (0, 0, 0.98), 50),
    "costas": ((0, 3.5, 1.0), (0, 0, 0.98), 50),
    "tq": ((-2.2, -2.75, 1.25), (0, 0, 0.98), 50),
    "tq_costas": ((2.2, 2.75, 1.3), (0, 0, 0.98), 50),
    "tq_costas_e": ((-2.2, 2.75, 1.3), (0, 0, 0.98), 50),
    "perfil": ((-3.5, 0, 1.0), (0, 0, 0.98), 50),
    "perfil_e": ((3.5, 0, 1.0), (0, 0, 0.98), 50),
    "elmo": ((-0.5, -1.25, 1.74), (0, 0, 1.73), 85),
    "elmo_perfil": ((-1.35, -0.05, 1.76), (0, 0, 1.75), 85),
    "elmo_costas": ((0.5, 1.3, 1.8), (0, 0, 1.72), 85),
    "torso": ((-0.4, -1.6, 1.35), (0, 0, 1.3), 70),
    "cintura": ((-0.5, -1.5, 1.0), (0, 0, 0.98), 70),
    "pernas": ((-0.6, -1.9, 0.45), (0, 0, 0.42), 60),
    "capa": ((0.7, 2.6, 1.15), (0, 0, 0.95), 50),
    "ombros_costas": ((0.45, 1.5, 1.6), (0, 0, 1.42), 70),
    "cima": ((0, 1.2, 3.2), (0, 0, 1.4), 50),
}


def main():
    a = bc.args()
    pasta = a[0]
    os.makedirs(pasta, exist_ok=True)
    res, amostras, barro, quadro = (800, 1000), 48, False, None
    esconder, mostrar, vistas = [], [], []
    pose = {}
    for x in a[1:]:
        if x.startswith("--res="):
            res = tuple(int(v) for v in x[6:].split("x"))
        elif x.startswith("--amostras="):
            amostras = int(x[11:])
        elif x.startswith("--esconder="):
            esconder = x[11:].split(",")
        elif x.startswith("--mostrar="):
            mostrar = x[10:].split(",")
        elif x.startswith("--quadro="):
            quadro = int(x[9:])
        elif x == "--barro":
            barro = True
        elif x.startswith("--pose="):
            pose.update(bc.POSE_NEUTRA if x[7:] == "neutra" else bc.POSE_BRACOS_BAIXOS)
        elif x.startswith("--ossos="):
            import json
            pose.update({k: tuple(v) for k, v in json.loads(x[8:]).items()})
        else:
            vistas.append(x)
    sc = bpy.context.scene
    if quadro is not None:
        sc.frame_set(quadro)
    if pose:
        bc.posar(pose)
    for nome in esconder:
        c = bpy.data.collections.get(nome)
        if c is not None:
            for o in c.all_objects:
                o.hide_render = True
        elif nome in bpy.data.objects:
            bpy.data.objects[nome].hide_render = True
    for nome in mostrar:
        if nome in bpy.data.objects:
            bpy.data.objects[nome].hide_render = False
    sc.render.engine = 'CYCLES'
    try:
        p = bpy.context.preferences.addons['cycles'].preferences
        p.compute_device_type = 'OPTIX'
        p.get_devices()
        for d in p.devices:
            d.use = d.type == 'OPTIX'
        sc.cycles.device = 'GPU'
    except Exception as e:
        print("[previa] sem GPU:", e)
    sc.cycles.samples = amostras
    sc.cycles.use_denoising = True
    sc.cycles.use_adaptive_sampling = True
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = 'JPEG'
    sc.render.image_settings.quality = 90
    sc.view_settings.view_transform = 'Filmic' if 'Filmic' in [i.identifier for i in bpy.types.ColorManagedViewSettings.bl_rna.properties['view_transform'].enum_items] else 'AgX'
    # mundo: céu de estúdio neutro, mais luz principal quente e contraluz fria
    w = bpy.data.worlds.new("Previa")
    w.use_nodes = True
    bg = w.node_tree.nodes.get("Background")
    hdr = os.path.join(bc.TEXTURAS, "PolyHaven", "estudio_reflexo.hdr")
    if os.path.exists(hdr):
        env = w.node_tree.nodes.new("ShaderNodeTexEnvironment")
        env.image = bpy.data.images.load(hdr)
        w.node_tree.links.new(env.outputs["Color"], bg.inputs["Color"])
        bg.inputs["Strength"].default_value = 0.9
    else:
        bg.inputs["Color"].default_value = (0.5, 0.52, 0.56, 1)
        bg.inputs["Strength"].default_value = 1.0
    sc.world = w
    sc.render.film_transparent = False

    def luz(nome, pos, energia, cor, tam):
        l = bpy.data.lights.new(nome, 'AREA')
        l.energy, l.color, l.size = energia, cor, tam
        o = bpy.data.objects.new(nome, l)
        sc.collection.objects.link(o)
        o.location = pos
        o.rotation_euler = (Vector((0, 0, 1.1)) - Vector(pos)).to_track_quat('-Z', 'Y').to_euler()
    luz("Chave", (-2.5, -3.0, 3.2), 900, (1.0, 0.93, 0.85), 1.5)
    luz("Contra", (2.5, 3.0, 2.6), 700, (0.8, 0.88, 1.0), 1.5)
    # chão para a sombra de contato
    bpy.ops.mesh.primitive_plane_add(size=30, location=(0, 0, 0))
    chao = bpy.context.active_object
    mc = bpy.data.materials.new("PreviaChao")
    mc.use_nodes = True
    mc.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.22, 0.22, 0.24, 1)
    mc.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.8
    chao.data.materials.append(mc)
    if barro:
        mb = bpy.data.materials.new("PreviaBarro")
        mb.use_nodes = True
        mb.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.55, 0.5, 0.46, 1)
        mb.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.65
        sc.view_layers[0].material_override = mb
    cam = bpy.data.objects.new("PreviaCam", bpy.data.cameras.new("PreviaCam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.clip_start = 0.05
    for v in vistas:
        if ":" in v:
            nome, e, t, lente = v.split(":")
            olho = tuple(float(x) for x in e.split(","))
            alvo = tuple(float(x) for x in t.split(","))
            lente = float(lente)
        else:
            nome = v
            olho, alvo, lente = VISTAS[v]
        cam.location = olho
        cam.rotation_euler = (Vector(alvo) - Vector(olho)).to_track_quat('-Z', 'Y').to_euler()
        cam.data.lens = lente
        sc.render.filepath = os.path.join(pasta, nome + ".jpg")
        bpy.ops.render.render(write_still=True)
        print("[previa]", sc.render.filepath)


main()
