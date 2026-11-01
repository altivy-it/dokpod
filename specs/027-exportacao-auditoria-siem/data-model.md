# Modelo de dados: Exportação de auditoria para SIEM

**Spec**: [spec.md](spec.md) | **Pesquisa**: [research.md](research.md)

Tudo no PostgreSQL existente, schema `dokpod`, por migrations SQL em `backend/libs/Dokpod.ControlPlane.Infrastructure/Migrations/Script/`. Nenhuma tabela nova cresce com o volume da trilha: a fila é o próprio `audit_events`, que já é particionado por mês ([D-001](research.md#d-001-cursor-na-ordem-de-confirmação-da-trilha)).

## `audit_events` (existente, colunas novas)

| Coluna | Tipo | Regra |
| --- | --- | --- |
| `tx_id` | `xid8` | Padrão `pg_current_xact_id()`, só para linhas novas; `NULL` nas antigas, que nunca são exportadas |
| `export_seq` | `bigint` | Padrão `nextval('dokpod.audit_export_seq')`, só para linhas novas |

- Índice `ix_audit_events_export_order (tx_id, export_seq) WHERE tx_id IS NOT NULL`, criado em cada partição, inclusive nas futuras (rotina de partições da migration `202609220002`).
- A migration não reescreve partições: `ADD COLUMN` sem padrão, depois `SET DEFAULT`. O `Down` remove padrões, índice, colunas e sequência.
- O gatilho de append-only não muda.

## `audit_export_destinations`

| Coluna | Tipo | Regra |
| --- | --- | --- |
| `destination_id` | `uuid` PK | gerado pela API |
| `name` | `varchar(64)` | obrigatório, único sem diferenciar maiúsculas |
| `protocol` | `varchar(16)` | `syslog` ou `otlp` |
| `host` | `varchar(253)` | nome DNS ou IP; filtros de [D-007](research.md#d-007-tls-e-destinos-permitidos) |
| `port` | `integer` | 1 a 65535, obrigatório |
| `path` | `varchar(256)` | só `otlp`; padrão `/v1/logs`; começa com `/` |
| `secret_ref` | `varchar(64)` | opcional; `[a-z0-9-]{1,64}` ([D-006](research.md#d-006-credenciais-do-destino)) |
| `categories` | `text[]` | subconjunto das chaves de `AuditCategories`, sempre contendo `audit_export` (threat model L7) |
| `active` | `boolean` | `true` ao criar |
| `version` | `bigint` | concorrência otimista (`If-Match`) |
| `idempotency_key` | `varchar(128)` | `Idempotency-Key` da criação; única enquanto `idempotency_expires_at_utc` não passar |
| `idempotency_expires_at_utc` | `timestamptz` | criação + 24 h |
| `created_at_utc`, `updated_at_utc` | `timestamptz` | |

- Limite de 5 linhas, conferido na transação de criação sob lock da tabela ([D-011](research.md#d-011-limites-e-contagem-da-fila)).
- Excluir um destino apaga a linha e o cursor dele na mesma transação. Se outra réplica estiver enviando, ela perde o lease na próxima renovação e para sem erro; o lote em andamento pode chegar ao destino uma vez.
- A chave de idempotência da criação vale por 24 h (guardada com o destino; depois disso, a mesma chave cria outro destino).

## `audit_export_cursors`

Uma linha por destino, com `ON DELETE CASCADE` a partir de `audit_export_destinations`.

| Coluna | Tipo | Regra |
| --- | --- | --- |
| `destination_id` | `uuid` PK, FK | |
| `last_tx_id` | `xid8` | começa no `xmin` do snapshot no momento da criação: o destino nasce no fim da trilha |
| `last_seq` | `bigint` | |
| `last_sent_at_utc` | `timestamptz` | hora da última confirmação do destino |
| `last_error_code` | `varchar(64)` | `tls_error`, `connection_refused`, `timeout`, `secret_missing`, `rejected`, `address_blocked` |
| `last_error_at_utc` | `timestamptz` | |
| `last_gap_at_utc` | `timestamptz` | última lacuna registrada, para o aviso persistente |
| `lease_owner` | `varchar(128)` | réplica que envia ([D-003](research.md#d-003-remetente-único-por-destino-entre-réplicas)) |
| `lease_until_utc` | `timestamptz` | |

## Estado exibido de um destino (calculado)

| Estado | Quando |
| --- | --- |
| `disabled` | `active = false` |
| `ok` | sem erro desde o último envio e fila abaixo de 1 lote |
| `catching_up` | sem erro e fila com mais de um lote |
| `failing` | último envio falhou; vem com `last_error_code` |
| `gap` | lacuna nas últimas 24 h (aviso persistente) |

## Eventos de auditoria novos

`AuditCategories` ganha a categoria `audit_export` com estas ações. Todas são gravadas com o ambiente reservado `AuditEvent.InstallationScopeId` ([D-012](research.md#d-012-eventos-da-instalação-na-trilha)): não aparecem na tela de auditoria por ambiente e saem no SIEM com `environmentId: null`.

| Ação | Detalhes (sem segredos) |
| --- | --- |
| `audit_export.destination_created` | `destinationId`, `name`, `protocol`, `host`, `port`, `categories`, se há `secretRef` |
| `audit_export.destination_updated` | campos alterados, com os mesmos cuidados |
| `audit_export.destination_deleted` | `destinationId`, `name` |
| `audit_export.test_sent` | `destinationId`, resultado e código de erro |
| `audit_export.gap` | `destinationId`, `fromUtc`, `toUtc`, `count` |
