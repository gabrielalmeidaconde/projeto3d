"""Textura de sujeira de batalha (repetível, RGBA linear) para os shaders Troia/Personagem e Troia/Pele.

Uso: python3 -I sujeira.py <arquivo_saida.png> [tamanho]

Canais (cada um é uma máscara 0..1; o shader combina com as máscaras assadas nos vértices):
  R  manchas largas (2 a 15 cm) com borda irregular
  G  escorridos verticais (suor, água suja, sangue que desceu)
  B  respingos: pontos de 1 a 6 mm, alguns com gotas satélites e rastro
  A  poeira fina (grão de ~1 mm)
Uma repetição cobre ~0,3 a 0,5 m da peça (as UVs das peças são em metros).
"""
import sys

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

OUT = sys.argv[1]
S = int(sys.argv[2]) if len(sys.argv) > 2 else 1024


def band(lo, hi, seed, ay=1.0, ax=1.0):
    """Ruído repetível com frequências entre lo e hi ciclos por repetição. ay/ax > 1 alongam no eixo y/x."""
    r = np.random.default_rng(seed)
    w = r.standard_normal((S, S)).astype(np.float32)
    F = np.fft.rfft2(w)
    fy = np.fft.fftfreq(S) * S * ay
    fx = np.fft.rfftfreq(S) * S * ax
    f = np.sqrt(fy[:, None] ** 2 + fx[None, :] ** 2)
    filt = np.where((f >= lo) & (f <= hi), 1.0 / np.maximum(f, 1.0), 0.0)
    n = np.fft.irfft2(F * filt, s=(S, S)).astype(np.float32)
    return (n - n.mean()) / (n.std() + 1e-9)


def ss(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


# ------------------------------------------------------------------ R: manchas
blot = band(2, 10, 1) * 0.8 + band(10, 48, 2) * 0.4 + band(48, 160, 3) * 0.12
blot = ss(-1.1, 1.3, blot)

# ------------------------------------------------------------------ G: escorridos (longos na vertical)
streak = band(10, 110, 4, ay=7.0) * 0.8 + band(40, 260, 5, ay=10.0) * 0.4
streak = ss(0.35, 1.6, streak) * (0.55 + 0.45 * ss(-1.0, 1.0, band(2, 8, 6)))

# ------------------------------------------------------------------ B: respingos
rng = np.random.default_rng(42)
im = Image.new("F", (S, S), 0.0)
dr = ImageDraw.Draw(im)


def dot(x, y, r, v):
    for ox in (-S, 0, S):
        for oy in (-S, 0, S):
            if x + ox + r < 0 or x + ox - r > S or y + oy + r < 0 or y + oy - r > S:
                continue
            dr.ellipse([x + ox - r, y + oy - r, x + ox + r, y + oy + r], fill=v)


scale = S / 1024.0
# agrupados: cada "golpe" espalha gotas em leque
for _ in range(34):
    cx, cy = rng.uniform(0, S, 2)
    ang = rng.uniform(0, 2 * np.pi)
    spread = rng.uniform(0.25, 0.9)
    n = int(rng.integers(18, 70))
    for _ in range(n):
        d = abs(rng.normal(0, 70 * scale)) + 4 * scale
        a = ang + rng.normal(0, spread)
        r = max(0.8, rng.gamma(2.0, 1.6) * scale) * (1.0 - 0.5 * min(d / (220 * scale), 1.0))
        x, y = cx + np.cos(a) * d, cy + np.sin(a) * d
        v = float(rng.uniform(0.6, 1.0))
        dot(x, y, r, v)
        # rastro curto na direção do respingo
        if r > 2.2 * scale and rng.random() < 0.5:
            for k in range(1, 5):
                dot(x + np.cos(a) * r * 0.9 * k, y + np.sin(a) * r * 0.9 * k, r * (1 - 0.2 * k), v * (1 - 0.15 * k))
    # mancha central do golpe
    for _ in range(int(rng.integers(2, 6))):
        dot(cx + rng.normal(0, 9 * scale), cy + rng.normal(0, 9 * scale), rng.uniform(5, 15) * scale, 1.0)
# pontos soltos
for _ in range(1400):
    x, y = rng.uniform(0, S, 2)
    dot(x, y, max(0.7, rng.gamma(1.5, 1.0) * scale), float(rng.uniform(0.5, 1.0)))
spat = np.asarray(im, dtype=np.float32)
spat = ndimage.gaussian_filter(spat, 0.7, mode="wrap")
spat = np.clip(spat, 0, 1)

# ------------------------------------------------------------------ A: poeira fina
dust = ss(-1.6, 1.6, band(70, 420, 7) * 0.8 + band(20, 70, 8) * 0.45)

rgba = np.stack([blot, streak, spat, dust], -1)
Image.fromarray(np.clip(rgba * 255 + 0.5, 0, 255).astype(np.uint8), "RGBA").save(OUT)
print("ok", OUT, "médias R G B A:", [round(float(c.mean()), 3) for c in (blot, streak, spat, dust)],
      "| respingo cobre %.1f%%" % (100 * float((spat > 0.4).mean())))
