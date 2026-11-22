-- Contract da spec 026: remove o scope dos ambientes e a tabela das janelas.
UPDATE dokpod.environments
SET scopes = array_to_json(ARRAY(
        SELECT scope FROM jsonb_array_elements_text(scopes::jsonb) AS scope WHERE scope <> 'change:override' ORDER BY scope))::text
WHERE CASE WHEN left(btrim(scopes), 1) = '[' THEN scopes::jsonb ? 'change:override' ELSE false END;

DROP TABLE dokpod.change_windows;
