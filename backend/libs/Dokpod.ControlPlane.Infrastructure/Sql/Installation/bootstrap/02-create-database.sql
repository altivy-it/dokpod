-- Spec 033 (FR-007, FR-008): banco e schema do Dokpod, de propriedade da role dona.
-- Executor psql, com a credencial administrativa. Parâmetros por -v: database, owner_role (identificadores).
-- Idempotente: cria o banco ausente; o schema `dokpod` é o mesmo que as migrations usam (IF NOT EXISTS).
--
-- Um administrador sem SUPERUSER só dá a posse do banco a quem ele pode assumir (PostgreSQL 16+), e só conecta ao
-- banco depois que 03-grants.sql tira o CONNECT de PUBLIC. Quando falta, ele entra em owner_role durante este arquivo
-- (CREATEROLE dá a ADMIN OPTION sobre as roles que criou) e sai no fim. O superusuário já pode tudo e não muda.
\set ON_ERROR_STOP on

SELECT NOT (pg_has_role(current_user, :'owner_role', 'SET') AND pg_has_role(current_user, :'owner_role', 'USAGE')) AS dokpod_temporary_member
\gset
\if :dokpod_temporary_member
SELECT format('GRANT %I TO %I WITH INHERIT TRUE, SET TRUE', :'owner_role', current_user)
\gexec
\endif

SELECT format('CREATE DATABASE %I OWNER %I ENCODING %L TEMPLATE template0', :'database', :'owner_role', 'UTF8')
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = :'database')
\gexec

\connect :"database"

REVOKE CREATE ON SCHEMA public FROM PUBLIC;
CREATE SCHEMA IF NOT EXISTS dokpod AUTHORIZATION :"owner_role";
ALTER SCHEMA dokpod OWNER TO :"owner_role";

\if :dokpod_temporary_member
SELECT format('REVOKE %I FROM %I', :'owner_role', current_user)
\gexec
\endif
