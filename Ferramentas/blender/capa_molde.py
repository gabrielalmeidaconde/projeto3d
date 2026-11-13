"""Molde da capa do Pátroclo: um leque de lã, estreito nos ombros e largo na barra, com os rasgos de batalha.

Coordenadas do molde em metros: u = para os lados (0 no meio), v = para baixo (0 na gola, L na barra).
pano(u, v) > 0 onde existe tecido; o valor é a distância aproximada até a beira (rasgo, furo ou barra).
A mesma função recorta a malha simulada no Blender e pinta o alfa da textura (beira desfiada).
Só numpy: roda no Blender e no Python do sistema.
"""
import numpy as np

L = 1.38          # comprimento, da gola à barra
W_GOLA = 0.74     # largura em cima
W_BARRA = 1.9     # largura embaixo
PINO_U = 0.25     # os dois broches ficam a esta distância do meio, no alto
PINO_V = 0.02
FRANZIDO = 0.14   # trecho da gola que cada broche prende franzido (em pregas)


def meia_largura(v):
    t = np.clip(np.asarray(v, dtype=np.float64) / L, 0.0, 1.0)
    return W_GOLA / 2 + (W_BARRA - W_GOLA) / 2 * t ** 0.92


def _hash(i, seed):
    x = np.sin(np.asarray(i, dtype=np.float64) * 127.1 + seed * 311.7) * 43758.5453
    return x - np.floor(x)


def ruido(x, seed=0.0):
    """Ruído de valor 1D suave em [-1, 1]."""
    x = np.asarray(x, dtype=np.float64)
    i = np.floor(x)
    f = x - i
    f = f * f * (3 - 2 * f)
    return (_hash(i, seed) * (1 - f) + _hash(i + 1, seed) * f) * 2 - 1


def fbm(x, seed=0.0, oitavas=3):
    s, a, tot = 0.0, 1.0, 0.0
    for k in range(oitavas):
        s = s + a * ruido(np.asarray(x) * 2 ** k, seed + k * 17.0)
        tot += a
        a *= 0.5
    return s / tot


# rasgos compridos que sobem da barra: (posição lateral s em -1..1, altura em m, largura na barra em m, semente)
RASGOS = [(-0.62, 0.52, 0.050, 1.0), (-0.21, 0.24, 0.030, 2.0), (0.17, 0.40, 0.044, 3.0), (0.50, 0.17, 0.026, 4.0), (0.74, 0.66, 0.056, 5.0)]
# furos: (u, v, raio em m, semente, queimado)
FUROS = [(-0.36, 0.86, 0.036, 1.0, True), (0.30, 1.00, 0.048, 2.0, True), (0.06, 0.70, 0.016, 3.0, False),
         (-0.12, 1.05, 0.024, 4.0, True), (0.47, 0.63, 0.013, 5.0, False), (-0.55, 1.08, 0.030, 6.0, True)]


def barra(u):
    """Recuo da barra (m) em cada ponto: irregular, com mordidas."""
    u = np.asarray(u, dtype=np.float64)
    r = 0.022 + 0.020 * fbm(u * 5.0, 3.0) + 0.012 * ruido(u * 21.0, 9.0)
    r = r + 0.075 * np.clip(ruido(u * 2.3, 21.0) - 0.25, 0, 1) / 0.75   # mordidas largas
    # canto de baixo do lado direito do personagem arrancado em diagonal
    r = r + np.clip((-u - 0.50) * 1.25, 0, 0.36)
    return np.maximum(r, 0.004)


def pano(u, v):
    u = np.asarray(u, dtype=np.float64)
    v = np.asarray(v, dtype=np.float64)
    h = meia_largura(v)
    hL = meia_largura(L)
    # laterais levemente irregulares e gola reta
    d = np.minimum(h - np.abs(u) - 0.004 * (1 + ruido(v * 30.0, 4.0)), v + 0.002)
    d = np.minimum(d, (L - barra(u)) - v)
    s = u / np.maximum(h, 1e-6)
    alt = L - v   # altura acima da barra
    for s0, altura, largura, seed in RASGOS:
        k = np.clip(alt / altura, 0.0, 1.0)
        # o rasgo segue o fio (s constante), com um vai-e-vem que cresce para a barra
        centro = (s0 + 0.035 * ruido(alt * 9.0, seed) * (1 - k)) * h
        larg = largura * (1 - k) ** 0.8 * (1 + 0.5 * ruido(alt * 26.0, seed + 7.0))
        dist = np.abs(u - centro) - larg / 2
        d = np.where(alt < altura, np.minimum(d, dist), np.minimum(d, np.hypot(u - centro, alt - altura)))
    for u0, v0, r, seed, _ in FUROS:
        ang = np.arctan2(v - v0, u - u0)
        rr = r * (1 + 0.38 * ruido(ang * 2.2, seed) + 0.16 * ruido(ang * 7.0, seed + 3.0))
        d = np.minimum(d, np.hypot(u - u0, (v - v0) * 0.8) - rr)
    return d


def queimado(u, v):
    """0..1: quanto o tecido está chamuscado (em volta dos furos de brasa e em trechos da barra)."""
    u = np.asarray(u, dtype=np.float64)
    v = np.asarray(v, dtype=np.float64)
    q = np.zeros(np.broadcast(u, v).shape)
    for u0, v0, r, seed, fogo in FUROS:
        if not fogo:
            continue
        dist = np.hypot(u - u0, (v - v0) * 0.8) - r
        q = np.maximum(q, np.clip(1 - dist / (r * 1.6 + 0.03), 0, 1))
    borda = np.clip(1 - ((L - barra(u)) - v) / 0.09, 0, 1) * np.clip(ruido(u * 3.1, 33.0) * 1.4 + 0.2, 0, 1)
    return np.maximum(q, borda)
