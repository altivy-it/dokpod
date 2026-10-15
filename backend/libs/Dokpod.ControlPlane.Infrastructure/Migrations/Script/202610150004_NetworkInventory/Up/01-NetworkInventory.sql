-- Spec 019: inventário de redes por host (projeção substituída por snapshot) e comando de remoção de rede.
CREATE TABLE dokpod.inventory_network_projections (
    environment_id uuid NOT NULL CONSTRAINT pk_inventory_network_projections PRIMARY KEY,
    revision bigint NOT NULL,
    observed_at_utc timestamp with time zone NOT NULL,
    CONSTRAINT fk_inventory_network_projections_environments FOREIGN KEY (environment_id)
        REFERENCES dokpod.environments (environment_id) ON DELETE CASCADE
);

CREATE TABLE dokpod.inventory_networks (
    environment_id uuid NOT NULL,
    network_id character(64) NOT NULL,
    name character varying(255) NOT NULL,
    driver character varying(255) NOT NULL,
    scope character varying(255) NOT NULL,
    internal boolean NOT NULL,
    ipv6 boolean NOT NULL,
    attachable boolean NOT NULL,
    system boolean NOT NULL,
    created_at timestamp with time zone,
    -- [{subnet, gateway}]; opções do driver nunca são gravadas (D-002).
    subnets jsonb NOT NULL DEFAULT '[]'::jsonb,
    labels jsonb NOT NULL DEFAULT '{}'::jsonb,
    -- [{containerId, containerName, ipv4, ipv6}], inclusive containers parados.
    containers jsonb NOT NULL DEFAULT '[]'::jsonb,
    container_count integer NOT NULL,
    truncated boolean NOT NULL DEFAULT false,
    observed_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_inventory_networks PRIMARY KEY (environment_id, network_id),
    CONSTRAINT fk_inventory_networks_projections FOREIGN KEY (environment_id)
        REFERENCES dokpod.inventory_network_projections (environment_id) ON DELETE CASCADE,
    CONSTRAINT ck_inventory_networks_id CHECK (network_id ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_inventory_networks_containers CHECK (container_count >= 0 AND jsonb_typeof(containers) = 'array'),
    CONSTRAINT ck_inventory_networks_subnets CHECK (jsonb_typeof(subnets) = 'array'),
    CONSTRAINT ck_inventory_networks_labels CHECK (jsonb_typeof(labels) = 'object')
);

CREATE INDEX ix_inventory_networks_name ON dokpod.inventory_networks (environment_id, name);
CREATE INDEX ix_inventory_networks_unused ON dokpod.inventory_networks (environment_id) WHERE container_count = 0 AND NOT system;

GRANT SELECT, INSERT, UPDATE, DELETE ON dokpod.inventory_network_projections, dokpod.inventory_networks TO dokpod_runtime;

-- Comando de host novo (tipo 10) com parâmetros próprios.
ALTER TABLE dokpod.agent_commands DROP CONSTRAINT ck_agent_commands_kind;
ALTER TABLE dokpod.agent_commands ADD CONSTRAINT ck_agent_commands_kind CHECK (kind BETWEEN 1 AND 10);
ALTER TABLE dokpod.agent_command_parameters DROP CONSTRAINT ck_agent_command_parameters_kind;
ALTER TABLE dokpod.agent_command_parameters ADD CONSTRAINT ck_agent_command_parameters_kind
    CHECK (kind IN ('pull_image', 'create_container', 'remove_volume', 'remove_network'));

-- D-006: quem já permite remover containers passa a permitir remover redes. A concessão por usuário continua no Keycloak.
UPDATE dokpod.environments
SET scopes = array_to_json(ARRAY(
        SELECT DISTINCT scope FROM jsonb_array_elements_text(scopes::jsonb || '["network:delete"]'::jsonb) AS scope ORDER BY scope))::text
WHERE scopes::jsonb ? 'container:delete' AND NOT scopes::jsonb ? 'network:delete';
