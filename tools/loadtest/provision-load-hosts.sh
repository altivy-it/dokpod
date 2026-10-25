#!/usr/bin/env bash
# Identidades dos hosts de carga (spec 005 T014): para cada ambiente "Carga NNN" já cadastrado, emite um
# certificado de cliente pela CA de laboratório, grava o fingerprint SHA-256 em dokpod.agent_identities e
# gera o agents.json do simulador. Só para laboratório: escreve direto no banco.
#
# Uso: provision-load-hosts.sh <quantidade> <pasta-de-saída> [pasta-da-CA] [container-postgres]
# A pasta de saída guarda chaves privadas: mantenha-a fora do repositório.
set -euo pipefail

count=${1:?informe a quantidade de hosts}
out=${2:?informe a pasta de saída}
ca_dir=${3:-$HOME/.microsoft/usersecrets/Dokpod/certs}
pg=${4:-altivy-identity_postgres_1}

psql() { podman exec -i "$pg" sh -c 'psql -U "$POSTGRES_USER" -d keycloak -v ON_ERROR_STOP=1 -At' ; }

mkdir -p "$out/certs"
chmod 700 "$out"

mapfile -t envs < <(echo "select environment_id || '|' || name from dokpod.environments
  where name like 'Carga %' order by name limit $count;" | psql)
[[ ${#envs[@]} -eq $count ]] || { echo "Só ${#envs[@]} ambientes 'Carga' cadastrados; pedidos $count." >&2; exit 1; }

ext=$(mktemp)
trap 'rm -f "$ext"' EXIT
printf 'basicConstraints=CA:FALSE\nkeyUsage=digitalSignature,keyEncipherment\nextendedKeyUsage=clientAuth\n' > "$ext"

sql="begin;"
json="["
for entry in "${envs[@]}"; do
  id=${entry%%|*}
  name=$(echo "${entry#*|}" | tr 'A-Z ' 'a-z-')
  key="$out/certs/$name.key" crt="$out/certs/$name.crt" pfx="$out/certs/$name.pfx"
  openssl req -new -newkey rsa:2048 -nodes -keyout "$key" -subj "/CN=dokpod-$name" -out "$crt.csr" 2>/dev/null
  openssl x509 -req -in "$crt.csr" -CA "$ca_dir/dokpod-dev-ca.crt" -CAkey "$ca_dir/dokpod-dev-ca.key" \
    -CAcreateserial -CAserial "$out/ca.srl" -days 30 -sha256 -extfile "$ext" -out "$crt" 2>/dev/null
  rm -f "$crt.csr"
  openssl pkcs12 -export -in "$crt" -inkey "$key" -out "$pfx" -passout pass:
  fp=$(openssl x509 -in "$crt" -noout -fingerprint -sha256 | cut -d= -f2 | tr -d :)
  sql+="update dokpod.agent_identities set certificate_fingerprint='$fp', revoked_at_utc=null where environment_id='$id';"
  json+="{\"environmentId\":\"$id\",\"pfx\":\"/load/certs/$name.pfx\"},"
done
sql+="commit;"
echo "$sql" | psql > /dev/null
echo "${json%,}]" > "$out/agents.json"
chmod -R go-rwx "$out"
echo "$count identidades provisionadas em $out/agents.json"
