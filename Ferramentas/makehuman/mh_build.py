"""Monta o corpo do Pátroclo a partir dos assets CC0 do MakeHuman e exporta JSON para o Unity.

Uso: python3 -I mh_build.py <pasta_dl> <arquivo_saida.json> <altura_m>

Também pode ser importado (mh_preview.py / mh_skin.py usam build()).
O que sai no JSON:
  Corpo        malha subdividida (Catmull-Clark) que aparece no jogo
  CorpoAjuste  malha base (sem subdivisão), usada só para ajustar a armadura
  Olhos, Sobrancelhas, Cilios, Cabelo   peças ajustadas (.mhclo)
"""
import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mh_detail  # noqa: E402

# ===================================================================== configuração
# macros do MakeHuman (0..1; músculo pode passar de 1 = extrapola o "maxmuscle")
MACRO = dict(muscle=1.12, weight=0.5, height=0.75, proportions=0.9,
             ethnic={"caucasian": 0.72, "african": 0.16, "asian": 0.12})

# modificadores: (target, peso). Físico de estátua grega: forte e definido, sem volumes inflados.
BODY = [
    # tronco em V: ombros largos, peito e dorsais firmes, cintura fina
    ("torso/torso-vshape-incr", 0.75),
    ("torso/torso-muscle-pectoral-incr", 0.65),
    ("torso/torso-muscle-dorsi-incr", 0.55),
    ("measure/measure-shoulder-dist-incr", 0.3),
    ("measure/measure-waist-circ-decr", 0.35),
    ("stomach/stomach-pregnant-decr", 0.4),
    ("hip/hip-scale-horiz-decr", 0.1),
    # pescoço forte, mas com a linha da mandíbula visível
    ("neck/neck-scale-horiz-incr", 0.24),
    ("neck/neck-scale-depth-incr", 0.12),
    ("measure/measure-neck-circ-incr", 0.12),
    # braços: deltoides e bíceps marcados, sem "linguiça"
    ("armslegs/l-upperarm-shoulder-muscle-incr", 0.7), ("armslegs/r-upperarm-shoulder-muscle-incr", 0.7),
    ("armslegs/l-upperarm-muscle-incr", 0.75), ("armslegs/r-upperarm-muscle-incr", 0.75),
    ("armslegs/l-lowerarm-muscle-incr", 0.55), ("armslegs/r-lowerarm-muscle-incr", 0.55),
    ("armslegs/l-hand-scale-incr", 0.1), ("armslegs/r-hand-scale-incr", 0.1),
    # pernas atléticas
    ("armslegs/l-upperleg-muscle-incr", 0.6), ("armslegs/r-upperleg-muscle-incr", 0.6),
    ("armslegs/l-lowerleg-muscle-incr", 0.55), ("armslegs/r-lowerleg-muscle-incr", 0.55),
]

FACE = [
    # rosto oval e forte (ideal grego): mandíbula marcada sem ficar larga, queixo projetado com covinha
    ("head/head-oval", 0.25),
    ("head/head-square", 0.15),
    ("head/head-fat-decr", 0.5),
    # cabeça um pouco maior: com ombros e pescoço tão largos ela parecia pequena
    ("head/head-scale-vert-incr", 0.12),
    ("head/head-scale-horiz-incr", 0.06),
    ("head/head-scale-depth-incr", 0.08),
    ("chin/chin-prominent-incr", 0.3),
    ("chin/chin-width-incr", 0.1),
    ("chin/chin-bones-incr", 0.2),
    ("chin/chin-cleft-incr", 0.35),
    # maçãs do rosto marcadas e bochechas secas
    ("cheek/l-cheek-bones-incr", 0.45), ("cheek/r-cheek-bones-incr", 0.45),
    ("cheek/l-cheek-volume-decr", 0.5), ("cheek/r-cheek-volume-decr", 0.5),
    ("cheek/l-cheek-inner-decr", 0.2), ("cheek/r-cheek-inner-decr", 0.2),
    # nariz grego: reto, ponte alta, ponta fina
    ("nose/nose-greek-incr", 0.45),
    ("nose/nose-hump-decr", 0.2),
    ("nose/nose-point-width-decr", 0.3),
    ("nose/nose-flaring-decr", 0.25),
    ("nose/nose-scale-horiz-decr", 0.1),
    ("nose/nose-volume-decr", 0.15),
    ("nose/nose-width1-decr", 0.15),
    # boca firme: lábio inferior menos carnudo, cantos neutros, menos projetada
    ("mouth/mouth-upperlip-volume-decr", 0.3),
    ("mouth/mouth-lowerlip-volume-decr", 0.5),
    ("mouth/mouth-angles-up", 0.1),
    ("mouth/mouth-cupidsbow-incr", 0.25),
    ("mouth/mouth-scale-depth-decr", 0.25),
    ("mouth/mouth-trans-backward", 0.15),
    # olhos um pouco fundos (olhar de guerreiro)
    ("eyes/l-eye-height2-decr", 0.1), ("eyes/r-eye-height2-decr", 0.1),
    ("eyes/l-eye-eyefold-down", 0.25), ("eyes/r-eye-eyefold-down", 0.25),
    ("eyes/l-eye-push1-in", 0.2), ("eyes/r-eye-push1-in", 0.2),
    ("eyes/l-eye-bag-decr", 0.3), ("eyes/r-eye-bag-decr", 0.3),
    # sobrancelha baixa e arcada forte
    ("eyebrows/eyebrows-trans-down", 0.25),
    ("eyebrows/eyebrows-trans-forward", 0.45),
    ("eyebrows/eyebrows-angle-down", 0.15),
    ("forehead/forehead-temple-decr", 0.2),
]

# realce da musculatura (passa-banda geométrico na malha base, fora cabeça/mãos/pés):
# (suavização fina a, suavização grossa b, ganho saliências, ganho sulcos, limite em dm)
ENHANCE = [(1, 6, 0.7, 0.45, 0.035)]

SUBDIVISIONS = 1

PROXIES = [
    # (pasta relativa ao pacote, nome base, material, nome da malha)
    ("eyes/high-poly", "high-poly", "olhos", "Olhos"),
    ("eyebrows/eyebrow001", "eyebrow001", "sobrancelha", "Sobrancelhas"),
    ("eyelashes/eyelashes01", "eyelashes01", "cilios", "Cilios"),
    ("hair/short04", "short04", "cabelo", "Cabelo"),
]


# ===================================================================== leitura
def load_base(dl):
    verts, uvs, faces = [], [], []  # faces: (group, [(v, vt), ...])
    group = None
    with open(os.path.join(dl, "3dobjs/base.obj")) as f:
        for line in f:
            if line.startswith("v "):
                verts.append([float(x) for x in line.split()[1:4]])
            elif line.startswith("vt "):
                uvs.append([float(x) for x in line.split()[1:3]])
            elif line.startswith("g "):
                group = line.split()[1]
            elif line.startswith("f "):
                corners = []
                for c in line.split()[1:]:
                    p = c.split("/")
                    corners.append((int(p[0]) - 1, int(p[1]) - 1 if len(p) > 1 and p[1] else -1))
                faces.append((group, corners))
    return np.array(verts, dtype=np.float64), np.array(uvs, dtype=np.float64), faces


_target_cache = {}


def load_target(path):
    if path in _target_cache:
        return _target_cache[path]
    idx, d = [], []
    with open(path) as f:
        for line in f:
            if not line.strip() or line.startswith("#"):
                continue
            p = line.split()
            idx.append(int(p[0]))
            d.append([float(p[1]), float(p[2]), float(p[3])])
    r = (np.array(idx, dtype=np.int64), np.array(d, dtype=np.float64))
    _target_cache[path] = r
    return r


def _lerp3(x):
    """Fatores (min, average, max) do MakeHuman para um valor 0..1 (extrapola fora disso)."""
    if x < 0.5:
        return {"min": (0.5 - x) * 2, "average": 1 - (0.5 - x) * 2, "max": 0.0}
    return {"min": 0.0, "average": 1 - (x - 0.5) * 2, "max": (x - 0.5) * 2}


def macro_targets(macro):
    """Lista (arquivo, peso) equivalente aos sliders de macro do MakeHuman (homem jovem)."""
    out = []
    for eth, ew in macro["ethnic"].items():
        out.append((f"{eth}-male-young", ew))
    mus = {k + "muscle": v for k, v in _lerp3(macro["muscle"]).items()}
    wei = {k + "weight": v for k, v in _lerp3(macro["weight"]).items()}
    hgt = (macro["height"] - 0.5) * 2
    for m, mw in mus.items():
        for w, ww in wei.items():
            f = mw * ww
            if abs(f) < 1e-6:
                continue
            out.append((f"universal-male-young-{m}-{w}", f))
            if hgt > 0:
                out.append((f"height/male-young-{m}-{w}-maxheight", f * hgt))
            if macro["proportions"] > 0:
                out.append((f"proportions/male-young-{m}-{w}-idealproportions", f * macro["proportions"]))
    return out


def apply_targets(V, dl, lst):
    T = os.path.join(dl, "targets")
    for name, w in lst:
        path = os.path.join(T, name if name.endswith(".target") else name + ".target")
        if not os.path.exists(path):
            raise FileNotFoundError(path)
        idx, d = load_target(path)
        if len(idx):
            V[idx] += d * w


# ===================================================================== pesos
def load_weights(dl, bone_index, nv):
    wraw = json.load(open(os.path.join(dl, "rigs/default_weights.mhw")))["weights"]
    W = np.zeros((nv, len(bone_index)), dtype=np.float32)
    for bname, lst in wraw.items():
        bi = bone_index[bname]
        a = np.array(lst, dtype=np.float64)
        np.add.at(W[:, bi], a[:, 0].astype(np.int64), a[:, 1])
    return W


def top4(W):
    """W: (n, nb) -> índices (n,4), pesos normalizados (n,4)."""
    idx = np.argsort(-W, axis=1)[:, :4]
    w = np.take_along_axis(W, idx, axis=1).astype(np.float64)
    s = w.sum(1, keepdims=True)
    s[s == 0] = 1
    w /= s
    idx[w == 0] = 0
    return idx, w


# ===================================================================== subdivisão
def catmull_clark(P, fv, ft, UV, W):
    """Um nível de Catmull-Clark. fv/ft: listas de faces (vértices / uvs). UV e pesos lineares.
    Retorna P2, fv2, ft2, UV2, W2 e as posições-limite aproximadas dos vértices originais (P2[:nv])."""
    nv, nf = len(P), len(fv)
    fp = np.array([P[f].mean(0) for f in fv])
    edge_id, ev, ef = {}, [], []
    for fi, f in enumerate(fv):
        k = len(f)
        for i in range(k):
            a, b = f[i], f[(i + 1) % k]
            key = (a, b) if a < b else (b, a)
            e = edge_id.get(key)
            if e is None:
                e = edge_id[key] = len(ev)
                ev.append(key)
                ef.append([])
            ef[e].append(fi)
    EV = np.array(ev)
    ne = len(EV)
    mid = (P[EV[:, 0]] + P[EV[:, 1]]) * 0.5
    ep = mid.copy()
    two = np.array([len(l) == 2 for l in ef])
    f0 = np.array([l[0] for l in ef])
    f1 = np.array([l[-1] for l in ef])
    ep[two] = (P[EV[two, 0]] + P[EV[two, 1]] + fp[f0[two]] + fp[f1[two]]) * 0.25

    Fsum = np.zeros((nv, 3))
    Fcnt = np.zeros(nv)
    for fi, f in enumerate(fv):
        Fsum[f] += fp[fi]
        Fcnt[f] += 1
    Rsum = np.zeros((nv, 3))
    Rcnt = np.zeros(nv)
    np.add.at(Rsum, EV[:, 0], mid)
    np.add.at(Rsum, EV[:, 1], mid)
    np.add.at(Rcnt, EV[:, 0], 1)
    np.add.at(Rcnt, EV[:, 1], 1)
    used = Fcnt > 0
    n = np.maximum(Rcnt, 1)
    newP = P.copy()
    newP[used] = ((Fsum[used] / Fcnt[used, None]) + 2 * Rsum[used] / n[used, None]
                  + (n[used, None] - 3) * P[used]) / n[used, None]
    bnd = ~two
    bsum = np.zeros((nv, 3))
    bcnt = np.zeros(nv)
    bev = EV[bnd]
    np.add.at(bsum, bev[:, 0], P[bev[:, 1]])
    np.add.at(bsum, bev[:, 1], P[bev[:, 0]])
    np.add.at(bcnt, bev[:, 0], 1)
    np.add.at(bcnt, bev[:, 1], 1)
    isb = bcnt == 2
    newP[isb] = 0.75 * P[isb] + 0.125 * bsum[isb]
    P2 = np.concatenate([newP, ep, fp])

    W2 = np.concatenate([W, (W[EV[:, 0]] + W[EV[:, 1]]) * 0.5,
                         np.array([W[f].mean(0) for f in fv], dtype=W.dtype)])

    nt = len(UV)
    uv_list = [UV]
    euv_id = {}
    extra = []
    fv2, ft2 = [], []
    for fi, (f, t) in enumerate(zip(fv, ft)):
        k = len(f)
        fuv = nt + len(extra)
        extra.append(UV[t].mean(0))
        eids, euvs = [], []
        for i in range(k):
            a, b = f[i], f[(i + 1) % k]
            eids.append(nv + edge_id[(a, b) if a < b else (b, a)])
            ta, tb = t[i], t[(i + 1) % k]
            key = (ta, tb) if ta < tb else (tb, ta)
            u = euv_id.get(key)
            if u is None:
                u = euv_id[key] = nt + len(extra)
                extra.append((UV[ta] + UV[tb]) * 0.5)
            euvs.append(u)
        fid = nv + ne + fi
        for i in range(k):
            fv2.append([f[i], eids[i], fid, eids[i - 1]])
            ft2.append([t[i], euvs[i], fuv, euvs[i - 1]])
    UV2 = np.concatenate([UV, np.array(extra)])
    return P2, fv2, ft2, UV2, W2


# ===================================================================== montagem
def enhance(V, fv, W, mask_excl, passes):
    """Realça a musculatura com um filtro passa-banda geométrico ao longo da normal:
    d = (suave_a(V) - suave_b(V))·N; saliências ganham k_mais, sulcos k_menos; limitado a +-cap (dm).
    Máscara por peso de osso (fora cabeça, mãos e pés)."""
    from scipy import sparse
    used = np.unique(np.concatenate(fv))
    loc = -np.ones(len(V), dtype=np.int64)
    loc[used] = np.arange(len(used))
    e = []
    for f in fv:
        k = len(f)
        for i in range(k):
            e.append((loc[f[i]], loc[f[(i + 1) % k]]))
    e = np.array(e)
    e = np.concatenate([e, e[:, ::-1]])
    A = sparse.coo_matrix((np.ones(len(e)), (e[:, 0], e[:, 1])), shape=(len(used),) * 2).tocsr()
    A.data[:] = 1.0
    deg = np.asarray(A.sum(1)).ravel()
    mask = np.clip(1.0 - W[used][:, mask_excl].sum(1), 0, 1)
    # a máscara também é suavizada para não criar degraus no pescoço/punhos/tornozelos
    for _ in range(4):
        mask = 0.5 * mask + 0.5 * (A @ mask) / deg

    def smooth(Q, n):
        for _ in range(n):
            Q = 0.5 * Q + 0.5 * (A @ Q) / deg[:, None]
        return Q

    def normals(Q):
        N = np.zeros_like(Q)
        for f in fv:
            lf = loc[f]
            p = Q[lf]
            for j in range(1, len(f) - 1):
                N[[lf[0], lf[j], lf[j + 1]]] += np.cross(p[j] - p[0], p[j + 1] - p[0])
        return N / np.maximum(np.linalg.norm(N, axis=1, keepdims=True), 1e-12)

    Q0 = V[used].copy()
    for a, b, kp, kn, cap in passes:
        Qa = smooth(Q0, a)
        Qb = smooth(Qa, b - a)
        N = normals(Qb)
        d = ((Qa - Qb) * N).sum(1)
        disp = np.where(d > 0, kp * d, kn * d)
        disp = cap * np.tanh(disp / cap)
        Q0 = Q0 + (disp * mask)[:, None] * N
    V = V.copy()
    V[used] = Q0
    return V


def vertex_normals(P, F):
    """Normais suaves (soma das normais das faces) para faces em lista; vetorizado para quads."""
    N = np.zeros_like(P)
    quads = [f for f in F if len(f) == 4]
    if quads:
        Q = np.array(quads)
        p0, p1, p2, p3 = P[Q[:, 0]], P[Q[:, 1]], P[Q[:, 2]], P[Q[:, 3]]
        fn = np.cross(p2 - p0, p3 - p1)
        for j in range(4):
            np.add.at(N, Q[:, j], fn)
    for f in F:
        if len(f) != 4:
            p = P[f]
            for k in range(1, len(f) - 1):
                N[[f[0], f[k], f[k + 1]]] += np.cross(p[k] - p[0], p[k + 1] - p[0])
    return N / np.maximum(np.linalg.norm(N, axis=1, keepdims=True), 1e-12)


# marcos da malha base hm08 do MakeHuman (índices de vértice estáveis entre targets)
NAVEL = (4120, 4121, 4116)
XIPHOID = (4070,)

# borrão (m) da escultura que vai para a geometria; o resto vira normal map (mh_skin.py)
GEOM_BLUR = 0.02


subtree = mh_detail.subtree


def build(dl, height, macro=MACRO, body=BODY, face=FACE, subdivisions=SUBDIVISIONS, proxies=PROXIES,
          enhance_passes=ENHANCE, do_sculpt=True, pack=None, verbose=True):
    """Retorna dict com: bones, meshes (listas numpy em espaço Unity) e dados auxiliares."""
    pack = pack or os.path.join(dl, "pack")
    V, UV, faces = load_base(dl)
    apply_targets(V, dl, macro_targets(macro))
    apply_targets(V, dl, body)
    apply_targets(V, dl, face)

    body_faces = [c for g, c in faces if g == "body"]
    fv = [np.array([v for v, _ in c]) for c in body_faces]
    ft = [np.array([t for _, t in c]) for c in body_faces]

    skel = json.load(open(os.path.join(dl, "rigs/default.mhskel")))
    bones_in = skel["bones"]
    order, seen = [], set()

    def visit(name):
        if name in seen:
            return
        par = bones_in[name]["parent"]
        if par:
            visit(par)
        seen.add(name)
        order.append(name)

    for b in bones_in:
        visit(b)
    bone_index = {n: i for i, n in enumerate(order)}
    W = load_weights(dl, bone_index, len(V))
    if enhance_passes:
        excl = subtree(order, bones_in, {"head", "wrist.L", "wrist.R", "foot.L", "foot.R"})
        V = enhance(V, fv, W, sorted(bone_index[b] for b in excl), enhance_passes)

    # subdivisão (o primeiro nível também dá a posição suavizada dos vértices originais)
    P, F, T, U, WW = V, fv, ft, UV, W
    for _ in range(subdivisions):
        P, F, T, U, WW = catmull_clark(P, F, T, U, WW)
    Vfit = P[:len(V)]  # vértices originais já suavizados: base para as peças ajustadas

    body_v = np.unique(np.concatenate(F))
    ymin = P[body_v, 1].min()
    ymax = P[body_v, 1].max()
    scale = height / (ymax - ymin)
    if verbose:
        print("altura MH (dm)", round(ymax - ymin, 3), "escala", round(scale, 5))

    def to_unity(p):
        """MakeHuman (dm, destro, frente +Z) -> Unity (m, canhoto): espelha X, pés em y=0."""
        q = (np.asarray(p, dtype=np.float64) - np.array([0.0, ymin, 0.0])) * scale
        q[..., 0] *= -1.0
        return q

    joints = {k: V[v].mean(axis=0) for k, v in skel["joints"].items()}
    bones_out = []
    for nme in order:
        b = bones_in[nme]
        bones_out.append({"name": nme, "parent": bone_index[b["parent"]] if b["parent"] else -1,
                          "head": to_unity(joints[b["head"]]), "tail": to_unity(joints[b["tail"]])})

    # escultura anatômica do tronco: só a parte larga (borrada) entra na geometria
    lm = mh_detail.Landmarks(bones_out, to_unity(V[list(NAVEL)].mean(0)), to_unity(V[list(XIPHOID)].mean(0)))
    Vadj = V
    if do_sculpt:
        def carve(Q, FF, WQ):
            NQ = vertex_normals(Q, FF)
            Nu = NQ * np.array([-1.0, 1.0, 1.0])
            M = mh_detail.body_masks(WQ, bone_index, order, bones_in)
            h = mh_detail.torso_field(to_unity(Q), Nu, M["torso"], lm, GEOM_BLUR)
            return Q + (h / scale)[:, None] * NQ
        Vadj = carve(V, fv, W)
        P = carve(P, F, WW)

    meshes = [mesh_data("Corpo", P, F, T, U, WW, "pele", to_unity, verbose)]
    meshes[0]["masks"] = {k: v[meshes[0]["src"]] for k, v in mh_detail.body_masks(WW, bone_index, order, bones_in).items()}
    if subdivisions:
        meshes.append(mesh_data("CorpoAjuste", Vadj, fv, ft, UV, W, "ajuste", to_unity, verbose))
    for folder, base, mat, label in proxies:
        Pp, puv, pf, pw = load_proxy(os.path.join(pack, folder), base, Vfit, W)
        meshes.append(mesh_data(label, Pp, [np.array([v for v, _ in c]) for c in pf],
                                [np.array([t for _, t in c]) for c in pf], puv, pw, mat, to_unity, verbose))
    return {"height": height, "bones": bones_out, "meshes": meshes, "bone_index": bone_index,
            "to_unity": to_unity, "landmarks": lm}


def mesh_data(name, P, fv, ft, UV, W, material, to_unity, verbose=True):
    """Separa vértices nas costuras de UV, calcula normais suaves e pesos top-4 (espaço Unity)."""
    nrm = np.zeros_like(P)
    for f in fv:
        p = P[f]
        for k in range(1, len(f) - 1):
            nrm[[f[0], f[k], f[k + 1]]] += np.cross(p[k] - p[0], p[k + 1] - p[0])
    ln = np.linalg.norm(nrm, axis=1, keepdims=True)
    ln[ln == 0] = 1
    nrm /= ln
    key = {}
    src_v, src_t, tris = [], [], []
    for f, t in zip(fv, ft):
        ids = []
        for v, u in zip(f, t):
            k = (int(v), int(u))
            if k not in key:
                key[k] = len(src_v)
                src_v.append(int(v))
                src_t.append(int(u))
            ids.append(key[k])
        for k in range(1, len(ids) - 1):
            tris += [ids[0], ids[k + 1], ids[k]]  # espelhamento em X inverte a orientação
    src_v = np.array(src_v)
    src_t = np.array(src_t)
    n = nrm[src_v].copy()
    n[:, 0] *= -1
    bi, bw = top4(W[src_v])
    uv = UV[src_t] if len(UV) else np.zeros((len(src_v), 2))
    uv = np.where((src_t >= 0)[:, None], uv, 0.0)
    if verbose:
        print(name, len(src_v), "vértices", len(tris) // 3, "triângulos")
    return {"name": name, "material": material, "vertices": to_unity(P[src_v]), "normals": n, "uvs": uv,
            "triangles": np.array(tris, dtype=np.int64), "boneIndex": bi, "boneWeight": bw, "src": src_v}


def load_proxy(folder, base, V, W):
    mh = os.path.join(folder, base + ".mhclo")
    scales, refs, obj_file, reading = {}, [], None, False
    with open(mh) as f:
        for line in f:
            p = line.split()
            if not p or line.startswith("#"):
                continue
            if reading and not p[0][0].isalpha():
                refs.append([float(x) for x in p])
                continue
            if p[0] in ("x_scale", "y_scale", "z_scale"):
                scales[p[0][0]] = (int(p[1]), int(p[2]), float(p[3]))
            elif p[0] == "obj_file":
                obj_file = p[1]
            elif p[0] == "verts":
                reading = True
    puv, pf = [], []
    with open(os.path.join(folder, obj_file)) as f:
        for line in f:
            if line.startswith("vt "):
                puv.append([float(x) for x in line.split()[1:3]])
            elif line.startswith("f "):
                c = []
                for q in line.split()[1:]:
                    s = q.split("/")
                    c.append((int(s[0]) - 1, int(s[1]) - 1 if len(s) > 1 and s[1] else -1))
                pf.append(c)
    sc = []
    for ax, i in (("x", 0), ("y", 1), ("z", 2)):
        if ax in scales:
            a, b, dist = scales[ax]
            sc.append(abs(V[a][i] - V[b][i]) / dist)
        else:
            sc.append(1.0)
    sc = np.array(sc)
    P = np.zeros((len(refs), 3))
    PW = np.zeros((len(refs), W.shape[1]), dtype=W.dtype)
    for i, r in enumerate(refs):
        if len(r) == 1:
            vi = int(r[0])
            P[i] = V[vi]
            PW[i] = W[vi]
        else:
            v1, v2, v3 = int(r[0]), int(r[1]), int(r[2])
            w1, w2, w3 = r[3], r[4], r[5]
            P[i] = w1 * V[v1] + w2 * V[v2] + w3 * V[v3] + np.array(r[6:9]) * sc
            PW[i] = w1 * W[v1] + w2 * W[v2] + w3 * W[v3]
    return P, np.array(puv), pf, PW


def to_json(res):
    out = {"height": res["height"], "bones": [], "meshes": []}
    for b in res["bones"]:
        out["bones"].append({"name": b["name"], "parent": b["parent"],
                             "head": np.round(b["head"], 5).tolist(), "tail": np.round(b["tail"], 5).tolist()})
    for m in res["meshes"]:
        out["meshes"].append({
            "name": m["name"], "material": m["material"],
            "vertices": np.round(m["vertices"], 5).ravel().tolist(),
            "normals": np.round(m["normals"], 4).ravel().tolist(),
            "uvs": np.round(m["uvs"], 5).ravel().tolist(),
            "triangles": m["triangles"].tolist(),
            "boneIndex": m["boneIndex"].ravel().tolist(),
            "boneWeight": np.round(m["boneWeight"], 4).ravel().tolist(),
        })
    return out


def main():
    dl, out, height = sys.argv[1], sys.argv[2], float(sys.argv[3])
    res = build(dl, height)
    json.dump(to_json(res), open(out, "w"))
    print("ok", out, os.path.getsize(out) // 1024, "KB")
    idx = res["bone_index"]
    for n in ("root", "spine03", "neck01", "head", "upperarm01.L", "wrist.L", "upperleg01.L", "foot.L"):
        b = res["bones"][idx[n]]
        print(n, np.round(b["head"], 3), "->", np.round(b["tail"], 3))


if __name__ == "__main__":
    main()
