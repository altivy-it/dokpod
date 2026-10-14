-- Spec 010: ações pause (5) e unpause (6) no pipeline de comandos (expand).
ALTER TABLE dokpod.agent_commands DROP CONSTRAINT ck_agent_commands_kind;
ALTER TABLE dokpod.agent_commands ADD CONSTRAINT ck_agent_commands_kind CHECK (kind BETWEEN 1 AND 6);
