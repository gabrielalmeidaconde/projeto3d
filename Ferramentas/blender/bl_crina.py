"""Crista e rabo de crina do elmo, feitos de mechas: cartões finos com textura de fios e alfa (crina.png).

Uso: blender -b Arte/Patroclo/Patroclo.blend -P bl_crina.py

Substitui as peças procedurais "Crista" (aleta lisa) e os dois rabos de crina. O suporte de bronze segue
o meridiano do elmo; a crina sai dele inclinada para trás, mais alta no topo, e continua num rabo comprido
que cai pelas costas por fora da capa (ou por cima do escudo, na versão com o escudo nas costas).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import numpy as np
from mathutils import Vector

import bl_comum as bc

CENTRO = np.array([0.0, -0.05, 1.74])     # centro do elmo (no Unity: 0, 1.74, 0.05)
M0, M1 = 37.0, 153.0                      # trecho do meridiano com crista (0 = testa, 90 = topo, 180 = nuca)
X = np.array([1.0, 0.0, 0.0])
rng = np.random.default_rng(5)
BRONZE, OURO, CRINA = 0, 1, 2
UV_BRONZE, UV_OURO = 0.5, 0.3             # metros por unidade de UV (como no Unity)


def unit(v):
    v = np.asarray(v, dtype=np.float64)
    return v / (np.linalg.norm(v, axis=-1, keepdims=True) + 1e-12)


class Malha:
    def __init__(self):
        self.v, self.n, self.uv, self.f, self.m = [], [], [], [], []

    def add(self, v, n, uv):
        self.v.append(np.asarray(v, dtype=np.float64))
        self.n.append(np.asarray(n, dtype=np.float64))
        self.uv.append((float(uv[0]), float(uv[1])))
        return len(self.v) - 1

    def quad(self, a, b, c, d, mat):
        self.f.append((a, b, c, d))
        self.m.append(mat)

    def criar(self, nome, malha_unity, caminho, materiais, pesos):
        obj = bc.novo_objeto(nome, self.v, self.f, "Elmo", malha_unity, caminho)
        me = obj.data
        for mn in materiais:
            me.materials.append(bpy.data.materials[mn])
        me.polygons.foreach_set("material_index", np.array(self.m, dtype=np.int32))
        cantos = np.empty(len(me.loops), dtype=np.int32)
        me.loops.foreach_get("vertex_index", cantos)
        uv = me.attributes.new("UVMetro", 'FLOAT2', 'CORNER')
        uv.data.foreach_set("vector", np.array(self.uv, dtype=np.float32)[cantos].ravel())
        me.shade_smooth()
        me.update()
        me.normals_split_custom_set(unit(np.array(self.n))[cantos].tolist())
        me.update()
        bc.pesos_por_osso(obj, pesos(np.array(self.v)))
        return obj


def meridiano(bvh, n=241):
    """Pontos do casco no plano de simetria, com tangente (para trás) e normal (para fora)."""
    ms = np.linspace(M0 - 3, M1 + 3, n)
    pts = []
    for m in ms:
        a = math.radians(m)
        d = Vector((0.0, -math.cos(a), math.sin(a)))
        hit = bvh.ray_cast(Vector(CENTRO.tolist()) + d * 0.6, -d)
        pts.append(np.array(hit[0]) if hit[0] is not None else CENTRO + np.array(d) * 0.13)
    pts = np.array(pts)
    pts[:, 0] = 0.0
    for _ in range(6):
        pts[1:-1] = (pts[:-2] + 2 * pts[1:-1] + pts[2:]) / 4
    # reamostra por comprimento de arco
    seg = np.linalg.norm(np.diff(pts, axis=0), axis=1)
    arc = np.concatenate([[0], np.cumsum(seg)])
    return ms, pts, arc


def estacoes(ms, pts, arc, passo):
    a0 = np.interp(M0, ms, arc)
    a1 = np.interp(M1, ms, arc)
    k = max(2, int(round((a1 - a0) / passo)) + 1)
    aa = np.linspace(a0, a1, k)
    p = np.stack([np.interp(aa, arc, pts[:, i]) for i in range(3)], -1)
    t = unit(np.gradient(p, axis=0))
    nr = np.stack([np.zeros(k), -t[:, 2], t[:, 1]], -1)
    nr *= np.sign(np.einsum('ij,ij->i', nr, p - CENTRO))[:, None]
    s = (aa - a0) / (a1 - a0)
    return p, t, nr, s, aa - a0


def suporte(M, ms, pts, arc):
    """Régua de bronze onde a crina é presa, com cravos de ouro dos lados."""
    p, t, nr, s, aa = estacoes(ms, pts, arc, 0.006)
    w, h = 0.0085, 0.024
    perfil = [(-1.0, -0.2), (-1.0, 0.45), (-0.82, 0.8), (-0.5, 1.0), (0.5, 1.0), (0.82, 0.8), (1.0, 0.45), (1.0, -0.2)]
    pn = [(-1, 0), (-1, 0.15), (-0.7, 0.7), (-0.2, 1), (0.2, 1), (0.7, 0.7), (1, 0.15), (1, 0)]
    pu = np.concatenate([[0], np.cumsum([math.hypot((perfil[i + 1][0] - perfil[i][0]) * w, (perfil[i + 1][1] - perfil[i][1]) * h) for i in range(len(perfil) - 1)])])
    aneis = []
    comp = aa[-1]
    for k in range(len(p)):
        e = min(1.0, 0.45 + 0.55 * min(aa[k], comp - aa[k]) / 0.014)      # pontas mais baixas
        anel = []
        for (px, ph), (nx, nh), u in zip(perfil, pn, pu):
            pos = p[k] + X * (px * w) + nr[k] * (ph * h * e)
            anel.append(M.add(pos, X * nx + nr[k] * nh, (u / UV_BRONZE, aa[k] / UV_BRONZE)))
        aneis.append(anel)
    for k in range(len(aneis) - 1):
        for j in range(len(perfil) - 1):
            M.quad(aneis[k][j], aneis[k][j + 1], aneis[k + 1][j + 1], aneis[k + 1][j], BRONZE)
    # tampas das pontas
    for k, sinal in ((0, -1.0), (len(aneis) - 1, 1.0)):
        ids = [M.add(M.v[i], t[k] * sinal, M.uv[i]) for i in aneis[k]]
        M.f.append(tuple(ids if sinal > 0 else ids[::-1]))
        M.m.append(BRONZE)
    # cravos de ouro: meia esfera achatada, de 2,6 em 2,6 cm, nos dois lados
    for a in np.arange(0.016, comp - 0.012, 0.026):
        c = np.array([np.interp(a, aa, p[:, i]) for i in range(3)])
        nn = unit(np.array([np.interp(a, aa, nr[:, i]) for i in range(3)]))
        tt = unit(np.array([np.interp(a, aa, t[:, i]) for i in range(3)]))
        for sx in (-1.0, 1.0):
            base = c + X * (sx * w) + nn * (h * 0.36)
            r, alt, nseg, nan = 0.0036, 0.0026, 10, 4
            an = []
            for q in range(nan + 1):
                th = q / nan * math.pi / 2
                anel = []
                for g in range(nseg):
                    ph = g / nseg * 2 * math.pi
                    d = tt * math.cos(ph) + nn * math.sin(ph)
                    pos = base + d * (r * math.cos(th)) + X * (sx * alt * math.sin(th))
                    anel.append(M.add(pos, d * math.cos(th) + X * (sx * math.sin(th)), (pos[1] / UV_OURO, pos[2] / UV_OURO)))
                an.append(anel)
            for q in range(nan):
                for g in range(nseg):
                    g2 = (g + 1) % nseg
                    M.quad(an[q][g], an[q][g2], an[q + 1][g2], an[q + 1][g], OURO)
    return p, t, nr, s, aa


def cartao(M, base, d0, eixo, comp, larg, curva, nucleo, nseg=6):
    """Uma mecha: tira de nseg quadriláteros que sai de 'base' na direção d0 e encurva; normais abertas em leque."""
    var = int(rng.integers(0, 4))
    u0, u1 = var / 4 + 0.012, (var + 1) / 4 - 0.012
    if rng.random() < 0.5:
        u0, u1 = u1, u0
    p = np.array(base, dtype=np.float64)
    d = unit(d0)
    seg = comp / nseg
    ids = []
    for k in range(nseg + 1):
        tt = k / nseg
        w = larg * (0.72 + 0.28 * min(1.0, tt * 2.5))
        ex = unit(eixo - d * np.dot(eixo, d))
        fora = unit(p - nucleo)
        fn = np.cross(ex, d)
        if np.dot(fn, fora) < 0:
            fn = -fn
        n = unit(0.35 * fn + 0.65 * fora)
        a = M.add(p - ex * w / 2, unit(n - ex * 0.32), (u0, tt))
        b = M.add(p + ex * w / 2, unit(n + ex * 0.32), (u1, tt))
        ids.append((a, b))
        d = unit(d + curva * ((0.25 + 1.5 * tt) / nseg))
        p = p + d * seg
    for k in range(nseg):
        M.quad(ids[k][0], ids[k][1], ids[k + 1][1], ids[k + 1][0], CRINA)


def altura(s):
    return 0.10 + 0.058 * math.sin(math.pi * s ** 0.7) ** 0.9 + 0.022 * s + 0.035 * s ** 3


def crina(M, ms, pts, arc):
    p, t, nr, s, aa = estacoes(ms, pts, arc, 0.0052)
    for i in range(len(p)):
        beta = math.radians(4.0 + 30.0 * s[i] ** 1.25 + 16.0 * s[i] ** 4)   # cada vez mais deitada para trás
        for j in (-2, -1, 0, 1, 2):
            if rng.random() < 0.05:
                continue
            base = p[i] + nr[i] * 0.011 + X * (j * 0.0046 + rng.normal(0, 0.0009)) + t[i] * rng.normal(0, 0.002)
            gama = math.radians(j * 7.0 + rng.normal(0, 3.0))
            d = unit(nr[i] * math.cos(beta) + t[i] * math.sin(beta) + X * math.tan(gama))
            comp = altura(s[i]) * rng.uniform(0.9, 1.07)
            yaw = math.radians(rng.normal(0, 18.0))
            eixo = t[i] if rng.random() < 0.55 else X
            eixo = eixo * math.cos(yaw) + np.cross(d, eixo) * math.sin(yaw)
            curva = t[i] * 0.55 + X * (j * 0.05) + np.array([0, 0, -0.14])
            cartao(M, base, d, eixo, comp, rng.uniform(0.027, 0.036), curva, p[i] - nr[i] * 0.035)
    return p[-4] + nr[-4] * 0.014, t[-4], nr[-4]


def caminho_rabo(inicio, tg, bvh, z_fim, folga=0.03, n=26):
    """Curva do rabo: sai da ponta de trás da crista, abre para trás e cai por fora do que estiver nas costas."""
    z = np.linspace(inicio[2], z_fim, n)
    tau = np.linspace(0, 1, n)
    y_livre = inicio[1] + 0.075 * (1 - np.exp(-tau * 9.0)) - 0.32 * tau ** 1.5
    y = y_livre.copy()
    y_obst = np.full(n, -1.0)
    for k in range(1, n):
        melhor = -1.0
        for dx in (-0.018, 0.0, 0.018):
            hit = bvh.ray_cast(Vector((dx, 1.2, z[k])), Vector((0, -1, 0)))
            if hit[0] is not None:
                melhor = max(melhor, hit[0].y)
        y_obst[k] = melhor
        y[k] = max(y[k], melhor + folga)
    for _ in range(5):
        y[1:-1] = (y[:-2] + 2 * y[1:-1] + y[2:]) / 4
        y[1:] = np.maximum(y[1:], y_obst[1:] + folga * 0.8)
    return np.stack([np.zeros(n), y, z], -1)


def rabo(nome, malha_unity, caminho, inicio, tg, bvh, z_fim, osso_baixo):
    M = Malha()
    path = caminho_rabo(inicio, tg, bvh, z_fim)
    seg = np.linalg.norm(np.diff(path, axis=0), axis=1)
    arc = np.concatenate([[0], np.cumsum(seg)])
    total = arc[-1]

    def ponto(a):
        return np.array([np.interp(a, arc, path[:, i]) for i in range(3)])
    for c in range(56):
        phi = rng.uniform(0, 2 * math.pi)
        rho = math.sqrt(rng.random())
        fim = total * rng.uniform(0.7, 1.0)
        nseg = 13
        var = int(rng.integers(0, 4))
        u0, u1 = var / 4 + 0.012, (var + 1) / 4 - 0.012
        if rng.random() < 0.5:
            u0, u1 = u1, u0
        larg = rng.uniform(0.026, 0.036)
        yaw = rng.normal(0, 0.5)
        ids = []
        for k in range(nseg + 1):
            tt = k / nseg
            a = fim * tt
            c0 = ponto(a)
            tp = unit(ponto(min(a + 0.01, total)) - ponto(max(a - 0.01, 0.0)))
            lado = unit(np.cross(tp, np.cross(X, tp)))           # X sem a parte ao longo do caminho
            tras = np.cross(tp, lado)
            if tras[1] < 0:
                tras = -tras
            r = (0.03 * (1 - 0.66 * a / total) + 0.005) * rho
            # perto do corpo o feixe achata: só abre para os lados e para trás
            rad = lado * math.cos(phi) + tras * abs(math.sin(phi)) * 0.8
            pos = c0 + rad * r
            ex = np.cross(tp, rad)
            ex = unit(ex * math.cos(yaw) + rad * math.sin(yaw))
            w = larg * (1 - 0.35 * tt)
            fora = unit(rad + tras * 0.5)
            ids.append((M.add(pos - ex * w / 2, unit(fora - ex * 0.3), (u0, tt)), M.add(pos + ex * w / 2, unit(fora + ex * 0.3), (u1, tt))))
        for k in range(nseg):
            M.quad(ids[k][0], ids[k][1], ids[k + 1][1], ids[k + 1][0], 0)
    z0 = inicio[2] - 0.03

    def pesos(v):
        k = np.clip((z0 - v[:, 2]) / 0.22, 0, 1)
        k = k * k * (3 - 2 * k)
        return {"head": 1 - k, osso_baixo: k}
    return M.criar(nome, malha_unity, caminho, ["M_Pat_Crista"], pesos)


def main():
    tex = os.path.join(bc.TEXTURAS, "Blender")
    bc.material_tex("M_Pat_Crista", "#B01C22", os.path.join(tex, "crina.png"), os.path.join(tex, "crina_normal.png"), alfa=True, rug=0.55)
    for n in ("Crista", "Rabo_de_Crina_Costas", "Rabo_de_Crina_Sobre_Escudo"):
        bc.aposentar(n)
    bvh = bc.bvh_de(["Elmo_Casco"])
    ms, pts, arc = meridiano(bvh)
    M = Malha()
    suporte(M, ms, pts, arc)
    fim, tg, nr = crina(M, ms, pts, arc)
    crista = M.criar("Crista", "Pat_Crista", "Armadura/Elmo/Crista", ["M_Pat_Bronze", "M_Pat_Ouro", "M_Pat_Crista"],
                     lambda v: {"head": np.ones(len(v))})
    print("[crina] crista: %d vértices, %d faces" % (len(M.v), len(M.f)))
    costas = ["Corpo", "Corselete", "Epomides", "Elmo_Casco", "Capa"]
    r1 = rabo("Rabo_de_Crina_Costas", "Pat_Rabo_de_Crina_Costas", "Armadura/Elmo/Crista/Rabo_de_Crina_Costas",
              fim, tg, bc.bvh_de(costas), 1.16, "spine01")
    r2 = rabo("Rabo_de_Crina_Sobre_Escudo", "Pat_Rabo_de_Crina_Sobre_Escudo", "Armadura/Elmo/Crista/Rabo_de_Crina_Sobre_Escudo",
              fim, tg, bc.bvh_de(costas + ["Escudo_Costas_Aspis"]), 1.22, "spine02")
    r2.hide_viewport = r2.hide_render = True
    print("[crina] rabos: %d e %d vértices" % (len(r1.data.vertices), len(r2.data.vertices)))
    bc.salvar(bpy.data.filepath)


main()
