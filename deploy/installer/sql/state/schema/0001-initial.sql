-- Spec 033 T019 (FR-044, D-014): estado da instalação, sem segredos. Forward-only; o último arquivo aplicado fica em
-- PRAGMA user_version. Modelo em specs/033-instalador-guiado/data-model.md, seção "Estado da instalação (SQLite)".

CREATE TABLE installation (
    installation_id TEXT NOT NULL PRIMARY KEY,
    singleton INTEGER NOT NULL DEFAULT 1 CHECK (singleton = 1) UNIQUE,
    version TEXT NOT NULL,
    edition TEXT NOT NULL CHECK (edition IN ('ce', 'business')),
    engine TEXT NOT NULL CHECK (engine IN ('docker-rootless', 'podman-rootless')),
    public_url TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE TABLE components (
    name TEXT NOT NULL PRIMARY KEY CHECK (name IN (
        'dokpod-postgres', 'keycloak-postgres', 'keycloak', 'edge', 'web', 'bff', 'api', 'agent', 'realm', 'migrations', 'pki', 'secrets')),
    origin TEXT NOT NULL CHECK (origin IN ('provisioned', 'existing')),
    desired_hash TEXT,
    observed_json TEXT,
    recreatable INTEGER NOT NULL CHECK (recreatable IN (0, 1)),
    updated_at TEXT NOT NULL
);

CREATE TABLE certificates (
    role TEXT NOT NULL PRIMARY KEY,
    origin TEXT NOT NULL CHECK (origin IN ('local-ca', 'organization')),
    subject_alt_names_json TEXT NOT NULL,
    not_after TEXT NOT NULL,
    fingerprint_sha256 TEXT NOT NULL,
    secret_ref TEXT,
    ca_state TEXT CHECK (ca_state IS NULL OR ca_state IN ('active', 'rotating')),
    updated_at TEXT NOT NULL
);

CREATE TABLE environments (
    environment_id TEXT NOT NULL PRIMARY KEY,
    name TEXT NOT NULL,
    engine TEXT NOT NULL CHECK (engine IN ('docker', 'podman', 'windows')),
    agent_fingerprint TEXT,
    package_generated_at TEXT,
    revoked_at TEXT
);

CREATE TABLE runs (
    run_id TEXT NOT NULL PRIMARY KEY,
    command TEXT NOT NULL,
    started_at TEXT NOT NULL,
    finished_at TEXT,
    outcome TEXT CHECK (outcome IS NULL OR outcome IN (
        'converged', 'changed', 'prerequisite_failed', 'declined', 'failed', 'paused_for_manual_migration')),
    exit_code INTEGER,
    report_path TEXT
);

CREATE INDEX ix_runs_started_at ON runs (started_at);

CREATE TABLE run_steps (
    run_id TEXT NOT NULL REFERENCES runs (run_id) ON DELETE CASCADE,
    step TEXT NOT NULL,
    status TEXT NOT NULL CHECK (status IN ('done', 'failed', 'skipped')),
    at TEXT NOT NULL,
    PRIMARY KEY (run_id, step)
);

CREATE TABLE sql_overrides (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    path TEXT NOT NULL,
    purpose TEXT NOT NULL,
    original_sha256 TEXT NOT NULL,
    applied_sha256 TEXT NOT NULL,
    accepted_schema_fingerprint TEXT,
    confirmed_by TEXT NOT NULL,
    confirmed_at TEXT NOT NULL,
    run_id TEXT NOT NULL REFERENCES runs (run_id)
);

CREATE TABLE run_lock (
    id INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),
    run_id TEXT NOT NULL,
    container_name TEXT NOT NULL,
    acquired_at TEXT NOT NULL
);

PRAGMA user_version = 1;
