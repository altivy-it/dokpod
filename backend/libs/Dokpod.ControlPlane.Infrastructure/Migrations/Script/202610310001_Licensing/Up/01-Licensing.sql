-- Spec 031 (T007): licenciamento Business offline. As tabelas existem nas duas edições para manter um só
-- schema; só a composição Business grava nelas (D-009). Cadeia de eventos: contracts/report-format.md.
CREATE TABLE dokpod.license_revisions (
    revision_id uuid NOT NULL CONSTRAINT pk_license_revisions PRIMARY KEY,
    document_type text NOT NULL,
    received_at_utc timestamp with time zone NOT NULL,
    received_by text NOT NULL,
    content_sha256 bytea NOT NULL,
    content bytea,
    license_id text,
    revision integer,
    outcome text NOT NULL,
    failure_code text,
    CONSTRAINT ck_license_revisions_document_type CHECK (document_type IN ('license', 'revocation')),
    CONSTRAINT ck_license_revisions_received_by CHECK (length(received_by) BETWEEN 1 AND 255),
    CONSTRAINT ck_license_revisions_sha256 CHECK (octet_length(content_sha256) = 32),
    CONSTRAINT ck_license_revisions_content CHECK (content IS NULL OR octet_length(content) BETWEEN 1 AND 65536),
    CONSTRAINT ck_license_revisions_license_id CHECK (license_id IS NULL OR license_id ~ '^[A-Za-z0-9_-]{1,128}$'),
    CONSTRAINT ck_license_revisions_revision CHECK (revision IS NULL OR revision >= 1),
    CONSTRAINT ck_license_revisions_outcome CHECK (outcome IN ('accepted', 'rejected')),
    -- Só documentos aceitos guardam o conteúdo; recusados ficam com hash, código e autoria (threat model L4).
    CONSTRAINT ck_license_revisions_accepted CHECK (
        (outcome = 'accepted' AND content IS NOT NULL AND failure_code IS NULL)
        OR (outcome = 'rejected' AND content IS NULL AND failure_code IS NOT NULL))
);

CREATE UNIQUE INDEX ux_license_revisions_accepted_revision
    ON dokpod.license_revisions (license_id, revision)
    WHERE outcome = 'accepted' AND document_type = 'license';
CREATE INDEX ix_license_revisions_received_at ON dokpod.license_revisions (received_at_utc);

-- Linha única, criada pela composição Business na primeira gravação com installation_id e chave HMAC aleatórios.
CREATE TABLE dokpod.licensing_state (
    singleton boolean NOT NULL DEFAULT true CONSTRAINT pk_licensing_state PRIMARY KEY,
    installation_id uuid NOT NULL,
    actor_hmac_key bytea NOT NULL,
    chain_head_seq bigint NOT NULL DEFAULT 0,
    chain_head_hash bytea,
    installation_slot_id text,
    active_revision_id uuid CONSTRAINT fk_licensing_state_active_revision REFERENCES dokpod.license_revisions (revision_id),
    revocation_sequence bigint NOT NULL DEFAULT 0,
    expiry_grace_until_utc timestamp with time zone,
    excess_started_at_utc timestamp with time zone,
    excess_grace_until_utc timestamp with time zone,
    last_trusted_instant_utc timestamp with time zone NOT NULL,
    version bigint NOT NULL DEFAULT 0,
    CONSTRAINT ck_licensing_state_singleton CHECK (singleton),
    CONSTRAINT ck_licensing_state_hmac_key CHECK (octet_length(actor_hmac_key) = 32),
    CONSTRAINT ck_licensing_state_chain CHECK (
        (chain_head_seq = 0 AND chain_head_hash IS NULL)
        OR (chain_head_seq > 0 AND octet_length(chain_head_hash) = 32)),
    CONSTRAINT ck_licensing_state_slot CHECK (installation_slot_id IS NULL OR length(installation_slot_id) BETWEEN 1 AND 64),
    CONSTRAINT ck_licensing_state_revocation_sequence CHECK (revocation_sequence >= 0),
    CONSTRAINT ck_licensing_state_excess CHECK ((excess_started_at_utc IS NULL) = (excess_grace_until_utc IS NULL)),
    CONSTRAINT ck_licensing_state_version CHECK (version >= 0)
);

CREATE TABLE dokpod.licensing_hosts (
    host_id uuid NOT NULL CONSTRAINT pk_licensing_hosts PRIMARY KEY,
    label text NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    removed_at_utc timestamp with time zone,
    CONSTRAINT ck_licensing_hosts_label CHECK (length(label) BETWEEN 1 AND 128),
    CONSTRAINT ck_licensing_hosts_removed CHECK (removed_at_utc IS NULL OR removed_at_utc >= created_at_utc)
);

CREATE TABLE dokpod.licensing_host_environments (
    host_id uuid NOT NULL CONSTRAINT fk_licensing_host_environments_host REFERENCES dokpod.licensing_hosts (host_id),
    environment_id uuid NOT NULL,
    linked_at_utc timestamp with time zone NOT NULL,
    unlinked_at_utc timestamp with time zone,
    CONSTRAINT pk_licensing_host_environments PRIMARY KEY (host_id, environment_id, linked_at_utc),
    CONSTRAINT ck_licensing_host_environments_unlinked CHECK (unlinked_at_utc IS NULL OR unlinked_at_utc >= linked_at_utc)
);

-- Um ambiente pertence a no máximo um host ativo (FR-012).
CREATE UNIQUE INDEX ux_licensing_host_environments_active
    ON dokpod.licensing_host_environments (environment_id)
    WHERE unlinked_at_utc IS NULL;

CREATE TABLE dokpod.licensing_capacity_observations (
    host_id uuid NOT NULL,
    observed_at_utc timestamp with time zone NOT NULL,
    vcpus integer,
    source text NOT NULL,
    environment_id uuid NOT NULL,
    CONSTRAINT ck_licensing_capacity_observations_vcpus CHECK (vcpus IS NULL OR vcpus >= 1),
    CONSTRAINT ck_licensing_capacity_observations_source CHECK (source IN ('engine', 'process_fallback', 'unknown'))
) PARTITION BY RANGE (observed_at_utc);

CREATE TABLE dokpod.licensing_capacity_observations_default PARTITION OF dokpod.licensing_capacity_observations DEFAULT;
CREATE INDEX ix_licensing_capacity_observations_host_time
    ON dokpod.licensing_capacity_observations (host_id, observed_at_utc);

-- Fora das partições: a retenção nunca a alcança (D-005, threat model B5).
CREATE TABLE dokpod.licensing_capacity_anchors (
    host_id uuid NOT NULL CONSTRAINT pk_licensing_capacity_anchors PRIMARY KEY,
    observed_at_utc timestamp with time zone NOT NULL,
    vcpus integer,
    source text NOT NULL,
    environment_id uuid NOT NULL,
    CONSTRAINT ck_licensing_capacity_anchors_vcpus CHECK (vcpus IS NULL OR vcpus >= 1),
    CONSTRAINT ck_licensing_capacity_anchors_source CHECK (source IN ('engine', 'process_fallback', 'unknown'))
);

CREATE TABLE dokpod.licensing_events (
    event_id uuid NOT NULL,
    chain_seq bigint NOT NULL,
    prev_hash bytea NOT NULL,
    record_hash bytea NOT NULL,
    occurred_at_utc timestamp with time zone NOT NULL,
    actor_id text,
    actor_ref text,
    correlation_id uuid NOT NULL,
    action text NOT NULL,
    details jsonb NOT NULL,
    CONSTRAINT pk_licensing_events PRIMARY KEY (event_id, occurred_at_utc),
    CONSTRAINT ck_licensing_events_chain_seq CHECK (chain_seq >= 1),
    CONSTRAINT ck_licensing_events_hashes CHECK (octet_length(prev_hash) = 32 AND octet_length(record_hash) = 32),
    CONSTRAINT ck_licensing_events_actor CHECK ((actor_id IS NULL) = (actor_ref IS NULL)),
    CONSTRAINT ck_licensing_events_action CHECK (action IN (
        'license.imported', 'license.rejected', 'license.state_changed', 'license.grace_started',
        'license.grace_ended', 'license.expiry_warning', 'license.report_exported', 'license.slot_changed',
        'license.trusted_instant_reset', 'capacity.daily_summary', 'revocation.imported',
        'host.linked', 'host.unlinked', 'host.removed')),
    CONSTRAINT ck_licensing_events_details CHECK (jsonb_typeof(details) = 'object')
) PARTITION BY RANGE (occurred_at_utc);

CREATE TABLE dokpod.licensing_events_default PARTITION OF dokpod.licensing_events DEFAULT;
-- licensing_state.chain_head_seq serializa a gravação; o índice serve à leitura em ordem da cadeia.
CREATE INDEX ix_licensing_events_chain_seq ON dokpod.licensing_events (chain_seq);
CREATE INDEX ix_licensing_events_action_time ON dokpod.licensing_events (action, occurred_at_utc);

CREATE FUNCTION dokpod.dokpod_reject_licensing_event_mutation()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'licensing events are append-only';
END;
$$;

CREATE TRIGGER trg_licensing_events_append_only
BEFORE UPDATE OR DELETE ON dokpod.licensing_events
FOR EACH ROW EXECUTE FUNCTION dokpod.dokpod_reject_licensing_event_mutation();

CREATE TRIGGER trg_licensing_events_no_truncate
BEFORE TRUNCATE ON dokpod.licensing_events
FOR EACH STATEMENT EXECUTE FUNCTION dokpod.dokpod_reject_licensing_event_mutation();

GRANT SELECT, INSERT ON dokpod.license_revisions TO dokpod_runtime;
GRANT SELECT, INSERT, UPDATE ON dokpod.licensing_state TO dokpod_runtime;
GRANT SELECT, INSERT, UPDATE ON dokpod.licensing_hosts, dokpod.licensing_host_environments TO dokpod_runtime;
GRANT SELECT, INSERT ON dokpod.licensing_capacity_observations, dokpod.licensing_events TO dokpod_runtime;
GRANT SELECT ON dokpod.licensing_capacity_anchors TO dokpod_runtime;

-- Criação antecipada passa a incluir as duas tabelas temporais do licenciamento.
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

        FOREACH parent_table IN ARRAY ARRAY['audit_events', 'agent_commands', 'agent_command_parameters', 'environment_events', 'host_resource_samples',
                'licensing_capacity_observations', 'licensing_events'] LOOP
            partition_column := CASE parent_table
                WHEN 'audit_events' THEN 'occurred_at_utc'
                WHEN 'agent_commands' THEN 'created_at_utc'
                WHEN 'agent_command_parameters' THEN 'created_at_utc'
                WHEN 'host_resource_samples' THEN 'minute'
                WHEN 'licensing_capacity_observations' THEN 'observed_at_utc'
                WHEN 'licensing_events' THEN 'occurred_at_utc'
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

-- Retenção passa a aceitar as observações de capacidade (observed_at_utc); licensing_events continua fora.
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
    -- 012 comandos e parâmetros, 031 observações de capacidade). Auditoria, inclusive licensing_events,
    -- e demais tabelas nunca são purgadas por esta função.
    -- array_remove descarta tabelas ainda inexistentes (NULL); sem isso `= ANY` resultaria em NULL.
    IF p_table IS NULL OR NOT (p_table = ANY (array_remove(ARRAY[
            to_regclass('dokpod.environment_events'),
            to_regclass('dokpod.host_resource_samples'),
            to_regclass('dokpod.agent_commands'),
            to_regclass('dokpod.agent_command_parameters'),
            to_regclass('dokpod.licensing_capacity_observations')], NULL))) THEN
        RAISE EXCEPTION 'table % is not eligible for temporal retention', p_table;
    END IF;
    IF p_column IS NULL OR p_column NOT IN ('occurred_at', 'minute', 'created_at_utc', 'observed_at_utc') THEN
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

-- Grava a âncora de cada host antes de remover observações (D-005): a última observação anterior ao corte,
-- sem nunca regredir o observed_at_utc já ancorado. A retenção genérica nunca é chamada direto para esta tabela.
CREATE FUNCTION dokpod.dokpod_retain_licensing_capacity(p_retention interval)
RETURNS bigint
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, dokpod
AS $$
DECLARE
    cutoff timestamptz := now() - p_retention;
BEGIN
    IF p_retention IS NULL OR p_retention < INTERVAL '35 days' THEN
        RAISE EXCEPTION 'licensing capacity retention % is below 35 days', p_retention;
    END IF;

    INSERT INTO dokpod.licensing_capacity_anchors AS a (host_id, observed_at_utc, vcpus, source, environment_id)
    SELECT DISTINCT ON (o.host_id) o.host_id, o.observed_at_utc, o.vcpus, o.source, o.environment_id
    FROM dokpod.licensing_capacity_observations o
    WHERE o.observed_at_utc < cutoff
    ORDER BY o.host_id, o.observed_at_utc DESC
    ON CONFLICT (host_id) DO UPDATE
        SET observed_at_utc = EXCLUDED.observed_at_utc,
            vcpus = EXCLUDED.vcpus,
            source = EXCLUDED.source,
            environment_id = EXCLUDED.environment_id
        WHERE EXCLUDED.observed_at_utc > a.observed_at_utc;

    RETURN dokpod.dokpod_apply_temporal_retention(
        'dokpod.licensing_capacity_observations'::regclass, 'observed_at_utc', p_retention);
END;
$$;

REVOKE ALL ON FUNCTION dokpod.dokpod_retain_licensing_capacity(interval) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION dokpod.dokpod_retain_licensing_capacity(interval) TO dokpod_runtime;
