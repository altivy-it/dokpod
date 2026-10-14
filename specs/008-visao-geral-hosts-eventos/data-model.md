# Modelo de Dados: Visão Geral, Hosts, Eventos e Inventário Enriquecido

Todas as mudanças de schema seguem expand-contract; nenhuma coluna existente é removida.

## environments (alteração)

| Coluna | Tipo | Regra |
| --- | --- | --- |
| `stage` | text null | `production` \| `staging` \| `development`; check constraint; null = sem estágio. |

## agent_descriptors (nova)

| Coluna | Tipo | Regra |
| --- | --- | --- |
| `environment_id` | uuid PK, FK environments | — |
| `engine_kind` | text | `docker` \| `podman` |
| `engine_version` | text | ≤ 64 |
| `engine_api_version` | text | ≤ 32 |
| `engine_rootless` | boolean null | — |
| `operating_system` / `operating_system_version` | text | — |
| `architecture` | text | `amd64` \| `arm64` |
| `agent_version` | text | ≤ 64 |
| `capabilities` | text[] | valores do enum `Capability` |
| `host_name` | text | ≤ 255, não confiável |
| `cpu_count` | integer null | ≥ 1 |
| `memory_bytes` | bigint null | ≥ 0 |
| `updated_at` | timestamptz | — |

## agent_connections (nova)

| Coluna | Tipo | Regra |
| --- | --- | --- |
| `environment_id` | uuid PK | — |
| `connected` | boolean | — |
| `session_started_at` | timestamptz null | — |
| `last_seen_at` | timestamptz null | atualizado ≤ 1/min; somente exibição, não usado para calcular a situação |
| `disconnected_at` | timestamptz null | — |

## inventory_containers (alteração)

| Coluna | Tipo | Regra |
| --- | --- | --- |
| `created_at` | timestamptz null | — |
| `started_at` | timestamptz null | — |
| `health` | text null | `none` \| `starting` \| `healthy` \| `unhealthy` |
| `restart_count` | integer null | ≥ 0 |
| `exit_code` | integer null | — |
| `oom_killed` | boolean null | — |
| `ports` | jsonb null | lista `{hostIp, hostPort, containerPort, protocol}` ≤ 64 itens |

## environment_events (nova, particionada por mês em `occurred_at`)

| Coluna | Tipo | Regra |
| --- | --- | --- |
| `event_id` | uuid | PK composta com `occurred_at` |
| `environment_id` | uuid | índice com `occurred_at desc` |
| `type` | text | allowlist de D-002, incluindo `events.dropped` |
| `severity` | text | `success` \| `info` \| `warning` \| `error` \| `neutral` |
| `occurred_at` | timestamptz | horário do engine |
| `received_at` | timestamptz | horário do plano de controle |
| `subject_kind` | text null | `container` \| `image` \| `volume` \| `host` \| `command` |
| `subject_id` / `subject_name` | text null | não confiáveis, ≤ 255 |
| `parameters` | jsonb | allowlist por tipo, ≤ 2 KiB |
| `aggregated_count` | integer | ≥ 1 |
| `source_key` | text null | dedup do engine (`id`+`timeNano`+`action`), único por ambiente na partição |

## Domínio

- `EnvironmentStage` (enum), `AgentDescriptor` (record), `EngineVersion` (parse/compare), `HostSituation` (`Disabled`, `Disconnected`, `Stale`, `UpdateRequired`, `Unknown`, `UpToDate`, avaliados nessa ordem), `ContainerDisplayStatus` (`Running`, `Starting`, `Failed`, `Restarting`, `Paused`, `Stopped`), `OperationalEvent`.
- `ContainerInventory` ganha os campos opcionais acima.

## Retenção de `environment_events`

- Função nova `dokpod_apply_temporal_retention(p_table regclass, p_column text, p_retention interval)` (genérica, reutilizada pela 009 para `host_resource_samples`) (D-004): remove partições inteiramente anteriores ao corte e apaga em lotes as linhas vencidas da partição que contém o corte.
- Configuração `EventRetention:Period` (padrão `30.00:00:00`), validada na inicialização.
