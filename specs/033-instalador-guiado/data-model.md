# Modelo de dados: Instalador guiado do Dokpod

**Spec**: [spec.md](spec.md) · **Pesquisa**: [research.md](research.md)

O instalador não cria tabelas novas no banco do Dokpod. Os comandos administrativos ([contrato](contracts/admin-commands.md))
usam `dokpod.environments`, `dokpod.agent_identities`, `dokpod.audit_events` e `dokpod."__EFMigrationsHistory"`, que já
existem. O estado do instalador fica em arquivos no host.

## Layout no host

| Caminho (padrão, usuário rootless) | Conteúdo | Permissão |
| --- | --- | --- |
| `~/.local/share/dokpod` (`installDir`) | `compose.yaml`, `dokpod.env` (sem segredos), `edge/`, `deploy.env`, `answers.yaml`, `state/` (`installation.db` e `backups/`), `sql-override/` (cópia conferida do último `--sql-dir`, para reexecução e auditoria, não secreta), `reports/`, `packages/` | `0750` |
| `~/.config/dokpod/secrets` (`secretsDir`) | Um arquivo por segredo e a pasta `pki/` (chave da CA local e PFX) | `0700`, arquivos `0600` |

`installDir` e `secretsDir` precisam ser diferentes e nenhum pode ficar dentro de um checkout Git. O instalador recusa
se estiverem.

## Estado da instalação (SQLite)

Banco `installDir/state/installation.db` ([D-014](research.md#d-014-estado-da-instalação-em-sqlite), FR-044). O schema
fica em `deploy/installer/sql/state/schema/`, e a versão em `PRAGMA user_version`.

| Tabela | Colunas principais | Regra |
| --- | --- | --- |
| `installation` | `installation_id` (UUID, PK, linha única), `version` (semver), `edition` (`ce`\|`business`), `engine` (`docker-rootless`\|`podman-rootless`), `public_url`, `created_at`, `updated_at` | `installation_id` nunca muda. `version` só aumenta (FR-032). Trocar `edition` ou `public_url` exige confirmação. |
| `components` | `name` (PK, enum de [Componente](#componente)), `origin`, `desired_hash`, `observed_json`, `recreatable` | Uma linha por item convergido. |
| `certificates` | `role` (PK), `origin`, `subject_alt_names_json`, `not_after`, `fingerprint_sha256`, `secret_ref`, `ca_state` (`active`\|`rotating`) | Ver [Certificado gerenciado](#certificado-gerenciado). |
| `environments` | `environment_id` (UUID, PK), `name`, `engine` (`docker`\|`podman`\|`windows`), `agent_fingerprint`, `package_generated_at`, `revoked_at` | Primeiro ambiente e hosts adicionais. |
| `sql_overrides` | `id` (PK), `path`, `purpose`, `original_sha256`, `applied_sha256`, `accepted_schema_fingerprint`, `confirmed_by`, `confirmed_at`, `run_id` (FK) | Desvios do catálogo SQL confirmados (FR-041, FR-042). Só recebe inserções; é o que `verify` aceita. |
| `runs` | `run_id` (UUID, PK), `command`, `started_at`, `finished_at`, `outcome`, `exit_code`, `report_path` | `outcome` em `converged`, `changed`, `prerequisite_failed`, `declined`, `failed`, `paused_for_manual_migration`. Retenção: as 200 últimas execuções e seus relatórios, preservando sempre as que têm `sql_overrides`. O volume é de uma linha por execução, então a regra de particionamento da constituição não se aplica. |
| `run_steps` | `run_id` (FK), `step`, `status` (`done`\|`failed`\|`skipped`), `at` | Gravado numa transação só depois da etapa terminar; apoia a retomada, mas o plano é sempre recalculado do observado. |
| `run_lock` | `id` (PK, sempre 1), `run_id`, `container_name`, `acquired_at` | Adquirido em `BEGIN IMMEDIATE`; ocupado dá código 7; velho (container inexistente) só é liberado com confirmação. |

Nenhuma coluna guarda segredo. `secret_ref` é só um caminho relativo a `secretsDir`.

## Componente

| Campo | Tipo | Regra |
| --- | --- | --- |
| `name` | enum | `dokpod-postgres`, `keycloak-postgres`, `keycloak`, `edge`, `web`, `bff`, `api`, `agent`, `realm`, `migrations`, `pki`, `secrets`. |
| `origin` | `provisioned` \| `existing` | `existing` nunca é removido nem recriado pelo instalador. |
| `desiredHash` | SHA-256 | Hash da configuração desejada, sem segredos. Hash igual mais imagem igual dá `keep`. |
| `observed` | objeto | Último estado lido: imagem e digest, health, versão. |
| `recreatable` | booleano | `false` para `existing`. |

Ações do plano: `create`, `update`, `keep`, `recreate`, `remove`. `remove` de componentes com dados (`dokpod-postgres`,
`keycloak-postgres`, volumes `dokpod-api-keys` e `dokpod-bff-keys`, identidades) só existe na recriação total com segunda
confirmação (FR-029).

## Arquivo de respostas (`answers.yaml`)

O esquema completo está em [contracts/answers.schema.json](contracts/answers.schema.json).

- **Segredos**: nenhum campo aceita segredo em texto. Segredos só entram como
  `{ "file": "<caminho absoluto>" }` ou `{ "env": "<NOME>" }` (FR-002). Esse arquivo ou variável não é copiado; o
  instalador lê o valor e grava em `secretsDir` o que a instalação precisa manter.
- **Credenciais administrativas**: as do PostgreSQL e do Keycloak existentes são lidas e descartadas ao fim da execução
  (FR-008, FR-015).
- **Respostas da execução guiada**: o modo guiado grava `answers.yaml` em `installDir` para reexecução não interativa.

## Catálogo SQL

Fonte: `backend/libs/Dokpod.ControlPlane.Infrastructure/Sql/manifest.json`
([D-013](research.md#d-013-catálogo-de-arquivos-sql-e-sobreposição-externa)).

| Campo | Tipo | Regra |
| --- | --- | --- |
| `version` | semver | Igual à da release; um diretório sobreposto com outra versão é recusado. |
| `files[].path` | caminho relativo à raiz do catálogo | Único. Migrations em `Migrations/Script/<id>/Up\|Down/*.sql`; o restante em `Installation/{bootstrap,checks,status}/`. Na fonte, `Installation/` fica em `Sql/` do projeto de infraestrutura; na imagem, tudo fica em `/app/sql`. |
| `files[].purpose` | `migration` \| `bootstrap` \| `check` \| `status` | |
| `files[].executor` | `ef` \| `psql` \| `admin` | `psql` usa variáveis `:"x"` e `:'x'`; `admin` usa `@x`; nenhum aceita concatenação. |
| `files[].parameters` | lista de `{name, kind: identifier\|literal\|secret}` | Um `secret` só vem de arquivo, nunca da linha de comando. |
| `files[].sha256` | hex | Conferido no build e na carga. |

Impressão do schema: `schema/expected-schema.json` por versão (FR-042), lida por
`Installation/checks/schema-fingerprint.sql`. Ela tem `objects[]` (`kind` em
`table|view|column|index|constraint|function|trigger`, `name`, `definitionSha256`) e `sha256` do conjunto. Ficam de fora
as partições criadas em execução, o que é herdado delas (`conparentid`, `tgparentid`), as restrições `NOT NULL` (já
na coluna) e as concessões, para que a impressão seja a mesma no PostgreSQL 17 e no 18.

Sobreposição: um arquivo com o mesmo `path` no `--sql-dir` substitui o da release, depois da confirmação nomeada, e é
registrado em `sql_overrides`.

## Certificado gerenciado

| Campo | Tipo | Regra |
| --- | --- | --- |
| `role` | `ca` \| `edge` \| `api` \| `agent:<environmentId>` | |
| `origin` | `local-ca` \| `organization` | `organization` nunca é reemitido; vencendo, o instalador só avisa. |
| `subjectAltNames` | lista | Validados contra o uso (D-005). |
| `notAfter` | instante | Aviso a partir de `renewWarningDays` (padrão 30). |
| `fingerprintSha256` | hex | Para agentes, é o que vai para `dokpod.agent_identities`. |
| `secretRef` | caminho relativo a `secretsDir` | Chave privada ou PFX; nunca em `installDir`. |

Transição da CA (FR-030): `active` → `rotating` (bundle com a raiz antiga e a nova em
`DOKPOD_API_CLIENT_CA_CERTIFICATE_PATH` e na confiança dos agentes) → `active` (nova) após todos os agentes reemitidos e
conectados, conforme [troca da CA](../../docs/runbooks/troca-da-ca-dos-agentes.md).

## Segredo da instalação

| Nome no `secretsDir` | Consumidor | Gerado quando |
| --- | --- | --- |
| `ConnectionStrings__ControlPlane` | API (role de login do runtime, padrão `dokpod_app`, membro do grupo `dokpod_runtime` das migrations) | Cenário com PostgreSQL provisionado ou existente. |
| `dokpod-owner-connection` | modo administrativo (role `dokpod_owner`) | Sempre. |
| `Authentication__Keycloak__ClientSecret` | BFF | Na criação do client `dokpod-bff`. |
| `keycloak-db-password`, `dokpod-db-password` | instâncias provisionadas | Cenário 1. |
| `keycloak-bootstrap-admin` | Keycloak provisionado, só no primeiro start | Cenário 1. Removido depois que o administrador nominal existe. |
| `initial-admin-password` | entrega ao operador, só sem TTY | Cenário 1, sem TTY. Removido na primeira execução seguinte que confirmar, pela API administrativa do Keycloak, que o usuário não tem mais a ação obrigatória `UPDATE_PASSWORD`. Nunca entra no relatório. |
| `pki/ca.key`, `pki/*.pfx`, `pki/*.pass` | instalador, API e agentes | Conforme D-005. |

Regra comum: um arquivo existente nunca é sobrescrito sem `recreate` daquele item (FR-028).

## Pacote de host

Arquivo `packages/<environmentId>.tar` com modo `0600` e SHA-256 ao lado. Ele contém:
- `agent.pfx` e `agent.pass`;
- `ca.crt`;
- `agent.env`, com endpoint e `environmentId`;
- `compose.agent.yaml`;
- `README.txt`.

Com `--engine windows`, o pacote troca `compose.agent.yaml` por `windows-service.txt`, com os parâmetros de
`deploy/agent/manage-windows-service.ps1`.

Ele não contém nenhum outro arquivo de `secretsDir`. A revogação usa `agent-identities revoke` e invalida o pacote.

## Relatório de execução

Arquivo `reports/<runId>.json`. Ele registra:
- versão, cenário, engine e o plano completo, com ação por componente;
- verificações iniciais e seus resultados;
- migrations pendentes, aplicadas ou exportadas, com hashes;
- certificados emitidos (fingerprint e validade);
- resultado e código de saída.

Ele não tem segredos, e o canário do SC-005 inclui este arquivo.
