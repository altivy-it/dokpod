# Modelo de Dados: Volumes

## `dokpod.volume_inventory`

| Coluna | Tipo | Observação |
| --- | --- | --- |
| `environment_id` | uuid | PK com `name` |
| `name` | varchar(255) | nome do volume no engine |
| `anonymous` | boolean | nome gerado (64 hex) |
| `driver` | varchar(128) | |
| `scope` | varchar(32) | `local` ou `global` |
| `created_at` | timestamptz null | |
| `labels` | jsonb | até 64 labels |
| `mounts` | jsonb | `[{containerId, containerName, destination, readOnly}]` |
| `observed_at` | timestamptz | horário do snapshot |

Índices: `(environment_id, name)` PK; `(environment_id) WHERE mounts = '[]'` para "sem uso".

## `dokpod.volume_inventory_revisions`

`environment_id` PK, `revision` bigint, `observed_at`. Snapshot com revisão menor é descartado.

## Registros

`volume:delete` acrescentado a `environments.scopes` onde há `container:delete` (D-006).

## Comando

`AgentCommandKind.RemoveVolume` com parâmetro `name`; auditoria `volume.remove` (sujeito = nome do volume; detalhes `name`, `justification`).
