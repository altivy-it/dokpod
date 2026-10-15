# Modelo de Dados: Governança de alertas e parada justificada

Migration `202610140001_AlertGovernance` (expand, com Down), schema `dokpod`.

## `alert_rules`

| Coluna | Tipo | Regras |
| --- | --- | --- |
| `rule_id` | uuid PK | ids fixos para as regras padrão |
| `name` | varchar(120) | obrigatório |
| `description` | varchar(500) | opcional |
| `condition_kind` | varchar(16) | `event` ou `metric` |
| `event_type` | varchar(64) | obrigatório quando `event`; um dos tipos operacionais |
| `metric` | varchar(32) | `host_cpu`, `host_memory`, `container_cpu`, `container_memory` quando `metric` |
| `threshold_percent` | numeric(5,2) | 1 a 100 quando `metric` |
| `duration_seconds` | int | 0 a 3.600 |
| `severity` | varchar(16) | `critical`, `high`, `medium`, `low` |
| `scope_stage` | varchar(16) | nulo = todos os estágios |
| `scope_environment_id` | uuid | nulo = todos os hosts |
| `scope_container_pattern` | varchar(128) | curinga `*`; nulo = todos |
| `enabled`, `built_in` | bool | — |
| `version` | int | incrementado a cada alteração |
| `updated_by`, `updated_at_utc` | varchar(255), timestamptz | — |

## `alerts`

| Coluna | Tipo | Regras |
| --- | --- | --- |
| `alert_id` | uuid PK | — |
| `rule_id` | uuid FK | — |
| `environment_id` | uuid | host do alvo |
| `container_id`, `container_name` | varchar(128) | nulos para alertas de host |
| `target_key` | varchar(200) | `environment_id[:container_id]` |
| `severity` | varchar(16) | copiada da regra na abertura |
| `state` | varchar(16) | `open`, `acknowledged`, `silenced`, `resolved` |
| `condition_text` | varchar(300) | texto legível, sem segredos |
| `opened_at_utc`, `last_occurred_at_utc` | timestamptz | — |
| `occurrences` | int | ≥ 1 |
| `assignee`, `silence_id` | varchar(255), uuid | opcionais |
| `resolved_at_utc`, `resolution_reason` | timestamptz, varchar(64) | `condition_cleared`, `manual`, `container_removed`, `rule_removed` |

Índice único parcial `(rule_id, target_key) WHERE state <> 'resolved'` (FR-003). Índices por `(state, environment_id)` e `resolved_at_utc`.

## `alert_timeline`

`alert_id` FK, `occurred_at_utc`, `kind` (`opened`, `occurred`, `acknowledged`, `silenced`, `unsilenced`, `resolved`), `actor` (nulo = sistema), `comment` (≤ 500, mascarado).

## `alert_silences`

`silence_id` PK, `scope_stage`, `scope_environment_id`, `scope_container_pattern`, `alert_id`, `starts_at_utc`, `ends_at_utc` (≤ início + 7 dias), `ended_at_utc`, `justification` (10 a 500, mascarada), `created_by`, `created_at_utc`.

## `action_justification_policies`

`stage` PK (`production`, `staging`, `development`), `required` bool, `updated_by`, `updated_at_utc`. Semeada: `production` = true; demais = false.

## `container_action_justifications`

| Coluna | Tipo | Regras |
| --- | --- | --- |
| `justification_id` | uuid PK | — |
| `command_id` | uuid único | uma justificativa por comando |
| `environment_id`, `container_id`, `container_name` | uuid, varchar(128), varchar(255) | nome preservado para containers removidos |
| `action` | varchar(16) | `stop`, `pause`, `delete` |
| `actor` | varchar(255) | sujeito do token |
| `reason` | varchar(500) | mascarada |
| `created_at_utc` | timestamptz | — |
| `ended_at_utc` | timestamptz | preenchido quando o container volta a executar |

"Vigente" = `ended_at_utc` nulo e ação `stop` ou `pause` mais recente do container. Índices por `(environment_id, container_id, created_at_utc desc)`.
