# Tarefas: Lista Unificada de Containers, Pausa e Ações em Lote

**Entrada**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/contracts-delta.md).

**Estado**: `draft` - aguardando revisão humana.

**Pré-requisitos**: Revisão humana; 008 T007/T012/T018 concluídas; 007 shell e `dok-data-grid` prontos.

## Fase 1: Preparação

- [ ] T001 Revisão humana; confirmar limite de lote (50) e confirmação reforçada (digitar a quantidade em Produção).
- [ ] T018 [P] [US3] Provisionar o scope `container:pause` e a política no resource server `dokpod-api` do Keycloak (realm de desenvolvimento/E2E em `deploy/keycloak/realm/` quando aplicável) e acrescentá-lo à tabela "Scopes iniciais" de `docs/configuracao-keycloak.md`.
- [ ] T002 [P] OpenAPI: `listContainers`, ações `pause`/`unpause`, scope `container:pause`, códigos de falha; regenerar cliente; `OpenApiContractTests`.
- [ ] T003 [P] Proto: `CommandKind` 5/6 e `Capability` 8/9; testes N/N-1 nos dois sentidos (agente N-1 rejeita explicitamente; servidor não despacha pausa sem capability anunciada; agente N contra servidor N-1).

## Fase 2: Backend (US1, US3)

- [ ] T004 [US3] Domínio: `Pause`/`Unpause` em `backend/libs/Dokpod.Domain/Commands/AgentCommand.cs` com gates e testes em `AgentCommandGateTests`.
- [ ] T005 [US3] Migration expand ampliando `ck_agent_commands_kind` de `kind BETWEEN 1 AND 4` para `1 AND 6` em `agent_commands`; `ContainerCommandService` aceita novas ações com `container:pause` e verifica capability da sessão; testes.
- [ ] T006 [US3] Agente: `AgentCommandMapper` e adapters Docker/Podman (`POST /containers/{id}/pause|unpause`) com journal-before-ack; capabilities condicionadas (Podman rootless cgroup v1 sem pausa); testes com engines reais.
- [ ] T007 [US1] Migration expand com `display_status` (backfill pelo mapeamento da 008), `ports_text` e índices; projeção passa a gravar `display_status`. `ContainerSearchService` + consulta multi-host em `PostgresInventoryProjectionStore` (autorização prévia por `IEnvironmentAuthorizationDecider`, filtros, busca, ordenação no servidor por nome/situação/host/criação, cursor, resumo com as 6 situações, `allowedActions`, métricas do cache da 009 anexadas); testes PostgreSQL real e de autorização negativa.
- [ ] T008 [US1] `GET /api/v1/containers` em `InventoryEndpoints.cs`; teste de desempenho SC-001 com 1.120 containers em 56 e 100 hosts.

## Fase 3: Frontend (US1, US2, US4)

- [ ] T009 [US1] `containers.facade.ts`: filtros na URL (estágio, host, engine, situação, busca, ordenação), cursor, dedupe, invalidação SignalR com atualização no lugar e aviso de novas linhas; testes Vitest.
- [ ] T010 [US1] `containers.page.*` conforme `Containers.dc.html` com `dok-data-grid`, chips de filtro (Engine, Estágio), resumo com as 6 situações, colunas (CPU/Memória quando 009 disponível, ordenáveis só na página carregada com indicação), "atualizado em", estados carregando/vazio/erro/forbidden/desatualizado; substituir `container-list.page.*` e redirecionar a rota legada.
- [ ] T011 [US2] `batch-actions.facade.ts`: elegibilidade (situação × `allowedActions` × conexão), limite, concorrência 5, chaves de idempotência e IDs de comando em `sessionStorage` (escopo da aba), acompanhamento consultando o servidor até estado terminal, resumo anunciado; testes de recarga e retry.
- [ ] T012 [US2] `confirm-batch.dialog.*` com lista de afetados/ignorados (com host para nomes iguais), aviso de volumes e, para remoção múltipla em Produção, campo que exige digitar a quantidade exata.
- [ ] T013 [US3] Ações Pausar/Retomar na linha e em lote, com estado "Pausado" e motivo quando desabilitadas.
- [ ] T014 [US4] Ações por linha (logs, terminal, inspecionar → rotas do detalhe 011/014; reiniciar, parar, remover) e exportação CSV com proteção contra injeção de fórmula.
- [ ] T015 Playwright em `frontend/tests/e2e/` (pytest): busca/filtros/paginação, lote com sucesso/falha/ignorados, recarga durante lote, remoção reforçada, pausa (com e sem `container:pause`), CSV, usuário sem acesso; medir SC-002 (20 containers em ≤ 30 s) e SC-005 (≤ 4 interações); acessibilidade com a ferramenta aprovada na 007.
- [ ] T016 Loop de verificação visual (1920, 1440, 1100, 390 e 320px) do grid com seleção, confirmação e estados; capturas lado a lado com `Containers.dc.html` e revisão humana registrada.

## Fase 4: Encerramento

- [ ] T017 Atualizar `docs/frontend.md` (lista unificada e lote) e runbook de comandos com `pause`/`unpause`; remover `features/containers/container-list.*` e `container-lifecycle.facade.*` legados após migração dos testes.

## Dependências e Ordem de Execução

T001 -> T002/T003/T018 -> T004-T006 (pausa) ∥ T007-T008 (lista) -> T009-T014 -> T015-T016 -> T017. A T018 recebeu esse número para não renumerar as tarefas citadas por outras specs (ex.: 011 usa T011).
