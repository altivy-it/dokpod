-- Spec 033 (SC-008): contagens de conferência antes e depois de uma recriação total recusada.
-- Executor admin (Npgsql), sem parâmetros. Saída: uma linha por tabela, `name` e `rows`.
SELECT 'audit_events' AS name, count(*)::bigint AS rows FROM dokpod.audit_events
UNION ALL
SELECT 'agent_identities', count(*)::bigint FROM dokpod.agent_identities
ORDER BY name;
