-- Spec 033 (D-003): sessão das migrations igual à do executor da aplicação (ControlPlaneConnectionString): fuso UTC,
-- porque a migration 202609220002 calcula os limites das partições pelo fuso da sessão, e o schema `dokpod`, onde as
-- migrations anteriores ao isolamento do schema criam as tabelas. Executor admin, sem parâmetros; vai no início do SQL
-- exportado e roda antes do `apply`.
SET TIME ZONE 'UTC';
SET search_path TO dokpod;
