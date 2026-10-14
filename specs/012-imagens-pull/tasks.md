# Tarefas: Inventário de Imagens e Download (Pull)

**Entrada**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/contracts-delta.md).

**Estado**: `implemented` - concluída em 2026-10-14, com revisão humana de segurança pendente antes de release. `NOT RUN`: registro privado de laboratório (`registry:2`, D-004), Docker real, pull pela stack real (Keycloak/BFF/agente); SC-002 medido apenas no E2E simulado.

**Pré-requisitos**: Revisão humana; T000 aprovada (threat model); 008 concluída (inclui `dokpod_apply_temporal_retention`).

## Fase 1: Contratos

- [x] T000 Threat model do pull em `docs/seguranca.md` (D-009) com lista de testes negativos, decisão sobre a lista de registros permitidos por estágio e retenção de comandos (D-008); registrar aprovação humana. — threat model, testes negativos, lista opcional de registros por estágio e retenção em `docs/seguranca.md` (aprovação de execução em `specs/README.md`; revisão de segurança pendente antes de release).
- [x] T001 Revisão humana; confirmar exclusão de verificação de assinatura, de `prune`, de progresso por camadas e de escolha de plataforma; definir papéis que recebem `image:pull`. — escopo confirmado conforme spec (sem assinatura, `prune`, progresso por camadas ou escolha de plataforma); `image:pull` na tabela de papéis de `docs/configuracao-keycloak.md`.
- [x] T002 [P] Proto: imagens (21/22, página ≤ 256 KiB), `AgentCommand.parameters` (7), `PULL_IMAGE` (7), capabilities 12/13, resultado com digest, `ContainerState.image_id` (15), `Heartbeat.image_revision` (3), `SnapshotRequest.scope` (2); registrar as tags novas em `specs/README.md`; testes N/N-1 nos dois sentidos e de hash `v2`. — proto com 21/22, `parameters` 7, `PULL_IMAGE`, capabilities 12/13, digest/tamanho, `image_id` 15, `image_revision` 3, `scope` 2; tags já registradas em `specs/README.md`; N/N-1 em `ProtocolCompatibilityTests` (Spec012_*) e hash `v2` em `ImageReferenceTests`.
- [x] T003 [P] OpenAPI: `listImages`, `lookupImage`, `pullImage` (erros do contrato), `kind`/`result` em status de comando, `images` no resumo (com `partial`), scope `image:pull`; provisionar scope no Keycloak e incluí-lo na tabela "Scopes iniciais" de `docs/configuracao-keycloak.md`; regenerar cliente. — `listImages`, `lookupImage`, `pullImage`, `kind`/`reference`/`result` no status e `images` no resumo (`OpenApiContractTests`); scope já provisionado nos scripts do Keycloak (010); cliente regenerado.

## Fase 2: Domínio e persistência

- [x] T004 [P] [US2] `ImageReference` (gramática OCI estrita, normalização, rejeição de credencial/query, tag mutável), alvo `HostTarget` e parâmetros de comando com hash `v2` (sem prazo; `v1` mantido para tipos 1-6) em `backend/libs/Dokpod.Domain/`; testes. — `ImageReference` (`Dokpod.Domain/Images`), `PullImageParameters`, `AgentCommandKinds.PayloadHash` (`v1` inalterado); `ImageReferenceTests`.
- [x] T005 [US1] Migrations: `inventory_images`; `inventory_containers.image_id`; `agent_commands` com `container_id`/`expected_container_revision` anuláveis + `CHECK` por tipo e `ck_agent_commands_kind` para `BETWEEN 1 AND 7`; `agent_command_parameters` com PK/FK `(environment_id, command_id, created_at_utc)` e partições mensais; retenção de `agent_commands` e `agent_command_parameters` via `dokpod_apply_temporal_retention`; stores; testes PostgreSQL real, incluindo rollback e que comandos não terminais não são removidos. — migration `202610130006_ImagesAndPull` (Up/Down verificados em PostgreSQL real), `PostgresImageInventoryStore`, parâmetros e saída no `PostgresAgentCommandStore`, retenção `PurgeTerminalAsync` (concluídos/falhos; pendentes e indeterminados preservados); `ImagesIntegrationTests`.

## Fase 3: Agente

- [x] T006 [US1] Snapshot/delta de imagens (Docker `/images/json`, Podman compatível) no ciclo de inventário, páginas ≤ 256 KiB/200 imagens, `image_revision` no heartbeat, snapshot por escopo; `image_id` em `ContainerState`; testes reais. — `ImageInventoryBuilder` (≤ 200 imagens/256 KiB, ≤ 64 nomes, revisão só muda com o conjunto), snapshot no início da sessão, em evento `image` e após pull, `image_revision` no heartbeat, `SnapshotScope.Images`, `image_id` em `ContainerState`; `ImageInventoryBuilderTests` e Podman real (`Images_ListAndContainerImageIdMatch`).
- [x] T007 [US2] Executor de pull (`/images/create?fromImage=` com parâmetros codificados e referência revalidada no agente) com prazo, códigos de falha, journal-before-ack e reconciliação (digest ou tag criada após o aceite, D-003); credenciais apenas do host; testes com registro de laboratório público/privado. — `DockerEngineClient.PullImageAsync` (parâmetros codificados, códigos de falha, digest/tamanho), revalidação no `AgentCommandMapper`, prazo do comando, trava por imagem, pull refeito após perda de resultado (idempotente; o digest resolvido vem do engine), credenciais só do host (`RegistryCredentialFile`, `DOKPOD_AGENT_REGISTRY_AUTH_FILE`); `ImagePullTests` e pull real de `quay.io/podman/hello` + repositório inexistente em Podman. Registro privado de laboratório: **NOT RUN**.

## Fase 4: API

- [x] T008 [US1] `ImageProjectionService` (aplicação de snapshot/delta, detecção de delta perdido por `image_revision`) e `ImageQueryService` (`listImages`, "em uso" por `image_id`, resumo com `partial`, `lookupImage` com referência normalizada); `images` em `getOverview`; testes de autorização. — `ImageSnapshotAccumulator` + `ReplaceSnapshotAsync` (snapshot obsoleto recusado), pedido de snapshot por `image_revision` divergente ou delta recebido, consulta com "em uso" por `image_id`, resumo `partial`, `lookupImage` normalizado, `images` no `getOverview`; autorização por ambiente (lista e lookup).
- [x] T009 [US2] `ImagePullService` + `pullImage` reutilizando `AgentCommandQueueService`; lista de registros permitidos por estágio; status com `kind`/`result`; eventos `image.pulled` (engine) e `command.succeeded` (pipeline) via 008; testes. — `ImagePullService` + `POST /images/pull` (fila de comandos existente, `Images:AllowedRegistriesByStage`, capability 13, auditoria `image.pull`), status com `kind`/`reference`/`result`; `ImagePullServiceTests`. Evento `image.pulled` do engine segue o pipeline de eventos da 008.

## Fase 5: Frontend

- [x] T010 [US1] `features/images/images.page.*` (grid com filtros, sem uso, tamanho, data, host, engine; resumo) no item de menu "Imagens". — `features/images/images.page.*` e `images.facade.ts` (grid com uso, host, engine, tamanho, data, ID; filtros e resumo com contagem parcial) no item de menu "Imagens" (`/imagens`).
- [x] T011 [US2] `pull-image.drawer.*` (referência validada, aviso de tag mutável, seleção de hosts do estágio, acompanhamento por host com os estados do comando) acionado da Visão geral e da tela Imagens; KPI "Imagens sem uso" ligado (indicação de contagem parcial). — `pull-image.drawer.*` + `image-pull.service.ts` (validação, aviso de tag mutável, hosts elegíveis com motivo, uma chave por host, acompanhamento até estado terminal, falhas legíveis); acionado da Visão geral e da tela Imagens; KPI "Imagens sem uso" ligado com indicação de contagem parcial.
- [x] T012 [US3] Serviço `image-lookup` em `data-access` para a spec 013. — `data-access/image-lookup.service.ts` (cache curto por host e referência canônica).
- [x] T013 Playwright em `frontend/tests/e2e/` (imagens, pull em 2 hosts, falhas legíveis, medição de SC-002), acessibilidade e loop visual desktop/mobile. — `frontend/tests/e2e/mock_ui/test_012_images.py` (5 testes com axe; pull em 2 hosts com falha legível; SC-002 no mock: 3,9 s; capturas 1440/390 em `artifacts/e2e/mock`). Não há board de Imagens em `docs/design/app`; a tela segue o padrão do board de Containers. Stack real: **NOT RUN**.

## Fase 6: Encerramento

- [x] T014 Varredura de credenciais (SC-003); documentar pull e credenciais no host em `docs/distribuicao.md` e `docs/seguranca.md`. — gitleaks sem achados nos arquivos da 012 (achados restantes são canários e mockups pré-existentes); pull e credenciais no host em `docs/distribuicao.md` e `docs/seguranca.md`.
- [x] T015 Suíte de testes negativos de T000 na API e no agente (SC-005): referência com credencial, query, espaço ou fora da gramática; registro não permitido; scope ausente; host não autorizado; agente sem capability 13; conflito de idempotência com parâmetros diferentes. — suíte negativa: `ImageReferenceTests`, `ImagePullTests` (agente), `ImagePullServiceTests` (credencial, query, espaço, maiúscula, registro não permitido, scope ausente, sem capability 13, agente offline, conflito de idempotência).

## Dependências e Ordem de Execução

T000 (gate) -> T001 -> T002/T003 -> T004/T005 -> T006/T007 -> T008/T009 -> T010-T013 -> T014/T015. A spec 013 depende de T002, T004, T005 e T012.
