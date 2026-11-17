#!/usr/bin/env bash
# Roda um método do editor do Unity sem janela. Uso: Ferramentas/unity.sh Classe.Metodo [args...]
# O editor do Unity não pode estar aberto neste projeto ao mesmo tempo.
set -u
cd "$(dirname "$0")/.."
UNITY="${UNITY:-$HOME/Unity/Hub/Editor/6000.3.25f1/Editor/Unity}"
LOG="${LOG:-Logs/unity_$(echo "$1" | tr -c 'A-Za-z0-9\n' '_').log}"
mkdir -p "$(dirname "$LOG")"
metodo="$1"; shift
"$UNITY" -batchmode -projectPath "$PWD" -executeMethod "$metodo" -quit -logFile "$LOG" -- "$@"
codigo=$?
grep -E "error CS|Exception:|^prancha |Pátroclo ligado" "$LOG" | sort -u | head -40
echo "saida $codigo ($LOG)"
exit $codigo
