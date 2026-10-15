# Contrato: API de Redes

## `GET /api/v1/networks`

Query: `stage`, `environmentId[]`, `engine[]`, `driver[]`, `unused` (bool), `system` (bool: `true` só do sistema, `false` sem as do sistema), `q` (2-128), `sort` (`name`, `-name`, `createdAt`, `-createdAt`, `environment`), `cursor`, `limit` (1-200).

Resposta 200: `{ networks: NetworkItem[], summary: { total, unused, unavailableHosts: [{environmentId, environmentName}] }, nextCursor }`.

`NetworkItem`: `environmentId`, `environmentName`, `stage`, `engine`, `hostConnected`, `networkId`, `name`, `driver`, `scope`, `internal`, `ipv6`, `attachable`, `system`, `createdAt`, `subnets[{subnet, gateway}]`, `labels`, `containers[{containerId, containerName, ipv4, ipv6}]`, `inUse`, `truncated`, `observedAt`, `canRemove`.

## `GET /api/v1/environments/{environmentId}/networks/{network}`

`network` = identificador (64 hex) ou nome. 200 `NetworkItem`; 404 `not_found`; 400 `network_invalid`.

## `POST /api/v1/environments/{environmentId}/networks/{networkId}/remove`

Cabeçalho `Idempotency-Key` (uuid). Corpo `{ deadlineUtc, justification? }`. Respostas: 202 `{ commandId }`; 400 `network_invalid`/`justification_invalid`/`invalid_network_request`; 403 `authorization_denied`; 404 `network_not_found`/`environment_not_found`; 409 `network_in_use`/`network_system`/`network_not_local`/`agent_disconnected`/`not_supported`/`command_id_conflict`; 422 `justification_required`; 503 `persistence_unavailable`/`authorization_unavailable`.

Resultado do comando em `GET /api/v1/environments/{id}/commands/{commandId}` com `action: remove_network` e `failureCode` `network_in_use`, `network_not_found`, `network_remove_failed`.
