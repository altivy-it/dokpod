-- Spec 012: inventário de imagens, image_id dos containers, comandos com alvo de host (pull) e parâmetros.
CREATE TABLE dokpod.inventory_image_projections (
    environment_id uuid NOT NULL CONSTRAINT pk_inventory_image_projections PRIMARY KEY,
    revision bigint NOT NULL,
    observed_at_utc timestamp with time zone NOT NULL,
    CONSTRAINT fk_inventory_image_projections_environments FOREIGN KEY (environment_id)
        REFERENCES dokpod.environments (environment_id) ON DELETE CASCADE
);

CREATE TABLE dokpod.inventory_images (
    environment_id uuid NOT NULL,
    image_id character varying(160) NOT NULL,
    repo_tags text[] NOT NULL,
    repo_digests text[] NOT NULL,
    size_bytes bigint NOT NULL,
    created_at timestamp with time zone,
    architecture character varying(32),
    os character varying(32),
    truncated boolean NOT NULL DEFAULT false,
    observed_at timestamp with time zone NOT NULL,
    revision bigint NOT NULL,
    CONSTRAINT pk_inventory_images PRIMARY KEY (environment_id, image_id),
    CONSTRAINT fk_inventory_images_projections FOREIGN KEY (environment_id)
        REFERENCES dokpod.inventory_image_projections (environment_id) ON DELETE CASCADE,
    CONSTRAINT ck_inventory_images_size CHECK (size_bytes >= 0),
    CONSTRAINT ck_inventory_images_names CHECK (cardinality(repo_tags) <= 64 AND cardinality(repo_digests) <= 64)
);

GRANT SELECT, INSERT, UPDATE, DELETE ON dokpod.inventory_image_projections, dokpod.inventory_images TO dokpod_runtime;

ALTER TABLE dokpod.inventory_containers ADD COLUMN image_id character varying(160);
CREATE INDEX ix_inventory_containers_image_id ON dokpod.inventory_containers (environment_id, image_id);

-- Alvo de host (D-002): container e revisão esperada nulos somente para tipos parametrizados.
ALTER TABLE dokpod.agent_commands ALTER COLUMN container_id DROP NOT NULL;
ALTER TABLE dokpod.agent_commands ALTER COLUMN expected_container_revision DROP NOT NULL;
ALTER TABLE dokpod.agent_commands DROP CONSTRAINT ck_agent_commands_kind;
ALTER TABLE dokpod.agent_commands ADD CONSTRAINT ck_agent_commands_kind CHECK (kind BETWEEN 1 AND 7);
ALTER TABLE dokpod.agent_commands ADD CONSTRAINT ck_agent_commands_target CHECK (
    (kind BETWEEN 1 AND 6) = (container_id IS NOT NULL AND expected_container_revision IS NOT NULL));
ALTER TABLE dokpod.agent_commands ADD COLUMN resolved_image_digest character varying(160);
ALTER TABLE dokpod.agent_commands ADD COLUMN image_size_bytes bigint;

CREATE TABLE dokpod.agent_command_parameters (
    environment_id uuid NOT NULL,
    command_id uuid NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    kind character varying(32) NOT NULL,
    parameters jsonb NOT NULL,
    CONSTRAINT pk_agent_command_parameters PRIMARY KEY (environment_id, command_id, created_at_utc),
    CONSTRAINT fk_agent_command_parameters_commands FOREIGN KEY (environment_id, command_id, created_at_utc)
        REFERENCES dokpod.agent_commands (environment_id, command_id, created_at_utc) ON DELETE CASCADE,
    CONSTRAINT ck_agent_command_parameters_kind CHECK (kind IN ('pull_image'))
) PARTITION BY RANGE (created_at_utc);

CREATE TABLE dokpod.agent_command_parameters_default PARTITION OF dokpod.agent_command_parameters DEFAULT;

REVOKE ALL ON dokpod.agent_command_parameters FROM PUBLIC;
GRANT SELECT, INSERT, DELETE ON dokpod.agent_command_parameters TO dokpod_runtime;

-- Criação antecipada passa a incluir agent_command_parameters (coluna created_at_utc).
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

        FOREACH parent_table IN ARRAY ARRAY['audit_events', 'agent_commands', 'agent_command_parameters', 'environment_events', 'host_resource_samples'] LOOP
            partition_column := CASE parent_table
                WHEN 'audit_events' THEN 'occurred_at_utc'
                WHEN 'agent_commands' THEN 'created_at_utc'
                WHEN 'agent_command_parameters' THEN 'created_at_utc'
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
