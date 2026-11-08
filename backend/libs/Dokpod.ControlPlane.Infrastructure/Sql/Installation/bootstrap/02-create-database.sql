-- Spec 033 (FR-007, FR-008): banco e schema do Dokpod, de propriedade da role dona.
-- Executor psql, com a credencial administrativa. Parâmetros por -v: database, owner_role (identificadores).
-- Idempotente: cria o banco ausente; o schema `dokpod` é o mesmo que as migrations usam (IF NOT EXISTS).
\set ON_ERROR_STOP on

SELECT format('CREATE DATABASE %I OWNER %I ENCODING %L TEMPLATE template0', :'database', :'owner_role', 'UTF8')
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = :'database')
\gexec

\connect :"database"

REVOKE CREATE ON SCHEMA public FROM PUBLIC;
CREATE SCHEMA IF NOT EXISTS dokpod AUTHORIZATION :"owner_role";
ALTER SCHEMA dokpod OWNER TO :"owner_role";
