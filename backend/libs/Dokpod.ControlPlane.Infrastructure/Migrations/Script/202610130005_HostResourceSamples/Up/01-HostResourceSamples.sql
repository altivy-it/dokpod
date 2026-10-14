-- Spec 009: série de 1 minuto por host (48 h por padrão); containers ficam somente em memória (D-002 A′).
CREATE TABLE dokpod.host_resource_samples (
    environment_id uuid NOT NULL,
    minute timestamp with time zone NOT NULL,
    cpu_percent real,
    memory_used_bytes bigint,
    memory_total_bytes bigint,
    sample_count smallint NOT NULL,
    CONSTRAINT pk_host_resource_samples PRIMARY KEY (environment_id, minute),
    CONSTRAINT ck_host_resource_samples_cpu CHECK (cpu_percent IS NULL OR (cpu_percent >= 0 AND cpu_percent <= 100)),
    CONSTRAINT ck_host_resource_samples_memory CHECK (
        (memory_used_bytes IS NULL OR memory_used_bytes >= 0) AND (memory_total_bytes IS NULL OR memory_total_bytes >= 0)),
    CONSTRAINT ck_host_resource_samples_count CHECK (sample_count >= 1)
) PARTITION BY RANGE (minute);

CREATE TABLE dokpod.host_resource_samples_default PARTITION OF dokpod.host_resource_samples DEFAULT;

GRANT SELECT, INSERT, UPDATE, DELETE ON dokpod.host_resource_samples TO dokpod_runtime;

-- Criação antecipada passa a incluir host_resource_samples (coluna minute).
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

        FOREACH parent_table IN ARRAY ARRAY['audit_events', 'agent_commands', 'environment_events', 'host_resource_samples'] LOOP
            partition_column := CASE parent_table
                WHEN 'audit_events' THEN 'occurred_at_utc'
                WHEN 'agent_commands' THEN 'created_at_utc'
                WHEN 'host_resource_samples' THEN 'minute'
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
