# Contrato HTTP: Delta OpenAPI v1 (008)

Todas as rotas exigem `bearerAuth` (token relayado pelo BFF), aceitam `X-Correlation-Id` e respondem `ProblemDetails` em 400/401/403/404/409/503. Mutações exigem antiforgery no BFF.

## Alterações em schemas existentes

- `RegisterEnvironmentRequest`: `stage` opcional (`production` \| `staging` \| `development`).
- `EnvironmentState`: `stage` (nullable), `host` existente, novo `agent` (`HostAgentView`, nullable), `situation` (`disabled` \| `disconnected` \| `stale` \| `update_required` \| `unknown` \| `up_to_date`), `containerCount` (int, nullable). `scopes` ganha os novos valores das specs 009-014 conforme forem entregues.
- `ContainerInventory`: `displayStatus` (`running` \| `starting` \| `failed` \| `restarting` \| `paused` \| `stopped`), `createdAt`, `startedAt`, `health`, `restartCount`, `exitCode`, `oomKilled`, `ports[]` (`PortBinding`) — todos opcionais.

## Novos schemas

- `HostAgentView`: `connected`, `sessionStartedAt`, `lastSeenAt`, `agentVersion`, `engine` (`docker` \| `podman`), `engineVersion`, `engineApiVersion`, `engineRootless`, `operatingSystem`, `operatingSystemVersion`, `architecture`, `hostName`, `cpuCount`, `memoryBytes`, `capabilities[]`.
- `PortBinding`: `hostIp`, `hostPort`, `containerPort`, `protocol`.
- `OverviewSummary`: `stage` (nullable), `observedAt` (mais antigo), `hosts` (`total`, `bySituation{}`), `containers` (`total`, `byDisplayStatus{}`), `engines[]` (`engine`, `hosts`, `containers`, `versions[]` {`version`, `hosts`}), `attentionEvents24h`.
- `OperationalEvent`: `eventId`, `environmentId`, `environmentName`, `type`, `severity`, `occurredAt`, `receivedAt`, `clockSkewDetected` (boolean, diferença > 5 min), `subjectKind`, `subjectId`, `subjectName`, `parameters{}`, `aggregatedCount`.
- `OperationalEventPage`: `events[]` (≤ 100), `nextCursor`.

## Novas operações

| Método e rota | operationId | Autorização | Descrição |
| --- | --- | --- | --- |
| `GET /api/v1/overview?stage=` | `getOverview` | `environment:read` por host | Resumo agregado apenas de hosts autorizados e habilitados. |
| `GET /api/v1/environments?stage=&engine=&situation=&q=&cursor=&limit=` | `listEnvironments` (existente, estendida) | `environment:read` | Filtros novos opcionais; sem eles o comportamento atual é preservado. Itens trazem `agent` e `situation`. Não há `listHosts`. |
| `PUT /api/v1/environments/{environmentId}/stage` | `setEnvironmentStage` | `environment:manage` | Corpo `{ "stage": ... \| null }`; idempotente (mesmo estágio não gera mudança nem auditoria); auditoria `environment.stage_changed` com estágio anterior e novo; sem `If-Match` (D-010). |
| `GET /api/v1/events?stage=&environmentId=&severity=&since=&until=&cursor=&limit=` | `listEvents` | `environment:read` por host | Eventos ordenados por `occurredAt desc`; `limit` ≤ 100; `since` padrão 24 h. |

## Tempo real

Hub `/hubs/control-plane` (existente): mensagens de invalidação passam a carregar `kind` (`inventory`, `host`, `events`) além do `environmentId`, com no máximo uma emissão por segundo por host e `kind`. O cliente busca o estado pela API.
