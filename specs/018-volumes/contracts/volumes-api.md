# Contrato: Volumes

| Operação | Rota | Autorização |
| --- | --- | --- |
| `listVolumes` | `GET /api/v1/volumes?stage&environmentId&engine&unused&anonymous&q&sort&cursor&limit` → `{ volumes[], summary{total,unused,unavailableHosts}, nextCursor }` | `environment:read` |
| `getVolume` | `GET /api/v1/environments/{environmentId}/volumes/{name}` → volume com montagens | `environment:read` |
| `getVolumeUsage` | `GET /api/v1/environments/{environmentId}/volumes/{name}/usage` → `{ sizeBytes, refCount }` (nulos quando o engine não informa) | `environment:read` |
| `removeVolume` | `POST /api/v1/environments/{environmentId}/volumes/{name}/remove` `{ deadlineUtc, justification? }` + `Idempotency-Key` → `202 { commandId }` | `volume:delete`; 409 `volume_in_use`; 422 `justification_required` |

Resultado do comando pelo `getContainerCommand` existente; códigos: `volume_in_use`, `volume_not_found`, `volume_remove_failed`, `not_supported`.
