# Modelo de Dados: Registros

## `dokpod.inventory_registry_projections`

| Coluna | Tipo | Regra |
| --- | --- | --- |
| `environment_id` | uuid PK, FK `environments` (cascade) | um registro por host |
| `revision` | bigint | revisão aceita; menor é descartada |
| `observed_at_utc` | timestamptz | |

## `dokpod.inventory_registries`

| Coluna | Tipo | Regra |
| --- | --- | --- |
| `environment_id` | uuid, FK da projeção (cascade) | PK com `registry` |
| `registry` | text | host[:porta], minúsculo, gramática da spec 012 |
| `mirrors` | jsonb | lista de registros espelho (até 16) |
| `mirror_of` | text nulo | registro de quem é espelho |
| `insecure`, `blocked`, `search`, `credential` | boolean | |
| `observed_at` | timestamptz | |

## `dokpod.registry_policies`

| Coluna | Tipo | Regra |
| --- | --- | --- |
| `stage` | text PK | `production`, `staging`, `development`, `none` |
| `registries` | jsonb | lista ordenada (vazia = qualquer), até 100 |
| `updated_by` | text | ator |
| `updated_at` | timestamptz | |

Auditoria `registry.policy_changed` (categoria `governance`): `stage`, `previousRegistries`, `registries` (listas separadas por vírgula; `*` = qualquer).
