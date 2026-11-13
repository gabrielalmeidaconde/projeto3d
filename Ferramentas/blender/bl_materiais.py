"""Liga as texturas do jogo nos materiais do arquivo do Blender, para ele abrir com a cara do personagem pronto.

Uso: blender -b Arte/Patroclo/Patroclo.blend -P bl_materiais.py     (depois dos pintores)

Só afeta a visualização dentro do Blender (vista "Material Preview" / render); o Unity tem os materiais dele.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy

import bl_comum as bc

T = bc.TEXTURAS
PH, BL, MH = os.path.join(T, "PolyHaven"), os.path.join(T, "Blender"), os.path.join(T, "MakeHuman")


def ph(n):
    return os.path.join(PH, n)


# material -> (cor que tinge, albedo, normal, máscara, alfa, metal, rugosidade)
MATS = {
    "M_Pat_Bronze": ("#A26A3A", ph("bronze_albedo.jpg"), ph("bronze_normal.jpg"), None, False, 1.0, 0.38),
    "M_Pat_BronzeEscuro": ("#5E3A1E", ph("bronze_albedo.jpg"), ph("bronze_normal.jpg"), None, False, 1.0, 0.5),
    "M_Pat_Ouro": ("#F2C65C", ph("bronze_albedo.jpg"), ph("bronze_normal.jpg"), None, False, 1.0, 0.32),
    "M_Pat_Lamina": ("#D8AA5F", ph("bronze_albedo.jpg"), ph("bronze_normal.jpg"), None, False, 1.0, 0.28),
    "M_Pat_Couro": ("#F2E6D9", ph("couro_albedo.jpg"), ph("couro_normal.jpg"), None, False, 0.0, 0.6),
    "M_Pat_Linho": ("#D6C6A2", ph("linho_albedo.jpg"), ph("linho_normal.jpg"), None, False, 0.0, 0.85),
    "M_Pat_Capa": ("#A21E22", ph("la_albedo.jpg"), ph("la_normal.jpg"), None, False, 0.0, 0.9),
    "M_Pat_Corte": ("#140D0A", None, None, None, False, 0.0, 0.95),
    "M_Pat_Emblema": ("#6E1015", ph("bronze_albedo.jpg"), ph("bronze_normal.jpg"), None, False, 0.2, 0.55),
    "M_Pat_Crista": ("#B01C22", os.path.join(BL, "crina.png"), os.path.join(BL, "crina_normal.png"), None, True, 0.0, 0.7),
    "M_Pat_CapaPano": ("#FFFFFF", os.path.join(BL, "capa_albedo.png"), os.path.join(BL, "capa_normal.jpg"), os.path.join(BL, "capa_mascara.png"), True, 0.0, 0.88),
    "M_Pat_BronzePintado": ("#FFFFFF", os.path.join(BL, "bronze_p_albedo.jpg"), os.path.join(BL, "bronze_p_normal.jpg"), os.path.join(BL, "bronze_p_mascara.png"), False, 1.0, 0.4),
    "M_Pat_LinhoPintado": ("#FFFFFF", os.path.join(BL, "linho_p_albedo.jpg"), os.path.join(BL, "linho_p_normal.jpg"), os.path.join(BL, "linho_p_mascara.png"), False, 0.0, 0.8),
    "M_Pat_Pele": ("#E4D6CC", os.path.join(MH, "pele_albedo.png"), os.path.join(MH, "pele_normal.png"), None, False, 0.0, 0.5),
    "M_Pat_Olhos": ("#FFFFFF", os.path.join(MH, "olho_castanho.png"), None, None, True, 0.0, 0.15),
    "M_Pat_Sobrancelhas": ("#FFFFFF", os.path.join(MH, "sobrancelha.png"), None, None, True, 0.0, 0.7),
    "M_Pat_Cilios": ("#FFFFFF", os.path.join(MH, "cilios.png"), None, None, True, 0.0, 0.7),
    "M_Pat_Cabelo": ("#E0A87C", os.path.join(MH, "cabelo_diffuse.png"), None, None, True, 0.0, 0.6),
}


def main():
    feitos = 0
    for nome, (cor, alb, nor, msk, alfa, metal, rug) in MATS.items():
        if bpy.data.materials.get(nome) is None:
            continue
        bc.material_tex(nome, cor, alb, nor, msk, alfa=alfa, metal=metal, rug=rug)
        feitos += 1
    # quem abrir o arquivo já vê o personagem com material (e não a malha cinza)
    for scr in bpy.data.screens:
        for a in scr.areas:
            if a.type == 'VIEW_3D':
                for sp in a.spaces:
                    if sp.type == 'VIEW_3D':
                        sp.shading.type = 'MATERIAL'
    col = bpy.data.collections.get("Procedural")
    if col is not None:
        col.hide_viewport = col.hide_render = True
    print("[materiais] %d materiais com textura" % feitos)
    bc.salvar(bpy.data.filepath)


main()
