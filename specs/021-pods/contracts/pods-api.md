# Contrato: API de Pods

## `GET /api/v1/pods`

Query: `stage`, `environmentId[]`, `status[]` (`running`, `degraded`, `exited`, `paused`, `created`, `error`), `containerId` (pods que contêm o container), `q`, `sort` (`name`, `-name`, `createdAt`, `-createdAt`, `environment`), `cursor`, `limit`.

Resposta: `{ pods: PodItem[], summary: { total, byStatus, withoutPodsHosts[], unavailableHosts[] }, nextCursor }`.

`PodItem`: `environmentId`, `environmentName`, `stage`, `hostConnected`, `podId`, `name`, `status`, `createdAt`, `labels`, `networks[]`, `infraId`, `containers[{ containerId, name, status, restarts, infra }]`, `runningContainers`, `agentPod`, `observedAt`, `allowedActions[]`.

## `GET /api/v1/environments/{environmentId}/pods/{pod}`

`pod` = identificador ou nome. 200 `PodItem`; 404.

## `POST /api/v1/environments/{environmentId}/pods/{podId}/actions`

`Idempotency-Key`; corpo `{ action, deadlineUtc, justification? }`. 202 `{ commandId }`; 400; 403; 404 `pod_not_found`; 409 `agent_pod`, `pod_state_conflict`, `agent_disconnected`, `not_supported`; 422 `justification_required`.

Status do comando (`GET .../commands/{id}`): `action: pod_<ação>`, `failureCode` (`pod_partial_failure`, `pod_not_found`, `agent_pod`) e `result.failedContainers[]`.
