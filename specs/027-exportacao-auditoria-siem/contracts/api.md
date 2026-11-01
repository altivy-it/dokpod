# Contrato HTTP: destinos de exportação da auditoria

Rotas novas em `contracts/openapi/dokpod-control-plane.v1.yaml`, com `x-dokpod-edition: business`, só registradas na composição Business ([D-008](../research.md#d-008-licença-e-composição)). Erros em `application/problem+json`. Autorização pelo Keycloak no recurso `urn:dokpod:installation`; a falha do Keycloak nega (`503 authorization_unavailable`).

| Método e rota | Scope | Gate da licença | Sucesso |
| --- | --- | --- | --- |
| `GET /api/v1/settings/audit-export/destinations` | `audit-export:read` | não | `200` lista com estado e `ETag` por item |
| `POST /api/v1/settings/audit-export/destinations` | `audit-export:manage` | sim | `201` com `Location` e `ETag`; exige `Idempotency-Key`, válida por 24 h |
| `PUT /api/v1/settings/audit-export/destinations/{id}` | `audit-export:manage` | sim (editar ou reativar) | `200` com novo `ETag`; exige `If-Match` |
| `DELETE /api/v1/settings/audit-export/destinations/{id}` | `audit-export:manage` | não (desativar e excluir nunca são bloqueados) | `204`; exige `If-Match` |
| `POST /api/v1/settings/audit-export/destinations/{id}/test` | `audit-export:manage` | sim | `200` com `{ delivered, errorCode? }`; até 5 por minuto por instalação, acima disso `429 rate_limited` (L8) |

Editar só para desativar (`active: false`, sem outra mudança) também não passa pelo gate: com a licença restrita, o administrador pode parar o envio.

## Corpo de criação e edição

```json
{
  "name": "siem-soc",
  "protocol": "syslog",
  "host": "siem.exemplo.interno",
  "port": 6514,
  "path": null,
  "secretRef": "siem-soc",
  "categories": ["environments", "containers", "commands", "audit_export"],
  "active": true
}
```

Regras em [data-model.md](../data-model.md#audit_export_destinations). Propriedades desconhecidas são recusadas. A categoria `audit_export` é obrigatória: se faltar em `categories`, a API a inclui ([threat model, L7](../threat-model.md#lacunas-encontradas)).

## Item da listagem

```json
{
  "destinationId": "…",
  "name": "siem-soc",
  "protocol": "syslog",
  "host": "siem.exemplo.interno",
  "port": 6514,
  "path": null,
  "secretRef": "siem-soc",
  "secretFiles": { "ca": true, "clientCertificate": false, "token": false },
  "categories": ["environments", "containers", "commands", "audit_export"],
  "active": true,
  "state": "failing",
  "lastSentAt": "2026-11-01T12:00:00Z",
  "lastError": { "code": "tls_error", "at": "2026-11-01T12:05:00Z" },
  "pending": { "count": 1532, "capped": false, "oldestAt": "2026-11-01T12:00:01Z" },
  "lastGapAt": null,
  "version": 3
}
```

`secretFiles` diz só se cada arquivo existe; o conteúdo nunca sai.

## Códigos de problema

| Status | `code` | Quando |
| --- | --- | --- |
| 400 | `audit_export_invalid_destination` | campo inválido, propriedade desconhecida, categoria inexistente |
| 403 | `authorization_denied` | sem o scope |
| 403 | `license_expired`, `license_revoked`, `license_capacity_exceeded`, `license_state_indeterminate`, `license_invalid` | gate da licença (spec 031); `license_invalid` cobre também licença sem a capability `audit-siem-export` |
| 404 | `audit_export_not_found` | destino inexistente |
| 409 | `audit_export_limit_reached` | já existem 5 destinos |
| 409 | `audit_export_name_taken` | nome repetido |
| 412 | `audit_export_version_conflict` | `If-Match` desatualizado |
| 429 | `rate_limited` | mais de 5 envios de teste por minuto |
| 422 | `audit_export_address_blocked` | endereço proibido ([D-007](../research.md#d-007-tls-e-destinos-permitidos)) |
| 422 | `audit_export_invalid_secret_ref` | `secretRef` fora do padrão |
