#!/usr/bin/env bash
# Joga todos os golpes do Pátroclo no treino e mede arma x elmo e capa x corpo.
# Uso: Ferramentas/colisoes.sh [-troia-sem-fotos] [-troia-so=TrechoDoIdDoGolpe]
# Saída: Logs/colisoes.txt, Logs/colisoes_quadros.csv e Capturas/Colisoes/. O editor do Unity não pode estar aberto.
cd "$(dirname "$0")/.."
UNITY="${UNITY:-$HOME/Unity/Hub/Editor/6000.3.25f1/Editor/Unity}"
rm -f Logs/colisoes.txt
__NV_PRIME_RENDER_OFFLOAD=1 __GLX_VENDOR_LIBRARY_NAME=nvidia "$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testFilter Troia.Tests.PatrocloColisoes.Medir -testResults "$PWD/Logs/colisoes-results.xml" -logFile Logs/colisoes.log "$@"
codigo=$?
grep -E "error CS|Exception" Logs/colisoes.log | sort -u | head -20
[ -f Logs/colisoes.txt ] && cat Logs/colisoes.txt || echo "sem relatório (saida $codigo, ver Logs/colisoes.log)"
exit $codigo
