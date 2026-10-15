-- A coluna actor_name é mantida: remover apagaria evidência (runbook da auditoria); o código anterior a ignora.
DROP FUNCTION IF EXISTS dokpod.dokpod_append_audit_event(
    uuid, timestamptz, uuid, smallint, varchar, varchar, uuid, uuid, smallint, varchar, bytea, varchar, jsonb, varchar);
DROP INDEX IF EXISTS dokpod.ix_audit_events_time_event;
