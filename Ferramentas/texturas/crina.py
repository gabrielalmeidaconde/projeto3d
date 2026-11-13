"""Textura das mechas de crina (crista e rabo do elmo): fios com alfa, para malhas de "cartões".

Uso: python3 -I crina.py <pasta_saida>   ->  crina.png (RGBA: cor neutra + alfa) e crina_normal.png

A imagem tem 4 mechas diferentes lado a lado; cada cartão da malha usa uma faixa (u) e o fio corre de
baixo (raiz, v = 0) para cima (ponta, v = 1). A cor é quase cinza: o material do Unity tinge de carmesim.
"""
import sys

import numpy as np
from PIL import Image

OUT = sys.argv[1]
W, H, NVAR = 1024, 2048, 4
CW = W // NVAR
rng = np.random.default_rng(11)

cov = np.zeros((H, W), np.float32)
hgt = np.zeros((H, W), np.float32)
col = np.zeros((H, W), np.float32)
rows = np.arange(H)
v = (1.0 - rows / (H - 1.0)).astype(np.float32)        # 0 na raiz (linha de baixo), 1 na ponta
off = np.arange(-7, 8)


def ss(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


for var in range(NVAR):
    lo, hi = var * CW + 14, (var + 1) * CW - 14
    # camada de fundo: tufo denso e escuro perto da raiz, para a base não ficar vazada
    xs = np.arange(var * CW, (var + 1) * CW)
    borda = np.minimum(xs - var * CW, (var + 1) * CW - 1 - xs)[None, :].astype(np.float32)
    topo = 0.62 + 0.1 * np.sin(xs * 0.11 + var)[None, :] + 0.05 * np.sin(xs * 0.47 + 2 * var)[None, :]
    und = ss(0.0, 0.25, topo - v[:, None]) * ss(5.0, 16.0, borda)
    cov[:, var * CW:(var + 1) * CW] = und
    col[:, var * CW:(var + 1) * CW] = 0.6
    for i in range(120):
        x0 = rng.uniform(lo, hi)
        fundo = rng.random()                        # profundidade: decide quem fica na frente
        esp = rng.uniform(2.6, 5.2)                 # espessura do fio/mecha em pixels
        vend = 1.0 - rng.beta(1.1, 5.0) * 0.42      # a maioria vai até a ponta; alguns fios são mais curtos
        xc = x0 + rng.normal(0, 9.0) * v + rng.normal(0, 4.5) * np.sin(v * rng.uniform(0.7, 2.2) * 6.283 + rng.uniform(0, 6.283)) * v
        xc = np.clip(xc, var * CW + 5, (var + 1) * CW - 6)
        r = esp * 0.5 * np.clip((vend - v) / 0.16, 0.0, 1.0) ** 0.7
        claro = rng.uniform(0.74, 1.0)
        cc = np.floor(xc).astype(np.int64)[:, None] + off[None, :]
        dist = np.abs(cc - xc[:, None])
        rr = np.maximum(r[:, None], 1e-3)
        a = np.clip(rr - dist + 0.5, 0.0, 1.0) * (r[:, None] > 0.05)
        hh = np.sqrt(np.clip(1.0 - (dist / rr) ** 2, 0.0, 1.0)) * (0.35 + 0.65 * fundo) * (a > 0)
        rr2 = np.broadcast_to(rows[:, None], cc.shape)
        ok = (cc >= var * CW) & (cc < (var + 1) * CW)
        m = ok & (hh > hgt[rr2, np.clip(cc, 0, W - 1)])
        hgt[rr2[m], cc[m]] = hh[m]
        col[rr2[m], cc[m]] = claro * (0.72 + 0.28 * hh[m])
        m2 = ok & (a > cov[rr2, np.clip(cc, 0, W - 1)])
        cov[rr2[m2], cc[m2]] = a[m2]

# raiz mais escura (sombra dentro do tufo) e ponta um pouco queimada de sol
tom = (0.68 + 0.32 * ss(0.0, 0.25, v)) * (1.0 + 0.1 * ss(0.7, 1.0, v))
rgb = np.clip(col * tom[:, None], 0, 1)
# leve variação quente/fria entre fios, para o tingimento não ficar chapado
quente = (np.sin(np.arange(W) * 0.9)[None, :] * 0.03).astype(np.float32)
img = np.stack([np.clip(rgb * (1 + quente), 0, 1), rgb, np.clip(rgb * (1 - quente), 0, 1), cov], -1)
# onde é transparente, a cor repete a média dos fios (senão os mipmaps criam uma franja escura)
vaz = cov < 0.02
img[vaz, 0:3] = 0.62
Image.fromarray((img * 255 + 0.5).astype(np.uint8), "RGBA").save(f"{OUT}/crina.png")
gy, gx = np.gradient(hgt)
k = 2.2
n = np.stack([-gx * k, gy * k, np.ones_like(hgt)], -1)
n /= np.linalg.norm(n, axis=-1, keepdims=True)
Image.fromarray(((n * 0.5 + 0.5) * 255 + 0.5).astype(np.uint8), "RGB").save(f"{OUT}/crina_normal.png")
print("ok crina: cobertura média %.2f | por faixa de v:" % cov.mean(), [round(float(cov[int((1 - a) * (H - 1))].mean()), 2) for a in (0.05, 0.3, 0.6, 0.85, 0.97)])
