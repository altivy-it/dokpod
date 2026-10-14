-- Volta à versão de 202610130001_ConsoleOverview (sem allowlist).
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
