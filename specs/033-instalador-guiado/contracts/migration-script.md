# Contrato: SQL de migrations exportado

Gerado por `admin migrations script` ([contrato](admin-commands.md)) e por `install.sh migrations export`. O objetivo é
dar ao usuário, por exemplo a um DBA, controle sobre o que vai ao banco (revisão de 2026-11-08, FR-037).

## Estrutura

```sql
-- dokpod-migrations: 1
-- product: dokpod 0.8.0 (dokpod-ce-api@sha256:<digest>)
-- from: 202611010002_ImageUpdateCheck
-- to: 202611150001_<Nome>
-- plan-hash: <sha256>
-- migration: 202611150001_<Nome> sha256=<sha256>
-- override: Migrations/Script/202611150001_<Nome>/Up/01-<Nome>.sql original=<sha256> applied=<sha256>
-- generated-at: 2026-11-15T12:00:00Z
-- Aplicar com: psql -v ON_ERROR_STOP=1 -f <arquivo>  (cada migration tem a própria transação)

-- (conteúdo de Installation/migrations/session.sql: fuso UTC e search_path dokpod)

-- dokpod-migration-begin: 202611150001_<Nome> sha256=<sha256>
START TRANSACTION;
-- SQL da migration, no formato do EF sem --idempotent
INSERT INTO dokpod."__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('202611150001_<Nome>', '10.0.0');
COMMIT;
-- dokpod-migration-end: 202611150001_<Nome>
```

## Regras

- Há uma linha `-- migration:` por migration pendente, na ordem de aplicação. O `sha256` é o do bloco entre os
  marcadores `-- dokpod-migration-begin:` e `-- dokpod-migration-end:` daquela migration, repetido no marcador de
  início. Num banco vazio, o primeiro bloco também cria o histórico.
- O arquivo começa pelo SQL de sessão do catálogo (fuso UTC e `search_path` `dokpod`), o mesmo do executor da aplicação.
- O arquivo não usa blocos `DO` para controle de reexecução. Reaplicar uma migration já aplicada falha na inserção do
  histórico (chave primária) dentro da transação, que é desfeita. Esse é o comportamento seguro esperado.
- Há uma linha `-- override:` por arquivo do catálogo sobreposto que entrou neste arquivo (FR-041). Sem sobreposição,
  não há nenhuma.
- O arquivo não contém segredo nem nome de role com senha. `GRANT`s, quando existem, nomeiam as roles
  `dokpod_owner` e `dokpod_runtime`.
- `from` vazio significa banco novo: o arquivo cria o schema `dokpod` e o histórico.
- **Compatibilidade**: executável por `psql` da versão do servidor homologado ou superior, sem extensões além das que as
  próprias migrations criam.
- Depois da aplicação manual, `admin migrations verify` confere o histórico e o schema real contra a impressão
  esperada (FR-042). Com `--script <arquivo>`, também aponta os arquivos alterados. O instalador só segue com
  `ok: true`.
