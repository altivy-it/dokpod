# Tarefas: Criação de Container

**Entrada**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/contracts-delta.md).

**Estado**: `approved-for-implementation` - execução autorizada em 2026-10-13 (specs/README.md); T002 (threat model) continua bloqueante.

**Pré-requisitos**: Registro do encerramento da decisão adiada (T001); threat model aprovado (T002); 012 T002/T004/T005/T012 e 011 T008 concluídas.

## Fase 1: Decisões e contratos (bloqueante)

- [ ] T001 Registrar em `docs/arquitetura.md` "Decisões adiadas" o encerramento de "criação declarativa" somente para container único (Compose/stacks continuam adiados), citando a aprovação de 2026-10-13 em `specs/README.md`; confirmar D-001 (opção A), D-006 e os limites fixos (32 portas, 100 variáveis, 10 volumes); atualizar o Estado de spec/plan/tasks se algo mudar.
- [ ] T002 Threat model de criação de container em `docs/seguranca.md` (escalada via opções/mounts, imagem maliciosa, vazamento de valores, perda do key ring da API, volumes anônimos apagados por remoção automática, DoS por recursos) com testes negativos derivados; registrar aprovação.
- [ ] T003 [P] Proto conforme [contracts-delta.md](contracts/contracts-delta.md) (incluindo `HealthCheck.on_failure` e `CommandResult.created_container_id = 8`); conferir as tags na tabela de `specs/README.md`; testes N/N-1.
- [ ] T004 [P] OpenAPI: `getContainerCreateOptions`, `createContainer`, `result.createdContainerId`, scope `container:create`; provisionar scope; regenerar cliente.

## Fase 2: Domínio e proteção (US1, US2, US4)

- [ ] T005 [P] [US4] `ContainerCreationSpec` com allowlist, limites e regras cruzadas (autoRemove × restart, `onFailure: restart` só Podman, prazo padrão) em `backend/libs/Dokpod.Domain/Containers/`; testes de tabela incluindo todas as opções proibidas.
- [ ] T006 [P] [US1] `EquivalentCommandRenderer` (docker/podman, redaction) com testes de paridade usados também pelo frontend (fixtures compartilhadas em `contracts/` ou `frontend/web/src/testing/`).
- [ ] T007 [US2] `SensitiveParameterProtector` (Data Protection com key ring próprio da API conforme D-006, propósito versionado, perda de chave → `parameters_unavailable`), migrations expand `protected_values`/`purged_at` e `ck_agent_commands_kind` `BETWEEN 1 AND 8` (com teste do `CHECK` de alvo para o kind 8 e rollback), job de purga pós-terminal; testes PostgreSQL real e canários.

## Fase 3: Agente (US1-US4)

- [ ] T008 [US1] `ContainerCreator`: pull-se-necessário (012) → create → start, label `dokpod.command-id`, rollback em `port_conflict`, resultado com id; journal sem valores sensíveis; reconciliação por label; reentrega após reinício do agente antes do `create` e `parameters_unavailable` quando purgado/expirado (D-002); recusa `anonymous_volume_with_auto_remove` após inspect da imagem; testes Docker real.
- [ ] T009 [US2] `HostSecretResolver` (arquivo protegido; segredo Podman conforme D-003) e consultas `HOST_SECRETS`/`HOST_NETWORKS`/`HOST_VOLUMES`; testes reais.
- [ ] T010 [US4] Validação defensiva no agente (mesma allowlist, bind allowlist de configuração, socket do engine sempre proibido); testes negativos.
- [ ] T011 [US1] Adapter Podman (compatível e Libpod mínimo para segredos e `health_on_failure: restart`), rootless (porta < 1024), Docker recusando `onFailure: restart` com `unsupported_option`; capability `CAPABILITY_CONTAINER_CREATE` só após testes reais em Linux e nunca anunciada pelo agente Windows nesta entrega.

## Fase 4: API (US1, US2)

- [ ] T012 [US1] `ContainerCreationService` + `createContainer` (validação, registros permitidos via `Images:AllowedRegistriesByStage` da 012, prazo padrão, idempotência, cifragem, auditoria com comando equivalente); testes, incluindo 400 por campo extra.
- [ ] T013 [US1] `getContainerCreateOptions` via `AgentQueryDispatcher` (011) + hosts elegíveis com CPU (009), somente com `CAPABILITY_CONTAINER_CREATE`, e presença de imagem (012); `networks[]` só para clientes da API.

## Fase 5: Frontend (US1-US3)

- [ ] T014 [US1] `new-container.drawer.*` conforme `NovoContainer.dc.html` (Reactive Forms; Imagem com ajuda da 012 sem "Assinatura verificada"; Nome; Engine/Host com legenda de CPU e chips; Portas repetíveis; Variáveis repetíveis marcadas como segredo (`sensitive`); "Usar segredo do cofre" com seleção por nome; Volume em campo único `origem:destino[:ro|rw]`; CPU/Memória/Reinício; marcador "Ativar health check em GET /healthz" com preset e ajuda por engine, "Opções avançadas" recolhidas; marcador de remoção automática com aviso de perda de logs/estado; sem campo de rede; "Criar e iniciar" desabilitado com motivo quando o host está desconectado; caixa com comando equivalente) aberto pela lista (010) e pela Visão geral (008).
- [ ] T015 [US1] `new-container.facade.ts`: validação assíncrona (nome/porta contra inventário), envio idempotente, acompanhamento até terminal, link para detalhe; testes Vitest (inclusive valores sensíveis nunca em estado serializável/URL/log de console).
- [ ] T016 Playwright: criação completa, conflitos, segredo, rootless, retry sem duplicação, host desconectado, remoção automática com `VOLUME` anônimo; medição do SC-001 (≤ 1 min com imagem presente) com evidência registrada; acessibilidade do drawer (foco, Esc, erros anunciados).
- [ ] T017 Loop visual desktop/mobile do drawer (vazio, preenchido, erros, enviando, concluído, falha); comparar com o board 04.

## Fase 6: Segurança

- [ ] T018 Testes negativos do threat model (SC-002) e varredura de canários (SC-003); evidência registrada.

## Dependências e Ordem de Execução

T001/T002 (gates) -> T003/T004 -> T005-T007 -> T008-T011 -> T012/T013 -> T014-T017 -> T018.
