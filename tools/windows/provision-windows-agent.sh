#!/usr/bin/env bash
# Identidade do agente Windows de laboratório (spec 006 T003): emite um certificado de cliente pela CA de
# laboratório para um ambiente já cadastrado no console, registra o fingerprint SHA-256 em
# dokpod.agent_identities e copia a CA para a pasta de saída. Só para laboratório: escreve direto no banco.
#
# Uso: provision-windows-agent.sh <environment-id> <pasta-de-saída> [pasta-da-CA] [container-postgres]
# A pasta de saída guarda a chave privada: mantenha-a fora do repositório e leve-a ao Windows por canal protegido.
# Saída: dokpod-agent-windows.pfx (sem senha, como os demais PFX de laboratório) e dokpod-dev-ca.crt.
set -euo pipefail
env_id=${1:?informe o UUID do ambiente cadastrado no console}
out=${2:?informe a pasta de saída}
ca_dir=${3:-$HOME/.microsoft/usersecrets/Dokpod/certs}
pg=${4:-altivy-identity_postgres_1}
[[ $env_id =~ ^[0-9a-fA-F-]{36}$ ]] || { echo "UUID inválido: $env_id" >&2; exit 1; }
psql() { podman exec -i "$pg" sh -c 'psql -U "$POSTGRES_USER" -d keycloak -v ON_ERROR_STOP=1 -At' ; }

name=$(echo "select name from dokpod.environments where environment_id = '$env_id';" | psql)
[[ -n $name ]] || { echo "Ambiente $env_id não cadastrado; cadastre-o no console antes." >&2; exit 1; }

mkdir -p "$out"
chmod 700 "$out"
key="$out/dokpod-agent-windows.key" crt="$out/dokpod-agent-windows.crt" pfx="$out/dokpod-agent-windows.pfx"
[[ ! -e $pfx ]] || { echo "$pfx já existe; use outra pasta para não sobrescrever uma identidade." >&2; exit 1; }
ext=$(mktemp)
trap 'rm -f "$ext" "$crt.csr"' EXIT
printf 'basicConstraints=CA:FALSE\nkeyUsage=digitalSignature,keyEncipherment\nextendedKeyUsage=clientAuth\n' > "$ext"
openssl req -new -newkey rsa:2048 -nodes -keyout "$key" -subj "/CN=dokpod-agent-windows" -out "$crt.csr" 2>/dev/null
openssl x509 -req -in "$crt.csr" -CA "$ca_dir/dokpod-dev-ca.crt" -CAkey "$ca_dir/dokpod-dev-ca.key" \
  -CAcreateserial -CAserial "$out/ca.srl" -days 90 -sha256 -extfile "$ext" -out "$crt" 2>/dev/null
openssl pkcs12 -export -in "$crt" -inkey "$key" -out "$pfx" -passout pass:
cp "$ca_dir/dokpod-dev-ca.crt" "$out/"
fp=$(openssl x509 -in "$crt" -noout -fingerprint -sha256 | cut -d= -f2 | tr -d :)
echo "insert into dokpod.agent_identities (certificate_fingerprint, environment_id) values ('$fp', '$env_id');" | psql > /dev/null
rm -f "$key" "$out/ca.srl"
chmod -R go-rwx "$out"
echo "Identidade do agente Windows para '$name' ($env_id)"
echo "  fingerprint SHA-256: $fp"
echo "  arquivos: $pfx e $out/dokpod-dev-ca.crt"
