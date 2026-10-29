#!/usr/bin/env bash
# Hook de deploy do Dokpod em host com compose (docs/runbooks/deploy-projeto.md), chamado por
# .github/workflows/deploy.yml via SSH. Só aplica imagens pelos digests do release-manifest.json validado;
# nunca aceita tag, versão ou digest livres.
#
# Uso:
#   compose-deploy-hook.sh apply <release-manifest.json> <arquivo-token> <usuário-registro>
#   compose-deploy-hook.sh check <release-manifest.json> <versão-esperada>
#
# Configuração do host (DOKPOD_DEPLOY_CONFIG, padrão ~/.config/dokpod-deploy/deploy.env), por exemplo:
#   COMPOSE_DIR=/srv/dokpod            # pasta do compose
#   COMPOSE_FILE=compose.yaml          # compose base; o hook grava compose-release.yaml ao lado
#   ENV_FILE=/srv/dokpod/dokpod.env    # --env-file do compose
#   PROJECT=dokpod                     # nome do projeto (containers <projeto>_<serviço>_1)
#   PROFILES=agent-podman              # perfis extras, separados por vírgula (opcional)
#   SERVICES="dokpod-api=api dokpod-bff=bff dokpod-web=web dokpod-agent=agent-podman"
#   ORDER="bff api web agent-podman"   # ordem de remoção (dependentes primeiro)
# O arquivo pode exportar variáveis exigidas pelo compose e preparar PATH.
#
# Idempotente: serviço cujo container já roda a imagem do digest do manifesto não é recriado.
set -euo pipefail

config=${DOKPOD_DEPLOY_CONFIG:-$HOME/.config/dokpod-deploy/deploy.env}
[[ -f "$config" ]] || { echo "Configuração ausente: $config" >&2; exit 1; }
# shellcheck source=/dev/null
. "$config"
: "${COMPOSE_DIR:?}" "${COMPOSE_FILE:?}" "${ENV_FILE:?}" "${PROJECT:?}" "${SERVICES:?}" "${ORDER:?}"

mode=${1:-}; manifest=${2:-}
[[ -f "$manifest" ]] || { echo "Manifesto ausente: $manifest" >&2; exit 2; }

# Linhas "<serviço> <referência>@<digest> <versão>" para as imagens mapeadas em SERVICES.
entries() {
  python3 - "$manifest" "$SERVICES" <<'PY'
import json, re, sys
manifest = json.load(open(sys.argv[1]))
mapping = dict(item.split("=", 1) for item in sys.argv[2].split())
seen = set()
for artifact in manifest["artifacts"]:
    if artifact["kind"] != "container" or artifact["name"] not in mapping:
        continue
    if not re.fullmatch(r"sha256:[0-9a-f]{64}", artifact["digest"]):
        sys.exit(f"digest inválido para {artifact['name']}")
    seen.add(artifact["name"])
    print(mapping[artifact["name"]], f"{artifact['reference']}@{artifact['digest']}", manifest["version"])
missing = set(mapping) - seen
if missing:
    sys.exit(f"manifesto sem as imagens {sorted(missing)}")
PY
}

container() { echo "${PROJECT}_${1}_1"; }
# Compara pelo ID da imagem: a mesma imagem pode estar no host com mais de um digest (por exemplo, após
# podman load), e o container reporta só um deles.
running_image() { podman inspect -f '{{.Image}}' "$(container "$1")" 2>/dev/null || true; }
image_id() { podman image inspect -f '{{.Id}}' "$1" 2>/dev/null || true; }

compose() {
  local args=(--env-file "$ENV_FILE" -f "$COMPOSE_FILE" -f compose-release.yaml)
  local profile
  for profile in ${PROFILES//,/ }; do args=(--profile "$profile" "${args[@]}"); done
  (cd "$COMPOSE_DIR" && podman-compose "${args[@]}" "$@")
}

apply() {
  local token=$1 user=$2 auth stale=() service reference version
  auth=$(dirname "$token")/auth.json
  podman login ghcr.io --username "$user" --password-stdin --authfile "$auth" < "$token" > /dev/null
  trap 'rm -f "$auth"' RETURN
  {
    echo "# Gerado por compose-deploy-hook.sh: imagens da release por digest."
    echo "services:"
    while read -r service reference version; do
      podman pull --quiet --authfile "$auth" "$reference" > /dev/null
      printf '  %s:\n    image: %s\n' "$service" "$reference"
      [[ "$(running_image "$service")" == "$(image_id "$reference")" ]] || stale+=("$service")
    done < <(entries)
  } > "$COMPOSE_DIR/compose-release.yaml.new"
  mv "$COMPOSE_DIR/compose-release.yaml.new" "$COMPOSE_DIR/compose-release.yaml"

  if [[ ${#stale[@]} -eq 0 ]]; then
    echo "Todos os serviços já rodam os digests da release; nada recriado."
    return
  fi
  echo "Recriando: ${stale[*]}"
  for service in $ORDER; do
    if [[ " ${stale[*]} " == *" $service "* ]]; then podman rm -f "$(container "$service")" > /dev/null 2>&1 || true; fi
  done
  compose up -d --no-build --no-deps "${stale[@]}" > /dev/null
}

check() {
  local expected=$1 service reference version failed=0 status image label
  while read -r service reference version; do
    [[ "$version" == "$expected" ]] || { echo "manifesto é da versão $version, esperado $expected"; failed=1; }
    for _ in $(seq 1 60); do
      status=$(podman inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$(container "$service")" 2>/dev/null || echo ausente)
      [[ "$status" == healthy || "$status" == running ]] && break
      sleep 2
    done
    image=$(running_image "$service")
    label=$(podman image inspect -f '{{index .Labels "org.opencontainers.image.version"}}' "$reference" 2>/dev/null || true)
    if [[ "$status" != healthy && "$status" != running ]] || [[ -z "$image" || "$image" != "$(image_id "$reference")" ]] || [[ "$label" != "$expected" ]]; then
      echo "FALHA $service: estado=$status imagem=${image:0:12} versão=${label:-?}"
      failed=1
    else
      echo "OK $service: $status, ${reference#*@}, versão $label"
    fi
  done < <(entries)
  return "$failed"
}

case "$mode" in
  apply) apply "${3:?arquivo do token}" "${4:?usuário do registro}" ;;
  check) check "${3:?versão esperada}" ;;
  *) echo "Uso: $0 apply|check <manifesto> ..." >&2; exit 2 ;;
esac
