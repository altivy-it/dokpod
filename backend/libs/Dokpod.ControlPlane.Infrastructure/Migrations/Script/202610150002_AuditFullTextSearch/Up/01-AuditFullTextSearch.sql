-- Spec 017 D-008: busca textual (full-text) na trilha de auditoria particionada.
-- Índice GIN por expressão no pai particionado: propaga para todas as partições, sem coluna nova nem reescrita da
-- trilha (append-only). A construção trava gravações de auditoria durante a migration (ver runbook).

-- Remove acentos sem depender da extensão unaccent (portável e IMMUTABLE, exigido por índice).
CREATE OR REPLACE FUNCTION dokpod.dokpod_fold_text(p_text text)
RETURNS text
LANGUAGE sql
IMMUTABLE
PARALLEL SAFE
RETURN translate(
    lower(coalesce(p_text, '')),
    'áàâãäåéèêëíìîïóòôõöúùûüçñý',
    'aaaaaaeeeeiiiiooooouuuucny');

-- Documento pesquisável: ação (código e palavras), ator, alvo, código de falha e valores dos detalhes.
CREATE OR REPLACE FUNCTION dokpod.dokpod_audit_search_document(
    p_action varchar,
    p_actor_id varchar,
    p_actor_name varchar,
    p_subject_id varchar,
    p_failure_code varchar,
    p_details jsonb)
RETURNS tsvector
LANGUAGE sql
IMMUTABLE
PARALLEL SAFE
-- Custo alto: o planejador prefere o índice GIN a calcular o vetor linha a linha (termos raros em 1 milhão de linhas).
COST 10000
RETURN to_tsvector('portuguese'::regconfig, dokpod.dokpod_fold_text(concat_ws(' ',
    p_action,
    translate(p_action, '._', '  '),
    p_actor_id,
    p_actor_name,
    p_subject_id,
    p_failure_code,
    translate(coalesce(p_failure_code, ''), '._', '  '),
    (SELECT string_agg(value, ' ') FROM jsonb_each_text(coalesce(p_details, '{}'::jsonb))))));

-- Consulta no mesmo dicionário e normalização (sintaxe de busca web: aspas, OR, -exclusão).
CREATE OR REPLACE FUNCTION dokpod.dokpod_audit_search_query(p_text text)
RETURNS tsquery
LANGUAGE sql
IMMUTABLE
PARALLEL SAFE
RETURN websearch_to_tsquery('portuguese'::regconfig, dokpod.dokpod_fold_text(p_text));

-- Prefixo do alvo (ids de container em minúsculas) indexável: com o GIN, o OR da busca vira BitmapOr.
CREATE INDEX IF NOT EXISTS ix_audit_events_subject_pattern
    ON dokpod.audit_events (subject_id varchar_pattern_ops);

CREATE INDEX IF NOT EXISTS ix_audit_events_search
    ON dokpod.audit_events
    USING gin (dokpod.dokpod_audit_search_document(action, actor_id, actor_name, subject_id, failure_code, details));

GRANT EXECUTE ON FUNCTION dokpod.dokpod_fold_text(text) TO dokpod_runtime;
GRANT EXECUTE ON FUNCTION dokpod.dokpod_audit_search_document(varchar, varchar, varchar, varchar, varchar, jsonb) TO dokpod_runtime;
GRANT EXECUTE ON FUNCTION dokpod.dokpod_audit_search_query(text) TO dokpod_runtime;
