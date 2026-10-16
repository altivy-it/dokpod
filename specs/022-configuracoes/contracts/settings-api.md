# Contrato: API de Configurações

- `GET /api/v1/settings/access` → `{ allowed }`.
- `GET /api/v1/settings/installation` (403 sem acesso) → `{ controlPlane: { version }, protocolVersion, database: { configured, lastMigration, pendingMigrations }, dependencies: [{ name, status, description, durationMs }], checkedAt, agents: { total, connected, updateRequired, unknown, byAgentVersion: [{ version, count }], byEngine: [{ engine, version, count }] } }`.
- `GET /api/v1/settings/parameters` (403 sem acesso) → `{ parameters: [{ group, key, label, value, source }] }` com `source` `default` ou `installation`.
- `GET /bff/version` → `{ version }`.
- `GET /api/v1/action-justification-policies` → cada política ganha `canEdit`.
