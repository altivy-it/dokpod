#!/usr/bin/env bash
# Aposenta os ambientes "Carga NNN" do laboratório (spec 005 T014, lacuna L5 do threat model consolidado):
# revoga as identidades dos agentes simulados, desabilita os ambientes e remove do Keycloak os recursos
# `urn:dokpod:environment:{id}` e as permissions compartilhadas `carga-*` que os autorizavam. As policies de
# grupo ficam: são as mesmas dos ambientes reais.
#
# Os ambientes não são apagados: a auditoria é append-only e os comandos têm FK RESTRICT para o ambiente,
# então o histórico da carga continua consultável. Só para laboratório: escreve direto no banco.
#
# Uso: retire-load-hosts.sh [--apply] [container-postgres] [url-do-keycloak]
# Sem --apply, só lista o que seria feito. O segredo do cliente `dokpod-provisioner` vem de
# DOKPOD_PROVISIONER_CLIENT_SECRET ou de ~/.microsoft/usersecrets/Dokpod/.env.
set -euo pipefail

apply=false
if [[ ${1:-} == --apply ]]; then apply=true; shift; fi
pg=${1:-altivy-identity_postgres_1}
kc=${2:-https://127.0.0.1:7443}
realm=dokpod

psql() { podman exec -i "$pg" sh -c 'psql -U "$POSTGRES_USER" -d keycloak -v ON_ERROR_STOP=1 -At' ; }

mapfile -t ids < <(echo "select environment_id from dokpod.environments where name like 'Carga %' order by name;" | psql)
echo "${#ids[@]} ambientes 'Carga' encontrados."
[[ ${#ids[@]} -gt 0 ]] || exit 0

if ! $apply; then
  echo "select count(*) filter (where enabled), count(*) filter (where i.revoked_at_utc is null)
    from dokpod.environments e left join dokpod.agent_identities i using (environment_id)
    where e.name like 'Carga %';" | psql | awk -F'|' '{print "Habilitados: " $1 "; identidades ativas: " $2}'
  echo "Rode com --apply para revogar, desabilitar e remover a autorização no Keycloak."
  exit 0
fi

echo "begin;
  update dokpod.agent_identities set revoked_at_utc = now()
    where revoked_at_utc is null and environment_id in (select environment_id from dokpod.environments where name like 'Carga %');
  update dokpod.environments set enabled = false where name like 'Carga %';
commit;" | psql > /dev/null
echo "Identidades revogadas e ambientes desabilitados."

secret=${DOKPOD_PROVISIONER_CLIENT_SECRET:-}
if [[ -z $secret && -f $HOME/.microsoft/usersecrets/Dokpod/.env ]]; then
  secret=$(sed -n 's/^DOKPOD_PROVISIONER_CLIENT_SECRET=//p' "$HOME/.microsoft/usersecrets/Dokpod/.env" | tr -d '"'\''\r')
fi
[[ -n $secret ]] || { echo "Defina DOKPOD_PROVISIONER_CLIENT_SECRET para remover a autorização no Keycloak." >&2; exit 1; }

# O certificado do Keycloak do laboratório é da CA local; fora do loopback a cadeia é validada normalmente.
tls=()
[[ $kc =~ ^https://(127\.0\.0\.1|localhost)(:|/|$) ]] && tls=(-k)
token=$(curl -sf "${tls[@]}" "$kc/realms/$realm/protocol/openid-connect/token" \
  --data-urlencode grant_type=client_credentials --data-urlencode client_id=dokpod-provisioner \
  --data-urlencode "client_secret=$secret" | jq -r .access_token)
unset secret
api() { curl -sf "${tls[@]}" -H "Authorization: Bearer $token" "$@"; }

client=$(api "$kc/admin/realms/$realm/clients?clientId=dokpod-api" | jq -r '.[0].id')
authz="$kc/admin/realms/$realm/clients/$client/authz/resource-server"
for id in "${ids[@]}"; do
  for rid in $(api "$authz/resource?name=urn:dokpod:environment:$id&exactName=true" | jq -r '.[]._id'); do
    api -X DELETE "$authz/resource/$rid" > /dev/null
  done
done
echo "Recursos removidos no Keycloak: ${#ids[@]}."

# O Keycloak já apaga a permission quando o último recurso dela é removido; isto limpa o que sobrar.
permissions=$(api "$authz/permission?max=-1" | jq -r '.[] | select(.name | startswith("carga-")) | .id')
for pid in $permissions; do
  api -X DELETE "$authz/permission/$pid" > /dev/null
done
echo "Permissions 'carga-*' restantes removidas: $(wc -w <<< "$permissions")."
