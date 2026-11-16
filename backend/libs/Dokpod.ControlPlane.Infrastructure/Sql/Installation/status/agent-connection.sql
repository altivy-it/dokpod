-- Spec 033 (T044, FR-030): conexão do agente de um ambiente, para a troca da CA esperar a reconexão com o certificado
-- novo antes de tirar a raiz antiga. Executor psql (-At -F '|'), conectado ao banco do Dokpod com a role dona.
-- Parâmetros por -v: environment_id (literal, UUID). Saída: uma linha `connected|session_started_at` (`t` ou `f` e a
-- época em segundos, 0 sem sessão), ou nenhuma linha quando o agente nunca conectou.
\set ON_ERROR_STOP on

SELECT connected, COALESCE(extract(epoch FROM session_started_at_utc)::bigint, 0)
FROM dokpod.agent_connections
WHERE environment_id = :'environment_id'::uuid;
