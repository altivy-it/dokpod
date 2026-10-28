# Tarefas: Auditoria

**Entrada**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/audit-api.md), [quickstart.md](quickstart.md).

**Estado**: `implemented` - implementada e validada na stack local em 2026-10-15; integrada à `main` em 2026-10-15 (PR #22); aguarda revisão do solicitante.

## Fase 1: Fundação

- [x] T001 Domínio: `ActorName` no `AuditEvent`, ação `audit.exported` e detalhe `count`, categorias de ação; testes. Evidência: `AuditConsoleTests` (domínio: nome do ator, exportação, categorias cobrindo todas as ações).
- [x] T002 Migration `202610150001_AuditConsole` (coluna, sobrecarga da função, índice) com Down; writer grava o nome via `IAuditActorNameProvider`; testes em PostgreSQL real (gravação, hash, rollback). Evidência: Up/Down/Up executados no banco descartável (Down mantém a coluna; Up idempotente); `AuditConsoleIntegrationTests` (nome gravado, hash e nova tentativa idempotente); `PostgresAuditSchemaIntegrationTests` com a assinatura de 14 parâmetros.
- [x] T003 [P] OpenAPI (contracts/audit-api.md), cliente regenerado e `OpenApiContractTests`. Evidência: `OpenApiContractTests`; cliente regenerado.

## Fase 2: Jornadas 1 a 3 - consultar, filtrar e detalhar (P1)

- [x] T004 Leitor PostgreSQL (`IAuditEventReader`): período, filtros, cursor, relacionados; testes em PostgreSQL real. Evidência: `AuditConsoleIntegrationTests` (hosts, ação, categoria, resultado, ator, texto com curinga escapado, período, cursor, relacionados, contagem, mais antigo).
- [x] T005 Serviço de consulta com `audit:read` por host, resumo e detalhe com 404 sem acesso; testes. Evidência: `AuditConsoleIntegrationTests` (`audit:read` por host, 404 sem acesso, relacionados filtrados, período máximo, categoria inválida).
- [x] T006 Endpoints `summary`, `events`, `events/{id}`; provedor do nome a partir do token. Evidência: stack real: resumo, lista, detalhe e exportação pelo BFF; `AuditActorNameProviderTests` (nome da requisição e reserva fora dela).
- [x] T007 Frontend: tela Governança → Auditoria (filtros na URL, lista paginada, drawer com relacionados e links), menu pela API e "Ver auditoria" no detalhe do container; Vitest. Evidência: `audit.spec.ts`; E2E `test_017_audit.py`; menu pela API; "Ver auditoria" no detalhe do container. Comandos de container passaram a sempre registrar o container como alvo (`JustifiedCommandTests`).

## Fase 3: Jornada 4 - exportação (P2)

- [x] T008 Exportação CSV (até 10.000, neutralização de fórmulas) e evento `audit.exported` por host; testes. Evidência: `AuditConsoleIntegrationTests` (exportação auditada por host, recusa acima de 10.000).
- [x] T009 Frontend: botão Exportar com o filtro atual; Vitest. Evidência: E2E de exportação.

## Fase 4: Verificação

- [x] T010 E2E simulada (`test_017_*.py`) com axe. Evidência: `run-mock-e2e.sh`: 78 aprovados; axe sem violações sérias/críticas.
- [x] T011 Carga (SC-003, 1 milhão de eventos em 56 hosts) e exportação (SC-005) em PostgreSQL real. Evidência: SC-003: p95 de 14 ms (máximo 21 ms) na primeira página com 1.000.000 de eventos em 56 hosts, 7 combinações de filtros; SC-005: exportação na stack real em menos de 1 s (124 eventos); limite de 10.000 testado em PostgreSQL real.
- [x] T012 Stack real: quickstart completo; SC-001, SC-002 e SC-004. Evidência: SC-001: parar, iniciar, resultado do comando e terminal (hub) com ator, host, alvo e resultado; SC-004: do detalhe do container ao evento com a justificativa em 2 interações; exportação registrada em cada host. SC-002 na stack real **NOT RUN** (não há usuário de acesso parcial no laboratório): coberto em PostgreSQL real e na E2E.
- [x] T013 Documentação (`docs/seguranca.md`, `docs/frontend.md`, runbook de auditoria) e evidências nesta lista. Evidência: `docs/seguranca.md` (threat model), `docs/frontend.md`, `docs/runbooks/operacao-auditoria-postgresql.md`.

## Fase 5: Particionamento e busca textual (pedido de 2026-10-15)

- [x] T014 Confirmar o particionamento da trilha. Evidência: `RANGE (occurred_at_utc)` mensal 2026-01..2036-12 + `DEFAULT` vazia na stack e no banco de testes; *pruning* no plano da consulta (132 partições descartadas para 24 h); teste `Partitions_CoverTheNextTwelveMonthsAndAllHaveTheSearchIndex`.
- [x] T015 Busca full-text (D-008): migration `202610150002_AuditFullTextSearch` (Up/Down/Up no banco descartável), leitor, tela, contrato, mock e E2E. Evidência: `FullTextSearch_FindsJustificationActorActionAndFailureIgnoringAccents` (acentos, radicais, exclusão, `or`, ator, ação, falha, prefixo e plano com GIN); SC-003 com buscas textuais: p95 121 ms em 1 milhão de eventos.

## Achados da validação na stack real

- O encerramento do terminal é gravado fora da requisição do usuário e saía sem nome: a API passou a usar o último nome visto para o `sub` (cache em memória, limitado).
- Eventos de comando sem justificativa não tinham o container como alvo, e o filtro "Ver auditoria" não os encontraria: o alvo passou a ser sempre gravado.
- Um build silencioso (`podman build -q`) não gerou a imagem nova da API; a validação passou a conferir o id da imagem em uso.

## Atualização da spec 023 (2026-10-17)

- Usuário de acesso parcial criado temporariamente no laboratório (só leitura): o menu não mostra Auditoria e as demais telas respeitam a permissão. O conteúdo da consulta de auditoria desse usuário não foi conferido evento a evento — segue coberto em PostgreSQL real e na E2E simulada.
