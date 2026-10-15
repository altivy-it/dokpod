UPDATE dokpod.environments
SET scopes = array_to_json(ARRAY(
        SELECT scope FROM jsonb_array_elements_text(scopes::jsonb) AS scope WHERE scope <> 'network:delete' ORDER BY scope))::text
WHERE scopes::jsonb ? 'network:delete';

-- Remoções de rede já gravadas impedem voltar a restrição; o Down falha nesse caso para não apagar comandos.
ALTER TABLE dokpod.agent_command_parameters DROP CONSTRAINT ck_agent_command_parameters_kind;
ALTER TABLE dokpod.agent_command_parameters ADD CONSTRAINT ck_agent_command_parameters_kind
    CHECK (kind IN ('pull_image', 'create_container', 'remove_volume'));
ALTER TABLE dokpod.agent_commands DROP CONSTRAINT ck_agent_commands_kind;
ALTER TABLE dokpod.agent_commands ADD CONSTRAINT ck_agent_commands_kind CHECK (kind BETWEEN 1 AND 9);

DROP TABLE IF EXISTS dokpod.inventory_networks;
DROP TABLE IF EXISTS dokpod.inventory_network_projections;
