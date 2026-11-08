-- Spec 033 (FR-008, D-009, D-013): roles do Dokpod, sem SUPERUSER, CREATEROLE, CREATEDB nem REPLICATION.
-- Executor psql, conectado com a credencial administrativa ao banco de manutenção. Parâmetros por -v:
--   owner_role, runtime_role (identificadores);
--   owner_password_file, runtime_password_file (caminhos dos arquivos montados em /run/secrets).
-- As senhas são lidas aqui, dos arquivos, e nunca passam por -v nem por variável de ambiente (L1/A1 do threat model).
-- Idempotente: cria a role ausente e reaplica atributos e senha nas existentes.
\set ON_ERROR_STOP on
\set owner_password `cat :'owner_password_file'`
\set runtime_password `cat :'runtime_password_file'`

SELECT format('CREATE ROLE %I', :'owner_role')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'owner_role')
\gexec

SELECT format('CREATE ROLE %I', :'runtime_role')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'runtime_role')
\gexec

SELECT format('ALTER ROLE %I LOGIN NOSUPERUSER NOCREATEROLE NOCREATEDB NOREPLICATION NOBYPASSRLS PASSWORD %L', :'owner_role', :'owner_password')
\gexec

SELECT format('ALTER ROLE %I LOGIN NOSUPERUSER NOCREATEROLE NOCREATEDB NOREPLICATION NOBYPASSRLS PASSWORD %L', :'runtime_role', :'runtime_password')
\gexec

\unset owner_password
\unset runtime_password
