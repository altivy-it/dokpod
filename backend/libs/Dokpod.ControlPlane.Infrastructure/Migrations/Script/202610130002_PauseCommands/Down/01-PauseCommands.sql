-- Rollback: só é possível sem comandos de pausa persistidos.
ALTER TABLE dokpod.agent_commands DROP CONSTRAINT ck_agent_commands_kind;
ALTER TABLE dokpod.agent_commands ADD CONSTRAINT ck_agent_commands_kind CHECK (kind BETWEEN 1 AND 4);
