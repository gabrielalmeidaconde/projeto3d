"""Pacote de troca Unity <-> Blender: um manifesto .json e um .bin com os blocos de números.

Tudo no arquivo está no sistema de eixos do Unity (x = direita do personagem, y = cima, z = frente,
mão esquerda). No Blender o personagem olha para -Y com Z para cima; u2b/b2u fazem a conversão.
Só usa numpy, para rodar tanto dentro do Blender quanto no Python do sistema.
"""
import json

import numpy as np

# Unity (x, y, z) -> Blender (-x, -z, y). É um espelhamento: a ordem dos triângulos inverte.
C = np.array([[-1.0, 0, 0, 0], [0, 0, -1, 0], [0, 1, 0, 0], [0, 0, 0, 1]])


def u2b(v):
    v = np.asarray(v, dtype=np.float64)
    return np.stack([-v[..., 0], -v[..., 2], v[..., 1]], -1)


def b2u(v):
    v = np.asarray(v, dtype=np.float64)
    return np.stack([-v[..., 0], v[..., 2], -v[..., 1]], -1)


def mat_u2b(m):
    return C @ np.asarray(m, dtype=np.float64).reshape(4, 4) @ C.T


def mat_b2u(m):
    return C.T @ np.asarray(m, dtype=np.float64).reshape(4, 4) @ C


def ler(caminho):
    """Lê <caminho>.json + <caminho>.bin. Devolve dict(ossos=[...], malhas=[dict com arrays numpy])."""
    with open(caminho + ".json", encoding="utf-8") as f:
        man = json.load(f)
    with open(caminho + ".bin", "rb") as f:
        buf = f.read()

    def arr(off, n, dt):
        if off is None or off < 0:
            return None
        return np.frombuffer(buf, dtype=dt, count=n, offset=off).copy()

    malhas = []
    for m in man["malhas"]:
        nv = m["nv"]
        d = dict(nome=m["nome"], rigida=bool(m["rigida"]), nv=nv, instancias=m.get("instancias") or [])
        d["pos"] = arr(m["pos"], nv * 3, "<f4").reshape(-1, 3)
        d["nor"] = arr(m["nor"], nv * 3, "<f4").reshape(-1, 3)
        for k, w in (("tan", 4), ("uv0", 2), ("uv1", 2), ("bw", 4)):
            a = arr(m.get(k, -1), nv * w, "<f4")
            d[k] = None if a is None else a.reshape(-1, w)
        a = arr(m.get("cor", -1), nv * 4, "u1")
        d["cor"] = None if a is None else a.reshape(-1, 4)
        a = arr(m.get("bi", -1), nv * 4, "<i4")
        d["bi"] = None if a is None else a.reshape(-1, 4)
        d["subs"] = [dict(material=s["material"], tri=arr(s["idx"], s["nt"] * 3, "<i4").reshape(-1, 3)) for s in m["subs"]]
        malhas.append(d)
    return dict(ossos=man["ossos"], malhas=malhas)


def escrever(caminho, ossos, malhas):
    """Grava o pacote. Cada malha: dict(nome, rigida, pos, nor, [tan, uv0, uv1, cor, bi, bw], subs=[dict(material, tri)], instancias)."""
    man = dict(versao=1, ossos=ossos, malhas=[])
    with open(caminho + ".bin", "wb") as f:
        def put(a, dt):
            at = f.tell()
            f.write(np.ascontiguousarray(a, dtype=dt).tobytes())
            return at

        for m in malhas:
            nv = len(m["pos"])
            e = dict(nome=m["nome"], rigida=bool(m.get("rigida", False)), nv=nv,
                     pos=put(m["pos"], "<f4"), nor=put(m["nor"], "<f4"), tan=-1, uv0=-1, uv1=-1, cor=-1, bi=-1, bw=-1)
            for k, dt in (("tan", "<f4"), ("uv0", "<f4"), ("uv1", "<f4"), ("cor", "u1"), ("bi", "<i4"), ("bw", "<f4")):
                if m.get(k) is not None:
                    assert len(m[k]) == nv, (m["nome"], k, len(m[k]), nv)
                    e[k] = put(m[k], dt)
            e["subs"] = [dict(material=s["material"], idx=put(s["tri"], "<i4"), nt=int(len(s["tri"]))) for s in m["subs"]]
            e["instancias"] = m.get("instancias") or []
            man["malhas"].append(e)
    with open(caminho + ".json", "w", encoding="utf-8") as f:
        json.dump(man, f)
