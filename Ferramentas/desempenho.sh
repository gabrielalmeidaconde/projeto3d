#!/usr/bin/env bash
# Mede o custo de desenhar a fase em vários pontos do mapa (batches, triângulos, tempo de quadro).
# Uso: Ferramentas/desempenho.sh [-troia-perf-variantes] [-troia-perf-fotos]
# Saída: Logs/desempenho.txt (e Capturas/Desempenho/ com -troia-perf-fotos). O editor do Unity não pode estar aberto.
cd "$(dirname "$0")/.."
UNITY="${UNITY:-$HOME/Unity/Hub/Editor/6000.3.25f1/Editor/Unity}"
rm -f Logs/desempenho.txt
__NV_PRIME_RENDER_OFFLOAD=1 __GLX_VENDOR_LIBRARY_NAME=nvidia "$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testFilter Troia.Tests.Desempenho.Medir -testResults "$PWD/Logs/desempenho-results.xml" -logFile Logs/desempenho.log "$@"
codigo=$?
grep -E "error CS|Exception" Logs/desempenho.log | sort -u | head -20
[ -f Logs/desempenho.txt ] && column -s';' -t Logs/desempenho.txt || echo "sem relatório (saida $codigo, ver Logs/desempenho.log)"
exit $codigo
