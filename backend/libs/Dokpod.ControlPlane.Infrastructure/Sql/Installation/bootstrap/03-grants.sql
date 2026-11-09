-- Spec 033 (D-009): acesso da role de runtime ao banco do Dokpod. Executor psql, conectado ao banco do Dokpod com a
-- credencial administrativa. Parâmetros por -v: database, runtime_role (identificadores).
--
-- Só a conexão: os privilégios em tabelas, funções e schema são os que as migrations concedem ao grupo
-- `dokpod_runtime` (do qual a role de runtime é membro, 01-create-roles.sql). Conceder DML amplo aqui quebraria regras
-- deliberadas das migrations, como a auditoria só por função de append. Idempotente.
\set ON_ERROR_STOP on

GRANT CONNECT ON DATABASE :"database" TO :"runtime_role";
REVOKE CONNECT ON DATABASE :"database" FROM PUBLIC;
