-- Spec 008: estágio, descritor/conexão do agente, inventário enriquecido e eventos operacionais (expand).

ALTER TABLE dokpod.environments
    ADD COLUMN stage character varying(16),
    ADD CONSTRAINT ck_environments_stage CHECK (stage IS NULL OR stage IN ('production', 'staging', 'development'));

CREATE INDEX ix_environments_stage ON dokpod.environments (stage);

CREATE TABLE dokpod.agent_descriptors (
    environment_id uuid NOT NULL CONSTRAINT pk_agent_descriptors PRIMARY KEY,
    engine_kind character varying(16) NOT NULL,
    engine_version character varying(64) NOT NULL,
    engine_api_version character varying(32) NOT NULL,
    engine_rootless boolean,
    operating_system character varying(16) NOT NULL,
    operating_system_version character varying(128) NOT NULL,
    architecture character varying(16) NOT NULL,
    agent_version character varying(64) NOT NULL,
    capabilities character varying(1024) NOT NULL,
    host_name character varying(255) NOT NULL,
    cpu_count integer,
    memory_bytes bigint,
    updated_at_utc timestamp with time zone NOT NULL,
    CONSTRAINT fk_agent_descriptors_environments FOREIGN KEY (environment_id)
        REFERENCES dokpod.environments (environment_id) ON DELETE CASCADE,
    CONSTRAINT ck_agent_descriptors_engine CHECK (engine_kind IN ('docker', 'podman')),
    CONSTRAINT ck_agent_descriptors_cpu CHECK (cpu_count IS NULL OR cpu_count >= 1),
    CONSTRAINT ck_agent_descriptors_memory CHECK (memory_bytes IS NULL OR memory_bytes >= 0)
);

CREATE TABLE dokpod.agent_connections (
    environment_id uuid NOT NULL CONSTRAINT pk_agent_connections PRIMARY KEY,
    connected boolean NOT NULL,
    session_started_at_utc timestamp with time zone,
    last_seen_at_utc timestamp with time zone,
    disconnected_at_utc timestamp with time zone,
    CONSTRAINT fk_agent_connections_environments FOREIGN KEY (environment_id)
        REFERENCES dokpod.environments (environment_id) ON DELETE CASCADE
);

ALTER TABLE dokpod.inventory_containers
    ADD COLUMN created_at_utc timestamp with time zone,
    ADD COLUMN started_at_utc timestamp with time zone,
    ADD COLUMN health character varying(16),
    ADD COLUMN restart_count integer,
    ADD COLUMN exit_code integer,
    ADD COLUMN oom_killed boolean,
    ADD COLUMN ports jsonb,
    ADD COLUMN display_status character varying(16) NOT NULL DEFAULT 'stopped',
    ADD CONSTRAINT ck_inventory_containers_health CHECK (health IS NULL OR health IN ('none', 'starting', 'healthy', 'unhealthy')),
    ADD CONSTRAINT ck_inventory_containers_restart_count CHECK (restart_count IS NULL OR restart_count >= 0),
    ADD CONSTRAINT ck_inventory_containers_display_status CHECK (display_status IN ('running', 'starting', 'failed', 'restarting', 'paused', 'stopped'));

-- Backfill do status exibido a partir do estado bruto existente (FR-006).
UPDATE dokpod.inventory_containers
SET display_status = CASE lower(state)
    WHEN 'running' THEN 'running'
    WHEN 'restarting' THEN 'restarting'
    WHEN 'paused' THEN 'paused'
    WHEN 'dead' THEN 'failed'
    ELSE 'stopped'
END;

CREATE INDEX ix_inventory_containers_display_status ON dokpod.inventory_containers (environment_id, display_status);
CREATE INDEX ix_inventory_containers_name ON dokpod.inventory_containers (lower(name), environment_id, container_id);

CREATE TABLE dokpod.environment_events (
    event_id uuid NOT NULL,
    environment_id uuid NOT NULL,
    type character varying(64) NOT NULL,
    severity character varying(16) NOT NULL,
    occurred_at timestamp with time zone NOT NULL,
    received_at timestamp with time zone NOT NULL,
    subject_kind character varying(16),
    subject_id character varying(255),
    subject_name character varying(255),
    parameters jsonb NOT NULL DEFAULT '{}'::jsonb,
    aggregated_count integer NOT NULL DEFAULT 1,
    source_key character varying(255),
    CONSTRAINT pk_environment_events PRIMARY KEY (event_id, occurred_at),
    CONSTRAINT ck_environment_events_severity CHECK (severity IN ('success', 'info', 'warning', 'error', 'neutral')),
    CONSTRAINT ck_environment_events_count CHECK (aggregated_count >= 1),
    CONSTRAINT ck_environment_events_parameters CHECK (octet_length(parameters::text) <= 4096)
) PARTITION BY RANGE (occurred_at);

CREATE TABLE dokpod.environment_events_default PARTITION OF dokpod.environment_events DEFAULT;

CREATE INDEX ix_environment_events_environment_time ON dokpod.environment_events (environment_id, occurred_at DESC);
CREATE INDEX ix_environment_events_severity_time ON dokpod.environment_events (severity, occurred_at);
CREATE UNIQUE INDEX ux_environment_events_source_key
    ON dokpod.environment_events (environment_id, source_key, occurred_at)
    WHERE source_key IS NOT NULL;

GRANT SELECT, INSERT, UPDATE, DELETE ON
    dokpod.agent_descriptors,
    dokpod.agent_connections,
    dokpod.environment_events
    TO dokpod_runtime;

-- Criação antecipada passa a incluir environment_events (coluna occurred_at); limites sempre em UTC,
-- independentemente do fuso da sessão.
CREATE OR REPLACE FUNCTION dokpod.dokpod_ensure_monthly_partitions(p_until date)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, dokpod
AS $$
DECLARE
    partition_month date := DATE '2026-01-01';
    next_month date;
    parent_table text;
    partition_column text;
    partition_table text;
    default_table text;
    default_has_rows boolean;
BEGIN
    IF p_until <= partition_month OR p_until > DATE '2040-01-01' THEN
        RAISE EXCEPTION 'partition horizon % is outside the supported range', p_until;
    END IF;

    WHILE partition_month < p_until LOOP
        next_month := (partition_month + INTERVAL '1 month')::date;

        FOREACH parent_table IN ARRAY ARRAY['audit_events', 'agent_commands', 'environment_events'] LOOP
            partition_column := CASE parent_table
                WHEN 'audit_events' THEN 'occurred_at_utc'
                WHEN 'agent_commands' THEN 'created_at_utc'
                ELSE 'occurred_at'
            END;
            partition_table := format('%s_%s', parent_table, to_char(partition_month, 'YYYY_MM'));
            default_table := parent_table || '_default';

            IF to_regclass(format('dokpod.%I', partition_table)) IS NULL THEN
                EXECUTE format(
                    'SELECT EXISTS (SELECT 1 FROM dokpod.%I WHERE %I >= $1 AND %I < $2)',
                    default_table, partition_column, partition_column)
                INTO default_has_rows
                USING (partition_month::timestamp AT TIME ZONE 'UTC'), (next_month::timestamp AT TIME ZONE 'UTC');

                IF default_has_rows THEN
                    RAISE EXCEPTION
                        'default partition %.% contains rows for [% - %)',
                        'dokpod', default_table, partition_month, next_month;
                END IF;

                EXECUTE format(
                    'CREATE TABLE dokpod.%I PARTITION OF dokpod.%I FOR VALUES FROM (%L) TO (%L)',
                    partition_table, parent_table,
                    (partition_month::timestamp AT TIME ZONE 'UTC'), (next_month::timestamp AT TIME ZONE 'UTC'));
            END IF;
        END LOOP;

        partition_month := next_month;
    END LOOP;
END;
$$;

REVOKE ALL ON FUNCTION dokpod.dokpod_ensure_monthly_partitions(date) FROM PUBLIC;

SELECT dokpod.dokpod_ensure_monthly_partitions(DATE '2037-01-01');

-- Retenção temporal genérica (008 D-004; reutilizada pela 009 e 012): remove partições
-- inteiramente anteriores ao corte e apaga em lotes as linhas vencidas da partição do corte.
CREATE OR REPLACE FUNCTION dokpod.dokpod_apply_temporal_retention(
    p_table regclass,
    p_column text,
    p_retention interval,
    p_batch_size integer DEFAULT 5000)
RETURNS bigint
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, dokpod
AS $$
DECLARE
    cutoff timestamptz := now() - p_retention;
    child record;
    upper_bound timestamptz;
    deleted bigint := 0;
    batch bigint;
BEGIN
    IF p_retention < INTERVAL '1 hour' THEN
        RAISE EXCEPTION 'retention % is below the supported minimum', p_retention;
    END IF;
    IF p_batch_size < 1 OR p_batch_size > 50000 THEN
        RAISE EXCEPTION 'batch size % is outside the supported range', p_batch_size;
    END IF;

    PERFORM set_config('lock_timeout', '2s', true);

    FOR child IN
        SELECT c.oid::regclass AS partition_name, pg_get_expr(c.relpartbound, c.oid) AS bound
        FROM pg_inherits i
        JOIN pg_class c ON c.oid = i.inhrelid
        WHERE i.inhparent = p_table
    LOOP
        CONTINUE WHEN child.bound = 'DEFAULT';
        upper_bound := substring(child.bound FROM 'TO \(''([^'']+)''\)')::timestamptz;
        IF upper_bound IS NOT NULL AND upper_bound <= cutoff THEN
            EXECUTE format('ALTER TABLE %s DETACH PARTITION %s', p_table, child.partition_name);
            EXECUTE format('DROP TABLE %s', child.partition_name);
        END IF;
    END LOOP;

    LOOP
        EXECUTE format(
            'WITH doomed AS (SELECT ctid FROM %s WHERE %I < $1 LIMIT $2) DELETE FROM %s t USING doomed WHERE t.ctid = doomed.ctid',
            p_table, p_column, p_table)
        USING cutoff, p_batch_size;
        GET DIAGNOSTICS batch = ROW_COUNT;
        deleted := deleted + batch;
        EXIT WHEN batch < p_batch_size;
    END LOOP;

    RETURN deleted;
END;
$$;

REVOKE ALL ON FUNCTION dokpod.dokpod_apply_temporal_retention(regclass, text, interval, integer) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION dokpod.dokpod_apply_temporal_retention(regclass, text, interval, integer) TO dokpod_runtime;
