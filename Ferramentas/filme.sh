#!/usr/bin/env bash
# Filma o herói quadro a quadro no treino (parado, correr, frear, virar, golpear) para caçar defeitos do pano e da crista.
# Uso: Ferramentas/filme.sh [-troia-pasta=Capturas/Filme] [-troia-lod0]
# Saída: quadros na pasta e Logs/filme.csv (agitação de cada pano). O editor do Unity não pode estar aberto.
cd "$(dirname "$0")/.."
UNITY="${UNITY:-$HOME/Unity/Hub/Editor/6000.3.25f1/Editor/Unity}"
rm -f Logs/filme.csv
__NV_PRIME_RENDER_OFFLOAD=1 __GLX_VENDOR_LIBRARY_NAME=nvidia "$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testFilter Troia.Tests.PatrocloFilme.Filmar -testResults "$PWD/Logs/filme-results.xml" -logFile Logs/filme.log "$@"
codigo=$?
grep -E "error CS" Logs/filme.log | sort -u | head -20
[ -f Logs/filme.csv ] && echo "ok: $(wc -l < Logs/filme.csv) quadros" || echo "sem filme (saida $codigo, ver Logs/filme.log)"
exit $codigo
