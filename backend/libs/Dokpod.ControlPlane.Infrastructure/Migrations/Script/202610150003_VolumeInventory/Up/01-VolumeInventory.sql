-- Spec 018: inventário de volumes por host (projeção substituída por snapshot) e comando de remoção de volume.
CREATE TABLE dokpod.inventory_volume_projections (
    environment_id uuid NOT NULL CONSTRAINT pk_inventory_volume_projections PRIMARY KEY,
    revision bigint NOT NULL,
    observed_at_utc timestamp with time zone NOT NULL,
    CONSTRAINT fk_inventory_volume_projections_environments FOREIGN KEY (environment_id)
        REFERENCES dokpod.environments (environment_id) ON DELETE CASCADE
);

CREATE TABLE dokpod.inventory_volumes (
    environment_id uuid NOT NULL,
    name character varying(255) NOT NULL,
    anonymous boolean NOT NULL,
    driver character varying(255) NOT NULL,
    scope character varying(255) NOT NULL,
    created_at timestamp with time zone,
    labels jsonb NOT NULL DEFAULT '{}'::jsonb,
    -- [{containerId, containerName, destination, readOnly}]; nunca o caminho do volume no host (D-002).
    mounts jsonb NOT NULL DEFAULT '[]'::jsonb,
    mount_count integer NOT NULL,
    truncated boolean NOT NULL DEFAULT false,
    observed_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_inventory_volumes PRIMARY KEY (environment_id, name),
    CONSTRAINT fk_inventory_volumes_projections FOREIGN KEY (environment_id)
        REFERENCES dokpod.inventory_volume_projections (environment_id) ON DELETE CASCADE,
    CONSTRAINT ck_inventory_volumes_mounts CHECK (mount_count >= 0 AND jsonb_typeof(mounts) = 'array'),
    CONSTRAINT ck_inventory_volumes_labels CHECK (jsonb_typeof(labels) = 'object')
);

CREATE INDEX ix_inventory_volumes_unused ON dokpod.inventory_volumes (environment_id) WHERE mount_count = 0;

GRANT SELECT, INSERT, UPDATE, DELETE ON dokpod.inventory_volume_projections, dokpod.inventory_volumes TO dokpod_runtime;

-- Comando de host novo (tipo 9) com parâmetros próprios.
ALTER TABLE dokpod.agent_commands DROP CONSTRAINT ck_agent_commands_kind;
ALTER TABLE dokpod.agent_commands ADD CONSTRAINT ck_agent_commands_kind CHECK (kind BETWEEN 1 AND 9);
ALTER TABLE dokpod.agent_command_parameters DROP CONSTRAINT ck_agent_command_parameters_kind;
ALTER TABLE dokpod.agent_command_parameters ADD CONSTRAINT ck_agent_command_parameters_kind
    CHECK (kind IN ('pull_image', 'create_container', 'remove_volume'));

-- D-006: a lista de scopes do registro é fixada no cadastro; quem já permite remover containers passa a permitir
-- remover volumes (mesma classe de ação destrutiva). A concessão por usuário continua no Keycloak.
UPDATE dokpod.environments
SET scopes = array_to_json(ARRAY(
        SELECT DISTINCT scope FROM jsonb_array_elements_text(scopes::jsonb || '["volume:delete"]'::jsonb) AS scope ORDER BY scope))::text
WHERE scopes::jsonb ? 'container:delete' AND NOT scopes::jsonb ? 'volume:delete';
