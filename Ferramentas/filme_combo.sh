#!/usr/bin/env bash
# Joga um combo no treino e filma o golpe final quadro a quadro (câmera do jogo e de lado), montando uma prancha.
# Uso: Ferramentas/filme_combo.sh LLL [arma 0|1|2] [quadros] [variantes ID1,ID2]   (prefixo A = no ar: ALLP)
# Saída: Capturas/Filme/<combo>_<arma>[_<variante>]/prancha_jogo.png e prancha_lado.png
cd "$(dirname "$0")/.."
UNITY="${UNITY:-$HOME/Unity/Hub/Editor/6000.3.25f1/Editor/Unity}"
combo="${1:-LLL}"; arma="${2:-0}"; quadros="${3:-12}"; variantes="${4:-}"
rm -f Library/ilpp.pid
__NV_PRIME_RENDER_OFFLOAD=1 __GLX_VENDOR_LIBRARY_NAME=nvidia "$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testFilter Troia.Tests.FilmeDoCombo.Filmar -testResults "$PWD/Logs/filme-results.xml" -logFile Logs/filme.log \
  -troia-combo="$combo" -troia-arma="$arma" -troia-quadros="$quadros" -troia-variantes="$variantes"
grep -E "error CS|Exception" Logs/filme.log | sort -u | head -5
python3 -I - "Capturas/Filme/${combo}_${arma}" <<'PY'
import sys, glob, os
from PIL import Image
for pasta in sorted(glob.glob(sys.argv[1] + '*')):
  if not os.path.exists(pasta + '/quadros.txt'): continue
  for vista in ('jogo', 'lado'):
    fs = sorted(glob.glob(f'{pasta}/{vista}_*.png'))
    if not fs: continue
    w = 240; cols = 6; rows = (len(fs) + cols - 1) // cols
    o = Image.new('RGB', (w * cols, w * rows))
    for i, f in enumerate(fs): o.paste(Image.open(f).resize((w, w), Image.LANCZOS), ((i % cols) * w, (i // cols) * w))
    o.save(f'{pasta}/prancha_{vista}.png')
  print(open(f'{pasta}/quadros.txt').readline().strip())
PY
