-- Spec 020: configuração de registros por host (substituída por mensagem do agente) e política de registros permitidos.
CREATE TABLE dokpod.inventory_registry_projections (
    environment_id uuid NOT NULL CONSTRAINT pk_inventory_registry_projections PRIMARY KEY,
    revision bigint NOT NULL,
    config_available boolean NOT NULL,
    observed_at_utc timestamp with time zone NOT NULL,
    CONSTRAINT fk_inventory_registry_projections_environments FOREIGN KEY (environment_id)
        REFERENCES dokpod.environments (environment_id) ON DELETE CASCADE
);

CREATE TABLE dokpod.inventory_registries (
    environment_id uuid NOT NULL,
    registry character varying(255) NOT NULL,
    mirrors jsonb NOT NULL DEFAULT '[]'::jsonb,
    mirror_of character varying(255),
    insecure boolean NOT NULL,
    blocked boolean NOT NULL,
    search boolean NOT NULL,
    -- Só a indicação de que o agente tem credencial; valores nunca saem do host (FR-001).
    credential boolean NOT NULL,
    observed_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_inventory_registries PRIMARY KEY (environment_id, registry),
    CONSTRAINT fk_inventory_registries_projections FOREIGN KEY (environment_id)
        REFERENCES dokpod.inventory_registry_projections (environment_id) ON DELETE CASCADE,
    CONSTRAINT ck_inventory_registries_mirrors CHECK (jsonb_typeof(mirrors) = 'array')
);

-- Política por estágio editável no console; sem linha, vale `Images:AllowedRegistriesByStage` (valor inicial).
CREATE TABLE dokpod.registry_policies (
    stage character varying(32) NOT NULL CONSTRAINT pk_registry_policies PRIMARY KEY,
    registries jsonb NOT NULL,
    updated_by character varying(255) NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT ck_registry_policies_stage CHECK (stage IN ('production', 'staging', 'development', 'none')),
    CONSTRAINT ck_registry_policies_registries CHECK (jsonb_typeof(registries) = 'array' AND jsonb_array_length(registries) <= 100)
);

GRANT SELECT, INSERT, UPDATE, DELETE ON dokpod.inventory_registry_projections, dokpod.inventory_registries, dokpod.registry_policies TO dokpod_runtime;
