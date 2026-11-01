-- Spec 025 (data-model.md): agenda por host, último resultado por referência em uso e condição de alerta.
-- As duas tabelas crescem com hosts e imagens em uso, não com o tempo: sem particionamento (plan.md).
CREATE TABLE dokpod.image_update_schedules (
    environment_id uuid NOT NULL CONSTRAINT pk_image_update_schedules PRIMARY KEY
        CONSTRAINT fk_image_update_schedules_environment REFERENCES dokpod.environments (environment_id) ON DELETE CASCADE,
    next_due_at_utc timestamp with time zone NOT NULL,
    last_requested_at_utc timestamp with time zone,
    last_manual_at_utc timestamp with time zone,
    pending_request_id uuid,
    lease_owner character varying(128),
    lease_until_utc timestamp with time zone,
    CONSTRAINT ck_image_update_schedules_lease CHECK (lease_owner IS NULL OR lease_until_utc IS NOT NULL)
);

CREATE INDEX ix_image_update_schedules_due ON dokpod.image_update_schedules (next_due_at_utc);

CREATE TABLE dokpod.image_update_checks (
    environment_id uuid NOT NULL
        CONSTRAINT fk_image_update_checks_environment REFERENCES dokpod.environments (environment_id) ON DELETE CASCADE,
    reference character varying(512) NOT NULL,
    platform character varying(64) NOT NULL,
    local_image_id character varying(80),
    state character varying(24) NOT NULL,
    reason character varying(32),
    remote_digest character varying(80),
    checked_at_utc timestamp with time zone NOT NULL,
    last_success_at_utc timestamp with time zone,
    outdated_since_utc timestamp with time zone,
    CONSTRAINT pk_image_update_checks PRIMARY KEY (environment_id, reference, platform),
    CONSTRAINT ck_image_update_checks_state CHECK (state IN ('up_to_date', 'update_available', 'not_checkable', 'failed')),
    CONSTRAINT ck_image_update_checks_reason CHECK (
        (state IN ('up_to_date', 'update_available') AND reason IS NULL)
        OR (state = 'not_checkable' AND reason IS NOT NULL AND reason IN ('digest_reference', 'local_image', 'registry_not_allowed', 'insecure_registry', 'agent_unsupported'))
        OR (state = 'failed' AND reason IS NOT NULL AND reason IN ('registry_unavailable', 'rate_limited', 'unauthorized', 'reference_not_found', 'timeout', 'invalid_response')))
);

CREATE INDEX ix_image_update_checks_state ON dokpod.image_update_checks (environment_id, state);

GRANT SELECT, INSERT, UPDATE, DELETE ON dokpod.image_update_schedules, dokpod.image_update_checks TO dokpod_runtime;

-- Condição "imagem desatualizada há mais de N dias" (D-010), por expand-contract: coluna nula e constraint ampliada.
-- Os IS NOT NULL explícitos importam: num CHECK, NULL IN (...) dá NULL e passaria.
ALTER TABLE dokpod.alert_rules ADD COLUMN outdated_days integer;
ALTER TABLE dokpod.alert_rules DROP CONSTRAINT ck_alert_rules_condition;
ALTER TABLE dokpod.alert_rules ADD CONSTRAINT ck_alert_rules_condition CHECK (
    (condition_kind = 'event' AND event_type IS NOT NULL AND metric IS NULL AND threshold_percent IS NULL AND outdated_days IS NULL)
    OR (condition_kind = 'metric' AND metric IN ('host_cpu', 'host_memory', 'container_cpu', 'container_memory')
        AND threshold_percent BETWEEN 1 AND 100 AND event_type IS NULL AND outdated_days IS NULL)
    OR (condition_kind = 'image_outdated' AND outdated_days IS NOT NULL AND outdated_days BETWEEN 1 AND 365
        AND event_type IS NULL AND metric IS NULL AND threshold_percent IS NULL));
