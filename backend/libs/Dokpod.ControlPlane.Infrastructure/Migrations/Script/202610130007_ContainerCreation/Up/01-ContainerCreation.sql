-- Spec 013: comando CREATE_CONTAINER (8) com alvo de host, valores protegidos e purga após estado terminal.
ALTER TABLE dokpod.agent_commands DROP CONSTRAINT ck_agent_commands_kind;
ALTER TABLE dokpod.agent_commands ADD CONSTRAINT ck_agent_commands_kind CHECK (kind BETWEEN 1 AND 8);
ALTER TABLE dokpod.agent_commands ADD COLUMN created_container_id character varying(128);

ALTER TABLE dokpod.agent_command_parameters DROP CONSTRAINT ck_agent_command_parameters_kind;
ALTER TABLE dokpod.agent_command_parameters ADD CONSTRAINT ck_agent_command_parameters_kind CHECK (kind IN ('pull_image', 'create_container'));
-- Valores literais de variáveis cifrados com Data Protection da API (D-002/D-006); nulos após a purga.
ALTER TABLE dokpod.agent_command_parameters ADD COLUMN protected_values bytea;
ALTER TABLE dokpod.agent_command_parameters ADD COLUMN purged_at timestamp with time zone;
ALTER TABLE dokpod.agent_command_parameters ADD CONSTRAINT ck_agent_command_parameters_purge CHECK (purged_at IS NULL OR protected_values IS NULL);

GRANT UPDATE (protected_values, purged_at) ON dokpod.agent_command_parameters TO dokpod_runtime;
