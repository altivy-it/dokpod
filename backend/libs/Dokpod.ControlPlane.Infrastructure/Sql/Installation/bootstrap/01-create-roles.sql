-- Spec 033 (FR-008, D-009, D-013): roles do Dokpod, sem SUPERUSER, CREATEROLE, CREATEDB, REPLICATION nem BYPASSRLS.
-- Executor psql, conectado com a credencial administrativa ao banco de manutenção. Parâmetros por -v:
--   owner_role, runtime_role (identificadores);
--   owner_password_file, runtime_password_file (caminhos dos arquivos montados em /run/secrets).
-- As senhas são lidas aqui, dos arquivos, e nunca passam por -v nem por variável de ambiente (L1/A1 do threat model).
--
-- Os privilégios do runtime vêm das próprias migrations, concedidos ao grupo `dokpod_runtime` (NOLOGIN, criado pela
-- migration 202609120001 quando ausente), com regras finas como a auditoria só por função de append. Aqui o grupo é
-- criado antes, porque a role dona não tem CREATEROLE, e a role de runtime entra nele. A role de runtime pode ser o
-- próprio `dokpod_runtime`, que então passa a ter LOGIN.
-- Idempotente: cria o que falta e reaplica atributos, senhas e participação no grupo.
\set ON_ERROR_STOP on
\set owner_password `cat :'owner_password_file'`
\set runtime_password `cat :'runtime_password_file'`

SELECT 'CREATE ROLE dokpod_runtime NOLOGIN'
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'dokpod_runtime')
\gexec

SELECT format('CREATE ROLE %I', :'owner_role')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'owner_role')
\gexec

SELECT format('CREATE ROLE %I', :'runtime_role')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'runtime_role')
\gexec

SELECT format('ALTER ROLE %I LOGIN PASSWORD %L', :'owner_role', :'owner_password')
\gexec

SELECT format('ALTER ROLE %I LOGIN PASSWORD %L', :'runtime_role', :'runtime_password')
\gexec

-- Só os atributos que a role tem: citar SUPERUSER, REPLICATION ou BYPASSRLS num ALTER ROLE, mesmo para retirar, exige
-- o próprio atributo, e um administrador sem SUPERUSER (CREATEROLE e CREATEDB, como em PostgreSQL gerenciado) falharia.
SELECT format('ALTER ROLE %I%s%s%s%s%s', rolname,
    CASE WHEN rolsuper THEN ' NOSUPERUSER' ELSE '' END, CASE WHEN rolcreaterole THEN ' NOCREATEROLE' ELSE '' END,
    CASE WHEN rolcreatedb THEN ' NOCREATEDB' ELSE '' END, CASE WHEN rolreplication THEN ' NOREPLICATION' ELSE '' END,
    CASE WHEN rolbypassrls THEN ' NOBYPASSRLS' ELSE '' END)
FROM pg_roles
WHERE rolname IN (:'owner_role', :'runtime_role') AND (rolsuper OR rolcreaterole OR rolcreatedb OR rolreplication OR rolbypassrls)
\gexec

SELECT format('GRANT dokpod_runtime TO %I', :'runtime_role')
WHERE :'runtime_role' <> 'dokpod_runtime'
\gexec

\unset owner_password
\unset runtime_password
