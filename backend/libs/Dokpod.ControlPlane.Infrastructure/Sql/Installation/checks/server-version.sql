-- Spec 033 (FR-003, FR-008): versão do servidor PostgreSQL, comparada pelo instalador com a mínima homologada.
-- Executor psql (-At). Sem parâmetros. Saída: uma linha, `server_version_num` (por exemplo 170006).
\set ON_ERROR_STOP on

SELECT current_setting('server_version_num')::integer AS server_version_num;
