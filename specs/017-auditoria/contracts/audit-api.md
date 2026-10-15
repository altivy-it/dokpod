# Contrato: Auditoria

Mudanças aditivas em `contracts/openapi/dokpod-control-plane.v1.yaml`. Erros em `application/problem+json`.

| Operação | Rota | Autorização |
| --- | --- | --- |
| `getAuditSummary` | `GET /api/v1/audit/summary` → `{ readableHosts, oldestEventAt }` | autenticado; conta hosts com `audit:read` |
| `listAuditEvents` | `GET /api/v1/audit/events?since&until&stage&environmentId&actor&category&action&outcome&q&cursor&limit` → `{ events[], nextCursor }` | `audit:read` por host |
| `getAuditEvent` | `GET /api/v1/audit/events/{eventId}?at=<occurredAt>` → `{ event, related[] }` | `audit:read` no host do evento; 404 sem acesso |
| `exportAuditEvents` | `GET /api/v1/audit/export?<mesmos filtros>` → `text/csv` | `audit:read` por host; 400 `export_too_large` acima de 10.000 |

`q`: UUID exato de correlação, comando ou evento; senão prefixo do alvo **ou** busca full-text (sintaxe web: aspas para frase, `or`, `-palavra` para excluir), sem diferenciar acentos. `since`/`until`: padrão últimas 24 h; período maior que 90 dias → 400 `period_too_long`. `limit`: 1 a 100 (padrão 50). `at` no detalhe localiza a partição; sem ele a busca usa os últimos 90 dias.
