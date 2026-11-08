-- Spec 033 (FR-042, D-013): impressão do schema `dokpod`, comparada com schema/expected-schema.json da versão.
-- Executor admin (Npgsql), sem parâmetros. Saída: uma linha por objeto, `kind`, `name` e `definition_sha256`.
-- Ficam de fora as partições (criadas antecipadamente em execução e na retenção), o que é herdado delas e as concessões,
-- que variam por instalação e por versão do PostgreSQL.
-- `definition_sha256` é o SHA-256 da definição normalizada pelo próprio PostgreSQL (pg_get_*def, format_type).
WITH tables AS (
    SELECT c.oid, c.relname, c.relkind
    FROM pg_class c
    JOIN pg_namespace n ON n.oid = c.relnamespace
    WHERE n.nspname = 'dokpod' AND c.relkind IN ('r', 'p', 'v', 'm') AND NOT c.relispartition
),
objects AS (
    SELECT CASE relkind WHEN 'v' THEN 'view' WHEN 'm' THEN 'view' ELSE 'table' END AS kind,
           relname::text AS name,
           CASE relkind WHEN 'v' THEN pg_get_viewdef(oid) WHEN 'm' THEN pg_get_viewdef(oid) ELSE relkind::text END AS definition
    FROM tables
    UNION ALL
    SELECT 'column', t.relname || '.' || a.attname,
           format_type(a.atttypid, a.atttypmod) || CASE WHEN a.attnotnull THEN ' not null' ELSE '' END
           || COALESCE(' default ' || pg_get_expr(d.adbin, d.adrelid), '')
           || CASE WHEN a.attidentity <> '' THEN ' identity ' || a.attidentity::text ELSE '' END
           || CASE WHEN a.attgenerated <> '' THEN ' generated ' || a.attgenerated::text ELSE '' END
    FROM tables t
    JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum > 0 AND NOT a.attisdropped
    LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
    WHERE t.relkind IN ('r', 'p')
    UNION ALL
    SELECT 'index', t.relname || '.' || i.relname, pg_get_indexdef(x.indexrelid)
    FROM tables t
    JOIN pg_index x ON x.indrelid = t.oid
    JOIN pg_class i ON i.oid = x.indexrelid
    UNION ALL
    SELECT 'constraint', t.relname || '.' || k.conname, pg_get_constraintdef(k.oid)
    FROM tables t
    JOIN pg_constraint k ON k.conrelid = t.oid
    -- Sem as cópias herdadas por partição (conparentid) e sem NOT NULL, que o PostgreSQL 18 passou a guardar aqui e já está
    -- na coluna: as duas dependem da versão e das partições, não do schema.
    WHERE k.conparentid = 0 AND k.contype <> 'n'
    UNION ALL
    SELECT 'function', p.proname || '(' || pg_get_function_identity_arguments(p.oid) || ')',
           pg_get_function_result(p.oid) || ' ' || l.lanname || ' ' || p.prosecdef::text || ' ' || p.prosrc
    FROM pg_proc p
    JOIN pg_namespace n ON n.oid = p.pronamespace
    JOIN pg_language l ON l.oid = p.prolang
    WHERE n.nspname = 'dokpod'
    UNION ALL
    SELECT 'trigger', t.relname || '.' || g.tgname, pg_get_triggerdef(g.oid)
    FROM tables t
    JOIN pg_trigger g ON g.tgrelid = t.oid
    WHERE NOT g.tgisinternal AND g.tgparentid = 0
)
SELECT kind, name, encode(sha256(convert_to(definition, 'UTF8')), 'hex') AS definition_sha256
FROM objects
ORDER BY kind, name;
