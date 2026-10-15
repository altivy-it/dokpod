# Contrato: Governança de alertas e parada justificada

Mudanças aditivas em `contracts/openapi/dokpod-control-plane.v1.yaml`. Erros em `application/problem+json`, como no restante da API.

## Alertas

| Operação | Rota | Scope |
| --- | --- | --- |
| `getAlertSummary` | `GET /api/v1/alerts/summary?stage=` → `{ open, bySeverity, recent[≤10] }` | `alert:read` |
| `listAlerts` | `GET /api/v1/alerts?view=open\|history&stage&environmentId&severity&state&q&since&until&cursor&limit` | `alert:read` |
| `getAlert` | `GET /api/v1/alerts/{alertId}` (com regra e linha do tempo) | `alert:read` |
| `acknowledgeAlert` | `POST /api/v1/alerts/{alertId}/acknowledge` `{ comment }` | `alert:manage` |
| `resolveAlert` | `POST /api/v1/alerts/{alertId}/resolve` `{ comment }` | `alert:manage` |
| `silenceAlert` | `POST /api/v1/alerts/{alertId}/silence` `{ until, justification }` | `alert:manage` |

## Regras e silenciamentos

| Operação | Rota | Scope |
| --- | --- | --- |
| `listAlertRules` | `GET /api/v1/alert-rules` | `alert:read` em algum host |
| `createAlertRule` / `updateAlertRule` / `deleteAlertRule` | `POST /api/v1/alert-rules`, `PUT`/`DELETE /api/v1/alert-rules/{ruleId}` (com `version` para concorrência) | `alert:manage` em todos os hosts abrangidos |
| `listAlertSilences` | `GET /api/v1/alert-silences?active=` | `alert:read` |
| `createAlertSilence` / `endAlertSilence` | `POST /api/v1/alert-silences`, `POST /api/v1/alert-silences/{silenceId}/end` | `alert:manage` nos hosts abrangidos |

## Justificativas

| Operação | Rota | Scope |
| --- | --- | --- |
| `listActionJustificationPolicies` | `GET /api/v1/action-justification-policies` | autenticado |
| `setActionJustificationPolicy` | `PUT /api/v1/action-justification-policies/{stage}` `{ required }` | `environment:manage` em todos os hosts do estágio (D-007) |
| `listContainerJustifications` | `GET /api/v1/environments/{environmentId}/containers/{containerId}/justifications` → `{ current, history[] }` | `environment:read` |
| `listEnvironmentJustifications` | `GET /api/v1/environments/{environmentId}/justifications?since&until&cursor` | `environment:read` |

`submitContainerCommand` ganha `justification` (string, 10 a 500, opcional). Com a política do estágio ativa e ação `stop`, `pause` ou `delete` sem justificativa: `422 justification_required`. Itens de container na lista e no detalhe ganham `intentional` (`{ action, actor, at, reason }`) quando há ação justificada vigente.
