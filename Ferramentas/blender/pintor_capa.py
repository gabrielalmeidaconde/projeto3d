"""Pinta as texturas da capa do Pátroclo direto no molde (capa_molde.py).

Uso: python3 -I pintor_capa.py <pasta_saida> [largura]
  -> capa_albedo.png  (RGBA: cor + alfa com a beira desfiada e os furos)
     capa_normal.jpg  (rugas do pano, relevo da barra tecida)
     capa_mascara.png (R metal, A lisura)

A textura cobre o molde inteiro: u = -W_BARRA/2..W_BARRA/2 da esquerda para a direita, v = 0 (gola, em cima da
imagem) a L (barra, embaixo). O fio da lã vem de um mapa de detalhe repetido no material; aqui fica o desenho:
tom da lã, barra tecida em ocre com grega, chamuscado em volta dos furos de brasa, barro na barra e o alfa.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image
from scipy import ndimage

import capa_molde as cm

OUT = sys.argv[1]
W = int(sys.argv[2]) if len(sys.argv) > 2 else 4096
H = W // 2
rng = np.random.default_rng(3)
PXU, PXV = cm.W_BARRA / W, cm.L / H        # metros por pixel


def ss(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def ruido(sig_v, sig_u, seed):
    """Ruído suave de média 0 e desvio 1; sigmas em pixels (v = vertical, u = horizontal)."""
    r = np.random.default_rng(seed).standard_normal((H, W)).astype(np.float32)
    n = ndimage.gaussian_filter(r, (sig_v, sig_u), mode="reflect")
    return (n - n.mean()) / (n.std() + 1e-9)


def mix(a, b, t):
    return a + (b - a) * t[..., None]


u = ((np.arange(W) + 0.5) / W * cm.W_BARRA - cm.W_BARRA / 2).astype(np.float64)
v = ((np.arange(H) + 0.5) / H * cm.L).astype(np.float64)
U, V = np.meshgrid(u, v)
d = cm.pano(U, V).astype(np.float32)
q = cm.queimado(U, V).astype(np.float32)
hl = cm.meia_largura(V)
s = (U / hl).astype(np.float32)
y = (cm.L - V).astype(np.float32)                 # altura acima da barra original
print("campos ok")

# ---------------------------------------------------------------------------- cor da lã
n_larga, n_media, n_fina = ruido(160, 160, 1), ruido(26, 26, 2), ruido(3.5, 3.5, 3)
n_fio = ruido(60, 1.3, 4)                         # riscos ao longo do fio (tingimento desigual)
carmesim = np.array([0.555, 0.098, 0.112], np.float32)
val = 1.0 + 0.07 * n_larga + 0.045 * n_media + 0.03 * n_fina + 0.04 * n_fio
cor = carmesim[None, None, :] * val[..., None]
# desbotado de sol nos ombros e no alto das costas
sol = ss(0.55, 0.0, V / cm.L).astype(np.float32) * (0.55 + 0.45 * ss(-1.0, 1.0, n_larga))
cor = mix(cor, np.array([0.63, 0.25, 0.22], np.float32)[None, None, :] * (0.9 + 0.1 * val[..., None]), 0.1 * sol)

# ---------------------------------------------------------------------------- barra tecida (fio ocre)
def faixa(y0, y1, borda=0.0012):
    return ss(y0 - borda, y0 + borda, y) * ss(y1 + borda, y1 - borda, y)


def grega(y0, alt):
    """Grega (meandro) contínua: célula de 8 x 5 unidades, traço de 0,62 unidade."""
    un = alt / 5.0
    gx = (U / un) % 8.0
    gy = (y - y0) / un
    pts = [(0.5, 0.5), (0.5, 4.5), (6.5, 4.5), (6.5, 2.5), (2.5, 2.5), (2.5, 0.5), (8.5, 0.5)]
    best = np.full(U.shape, 1e9, np.float32)
    for sh in (-8.0, 0.0, 8.0):
        px = (gx + sh).astype(np.float32)
        for (ax, ay), (bx, by) in zip(pts[:-1], pts[1:]):
            vx, vy = bx - ax, by - ay
            t = np.clip(((px - ax) * vx + (gy - ay) * vy) / (vx * vx + vy * vy), 0, 1)
            best = np.minimum(best, np.hypot(px - (ax + t * vx), gy - (ay + t * vy)))
    return ss(0.36, 0.26, best) * faixa(y0 - 0.002, y0 + alt + 0.002)


fio = np.maximum.reduce([faixa(0.085, 0.094), grega(0.106, 0.058), faixa(0.176, 0.185), faixa(0.196, 0.2005)])
# pontos quadrados acima da barra, de 3 em 3 cm
px3 = (U / 0.03) % 1.0
fio = np.maximum(fio, ss(0.36, 0.3, np.abs(px3 - 0.5).astype(np.float32) * 2) * faixa(0.214, 0.223))
# listra fina acompanhando as duas laterais
fio = np.maximum(fio, ss(0.004, 0.0025, np.abs(np.abs(s) - 0.93) * hl.astype(np.float32)) * ss(0.02, 0.06, V.astype(np.float32)))
# fio gasto: some em manchas e fica ralo
gasto = ss(-0.3, 1.1, 0.75 * ruido(40, 40, 5) + 0.5 * ruido(5, 5, 6))
fio = fio * (1.0 - 0.75 * gasto) * (0.82 + 0.18 * ss(-1, 1, ruido(1.2, 6, 7)))
ocre = np.array([0.80, 0.60, 0.24], np.float32)
cor = mix(cor, ocre[None, None, :] * (0.88 + 0.16 * n_fina[..., None] * 0.5 + 0.06 * n_media[..., None]), fio)
print("barra ok")

# ---------------------------------------------------------------------------- barro, chamuscado e beira
barro = ss(0.42, 0.0, y) * (0.5 + 0.5 * ss(-0.8, 1.0, ruido(50, 22, 8) + 0.5 * ruido(9, 9, 9)))
barro = np.clip(barro + 0.5 * ss(0.12, 0.0, y), 0, 1)
cor = mix(cor, np.array([0.27, 0.19, 0.14], np.float32)[None, None, :] * (0.85 + 0.15 * val[..., None]), 0.62 * barro)
qn = np.clip(q + 0.22 * ruido(7, 7, 10) * ss(0.0, 0.3, q), 0, 1)
cor = mix(cor, np.array([0.21, 0.085, 0.06], np.float32)[None, None, :], 0.75 * ss(0.05, 0.55, qn))
cor = mix(cor, np.array([0.035, 0.028, 0.026], np.float32)[None, None, :], ss(0.5, 0.95, qn))
# fibra exposta (mais clara e sem tinta) rente aos rasgos, menos onde está chamuscado
beira = ss(0.011, 0.0005, d) * (1 - ss(0.2, 0.6, qn))
cor = mix(cor, np.array([0.66, 0.36, 0.33], np.float32)[None, None, :] * (0.9 + 0.1 * n_fina[..., None]), 0.5 * beira)
cor = np.clip(cor, 0, 1)

# ---------------------------------------------------------------------------- alfa: beira desfiada e fios soltos
desfio = 0.0042 * ruido(16, 1.1, 11) + 0.0022 * ruido(1.4, 9, 12) + 0.0012 * ruido(2.5, 2.5, 13)
alfa = np.clip(0.5 + (d + desfio * (1 - 0.6 * ss(0.3, 0.8, qn))) / (1.6 * PXU), 0, 1)
# fios da trama pendurados abaixo da barra rasgada
col = np.floor(U / 0.0017)
h1 = np.modf(np.sin(col * 127.1) * 43758.5453)[0] % 1.0
h2 = np.modf(np.sin(col * 311.7) * 24634.6345)[0] % 1.0
dentro_col = np.abs((U / 0.0017) % 1.0 - 0.5) < 0.3
dh = ((cm.L - cm.barra(U)) - V).astype(np.float32)        # < 0 abaixo da beira da barra
comp = 0.004 + 0.034 * (h2.astype(np.float32) ** 2)
solto = (np.abs(h1) > 0.74) & dentro_col & (dh < 0.002) & (dh > -comp) & (np.abs(U) < cm.meia_largura(cm.L) - 0.01)
# os fios só existem onde logo acima há tecido (não dentro dos rasgos)
acima = cm.pano(U, np.maximum(V + dh - 0.004, 0.0)) > 0.002
alfa = np.maximum(alfa, (solto & acima).astype(np.float32) * 0.9)
img = np.concatenate([cor, alfa[..., None]], -1)
# onde é transparente a cor continua a do tecido vizinho (senão os mipmaps criam franja)
vaz = alfa < 0.02
_, (iy, ix) = ndimage.distance_transform_edt(vaz, return_indices=True)
img[vaz, :3] = img[iy[vaz], ix[vaz], :3]
Image.fromarray((img * 255 + 0.5).astype(np.uint8), "RGBA").save(f"{OUT}/capa_albedo.png", optimize=True)
print("albedo ok: tecido em %.0f%% da textura" % (100 * (alfa > 0.5).mean()))

# ---------------------------------------------------------------------------- normal: rugas, relevo do bordado e do chamuscado
alt = 0.0011 * ruido(46, 30, 20) + 0.0005 * ruido(14, 7, 21) + 0.00016 * ruido(3, 3, 22)
alt = alt + 0.00045 * ndimage.gaussian_filter(fio, 1.2) + 0.0004 * ruido(2.2, 2.2, 23) * ss(0.2, 0.8, qn)
alt = alt - 0.0008 * ss(0.006, 0.0, d)            # a beira afina
dv, du = np.gradient(alt.astype(np.float32))
nx, ny = -du / PXU, dv / PXV
nz = np.ones_like(nx)
ln = np.sqrt(nx * nx + ny * ny + nz * nz)
nrm = np.stack([nx / ln, ny / ln, nz / ln], -1)
Image.fromarray(((nrm * 0.5 + 0.5) * 255 + 0.5).astype(np.uint8), "RGB").save(f"{OUT}/capa_normal.jpg", quality=93, subsampling=0)

# ---------------------------------------------------------------------------- máscara: metal (R) e lisura (A)
liso = 0.11 + 0.035 * n_media + 0.2 * fio - 0.06 * barro
liso = liso * (1 - 0.7 * ss(0.3, 0.9, qn))
msk = np.stack([np.zeros_like(liso), np.ones_like(liso), np.zeros_like(liso), np.clip(liso, 0.02, 1)], -1)
m8 = Image.fromarray((msk * 255 + 0.5).astype(np.uint8), "RGBA").resize((W // 2, H // 2), Image.LANCZOS)
m8.save(f"{OUT}/capa_mascara.png", optimize=True)
for f in ("capa_albedo.png", "capa_normal.jpg", "capa_mascara.png"):
    print("%s %.1f MB" % (f, os.path.getsize(f"{OUT}/{f}") / 1e6))
