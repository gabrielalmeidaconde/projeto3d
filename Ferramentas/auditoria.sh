#!/usr/bin/env bash
# Fotografa cada golpe das armas em cinco momentos (Capturas/Auditoria) e monta pranchas por arma.
# Uso: Ferramentas/auditoria.sh [-troia-arma=N]. O editor do Unity não pode estar aberto neste projeto.
cd "$(dirname "$0")/.."
UNITY="${UNITY:-$HOME/Unity/Hub/Editor/6000.3.25f1/Editor/Unity}"
rm -rf Capturas/Auditoria; rm -f Library/ilpp.pid
__NV_PRIME_RENDER_OFFLOAD=1 __GLX_VENDOR_LIBRARY_NAME=nvidia "$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testFilter Troia.Tests.AuditoriaGolpes.Golpes -testResults "$PWD/Logs/auditoria-results.xml" -logFile Logs/auditoria.log "$@"
grep -E "error CS|Exception" Logs/auditoria.log | sort -u | head -10
python3 -I - <<'PY'
import glob, os
from PIL import Image, ImageDraw
pasta = 'Capturas/Auditoria'
fs = sorted(f for f in glob.glob(pasta + '/[0-9]_*.png'))
golpes = {}
for f in fs:
    nome = os.path.basename(f)[:-4]
    chave, k = nome.rsplit('_', 1)
    golpes.setdefault(chave, {})[int(k)] = f
for arma in sorted({c[0] for c in golpes}):
    lista = sorted(c for c in golpes if c[0] == arma)
    for parte in range(0, len(lista), 8):
        bloco = lista[parte:parte + 8]
        w, h = 216, 270
        o = Image.new('RGB', (w * 5, h * len(bloco)), (16, 16, 16))
        for i, chave in enumerate(bloco):
            for k in range(5):
                if k not in golpes[chave]: continue
                im = Image.open(golpes[chave][k]).crop((300, 30, 660, 480)).resize((w, h), Image.LANCZOS)
                if k == 0: ImageDraw.Draw(im).text((4, 4), chave[2:], fill=(255, 230, 0))
                o.paste(im, (k * w, i * h))
        o.save(f'{pasta}/prancha_{arma}_{parte // 8}.png')
print('pranchas prontas')
PY
