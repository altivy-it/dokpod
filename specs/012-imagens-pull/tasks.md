# Tarefas: Inventário de Imagens e Download (Pull)

**Entrada**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/contracts-delta.md).

**Estado**: `draft` - aguardando revisão humana.

**Pré-requisitos**: Revisão humana; T000 aprovada (threat model); 008 concluída (inclui `dokpod_apply_temporal_retention`).

## Fase 1: Contratos

- [ ] T000 Threat model do pull em `docs/seguranca.md` (D-009) com lista de testes negativos, decisão sobre a lista de registros permitidos por estágio e retenção de comandos (D-008); registrar aprovação humana.
- [ ] T001 Revisão humana; confirmar exclusão de verificação de assinatura, de `prune`, de progresso por camadas e de escolha de plataforma; definir papéis que recebem `image:pull`.
- [ ] T002 [P] Proto: imagens (21/22, página ≤ 256 KiB), `AgentCommand.parameters` (7), `PULL_IMAGE` (7), capabilities 12/13, resultado com digest, `ContainerState.image_id` (15), `Heartbeat.image_revision` (3), `SnapshotRequest.scope` (2); registrar as tags novas em `specs/README.md`; testes N/N-1 nos dois sentidos e de hash `v2`.
- [ ] T003 [P] OpenAPI: `listImages`, `lookupImage`, `pullImage` (erros do contrato), `kind`/`result` em status de comando, `images` no resumo (com `partial`), scope `image:pull`; provisionar scope no Keycloak e incluí-lo na tabela "Scopes iniciais" de `docs/configuracao-keycloak.md`; regenerar cliente.

## Fase 2: Domínio e persistência

- [ ] T004 [P] [US2] `ImageReference` (gramática OCI estrita, normalização, rejeição de credencial/query, tag mutável), alvo `HostTarget` e parâmetros de comando com hash `v2` (sem prazo; `v1` mantido para tipos 1-6) em `backend/libs/Dokpod.Domain/`; testes.
- [ ] T005 [US1] Migrations: `inventory_images`; `inventory_containers.image_id`; `agent_commands` com `container_id`/`expected_container_revision` anuláveis + `CHECK` por tipo e `ck_agent_commands_kind` para `BETWEEN 1 AND 7`; `agent_command_parameters` com PK/FK `(environment_id, command_id, created_at_utc)` e partições mensais; retenção de `agent_commands` e `agent_command_parameters` via `dokpod_apply_temporal_retention`; stores; testes PostgreSQL real, incluindo rollback e que comandos não terminais não são removidos.

## Fase 3: Agente

- [ ] T006 [US1] Snapshot/delta de imagens (Docker `/images/json`, Podman compatível) no ciclo de inventário, páginas ≤ 256 KiB/200 imagens, `image_revision` no heartbeat, snapshot por escopo; `image_id` em `ContainerState`; testes reais.
- [ ] T007 [US2] Executor de pull (`/images/create?fromImage=` com parâmetros codificados e referência revalidada no agente) com prazo, códigos de falha, journal-before-ack e reconciliação (digest ou tag criada após o aceite, D-003); credenciais apenas do host; testes com registro de laboratório público/privado.

## Fase 4: API

- [ ] T008 [US1] `ImageProjectionService` (aplicação de snapshot/delta, detecção de delta perdido por `image_revision`) e `ImageQueryService` (`listImages`, "em uso" por `image_id`, resumo com `partial`, `lookupImage` com referência normalizada); `images` em `getOverview`; testes de autorização.
- [ ] T009 [US2] `ImagePullService` + `pullImage` reutilizando `AgentCommandQueueService`; lista de registros permitidos por estágio; status com `kind`/`result`; eventos `image.pulled` (engine) e `command.succeeded` (pipeline) via 008; testes.

## Fase 5: Frontend

- [ ] T010 [US1] `features/images/images.page.*` (grid com filtros, sem uso, tamanho, data, host, engine; resumo) no item de menu "Imagens".
- [ ] T011 [US2] `pull-image.drawer.*` (referência validada, aviso de tag mutável, seleção de hosts do estágio, acompanhamento por host com os estados do comando) acionado da Visão geral e da tela Imagens; KPI "Imagens sem uso" ligado (indicação de contagem parcial).
- [ ] T012 [US3] Serviço `image-lookup` em `data-access` para a spec 013.
- [ ] T013 Playwright em `frontend/tests/e2e/` (imagens, pull em 2 hosts, falhas legíveis, medição de SC-002), acessibilidade e loop visual desktop/mobile.

## Fase 6: Encerramento

- [ ] T014 Varredura de credenciais (SC-003); documentar pull e credenciais no host em `docs/distribuicao.md` e `docs/seguranca.md`.
- [ ] T015 Suíte de testes negativos de T000 na API e no agente (SC-005): referência com credencial, query, espaço ou fora da gramática; registro não permitido; scope ausente; host não autorizado; agente sem capability 13; conflito de idempotência com parâmetros diferentes.

## Dependências e Ordem de Execução

T000 (gate) -> T001 -> T002/T003 -> T004/T005 -> T006/T007 -> T008/T009 -> T010-T013 -> T014/T015. A spec 013 depende de T002, T004, T005 e T012.
