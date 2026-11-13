"""Capa de lã do Pátroclo com simulação de tecido.

Uso: blender -b Arte/Patroclo/Patroclo.blend -P bl_capa.py -- [--quadros=N] [--so-sim]

O molde (capa_molde.py) começa plano atrás do personagem, preso por dois pontos (os broches); os pontos
sobem para os ombros e o pano cai sobre o corpo, a armadura e as pernas. O corpo fica de braços abaixados
na simulação, para o pano não atravessar os braços na pose de jogo. O resultado vira o objeto "Capa".
"""
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bmesh
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

import bl_comum as bc
import capa_molde as cm

T0 = time.time()
# peças em que o pano encosta: (objeto, fração de triângulos mantida, folga em metros).
# A folga do corselete é maior porque as escamas e o cinturão ficam por cima dele.
COLISORES = [("Corpo", 0.12, 0.006), ("Corselete", 0.3, 0.013), ("Epomides", 0.3, 0.007), ("Quiton", 0.3, 0.006),
             ("Pteruges", 0.4, 0.006), ("Cnemides", 0.25, 0.006), ("Bracadeiras", 0.25, 0.006), ("Elmo_Casco", 0.1, 0.006)]
NU, NV = 111, 116          # colunas e linhas do molde (células de ~0,5 cm na gola a ~1,4 cm na barra)
Y0, Z0 = 0.42, 1.44        # plano inicial do molde: atrás do personagem, gola abaixo da nuca do elmo
SIM_NPY = os.path.join(bc.ARTE, "troca", "capa_sim.npy")


def criar_colisores():
    """Uma cópia simplificada de cada peça (na pose atual), com modificador de colisão."""
    objs = []
    total = 0
    corpo = bc.bvh_de(["Corpo"])
    for nome, ratio, folga in COLISORES:
        o = bpy.data.objects.get(nome)
        if o is None:
            print("[capa] AVISO: sem o objeto", nome)
            continue
        me = bc.malha_avaliada(o)
        me.name = "Colisor_" + nome
        if nome != "Corpo":
            # As peças têm espessura (face de fora e face de dentro). Com as duas, a de dentro puxa o pano
            # para dentro do corpo; fica só a de fora: a que olha para longe da pele mais próxima.
            bm = bmesh.new()
            bm.from_mesh(me)
            fora = []
            for f in bm.faces:
                c = f.calc_center_median()
                perto = corpo.find_nearest(c)[0]
                if perto is not None and (c - perto).dot(f.normal) <= 0.0:
                    fora.append(f)
            bmesh.ops.delete(bm, geom=fora, context='FACES')
            bm.to_mesh(me)
            bm.free()
        obj = bpy.data.objects.new("Colisor_" + nome, me)
        bc.colecao("Apoio").objects.link(obj)
        md = obj.modifiers.new("Dizimar", 'DECIMATE')
        md.ratio = ratio
        bc.ativo(obj)
        bpy.ops.object.modifier_apply(modifier=md.name)
        obj.modifiers.new("Colisao", 'COLLISION')
        obj.collision.thickness_outer = folga
        obj.collision.thickness_inner = 0.004
        obj.collision.cloth_friction = 4.0
        obj.collision.damping = 0.5
        obj.hide_render = True
        total += len(obj.data.polygons)
        objs.append(obj)
    print("[capa] colisores: %d peças, %d triângulos" % (len(objs), total))
    return objs


def molde():
    """Grade em leque recortada pelos rasgos. Devolve (u, v) dos vértices e as faces (quadriláteros)."""
    v = np.linspace(0.0, cm.L, NV)
    s = np.linspace(-1.0, 1.0, NU)
    V, Sg = np.meshgrid(v, s, indexing='ij')
    U = Sg * cm.meia_largura(V)
    d = cm.pano(U, V)
    # a célula fica se algum ponto dela (cantos, meios das arestas, centro) ainda é tecido; a sobra o alfa da textura corta
    dc = cm.pano((U[:-1, :-1] + U[1:, 1:] + U[:-1, 1:] + U[1:, :-1]) / 4, (V[:-1, :-1] + V[1:, 1:]) / 2)
    mx = np.maximum.reduce([d[:-1, :-1], d[1:, :-1], d[:-1, 1:], d[1:, 1:], dc])
    # ... e ficam também as células até 3,5 cm abaixo de tecido: é onde a textura desenha os fios pendurados da barra
    d2 = cm.pano(U, np.maximum(V - 0.035, 0.0))
    abaixo = np.maximum.reduce([d2[:-1, :-1], d2[1:, :-1], d2[:-1, 1:], d2[1:, 1:]]) > 0.001
    keep = (mx > -0.003) | (abaixo & (V[:-1, :-1] > cm.L - 0.5))
    idx = np.arange(NV * NU).reshape(NV, NU)
    quads = np.stack([idx[:-1, :-1], idx[:-1, 1:], idx[1:, 1:], idx[1:, :-1]], -1)[keep]
    # só o maior pedaço ligado (retalhos soltos cairiam no chão)
    pai = np.arange(NV * NU)

    def raiz(i):
        while pai[i] != i:
            pai[i] = pai[pai[i]]
            i = pai[i]
        return i
    for q in quads:
        r0 = raiz(q[0])
        for k in q[1:]:
            rk = raiz(k)
            if rk != r0:
                pai[rk] = r0
    raizes = np.array([raiz(q[0]) for q in quads])
    vals, cont = np.unique(raizes, return_counts=True)
    quads = quads[raizes == vals[np.argmax(cont)]]
    usados = np.unique(quads)
    novo = np.full(NV * NU, -1)
    novo[usados] = np.arange(len(usados))
    return U.ravel()[usados], V.ravel()[usados], novo[quads]


def ombros(bvh):
    """Ponto de cada broche: em cima do ombro, sobre a aba do corselete."""
    pts = {}
    for lado, sx in (("L", 1.0), ("R", -1.0)):
        melhor = None
        for y in (0.015,):
            hit = bvh.ray_cast(Vector((sx * 0.162, y, 1.9)), Vector((0, 0, -1)))
            if hit[0] is not None:
                melhor = hit[0]
        pts[lado] = melhor
    return pts


def simular():
    a = bc.args()
    quadros = 170
    for x in a:
        if x.startswith("--quadros="):
            quadros = int(x[10:])
    sc = bpy.context.scene
    arm = bc.esqueleto()
    for o in list(bpy.data.objects):
        if o.name.startswith("Colisor") or o.name == "Capa_Sim":
            bpy.data.objects.remove(o, do_unlink=True)

    bc.posar(bc.POSE_BRACOS_BAIXOS)
    cols = criar_colisores()
    bvh = bc.bvh_de(["Corpo", "Epomides", "Corselete"])
    bc.posar(None)
    pinos = ombros(bvh)
    print("[capa] ombros:", {k: tuple(round(c, 3) for c in p) for k, p in pinos.items()})

    U, V, quads = molde()
    n = len(U)
    base = np.stack([U, np.full(n, Y0), Z0 - V], -1)
    me = bpy.data.meshes.new("Capa_Sim")
    me.from_pydata(base.tolist(), [], quads.tolist())
    me.update()
    obj = bpy.data.objects.new("Capa_Sim", me)
    bc.colecao("Apoio").objects.link(obj)
    obj["molde_u"] = U.tolist()
    obj["molde_v"] = V.tolist()

    # Pinos: cada broche prende um trecho da gola franzido em pregas (é o que cria as dobras em leque).
    # O pano chega aos ombros em três etapas: por trás e abaixo da nuca do elmo, acima do ombro, sobre a aba.
    via = base.copy()
    via2 = base.copy()
    fim = base.copy()
    peso = np.zeros(n)
    ex = np.array([1.0, 0, 0])
    dv = np.array([0.0, 0.93, -0.37])
    dv /= np.linalg.norm(dv)
    nrm = np.cross(ex, dv)
    centros = {}
    for lado, sx in (("L", 1.0), ("R", -1.0)):
        du, dvv = U - sx * cm.PINO_U, V - cm.PINO_V
        m = (np.abs(du) < cm.FRANZIDO / 2) & (V < 0.042)
        peso[m] = 1.0
        p = np.array(pinos[lado]) + np.array([0, 0, 0.008])
        centros[lado] = p
        k = du[m] / (cm.FRANZIDO / 2)
        prega = 0.0045 * np.cos(du[m] * 2 * np.pi / 0.035)
        alvo = p[None, :] + (k * 0.017)[:, None] * ex + dvv[m, None] * dv + prega[:, None] * nrm
        fim[m] = alvo
        pv = np.array([p[0] + sx * 0.02, 0.22, p[2] - 0.1])
        via[m] = pv[None, :] + (k * 0.017)[:, None] * ex + dvv[m, None] * np.array([0, 0, -1.0]) + prega[:, None] * np.array([0, 1.0, 0])
        via2[m] = alvo + np.array([0.0, 0.11, 0.075])
    print("[capa] molde: %d vértices, %d faces, %d presos" % (n, len(quads), int(peso.sum())))
    vg = obj.vertex_groups.new(name="Pino")
    vg.add(np.nonzero(peso)[0].tolist(), 1.0, 'REPLACE')
    obj.shape_key_add(name="Basis")
    k1 = obj.shape_key_add(name="Via")
    k2 = obj.shape_key_add(name="Via2")
    k3 = obj.shape_key_add(name="Fim")
    k1.data.foreach_set("co", via.ravel())
    k2.data.foreach_set("co", via2.ravel())
    k3.data.foreach_set("co", fim.ravel())
    f1, f2, f3 = 30, 60, 92
    for kb, chaves in ((k1, ((1, 0.0), (f1, 1.0), (f2, 0.0))), (k2, ((1, 0.0), (f1, 0.0), (f2, 1.0), (f3, 0.0))), (k3, ((1, 0.0), (f2, 0.0), (f3, 1.0)))):
        for f, val in chaves:
            kb.value = val
            kb.keyframe_insert("value", frame=f)

    md = obj.modifiers.new("Pano", 'CLOTH')
    st = md.settings
    st.quality = 7
    st.mass = 0.02
    st.tension_stiffness = 80
    st.compression_stiffness = 25
    st.shear_stiffness = 30
    st.bending_stiffness = 0.12
    st.tension_damping = 10
    st.compression_damping = 10
    st.shear_damping = 10
    st.bending_damping = 0.5
    st.air_damping = 2.2
    st.vertex_group_mass = "Pino"
    st.pin_stiffness = 1.0
    cs = md.collision_settings
    cs.use_collision = True
    cs.collision_quality = 2
    cs.distance_min = 0.005
    cs.friction = 4
    cs.use_self_collision = True
    cs.self_distance_min = 0.002
    cs.self_friction = 6
    # segunda fase: o escudo das costas chega por trás e prensa o pano (variante "Capa_Escudo")
    fase2 = 95
    esc = bpy.data.objects.get("Escudo_Costas_Aspis")
    col_esc = None
    if esc is not None:
        mesc = bc.malha_avaliada(esc)
        col_esc = bpy.data.objects.new("Colisor_Escudo", mesc)
        bc.colecao("Apoio").objects.link(col_esc)
        dz = col_esc.modifiers.new("Dizimar", 'DECIMATE')
        dz.ratio = 0.6
        bc.ativo(col_esc)
        bpy.ops.object.modifier_apply(modifier=dz.name)
        col_esc.modifiers.new("Colisao", 'COLLISION')
        col_esc.collision.thickness_outer = 0.008
        col_esc.collision.thickness_inner = 0.004
        col_esc.collision.cloth_friction = 4.0
        col_esc.hide_render = True
        for f, dy in ((1, 1.2), (quadros, 1.2), (quadros + 55, 0.0)):
            col_esc.location = (0.0, dy, 0.0)
            col_esc.keyframe_insert("location", frame=f)
    total = quadros + (fase2 if col_esc is not None else 0)
    md.point_cache.frame_start = 1
    md.point_cache.frame_end = total
    sc.frame_start, sc.frame_end = 1, total

    t = time.time()
    co_livre = None
    for f in range(1, total + 1):
        sc.frame_set(f)
        if f == quadros:
            co_livre = bc.np_verts(obj.evaluated_get(bpy.context.evaluated_depsgraph_get()).data)
        if f % 10 == 0:
            ev = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
            co = bc.np_verts(ev.data)
            print("[capa] quadro %3d  %.0f s  z min %.3f  y max %.3f" % (f, time.time() - t, co[:, 2].min(), co[:, 1].max()), flush=True)
    ev = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    co_escudo = bc.np_verts(ev.data) if col_esc is not None else None
    co = co_livre
    np.save(SIM_NPY, co)
    if co_escudo is not None:
        np.save(SIM_NPY.replace(".npy", "_escudo.npy"), co_escudo)
    # comprimento das arestas contra o molde: mostra se o pano esticou demais
    e = np.array([(ed.vertices[0], ed.vertices[1]) for ed in me.edges])
    l0 = np.linalg.norm(base[e[:, 0]] - base[e[:, 1]], axis=1)
    l1 = np.linalg.norm(co[e[:, 0]] - co[e[:, 1]], axis=1)
    print("[capa] esticou: média %.1f%%, pior %.1f%%" % (100 * (l1 / l0 - 1).mean(), 100 * (l1 / l0 - 1).max()))
    for o in list(bpy.data.objects):
        if o.name.startswith("Colisor") or o.name == "Capa_Sim":
            bpy.data.objects.remove(o, do_unlink=True)
    np.savez(SIM_NPY.replace(".npy", "_molde.npz"), U=U, V=V, quads=quads, cL=centros["L"], cR=centros["R"], nrm=nrm)
    print("[capa] simulação em %.0f s" % (time.time() - T0))
    return co, U, V, quads, centros, nrm, co_escudo


def pesos_do_corpo():
    """Vértices do corpo em repouso com os pesos de cada um (matriz vértice x osso)."""
    o = bpy.data.objects["Corpo"]
    me = o.data
    nomes = [g.name for g in o.vertex_groups]
    w = np.zeros((len(me.vertices), len(nomes)), dtype=np.float32)
    for v in me.vertices:
        for g in v.groups:
            w[v.index, g.group] = g.weight
    w /= np.maximum(w.sum(1, keepdims=True), 1e-9)
    # ossos que o pano usa mas que nenhum vértice da pele segue (o quadril "root") entram com coluna vazia
    for extra in ("root", "upperleg01.L", "upperleg01.R"):
        if extra not in nomes:
            nomes.append(extra)
            w = np.concatenate([w, np.zeros((len(w), 1), dtype=np.float32)], 1)
    return bc.np_verts(me), w, nomes


def ss(e0, e1, x):
    t = np.clip((np.asarray(x, dtype=np.float64) - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def pesos_do_pano(co, quads):
    """Pesos contínuos: em cima a pele das costas mais próxima; descendo passa para o quadril e, perto das pernas, para as coxas."""
    from mathutils.kdtree import KDTree
    bco, bw, nomes = pesos_do_corpo()
    dom = np.array(nomes)[bw.argmax(1)]
    tronco = np.array([d.startswith(("spine0", "clavicle", "neck01")) or d == "root" for d in dom])
    perna = np.array([d.startswith(("upperleg0", "lowerleg0")) for d in dom])
    it = np.nonzero(tronco)[0]
    kt = KDTree(len(it))
    for k, i in enumerate(it):
        kt.insert(bco[i], k)
    kt.balance()
    ip = np.nonzero(perna)[0]
    kp = KDTree(len(ip))
    for k, i in enumerate(ip):
        kp.insert(bco[i], k)
    kp.balance()
    n = len(co)
    w = np.zeros((n, len(nomes)), dtype=np.float64)
    dperna = np.zeros(n)
    for i in range(n):
        _, k, _ = kt.find(co[i])
        w[i] = bw[it[k]]
        dperna[i] = kp.find(co[i])[2]
    # só ossos do tronco: o pano não segue os braços (ficaria esticado quando eles levantam)
    usar = np.array([nm.startswith(("spine0", "clavicle", "neck0")) or nm == "root" for nm in nomes])
    w[:, ~usar] = 0
    w /= np.maximum(w.sum(1, keepdims=True), 1e-9)
    # suaviza pela malha, para não haver saltos entre vértices vizinhos
    viz = [[] for _ in range(n)]
    for q in quads:
        for a in range(4):
            viz[q[a]].append(q[(a + 1) % 4])
            viz[q[a]].append(q[(a + 3) % 4])
    mx = max(len(v) for v in viz)
    idx = np.array([v + [i] * (mx - len(v)) for i, v in enumerate(viz)])
    for _ in range(30):
        w = 0.5 * w + 0.5 * w[idx].mean(1)
    z, x = co[:, 2], co[:, 0]
    iroot, il, ir = nomes.index("root"), nomes.index("upperleg01.L"), nomes.index("upperleg01.R")
    quadril = np.zeros_like(w)
    quadril[:, iroot] = 1
    k = ss(1.2, 0.95, z)[:, None]
    w = w * (1 - k) + quadril * k
    f = (0.8 * ss(0.24, 0.09, dperna) * ss(0.98, 0.8, z))[:, None]
    coxa = np.zeros_like(w)
    lado = ss(-0.06, 0.06, x)            # no Blender, +X é o lado esquerdo do personagem
    coxa[:, il] = lado
    coxa[:, ir] = 1 - lado
    w = w * (1 - f) + coxa * f
    for _ in range(8):
        w = 0.5 * w + 0.5 * w[idx].mean(1)
    w /= np.maximum(w.sum(1, keepdims=True), 1e-9)
    return {nm: w[:, j] for j, nm in enumerate(nomes) if w[:, j].max() > 1e-3}, (bco, bw, nomes, it, kt)


def broches(centros, nrm, corpo):
    """Dois discos de bronze com aro de ouro que prendem a capa nos ombros."""
    bco, bw, nomes, it, kt = corpo
    perfil = [(0.035, -0.004, 1), (0.0375, -0.001, 1), (0.0365, 0.0025, 1), (0.032, 0.005, 1), (0.0285, 0.0048, 1), (0.0265, 0.003, 0),
              (0.021, 0.0052, 0), (0.014, 0.0082, 0), (0.0085, 0.0098, 1), (0.004, 0.0122, 1), (0.0, 0.0128, 1)]
    seg = 40
    vs, fs, ms, uvs = [], [], [], []
    for lado in ("L", "R"):
        c = centros[lado] + nrm * 0.006
        e1 = np.array([1.0, 0, 0])
        e2 = np.cross(nrm, e1)
        b0 = len(vs)
        for r, h, _ in perfil:
            for j in range(seg):
                a = 2 * np.pi * j / seg
                vs.append(c + (e1 * np.cos(a) + e2 * np.sin(a)) * r + nrm * h)
                uvs.append((r * np.cos(a) / 0.3, r * np.sin(a) / 0.3))
        for i in range(len(perfil) - 1):
            for j in range(seg):
                j2 = (j + 1) % seg
                fs.append((b0 + i * seg + j, b0 + i * seg + j2, b0 + (i + 1) * seg + j2, b0 + (i + 1) * seg + j))
                ms.append(perfil[i][2] if perfil[i][2] == perfil[i + 1][2] else 1)
    obj = bc.novo_objeto("Capa_Broches", vs, fs, "Roupa", "Pat_Capa_Broches", "Roupa/Capa_Broches")
    me = obj.data
    me.materials.append(bpy.data.materials["M_Pat_Bronze"])
    me.materials.append(bpy.data.materials["M_Pat_Ouro"])
    me.polygons.foreach_set("material_index", np.array(ms, dtype=np.int32))
    cantos = np.empty(len(me.loops), dtype=np.int32)
    me.loops.foreach_get("vertex_index", cantos)
    me.attributes.new("UVMetro", 'FLOAT2', 'CORNER').data.foreach_set("vector", np.array(uvs, dtype=np.float32)[cantos].ravel())
    me.shade_smooth()
    # cada broche inteiro segue o osso do ponto de pele mais próximo do centro dele
    w = np.zeros((len(vs), len(nomes)))
    por = len(perfil) * seg
    for k, lado in enumerate(("L", "R")):
        _, kk, _ = kt.find(centros[lado])
        w[k * por:(k + 1) * por] = bw[it[kk]]
    bc.pesos_por_osso(obj, {nm: w[:, j] for j, nm in enumerate(nomes) if w[:, j].max() > 1e-3})
    return obj


def peca(nome, co, U, V, quads):
    obj = bc.novo_objeto(nome, co, quads, "Roupa", "Pat_" + nome, "Roupa/" + nome)
    me = obj.data
    tex = os.path.join(bc.TEXTURAS, "Blender")
    mat = bc.material_tex("M_Pat_CapaPano", "#FFFFFF", os.path.join(tex, "capa_albedo.png"), os.path.join(tex, "capa_normal.png"),
                          os.path.join(tex, "capa_mascara.png"), alfa=True, rug=0.85)
    if not os.path.exists(os.path.join(tex, "capa_albedo.png")):
        mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = bc.hexa("#8E1A1E")
    me.materials.append(mat)
    cantos = np.empty(len(me.loops), dtype=np.int32)
    me.loops.foreach_get("vertex_index", cantos)
    # UVMapa: o molde inteiro na textura (u para os lados, v = 1 na gola); UVMetro: em metros, para a sujeira
    uv = np.stack([0.5 + U / cm.W_BARRA, 1.0 - V / cm.L], -1)
    me.attributes.new("UVMapa", 'FLOAT2', 'CORNER').data.foreach_set("vector", uv[cantos].astype(np.float32).ravel())
    um = np.stack([U, -V], -1) / 0.5
    me.attributes.new("UVMetro", 'FLOAT2', 'CORNER').data.foreach_set("vector", um[cantos].astype(np.float32).ravel())
    me.shade_smooth()
    pesos, corpo = pesos_do_pano(co, quads)
    bc.pesos_por_osso(obj, pesos)
    sub = obj.modifiers.new("Subdivisao", 'SUBSURF')
    sub.levels = sub.render_levels = 1
    obj["molde_largura"] = cm.W_BARRA
    obj["molde_comprimento"] = cm.L
    print("[capa] %s: %d vértices, %d ossos" % (nome, len(co), len(pesos)))
    return corpo


def finalizar(co, U, V, quads, centros, nrm, co_escudo=None):
    bc.aposentar("Capa")
    corpo = peca("Capa", co, U, V, quads)
    if co_escudo is not None:
        # a mesma capa prensada pelo escudo das costas; o painel do Unity mostra uma ou outra
        peca("Capa_Escudo", co_escudo, U, V, quads)
        bpy.data.objects["Capa_Escudo"].hide_viewport = bpy.data.objects["Capa_Escudo"].hide_render = True
    broches(centros, nrm, corpo)


def main():
    so_final = "--so-final" in bc.args()
    if so_final:
        co = np.load(SIM_NPY)
        d = np.load(SIM_NPY.replace(".npy", "_molde.npz"))
        esc = SIM_NPY.replace(".npy", "_escudo.npy")
        dados = (co, d["U"], d["V"], d["quads"], {"L": d["cL"], "R": d["cR"]}, d["nrm"], np.load(esc) if os.path.exists(esc) else None)
    else:
        dados = simular()
    finalizar(*dados)
    bc.salvar(bpy.data.filepath)

main()
