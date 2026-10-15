# Modelo de Dados: Redes

## `dokpod.inventory_network_projections`

| Coluna | Tipo | Regra |
| --- | --- | --- |
| `environment_id` | uuid PK, FK `environments` (cascade) | um registro por host |
| `revision` | bigint | revisão do último snapshot aceito; snapshot com revisão menor é descartado |
| `observed_at_utc` | timestamptz | horário do snapshot |

## `dokpod.inventory_networks`

| Coluna | Tipo | Regra |
| --- | --- | --- |
| `environment_id` | uuid, FK da projeção (cascade) | PK com `network_id` |
| `network_id` | text | 64 hex minúsculos |
| `name` | text | gramática de nome do engine, até 255 |
| `driver`, `scope` | text | até 255 |
| `internal`, `ipv6`, `attachable`, `system`, `truncated` | boolean | |
| `created_at` | timestamptz nulo | quando o engine informa |
| `subnets` | jsonb | `[{subnet, gateway}]`, até 16 |
| `labels` | jsonb | até 64 pares |
| `containers` | jsonb | `[{containerId, containerName, ipv4, ipv6}]`, até 200 |
| `container_count` | int | quantidade de conexões (índice parcial para `= 0`) |
| `observed_at` | timestamptz | |

## Comando

- `agent_commands.kind` aceita 10 (`REMOVE_NETWORK`); `agent_command_parameters.kind` aceita `remove_network` com `{networkId, name}`.
- Hash do payload v2: `network=<networkId>`.
- Auditoria `network.remove` (categoria `networks`): alvo = nome da rede; detalhes `name`, `networkId` e `justification` (mascarada) quando houver.
