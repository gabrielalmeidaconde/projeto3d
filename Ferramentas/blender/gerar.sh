#!/usr/bin/env bash
# Refaz o Pátroclo de ponta a ponta: Unity (peças procedurais) -> Blender (capa simulada, crina, texturas) -> Unity.
#
#   Ferramentas/blender/gerar.sh            tudo
#   Ferramentas/blender/gerar.sh blender    só a parte do Blender em diante (a base do Unity já existe)
#   Ferramentas/blender/gerar.sh unity      só gera o personagem no Unity com o pacote que já existe
#
# Variáveis: UNITY (caminho do editor), BLENDER (padrão: blender), LOGS (pasta dos registros).
set -euo pipefail
cd "$(dirname "$0")/../.."
UNITY="${UNITY:-$HOME/Unity/Hub/Editor/6000.3.25f1/Editor/Unity}"
BLENDER="${BLENDER:-blender}"
LOGS="${LOGS:-Logs/patroclo}"
ETAPA="${1:-tudo}"
BLEND=Arte/Patroclo/Patroclo.blend
TEX=Assets/Troia/Personagens/Patroclo/Texturas/Blender
mkdir -p "$LOGS" "$TEX"

unity() { "$UNITY" -batchmode -projectPath "$PWD" -executeMethod "Troia.EditorTools.PatrocloBuilder.$1" -quit -logFile "$LOGS/unity_$1.log"; }
bl() { echo "== $1"; "$BLENDER" -b "${@:2}" -P "Ferramentas/blender/$1" > "$LOGS/${1%.py}.log" 2>&1 || { tail -20 "$LOGS/${1%.py}.log"; exit 1; }; grep -E "^\[" "$LOGS/${1%.py}.log" | tail -6 || true; }

if [ "$ETAPA" = tudo ]; then
  echo "== Unity: peças procedurais e base para o Blender"
  unity ExportBaseFromCommandLine
fi
if [ "$ETAPA" != unity ]; then
  python3 -I Ferramentas/texturas/crina.py "$TEX"
  python3 -I Ferramentas/blender/pintor_capa.py "$TEX"
  bl bl_montar.py --factory-startup
  bl bl_capa.py "$BLEND"
  bl bl_crina.py "$BLEND"
  bl bl_uv.py "$BLEND"
  bl bl_exportar.py "$BLEND"
  echo "== pintor (bronze e linho)"
  python3 -I Ferramentas/blender/pintor.py Assets/Troia/Personagens/Patroclo/Fonte/patroclo_blender "$TEX" 4096
  bl bl_materiais.py "$BLEND"
fi
echo "== Unity: personagem final e capturas"
unity BuildFromCommandLine
unity CaptureFromCommandLine
grep -E "\[Troia\]|error CS" "$LOGS/unity_BuildFromCommandLine.log" | sort -u | tail -12
