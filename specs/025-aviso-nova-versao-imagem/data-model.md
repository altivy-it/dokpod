# Modelo de dados: Aviso de nova versão de imagem

Schema `dokpod` do PostgreSQL existente. As duas tabelas novas não crescem com o tempo: uma linha por host e uma por referência em uso por host ([D-007](research.md#d-007-persistência)).

## `image_update_schedules`

Agenda da verificação por host ([D-006](research.md#d-006-agendamento-réplicas-e-verificar-agora)).

| Coluna | Tipo | Restrição |
| --- | --- | --- |
| `environment_id` | `uuid` | PK; FK `environments` com `ON DELETE CASCADE` |
| `next_due_at_utc` | `timestamptz` | obrigatório |
| `last_requested_at_utc` | `timestamptz` | nulo até o primeiro pedido |
| `last_manual_at_utc` | `timestamptz` | nulo; limita "Verificar agora" a um por minuto |
| `pending_request_id` | `uuid` | nulo; pedido enviado ainda sem última página |
| `lease_owner` | `varchar(128)` | nulo |
| `lease_until_utc` | `timestamptz` | nulo; obrigatório quando `lease_owner` existe |

Índice: `(next_due_at_utc)`.

## `image_update_checks`

Último resultado por host, referência e plataforma.

| Coluna | Tipo | Restrição |
| --- | --- | --- |
| `environment_id` | `uuid` | PK (1); FK `environments` com `ON DELETE CASCADE` |
| `reference` | `varchar(512)` | PK (2); referência normalizada como na spec 012 (`ImageReference`) |
| `platform` | `varchar(64)` | PK (3); `os/arquitetura[/variante]` da imagem local |
| `local_image_id` | `varchar(80)` | `sha256:` + 64 hex; nulo quando a imagem não está no host |
| `state` | `varchar(24)` | `up_to_date`, `update_available`, `not_checkable`, `failed` |
| `reason` | `varchar(32)` | nulo em `up_to_date` e `update_available`; valores abaixo |
| `remote_digest` | `varchar(80)` | último digest remoto conhecido (nulo em `not_checkable`) |
| `checked_at_utc` | `timestamptz` | obrigatório; instante em que o plano de controle recebeu o resultado (não o relógio do agente), para comparar com o instante do pedido |
| `last_success_at_utc` | `timestamptz` | nulo até a primeira verificação sem falha |
| `outdated_since_utc` | `timestamptz` | preenchido na transição para `update_available`, mantido enquanto o estado permanecer, apagado nos demais estados |

Índices: a PK; `(environment_id, state)` para os contadores.

Motivos (`reason`):

| Estado | Motivos |
| --- | --- |
| `not_checkable` | `digest_reference`, `local_image`, `registry_not_allowed`, `insecure_registry`, `agent_unsupported` |
| `failed` | `registry_unavailable`, `rate_limited`, `unauthorized`, `reference_not_found`, `timeout`, `invalid_response` |

Em `failed`, `remote_digest` e `last_success_at_utc` guardam o último resultado bom, e `outdated_since_utc` é mantido. Uma falha nunca vira `up_to_date` (SC-002).

## Estado derivado por container

Não é gravado ([D-008](research.md#d-008-container-com-a-versão-anterior)):

| Verificação da referência | `image_id` do container | Estado do container |
| --- | --- | --- |
| `up_to_date` | igual a `local_image_id` | `up_to_date` |
| `up_to_date` | diferente | `container_behind` |
| `update_available`, `not_checkable`, `failed` | qualquer | o da referência |
| sem linha | qualquer | ausente (ainda não verificado) |

O dado fica desatualizado quando `checked_at_utc` é anterior à última conexão perdida do host. A tela mostra a data e marca "desatualizado" quando o host está desconectado (caso de borda).

## Regra de alerta (spec 016)

`AlertConditionKind.ImageOutdated` com `outdated_days int` (1 a 365, obrigatório só para essa condição). Coluna nova e nula em `alert_rules`, por expand-contract ([D-010](research.md#d-010-condição-de-alerta)).

## Transições

```text
(sem linha) ──verificação──▶ up_to_date | update_available | not_checkable | failed
up_to_date ──tag mudou──▶ update_available (outdated_since = agora)
update_available ──pull e recriação──▶ up_to_date (outdated_since = nulo)
qualquer ──registro falhou──▶ failed (mantém o último resultado bom e outdated_since)
failed ──registro voltou──▶ up_to_date | update_available
referência deixa de ser usada ──▶ linha removida no próximo resultado completo
```
