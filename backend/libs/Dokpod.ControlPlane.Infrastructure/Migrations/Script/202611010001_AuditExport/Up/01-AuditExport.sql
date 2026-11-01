-- Spec 027 (T003): exportação contínua da auditoria para SIEM. As tabelas existem nas duas edições para manter um
-- só schema; só a composição Business grava nelas. A fila é a própria trilha (D-001): cada destino guarda um cursor
-- em (tx_id, export_seq), na ordem de confirmação das transações.
CREATE SEQUENCE dokpod.audit_export_seq AS bigint;

-- Expand sem reescrever partições: colunas sem padrão primeiro, padrão depois. Linhas antigas ficam NULL e nunca
-- são exportadas; um destino novo começa no fim da trilha.
ALTER TABLE dokpod.audit_events
    ADD COLUMN tx_id xid8,
    ADD COLUMN export_seq bigint;
ALTER TABLE dokpod.audit_events
    ALTER COLUMN tx_id SET DEFAULT pg_current_xact_id(),
    ALTER COLUMN export_seq SET DEFAULT nextval('dokpod.audit_export_seq');
ALTER SEQUENCE dokpod.audit_export_seq OWNED BY dokpod.audit_events.export_seq;

-- Índice na tabela-mãe: o PostgreSQL o cria em cada partição existente e nas futuras.
CREATE INDEX ix_audit_events_export_order ON dokpod.audit_events (tx_id, export_seq) WHERE tx_id IS NOT NULL;

CREATE TABLE dokpod.audit_export_destinations (
    destination_id uuid NOT NULL CONSTRAINT pk_audit_export_destinations PRIMARY KEY,
    name character varying(64) NOT NULL,
    protocol character varying(16) NOT NULL,
    host character varying(253) NOT NULL,
    port integer NOT NULL,
    path character varying(256),
    secret_ref character varying(64),
    categories text[] NOT NULL,
    active boolean NOT NULL DEFAULT true,
    version bigint NOT NULL DEFAULT 1,
    idempotency_key character varying(128),
    idempotency_expires_at_utc timestamp with time zone,
    created_at_utc timestamp with time zone NOT NULL,
    updated_at_utc timestamp with time zone NOT NULL,
    CONSTRAINT ck_audit_export_destinations_name CHECK (length(btrim(name)) BETWEEN 1 AND 64 AND name = btrim(name)),
    CONSTRAINT ck_audit_export_destinations_protocol CHECK (protocol IN ('syslog', 'otlp')),
    CONSTRAINT ck_audit_export_destinations_host CHECK (length(host) BETWEEN 1 AND 253),
    CONSTRAINT ck_audit_export_destinations_port CHECK (port BETWEEN 1 AND 65535),
    CONSTRAINT ck_audit_export_destinations_path CHECK (
        (protocol <> 'otlp' OR (path IS NOT NULL AND path LIKE '/%'))
        AND (protocol <> 'syslog' OR path IS NULL)),
    CONSTRAINT ck_audit_export_destinations_secret_ref CHECK (secret_ref IS NULL OR secret_ref ~ '^[a-z0-9-]{1,64}$'),
    -- Threat model L7: a categoria audit_export vai a todo destino, para o SOC sempre ver a criação de destinos.
    CONSTRAINT ck_audit_export_destinations_categories CHECK ('audit_export' = ANY (categories)),
    CONSTRAINT ck_audit_export_destinations_version CHECK (version >= 1),
    CONSTRAINT ck_audit_export_destinations_idempotency CHECK (
        (idempotency_key IS NULL) = (idempotency_expires_at_utc IS NULL)
        AND (idempotency_key IS NULL OR length(idempotency_key) BETWEEN 1 AND 128))
);

CREATE UNIQUE INDEX ux_audit_export_destinations_name ON dokpod.audit_export_destinations (lower(name));
CREATE UNIQUE INDEX ux_audit_export_destinations_idempotency
    ON dokpod.audit_export_destinations (idempotency_key) WHERE idempotency_key IS NOT NULL;

CREATE TABLE dokpod.audit_export_cursors (
    destination_id uuid NOT NULL CONSTRAINT pk_audit_export_cursors PRIMARY KEY
        CONSTRAINT fk_audit_export_cursors_destination REFERENCES dokpod.audit_export_destinations (destination_id) ON DELETE CASCADE,
    last_tx_id xid8 NOT NULL,
    last_seq bigint NOT NULL DEFAULT 0,
    last_sent_at_utc timestamp with time zone,
    last_error_code character varying(64),
    last_error_at_utc timestamp with time zone,
    last_gap_at_utc timestamp with time zone,
    lease_owner character varying(128),
    lease_until_utc timestamp with time zone,
    CONSTRAINT ck_audit_export_cursors_error_code CHECK (last_error_code IS NULL OR last_error_code IN (
        'tls_error', 'connection_refused', 'timeout', 'secret_missing', 'rejected', 'address_blocked')),
    CONSTRAINT ck_audit_export_cursors_lease CHECK ((lease_owner IS NULL) = (lease_until_utc IS NULL))
);

GRANT SELECT, INSERT, UPDATE, DELETE ON dokpod.audit_export_destinations, dokpod.audit_export_cursors TO dokpod_runtime;
