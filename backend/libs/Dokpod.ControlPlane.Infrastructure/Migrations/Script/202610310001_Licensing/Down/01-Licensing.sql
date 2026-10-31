-- Volta às versões anteriores das funções de partição e retenção e remove o licenciamento (spec 031).
DROP FUNCTION dokpod.dokpod_retain_licensing_capacity(interval);

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
    -- SECURITY DEFINER: somente tabelas temporais previstas nas specs (008 eventos, 009 amostras,
    -- 012 comandos e parâmetros). Auditoria e demais tabelas nunca são purgadas por esta função.
    -- array_remove descarta tabelas ainda inexistentes (NULL); sem isso `= ANY` resultaria em NULL.
    IF p_table IS NULL OR NOT (p_table = ANY (array_remove(ARRAY[
            to_regclass('dokpod.environment_events'),
            to_regclass('dokpod.host_resource_samples'),
            to_regclass('dokpod.agent_commands'),
            to_regclass('dokpod.agent_command_parameters')], NULL))) THEN
        RAISE EXCEPTION 'table % is not eligible for temporal retention', p_table;
    END IF;
    IF p_column IS NULL OR p_column NOT IN ('occurred_at', 'minute', 'created_at_utc') THEN
        RAISE EXCEPTION 'column % is not eligible for temporal retention', p_column;
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
        -- ctid só é único dentro de uma partição: comparar também tableoid evita apagar linha recente de outra
        -- partição que tenha o mesmo ctid de uma linha vencida (correção da 008 encontrada na 014).
        EXECUTE format(
            'WITH doomed AS (SELECT tableoid, ctid FROM %s WHERE %I < $1 LIMIT $2) DELETE FROM %s t USING doomed WHERE t.tableoid = doomed.tableoid AND t.ctid = doomed.ctid',
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

DROP TABLE dokpod.licensing_events;
DROP FUNCTION dokpod.dokpod_reject_licensing_event_mutation();
DROP TABLE dokpod.licensing_capacity_anchors;
DROP TABLE dokpod.licensing_capacity_observations;
DROP TABLE dokpod.licensing_host_environments;
DROP TABLE dokpod.licensing_hosts;
DROP TABLE dokpod.licensing_state;
DROP TABLE dokpod.license_revisions;
