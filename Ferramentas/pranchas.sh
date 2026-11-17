#!/usr/bin/env bash
# Gera as pranchas de pose dos golpes, uma arma por processo (todas juntas esgotam a memória).
# Uso: Ferramentas/pranchas.sh [vista: frente|lado|tras] [armas...]   -> Capturas/Golpes/
cd "$(dirname "$0")/.."
vista="${1:-frente}"; shift || true
armas=("$@"); [ ${#armas[@]} -eq 0 ] && armas=(xifos lanca escudo base)
for a in "${armas[@]}"; do
  LOG="Logs/pranchas_$a.log" Ferramentas/unity.sh Troia.EditorTools.PatrocloJogo.Pranchas Capturas/Golpes "$vista" "$a" | grep -v "^prancha" 
done
