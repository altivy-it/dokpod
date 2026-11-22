-- Spec 026 (T011): janelas de mudança por estágio. A tabela existe nas duas edições para manter um só schema; só a
-- composição Business grava e lê nela. Tem no máximo uma linha por estágio, então não cresce nem é particionada (D-003).
CREATE TABLE dokpod.change_windows (
    stage text NOT NULL CONSTRAINT pk_change_windows PRIMARY KEY,
    time_zone text NOT NULL,
    ranges jsonb NOT NULL,
    version integer NOT NULL,
    updated_by text NOT NULL,
    updated_at_utc timestamp with time zone NOT NULL,
    CONSTRAINT ck_change_windows_stage CHECK (stage IN ('production', 'staging', 'development')),
    CONSTRAINT ck_change_windows_time_zone CHECK (length(time_zone) BETWEEN 1 AND 64),
    CONSTRAINT ck_change_windows_ranges CHECK (jsonb_typeof(ranges) = 'array' AND jsonb_array_length(ranges) BETWEEN 1 AND 14),
    CONSTRAINT ck_change_windows_version CHECK (version >= 1)
);

GRANT SELECT, INSERT, UPDATE, DELETE ON dokpod.change_windows TO dokpod_runtime;

-- D-009: os ambientes cadastrados passam a declarar change:override. A concessão por usuário continua no Keycloak,
-- onde nenhum papel recebe o scope por padrão. O CASE só converte o que é uma lista JSON, como o cadastro grava.
UPDATE dokpod.environments
SET scopes = array_to_json(ARRAY(
        SELECT DISTINCT scope FROM jsonb_array_elements_text(scopes::jsonb || '["change:override"]'::jsonb) AS scope ORDER BY scope))::text
WHERE CASE WHEN left(btrim(scopes), 1) = '[' THEN NOT scopes::jsonb ? 'change:override' ELSE false END;
