-- Spec 021: inventário de pods do Podman por host (projeção substituída por snapshot) e ações de pod.
CREATE TABLE dokpod.inventory_pod_projections (
    environment_id uuid NOT NULL CONSTRAINT pk_inventory_pod_projections PRIMARY KEY,
    revision bigint NOT NULL,
    -- Falso quando o engine não tem pods (Docker).
    pods_supported boolean NOT NULL,
    observed_at_utc timestamp with time zone NOT NULL,
    CONSTRAINT fk_inventory_pod_projections_environments FOREIGN KEY (environment_id)
        REFERENCES dokpod.environments (environment_id) ON DELETE CASCADE
);

CREATE TABLE dokpod.inventory_pods (
    environment_id uuid NOT NULL,
    pod_id character(64) NOT NULL,
    name character varying(255) NOT NULL,
    status character varying(64) NOT NULL,
    created_at timestamp with time zone,
    labels jsonb NOT NULL DEFAULT '{}'::jsonb,
    networks jsonb NOT NULL DEFAULT '[]'::jsonb,
    infra_id character(64),
    -- [{containerId, name, status, restarts, infra}]
    containers jsonb NOT NULL DEFAULT '[]'::jsonb,
    container_count integer NOT NULL,
    running_count integer NOT NULL,
    agent_pod boolean NOT NULL,
    truncated boolean NOT NULL DEFAULT false,
    observed_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_inventory_pods PRIMARY KEY (environment_id, pod_id),
    CONSTRAINT fk_inventory_pods_projections FOREIGN KEY (environment_id)
        REFERENCES dokpod.inventory_pod_projections (environment_id) ON DELETE CASCADE,
    CONSTRAINT ck_inventory_pods_id CHECK (pod_id ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_inventory_pods_containers CHECK (jsonb_typeof(containers) = 'array' AND container_count >= 0 AND running_count >= 0),
    CONSTRAINT ck_inventory_pods_labels CHECK (jsonb_typeof(labels) = 'object')
);

CREATE INDEX ix_inventory_pods_name ON dokpod.inventory_pods (environment_id, name);
-- Pods que contêm um container (link do detalhe do container).
CREATE INDEX ix_inventory_pods_containers ON dokpod.inventory_pods USING gin (containers jsonb_path_ops);

GRANT SELECT, INSERT, UPDATE, DELETE ON dokpod.inventory_pod_projections, dokpod.inventory_pods TO dokpod_runtime;

-- Comando de host novo (tipo 11), parâmetros próprios e membros do pod que falharam (FR-008).
ALTER TABLE dokpod.agent_commands DROP CONSTRAINT ck_agent_commands_kind;
ALTER TABLE dokpod.agent_commands ADD CONSTRAINT ck_agent_commands_kind CHECK (kind BETWEEN 1 AND 11);
ALTER TABLE dokpod.agent_commands ADD COLUMN failed_containers character varying(4096);
ALTER TABLE dokpod.agent_command_parameters DROP CONSTRAINT ck_agent_command_parameters_kind;
ALTER TABLE dokpod.agent_command_parameters ADD CONSTRAINT ck_agent_command_parameters_kind
    CHECK (kind IN ('pull_image', 'create_container', 'remove_volume', 'remove_network', 'pod_action'));
