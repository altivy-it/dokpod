-- Spec 033 (D-009): acesso da role de runtime ao banco do Dokpod. Executor psql, conectado ao banco de manutenção com a
-- credencial administrativa (os privilégios de banco valem de qualquer banco, e um administrador sem SUPERUSER não
-- conecta mais ao do Dokpod depois do REVOKE abaixo). Parâmetros por -v: database, owner_role, runtime_role (identificadores).
--
-- Só a conexão: os privilégios em tabelas, funções e schema são os que as migrations concedem ao grupo
-- `dokpod_runtime` (do qual a role de runtime é membro, 01-create-roles.sql). Conceder DML amplo aqui quebraria regras
-- deliberadas das migrations, como a auditoria só por função de append. Idempotente.
--
-- Sem SUPERUSER, o administrador concede como dono do banco: entra em owner_role durante este arquivo, como em
-- 02-create-database.sql, e sai no fim.
\set ON_ERROR_STOP on

SELECT NOT (pg_has_role(current_user, :'owner_role', 'SET') AND pg_has_role(current_user, :'owner_role', 'USAGE')) AS dokpod_temporary_member
\gset
\if :dokpod_temporary_member
SELECT format('GRANT %I TO %I WITH INHERIT TRUE, SET TRUE', :'owner_role', current_user)
\gexec
\endif

GRANT CONNECT ON DATABASE :"database" TO :"runtime_role";
REVOKE CONNECT ON DATABASE :"database" FROM PUBLIC;

\if :dokpod_temporary_member
SELECT format('REVOKE %I FROM %I', :'owner_role', current_user)
\gexec
\endif
