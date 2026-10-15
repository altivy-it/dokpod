# Modelo de Dados: Auditoria

## `dokpod.audit_events` (existente, spec 001)

| Coluna | Mudança | Observação |
| --- | --- | --- |
| `actor_name` | nova, `varchar(255)` anulável | nome exibível do usuário no momento da ação; nulo para agente, sistema e eventos anteriores |

Índice novo: `ix_audit_events_time_event (occurred_at_utc DESC, event_id DESC)`.

Função nova (sobrecarga): `dokpod_append_audit_event(..., p_subject_id, p_details, p_actor_name)`; `EXECUTE` só para `dokpod_runtime`. As assinaturas anteriores continuam válidas.

Down: remove a sobrecarga, o índice e a coluna (a coluna nova não guarda evidência anterior à migration).

## Ação nova

- `audit.exported`: sujeito `audit`; detalhes `since`, `until`, `count`.

## Visão de leitura (API)

`AuditEventView`: `id`, `occurredAt`, `correlationId`, `actorKind` (`user`, `agent`, `system`), `actorId`, `actorName`, `action`, `category`, `environmentId`, `outcome` (`succeeded`, `denied`, `failed`), `failureCode`, `subjectId`, `commandId`, `details`.
