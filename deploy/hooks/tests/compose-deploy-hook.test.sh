#!/usr/bin/env bash
# Testes do compose-deploy-hook.sh (bug deploy-hook-falso-sucesso) com podman e podman-compose falsos.
# Exige bash e python3; no CI roda num container python. Sem efeitos fora de um diretório temporário.
#
# Uso: compose-deploy-hook.test.sh [--help]
set -euo pipefail

if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
  sed -n '2,5p' "$0" | sed 's/^# \{0,1\}//'
  exit 0
fi

hook=$(cd "$(dirname "$0")/.." && pwd)/compose-deploy-hook.sh
root=$(mktemp -d)
trap 'rm -rf "$root"' EXIT
failures=0

digest=sha256:$(printf 'a%.0s' $(seq 1 64))
cat > "$root/manifest.json" <<JSON
{"version":"0.12.0","artifacts":[
  {"kind":"container","name":"dokpod-ce-api","reference":"ghcr.io/altivy-it/dokpod-ce-api","digest":"$digest"}
]}
JSON

# podman falso: o container "dokpod_api_1" roda a imagem do ID em $FAKE_RUNNING; a imagem do manifesto tem o ID
# "id-new" e o rótulo de versão em $FAKE_LABEL. Cada chamada vai para $FAKE_LOG.
mkdir -p "$root/bin" "$root/compose"
cat > "$root/bin/podman" <<'SH'
#!/usr/bin/env bash
echo "podman $*" >> "$FAKE_LOG"
case "$*" in
  login*) cat > /dev/null ;;
  pull*) ;;
  "inspect -f {{.Image}} dokpod_api_1") echo "$FAKE_RUNNING" ;;
  inspect*) echo healthy ;;
  "image inspect -f {{.Id}}"*) echo id-new ;;
  "image inspect -f"*) echo "$FAKE_LABEL" ;;
  rm*) ;;
esac
SH
cat > "$root/bin/podman-compose" <<'SH'
#!/usr/bin/env bash
echo "podman-compose $*" >> "$FAKE_LOG"
SH
chmod +x "$root/bin/podman" "$root/bin/podman-compose"
printf 'token' > "$root/token"

run() { # run <SERVICES> <modo> <args...>: grava a saída em $root/out e devolve o código do hook
  local services=$1; shift
  cat > "$root/deploy.env" <<ENV
COMPOSE_DIR=$root/compose
COMPOSE_FILE=compose.yaml
ENV_FILE=$root/compose/dokpod.env
PROJECT=dokpod
SERVICES="$services"
ORDER="api"
ENV
  : > "$root/calls.log"
  local code=0
  PATH="$root/bin:$PATH" DOKPOD_DEPLOY_CONFIG="$root/deploy.env" FAKE_LOG="$root/calls.log" \
    FAKE_RUNNING="${FAKE_RUNNING:-id-old}" FAKE_LABEL="${FAKE_LABEL:-0.12.0}" \
    bash "$hook" "$@" > "$root/out" 2>&1 || code=$?
  return "$code"
}

expect() { # expect <nome> <código esperado> <texto esperado na saída> -- <comando run ...>
  local name=$1 expected=$2 text=$3 code=0; shift 4
  "$@" || code=$?
  if [[ "$code" -ne "$expected" ]] || ! grep -qF -- "$text" "$root/out"; then
    echo "FALHOU: $name (código $code, esperado $expected; saída: $(tr '\n' ' ' < "$root/out"))"
    failures=$((failures + 1))
  else
    echo "ok: $name"
  fi
}

old="dokpod-api=api"
expect "apply recusa SERVICES com nomes fora do manifesto" 3 "nada aplicado" -- \
  run "$old" apply "$root/manifest.json" "$root/token" user
grep -q "podman-compose" "$root/calls.log" && { echo "FALHOU: apply recriou serviço com mapeamento inválido"; failures=$((failures + 1)); }
expect "check recusa SERVICES com nomes fora do manifesto" 3 "nada aplicado" -- \
  run "$old" check "$root/manifest.json" 0.12.0

expect "apply recria o serviço desatualizado" 0 "Recriando: api" -- \
  run "dokpod-ce-api=api" apply "$root/manifest.json" "$root/token" user
grep -q "podman-compose .* up -d --no-build --no-deps api" "$root/calls.log" || { echo "FALHOU: apply não chamou compose up"; failures=$((failures + 1)); }
FAKE_RUNNING=id-new expect "apply não recria o que já roda o digest" 0 "nada recriado" -- \
  run "dokpod-ce-api=api" apply "$root/manifest.json" "$root/token" user
FAKE_RUNNING=id-new expect "check aprova a versão esperada" 0 "OK api" -- \
  run "dokpod-ce-api=api" check "$root/manifest.json" 0.12.0
FAKE_RUNNING=id-old expect "check reprova o container com outra imagem" 1 "FALHA api" -- \
  run "dokpod-ce-api=api" check "$root/manifest.json" 0.12.0

if [[ "$failures" -gt 0 ]]; then
  echo "$failures teste(s) falharam."
  exit 1
fi
echo "Todos os testes passaram."
