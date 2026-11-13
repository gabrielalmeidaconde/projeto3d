"""Detalhes anatômicos procedurais do Pátroclo (espaço do Unity, metros).

Cada detalhe é um campo de altura h(p) avaliado na superfície da pele:
- a versão "suave" (blur) vai para a geometria (mh_build.py), que só resolve formas largas;
- a diferença entre a versão nítida e a suave vira normal map (mh_skin.py).
Assim os sulcos finos (abdômen, cinturão ilíaco, veias, rugas de expressão) aparecem sem
aumentar a malha.
"""
import numpy as np


def ss(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def _bw(w, blur):
    """Largura efetiva e fator de amplitude de um perfil gaussiano borrado."""
    we = np.sqrt(w * w + blur * blur)
    return we, w / we


def ridge(dist, w, amp, blur=0.0):
    we, k = _bw(w, blur)
    return amp * k * np.exp(-(dist / we) ** 2)


def seg_dist(q, a, b):
    """Distância 2D de pontos q (n,2) ao segmento ab; também o parâmetro t."""
    ab = b - a
    t = np.clip(((q - a) @ ab) / max(ab @ ab, 1e-12), 0, 1)
    d = q - (a + t[:, None] * ab)
    return np.sqrt((d * d).sum(1)), t


def poly_dist(q, pts):
    """Distância 2D à polilinha e parâmetro acumulado normalizado (0..1)."""
    pts = np.asarray(pts, dtype=np.float64)
    seglen = np.linalg.norm(np.diff(pts, axis=0), axis=1)
    cum = np.concatenate([[0], np.cumsum(seglen)])
    best = np.full(len(q), np.inf)
    par = np.zeros(len(q))
    for i in range(len(pts) - 1):
        d, t = seg_dist(q, pts[i], pts[i + 1])
        m = d < best
        best[m] = d[m]
        par[m] = (cum[i] + t[m] * seglen[i]) / cum[-1]
    return best, par


def _hash_noise(x, seed=0.0):
    """Ruído suave 1D barato (soma de senos) para variar a intensidade ao longo das linhas."""
    return 0.5 + 0.25 * np.sin(x * 1.7 + seed) + 0.15 * np.sin(x * 4.1 + seed * 2.3) + 0.1 * np.sin(x * 9.3 + seed * 0.7)


class Landmarks:
    def __init__(self, bones, navel, xiphoid):
        self.b = {b["name"]: (np.asarray(b["head"]), np.asarray(b["tail"])) for b in bones}
        self.yN = navel[1]
        self.yX = xiphoid[1]
        self.s = (self.yX - self.yN) / 1.95
        h = self.b
        eye = h["eye.L"][1]
        self.eye = np.array([abs(eye[0]), eye[1], eye[2]])
        self.brow_y = h["oculi01.L"][1][1]
        self.mouth = np.array([abs(h["oris03.L"][1][0]) + 0.004, (h["oris03.L"][1][1] + h["oris07.L"][1][1]) * 0.5])
        self.mouth_y = (h["oris04.L"][0][1] + h["oris06.L"][0][1]) * 0.5
        self.nose_y = h["levator04.L"][0][1]
        self.chin_y = h["special04"][1][1] - 0.008
        self.face_z = eye[2]

    def bone(self, name):
        return self.b[name]


# ===================================================================== tronco
def torso_field(P, N, mask, L, blur=0.0):
    """Abdômen em gomos, linha alba, oblíquos, cinturão ilíaco e serrátil."""
    s = L.s
    x, y, z = P[:, 0], P[:, 1], P[:, 2]
    X = np.abs(x) / s
    front = ss(0.25, 0.6, N[:, 2]) * (z > 0.02)
    lat = ss(0.35, 0.7, np.abs(N[:, 0])) * (X > 0.9)
    h = np.zeros(len(P))
    b = blur / s
    # ---- reto abdominal (coordenadas normalizadas por s)
    region = np.zeros(len(P))
    rows = [(-1.72, -0.1, 0.5, 0.74, 0.024, 0.22), (0.06, 0.66, 0.84, 0.84, 0.046, 0.11),
            (0.74, 1.3, 0.86, 0.86, 0.046, 0.11), (1.38, 1.92, 0.8, 0.8, 0.038, 0.12)]
    Y = (y - L.yN) / s - 0.16 * X
    for r0, r1, xo0, xo1, amp, tw0 in rows:
        t = np.clip((Y - r0) / (r1 - r0), 0, 1)
        xo = xo0 + (xo1 - xo0) * t
        xi = 0.05
        xc = (xi + xo) * 0.5
        hw = (xo - xi) * 0.5
        yc = (r0 + r1) * 0.5
        hh = (r1 - r0) * 0.5
        u = (X - xc) / hw
        v = (Y - yc) / hh
        r = (np.abs(u) ** 3 + np.abs(v) ** 3) ** (1 / 3)
        edge = (1 - r) * np.minimum(hw, hh)  # ~distância até a borda do gomo (unidades s)
        tw = np.sqrt(tw0 ** 2 + b * b)
        dome = 0.6 + 0.4 * np.clip(1 - r * r, 0, 1)  # gomo abaulado, não chato
        h += amp * ss(-tw, tw, edge - 0.03) * dome
        region = np.maximum(region, ss(-0.3, 0.05, edge))
    h -= 0.022 * region
    # linha alba
    band = ss(-1.3, -0.6, (y - L.yN) / s) * (1 - ss(1.9, 2.15, (y - L.yN) / s))
    h += ridge(X, 0.045, -0.02, b) * band
    # oblíquo externo
    ob = ((X - 1.12) / np.sqrt(0.34 ** 2 + b * b)) ** 2 + (((y - L.yN) / s - 0.05) / np.sqrt(0.8 ** 2 + b * b)) ** 2
    h += 0.04 * np.exp(-ob * 1.6) * ss(0.0, 0.35, N[:, 2] + 0.2)
    # cinturão ilíaco (sulco + borda saliente acima)
    q = np.stack([X, (y - L.yN) / s], 1)
    A, B = np.array([1.06, -0.42]), np.array([0.36, -1.85])
    d, t = seg_dist(q, A, B)
    ab = B - A
    side = ((q - A) @ np.array([-ab[1], ab[0]])) > 0
    fade = ss(0.0, 0.12, t) * (1 - ss(0.85, 1.0, t))
    h += ridge(d, 0.085, -0.032, b) * fade
    h += ridge(d - 0.15, 0.1, 0.016, b) * side * fade
    # serrátil anterior
    for i in range(4):
        cz = 1.02 - 0.16 * i
        cy = 1.25 + 0.27 * i
        a = np.radians(38)
        Z = z / s - cz
        Yy = (y - L.yN) / s - cy
        du = Z * np.cos(a) - Yy * np.sin(a)
        dv = Z * np.sin(a) + Yy * np.cos(a)
        ru, rv = np.sqrt(0.2 ** 2 + b * b), np.sqrt(0.1 ** 2 + b * b)
        h += 0.012 * (0.2 * 0.1) / (ru * rv) * np.exp(-1.5 * ((du / ru) ** 2 + (dv / rv) ** 2)) * lat
    return h * s * mask * np.maximum(front, lat)


# ===================================================================== braços: veias
def _limb_frame(L, side):
    """Eixos do antebraço e do braço e direção dorsal (costas da mão) para um lado ('L'/'R')."""
    sh = L.bone(f"upperarm01.{side}")[0]
    el = L.bone(f"lowerarm01.{side}")[0]
    wr = L.bone(f"wrist.{side}")[0]
    thumb = L.bone(f"finger1-1.{side}")[0]
    pinky = L.bone(f"finger5-1.{side}")[0]
    mid = L.bone(f"finger3-1.{side}")[0]
    fingers = mid - wr
    fingers /= np.linalg.norm(fingers)
    ts = thumb - pinky
    ts -= fingers * (ts @ fingers)
    ts /= np.linalg.norm(ts)
    palm = np.cross(fingers, ts) if side == "R" else np.cross(ts, fingers)
    return sh, el, wr, ts, -palm


def _cyl(P, a, b, ref):
    """Coordenadas cilíndricas em torno do eixo a->b: (t 0..1, ângulo a partir de ref, raio)."""
    ax = b - a
    ln = np.linalg.norm(ax)
    ax = ax / ln
    rel = P - a
    t = rel @ ax / ln
    radial = rel - np.outer(rel @ ax, ax)
    r = np.linalg.norm(radial, axis=1)
    e1 = ref - ax * (ref @ ax)
    e1 /= np.linalg.norm(e1)
    e2 = np.cross(ax, e1)
    ang = np.arctan2(radial @ e2, radial @ e1)
    return t, ang, r, ln


def _vein_paths(rng, n, t0, t1, a0, a1, wig):
    pts = []
    tt = np.linspace(t0, t1, n)
    aa = np.linspace(a0, a1, n) + rng.normal(0, wig, n)
    for t, a in zip(tt, aa):
        pts.append((t, a))
    return np.array(pts)


def arm_field(P, N, mask_fore, mask_upper, L, side, blur=0.0):
    """Veias do antebraço, do bíceps e do dorso da mão (relevo de ~1 mm)."""
    sh, el, wr, ts, dors = _limb_frame(L, side)
    sgn = 1.0 if side == "R" else -1.0
    rng = np.random.default_rng(7 if side == "R" else 11)
    h = np.zeros(len(P))
    # antebraço: ângulos medidos a partir do lado do polegar (radial), girando para o dorso
    t, ang, r, ln = _cyl(P, el, wr, ts)
    ang = ang * np.sign((np.cross(wr - el, ts)) @ dors)  # ângulo positivo gira para o dorso
    veins = [
        # (t0, t1, ang0, ang1, wiggle, largura m, altura m)
        (0.05, 0.95, 0.25, 0.05, 0.08, 0.0026, 0.0013),    # cefálica (lado do polegar)
        (0.1, 0.9, -1.2, -0.9, 0.1, 0.0024, 0.0011),       # mediana (face ventral)
        (0.15, 0.85, -2.3, -2.6, 0.1, 0.0022, 0.0009),     # basílica (lado do mindinho)
        (0.35, 0.95, 0.9, 0.5, 0.1, 0.002, 0.0008),        # ramo dorsal
        (0.2, 0.6, -1.15, 0.2, 0.06, 0.0019, 0.0008),      # anastomose em diagonal
        (0.25, 0.97, 1.75, 1.35, 0.1, 0.0022, 0.0011),     # dorsal do lado do mindinho
        (0.5, 0.97, 1.5, 0.95, 0.08, 0.0019, 0.0008),      # ramo dorsal até o punho
    ]
    for t0, t1, a0, a1, wig, w, amp in veins:
        path = _vein_paths(rng, 7, t0, t1, a0, a1, wig)
        q = np.stack([t * ln, ang * 0.042], 1)
        pp = np.stack([path[:, 0] * ln, path[:, 1] * 0.042], 1)
        d, par = poly_dist(q, pp)
        fade = ss(0.0, 0.15, par) * (1 - ss(0.85, 1.0, par))
        h += ridge(d, w, amp, blur) * fade * (0.7 + 0.3 * _hash_noise(par * 12, t0 * 9))
    h *= mask_fore
    # braço: cefálica na borda lateral do bíceps e um ramo atravessando
    t, ang, r, ln = _cyl(P, sh, el, ts)
    ang = ang * np.sign((np.cross(el - sh, ts)) @ dors)
    hu = np.zeros(len(P))
    for t0, t1, a0, a1, wig, w, amp in [(0.45, 0.98, 0.15, 0.3, 0.08, 0.0032, 0.0008),
                                        (0.6, 0.95, -0.9, -0.3, 0.1, 0.0024, 0.0005)]:
        path = _vein_paths(rng, 6, t0, t1, a0, a1, wig)
        q = np.stack([t * ln, ang * 0.045], 1)
        pp = np.stack([path[:, 0] * ln, path[:, 1] * 0.045], 1)
        d, par = poly_dist(q, pp)
        fade = ss(0.0, 0.2, par) * (1 - ss(0.8, 1.0, par))
        hu += ridge(d, w, amp, blur) * fade
    h += hu * mask_upper
    return h


# ===================================================================== rosto
def face_field(P, N, mask, L, blur=0.0):
    """Rugas de expressão discretas: testa, glabela, pés de galinha, sulco nasogeniano,
    sulco mentolabial, pálpebra inferior e covinha do queixo (sulcos de 0,3-0,9 mm)."""
    x, y, z = P[:, 0], P[:, 1], P[:, 2]
    X = np.abs(x)
    front = ss(0.1, 0.45, N[:, 2]) * (z > L.face_z - 0.06)
    h = np.zeros(len(P))
    q = np.stack([X, y], 1)
    e = L.eye
    by = L.brow_y
    # testa: três linhas horizontais levemente arqueadas e interrompidas
    for i, dy in enumerate((0.03, 0.044)):
        yy = by + dy + 4.0 * X * X - 0.002 * i
        d = np.abs(y - yy)
        along = (1 - ss(0.03 + 0.004 * i, 0.048, X)) * _hash_noise(X * 140 + i * 3.1, i)
        h += ridge(d, 0.0012, -0.00017, blur) * along
    # glabela: duas linhas verticais e uma horizontal sobre o nariz
    for sx in (0.0065,):
        d, t = seg_dist(q, np.array([sx, by - 0.007]), np.array([sx + 0.001, by + 0.007]))
        h += ridge(d, 0.0013, -0.0002, blur) * ss(0.0, 0.25, t) * (1 - ss(0.6, 1.0, t))
    d = np.abs(y - (by - 0.013))
    h += ridge(d, 0.0013, -0.00008, blur) * (1 - ss(0.006, 0.011, X))
    # pés de galinha
    corner = np.array([e[0] + 0.019, e[1] - 0.001])
    for k, ang in enumerate((-28.0, -6.0, 16.0)):
        a = np.radians(ang)
        dirv = np.array([np.cos(a), np.sin(a)])
        d, t = seg_dist(q, corner + dirv * 0.004, corner + dirv * 0.017)
        h += ridge(d, 0.0009, -0.00015, blur) * ss(0.0, 0.25, t) * (1 - ss(0.6, 1.0, t))
    # pálpebra inferior (sulco sob o olho)
    u = (X - e[0]) / 0.017
    yy = e[1] - 0.0115 - 0.003 * (1 - np.clip(u * u, 0, 1))
    h += ridge(np.abs(y - yy), 0.0014, -0.00022, blur) * (1 - ss(0.7, 1.0, np.abs(u)))
    # sulco nasogeniano (do nariz ao canto da boca) com a bochecha saliente ao lado
    m = L.mouth
    pts = [(0.019, L.nose_y + 0.008), (0.025, L.nose_y - 0.006), (m[0] + 0.009, m[1] + 0.002), (m[0] + 0.011, m[1] - 0.012)]
    d, t = poly_dist(q, pts)
    sidev = X > np.interp(y, [p[1] for p in pts[::-1]], [p[0] for p in pts[::-1]])
    fade = ss(0.0, 0.15, t) * (1 - ss(0.7, 1.0, t))
    h += ridge(d, 0.0024, -0.0006, blur) * fade
    h += ridge(d - 0.004, 0.0032, 0.0003, blur) * sidev * fade
    # sulco mentolabial e covinha do queixo
    yy = L.mouth_y - 0.019 + 6.0 * X * X
    h += ridge(np.abs(y - yy), 0.003, -0.0006, blur) * (1 - ss(0.012, 0.02, X))
    d, t = seg_dist(q, np.array([0.0, L.chin_y - 0.006]), np.array([0.0, L.chin_y + 0.008]))
    h += ridge(d, 0.0022, -0.0004, blur) * ss(0.0, 0.3, t) * (1 - ss(0.7, 1.0, t))
    # linhas finas verticais nos lábios
    lip = (1 - ss(0.016, 0.022, X)) * np.exp(-((y - L.mouth_y) / 0.006) ** 2)
    h += -0.00012 * (0.5 + 0.5 * np.cos(X * 2 * np.pi / 0.0021)) ** 6 * lip
    return h * mask * front


# ===================================================================== tudo
def full_field(P, N, M, L, blur=0.0, parts=("torso", "arms", "face")):
    """M: dicionário de máscaras por ponto (torso, head, foreL, foreR, upperL, upperR).
    Só avalia cada parte onde a máscara correspondente é positiva."""
    h = np.zeros(len(P))

    def sel(*keys):
        m = np.zeros(len(P), bool)
        for k in keys:
            m |= M[k] > 1e-4
        return np.nonzero(m)[0]

    if "torso" in parts:
        i = sel("torso")
        if len(i):
            h[i] += torso_field(P[i], N[i], M["torso"][i], L, blur)
    if "arms" in parts:
        for sd in ("L", "R"):
            i = sel("fore" + sd, "upper" + sd)
            if len(i):
                h[i] += arm_field(P[i], N[i], M["fore" + sd][i], M["upper" + sd][i], L, sd, blur)
    if "face" in parts:
        i = sel("head")
        if len(i):
            h[i] += face_field(P[i], N[i], M["head"][i], L, blur)
    return h


def detail_field(P, N, M, L, geom_blur):
    """O que falta na geometria: tudo nítido menos a parte borrada do tronco que já foi esculpida."""
    return full_field(P, N, M, L, 0.0) - full_field(P, N, M, L, geom_blur, parts=("torso",))


def unity_tangents(P, N, UV, tris):
    """Mesmo algoritmo do Mesh.RecalculateTangents (acúmulo por triângulo + Gram-Schmidt)."""
    t = tris.reshape(-1, 3)
    p0, p1, p2 = P[t[:, 0]], P[t[:, 1]], P[t[:, 2]]
    w0, w1, w2 = UV[t[:, 0]], UV[t[:, 1]], UV[t[:, 2]]
    e1, e2 = p1 - p0, p2 - p0
    s1, s2 = w1[:, 0] - w0[:, 0], w2[:, 0] - w0[:, 0]
    t1, t2 = w1[:, 1] - w0[:, 1], w2[:, 1] - w0[:, 1]
    den = s1 * t2 - s2 * t1
    r = np.where(np.abs(den) > 1e-20, 1.0 / np.where(den == 0, 1, den), 0.0)
    sdir = (e1 * t2[:, None] - e2 * t1[:, None]) * r[:, None]
    tdir = (e2 * s1[:, None] - e1 * s2[:, None]) * r[:, None]
    T = np.zeros_like(P)
    Bt = np.zeros_like(P)
    for j in range(3):
        np.add.at(T, t[:, j], sdir)
        np.add.at(Bt, t[:, j], tdir)
    T = T - N * (T * N).sum(1, keepdims=True)
    T /= np.maximum(np.linalg.norm(T, axis=1, keepdims=True), 1e-12)
    w = np.where((np.cross(N, T) * Bt).sum(1) < 0, -1.0, 1.0)
    return T, w


def subtree(order, bones_in, roots):
    """Nomes dos ossos que descendem (inclusive) de algum osso em roots."""
    out = set()
    for n in order:
        b = n
        while b:
            if b in roots:
                out.add(n)
                break
            b = bones_in[b]["parent"]
    return out


def body_masks(W, bone_index, order, bones_in):
    """Máscaras por vértice a partir dos pesos de osso."""
    def tot(roots):
        names = subtree(order, bones_in, set(roots))
        idx = sorted(bone_index[n] for n in names)
        return np.clip(W[:, idx].sum(1), 0, 1)

    head = tot(["head"])
    limbs = tot(["upperarm01.L", "upperarm01.R", "upperleg01.L", "upperleg01.R", "neck01"])
    M = {"head": head, "torso": np.clip(1 - limbs, 0, 1)}
    for sd in ("L", "R"):
        M["fore" + sd] = np.clip(tot([f"lowerarm01.{sd}"]) - tot([f"wrist.{sd}"]), 0, 1)
        M["upper" + sd] = np.clip(tot([f"upperarm01.{sd}"]) - tot([f"lowerarm01.{sd}"]), 0, 1)
    return M
