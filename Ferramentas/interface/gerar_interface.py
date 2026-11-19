#!/usr/bin/env python3
"""
Arte da interface (HUD e menus): molduras de bronze e ouro, greca, medalhões, barras, ícones das armas, louros.
Tudo desenhado aqui em 4x e reduzido (bordas lisas). O sufixo _bN no nome do arquivo é a borda do 9-slice em
pixels; quem configura a importação no Unity é Assets/Troia/Scripts/Editor/ImportarInterface.cs.

Uso (na raiz do projeto): python3 -I Ferramentas/interface/gerar_interface.py
Grava em Assets/Troia/Resources/Troia/UI e uma prancha com tudo em Capturas/Interface/prancha.png.
"""
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SAIDA = os.path.join(RAIZ, 'Assets', 'Troia', 'Resources', 'Troia', 'UI')
PRANCHA = os.path.join(RAIZ, 'Capturas', 'Interface')
S = 4  # superamostragem

OURO_CLARO = (246, 219, 150)
OURO = (214, 168, 78)
BRONZE = (140, 92, 44)
BRONZE_ESCURO = (66, 42, 20)
PEDRA_ALTO = (30, 25, 19)
PEDRA_BAIXO = (10, 8, 6)

rng = np.random.default_rng(1184)
gerados = []


def tela(w, h):
    return Image.new('RGBA', (w * S, h * S), (0, 0, 0, 0))


def salvar(img, nome):
    w, h = img.size[0] // S, img.size[1] // S
    final = img.resize((w, h), Image.LANCZOS)
    final.save(os.path.join(SAIDA, nome + '.png'))
    gerados.append((nome, final))
    return final


def gradiente_vertical(w, h, topo, base, alfa=255):
    t = np.linspace(0, 1, h)[:, None, None]
    a = np.array(topo, float)[None, None, :]
    b = np.array(base, float)[None, None, :]
    rgb = a + (b - a) * t
    rgb = np.repeat(rgb, w, axis=1)
    alpha = np.full((h, w, 1), alfa, float)
    return Image.fromarray(np.concatenate([rgb, alpha], axis=2).astype(np.uint8), 'RGBA')


def ruido_pedra(img, forca=10):
    """Granulado e manchas suaves: a placa parece pedra/bronze gasto, não um retângulo chapado."""
    a = np.array(img).astype(float)
    h, w = a.shape[:2]
    fino = rng.normal(0, forca, (h, w))
    manchas = np.array(Image.fromarray((rng.random((h // 16 + 2, w // 16 + 2)) * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)).astype(float)
    manchas = (manchas - 128) / 128 * forca * 1.2
    for c in range(3):
        a[..., c] = np.clip(a[..., c] + fino + manchas, 0, 255)
    return Image.fromarray(a.astype(np.uint8), 'RGBA')


def mascara_retangulo(w, h, inset, raio):
    m = Image.new('L', (w, h), 0)
    ImageDraw.Draw(m).rounded_rectangle([inset, inset, w - 1 - inset, h - 1 - inset], raio, fill=255)
    return m


def losango(d, cx, cy, r, cor, contorno=None):
    pts = [(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)]
    d.polygon(pts, fill=cor, outline=contorno)


def moldura(img, inset, largura, cor, raio=0):
    d = ImageDraw.Draw(img)
    w, h = img.size
    d.rounded_rectangle([inset, inset, w - 1 - inset, h - 1 - inset], raio, outline=cor, width=largura)


def painel(nome, w, h, borda, cantos=True, alfa=236, interno=True):
    W, H = w * S, h * S
    fundo = ruido_pedra(gradiente_vertical(W, H, PEDRA_ALTO, PEDRA_BAIXO, alfa), 7)
    mask = mascara_retangulo(W, H, 2 * S, 3 * S)
    img = tela(w, h)
    img.paste(fundo, (0, 0), mask)
    # sombra interna: escurece as bordas por dentro
    sombra = Image.new('L', (W, H), 0)
    ImageDraw.Draw(sombra).rounded_rectangle([10 * S, 10 * S, W - 10 * S, H - 10 * S], 6 * S, fill=255)
    sombra = sombra.filter(ImageFilter.GaussianBlur(7 * S))
    escuro = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    a = (255 - np.array(sombra)).astype(float) * 0.5 * (np.array(mask) / 255)
    escuro.putalpha(Image.fromarray(a.astype(np.uint8)))
    img = Image.alpha_composite(img, escuro)
    # filetes: ouro por fora, bronze por dentro
    moldura(img, 3 * S, 2 * S, OURO + (255,), 3 * S)
    if interno:
        moldura(img, 8 * S, S, BRONZE + (220,), 2 * S)
    if cantos:
        d = ImageDraw.Draw(img)
        for cx in (8 * S, W - 1 - 8 * S):
            for cy in (8 * S, H - 1 - 8 * S):
                losango(d, cx, cy, 5 * S, OURO_CLARO + (255,), BRONZE_ESCURO + (255,))
                losango(d, cx, cy, 2 * S, BRONZE_ESCURO + (255,))
    return salvar(img, f'{nome}_b{borda}')


def greca(nome='Greca', w=48, h=16):
    """Meandro grego em faixa contínua (repete na horizontal)."""
    img = tela(w, h)
    d = ImageDraw.Draw(img)
    u = S * 2  # unidade
    lw = int(1.6 * S)
    cor = OURO + (255,)
    y0, y1 = 2 * S, h * S - 2 * S
    d.line([(0, y0), (w * S, y0)], fill=cor, width=lw)
    d.line([(0, y1), (w * S, y1)], fill=cor, width=lw)
    # um módulo do meandro por 24 px
    for m in range(w // 24):
        x = m * 24 * S
        pts = [(x + 3 * u, y1 - u), (x + 3 * u, y0 + u), (x + 9 * u, y0 + u), (x + 9 * u, y1 - 2.5 * u),
               (x + 5.5 * u, y1 - 2.5 * u), (x + 5.5 * u, y0 + 2.6 * u), (x + 7 * u, y0 + 2.6 * u)]
        d.line(pts, fill=cor, width=lw, joint='curve')
        d.line([(x, y1 - u), (x + 3 * u, y1 - u)], fill=cor, width=lw)
        d.line([(x + 9 * u, y1 - u), (x + 12 * u, y1 - u)], fill=cor, width=lw)
    return salvar(img, nome)


def divisor(nome='Divisor', w=512, h=24):
    img = tela(w, h)
    W, H = w * S, h * S
    cy = H // 2
    linha = np.zeros((H, W), float)
    x = np.linspace(-1, 1, W)
    fade = np.clip(1 - np.abs(x) ** 2.2, 0, 1)
    linha[cy - S // 2:cy + S // 2 + 1, :] = fade
    rgba = np.zeros((H, W, 4), np.uint8)
    rgba[..., 0], rgba[..., 1], rgba[..., 2] = OURO
    rgba[..., 3] = (linha * 255).astype(np.uint8)
    img = Image.fromarray(rgba, 'RGBA')
    d = ImageDraw.Draw(img)
    losango(d, W // 2, cy, 8 * S, OURO_CLARO + (255,), BRONZE_ESCURO + (255,))
    losango(d, W // 2, cy, 3 * S, BRONZE_ESCURO + (255,))
    for k in (1, -1):
        losango(d, W // 2 + k * 20 * S, cy, 3 * S, OURO + (255,))
        losango(d, W // 2 + k * 32 * S, cy, 2 * S, OURO + (200,))
    return salvar(img, nome)


def barra_moldura(nome='Barra_Moldura', w=96, h=20, borda=9):
    img = tela(w, h)
    d = ImageDraw.Draw(img)
    W, H = w * S, h * S
    d.rounded_rectangle([S, S, W - S, H - S], 3 * S, fill=(6, 5, 4, 235), outline=BRONZE + (255,), width=int(1.5 * S))
    d.rounded_rectangle([3 * S, 3 * S, W - 3 * S, H - 3 * S], 2 * S, outline=(0, 0, 0, 200), width=S)
    # pontas de ouro
    for x in (2.5 * S, W - 2.5 * S):
        losango(d, x, H / 2, 3.2 * S, OURO_CLARO + (255,), BRONZE_ESCURO + (255,))
    return salvar(img, f'{nome}_b{borda}')


def barra_preenchimento(nome='Barra_Preenchimento', w=64, h=16):
    """Branca com volume (brilho em cima, sombra embaixo); a cor vem do jogo."""
    y = np.linspace(0, 1, h * S)[:, None]
    luz = 0.62 + 0.38 * np.exp(-((y - 0.28) / 0.22) ** 2) - 0.22 * y
    luz = np.clip(luz, 0, 1)
    estrias = 1 - 0.06 * (np.sin(np.linspace(0, 40 * np.pi, w * S))[None, :] > 0.7)
    v = np.clip(luz * estrias, 0, 1)
    rgba = np.zeros((h * S, w * S, 4), np.uint8)
    rgba[..., :3] = (v[..., None] * 255).astype(np.uint8)
    rgba[..., 3] = 255
    return salvar(Image.fromarray(rgba, 'RGBA'), nome)


def brilho(nome='Brilho', w=128):
    x = np.linspace(-1, 1, w * S)
    r = np.sqrt(x[None, :] ** 2 + x[:, None] ** 2)
    a = np.clip(np.exp(-(r / 0.45) ** 2) - 0.02, 0, 1)
    rgba = np.zeros((w * S, w * S, 4), np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = (a * 255).astype(np.uint8)
    return salvar(Image.fromarray(rgba, 'RGBA'), nome)


def vinheta(nome='Vinheta', w=256):
    x = np.linspace(-1, 1, w * S)
    r = np.sqrt(x[None, :] ** 2 + x[:, None] ** 2) / np.sqrt(2)
    a = 0.78 + 0.2 * np.clip(r, 0, 1) ** 1.4
    rgba = np.zeros((w * S, w * S, 4), np.uint8)
    rgba[..., 0], rgba[..., 1], rgba[..., 2] = 5, 4, 3
    rgba[..., 3] = (a * 255).astype(np.uint8)
    return salvar(Image.fromarray(rgba, 'RGBA'), nome)


def degrade(nome='Degrade', w=256, h=192):
    """Escuro à direita, some à esquerda: dá leitura ao texto sem desenhar uma caixa."""
    x = np.linspace(0, 1, w * S)
    y = np.linspace(0, 1, h * S)
    a = (np.clip(x, 0, 1) ** 1.4)[None, :] * (1 - np.clip((y - 0.55) / 0.45, 0, 1) ** 1.5)[:, None] * 0.8
    rgba = np.zeros((h * S, w * S, 4), np.uint8)
    rgba[..., 0], rgba[..., 1], rgba[..., 2] = 6, 5, 4
    rgba[..., 3] = (a * 255).astype(np.uint8)
    return salvar(Image.fromarray(rgba, 'RGBA'), nome)


def medalhao(nome='Medalhao', w=192):
    img = tela(w, w)
    d = ImageDraw.Draw(img)
    W = w * S
    c = W / 2
    fundo = ruido_pedra(gradiente_vertical(W, W, (34, 27, 19), (9, 7, 5)), 8)
    m = Image.new('L', (W, W), 0)
    ImageDraw.Draw(m).ellipse([10 * S, 10 * S, W - 10 * S, W - 10 * S], fill=255)
    img.paste(fundo, (0, 0), m)
    d = ImageDraw.Draw(img)
    d.ellipse([4 * S, 4 * S, W - 4 * S, W - 4 * S], outline=OURO + (255,), width=4 * S)
    d.ellipse([12 * S, 12 * S, W - 12 * S, W - 12 * S], outline=BRONZE + (230,), width=int(1.5 * S))
    for k in range(24):
        a = k / 24 * 2 * math.pi
        r = W / 2 - 8 * S
        x, y = c + r * math.cos(a), c + r * math.sin(a)
        d.ellipse([x - 1.6 * S, y - 1.6 * S, x + 1.6 * S, y + 1.6 * S], fill=OURO_CLARO + (255,))
    return salvar(img, nome)


def anel(nome='Anel', w=192, espessura=9):
    img = tela(w, w)
    d = ImageDraw.Draw(img)
    W = w * S
    d.ellipse([3 * S, 3 * S, W - 3 * S, W - 3 * S], outline=(255, 255, 255, 255), width=espessura * S)
    return salvar(img, nome)


def disco(nome='Disco', w=64):
    img = tela(w, w)
    ImageDraw.Draw(img).ellipse([S, S, w * S - S, w * S - S], fill=(255, 255, 255, 255))
    return salvar(img, nome)


def alvo(nome='Alvo', w=128):
    img = tela(w, w)
    d = ImageDraw.Draw(img)
    W = w * S
    lw = 4 * S
    br = 30 * S
    m = 10 * S
    cor = OURO_CLARO + (255,)
    for sx in (0, 1):
        for sy in (0, 1):
            x0 = m if sx == 0 else W - m
            y0 = m if sy == 0 else W - m
            dx = 1 if sx == 0 else -1
            dy = 1 if sy == 0 else -1
            d.line([(x0, y0 + dy * br), (x0, y0), (x0 + dx * br, y0)], fill=cor, width=lw, joint='curve')
            losango(d, x0 + dx * 9 * S, y0 + dy * 9 * S, 3 * S, OURO + (255,))
    losango(d, W / 2, m - 2 * S, 5 * S, OURO_CLARO + (255,))
    contorno = img.filter(ImageFilter.MaxFilter(9))
    sombra = Image.new('RGBA', img.size, (0, 0, 0, 0))
    sombra.putalpha(contorno.getchannel('A').point(lambda v: int(v * 0.6)))
    return salvar(Image.alpha_composite(sombra, img), nome)


def tecla(nome='Tecla', w=40, borda=10):
    img = tela(w, w)
    d = ImageDraw.Draw(img)
    W = w * S
    d.rounded_rectangle([2 * S, 2 * S, W - 2 * S, W - 2 * S], 6 * S, fill=(14, 12, 9, 235), outline=OURO + (255,), width=int(1.6 * S))
    d.rounded_rectangle([5 * S, 4 * S, W - 5 * S, W * 0.45], 4 * S, fill=(255, 240, 200, 16))
    return salvar(img, f'{nome}_b{borda}')


def carta(nome='Carta', w=256, h=384, borda=60):
    W, H = w * S, h * S
    fundo = ruido_pedra(gradiente_vertical(W, H, (34, 28, 21), (8, 7, 5), 245), 8)
    mask = mascara_retangulo(W, H, 2 * S, 6 * S)
    img = tela(w, h)
    img.paste(fundo, (0, 0), mask)
    moldura(img, 3 * S, 3 * S, OURO + (255,), 6 * S)
    moldura(img, 11 * S, S, BRONZE + (230,), 3 * S)
    d = ImageDraw.Draw(img)
    # cantos com roseta
    for cx in (11 * S, W - 11 * S):
        for cy in (11 * S, H - 11 * S):
            d.ellipse([cx - 7 * S, cy - 7 * S, cx + 7 * S, cy + 7 * S], fill=BRONZE_ESCURO + (255,), outline=OURO + (255,), width=int(1.5 * S))
            losango(d, cx, cy, 4 * S, OURO_CLARO + (255,))
    # frontão no meio da borda de cima e de baixo
    for cy, k in ((3 * S, 1), (H - 3 * S, -1)):
        d.polygon([(W / 2 - 22 * S, cy), (W / 2, cy + k * 12 * S), (W / 2 + 22 * S, cy)], fill=OURO + (255,))
        losango(d, W / 2, cy + k * 4 * S, 3 * S, BRONZE_ESCURO + (255,))
    return salvar(img, f'{nome}_b{borda}')


def marcador(nome='Losango', w=32):
    img = tela(w, w)
    d = ImageDraw.Draw(img)
    W = w * S
    losango(d, W / 2, W / 2, W / 2 - 2 * S, OURO_CLARO + (255,), BRONZE_ESCURO + (255,))
    losango(d, W / 2, W / 2, W / 5, BRONZE + (255,))
    return salvar(img, nome)


def realce(nome='Realce', w=256, h=48):
    """Faixa dourada que acende atrás do botão escolhido."""
    x = np.linspace(-1, 1, w * S)
    y = np.linspace(-1, 1, h * S)
    a = np.clip(1 - np.abs(x) ** 2.4, 0, 1)[None, :] * np.clip(1 - np.abs(y) ** 4, 0, 1)[:, None] * 0.42
    rgba = np.zeros((h * S, w * S, 4), np.uint8)
    rgba[..., 0], rgba[..., 1], rgba[..., 2] = 150, 98, 34
    rgba[..., 3] = (a * 255).astype(np.uint8)
    img = Image.fromarray(rgba, 'RGBA')
    d = ImageDraw.Draw(img)
    for yy in (S, h * S - 2 * S):
        for i in range(w * S):
            f = 1 - abs(i / (w * S) * 2 - 1) ** 2
            if f > 0:
                d.point((i, yy), fill=OURO_CLARO + (int(255 * f),))
                d.point((i, yy + 1), fill=OURO_CLARO + (int(255 * f),))
    return salvar(img, nome)


def louro(nome='Louro', w=96, h=192):
    """Ramo de louro em arco, como um parêntese: lado esquerdo de uma coroa (o jogo espelha para o outro lado)."""
    img = tela(w, h)
    d = ImageDraw.Draw(img)
    W, H = w * S, h * S
    cx, cy, rx, ry = W * 1.0, H * 0.5, W * 0.78, H * 0.44

    def ponto(th):
        return cx - rx * math.cos(th), cy + ry * math.sin(th)

    n = 80
    ths = [math.radians(78 - 156 * k / (n - 1)) for k in range(n)]  # de baixo para cima
    pts = [ponto(t) for t in ths]
    d.line(pts, fill=BRONZE + (255,), width=int(2.6 * S), joint='curve')
    for k in range(3, n - 2, 6):
        x, y = pts[k]
        x2, y2 = pts[k + 1]
        tx, ty = x2 - x, y2 - y
        norma = math.hypot(tx, ty)
        tx, ty = tx / norma, ty / norma
        esc = 0.55 + 0.45 * (1 - abs(k / n - 0.45))
        comp, larg = 22 * S * esc, 7.5 * S * esc
        for lado in (1, -1):
            ang = math.radians(42) * lado
            dx = tx * math.cos(ang) - ty * math.sin(ang)
            dy = tx * math.sin(ang) + ty * math.cos(ang)
            px, py = -dy, dx
            esq, dir_ = [], []
            for i in range(13):
                f = i / 12
                meia = larg * math.sin(math.pi * f) ** 0.75 * (1 - 0.35 * f)
                bx, by = x + dx * comp * f, y + dy * comp * f
                esq.append((bx + px * meia, by + py * meia))
                dir_.append((bx - px * meia, by - py * meia))
            folha = esq + dir_[::-1]
            d.polygon(folha, fill=(OURO if lado > 0 else (190, 145, 62)) + (255,), outline=BRONZE_ESCURO + (255,))
            d.line([(x, y), (x + dx * comp * 0.85, y + dy * comp * 0.85)], fill=BRONZE + (255,), width=S)
    # folha da ponta
    x, y = pts[-1]
    d.ellipse([x - 4 * S, y - 9 * S, x + 4 * S, y + 2 * S], fill=OURO_CLARO + (255,), outline=BRONZE_ESCURO + (255,))
    return salvar(img, nome)


# ---------------------------------------------------------------- armas (silhuetas brancas com volume; o jogo tinge)
def _sombreada(forma, luz=(-0.6, -0.8)):
    """Silhueta branca com relevo: claro em cima/esquerda, escuro embaixo, contorno escuro."""
    a = np.array(forma.getchannel('A')).astype(float) / 255
    gy, gx = np.gradient(np.array(forma.getchannel('A').filter(ImageFilter.GaussianBlur(3 * S))).astype(float))
    rel = np.clip(0.78 + (gx * luz[0] + gy * luz[1]) * 0.02, 0.45, 1)
    rgba = np.zeros(a.shape + (4,), np.uint8)
    rgba[..., :3] = (rel[..., None] * 255).astype(np.uint8)
    rgba[..., 3] = (a * 255).astype(np.uint8)
    img = Image.fromarray(rgba, 'RGBA')
    contorno = forma.getchannel('A').filter(ImageFilter.MaxFilter(5 * 2 + 1))
    fundo = Image.new('RGBA', img.size, (20, 14, 8, 0))
    fundo.putalpha(contorno.point(lambda v: int(v * 0.85)))
    return Image.alpha_composite(fundo, img)


def arma_xifos(w=128):
    forma = tela(w, w)
    d = ImageDraw.Draw(forma)
    W = w * S
    # lâmina em folha, na diagonal
    lam = [(0.5, 0.06), (0.585, 0.28), (0.57, 0.55), (0.53, 0.66), (0.47, 0.66), (0.43, 0.55), (0.415, 0.28)]
    d.polygon([(x * W, y * W) for x, y in lam], fill=(255, 255, 255, 255))
    d.rectangle([0.33 * W, 0.66 * W, 0.67 * W, 0.71 * W], fill=(255, 255, 255, 255))
    d.rectangle([0.47 * W, 0.71 * W, 0.53 * W, 0.86 * W], fill=(255, 255, 255, 255))
    d.ellipse([0.43 * W, 0.85 * W, 0.57 * W, 0.95 * W], fill=(255, 255, 255, 255))
    forma = forma.rotate(-35, resample=Image.BICUBIC, center=(W / 2, W / 2))
    img = _sombreada(forma)
    ImageDraw.Draw(img)
    return salvar(img, 'Arma_Xifos')


def arma_lanca(w=128):
    forma = tela(w, w)
    d = ImageDraw.Draw(forma)
    W = w * S
    d.rectangle([0.485 * W, 0.24 * W, 0.515 * W, 0.95 * W], fill=(255, 255, 255, 255))
    d.polygon([(0.5 * W, 0.03 * W), (0.565 * W, 0.17 * W), (0.535 * W, 0.27 * W), (0.465 * W, 0.27 * W), (0.435 * W, 0.17 * W)], fill=(255, 255, 255, 255))
    d.rectangle([0.47 * W, 0.27 * W, 0.53 * W, 0.31 * W], fill=(255, 255, 255, 255))
    d.polygon([(0.5 * W, 0.98 * W), (0.525 * W, 0.9 * W), (0.475 * W, 0.9 * W)], fill=(255, 255, 255, 255))
    forma = forma.rotate(-35, resample=Image.BICUBIC, center=(W / 2, W / 2))
    return salvar(_sombreada(forma), 'Arma_Lanca')


def arma_escudo(w=128):
    forma = tela(w, w)
    d = ImageDraw.Draw(forma)
    W = w * S
    d.ellipse([0.1 * W, 0.1 * W, 0.9 * W, 0.9 * W], fill=(255, 255, 255, 255))
    img = _sombreada(forma)
    d2 = ImageDraw.Draw(img)
    d2.ellipse([0.16 * W, 0.16 * W, 0.84 * W, 0.84 * W], outline=(120, 110, 100, 255), width=3 * S)
    # emblema da formiga dos mirmidões, estilizado: três gomos e patas
    for cy, r in ((0.38, 0.06), (0.5, 0.045), (0.63, 0.075)):
        d2.ellipse([0.5 * W - r * W, cy * W - r * W, 0.5 * W + r * W, cy * W + r * W], fill=(110, 100, 90, 255))
    for k, cy in enumerate((0.46, 0.5, 0.54)):
        for lado in (1, -1):
            d2.line([(0.5 * W, cy * W), (0.5 * W + lado * 0.15 * W, (cy - 0.06 + k * 0.06) * W), (0.5 * W + lado * 0.2 * W, (cy + 0.02 + k * 0.05) * W)],
                    fill=(110, 100, 90, 255), width=2 * S, joint='curve')
    return salvar(img, 'Arma_Escudo')


def prancha():
    os.makedirs(PRANCHA, exist_ok=True)
    larg = 1400
    x = y = 16
    linha_h = 0
    pos = []
    for nome, im in gerados:
        w, h = im.size
        if x + w > larg - 16:
            x = 16
            y += linha_h + 16
            linha_h = 0
        pos.append((nome, im, x, y))
        x += w + 16
        linha_h = max(linha_h, h)
    alt = y + linha_h + 16
    fundo = Image.new('RGBA', (larg, alt), (70, 78, 92, 255))
    for nome, im, px, py in pos:
        fundo.alpha_composite(im, (px, py))
    fundo.save(os.path.join(PRANCHA, 'prancha.png'))


def main():
    os.makedirs(SAIDA, exist_ok=True)
    painel('Painel', 192, 192, 36)
    painel('Placa', 96, 96, 20, cantos=False, alfa=225, interno=False)
    carta()
    greca()
    divisor()
    barra_moldura()
    barra_preenchimento()
    brilho()
    vinheta()
    degrade()
    medalhao()
    anel()
    disco()
    alvo()
    tecla()
    realce()
    marcador()
    louro()
    arma_xifos()
    arma_lanca()
    arma_escudo()
    prancha()
    print(f'{len(gerados)} imagens em {SAIDA}')


if __name__ == '__main__':
    main()
