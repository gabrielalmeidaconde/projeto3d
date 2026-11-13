"""Copia as texturas de cabelo, sobrancelha e cílios do pacote CC0 do MakeHuman para o projeto.
Sobrancelhas e cílios vêm quase pretos; aqui ganham cor castanha escura (com variação por fio),
mantendo o alfa, para o material não precisar tingir preto (o que não clareia).

Uso: python3 -I mh_pelos.py <pasta_pack> <pasta_saida>
"""
import os
import shutil
import sys

import numpy as np
from PIL import Image


def recolor(src, dst, color, alpha_gain=1.0):
    a = np.asarray(Image.open(src).convert("RGBA"), dtype=np.float64) / 255
    lum = a[..., :3].mean(2)
    var = 0.75 + 0.5 * np.clip(lum / max(lum[a[..., 3] > 0.3].mean(), 1e-3) - 0.5, 0, 1)
    rng = np.random.default_rng(5)
    var *= 0.9 + 0.2 * rng.random(lum.shape)
    rgb = np.clip(np.array(color)[None, None, :] * var[..., None], 0, 1)
    out = np.concatenate([rgb, np.clip(a[..., 3:] * alpha_gain, 0, 1)], 2)
    Image.fromarray((out * 255 + 0.5).astype(np.uint8)).save(dst)


def main():
    pack, out = sys.argv[1], sys.argv[2]
    shutil.copyfile(os.path.join(pack, "hair/short04/short04_diffuse.png"), os.path.join(out, "cabelo_diffuse.png"))
    recolor(os.path.join(pack, "eyebrows/eyebrow001/eyebrow001.png"), os.path.join(out, "sobrancelha.png"), (0.12, 0.085, 0.06), 1.7)
    recolor(os.path.join(pack, "eyelashes/eyelashes01/eyelashes01.png"), os.path.join(out, "cilios.png"), (0.07, 0.05, 0.04), 1.1)
    print("ok", out)


if __name__ == "__main__":
    main()
