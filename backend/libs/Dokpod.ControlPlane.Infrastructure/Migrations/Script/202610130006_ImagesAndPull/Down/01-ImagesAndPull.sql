-- Reverte a spec 012: remove pulls (tipo 7) antes de restaurar NOT NULL e a faixa de tipos.
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

DROP TABLE dokpod.agent_command_parameters;
DELETE FROM dokpod.agent_commands WHERE kind = 7;
ALTER TABLE dokpod.agent_commands DROP COLUMN image_size_bytes;
ALTER TABLE dokpod.agent_commands DROP COLUMN resolved_image_digest;
ALTER TABLE dokpod.agent_commands DROP CONSTRAINT ck_agent_commands_target;
ALTER TABLE dokpod.agent_commands DROP CONSTRAINT ck_agent_commands_kind;
ALTER TABLE dokpod.agent_commands ADD CONSTRAINT ck_agent_commands_kind CHECK (kind BETWEEN 1 AND 6);
ALTER TABLE dokpod.agent_commands ALTER COLUMN expected_container_revision SET NOT NULL;
ALTER TABLE dokpod.agent_commands ALTER COLUMN container_id SET NOT NULL;

DROP INDEX dokpod.ix_inventory_containers_image_id;
ALTER TABLE dokpod.inventory_containers DROP COLUMN image_id;
DROP TABLE dokpod.inventory_images;
DROP TABLE dokpod.inventory_image_projections;
