# Modelo de Dados: Lista Unificada e Ações em Lote

## Persistência

- `inventory_containers`: coluna nova `display_status` (text, `running` \| `starting` \| `failed` \| `restarting` \| `paused` \| `stopped`), preenchida pela aplicação ao projetar com o mapeamento da 008 e com backfill na migration expand (D-006); coluna `ports_text` mantida pela aplicação para busca; índices `(environment_id, name)`, `(display_status)`, `(created_at)`.
- `agent_commands`: constraint `ck_agent_commands_kind` ampliada de `kind BETWEEN 1 AND 4` para `kind BETWEEN 1 AND 6` (`pause` = 5, `unpause` = 6) via migration expand; a 012 e a 013 ampliam depois para 7 e 8.

## Domínio

- `AgentCommandKind`: `Pause`, `Unpause`; gates: `Pause` exige estado `running`; `Unpause` exige `paused`; scope `container:pause` para ambos.

## Frontend

- `ContainerRow`: `environmentId`, `environmentName`, `engine`, `containerId`, `name`, `image`, `displayStatus`, `state`, `ports[]`, `createdAt`, `revision`, `observedAt`, `resources?`.
- `BatchOperation` (`sessionStorage`, escopo da aba): `batchId`, `action`, `createdAt`, `items[]` {`environmentId`, `containerId`, `name`, `idempotencyKey`, `commandId?`, `state`, `failureCode?`, `skippedReason?`}.
