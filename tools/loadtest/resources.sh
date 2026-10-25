#!/usr/bin/env bash
# Spec 005 T013: amostra CPU (%) e memória (MiB) de containers a cada 10 s até ser interrompido.
# Uso: resources.sh <saida.csv> <container> [container...]   (CONTAINER_ENGINE=docker|podman, padrão podman)
set -euo pipefail
out=$1; shift
engine=${CONTAINER_ENGINE:-podman}
echo "horario,container,cpu_pct,mem_mib" > "$out"
while true; do
  now=$(date -u +%H:%M:%S)
  "$engine" stats --no-stream --format '{{.Name}},{{.CPUPerc}},{{.MemUsage}}' "$@" 2>/dev/null | while IFS=, read -r name cpu mem; do
    used=${mem%% /*}
    mib=$(awk -v v="$used" 'BEGIN{n=v+0; u=v; gsub(/[0-9.]/,"",u); if(u~/^G/)n*=1024; else if(u~/^k/)n/=1024; else if(u~/^B/)n/=1048576; printf "%.0f", n}')
    echo "$now,$name,${cpu%\%},$mib" >> "$out"
  done
  sleep 10
done
