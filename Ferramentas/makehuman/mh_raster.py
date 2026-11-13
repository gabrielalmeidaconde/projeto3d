"""Rasterizador simples em numpy: prévias de formato (mh_preview.py) e "bake" de atributos no espaço UV (mh_skin.py)."""
import numpy as np


def _raster(px, py, depth, tris, W, H, chunk=200000):
    """px,py: posição em pixels dos vértices; depth: profundidade (menor = mais perto).
    Retorna (tri_id[H*W], b0, b1, b2) com -1 onde não há triângulo. Baricêntricas em espaço de tela."""
    a, b, c = tris[:, 0], tris[:, 1], tris[:, 2]
    x0, y0, x1, y1, x2, y2 = px[a], py[a], px[b], py[b], px[c], py[c]
    xmin = np.clip(np.floor(np.minimum(np.minimum(x0, x1), x2)), 0, W - 1).astype(np.int64)
    xmax = np.clip(np.ceil(np.maximum(np.maximum(x0, x1), x2)), 0, W - 1).astype(np.int64)
    ymin = np.clip(np.floor(np.minimum(np.minimum(y0, y1), y2)), 0, H - 1).astype(np.int64)
    ymax = np.clip(np.ceil(np.maximum(np.maximum(y0, y1), y2)), 0, H - 1).astype(np.int64)
    bw = xmax - xmin + 1
    bh = ymax - ymin + 1
    area = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0)
    ok = (np.abs(area) > 1e-12) & (xmax >= xmin) & (ymax >= ymin)
    zbuf = np.full(H * W, np.inf)
    tid = np.full(H * W, -1, dtype=np.int64)
    bary = np.zeros((H * W, 3))
    ids_all = np.nonzero(ok)[0]
    # agrupa por tamanho da caixa para vetorizar
    sizes = np.stack([bw[ids_all], bh[ids_all]], 1)
    keys = np.unique(sizes, axis=0)
    for sw, sh in keys:
        sel = ids_all[(sizes[:, 0] == sw) & (sizes[:, 1] == sh)]
        npx = sw * sh
        step = max(1, chunk // npx)
        gx, gy = np.meshgrid(np.arange(sw), np.arange(sh))
        gx = gx.ravel()
        gy = gy.ravel()
        for s in range(0, len(sel), step):
            t = sel[s:s + step]
            X = xmin[t, None] + gx[None]
            Y = ymin[t, None] + gy[None]
            cx = X + 0.5
            cy = Y + 0.5
            ar = area[t, None]
            w0 = ((x1[t, None] - cx) * (y2[t, None] - cy) - (x2[t, None] - cx) * (y1[t, None] - cy)) / ar
            w1 = ((x2[t, None] - cx) * (y0[t, None] - cy) - (x0[t, None] - cx) * (y2[t, None] - cy)) / ar
            w2 = 1 - w0 - w1
            inside = (w0 >= -1e-6) & (w1 >= -1e-6) & (w2 >= -1e-6) & (X < W) & (Y < H)
            if not inside.any():
                continue
            ti = np.broadcast_to(t[:, None], X.shape)[inside]
            pix = (Y * W + X)[inside]
            w0 = w0[inside]
            w1 = w1[inside]
            w2 = w2[inside]
            z = w0 * depth[a[ti]] + w1 * depth[b[ti]] + w2 * depth[c[ti]]
            order = np.lexsort((z, pix))
            pix, z, ti, w0, w1, w2 = pix[order], z[order], ti[order], w0[order], w1[order], w2[order]
            first = np.ones(len(pix), bool)
            first[1:] = pix[1:] != pix[:-1]
            pix, z, ti, w0, w1, w2 = pix[first], z[first], ti[first], w0[first], w1[first], w2[first]
            win = z < zbuf[pix]
            pix = pix[win]
            zbuf[pix] = z[win]
            tid[pix] = ti[win]
            bary[pix] = np.stack([w0[win], w1[win], w2[win]], 1)
    return tid, bary, zbuf


def look_at(eye, target, up=(0, 1, 0)):
    eye, target, up = (np.asarray(x, dtype=np.float64) for x in (eye, target, up))
    f = target - eye
    f /= np.linalg.norm(f)
    r = np.cross(f, up)
    r /= np.linalg.norm(r)
    u = np.cross(r, f)
    return eye, r, u, f


def render(objs, cam, W=600, H=800, fov=30.0, bg=(0.16, 0.17, 0.2), lights=None):
    """Objetos com "alpha": (tex RGBA, uv) são desenhados depois, misturados por cima dos opacos."""
    opaque = [o for o in objs if "alpha" not in o]
    trans = [o for o in objs if "alpha" in o]
    img, zb = _render(opaque, cam, W, H, fov, bg, lights)
    for o in trans:
        oc, oz, oa = _render([o], cam, W, H, fov, bg, lights, want_alpha=True)
        a = (oa * (oz < zb - 1e-4))[..., None]
        img = img * (1 - a) + oc * a
    return img


def _render(objs, cam, W, H, fov, bg, lights, want_alpha=False):
    """objs: lista de dicts {P (n,3), N (n,3), T (m,3), color (3,) | per-vertex (n,3) | (tex, uv)}.
    cam: (eye, target). Câmera em perspectiva; mesmo sistema de coordenadas dos vértices (Unity: Y para cima).
    Retorna imagem float (H,W,3)."""
    eye, r, u, f = look_at(*cam)
    fpx = 0.5 * H / np.tan(np.radians(fov) * 0.5)
    allP, allT, offs = [], [], 0
    for o in objs:
        allP.append(o["P"])
        allT.append(o["T"] + offs)
        offs += len(o["P"])
    P = np.concatenate(allP)
    T = np.concatenate(allT)
    rel = P - eye
    xc, yc, zc = rel @ r, rel @ u, rel @ f
    zc = np.maximum(zc, 1e-4)
    px = W * 0.5 + fpx * xc / zc
    py = H * 0.5 - fpx * yc / zc
    tid, bary, zbuf = _raster(px, py, zc, T, W, H)
    img = np.tile(np.array(bg, dtype=np.float64), (H * W, 1))
    alpha = np.zeros(H * W)
    hit = tid >= 0
    if not hit.any():
        return (img.reshape(H, W, 3), zbuf.reshape(H, W), alpha.reshape(H, W)) if want_alpha else (img.reshape(H, W, 3), zbuf.reshape(H, W))
    t = tid[hit]
    bw = bary[hit]
    # correção de perspectiva
    iz = 1.0 / zc[T[t]]
    bw = bw * iz
    bw /= bw.sum(1, keepdims=True)
    lights = lights or [((-0.5, 0.6, 0.8), (1.0, 0.95, 0.88), 1.0),
                        ((0.7, 0.2, 0.3), (0.55, 0.62, 0.75), 0.45),
                        ((0.2, 0.5, -1.0), (1.0, 0.9, 0.8), 0.6)]
    # monta cor e normal por objeto
    col = np.zeros((len(t), 3))
    nrm = np.zeros((len(t), 3))
    start = 0
    for o in objs:
        n = len(o["T"])
        sel = (t >= start) & (t < start + n)
        if sel.any():
            lt = t[sel] - start
            tri = o["T"][lt]
            b = bw[sel]
            N = o["N"]
            nn = N[tri[:, 0]] * b[:, :1] + N[tri[:, 1]] * b[:, 1:2] + N[tri[:, 2]] * b[:, 2:3]
            if "nmap" in o:
                ntex, nuv, TT, ww = o["nmap"]
                uu = nuv[tri[:, 0]] * b[:, :1] + nuv[tri[:, 1]] * b[:, 1:2] + nuv[tri[:, 2]] * b[:, 2:3]
                th, tw = ntex.shape[:2]
                ix = np.clip((uu[:, 0] % 1.0) * tw, 0, tw - 1).astype(int)
                iy = np.clip((1 - uu[:, 1] % 1.0) * th, 0, th - 1).astype(int)
                nt = ntex[iy, ix, :3] * 2 - 1
                tt = TT[tri[:, 0]] * b[:, :1] + TT[tri[:, 1]] * b[:, 1:2] + TT[tri[:, 2]] * b[:, 2:3]
                nn = nn / np.maximum(np.linalg.norm(nn, axis=1, keepdims=True), 1e-9)
                tt = tt - nn * (tt * nn).sum(1, keepdims=True)
                tt /= np.maximum(np.linalg.norm(tt, axis=1, keepdims=True), 1e-9)
                bb = np.cross(nn, tt) * ww[tri[:, 0]][:, None]
                nn = tt * nt[:, :1] + bb * nt[:, 1:2] + nn * nt[:, 2:3]
            nrm[sel] = nn
            if "alpha" in o:
                atex, auv = o["alpha"]
                uu = auv[tri[:, 0]] * b[:, :1] + auv[tri[:, 1]] * b[:, 1:2] + auv[tri[:, 2]] * b[:, 2:3]
                th, tw = atex.shape[:2]
                ix = np.clip((uu[:, 0] % 1.0) * tw, 0, tw - 1).astype(int)
                iy = np.clip((1 - uu[:, 1] % 1.0) * th, 0, th - 1).astype(int)
                idx = np.nonzero(hit)[0][sel]
                alpha[idx] = atex[iy, ix, 3]
            c = o.get("color", (0.75, 0.72, 0.7))
            if isinstance(c, tuple) and len(c) == 2 and hasattr(c[0], "shape") and c[0].ndim == 3:
                tex, uv = c
                uu = uv[tri[:, 0]] * b[:, :1] + uv[tri[:, 1]] * b[:, 1:2] + uv[tri[:, 2]] * b[:, 2:3]
                th, tw = tex.shape[:2]
                ix = np.clip((uu[:, 0] % 1.0) * tw, 0, tw - 1).astype(int)
                iy = np.clip((1 - uu[:, 1] % 1.0) * th, 0, th - 1).astype(int)
                col[sel] = tex[iy, ix, :3]
            else:
                c = np.asarray(c, dtype=np.float64)
                if c.ndim == 2:
                    col[sel] = c[tri[:, 0]] * b[:, :1] + c[tri[:, 1]] * b[:, 1:2] + c[tri[:, 2]] * b[:, 2:3]
                else:
                    col[sel] = c
        start += n
    nrm /= np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), 1e-9)
    # luzes definidas em espaço de câmera (x direita, y cima, z para a câmera)
    view = -(f)
    shade = np.zeros((len(t), 3)) + 0.12
    for d, c, k in lights:
        d = np.asarray(d, dtype=np.float64)
        dw = d[0] * r + d[1] * u + d[2] * view
        dw /= np.linalg.norm(dw)
        ndl = np.clip(nrm @ dw, 0, 1)
        shade += np.outer(ndl, np.asarray(c)) * k
    # especular leve
    hv = (np.array(lights[0][0])[0] * r + np.array(lights[0][0])[1] * u + np.array(lights[0][0])[2] * view)
    hv = hv / np.linalg.norm(hv) + view
    hv /= np.linalg.norm(hv)
    spec = np.clip(nrm @ hv, 0, 1) ** 40 * 0.15
    out = col * shade + spec[:, None]
    img[hit] = out
    img = np.clip(img.reshape(H, W, 3), 0, 1)
    if want_alpha:
        return img, zbuf.reshape(H, W), alpha.reshape(H, W)
    return img, zbuf.reshape(H, W)


def bake_uv(uv, tris, values, size, dilate=8):
    """Rasteriza valores por vértice (n,k) no espaço UV (size x size). Preenche bordas por vizinho mais próximo."""
    from scipy import ndimage
    px = uv[:, 0] * size
    py = (1 - uv[:, 1]) * size
    tid, bary, _ = _raster(px, py, np.zeros(len(uv)), tris, size, size)
    k = values.shape[1]
    out = np.zeros((size * size, k))
    hit = tid >= 0
    tri = tris[tid[hit]]
    b = bary[hit]
    out[hit] = values[tri[:, 0]] * b[:, :1] + values[tri[:, 1]] * b[:, 1:2] + values[tri[:, 2]] * b[:, 2:3]
    out = out.reshape(size, size, k)
    mask = hit.reshape(size, size)
    if dilate:
        _, (iy, ix) = ndimage.distance_transform_edt(~mask, return_indices=True)
        out = out[iy, ix]
    return out, mask
