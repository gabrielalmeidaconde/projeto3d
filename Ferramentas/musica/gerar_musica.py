#!/usr/bin/env python3
"""
Trilha de combate da Fase 1, em camadas: cada nota de estilo (D, C, B, A, S, SS, SSS) liga mais instrumentos,
como no Devil May Cry. Tudo é composto e sintetizado aqui (o projeto não usa amostras de terceiros).

Uma faixa por camada, todas com o mesmo comprimento (16 compassos a 150 BPM = 25,6 s) e em loop perfeito:
caudas de reverberação e notas que passam do fim voltam para o começo. O jogo toca todas juntas e só mexe
no volume (MusicaDinamica.cs).

  0 Base       cordas graves e bordão de ré (também fora de combate)
  1 D          tambores de guerra (taiko) e aro
  2 C          baixo e darbuka
  3 B          guitarras pesadas (duas, esquerda e direita)
  4 A          bateria e cordas em staccato
  5 S          melodia no aulos e metais
  6 SS         coro, bumbo duplo e pratos
  7 SSS        solo de guitarra em harmonia e viradas de tom-tom

Uso (na raiz do projeto): python3 -I Ferramentas/musica/gerar_musica.py
Grava Assets/Troia/Resources/Troia/Musica/Camada_N.wav e, em Capturas/Musica, uma prévia que sobe de D a SSS.
"""
import os
import sys
import time
import wave

import numpy as np
from scipy import signal
from scipy.ndimage import minimum_filter1d, uniform_filter1d

RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SAIDA = os.path.join(RAIZ, 'Assets', 'Troia', 'Resources', 'Troia', 'Musica')
PREVIA = os.path.join(RAIZ, 'Capturas', 'Musica')

SR = 48000            # síntese
SR_SAIDA = 32000      # arquivos (48k -> 32k exato: 2/3); a 150 BPM a batida tem 12 800 amostras
BPM = 150
PASSO = SR * 60 // BPM // 4          # semicolcheia: 4800 amostras
COMPASSOS = 16
L = COMPASSOS * 16 * PASSO           # 1 228 800 amostras = 25,6 s
CAUDA = 6 * SR

# ---------------------------------------------------------------- harmonia
# um acorde por compasso: ré menor com o segundo grau frígio (Mi bemol) e a dominante maior no fim
ACORDES = ['Dm', 'Dm', 'Eb', 'Dm', 'Dm', 'Dm', 'C', 'Eb', 'Bb', 'Bb', 'C', 'C', 'Gm', 'Gm', 'A', 'A']
# raiz grave da guitarra (afinação baixa) e terça do acorde
RAIZ_GUITARRA = {'Dm': 38, 'Eb': 39, 'C': 36, 'Bb': 34, 'Gm': 43, 'A': 45}
TERCA = {'Dm': 3, 'Eb': 4, 'C': 4, 'Bb': 4, 'Gm': 3, 'A': 4}
# escala de cada acorde (classes de altura), para harmonizar a melodia em terças
ESCALA = {
    'Dm': [2, 4, 5, 7, 9, 10, 0],
    'Eb': [3, 5, 7, 9, 10, 0, 2],
    'C': [0, 2, 4, 5, 7, 9, 10],
    'Bb': [10, 0, 2, 3, 5, 7, 9],
    'Gm': [7, 9, 10, 0, 2, 3, 5],
    'A': [9, 10, 1, 2, 4, 5, 7],
}
NOMES = {'C': 0, 'D': 2, 'E': 4, 'F': 5, 'G': 7, 'A': 9, 'B': 11}


def nota(s):
    pc, i = NOMES[s[0]], 1
    while i < len(s) and s[i] in '#b':
        pc += 1 if s[i] == '#' else -1
        i += 1
    return pc + 12 * (int(s[i:]) + 1)


# melodia (aulos): por compasso, (nome, duração em semicolcheias); soma 16 em cada compasso
MELODIA = [
    [('D5', 8), ('C5', 2), ('Bb4', 2), ('A4', 4)],
    [('A4', 6), ('Bb4', 2), ('A4', 2), ('G4', 2), ('F4', 2), ('E4', 2)],
    [('G4', 4), ('Bb4', 4), ('Eb5', 6), ('D5', 2)],
    [('D5', 6), ('C#5', 2), ('D5', 4), ('A4', 4)],
    [('F4', 2), ('G4', 2), ('A4', 4), ('Bb4', 2), ('A4', 2), ('G4', 2), ('F4', 2)],
    [('E4', 4), ('F4', 2), ('E4', 2), ('D4', 8)],
    [('E4', 4), ('G4', 4), ('C5', 6), ('Bb4', 2)],
    [('Bb4', 6), ('C5', 2), ('Bb4', 4), ('G4', 4)],
    [('D5', 6), ('F5', 2), ('D5', 4), ('Bb4', 4)],
    [('C5', 4), ('D5', 4), ('Eb5', 4), ('D5', 4)],
    [('E5', 6), ('G5', 2), ('E5', 4), ('C5', 4)],
    [('D5', 4), ('E5', 4), ('F5', 4), ('E5', 4)],
    [('D5', 8), ('Bb4', 4), ('G4', 4)],
    [('A4', 4), ('Bb4', 4), ('C5', 4), ('D5', 4)],
    [('C#5', 8), ('E5', 4), ('G5', 4)],
    [('F5', 4), ('E5', 4), ('D5', 2), ('C#5', 2), ('A4', 4)],
]


def eventos_melodia():
    """(passo global, duração, midi, fim de frase). A última nota de cada frase de 2 compassos respira."""
    ev = []
    for c, compasso in enumerate(MELODIA):
        assert sum(d for _, d in compasso) == 16, c
        p = c * 16
        for k, (n, d) in enumerate(compasso):
            ev.append((p, d, nota(n), c % 2 == 1 and k == len(compasso) - 1, c))
            p += d
    return ev


def terca_acima(m, acorde):
    esc = ESCALA[acorde]
    pc = m % 12
    if pc not in esc:
        return m + 3
    i = esc.index(pc)
    alvo = esc[(i + 2) % 7]
    return m + (alvo - pc) % 12


# ---------------------------------------------------------------- utilitários de sinal
def mtof(m):
    return 440.0 * 2 ** ((np.asarray(m, dtype=float) - 69) / 12)


def _sos(tipo, fc, ordem):
    if tipo == 'band':
        fc = [fc[0], min(fc[1], SR * 0.45)]
    else:
        fc = min(fc, SR * 0.45)
    return signal.butter(ordem, fc, tipo, fs=SR, output='sos')


def lp(x, fc, ordem=2):
    return signal.sosfilt(_sos('low', fc, ordem), x, axis=-1)


def hp(x, fc, ordem=2):
    return signal.sosfilt(_sos('high', fc, ordem), x, axis=-1)


def bp(x, lo, hi, ordem=2):
    return signal.sosfilt(_sos('band', (lo, hi), ordem), x, axis=-1)


def pico(x, f0, ganho_db, q):
    """Equalizador paramétrico (biquad de pico, RBJ)."""
    a_ = 10 ** (ganho_db / 40)
    w = 2 * np.pi * f0 / SR
    al = np.sin(w) / (2 * q)
    b = np.array([1 + al * a_, -2 * np.cos(w), 1 - al * a_])
    a = np.array([1 + al / a_, -2 * np.cos(w), 1 - al / a_])
    return signal.lfilter(b / a[0], a / a[0], x, axis=-1)


def _blep(t, dt):
    y = np.zeros_like(t)
    m = t < dt
    x = t[m] / dt[m]
    y[m] = x + x - x * x - 1
    m = t > 1 - dt
    x = (t[m] - 1) / dt[m]
    y[m] = x * x + x + x + 1
    return y


def _fase(freq):
    dt = np.asarray(freq, dtype=float) / SR
    return np.cumsum(dt) - dt, dt


def serra(freq, fase0=0.0):
    """Dente de serra sem serrilhado (PolyBLEP); freq é um vetor em Hz, uma por amostra."""
    ph, dt = _fase(freq)
    t = (ph + fase0) % 1.0
    return 2 * t - 1 - _blep(t, dt)


def pulso(freq, largura=0.5, fase0=0.0):
    ph, dt = _fase(freq)
    t1 = (ph + fase0) % 1.0
    t2 = (ph + fase0 + largura) % 1.0
    return (2 * t1 - 1 - _blep(t1, dt)) - (2 * t2 - 1 - _blep(t2, dt))


def seno(freq, fase0=0.0):
    ph, _ = _fase(freq)
    return np.sin(2 * np.pi * (ph + fase0))


def cte(f, n):
    return np.full(n, float(f))


def envelope(n_on, a, d, s, r):
    """Ataque curvo, queda exponencial até s e soltura depois de n_on; comprimento n_on + r*SR."""
    a = max(1, int(a * SR))
    d = max(1.0, d * SR)
    nr = max(1, int(r * SR))
    i = np.arange(n_on, dtype=float)
    on = np.where(i < a, 1 - (1 - i / a) ** 2, s + (1 - s) * np.exp(-(i - a) / d))
    fim = on[-1] if n_on else 0.0
    return np.concatenate([on, fim * np.exp(-np.arange(nr) / (nr / 6))])


def pan(x, p):
    a = (p + 1) * np.pi / 4
    return np.stack([x * np.cos(a), x * np.sin(a)])


def tempo(passo_global, jitter_ms=0.0, g=None):
    j = g.normal(0, jitter_ms) if (g is not None and jitter_ms) else 0.0
    return int(passo_global * PASSO + j * SR / 1000)


class Faixa:
    def __init__(self):
        self.buf = np.zeros((2, L + CAUDA))

    def soma(self, x, inicio, p=0.0):
        if x.ndim == 1:
            x = pan(x, p)
        i0 = int(inicio)
        if i0 < 0:
            x = x[:, -i0:]
            i0 = 0
        fim = min(i0 + x.shape[1], self.buf.shape[1])
        self.buf[:, i0:fim] += x[:, :fim - i0]


# ---------------------------------------------------------------- efeitos
def resposta(rt60, pre=0.015, amortecimento=5000.0, semente=0):
    """Resposta de sala sintética: reflexões iniciais e cauda difusa que escurece com o tempo."""
    g = np.random.default_rng(semente)
    n = int(rt60 * SR)
    t = np.arange(n) / SR
    d0 = int(pre * SR)
    ir = np.zeros((2, n + d0))
    for c in range(2):
        ruido = g.standard_normal(n)
        escuro = lp(ruido, amortecimento, 1) * np.exp(-6.91 * t / rt60)
        claro = ruido * np.exp(-6.91 * t / (rt60 * 0.3))
        cauda = (escuro * 3 + 0.4 * claro) * np.clip(t / 0.025, 0, 1)
        ir[c, d0:] = cauda
        for k in range(10):
            atraso = int(g.uniform(0.003, pre + 0.035) * SR)
            ir[c, atraso] += g.uniform(0.6, 1.4) * (1 if k % 2 == 0 else -1) * np.sqrt(np.mean(cauda[:2000] ** 2) + 1e-9) * 40
    ir /= np.sqrt((ir ** 2).sum(axis=1, keepdims=True))
    return ir


SALA = None
SALAO = None
CAVERNA = None


def reverb(x, ir, mix, corte=180.0):
    molhado = np.stack([signal.fftconvolve(x[c], ir[c]) for c in range(2)])
    molhado = hp(molhado, corte)
    out = np.zeros((2, molhado.shape[1]))
    out[:, :x.shape[1]] += x
    out += molhado * mix
    return out


def eco(x, tempo_s, retorno, mix, repeticoes=6):
    """Eco pingue-pongue, mais escuro a cada volta."""
    d = int(tempo_s * SR)
    out = np.zeros((2, x.shape[1] + d * repeticoes))
    out[:, :x.shape[1]] += x
    mono = x.mean(axis=0)
    for k in range(1, repeticoes + 1):
        mono = lp(mono, 3500, 1)
        out[k % 2, d * k:d * k + len(mono)] += mono * mix * retorno ** (k - 1)
    return out


def dobrar(x):
    """Loop perfeito: tudo o que passa do fim volta para o começo."""
    out = np.zeros((2, L))
    for i in range(0, x.shape[1], L):
        seg = x[:, i:i + L]
        out[:, :seg.shape[1]] += seg
    return out


def limitador(x, teto_db, olhar=0.004, soltura=0.09):
    """Limitador com antecipação: segura picos isolados (bumbo, darbuka) sem amassar o resto. Circular, para o loop."""
    teto = 10 ** (teto_db / 20)
    w = 2 * int(olhar * SR) + 1
    bruto = np.minimum(1.0, teto / np.maximum(np.abs(x).max(axis=0), 1e-9))
    suave = uniform_filter1d(minimum_filter1d(bruto, w, mode='wrap'), w, mode='wrap')
    a = 1 - np.exp(-1 / (soltura * SR))
    solta = signal.lfilter([a], [1, a - 1], suave, zi=[suave[-1] * (1 - a)])[0]
    return x * np.minimum(suave, solta)


def rms_db(x):
    return 20 * np.log10(np.sqrt(np.mean(x ** 2)) + 1e-12)


# ---------------------------------------------------------------- percussão
def taiko(vel, f0, g):
    n = int(1.6 * SR)
    t = np.arange(n) / SR
    f = f0 * (1 + 0.8 * np.exp(-t * 22))
    corpo = seno(f) * np.exp(-t * 2.6) + 0.4 * seno(f * 1.58) * np.exp(-t * 5) + 0.18 * seno(f * 2.31) * np.exp(-t * 9)
    pele = lp(g.standard_normal(n), 700) * np.exp(-t * 22) * 6
    tapa = bp(g.standard_normal(n), 1200, 5000) * np.exp(-t * 80) * 0.9
    x = corpo + pele + tapa
    return np.tanh(1.8 * x) / np.tanh(1.8) * vel


def aro(vel, g):
    n = int(0.15 * SR)
    t = np.arange(n) / SR
    x = bp(g.standard_normal(n), 1800, 6500) * np.exp(-t * 70) * 2.2 + seno(cte(1150, n)) * np.exp(-t * 90) * 0.5
    return x * vel


def bumbo(vel, g):
    n = int(0.5 * SR)
    t = np.arange(n) / SR
    corpo = seno(46 + 120 * np.exp(-t * 38)) * np.exp(-t * 6.5)
    clique = hp(g.standard_normal(n), 2500) * np.exp(-t * 260) * 0.9
    return np.tanh(2.2 * (corpo + clique)) / np.tanh(2.2) * vel


def caixa(vel, g):
    n = int(0.45 * SR)
    t = np.arange(n) / SR
    tom = (seno(190 * (1 + 0.1 * np.exp(-t * 60))) * 0.8 + seno(cte(330, n)) * 0.4) * np.exp(-t * 22)
    esteira = bp(g.standard_normal(n), 1800, 9000) * np.exp(-t * 14) * 2.4
    estalo = hp(g.standard_normal(n), 4000) * np.exp(-t * 120) * 1.2
    return np.tanh(1.4 * (tom + esteira + estalo)) * vel


FREQ_METAL = [205.3, 304.4, 369.6, 522.7, 540.0, 800.0]


def _metal(n, g, escala):
    return sum(pulso(cte(f * escala, n), 0.5, g.random()) for f in FREQ_METAL) / 6


def chimbal(vel, aberto, g):
    n = int((0.7 if aberto else 0.14) * SR)
    t = np.arange(n) / SR
    x = bp(0.8 * _metal(n, g, 1.9) + 0.6 * g.standard_normal(n), 6500, 16000)
    return x * np.exp(-t * (6 if aberto else 55)) * 2.5 * vel


def prato(vel, g, china=False):
    n = int(2.6 * SR)
    t = np.arange(n) / SR
    out = []
    for _ in range(2):
        m = _metal(n, g, 1.25 if china else 1.45)
        x = bp(0.7 * m + g.standard_normal(n), 2600 if china else 3200, 16000)
        x *= np.exp(-t * (2.2 if china else 1.5)) * (1 + 0.3 * np.exp(-t * 30))
        out.append(x)
    return np.stack(out) * 1.4 * vel


def tom_tom(vel, f0, g):
    n = int(0.9 * SR)
    t = np.arange(n) / SR
    corpo = seno(f0 * (1 + 0.45 * np.exp(-t * 18))) * np.exp(-t * 5)
    ataque = bp(g.standard_normal(n), 300, 3000) * np.exp(-t * 45) * 1.2
    return np.tanh(1.6 * (corpo + ataque)) * vel


def darbuka(tipo, vel, g):
    if tipo == 'D':
        n = int(0.5 * SR)
        t = np.arange(n) / SR
        x = seno(105 * (1 + 0.35 * np.exp(-t * 35))) * np.exp(-t * 8) + lp(g.standard_normal(n), 900) * np.exp(-t * 40) * 3
    else:
        n = int(0.16 * SR)
        t = np.arange(n) / SR
        x = (bp(g.standard_normal(n), 2500, 9000) * np.exp(-t * 75) * 2.4
             + seno(cte(620, n)) * np.exp(-t * 55) * 0.5 + seno(cte(1240, n)) * np.exp(-t * 70) * 0.2)
    return x * vel


# ---------------------------------------------------------------- cordas dedilhadas (Karplus-Strong) e guitarra
def corda_ks(freq, n, amortece, brilho, g):
    per = max(2, int(round(SR / freq - 0.5)))
    exc = lp(g.uniform(-1, 1, per), brilho)
    exc -= exc.mean()
    y = np.zeros(n)
    y[:min(per, n)] = exc[:min(per, n)]
    i = per
    while i < n:
        j = min(i + per, n)
        idx = np.arange(i, j)
        a = y[idx - per]
        b = np.where(idx - per - 1 >= 0, y[np.maximum(idx - per - 1, 0)], 0.0)
        y[i:j] = amortece * 0.5 * (a + b)
        i = j
    return y


def guitarra_nota(notas, n_on, abafado, vel, g):
    corte = int(0.035 * SR)
    n = n_on + corte
    x = np.zeros(n + int(0.03 * SR))
    for k, m in enumerate(notas):
        atraso = int(k * 0.005 * SR) + int(abs(g.normal(0, 0.0015)) * SR)
        s = corda_ks(float(mtof(m)), n, 0.988 if abafado else 0.9986, 2200 if abafado else 7000, g)
        x[atraso:atraso + n] += s[:len(x) - atraso]
    x = x[:n]
    env = np.ones(n)
    env[n_on:] = np.linspace(1, 0, corte)
    if abafado:
        x = lp(x, 1500)
    return x * env * vel


def amplificador(x, ganho=30.0):
    x = hp(x, 110)
    x = pico(x, 800, 5, 0.9)
    x = np.tanh(ganho * x + 0.1) - np.tanh(0.1)
    x = pico(x, 120, 3, 0.7)
    x = pico(x, 420, -5, 1.2)
    x = pico(x, 2300, 4, 1.0)
    x = lp(x, 5200, 4)
    return hp(x, 70)


# ---------------------------------------------------------------- sintetizadores
def baixo_nota(m, n_on, vel):
    n = n_on + int(0.06 * SR)
    t = np.arange(n) / SR
    f = cte(float(mtof(m)), n)
    s = serra(f) * 0.6 + seno(f) * 0.9
    escuro, claro = lp(s, 380), lp(s, 2000)
    x = escuro + (claro - escuro) * np.exp(-t * 14)
    return x * envelope(n_on, 0.004, 0.18, 0.55, 0.06)[:n] * vel


def cordas(m, n_on, a, r, g, vozes=5, brilho=3200.0, vel=1.0, vibrato=0.005, largura=0.7):
    n = n_on + int(r * SR)
    t = np.arange(n) / SR
    f0 = float(mtof(m))
    out = np.zeros((2, n))
    for _ in range(vozes):
        det = 2 ** (g.uniform(-10, 10) / 1200)
        vib = 1 + vibrato * np.sin(2 * np.pi * g.uniform(4.8, 6.2) * t + g.random() * 6.28) * np.clip((t - 0.15) / 0.3, 0, 1)
        out += pan(serra(f0 * det * vib, g.random()), g.uniform(-largura, largura))
    out = hp(lp(out, brilho, 2), 60)
    return out * envelope(n_on, a, 0.4, 0.85, r)[:n] * vel / np.sqrt(vozes)


def metais(m, n_on, vel, g):
    n = n_on + int(0.25 * SR)
    t = np.arange(n) / SR
    env = envelope(n_on, 0.05, 0.25, 0.75, 0.25)[:n]
    f = float(mtof(m)) * (1 - 0.015 * np.exp(-t * 30))
    x = sum(serra(f * 2 ** (d / 1200), g.random()) for d in (-7, 0, 6)) / 3
    escuro, medio, claro = lp(x, 500), lp(x, 1500), lp(x, 3600)
    b = np.clip(env * vel, 0, 1)
    y = escuro + (medio - escuro) * b + (claro - medio) * b ** 3
    return np.tanh(1.3 * y * env * vel)


def coro_nota(m, n_on, g, vozes=6):
    n = n_on + int(0.9 * SR)
    t = np.arange(n) / SR
    f0 = float(mtof(m))
    out = np.zeros((2, n))
    for _ in range(vozes):
        det = 2 ** (g.uniform(-14, 14) / 1200)
        vib = 1 + 0.006 * np.sin(2 * np.pi * g.uniform(5.0, 5.8) * t + g.random() * 6.28)
        f = f0 * det * vib
        voz = 0.6 * serra(f, g.random()) + 0.4 * pulso(f, 0.3, g.random())
        out += pan(voz, g.uniform(-0.8, 0.8))
    sopro = bp(g.standard_normal(n), 800, 3500) * 0.25
    out += np.stack([sopro, sopro])
    return out * envelope(n_on, 0.35, 0.5, 0.9, 0.9)[:n] / np.sqrt(vozes)


def formantes_ah(x):
    return (bp(x, 620, 920) * 1.0 + bp(x, 980, 1320) * 0.7 + bp(x, 2450, 3000) * 0.35 + bp(x, 3300, 3800) * 0.18
            + lp(x, 300) * 0.25)


def linha_continua(eventos, transpor, ornamento, n_total, g, glide=0.03, vib_semitons=0.35, vib_hz=5.6):
    """Frequência e amplitude contínuas para instrumentos solistas (legato, ornamento, vibrato)."""
    alvo = np.full(n_total, np.nan)
    amp = np.zeros(n_total)
    vib = np.zeros(n_total)
    for (p, d, m, respira, _) in eventos:
        i0 = p * PASSO
        dur = d * PASSO
        if respira:
            dur = int(dur * 0.7)
        alvo[i0:i0 + dur] = m + transpor
        if d >= 6 and ornamento:
            k = int(0.045 * SR) if ornamento == 'acima' else int(0.09 * SR)
            alvo[i0:i0 + k] = m + transpor + (2 if ornamento == 'acima' else -2)
        e = envelope(dur, 0.018, 0.5, 0.85, 0.06)
        fim = min(i0 + len(e), n_total)
        amp[i0:fim] = np.maximum(amp[i0:fim], e[:fim - i0] * (0.92 + 0.08 * g.random()))
        if d >= 4:
            t = np.arange(dur) / SR
            vib[i0:i0 + dur] = vib_semitons * np.sin(2 * np.pi * vib_hz * t) * np.clip((t - 0.18) / 0.25, 0, 1)
    # nas pausas a altura fica onde estava; suaviza as passagens (portamento curto)
    idx = np.where(~np.isnan(alvo), np.arange(n_total), 0)
    np.maximum.accumulate(idx, out=idx)
    alvo = np.where(np.isnan(alvo), alvo[idx], alvo)
    alvo = np.nan_to_num(alvo, nan=float(eventos[0][2] + transpor))
    a = 1 - np.exp(-1 / (glide * SR))
    suave = signal.lfilter([a], [1, a - 1], alvo, zi=[alvo[0] * (1 - a)])[0]
    return mtof(suave + vib), amp


# ---------------------------------------------------------------- camadas
def camada_base(g):
    f = Faixa()
    c = 0
    while c < COMPASSOS:
        ac = ACORDES[c]
        k = 1
        while c + k < COMPASSOS and ACORDES[c + k] == ac:
            k += 1
        r = RAIZ_GUITARRA[ac]
        n = k * 16 * PASSO
        for m, v in ((r, 0.9), (r + 7, 0.6), (r + 12, 0.55), (r + 12 + TERCA[ac], 0.35)):
            f.soma(cordas(m, n, 0.7, 1.2, g, vozes=4, brilho=1600, vel=v, vibrato=0.004), tempo(c * 16))
        # trêmulo agudo na terça: tensão contida
        alto = r + 36 + TERCA[ac]
        x = cordas(alto, n, 0.9, 1.0, g, vozes=3, brilho=5000, vel=0.16, vibrato=0.003)
        trem = 0.55 + 0.45 * np.sin(2 * np.pi * 12.5 * np.arange(x.shape[1]) / SR)
        f.soma(x * trem, tempo(c * 16))
        c += k
    # bordão de ré que respira devagar
    n = L + 2 * SR
    t = np.arange(n) / SR
    bordao = cordas(38, n - SR, 2.0, 1.0, g, vozes=4, brilho=900, vel=0.7, vibrato=0.002)
    bordao *= 0.8 + 0.2 * np.sin(2 * np.pi * t / (L / SR / 2))[:bordao.shape[1]]
    f.soma(bordao, 0)
    return reverb(f.buf, SALAO, 0.45)


def camada_taiko(g):
    f = Faixa()
    for c in range(COMPASSOS):
        b = c * 16
        f.soma(taiko(1.0, 50, g), tempo(b, 2, g), -0.1)
        f.soma(taiko(0.85, 50, g), tempo(b + 8, 2, g), 0.1)
        for s, v in ((6, 0.55), (11, 0.5), (14, 0.65)):
            f.soma(taiko(v, 88, g), tempo(b + s, 3, g), g.uniform(-0.5, 0.5))
        for s in (4, 12):
            f.soma(aro(0.35, g), tempo(b + s, 2, g), 0.35)
        if c % 4 == 3:
            for i, s in enumerate((12, 13, 14, 15)):
                f.soma(taiko(0.5 + 0.15 * i, 88 if i % 2 else 70, g), tempo(b + s, 2, g), (-0.4, 0.4)[i % 2])
            if c == COMPASSOS - 1:
                f.soma(taiko(1.0, 46, g), tempo(b + 15, 1, g))
    return reverb(f.buf, SALAO, 0.32)


def camada_baixo_darbuka(g):
    baixo = Faixa()
    for c in range(COMPASSOS):
        r = RAIZ_GUITARRA[ACORDES[c]] - 12
        if r < 24:
            r += 12
        for s in range(0, 16, 2):
            m = r + (12 if s in (6, 14) else 0)
            baixo.soma(baixo_nota(m, int(PASSO * 1.7), 1.0 if s in (0, 8) else 0.8), tempo(c * 16 + s, 2, g))
    b = baixo.buf.mean(axis=0)
    b = np.tanh(1.8 * b) / np.tanh(1.8)
    b = hp(lp(b, 3000), 30)
    darb = Faixa()
    for c in range(COMPASSOS):
        p0 = c * 16
        for s, tipo, v in ((0, 'D', 1.0), (2, 'T', 0.8), (6, 'T', 0.75), (8, 'D', 0.9), (12, 'T', 0.85)):
            darb.soma(darbuka(tipo, v, g), tempo(p0 + s, 3, g), 0.25)
        for s in (3, 5, 10, 14, 15):
            darb.soma(darbuka('T', 0.28, g), tempo(p0 + s, 4, g), 0.3)
        if c % 4 == 3:
            for s in (12, 13, 14, 15):
                darb.soma(darbuka('T', 0.55, g), tempo(p0 + s, 2, g), 0.2)
    out = pan(b, 0) * 1.0 + reverb(darb.buf, SALA, 0.25) [:, :L + CAUDA] * 0.9
    return out


def camada_guitarras(g):
    muda = [c == 0 or ACORDES[c] != ACORDES[c - 1] for c in range(COMPASSOS)]
    pistas = []
    for lado in (-1, 1):
        f = Faixa()
        gl = np.random.default_rng(int(g.integers(1 << 30)))
        for c in range(COMPASSOS):
            ac = ACORDES[c]
            r = RAIZ_GUITARRA[ac]
            p0 = c * 16
            if c == COMPASSOS - 1:
                passos = [(0, 6, False), (8, 1, True), (10, 1, True), (11, 1, True)]
            elif muda[c]:
                passos = [(0, 6, False), (8, 1, True), (10, 1, True), (11, 1, True), (12, 1, True), (14, 1, True), (15, 1, True)]
            else:
                passos = [(s, 1, True) for s in (0, 2, 3, 4, 6, 7, 8, 10, 11, 12, 14, 15)]
            for s, d, abafado in passos:
                acento = s in (0, 8)
                notas = [r, r + 7] if abafado else [r, r + 7, r + 12]
                n_on = int(PASSO * (0.85 if abafado else d * 0.97))
                v = (1.0 if acento else 0.8) * gl.uniform(0.92, 1.0)
                f.soma(guitarra_nota(notas, n_on, abafado, v, gl), tempo(p0 + s, 2.5, gl))
            if c == COMPASSOS - 1:
                # corrida frígia de volta para o ré
                for s, m in ((12, 45), (13, 46), (14, 49), (15, 50)):
                    f.soma(guitarra_nota([m], int(PASSO * 0.95), False, 0.9, gl), tempo(p0 + s, 2, gl))
        x = amplificador(f.buf.mean(axis=0) * 1.4)
        pistas.append(pan(x, 0.85 * lado))
    return reverb(pistas[0] + pistas[1], SALA, 0.1)


def camada_bateria_cordas(g):
    bat = Faixa()
    for c in range(COMPASSOS):
        p0 = c * 16
        bumbos = (0, 2, 6, 8, 10, 14) if c >= 12 else (0, 6, 8, 10)
        for s in bumbos:
            bat.soma(bumbo(1.0 if s in (0, 8) else 0.85, g), tempo(p0 + s, 1.5, g))
        for s in (4, 12):
            bat.soma(caixa(1.0, g), tempo(p0 + s, 1.5, g), 0.05)
        for s in range(0, 16, 2):
            aberto = c % 4 == 3 and s == 14
            bat.soma(chimbal(0.55 if s % 4 == 0 else 0.4, aberto, g), tempo(p0 + s, 2, g), 0.35)
        if c in (0, 8):
            bat.soma(prato(0.9, g), tempo(p0, 1, g))
        if c % 8 == 7:
            for i, s in enumerate((12, 13, 14, 15)):
                bat.soma(caixa(0.6 + 0.12 * i, g), tempo(p0 + s, 1.5, g), 0.05)
    bat_out = reverb(bat.buf, SALA, 0.16)
    cor = Faixa()
    padrao = (0, 12, 7, 12, 'T', 12, 7, 12)
    for c in range(COMPASSOS):
        ac = ACORDES[c]
        r = (RAIZ_GUITARRA[ac] % 12) + 48
        if r < 50:
            r += 12
        for s in range(16):
            iv = padrao[s % 8]
            m = r + (TERCA[ac] + 12 if iv == 'T' else iv)
            v = 0.9 if s % 4 == 0 else 0.65
            cor.soma(cordas(m, int(PASSO * 0.6), 0.004, 0.09, g, vozes=3, brilho=4800, vel=v, vibrato=0.0, largura=0.5),
                     tempo(c * 16 + s, 2, g))
    cor_out = reverb(cor.buf, SALAO, 0.22)
    n = max(bat_out.shape[1], cor_out.shape[1])
    out = np.zeros((2, n))
    out[:, :bat_out.shape[1]] += bat_out
    out[:, :cor_out.shape[1]] += cor_out * 0.55
    return out


def camada_aulos_metais(g):
    ev = eventos_melodia()
    n_total = L + CAUDA
    freq, amp = linha_continua(ev, 0, 'acima', n_total, g)
    fonte = pulso(freq, 0.32) * 0.6 + serra(freq) * 0.4
    fonte = pico(pico(fonte, 1100, 9, 1.5), 2600, 5, 2.0)
    fonte = hp(lp(fonte, 7000), 220)
    sopro = bp(g.standard_normal(n_total), 1000, 5000) * 0.12
    aulos = (fonte + sopro) * amp
    aulos = eco(pan(aulos, 0.0), 0.3, 0.35, 0.22)
    aulos = reverb(aulos, SALAO, 0.32)
    met = Faixa()
    muda = [c == 0 or ACORDES[c] != ACORDES[c - 1] for c in range(COMPASSOS)]
    for c in range(COMPASSOS):
        ac = ACORDES[c]
        r = RAIZ_GUITARRA[ac]
        voz = [r, r + 7, r + 12, r + 12 + TERCA[ac]]
        if c >= 14:
            if c == 14:
                for i, m in enumerate(voz):
                    met.soma(metais(m, 32 * PASSO - int(0.2 * SR), 0.95, g), tempo(c * 16, 2, g), (-0.4, 0.4, -0.2, 0.2)[i])
            continue
        if muda[c] or c % 2 == 0:
            for i, m in enumerate(voz):
                met.soma(metais(m, int(PASSO * 2.6), 0.9, g), tempo(c * 16, 2, g), (-0.4, 0.4, -0.2, 0.2)[i])
            for i, m in enumerate(voz):
                met.soma(metais(m, int(PASSO * 1.2), 0.7, g), tempo(c * 16 + 6, 2, g), (-0.4, 0.4, -0.2, 0.2)[i])
    met_out = reverb(met.buf, SALAO, 0.3)
    n = max(aulos.shape[1], met_out.shape[1])
    out = np.zeros((2, n))
    out[:, :aulos.shape[1]] += aulos
    out[:, :met_out.shape[1]] += met_out * 0.5
    return out


def camada_coro(g):
    coro = Faixa()
    c = 0
    while c < COMPASSOS:
        ac = ACORDES[c]
        k = 1
        while c + k < COMPASSOS and ACORDES[c + k] == ac:
            k += 1
        r = RAIZ_GUITARRA[ac] + 12
        for m in (r, r + 12, r + 12 + TERCA[ac], r + 19):
            coro.soma(coro_nota(m, k * 16 * PASSO, g), tempo(c * 16))
        c += k
    coro_out = reverb(formantes_ah(coro.buf), CAVERNA, 0.5)
    bat = Faixa()
    for c in range(COMPASSOS):
        p0 = c * 16
        if c >= 12:
            for s in range(16):
                bat.soma(bumbo(0.7, g), tempo(p0 + s, 1, g))
        if c % 2 == 0:
            bat.soma(prato(0.8, g, china=True), tempo(p0, 1, g))
    bat_out = reverb(bat.buf, SALA, 0.14)
    n = max(coro_out.shape[1], bat_out.shape[1])
    out = np.zeros((2, n))
    out[:, :coro_out.shape[1]] += coro_out
    out[:, :bat_out.shape[1]] += bat_out * 0.45
    return out


def camada_lenda(g):
    ev = eventos_melodia()
    harmonia = [(p, d, terca_acima(m, ACORDES[c]), resp, c) for (p, d, m, resp, c) in ev]
    n_total = L + CAUDA
    pistas = []
    for lado, linha in ((-0.35, ev), (0.35, harmonia)):
        freq, amp = linha_continua(linha, -12, 'abaixo', n_total, g, glide=0.02, vib_semitons=0.45, vib_hz=6.0)
        fonte = serra(freq) * 0.7 + pulso(freq, 0.5) * 0.3
        x = amplificador(fonte * amp * 0.25, ganho=22)
        pistas.append(pan(x, lado))
    solo = eco(pistas[0] + pistas[1], 0.3, 0.3, 0.2)
    solo = reverb(solo, SALAO, 0.25)
    tons = Faixa()
    for c in range(3, COMPASSOS, 4):
        p0 = c * 16
        for i, s in enumerate(range(8, 16)):
            f0 = (220, 180, 150, 120)[i // 2]
            tons.soma(tom_tom(0.75 + 0.03 * i, f0, g), tempo(p0 + s, 1.5, g), (0.5, 0.2, -0.2, -0.5)[i // 2])
        tons.soma(prato(0.9, g), tempo((c + 1) * 16, 1, g))
    tons_out = reverb(tons.buf, SALA, 0.2)
    n = max(solo.shape[1], tons_out.shape[1])
    out = np.zeros((2, n))
    out[:, :solo.shape[1]] += solo
    out[:, :tons_out.shape[1]] += tons_out * 0.6
    return out


CAMADAS = [
    ('Base', camada_base, -23.0),
    ('Tambores', camada_taiko, -19.0),
    ('Baixo e darbuka', camada_baixo_darbuka, -19.5),
    ('Guitarras', camada_guitarras, -18.5),
    ('Bateria e cordas', camada_bateria_cordas, -19.0),
    ('Aulos e metais', camada_aulos_metais, -20.0),
    ('Coro', camada_coro, -21.0),
    ('Lenda', camada_lenda, -20.5),
]


# ---------------------------------------------------------------- saída
def para_saida(x):
    """48k -> 32k sem emenda no loop (reamostra em círculo)."""
    k = 3 * 200
    ext = np.concatenate([x[:, -k:], x, x[:, :k]], axis=1)
    y = signal.resample_poly(ext, 2, 3, axis=1)
    return y[:, 400:400 + x.shape[1] * 2 // 3]


def gravar(caminho, y, g):
    y = np.clip(y + (g.random(y.shape) - g.random(y.shape)) / 32768, -1, 1)
    pcm = np.round(y.T * 32767).astype('<i2')
    with wave.open(caminho, 'wb') as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR_SAIDA)
        w.writeframes(pcm.tobytes())


def main():
    global SALA, SALAO, CAVERNA
    inicio = time.time()
    SALA = resposta(0.9, 0.008, 7000, 11)
    SALAO = resposta(2.6, 0.02, 4500, 12)
    CAVERNA = resposta(4.0, 0.03, 3500, 13)
    os.makedirs(SAIDA, exist_ok=True)
    os.makedirs(PREVIA, exist_ok=True)
    faixas = []
    for i, (nome, fn, alvo) in enumerate(CAMADAS):
        t0 = time.time()
        x = dobrar(fn(np.random.default_rng(1184 + i)))
        x = hp(x, 28)
        x *= 10 ** ((alvo - rms_db(x)) / 20)
        x = limitador(x, alvo + 13)
        x *= 10 ** ((alvo - rms_db(x)) / 20)
        faixas.append(x)
        print(f'camada {i} {nome:18s} pronta em {time.time() - t0:5.1f} s', flush=True)
    soma = sum(faixas)
    pico_total = np.abs(soma).max()
    ganho = 0.89 / pico_total
    faixas = [x * ganho for x in faixas]
    g = np.random.default_rng(7)
    saidas = [para_saida(x) for x in faixas]
    for i, y in enumerate(saidas):
        gravar(os.path.join(SAIDA, f'Camada_{i}.wav'), y, g)
    print(f'ganho geral {20 * np.log10(ganho):+.1f} dB; amostras por faixa {saidas[0].shape[1]}')
    print('camada  RMS sozinha  pico sozinha  | mistura até aqui: RMS  pico')
    acc = np.zeros_like(saidas[0])
    for i, y in enumerate(saidas):
        acc = acc + y
        print(f'  {i}     {rms_db(y):6.1f} dB   {20 * np.log10(np.abs(y).max()):6.1f} dB   |  {rms_db(acc):6.1f} dB  {20 * np.log10(np.abs(acc).max()):6.1f} dB')
    # prévia: fora de combate, depois D, C, B, A, S, SS, SSS (meio loop cada) e a queda ao apanhar
    meio = saidas[0].shape[1] // 2
    roteiro = [('fora de combate', 1, 0.0)] + [(r, k + 2, 0.0) for k, r in enumerate(['D', 'C', 'B', 'A', 'S', 'SS', 'SSS'])]
    roteiro += [('SSS (de novo)', 8, 0.0), ('apanhou: cai para B', 4, 0.0)]
    pedacos = []
    for k, (_, camadas, _) in enumerate(roteiro):
        ini = (k % 2) * meio
        seg = sum(saidas[j][:, ini:ini + meio] for j in range(camadas))
        if k == 0:
            seg = seg * 0.7
        pedacos.append(seg)
    previa = np.concatenate(pedacos, axis=1)
    fade = int(1.5 * SR_SAIDA)
    previa[:, -fade:] *= np.linspace(1, 0, fade)
    gravar(os.path.join(PREVIA, 'previa_D_ate_SSS.wav'), previa, g)
    with open(os.path.join(PREVIA, 'roteiro.txt'), 'w', encoding='utf-8') as f:
        for k, (nome, camadas, _) in enumerate(roteiro):
            f.write(f'{k * meio / SR_SAIDA:5.1f} s  {nome} ({camadas} camadas)\n')
    espectrograma(previa.mean(axis=0), os.path.join(PREVIA, 'espectrograma.png'))
    print(f'prévia: {previa.shape[1] / SR_SAIDA:.1f} s; tudo em {time.time() - inicio:.0f} s')


def espectrograma(x, caminho):
    """Imagem para conferir a prévia de olho: tempo na horizontal, frequência (log) na vertical."""
    try:
        from PIL import Image
    except ImportError:
        return
    f, t, s = signal.spectrogram(x, SR_SAIDA, nperseg=2048, noverlap=1536)
    s = 10 * np.log10(s + 1e-12)
    linhas = np.geomspace(30, 15000, 360)
    img = np.stack([np.interp(linhas, f, s[:, j]) for j in range(s.shape[1])], axis=1)[::-1]
    img = np.clip((img + 110) / 80, 0, 1)
    rgb = np.stack([img ** 0.7, img ** 1.6, 0.35 * img + 0.35 * img ** 3], axis=-1)
    Image.fromarray((rgb * 255).astype(np.uint8)).resize((1600, 360)).save(caminho)


if __name__ == '__main__':
    sys.exit(main())
