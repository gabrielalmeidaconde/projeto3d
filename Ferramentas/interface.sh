#!/usr/bin/env bash
# Passa pelas telas do jogo e salva imagens da interface em Capturas/Interface. O editor do Unity não pode estar aberto.
cd "$(dirname "$0")/.."
UNITY="${UNITY:-$HOME/Unity/Hub/Editor/6000.3.25f1/Editor/Unity}"
__NV_PRIME_RENDER_OFFLOAD=1 __GLX_VENDOR_LIBRARY_NAME=nvidia "$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode \
  -testFilter Troia.Tests.InterfaceCapturas.Telas -testResults "$PWD/Logs/interface-results.xml" -logFile Logs/interface.log "$@"
codigo=$?
grep -E "error CS|Exception|Assert" Logs/interface.log | sort -u | head -20
ls Capturas/Interface/*.png 2>/dev/null
exit $codigo
