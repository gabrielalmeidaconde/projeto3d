"""Prepara as texturas escaneadas do Poly Haven (CC0) para os materiais do Pátroclo (4K).

Uso: python3 -I prep_polyhaven.py <pasta_downloads> <pasta_saida>

<pasta_downloads> deve conter os JPG 4k baixados de https://polyhaven.com
(<asset>_{diff,rough,nor_gl}_4k.jpg) para brown_leather, rough_linen, caban e
monastery_stone_floor, e o HDRI brown_photostudio_02_2k.hdr.

Saída, por material: <nome>_albedo.jpg e <nome>_normal.jpg (4096) e <nome>_ms.png (2048;
R = metálico, A = suavidade = 1 - rugosidade, no formato do URP Lit).
Linho e lã saem com cor neutra para o painel de design tingir pelo material.
O bronze não vem daqui: é gerado por bronze_martelado.py.
"""
import os
import shutil
import sys

import numpy as np
from PIL import Image

SRC, OUT = sys.argv[1], sys.argv[2]
os.makedirs(OUT, exist_ok=True)
SIZE = 4096


def load(name, kind):
    path = next(os.path.join(SRC, f) for f in os.listdir(SRC) if f.startswith(f"{name}_{kind}") and f.endswith(".jpg"))
    im = Image.open(path).convert("RGB")
    if im.size != (SIZE, SIZE):
        im = im.resize((SIZE, SIZE), Image.LANCZOS)
    return np.asarray(im).astype(np.float32) / 255.0


def save_rgb(arr, path, quality=93):
    Image.fromarray(np.clip(arr * 255 + 0.5, 0, 255).astype(np.uint8)).save(path, quality=quality, subsampling=0)


def save_ms(metal, smooth, path, size=2048):
    h, w = smooth.shape
    rgba = np.zeros((h, w, 4), dtype=np.float32)
    rgba[..., 0] = metal
    rgba[..., 1] = metal
    rgba[..., 2] = metal
    rgba[..., 3] = smooth
    img = Image.fromarray(np.clip(rgba * 255 + 0.5, 0, 255).astype(np.uint8), "RGBA")
    img.resize((size, size), Image.LANCZOS).save(path)


def luminance(c):
    return c[..., 0] * 0.2126 + c[..., 1] * 0.7152 + c[..., 2] * 0.0722


def neutral(c, mean, contrast):
    """Tira a cor e deixa só a variação de brilho, com média `mean`."""
    lum = luminance(c)
    lum = lum / lum.mean()
    g = mean * np.power(lum, contrast)
    return np.repeat(g[..., None], 3, axis=2)


def copy_normal(name, out):
    save_rgb(load(name, "nor_gl"), os.path.join(OUT, f"{out}_normal.jpg"), quality=95)


# ------------------------------------------------------------------ couro (mantém a cor marrom original)
diff = load("brown_leather", "diff")
rough = luminance(load("brown_leather", "rough"))
# clareia: o couro escaneado é escuro demais para uma cena noturna
save_rgb(np.clip(diff * 1.45, 0, 1), os.path.join(OUT, "couro_albedo.jpg"))
save_ms(np.zeros_like(rough), np.clip(1 - rough, 0, 1) * 0.9, os.path.join(OUT, "couro_ms.png"))
copy_normal("brown_leather", "couro")

# ------------------------------------------------------------------ linho e lã (cor neutra para tingir)
for src, out, mean, contrast in (("rough_linen", "linho", 0.92, 1.4), ("caban", "la", 0.8, 1.6)):
    diff = load(src, "diff")
    rough = luminance(load(src, "rough"))
    save_rgb(neutral(diff, mean, contrast), os.path.join(OUT, f"{out}_albedo.jpg"))
    save_ms(np.zeros_like(rough), np.clip(1 - rough, 0, 1), os.path.join(OUT, f"{out}_ms.png"))
    copy_normal(src, out)

# ------------------------------------------------------------------ pedra do pedestal (cor original, um pouco mais quente)
diff = load("monastery_stone_floor", "diff")
rough = luminance(load("monastery_stone_floor", "rough"))
save_rgb(np.clip(diff * np.array([1.06, 1.0, 0.92]), 0, 1), os.path.join(OUT, "pedra_albedo.jpg"))
save_ms(np.zeros_like(rough), np.clip(1 - rough, 0, 1) * 0.8, os.path.join(OUT, "pedra_ms.png"))
copy_normal("monastery_stone_floor", "pedra")

# ------------------------------------------------------------------ HDRI de estúdio (só reflexos da vitrine)
shutil.copyfile(os.path.join(SRC, "brown_photostudio_02_2k.hdr"), os.path.join(OUT, "estudio_reflexo.hdr"))

with open(os.path.join(OUT, "LICENCAS.txt"), "w") as f:
    f.write(
        "Texturas derivadas de materiais do Poly Haven (polyhaven.com), licença CC0, em 4k:\n"
        "couro <- brown_leather; linho <- rough_linen; la <- caban; pedra <- monastery_stone_floor.\n"
        "estudio_reflexo.hdr <- HDRI brown_photostudio_02 (2k), usado só nos reflexos da vitrine.\n"
        "bronze_* é procedural (Ferramentas/texturas/bronze_martelado.py), sem asset de terceiros.\n"
        "Geradas por Ferramentas/texturas/prep_polyhaven.py. O canal A de *_ms.png é 1 - rugosidade.\n"
    )
print("ok", sorted(os.listdir(OUT)))
