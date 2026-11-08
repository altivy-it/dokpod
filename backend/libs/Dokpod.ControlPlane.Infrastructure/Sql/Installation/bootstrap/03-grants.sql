-- Spec 033 (D-009): privilégios da role de runtime: DML e EXECUTE no schema `dokpod`, sem DDL.
-- Executor psql, conectado ao banco do Dokpod como a role dona (ou administrativa). Parâmetros por -v:
--   database, owner_role, runtime_role (identificadores).
-- Rodado depois das migrations e a cada atualização; idempotente. ALTER DEFAULT PRIVILEGES cobre o que a role dona criar
-- depois (tabelas, sequências e funções de migrations futuras).
\set ON_ERROR_STOP on

GRANT CONNECT ON DATABASE :"database" TO :"runtime_role";
GRANT USAGE ON SCHEMA dokpod TO :"runtime_role";
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA dokpod TO :"runtime_role";
GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA dokpod TO :"runtime_role";
GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA dokpod TO :"runtime_role";

ALTER DEFAULT PRIVILEGES FOR ROLE :"owner_role" IN SCHEMA dokpod
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO :"runtime_role";
ALTER DEFAULT PRIVILEGES FOR ROLE :"owner_role" IN SCHEMA dokpod
    GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO :"runtime_role";
ALTER DEFAULT PRIVILEGES FOR ROLE :"owner_role" IN SCHEMA dokpod
    GRANT EXECUTE ON FUNCTIONS TO :"runtime_role";
