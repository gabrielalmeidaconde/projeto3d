"""Gera os mapas da pele do Pátroclo no UV do corpo do MakeHuman (padrão 4096 px).

Uso: python3 -I mh_skin.py <pasta_dl> <pele_difusa_makehuman.png> <pasta_saida> [tamanho]
Saídas:
  pele_normal.png    normal map (espaço tangente, OpenGL/Unity): abdômen, cinturão ilíaco, serrátil,
                     veias dos braços e rugas de expressão (mh_detail.py)
  pele_albedo.png    difusa ampliada com variação de cor (vermelhidão no nariz, orelhas, bochechas,
                     articulações e nós dos dedos; lábios; veias), poros, barba por fazer, couro
                     cabeludo sob o cabelo e dobras escurecidas (cavidade)
  pele_ms.png        R = metálico (0), A = suavidade: zona T e lábios mais brilhantes, barba fosca
"""
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage, sparse

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mh_build  # noqa: E402
import mh_detail  # noqa: E402
import mh_raster  # noqa: E402

Image.MAX_IMAGE_PIXELS = None


def curvature(P, tris, N, scales=(2, 8, 24)):
    """Concavidade multi-escala por vértice (positivo = dobra), unindo vértices duplicados nas costuras."""
    key = np.round(P, 6)
    uq, inv = np.unique(key, axis=0, return_inverse=True)
    inv = inv.ravel()
    t = inv[tris.reshape(-1, 3)]
    e = np.concatenate([t[:, [0, 1]], t[:, [1, 2]], t[:, [2, 0]]])
    e = np.concatenate([e, e[:, ::-1]])
    A = sparse.coo_matrix((np.ones(len(e)), (e[:, 0], e[:, 1])), shape=(len(uq),) * 2).tocsr()
    A.data[:] = 1.0
    deg = np.asarray(A.sum(1)).ravel()
    Nu = np.zeros_like(uq)
    np.add.at(Nu, inv, N)
    Nu /= np.maximum(np.linalg.norm(Nu, axis=1, keepdims=True), 1e-9)
    out = np.zeros(len(uq))
    Q = uq.copy()
    done = 0
    for sc in scales:
        for _ in range(sc - done):
            Q = 0.5 * Q + 0.5 * (A @ Q) / deg[:, None]
        done = sc
        c = ((Q - uq) * Nu).sum(1)  # >0: a superfície suavizada fica "para fora" = ponto em dobra
        out += np.clip(c / (0.0015 * np.sqrt(sc)), 0, 1.5)
    return out[inv] / len(scales)


def bake(res, size, step=300000):
    """Rasteriza o corpo no UV e avalia, por texel, posição, normal, máscaras, cavidade e o detalhe
    procedural (altura e normal em espaço tangente). Processa em blocos para caber na memória em 4K."""
    m = next(mm for mm in res["meshes"] if mm["name"] == "Corpo")
    P, N, UV = m["vertices"], m["normals"], m["uvs"]
    tris = m["triangles"].reshape(-1, 3)
    T, w = mh_detail.unity_tangents(P, N, UV, m["triangles"])
    keys = list(m["masks"].keys())
    Mv = np.stack([m["masks"][k] for k in keys], 1)
    cav_v = curvature(P, m["triangles"], N)
    tid, bary, zb = mh_raster._raster(UV[:, 0] * size, (1 - UV[:, 1]) * size, np.zeros(len(UV)), tris, size, size)
    del zb
    hidx = np.nonzero(tid >= 0)[0]
    n = len(hidx)
    out = {"hidx": hidx, "size": size,
           "nts": np.zeros((n, 3), np.float32), "hgt": np.zeros(n, np.float32), "cav": np.zeros(n, np.float32),
           "p": np.zeros((n, 3), np.float32), "n": np.zeros((n, 3), np.float32),
           "M": {k: np.zeros(n, np.float32) for k in keys}}
    L = res["landmarks"]
    eps = 0.0004
    for s0 in range(0, n, step):
        sl = slice(s0, min(s0 + step, n))
        ti = tid[hidx[sl]]
        b = bary[hidx[sl]]
        tri = tris[ti]

        def I(A):
            return A[tri[:, 0]] * b[:, :1] + A[tri[:, 1]] * b[:, 1:2] + A[tri[:, 2]] * b[:, 2:3]

        p = I(P)
        nn = I(N)
        nn /= np.maximum(np.linalg.norm(nn, axis=1, keepdims=True), 1e-9)
        tt = I(T)
        tt = tt - nn * (tt * nn).sum(1, keepdims=True)
        tt /= np.maximum(np.linalg.norm(tt, axis=1, keepdims=True), 1e-9)
        bb = np.cross(nn, tt) * w[tri[:, 0]][:, None]
        mm = I(Mv)
        MM = {k: mm[:, i] for i, k in enumerate(keys)}

        def f(q):
            return mh_detail.detail_field(q, nn, MM, L, mh_build.GEOM_BLUR)

        h0 = f(p)
        gt = (f(p + eps * tt) - f(p - eps * tt)) / (2 * eps)
        gb = (f(p + eps * bb) - f(p - eps * bb)) / (2 * eps)
        v = np.stack([-gt, -gb, np.ones_like(gt)], 1)
        out["nts"][sl] = v / np.linalg.norm(v, axis=1, keepdims=True)
        out["hgt"][sl] = h0
        out["cav"][sl] = I(cav_v[:, None])[:, 0]
        out["p"][sl] = p
        out["n"][sl] = nn
        for k in keys:
            out["M"][k][sl] = MM[k]
        print("  texels", sl.stop, "/", n)
    del tid, bary
    # para preencher as bordas das ilhas de UV: cada texel vazio copia o texel útil mais próximo
    hit = np.zeros(size * size, bool)
    hit[hidx] = True
    _, (iy, ix) = ndimage.distance_transform_edt(~hit.reshape(size, size), return_indices=True)
    pos_of = np.full(size * size, -1, np.int32)
    pos_of[hidx] = np.arange(n, dtype=np.int32)
    out["src"] = pos_of[(iy * size + ix).ravel()]
    return out


def full(tx, a):
    """Valores por texel útil (n,) ou (n,k) -> imagem (size, size[, k]) com as bordas preenchidas."""
    s = tx["size"]
    r = a[tx["src"]]
    return r.reshape((s, s) + a.shape[1:])


def _ss(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def beard_and_scalp(tx, L, seed=1184):
    """Barba por fazer (densidade 0..1) e couro cabeludo sob o cabelo (0..1), por texel útil.
    Tudo a partir da posição 3D do texel e dos marcos do rosto, então acompanha mudanças de formato."""
    p, M = tx["p"], tx["M"]
    head = M["head"]
    x, y, z = p[:, 0], p[:, 1], p[:, 2]
    X = np.abs(x)
    rng = np.random.default_rng(seed)
    k = rng.normal(size=(6, 3)) * 140.0
    ph = rng.uniform(0, 6.28, 6)
    low = sum(np.sin(p @ k[i] + ph[i]) for i in range(6)) / 6.0
    nose_base = L.nose_y - 0.006
    top_x = [0.0, 0.022, 0.034, 0.05, 0.062, 0.072]
    top_y = [nose_base - 0.002, nose_base - 0.004, L.mouth[1] + 0.018, L.eye[1] - 0.042, L.eye[1] - 0.026, L.eye[1] - 0.012]
    ytop = np.interp(X, top_x, top_y) + low * 0.003
    top = 1 - _ss(ytop - 0.01, ytop + 0.002, y)
    ylow = np.interp(z, [0.02, 0.06, 0.1, 0.13], [L.chin_y + 0.034, L.chin_y + 0.01, L.chin_y - 0.01, L.chin_y - 0.016]) + low * 0.003
    bottom = _ss(ylow - 0.012, ylow + 0.004, y)
    back = _ss(0.012, 0.03, z) * (1 - _ss(0.066, 0.074, X))
    lips = (X / 0.021) ** 2 + ((y - L.mouth_y - 0.001) / 0.0105) ** 2
    nolips = _ss(0.9, 1.3, lips)
    dens = 0.55 + 0.45 * np.maximum(_ss(L.mouth_y + 0.004, L.mouth_y - 0.03, y), 1 - _ss(0.018, 0.03, X))
    beard = np.clip(top * bottom * back * nolips * dens * (head > 0.3), 0, 1)
    beard *= 0.85 + 0.15 * low
    c = np.array([0.0, L.eye[1] + 0.005, L.face_z - 0.12])
    phi = np.abs(np.arctan2(x - c[0], z - c[2]))
    hl = np.interp(phi, [0.0, 0.45, 0.9, 1.45, 1.75, 2.3, 3.2],
                   [L.brow_y + 0.056, L.brow_y + 0.046, L.brow_y + 0.028, L.eye[1] + 0.028, L.eye[1] + 0.028, L.eye[1] - 0.03, L.eye[1] - 0.085])
    hl = hl + low * 0.004
    scalp = _ss(hl - 0.006, hl + 0.012, y) * (head > 0.5)
    ear = (X > 0.07) & (y < L.eye[1] + 0.036) & (y > L.eye[1] - 0.04) & (np.abs(z - (L.face_z - 0.17)) < 0.04)
    scalp *= ~ear
    lipmask = (1 - _ss(0.75, 1.15, lips)) * (z > L.face_z - 0.04) * (head > 0.5)
    return beard.astype(np.float32), scalp.astype(np.float32), lipmask.astype(np.float32)


def blob(p, c, r):
    d2 = ((p - np.asarray(c, dtype=np.float32)) ** 2).sum(1)
    return np.exp(-d2 / (r * r))


def color_zones(tx, res):
    """Vermelhidão (0..1) e zona T (0..1) por texel útil, a partir de pontos do rosto e das articulações."""
    p, M = tx["p"], tx["M"]
    L = res["landmarks"]
    bones = {b["name"]: np.asarray(b["head"], dtype=np.float32) for b in res["bones"]}
    head = M["head"]
    e = L.eye
    red = np.zeros(len(p), np.float32)
    # nariz, bochechas e orelhas
    red += 0.8 * blob(p, (0.0, L.nose_y + 0.006, L.face_z + 0.008), 0.013) * head
    for sx in (-1, 1):
        red += 0.45 * blob(p, (sx * 0.045, e[1] - 0.03, L.face_z - 0.035), 0.022) * head
        red += 0.7 * blob(p, (sx * 0.078, e[1] + 0.0, L.face_z - 0.17), 0.028) * head
    # articulações: cotovelos, joelhos, nós dos dedos e pontas dos dedos
    for sd in ("L", "R"):
        red += 0.35 * blob(p, bones[f"lowerarm01.{sd}"], 0.035)
        red += 0.4 * blob(p, bones[f"lowerleg01.{sd}"], 0.045)
        for f in range(1, 6):
            for seg in (1, 2, 3):
                nm = f"finger{f}-{seg}.{sd}"
                if nm in bones:
                    red += (0.5 if seg == 1 else 0.3) * blob(p, bones[nm], 0.011)
    tz = (blob(p, (0.0, L.brow_y + 0.03, L.face_z), 0.03) + blob(p, (0.0, L.nose_y + 0.012, L.face_z + 0.01), 0.018)) * head
    return np.clip(red, 0, 1), np.clip(tz, 0, 1)


def gnoise(size, sigma, seed):
    r = np.random.default_rng(seed)
    n = ndimage.gaussian_filter(r.standard_normal((size, size)).astype(np.float32), sigma, mode="wrap")
    return n / (n.std() + 1e-9)


def main():
    dl, diffuse, outdir = sys.argv[1], sys.argv[2], sys.argv[3]
    size = int(sys.argv[4]) if len(sys.argv) > 4 else 4096
    res = mh_build.build(dl, 1.83, verbose=False)
    tx = bake(res, size)
    Image.fromarray(np.clip(full(tx, tx["nts"]) * 127.5 + 127.5, 0, 255).astype(np.uint8)).save(os.path.join(outdir, "pele_normal.png"))
    print("normal ok")

    L = res["landmarks"]
    beard, scalp, lip = beard_and_scalp(tx, L)
    red, tzone = color_zones(tx, res)
    arms = tx["M"]["foreL"] + tx["M"]["foreR"] + tx["M"]["upperL"] + tx["M"]["upperR"]
    vein = np.clip(tx["hgt"] / 0.0007, 0, 1) * np.clip(arms, 0, 1)
    hp_n = tx["hgt"]

    base = np.asarray(Image.open(diffuse).convert("RGB").resize((size, size), Image.LANCZOS), dtype=np.float32) / 255
    bm, sm, lm_, rm, vm = (full(tx, a) for a in (beard, scalp, lip, red, vein))
    # variação de cor da pele: manchas largas (tom) e poros (pontinhos finos)
    mott = gnoise(size, size / 180, 21) * 0.6 + gnoise(size, size / 900, 22) * 0.4
    pores = gnoise(size, 0.7, 23)
    alb = base * (1 + 0.035 * mott[..., None]) * (1 - 0.035 * np.clip(pores, 0, 3)[..., None] / 3)
    alb *= 1 - (rm * (0.6 + 0.4 * np.clip(mott, -1, 1) * 0.5))[..., None] * np.array([0.0, 0.13, 0.15], np.float32)
    alb *= 1 - lm_[..., None] * np.array([0.04, 0.2, 0.17], np.float32)
    alb *= 1 - vm[..., None] * np.array([0.08, 0.05, 0.0], np.float32)
    # barba por fazer: sombra azulada + fios (pontos); couro cabeludo escuro com fios curtos
    rng = np.random.default_rng(77)
    dots = rng.random((size, size), dtype=np.float32)
    alb *= 1 - bm[..., None] * np.array([0.28, 0.28, 0.24], np.float32) * 0.85
    hc = np.array([0.09, 0.065, 0.05], np.float32)
    hair = dots > 1 - 0.3 * bm
    alb[hair] = alb[hair] * 0.55 + hc * 0.45
    sc = np.array([0.075, 0.055, 0.042], np.float32) * (0.8 + 0.4 * ndimage.gaussian_filter(dots, 1.0)[..., None])
    alb = alb * (1 - 0.75 * sm[..., None]) + sc * 0.75 * sm[..., None]
    hair = dots > 1 - 0.55 * sm
    alb[hair] = alb[hair] * 0.4 + hc * 0.6
    del dots, hair
    # cavidade: dobras da geometria + sulcos do detalhe (passa-alta); dobras um pouco mais escuras e rosadas
    hgt = full(tx, hp_n)
    hp = hgt - ndimage.gaussian_filter(hgt, 12)
    groove = np.clip(-hp / 0.0009, 0, 1)
    c = np.clip(0.3 * full(tx, tx["cav"]) + 0.5 * groove, 0, 1)
    c = ndimage.gaussian_filter(c, 1.2)
    alb = alb * (1 - 0.25 * c[..., None]) * (1 - np.array([0.0, 0.18, 0.22], np.float32) * 0.3 * c[..., None])
    Image.fromarray(np.clip(alb * 255 + 0.5, 0, 255).astype(np.uint8)).save(os.path.join(outdir, "pele_albedo.png"))
    del alb, base
    print("albedo ok")

    # suavidade: pele levemente oleosa, zona T e lábios mais brilhantes, barba e couro cabeludo foscos
    smooth = 0.4 + 0.14 * tzone + 0.2 * lip - 0.12 * beard - 0.16 * scalp + 0.03 * np.clip(arms + tx["M"]["torso"], 0, 1)
    sm_img = full(tx, smooth.astype(np.float32)) - 0.05 * np.clip(pores, -2, 2) / 2 - 0.06 * c
    half = size // 2
    a8 = np.clip(sm_img * 255 + 0.5, 0, 255).astype(np.uint8)
    rgba = np.zeros((size, size, 4), np.uint8)
    rgba[..., 3] = a8
    Image.fromarray(rgba, "RGBA").resize((half, half), Image.LANCZOS).save(os.path.join(outdir, "pele_ms.png"))
    print("ok", outdir)


if __name__ == "__main__":
    main()
