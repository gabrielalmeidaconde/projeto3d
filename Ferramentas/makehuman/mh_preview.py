"""Prévia rápida (sem Unity) do corpo e do rosto gerados por mh_build.py.

Uso: python3 -I mh_preview.py <pasta_dl> <saida.png> [pele.png] [--old]
Gera uma folha com corpo (frente, 3/4, perfil, costas) e rosto (frente, 3/4, perfil).
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mh_build  # noqa: E402
import mh_raster  # noqa: E402

OLD = dict(
    macro=dict(muscle=0.925, weight=0.5, height=0.75, proportions=0.8,
               ethnic={"caucasian": 0.72, "african": 0.16, "asian": 0.12}),
    body=[("torso/torso-vshape-incr", 0.7), ("torso/torso-muscle-pectoral-incr", 0.55),
          ("torso/torso-muscle-dorsi-incr", 0.6), ("torso/torso-scale-horiz-incr", 0.25),
          ("armslegs/l-upperarm-muscle-incr", 0.6), ("armslegs/r-upperarm-muscle-incr", 0.6),
          ("armslegs/l-lowerarm-muscle-incr", 0.5), ("armslegs/r-lowerarm-muscle-incr", 0.5),
          ("armslegs/l-upperleg-muscle-incr", 0.45), ("armslegs/r-upperleg-muscle-incr", 0.45),
          ("armslegs/l-lowerleg-muscle-incr", 0.45), ("armslegs/r-lowerleg-muscle-incr", 0.45)],
    face=[], subdivisions=0,
    proxies=[("eyes/high-poly", "high-poly", "olhos", "Olhos"),
             ("eyebrows/eyebrow010", "eyebrow010", "sobrancelha", "Sobrancelhas")])


def objs_from(res, skin=None, only=None):
    objs = []
    for m in res["meshes"]:
        if m["material"] == "ajuste" or (only and m["name"] not in only):
            continue
        o = {"P": m["vertices"], "N": m["normals"], "T": m["triangles"].reshape(-1, 3)}
        if m["name"] == "Corpo":
            o["color"] = (skin, m["uvs"]) if skin is not None else (0.78, 0.74, 0.7)
        elif m["name"] == "Olhos":
            o["color"] = (0.9, 0.9, 0.9)
        else:
            o["color"] = (0.12, 0.09, 0.07)
        objs.append(o)
    return objs


def sheet(res, skin=None, label=""):
    objs = objs_from(res, skin)
    h = res["height"]
    head = res["bones"][res["bone_index"]["head"]]["head"]
    fc = np.array([0.0, head[1] + 0.09, head[2] + 0.02])
    views = []
    for ang in (0, 35, 90, 180):
        a = np.radians(ang)
        eye = np.array([np.sin(a) * 5.2, h * 0.55, np.cos(a) * 5.2])
        views.append(mh_raster.render(objs, (eye, (0, h * 0.53, 0)), W=380, H=760, fov=22))
    faces = []
    for ang in (0, 30, 90):
        a = np.radians(ang)
        eye = fc + np.array([np.sin(a) * 0.95, 0.02, np.cos(a) * 0.95])
        faces.append(mh_raster.render(objs, (eye, fc), W=380, H=460, fov=18))
    top = np.concatenate(views, 1)
    bot = np.concatenate(faces + [np.zeros((460, 380, 3))], 1)
    img = np.concatenate([top, bot], 0)
    return Image.fromarray((img * 255).astype(np.uint8))


def main():
    dl, out = sys.argv[1], sys.argv[2]
    args = [a for a in sys.argv[3:] if not a.startswith("--")]
    skin = None
    if args:
        skin = np.asarray(Image.open(args[0]).convert("RGB"), dtype=np.float64) / 255.0
    kw = OLD if "--old" in sys.argv else {}
    res = mh_build.build(dl, 1.83, **kw, verbose=False)
    sheet(res, skin).save(out)
    print("ok", out)


if __name__ == "__main__":
    main()
