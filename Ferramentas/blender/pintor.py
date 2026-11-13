"""Pinta as texturas exclusivas das peças de bronze e de linho do Pátroclo (atlas abertos por bl_uv.py).

Uso: python3 -I pintor.py <pacote sem extensão> <pasta_saida> [tamanho] [conjuntos]
  -> bronze_p_albedo.jpg / bronze_p_normal.jpg / bronze_p_mascara.png   (material M_Pat_BronzePintado)
     linho_p_albedo.jpg  / linho_p_normal.jpg  / linho_p_mascara.png    (material M_Pat_LinhoPintado)

Lê as malhas do pacote que o Blender exporta para o Unity, desenha os triângulos de cada conjunto no atlas e
pinta ponto a ponto, sabendo a posição 3D, a oclusão assada, a curvatura e a distância à beira da peça:
  bronze: pátina escura nas frestas e gravações, metal vivo nas quinas, friso gravado com pontos em toda beira,
          sobrancelhas e palmeta no elmo, volutas nas cnêmides, onda e anéis no escudo, raios no disco, mossas e cortes;
  linho:  estrela de dezesseis pontas em volta do disco do peito, dente-de-lobo na cintura, filete nas beiras, manchas.
O grão fino (martelado, trama) vem de um mapa de detalhe repetido no material do Unity.
"""
import os
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
sys.path.insert(0, os.path.join(AQUI, "..", "makehuman"))

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage, sparse
from scipy.sparse.csgraph import dijkstra

import troca
from mh_raster import _raster

PACOTE, OUT = sys.argv[1], sys.argv[2]
S = int(sys.argv[3]) if len(sys.argv) > 3 else 4096
QUAIS = sys.argv[4].split(",") if len(sys.argv) > 4 else ["bronze", "linho"]
pk = troca.ler(PACOTE)
ELMO_C = np.array([0.0, 1.74, 0.05])
rng = np.random.default_rng(21)


def ss(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def pulso(x, centro, meia):
    """1 no centro, 0 a partir de 'meia' de distância (borda suave)."""
    return ss(meia, meia * 0.45, np.abs(x - centro))


def ruido3(P, freq, seed):
    q = P.astype(np.float64) * freq
    i = np.floor(q).astype(np.int64)
    f = (q - i).astype(np.float32)
    f = f * f * (3 - 2 * f)

    def h(dx, dy, dz):
        n = (i[:, 0] + dx) * 374761393 + (i[:, 1] + dy) * 668265263 + (i[:, 2] + dz) * 2147483647 + seed * 1442695041
        n = (n ^ (n >> 13)) * 1274126177
        n = n ^ (n >> 16)
        return ((n & 0xFFFFFF) / np.float32(0xFFFFFF)).astype(np.float32)
    x0 = h(0, 0, 0) * (1 - f[:, 0]) + h(1, 0, 0) * f[:, 0]
    x1 = h(0, 1, 0) * (1 - f[:, 0]) + h(1, 1, 0) * f[:, 0]
    x2 = h(0, 0, 1) * (1 - f[:, 0]) + h(1, 0, 1) * f[:, 0]
    x3 = h(0, 1, 1) * (1 - f[:, 0]) + h(1, 1, 1) * f[:, 0]
    return ((x0 * (1 - f[:, 1]) + x1 * f[:, 1]) * (1 - f[:, 2]) + (x2 * (1 - f[:, 1]) + x3 * f[:, 1]) * f[:, 2]) * 2 - 1


def fbm3(P, freq, seed, oitavas=3):
    s, a, tot = 0.0, 1.0, 0.0
    for k in range(oitavas):
        s = s + a * ruido3(P, freq * 2 ** k, seed + 31 * k)
        tot += a
        a *= 0.5
    return s / tot


# ----------------------------------------------------------------------------------------- malhas do conjunto
def juntar(material):
    P, N, UV, UM, AO, PID, TRI, nomes = [], [], [], [], [], [], [], []
    base = 0
    for m in pk["malhas"]:
        for s in m["subs"]:
            if s["material"] != material or len(s["tri"]) == 0:
                continue
            usados = np.unique(s["tri"])
            remap = np.full(m["nv"], -1, dtype=np.int64)
            remap[usados] = np.arange(len(usados))
            P.append(m["pos"][usados].astype(np.float64))
            N.append(m["nor"][usados].astype(np.float64))
            UV.append(m["uv0"][usados].astype(np.float64))
            UM.append(m["uv1"][usados].astype(np.float64))
            AO.append(m["cor"][usados, 0].astype(np.float32) / 255.0 if m["cor"] is not None else np.ones(len(usados), np.float32))
            PID.append(np.full(len(usados), len(nomes)))
            TRI.append(remap[s["tri"]] + base)
            nomes.append(m["nome"])
            base += len(usados)
    G = dict(P=np.concatenate(P), N=np.concatenate(N), uv=np.concatenate(UV), um=np.concatenate(UM), ao=np.concatenate(AO),
             pid=np.concatenate(PID), tri=np.concatenate(TRI), nomes=nomes)
    print("  %s: %d peças, %d vértices, %d triângulos" % (material, len(nomes), len(G["P"]), len(G["tri"])))
    return G


def campos_da_malha(G, periodo):
    """Por vértice: curvatura (+ = convexo), distância à beira da peça e fase ao longo da beira (para o pontilhado)."""
    P, tri = G["P"], G["tri"]
    n = len(P)
    # solda por posição dentro de cada peça (o pacote repete vértices nas costuras de UV)
    key = np.concatenate([np.round(P * 2e5).astype(np.int64), G["pid"][:, None]], 1)
    _, first, inv = np.unique(key, axis=0, return_index=True, return_inverse=True)
    inv = inv.ravel()
    Pw = P[first]
    nw = len(Pw)
    tw = inv[tri]
    # normais soldadas (ponderadas por área)
    fn = np.cross(Pw[tw[:, 1]] - Pw[tw[:, 0]], Pw[tw[:, 2]] - Pw[tw[:, 0]])
    Nw = np.zeros((nw, 3))
    for k in range(3):
        np.add.at(Nw, tw[:, k], fn)
    # a orientação certa é a das normais do pacote (a ordem dos triângulos vem do Unity)
    Nref = np.zeros((nw, 3))
    np.add.at(Nref, inv, G["N"])
    Nw *= np.sign(np.einsum('ij,ij->i', Nw, Nref) + 1e-12)[:, None]
    Nw /= np.linalg.norm(Nw, axis=1, keepdims=True) + 1e-20
    e = np.concatenate([tw[:, [0, 1]], tw[:, [1, 2]], tw[:, [2, 0]]])
    es = np.sort(e, axis=1)
    eu, cont = np.unique(es, axis=0, return_counts=True)
    a, b = eu[:, 0], eu[:, 1]
    dp = Pw[b] - Pw[a]
    ln = np.linalg.norm(dp, axis=1) + 1e-12
    kk = np.einsum('ij,ij->i', Nw[b] - Nw[a], dp) / (ln * ln)
    curv = np.zeros(nw)
    cnt = np.zeros(nw)
    np.add.at(curv, a, kk)
    np.add.at(curv, b, kk)
    np.add.at(cnt, a, 1)
    np.add.at(cnt, b, 1)
    curv /= np.maximum(cnt, 1)
    graf = sparse.coo_matrix((np.concatenate([ln, ln]), (np.concatenate([a, b]), np.concatenate([b, a]))), shape=(nw, nw)).tocsr()
    # suaviza a curvatura pela malha
    adj = sparse.coo_matrix((np.ones(len(a) * 2), (np.concatenate([a, b]), np.concatenate([b, a]))), shape=(nw, nw)).tocsr()
    grau = np.asarray(adj.sum(1)).ravel() + 1e-9
    for _ in range(3):
        curv = 0.5 * curv + 0.5 * (adj @ curv) / grau
    # beira: arestas com um triângulo só; encadeia em laços para medir o comprimento ao longo dela
    be = eu[cont == 1]
    fase_s = np.zeros(nw)
    laco_len = np.ones(nw)
    viz = {}
    for x, y in be.tolist():
        viz.setdefault(x, []).append(y)
        viz.setdefault(y, []).append(x)
    visto = set()
    for ini in list(viz.keys()):
        if ini in visto:
            continue
        cad = [ini]
        visto.add(ini)
        cur = ini
        while True:
            prox = [v for v in viz[cur] if v not in visto]
            if not prox:
                break
            cur = prox[0]
            visto.add(cur)
            cad.append(cur)
        cad = np.array(cad)
        seg = np.linalg.norm(Pw[np.roll(cad, -1)] - Pw[cad], axis=1)
        s = np.concatenate([[0], np.cumsum(seg[:-1])])
        fase_s[cad] = s
        laco_len[cad] = max(seg.sum(), 1e-6)
    fontes = np.unique(be)
    if len(fontes):
        dist, _, src = dijkstra(graf, directed=False, indices=fontes, min_only=True, return_predecessors=True)
        dist[~np.isfinite(dist)] = 1.0
        src = np.where(src < 0, 0, src)
        s_v = fase_s[src]
        L_v = laco_len[src]
    else:
        dist = np.ones(nw)
        s_v = np.zeros(nw)
        L_v = np.ones(nw)
    per = L_v / np.maximum(np.round(L_v / periodo), 1)        # período ajustado para fechar o laço
    ang = 2 * np.pi * s_v / per
    return curv[inv].astype(np.float32), dist[inv].astype(np.float32), np.cos(ang)[inv].astype(np.float32), np.sin(ang)[inv].astype(np.float32), per[inv].astype(np.float32)


class Atlas:
    """Rasterização do conjunto: para cada ponto pintado, o triângulo e os pesos; interpola qualquer atributo de vértice."""

    def __init__(self, G):
        self.G = G
        uv = G["uv"]
        tid, bary, _ = _raster(uv[:, 0] * S, (1 - uv[:, 1]) * S, np.zeros(len(uv)), G["tri"], S, S)
        self.pix = np.nonzero(tid >= 0)[0]
        self.t = G["tri"][tid[self.pix]]
        self.b = bary[self.pix].astype(np.float32)
        self.n = len(self.pix)
        self.mask = (tid >= 0).reshape(S, S)
        self.pid = G["pid"][self.t[:, 0]]
        # metros por pixel de cada peça (o atlas dá a cada peça uma escala própria)
        P, tri = G["P"], G["tri"]
        a3 = 0.5 * np.linalg.norm(np.cross(P[tri[:, 1]] - P[tri[:, 0]], P[tri[:, 2]] - P[tri[:, 0]]), axis=1)
        a, b, c = uv[tri[:, 0]], uv[tri[:, 1]], uv[tri[:, 2]]
        auv = 0.5 * np.abs((b[:, 0] - a[:, 0]) * (c[:, 1] - a[:, 1]) - (c[:, 0] - a[:, 0]) * (b[:, 1] - a[:, 1]))
        tp = G["pid"][tri[:, 0]]
        self.mpp_peca = np.array([np.sqrt(a3[tp == k].sum() / max(auv[tp == k].sum(), 1e-12)) / S for k in range(len(G["nomes"]))])
        self.mpp = self.mpp_peca[self.pid].astype(np.float32)
        print("  atlas: %.0f%% pintado; mm por pixel: %s" % (100 * self.n / (S * S), ", ".join("%s %.2f" % (nm.replace("Pat_", ""), 1000 * v) for nm, v in zip(G["nomes"], self.mpp_peca))))

    def v(self, a):
        a = np.asarray(a, dtype=np.float32)
        if a.ndim == 1:
            return a[self.t[:, 0]] * self.b[:, 0] + a[self.t[:, 1]] * self.b[:, 1] + a[self.t[:, 2]] * self.b[:, 2]
        return a[self.t[:, 0]] * self.b[:, :1] + a[self.t[:, 1]] * self.b[:, 1:2] + a[self.t[:, 2]] * self.b[:, 2:3]

    def img(self, vals, fundo=0.0):
        k = 1 if vals.ndim == 1 else vals.shape[1]
        out = np.full((S * S, k), fundo, dtype=np.float32)
        out[self.pix] = vals.reshape(self.n, k)
        return out.reshape(S, S, k) if k > 1 else out.reshape(S, S)

    def estender(self, im, px=None):
        """Copia a cor da beira das ilhas para fora (evita costura escura nos mipmaps)."""
        _, (iy, ix) = ndimage.distance_transform_edt(~self.mask, return_indices=True)
        return im[iy, ix]

    def riscos(self, quantos, comp_m, larg_m, pecas=None, seed=0):
        """Imagem com riscos retos (cortes, arranhões) em posições aleatórias das ilhas. comp e larg em metros."""
        r = np.random.default_rng(seed)
        im = Image.new("F", (S, S), 0.0)
        dr = ImageDraw.Draw(im)
        ok = np.ones(self.n, bool) if pecas is None else np.isin(self.pid, pecas)
        cand = np.nonzero(ok)[0]
        if len(cand) == 0:
            return np.zeros((S, S), np.float32)
        for i in r.choice(cand, size=quantos):
            p = self.pix[i]
            y, x = divmod(int(p), S)
            mpp = float(self.mpp[i])
            L = r.uniform(*comp_m) / mpp
            w = max(1.0, r.uniform(*larg_m) / mpp)
            a = r.uniform(0, np.pi)
            dx, dy = np.cos(a) * L / 2, np.sin(a) * L / 2
            dr.line([x - dx, y - dy, x + dx, y + dy], fill=float(r.uniform(0.5, 1.0)), width=int(round(w)))
        return np.asarray(im, dtype=np.float32) * self.mask

    def mossas(self, quantas, raio_m, pecas=None, seed=0):
        r = np.random.default_rng(seed)
        out = np.zeros((S, S), np.float32)
        ok = np.ones(self.n, bool) if pecas is None else np.isin(self.pid, pecas)
        cand = np.nonzero(ok)[0]
        for i in r.choice(cand, size=quantas):
            y, x = divmod(int(self.pix[i]), S)
            rad = r.uniform(*raio_m) / float(self.mpp[i])
            k = int(rad * 2.2) + 1
            yy, xx = np.mgrid[-k:k + 1, -k:k + 1]
            g = np.exp(-(xx * xx + yy * yy) / (2 * (rad * 0.6) ** 2)).astype(np.float32) * r.uniform(0.5, 1.0)
            y0, y1, x0, x1 = max(0, y - k), min(S, y + k + 1), max(0, x - k), min(S, x + k + 1)
            out[y0:y1, x0:x1] = np.maximum(out[y0:y1, x0:x1], g[y0 - (y - k):y1 - (y - k), x0 - (x - k):x1 - (x - k)])
        return out * self.mask

    def normal(self, alt_img):
        """Mapa de normal (espaço tangente do atlas) a partir da altura em metros."""
        h = self.estender(alt_img)
        mpp = self.estender(self.img(self.mpp, 1e-3))
        gy, gx = np.gradient(h)
        nx, ny = -gx / mpp, gy / mpp
        ln = np.sqrt(nx * nx + ny * ny + 1.0)
        return np.stack([nx / ln, ny / ln, 1.0 / ln], -1)

    def salvar(self, nome, albedo, alt, metal, liso):
        alb = self.estender(np.clip(albedo, 0, 1))
        Image.fromarray((alb * 255 + 0.5).astype(np.uint8), "RGB").save(f"{OUT}/{nome}_albedo.jpg", quality=92, subsampling=0)
        nrm = self.normal(alt)
        Image.fromarray(((nrm * 0.5 + 0.5) * 255 + 0.5).astype(np.uint8), "RGB").save(f"{OUT}/{nome}_normal.jpg", quality=93, subsampling=0)
        msk = np.stack([self.estender(np.clip(metal, 0, 1)), np.ones((S, S), np.float32), np.zeros((S, S), np.float32),
                        self.estender(np.clip(liso, 0, 1))], -1)
        Image.fromarray((msk * 255 + 0.5).astype(np.uint8), "RGBA").resize((S // 2, S // 2), Image.LANCZOS).save(f"{OUT}/{nome}_mascara.png", optimize=True)
        for suf in ("_albedo.jpg", "_normal.jpg", "_mascara.png"):
            print("  %s%s %.1f MB" % (nome, suf, os.path.getsize(f"{OUT}/{nome}{suf}") / 1e6))


def espiral(x, y, r0, voltas, larg):
    """Linha em espiral (voluta) centrada na origem: 1 em cima do traço."""
    r = np.hypot(x, y)
    a = np.arctan2(y, x)
    passo = r0 / voltas
    k = np.round((r - (a / (2 * np.pi)) * passo) / passo)
    rr = (k + a / (2 * np.pi)) * passo
    return pulso(r, rr, larg) * (r < r0 + larg) * (r > passo * 0.3)


# ----------------------------------------------------------------------------------------- bronze
def bronze():
    print("bronze")
    G = juntar("M_Pat_BronzePintado")
    curv, db, fc, fs_, per = campos_da_malha(G, 0.0095)
    A = Atlas(G)
    P = A.v(G["P"])
    ao = A.v(G["ao"])
    cv = A.v(curv)
    d = A.v(db)
    pid = A.pid
    nomes = G["nomes"]

    def peca(nome):
        return pid == nomes.index(nome) if nome in nomes else np.zeros(A.n, bool)
    elmo, cnem, escudo, disco = peca("Pat_Elmo_Casco"), peca("Pat_Cnemides"), peca("Pat_Aspis"), peca("Pat_Disco_Peito")
    grav = np.zeros(A.n, np.float32)      # gravado (sulco): 0..1
    rel = np.zeros(A.n, np.float32)       # relevo em metros (positivo = saliente)

    # friso em toda beira: dois filetes gravados e uma carreira de pontos puncionados
    filete = np.maximum(pulso(d, 0.0058, 0.0011), pulso(d, 0.0086, 0.0008))
    cosf, sinf, perf = A.v(fc), A.v(fs_), A.v(per)
    arco = np.abs(np.arctan2(sinf, cosf)) * perf / (2 * np.pi)
    pontos = ss(0.0021, 0.0012, np.hypot(d - 0.0138, arco))
    grande = ~(disco | peca("Pat_Cinturao"))
    grav = np.maximum(grav, np.where(disco, pulso(d, 0.004, 0.0008), filete))
    grav = np.maximum(grav, pontos * grande * 0.8)

    # elmo: sobrancelhas em relevo, palmeta gravada na testa, volutas nas faces
    if elmo.any():
        q = P[elmo] - ELMO_C.astype(np.float32)
        rr = np.linalg.norm(q, axis=1)
        lat = np.degrees(np.arcsin(np.clip(q[:, 1] / rr, -1, 1)))
        lon = np.degrees(np.arctan2(q[:, 0], q[:, 2]))
        R = 0.112
        g2m = R * np.pi / 180.0
        al = np.abs(lon)
        t = np.clip((al - 5.0) / 27.0, 0, 1)
        sob = 3.5 + 6.5 * np.sin(np.pi * t ** 0.8) * (1 - 0.35 * t)
        dist = np.abs(lat - sob) * g2m
        meia = 0.0042 * (1 - 0.55 * t) * (al > 5.0) * (al < 32.0)
        r_e = np.where(meia > 0, np.clip(1 - (dist / np.maximum(meia, 1e-6)) ** 2, 0, 1), 0) * 0.0011
        g_e = pulso(dist, 0.0, 0.0006) * (meia > 0) * ss(32.0, 28.0, al) * ss(5.0, 8.0, al)
        # palmeta: leque de pétalas gravadas acima do nasal
        x = lon * g2m * np.cos(np.radians(lat))
        y = (lat - 20.0) * g2m
        r = np.hypot(x, y)
        a = np.degrees(np.arctan2(x, y))
        rmax = 0.036 * (0.62 + 0.38 * np.cos(np.radians(np.clip(a, -90, 90))))
        dentro = (np.abs(a) < 78) & (y > -0.004)
        raio = pulso(((a + 78) / 22.3) % 1.0, 0.5, 0.11) * (r > 0.0085) * (r < rmax)
        arcos = np.maximum(pulso(r, 0.0085, 0.0007), pulso(r, rmax, 0.0008))
        g_p = np.maximum(raio, arcos) * dentro * (r < rmax + 0.001)
        g_p = np.maximum(g_p, ss(0.0048, 0.0036, r) * 0.9)
        # volutas nas faces
        g_v = np.zeros_like(lat)
        for sx in (-1.0, 1.0):
            vx = (lon - sx * 38.0) * g2m * np.cos(np.radians(lat)) * sx
            vy = (lat + 37.0) * g2m
            g_v = np.maximum(g_v, espiral(vx, vy, 0.015, 2.5, 0.0007))
        # dois filetes em volta da copa
        g_c = np.maximum(pulso(lat, 63.0, 0.5), pulso(lat, 65.2, 0.4)) * (np.abs(lon) > 14)
        grav[elmo] = np.maximum.reduce([grav[elmo], g_e, g_p, g_v, g_c])
        rel[elmo] += r_e.astype(np.float32)

    # cnêmides: par de volutas abaixo do joelho e filete acompanhando a canela
    if cnem.any():
        q = P[cnem]
        for sx in (-1.0, 1.0):
            lado = q[:, 0] * sx > 0
            if not lado.any():
                continue
            ql = q[lado]
            y0, y1 = ql[:, 1].min(), ql[:, 1].max()
            cx = np.median(ql[:, 0])
            frente = ql[:, 2] > np.percentile(ql[:, 2], 35)
            ya = y0 + 0.66 * (y1 - y0)
            g = np.zeros(len(ql), np.float32)
            for s2 in (-1.0, 1.0):
                g = np.maximum(g, espiral((ql[:, 0] - cx) * s2 - 0.017, ql[:, 1] - ya, 0.014, 2.3, 0.0007))
            # haste que desce das volutas e filete central
            haste = pulso(np.abs(ql[:, 0] - cx), 0.0, 0.0008) * (ql[:, 1] < ya - 0.012) * (ql[:, 1] > y0 + 0.07)
            g = np.maximum(g, haste) * frente
            idx = np.nonzero(cnem)[0][lado]
            grav[idx] = np.maximum(grav[idx], g)

    # escudo: anéis, faixa de onda corrida e raios curtos em volta do emblema
    if escudo.any():
        q = P[escudo]
        r = np.hypot(q[:, 0], q[:, 1])
        th = np.arctan2(q[:, 1], q[:, 0])
        g = np.maximum.reduce([pulso(r, 0.255, 0.0012), pulso(r, 0.262, 0.0009), pulso(r, 0.318, 0.0009), pulso(r, 0.372, 0.0012), pulso(r, 0.379, 0.0009)])
        # onda corrida (cão correndo): cristas em gancho entre os anéis
        n_ondas = 44
        ph = (th * n_ondas / (2 * np.pi)) % 1.0
        ry = (r - 0.325) / 0.04
        onda = pulso(ry, 0.5 + 0.34 * np.sin(2 * np.pi * ph), 0.085) * (ry > 0.02) * (ry < 0.98)
        gancho = espiral((ph - 0.5) * 0.055, (ry - 0.52) * 0.04, 0.011, 1.6, 0.0009)
        g = np.maximum(g, np.maximum(onda, gancho) * (ry > 0) * (ry < 1))
        # raios gravados saindo do meio
        raios = pulso((th * 24 / (2 * np.pi)) % 1.0, 0.5, 0.035 / np.maximum(r / 0.2, 0.3)) * (r > 0.205) * (r < 0.248)
        g = np.maximum(g, raios)
        grav[escudo] = np.maximum(grav[escudo], g.astype(np.float32))

    # disco do peito: raios e anel
    if disco.any():
        q = P[disco]
        c = q.mean(0)
        nrm = A.v(G["N"])[disco].mean(0)
        nrm /= np.linalg.norm(nrm)
        e1 = np.cross([0.0, 1.0, 0.0], nrm)
        e1 /= np.linalg.norm(e1)
        e2 = np.cross(nrm, e1)
        x, y = (q - c) @ e1, (q - c) @ e2
        r = np.hypot(x, y)
        th = np.arctan2(y, x)
        g = np.maximum(pulso(r, 0.061, 0.0009), pulso((th * 20 / (2 * np.pi)) % 1.0, 0.5, 0.05) * (r > 0.036) * (r < 0.058))
        grav[disco] = np.maximum(grav[disco], g.astype(np.float32))

    print("  gravações ok")
    # ruídos 3D (contínuos entre as ilhas do atlas)
    n1 = fbm3(P, 9.0, 1)
    n2 = fbm3(P, 38.0, 2)
    n3 = fbm3(P, 140.0, 3, 2)
    cav = np.clip(1.0 - ao, 0, 1)
    # mossas, cortes e arranhões (no espaço do atlas)
    mossa = A.mossas(46, (0.004, 0.011), seed=4)
    corte = ndimage.gaussian_filter(A.riscos(30, (0.02, 0.06), (0.0011, 0.0019), seed=5), 0.8)
    arranho = A.riscos(900, (0.006, 0.035), (0.0003, 0.0006), seed=6)
    mos, cor_, arr = mossa.ravel()[A.pix], corte.ravel()[A.pix], arranho.ravel()[A.pix]

    # metal vivo: quinas, relevos altos e áreas de atrito; pátina: frestas, sulcos e partes pouco tocadas
    vivo = np.clip(ss(4.0, 26.0, cv) * 0.9 + ss(0.0002, 0.0009, rel) * 0.8 + ss(0.25, 0.8, n1 * 0.6 + n2 * 0.4) * 0.55 - cav * 0.8, 0, 1)
    vivo = np.maximum(vivo, ss(0.0035, 0.0005, d) * 0.7)
    patina = np.clip(0.2 + 1.1 * cav ** 1.15 + 0.4 * ss(-0.1, -0.75, n1 * 0.65 + n2 * 0.35) + 0.25 * n3 * 0.5 - 0.7 * vivo, 0, 1)
    patina = np.maximum(patina, grav * 0.92)
    patina = np.maximum(patina, mos * 0.45)
    base = np.array([0.67, 0.445, 0.235], np.float32)
    claro = np.array([0.87, 0.67, 0.40], np.float32)
    escuro = np.array([0.185, 0.118, 0.07], np.float32)
    verde = np.array([0.24, 0.35, 0.29], np.float32)
    alb = base[None, :] * (1 + 0.06 * n2[:, None])
    alb = alb + (claro[None, :] - alb) * (vivo * 0.85)[:, None]
    alb = alb + (escuro[None, :] - alb) * (patina * 0.9)[:, None]
    azin = (ss(0.45, 0.85, cav) * ss(0.1, 0.6, n2) * 0.55 + grav * ss(0.2, 0.7, n1) * 0.35)
    alb = alb + (verde[None, :] - alb) * azin[:, None]
    alb = alb + (claro[None, :] * 1.05 - alb) * (cor_ * 0.9)[:, None]          # corte recente: metal novo
    alb = alb * (1 + 0.22 * arr[:, None])
    metal = np.clip(1.0 - 0.45 * patina - 0.5 * azin, 0, 1)
    liso = np.clip(0.6 + 0.2 * vivo - 0.36 * patina - 0.25 * azin + 0.1 * cor_ - 0.12 * arr + 0.04 * n3, 0.05, 0.95)
    alt = rel - 0.00042 * grav - 0.0011 * mos - 0.00045 * cor_ - 0.00006 * arr + 0.00005 * n3
    A.salvar("bronze_p", A.img(alb), A.img(alt), A.img(metal), A.img(liso, 0.5))


# ----------------------------------------------------------------------------------------- linho
def linho():
    print("linho")
    G = juntar("M_Pat_LinhoPintado")
    curv, db, fc, fs_, per = campos_da_malha(G, 0.012)
    A = Atlas(G)
    P = A.v(G["P"])
    ao = A.v(G["ao"])
    d = A.v(db)
    pid = A.pid
    nomes = G["nomes"]
    cors = pid == nomes.index("Pat_Corselete") if "Pat_Corselete" in nomes else np.zeros(A.n, bool)
    aba = ~cors
    x, y, z = P[:, 0], P[:, 1], P[:, 2]
    frente = z > 0.02
    n1 = fbm3(P, 7.0, 11)
    n2 = fbm3(P, 30.0, 12)
    n3 = fbm3(P, 150.0, 13, 2)
    tinta = np.zeros(A.n, np.float32)

    # estrela de dezesseis pontas em volta do disco do peito (o "corselete estrelado")
    cx, cy = 0.0, 1.34
    # distância ao longo da superfície do peito ~ distância no plano frontal, com a curvatura lateral compensada
    xx = np.arcsin(np.clip(x / 0.19, -1, 1)) * 0.19
    r = np.hypot(xx - cx, y - cy)
    a = np.arctan2(xx - cx, y - cy)
    k = a * 16 / (2 * np.pi)
    par = (np.round(k).astype(np.int64) % 2) == 0
    ponta = np.where(par, 0.142, 0.112)
    larg = np.clip((ponta - r) / (ponta - 0.074), 0, 1) * 0.5
    raio = ss(0.0, 0.035, larg - np.abs(k - np.round(k))) * (r > 0.0745) * (r < ponta)
    anel = np.maximum(pulso(r, 0.0775, 0.0016), pulso(r, 0.082, 0.0008))
    tinta = np.maximum(tinta, np.maximum(raio, anel) * cors * frente)

    # dente-de-lobo logo acima do cinturão, em volta do tronco
    th = np.arctan2(x, z - 0.02)
    arco = th * 0.17
    yb = y - 1.238
    dente_h = 0.03
    fase = (arco / 0.03) % 1.0
    dente = ss(0.0, 0.04, (1 - yb / dente_h) * 0.5 - np.abs(fase - 0.5)) * (yb > 0) * (yb < dente_h)
    faixa = np.maximum(pulso(yb, -0.004, 0.0016), pulso(yb, dente_h + 0.008, 0.0012))
    tinta = np.maximum(tinta, np.maximum(dente, faixa) * cors)

    # filete pintado acompanhando toda beira (costura com a barra carmesim)
    tinta = np.maximum(tinta, pulso(d, 0.0075, 0.0014))
    # abas dos ombros: estrela de oito pontas na ponta da frente
    if aba.any():
        for sx in (-1.0, 1.0):
            m = aba & (x * sx > 0) & frente
            if not m.any():
                continue
            ex, ey = x[m] - sx * 0.142, y[m] - 1.445
            rr = np.hypot(ex, ey)
            aa = np.arctan2(ex, ey) * 8 / (2 * np.pi)
            st = ss(0.0, 0.05, np.clip((0.026 - rr) / 0.02, 0, 1) * 0.5 - np.abs(aa - np.round(aa))) * (rr < 0.026)
            st = np.maximum(st, pulso(rr, 0.0, 0.004))
            tinta[m] = np.maximum(tinta[m], st.astype(np.float32))
    # tinta gasta: some em manchas e nas partes de atrito
    gasta = ss(0.15, 0.85, 0.6 * n2 + 0.5 * n3 + 0.25 * n1)
    tinta = tinta * (1 - 0.6 * gasta)
    print("  pintura ok")

    cav = np.clip(1.0 - ao, 0, 1)
    linho_c = np.array([0.815, 0.755, 0.615], np.float32)
    alb = linho_c[None, :] * (1 + 0.05 * n1 + 0.035 * n2 + 0.02 * n3)[:, None]
    # amarelado e suor: manchas largas; encardido nas frestas e perto das beiras
    mancha = ss(0.1, 0.7, n1) * 0.5 + ss(0.3, 0.9, fbm3(P, 3.0, 14)) * 0.5
    alb = alb + (np.array([0.66, 0.56, 0.39], np.float32)[None, :] - alb) * (0.45 * mancha)[:, None]
    sujo = np.clip(0.9 * cav ** 1.2 + 0.35 * ss(0.012, 0.0, d) + 0.25 * ss(1.3, 1.05, y) * ss(-0.2, 0.6, n2), 0, 1)
    alb = alb + (np.array([0.36, 0.29, 0.21], np.float32)[None, :] - alb) * (0.7 * sujo)[:, None]
    carmesim = np.array([0.50, 0.095, 0.105], np.float32) * (1 + 0.08 * n2[:, None])
    alb = alb + (carmesim - alb) * (tinta * 0.93)[:, None]
    liso = np.clip(0.2 + 0.03 * n2 - 0.08 * sujo + 0.06 * tinta, 0.03, 0.9)
    # rugas leves do linho colado em camadas e a espessura da tinta
    alt = 0.00035 * n1 + 0.00012 * n2 + 0.00004 * tinta
    A.salvar("linho_p", A.img(alb), A.img(alt), A.img(np.zeros(A.n, np.float32)), A.img(liso, 0.2))


if "bronze" in QUAIS:
    bronze()
if "linho" in QUAIS:
    linho()
