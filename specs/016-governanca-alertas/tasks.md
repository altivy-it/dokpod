# Tarefas: Governança de alertas e parada justificada

**Entrada**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/alerts-api.md), [quickstart.md](quickstart.md).

**Estado**: `in-progress` - implementação iniciada em 2026-10-14.

**Pré-requisitos**: spec aprovada (2026-10-14); 015, correção de métricas (#18) e correção do sino (#20) incorporadas à branch.

## Fase 1: Fundação

- [ ] T001 Domínio `Dokpod.Domain/Alerts/`: regra (validação de condição, limite, duração, severidade e abrangência; casamento de padrão de container), alerta (transições de estado), silenciamento (janela ≤ 7 dias) e justificativa (10 a 500 caracteres, mascaramento reaproveitando a regra do terminal); testes unitários.
- [ ] T002 Migration `202610140001_AlertGovernance` (tabelas, índice único parcial, regras padrão e política de Produção semeadas) com Down; stores PostgreSQL; testes em PostgreSQL real, inclusive rollback e o índice único.
- [ ] T003 [P] Scopes `alert:read`/`alert:manage`: `EnvironmentResourceScopes`, `configure-keycloak.ps1`, `docs/configuracao-keycloak.md`; ações novas de auditoria em `AuditEvent`.
- [ ] T004 [P] OpenAPI (contracts/alerts-api.md) e cliente regenerado; `OpenApiContractTests`.

## Fase 2: Jornada 3 - parar, pausar e remover com justificativa (P1)

- [ ] T005 [US3] Política por estágio (`GET`/`PUT /api/v1/action-justification-policies`, D-007) e auditoria da mudança; testes.
- [ ] T006 [US3] `ContainerCommandService`: `justification` opcional; `422 justification_required` com a política ativa para stop/pause/delete; gravação idempotente após o enfileiramento (D-005); testes (exigida, opcional, reinício sem justificativa, nova tentativa idempotente, mascaramento).
- [ ] T007 [US3] API de justificativas (container e host) e campo `intentional` na lista e no detalhe; encerramento da marcação quando o container volta a executar (varredura); testes em PostgreSQL real.
- [ ] T008 [US3] Frontend: diálogos de parar, pausar e remover (lista, detalhe e lote) com campo de justificativa obrigatório ou opcional conforme a política; rótulos "Parado intencionalmente"/"Pausado intencionalmente" com autor, horário e motivo; Vitest.

## Fase 3: Jornadas 1, 2 e 4 - alertas, ciclo de vida e regras (P1)

- [ ] T009 [US1] `AlertEvaluator` chamado por `OperationalEventService.AppendAsync`: regra por evento, deduplicação por regra e alvo, encerramento justificado ignorado, regra de encerramento sem códigos 0/137/143, silenciamento ativo; testes.
- [ ] T010 [US2] `AlertSweeper` (30 s): resolução automática (container em execução ou removido, agente reconectado, limite normalizado), limites de CPU e memória com duração, fim de silenciamento, fim da marcação intencional; worker com retenção de 90 dias; testes com relógio controlado.
- [ ] T011 [US1/US2] Endpoints de alertas (resumo, lista aberta e histórico, detalhe com linha do tempo, reconhecer, resolver, silenciar) com filtro por `alert:read` e ações com `alert:manage`; `InvalidationKinds.Alerts`; testes.
- [ ] T012 [US4] Endpoints de regras (listar, criar, editar com versão, ativar/desativar, excluir resolvendo os alertas) e auditoria antes/depois; testes.
- [ ] T013 [US1] Frontend: tela Governança → Alertas (Abertos e Histórico com filtros na URL, detalhe com linha do tempo, reconhecer, resolver e silenciar), contador do menu e sino com alertas abertos; Vitest.
- [ ] T014 [US4] Frontend: vista Regras (lista, drawer de criar/editar, ativar/desativar) e política de justificativa por estágio; Vitest.

## Fase 4: Jornadas 5 e 6 - silenciamentos, histórico e auditoria (P2)

- [ ] T015 [US5] Endpoints e vista Silenciamentos (criar, listar ativos e encerrados, encerrar); testes.
- [ ] T016 [US6] Histórico com filtros por período, host, severidade e regra; auditoria de todas as ações de governança; testes.

## Fase 5: Verificação

- [ ] T017 E2E simulada (`test_016_*.py`): tela Alertas, sino e menu, reconhecer, regras, silenciamento, diálogos com justificativa nas três ações, rótulos intencionais; axe sem violações sérias/críticas.
- [ ] T018 Stack real (Linux, Podman): quickstart completo; SC-001, SC-002, SC-003, SC-004 e SC-005 medidos; SC-007 em PostgreSQL real.
- [ ] T019 Documentação (`docs/seguranca.md`, `docs/frontend.md`, runbook de retenção) e evidências nesta lista.
