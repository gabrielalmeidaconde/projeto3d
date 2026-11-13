"""Bronze martelado e polido (4096 px, repetível) para a armadura do Pátroclo.

Uso: python3 -I bronze_martelado.py <pasta_saida> [tamanho]

Uma repetição da textura cobre 0,5 m da peça (escala de UV do bronze no MeshBuilder), então
cada pixel tem ~0,12 mm. Camadas:
  - marcas de martelo: amassados redondos sobrepostos (3 a 5 mm de raio), rasos e repolidos;
  - ondulação larga da chapa batida;
  - riscos finos de polimento (direcionais), arranhões fundos e sulcos largos de golpes de lâmina;
  - manchas de mão/gordura (menos brilho) e pátina esverdeada opcional (PATINA, desligada).
Saída: bronze_albedo.jpg (cinza neutro: o material tinge), bronze_normal.jpg (OpenGL) e
bronze_ms.png (R = metálico, A = suavidade).
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage
from scipy.spatial import cKDTree

OUT = sys.argv[1]
S = int(sys.argv[2]) if len(sys.argv) > 2 else 4096
TILE_MM = 500.0
PX = TILE_MM / S  # mm por pixel
rng = np.random.default_rng(2024)


def band_noise(lo, hi, seed):
    """Ruído repetível com frequências entre lo e hi ciclos por repetição (normalizado, média 0, desvio 1)."""
    r = np.random.default_rng(seed)
    w = r.standard_normal((S, S)).astype(np.float32)
    F = np.fft.rfft2(w)
    fy = np.fft.fftfreq(S) * S
    fx = np.fft.rfftfreq(S) * S
    f = np.sqrt(fy[:, None] ** 2 + fx[None, :] ** 2)
    filt = np.where((f >= lo) & (f <= hi), 1.0 / np.maximum(f, 1.0), 0.0)
    n = np.fft.irfft2(F * filt, s=(S, S)).astype(np.float32)
    return (n - n.mean()) / (n.std() + 1e-9)


def ss(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


# ------------------------------------------------------------------ marcas de martelo (amassados redondos sobrepostos)
ND = 11000
cent = rng.random((ND, 2))
rad = rng.uniform(0.0055, 0.0105, ND).astype(np.float32)  # em fração da repetição (2,7 a 5,2 mm)
dep = rng.uniform(0.5, 1.0, ND).astype(np.float32)
tree = cKDTree(cent, boxsize=1.0)
dimple = np.zeros(S * S, np.float32)
rows_per = max(1, 2_000_000 // S)
for r0 in range(0, S, rows_per):
    r1 = min(S, r0 + rows_per)
    ys, xs = np.mgrid[r0:r1, 0:S].astype(np.float32)
    q = np.stack([(ys.ravel() + 0.5) / S, (xs.ravel() + 0.5) / S], 1)
    d, idx = tree.query(q, k=10, workers=-1)
    t = np.clip(1.0 - (d / rad[idx]) ** 2, 0, 1)
    dimple[r0 * S:r1 * S] = -(dep[idx] * t * t).sum(1)
dimple = dimple.reshape(S, S)
dimple = ndimage.gaussian_filter(dimple, 1.5, mode="wrap")
dimple -= dimple.mean()
print("martelo ok")

# ------------------------------------------------------------------ riscos
def draw_scratches(n, len_mm, width, ang_mean, ang_sd, directional_frac, seed):
    r = np.random.default_rng(seed)
    im = Image.new("F", (S, S), 0.0)
    dr = ImageDraw.Draw(im)
    for k in range(n):
        x0, y0 = r.uniform(0, S, 2)
        L = r.uniform(*len_mm) / PX
        a = r.normal(ang_mean, ang_sd) if r.random() < directional_frac else r.uniform(0, np.pi)
        x1, y1 = x0 + np.cos(a) * L, y0 + np.sin(a) * L
        v = float(r.uniform(0.4, 1.0))
        for ox in (-S, 0, S):
            for oy in (-S, 0, S):
                if min(x0, x1) + ox > S or max(x0, x1) + ox < 0 or min(y0, y1) + oy > S or max(y0, y1) + oy < 0:
                    continue
                dr.line([(x0 + ox, y0 + oy), (x1 + ox, y1 + oy)], fill=v, width=width)
    return np.asarray(im, dtype=np.float32)


fine = draw_scratches(9000, (1.5, 18.0), 1, np.radians(25), np.radians(12), 0.7, 3)
fine = np.maximum(fine, draw_scratches(2500, (1.0, 8.0), 1, 0.0, 0.0, 0.0, 4))
deep = draw_scratches(300, (15.0, 70.0), 2, 0.0, 0.0, 0.0, 5)
gouge = draw_scratches(42, (18.0, 55.0), 4, 0.0, 0.0, 0.0, 9)   # golpes de lâmina: sulcos largos e fundos
fine = ndimage.gaussian_filter(fine, 0.55, mode="wrap")
deep = ndimage.gaussian_filter(deep, 0.8, mode="wrap")
gouge = ndimage.gaussian_filter(gouge, 1.3, mode="wrap")
print("riscos ok")

# ------------------------------------------------------------------ camadas largas
wave = band_noise(1, 6, 11)            # ondulação da chapa
mid = band_noise(6, 40, 12)            # variação de tom
smudge = ss(0.6, 1.6, band_noise(3, 18, 13))   # manchas de mão (menos brilho)
pat_n = band_noise(4, 30, 14) + 0.35 * band_noise(30, 140, 15)
PATINA = 0.0  # armadura de herói, bem cuidada; aumente (até 1) para envelhecer o bronze
patina = ss(2.1, 2.9, pat_n) * 0.7 * PATINA

# ------------------------------------------------------------------ altura (mm) e normal
h = 0.13 * dimple / (np.abs(dimple).max() + 1e-9) + 0.12 * wave * 0.25 - 0.012 * fine - 0.035 * deep - 0.08 * gouge
h = h.astype(np.float32)
dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) / (2 * PX)
dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) / (2 * PX)
n = np.stack([-dx, dy, np.ones_like(h)], -1)  # linhas descem na imagem = v diminui (OpenGL)
n /= np.linalg.norm(n, axis=-1, keepdims=True)
Image.fromarray(np.clip(n * 127.5 + 127.5, 0, 255).astype(np.uint8)).save(os.path.join(OUT, "bronze_normal.jpg"), quality=95, subsampling=0)
del n, dx, dy
print("normal ok")

# ------------------------------------------------------------------ cor (neutra; o material tinge) e brilho
lum = 0.93 * (1 + 0.035 * mid) * (1 - 0.06 * np.clip(dimple / (np.abs(dimple).max() + 1e-9), 0, 1))
lum = lum * (1 + 0.05 * fine) * (1 - 0.12 * deep) * (1 - 0.22 * gouge)
alb = np.repeat(lum[..., None], 3, axis=2)
pat_col = np.array([0.6, 0.68, 0.6], dtype=np.float32)
alb = alb * (1 - patina[..., None]) + pat_col * patina[..., None] * (0.85 + 0.15 * mid[..., None])
Image.fromarray(np.clip(alb * 255 + 0.5, 0, 255).astype(np.uint8)).save(os.path.join(OUT, "bronze_albedo.jpg"), quality=93, subsampling=0)
del alb
smooth = 0.88 - 0.03 * np.abs(wave) - 0.12 * fine - 0.25 * deep - 0.4 * gouge - 0.14 * smudge - 0.3 * patina
smooth = np.clip(smooth, 0.2, 0.93)
metal = np.clip(1.0 - 0.75 * patina, 0, 1)
rgba = np.zeros((S, S, 4), dtype=np.uint8)
m8 = np.clip(metal * 255 + 0.5, 0, 255).astype(np.uint8)
rgba[..., 0] = m8
rgba[..., 1] = m8
rgba[..., 2] = m8
rgba[..., 3] = np.clip(smooth * 255 + 0.5, 0, 255).astype(np.uint8)
Image.fromarray(rgba, "RGBA").save(os.path.join(OUT, "bronze_ms.png"), optimize=True)
print("ok", OUT, "pátina %.1f%%" % (100 * (patina > 0.5).mean()))
