#!/usr/bin/env bash
# Exercício local da assinatura e da proveniência das imagens (spec 005 T012, gate 3), sem efeito externo.
# Registro descartável e cosign numa rede Podman interna (sem saída para a internet): nada vai ao Rekor,
# ao Fulcro ou a registro público. As chaves são geradas na hora e apagadas no fim.
#
# Casos positivos: assinatura e atestado de proveniência verificados pelo digest.
# Casos negativos: chave errada, tag movida para outro digest, imagem sem assinatura, atestado de outro tipo,
# chave retirada da confiança (revogação de chave) e assinatura adulterada no registro.
#
# Uso: exercise-signing-local.sh [imagem-assinada] [outra-imagem] [pasta-de-trabalho]
# No Podman rootless, os containers rodam com o usuário de quem chama (`--userns keep-id`) para ler e gravar a pasta.
# Saída: uma linha PASS/FAIL por caso; código 1 se algum caso divergir do esperado.
set -euo pipefail

signed=${1:-localhost/dokpod-api:e2e}
other=${2:-localhost/dokpod-agent:e2e}
work=${3:-$(mktemp -d)}
net=dok005-signing
registry=dok005-signing-registry
cosign_image=gcr.io/projectsigstore/cosign:v3.1.3
skopeo_image=quay.io/skopeo/stable:v1.18.0
registry_image=docker.io/library/registry:2
repo=$registry:5000/dokpod-api
failures=0
# Chaves descartáveis do exercício, sem senha.
export COSIGN_PASSWORD=''

cleanup() {
  podman rm -f "$registry" > /dev/null 2>&1 || true
  podman network rm -f "$net" > /dev/null 2>&1 || true
  rm -rf "$work"
}
trap cleanup EXIT

mkdir -p "$work"
chmod 700 "$work"
podman network create --internal "$net" > /dev/null
podman run -d --name "$registry" --network "$net" "$registry_image" > /dev/null

in_net() { podman run --rm --network "$net" --userns keep-id --user "$(id -u):$(id -g)" -v "$work":/w:z -w /w -e COSIGN_PASSWORD "$@"; }
cosign() { in_net "$cosign_image" "$@"; }
push() { # push <imagem-local> <tag>
  podman save -q --format oci-archive -o "$work/image.tar" "$1" > /dev/null
  in_net "$skopeo_image" copy -q --dest-tls-verify=false oci-archive:/w/image.tar "docker://$repo:$2" > /dev/null
  rm -f "$work/image.tar"
}
digest_of() { in_net "$skopeo_image" inspect --tls-verify=false --format '{{.Digest}}' "docker://$repo:$1"; }
expect() { # expect <pass|fail> <nome> <comando...>
  local want=$1 name=$2; shift 2
  if "$@" > "$work/last.log" 2>&1; then got=pass; else got=fail; fi
  if [[ $got == "$want" ]]; then
    echo "PASS  $name (esperado: $want)"
    # Motivo da recusa, para conferir que o caso negativo falhou pela razão certa.
    [[ $got == fail ]] && grep -m1 -iE "error|no matching|invalid|denied|not found" "$work/last.log" | sed 's/^/      motivo: /' || true
  else echo "FAIL  $name (esperado: $want, obtido: $got)"; sed 's/^/      /' "$work/last.log" | tail -5; failures=$((failures + 1)); fi
}
verify() { cosign verify --key "$1" --insecure-ignore-tlog=true --allow-http-registry --allow-insecure-registry "$2"; }
verify_attestation() { cosign verify-attestation --key "$1" --type "$2" --insecure-ignore-tlog=true --allow-http-registry --allow-insecure-registry "$3"; }

cosign generate-key-pair --output-key-prefix release > /dev/null 2>&1
cosign generate-key-pair --output-key-prefix other > /dev/null 2>&1
cosign generate-key-pair --output-key-prefix rotated > /dev/null 2>&1

push "$signed" v0.0.0-exercise
d1=$(digest_of v0.0.0-exercise)
cosign sign --key release.key --tlog-upload=false --use-signing-config=false --allow-http-registry --allow-insecure-registry --yes "$repo@$d1" > /dev/null 2>&1
cat > "$work/provenance.json" <<EOF
{"buildDefinition":{"buildType":"https://dokpod.local/exercise","externalParameters":{"ref":"refs/tags/v0.0.0-exercise"}},"runDetails":{"builder":{"id":"local-exercise"}}}
EOF
cosign attest --key release.key --type slsaprovenance1 --predicate provenance.json --tlog-upload=false --use-signing-config=false \
  --allow-http-registry --allow-insecure-registry --yes "$repo@$d1" > /dev/null 2>&1

expect pass "assinatura válida pelo digest"            verify release.pub "$repo@$d1"
expect pass "atestado de proveniência pelo digest"      verify_attestation release.pub slsaprovenance1 "$repo@$d1"
expect fail "chave errada"                              verify other.pub "$repo@$d1"
expect fail "atestado com chave errada"                 verify_attestation other.pub slsaprovenance1 "$repo@$d1"
expect fail "atestado de outro tipo"                    verify_attestation release.pub spdxjson "$repo@$d1"
expect fail "chave retirada da confiança (rotação)"     verify rotated.pub "$repo@$d1"

push "$other" v0.0.0-exercise
d2=$(digest_of v0.0.0-exercise)
[[ $d1 != "$d2" ]] || { echo "FAIL  a tag não mudou de digest"; failures=$((failures + 1)); }
expect fail "tag movida para outro digest"              verify release.pub "$repo:v0.0.0-exercise"
expect pass "digest original continua verificável"      verify release.pub "$repo@$d1"

push "$signed" unsigned
podman save -q --format oci-archive -o "$work/image.tar" "$other" > /dev/null
in_net "$skopeo_image" copy -q --dest-tls-verify=false oci-archive:/w/image.tar "docker://$registry:5000/dokpod-unsigned:v0.0.0-exercise" > /dev/null
rm -f "$work/image.tar"
expect fail "imagem sem assinatura"                     verify release.pub "$registry:5000/dokpod-unsigned:v0.0.0-exercise"

# Adulteração: o cosign v3 publica as assinaturas e atestados como referrers, no índice da tag
# `sha256-<digest>`. Copiar o índice de d1 para d2 apresenta a d2 assinaturas feitas sobre outro conteúdo.
in_net "$skopeo_image" copy -q --all --src-tls-verify=false --dest-tls-verify=false \
  "docker://$repo:sha256-${d1#sha256:}" "docker://$repo:sha256-${d2#sha256:}" > /dev/null
expect fail "assinatura de outro digest copiada"        verify release.pub "$repo@$d2"

echo "Digest assinado: $d1"
[[ $failures -eq 0 ]] && echo "Todos os casos conferem." || { echo "$failures caso(s) divergiram."; exit 1; }
