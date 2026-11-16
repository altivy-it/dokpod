-- Spec 033 (FR-008, existing-prepared): atributos e privilégios de uma role do Dokpod, para recusar excesso ou falta.
-- Executor psql (-At -F '|'), conectado ao banco do Dokpod. Parâmetros por -v: role (identificador), database
-- (identificador). Saída: uma linha por verificação, `verificação|valor` com valor `t` ou `f`. O instalador decide o
-- esperado para a role dona e para a de runtime. `runtime_group_member` diz se a role recebe os privilégios que as
-- migrations concedem ao grupo `dokpod_runtime`. `schema_exists` e `database_create` decidem, num banco preparado pelo
-- usuário antes das migrations, se a role dona pode criar o schema `dokpod` (a migration o cria quando falta).
\set ON_ERROR_STOP on

SELECT check_name, value
FROM (
    SELECT 1 AS position, 'exists' AS check_name, EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'role') AS value
    UNION ALL SELECT 2, 'login', COALESCE((SELECT rolcanlogin FROM pg_roles WHERE rolname = :'role'), false)
    UNION ALL SELECT 3, 'superuser', COALESCE((SELECT rolsuper FROM pg_roles WHERE rolname = :'role'), false)
    UNION ALL SELECT 4, 'createrole', COALESCE((SELECT rolcreaterole FROM pg_roles WHERE rolname = :'role'), false)
    UNION ALL SELECT 5, 'createdb', COALESCE((SELECT rolcreatedb FROM pg_roles WHERE rolname = :'role'), false)
    UNION ALL SELECT 6, 'replication', COALESCE((SELECT rolreplication FROM pg_roles WHERE rolname = :'role'), false)
    UNION ALL SELECT 7, 'bypassrls', COALESCE((SELECT rolbypassrls FROM pg_roles WHERE rolname = :'role'), false)
    UNION ALL SELECT 8, 'database_connect',
        EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'role') AND has_database_privilege(:'role', :'database', 'CONNECT')
    UNION ALL SELECT 9, 'schema_usage',
        EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'role') AND to_regnamespace('dokpod') IS NOT NULL
        AND has_schema_privilege(:'role', 'dokpod', 'USAGE')
    UNION ALL SELECT 10, 'schema_create',
        EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'role') AND to_regnamespace('dokpod') IS NOT NULL
        AND has_schema_privilege(:'role', 'dokpod', 'CREATE')
    UNION ALL SELECT 11, 'schema_owner',
        EXISTS (SELECT 1 FROM pg_namespace n JOIN pg_roles r ON r.oid = n.nspowner WHERE n.nspname = 'dokpod' AND r.rolname = :'role')
    UNION ALL SELECT 12, 'runtime_group_member',
        EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'role') AND EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'dokpod_runtime')
        AND pg_has_role(:'role', 'dokpod_runtime', 'MEMBER')
    UNION ALL SELECT 13, 'schema_exists', to_regnamespace('dokpod') IS NOT NULL
    UNION ALL SELECT 14, 'database_create',
        EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'role') AND has_database_privilege(:'role', :'database', 'CREATE')
) AS checks
ORDER BY position;
