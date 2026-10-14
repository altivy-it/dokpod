-- Reverte a spec 013: remove comandos de criação (tipo 8) e as colunas de proteção.
REVOKE UPDATE (protected_values, purged_at) ON dokpod.agent_command_parameters FROM dokpod_runtime;
DELETE FROM dokpod.agent_commands WHERE kind = 8;
ALTER TABLE dokpod.agent_command_parameters DROP CONSTRAINT ck_agent_command_parameters_purge;
ALTER TABLE dokpod.agent_command_parameters DROP COLUMN purged_at;
ALTER TABLE dokpod.agent_command_parameters DROP COLUMN protected_values;
ALTER TABLE dokpod.agent_command_parameters DROP CONSTRAINT ck_agent_command_parameters_kind;
ALTER TABLE dokpod.agent_command_parameters ADD CONSTRAINT ck_agent_command_parameters_kind CHECK (kind IN ('pull_image'));
ALTER TABLE dokpod.agent_commands DROP COLUMN created_container_id;
ALTER TABLE dokpod.agent_commands DROP CONSTRAINT ck_agent_commands_kind;
ALTER TABLE dokpod.agent_commands ADD CONSTRAINT ck_agent_commands_kind CHECK (kind BETWEEN 1 AND 7);
