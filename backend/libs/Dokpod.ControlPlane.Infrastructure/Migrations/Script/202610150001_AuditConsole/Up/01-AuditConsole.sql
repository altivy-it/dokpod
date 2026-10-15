-- Idempotente na coluna e no índice: o Down mantém a coluna (evidência) e o Up pode ser reaplicado.
-- Spec 017 D-001/D-005: nome exibível do ator e consulta da trilha por período (expand).
ALTER TABLE dokpod.audit_events
    ADD COLUMN IF NOT EXISTS actor_name character varying(255);

CREATE INDEX IF NOT EXISTS ix_audit_events_time_event
    ON dokpod.audit_events (occurred_at_utc DESC, event_id DESC);

-- Sobrecarga com o nome do ator; as assinaturas anteriores continuam válidas.
CREATE FUNCTION dokpod.dokpod_append_audit_event(
    p_event_id uuid,
    p_occurred_at_utc timestamptz,
    p_correlation_id uuid,
    p_actor_kind smallint,
    p_actor_id varchar(128),
    p_action varchar(128),
    p_environment_id uuid,
    p_command_id uuid,
    p_outcome smallint,
    p_failure_code varchar(128),
    p_payload_hash bytea,
    p_subject_id varchar(255),
    p_details jsonb,
    p_actor_name varchar(255))
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, dokpod
AS $$
DECLARE
    inserted_event_id uuid;
    existing_payload_hash bytea;
BEGIN
    INSERT INTO audit_event_keys (event_id, payload_hash)
    VALUES (p_event_id, p_payload_hash)
    ON CONFLICT (event_id) DO NOTHING
    RETURNING event_id INTO inserted_event_id;

    IF inserted_event_id IS NULL THEN
        SELECT payload_hash INTO existing_payload_hash FROM audit_event_keys WHERE event_id = p_event_id;
        IF existing_payload_hash IS DISTINCT FROM p_payload_hash THEN
            RAISE EXCEPTION 'audit event % already exists with conflicting payload', p_event_id USING ERRCODE = '23505';
        END IF;
        RETURN;
    END IF;

    INSERT INTO audit_events (
        event_id, occurred_at_utc, correlation_id, actor_kind,
        actor_id, action, environment_id, command_id, outcome, failure_code, subject_id, details, actor_name)
    VALUES (
        p_event_id, p_occurred_at_utc, p_correlation_id, p_actor_kind,
        p_actor_id, p_action, p_environment_id, p_command_id, p_outcome, p_failure_code, p_subject_id, p_details, p_actor_name);
END;
$$;

REVOKE ALL ON FUNCTION dokpod.dokpod_append_audit_event(
    uuid, timestamptz, uuid, smallint, varchar, varchar, uuid, uuid, smallint, varchar, bytea, varchar, jsonb, varchar) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION dokpod.dokpod_append_audit_event(
    uuid, timestamptz, uuid, smallint, varchar, varchar, uuid, uuid, smallint, varchar, bytea, varchar, jsonb, varchar) TO dokpod_runtime;
