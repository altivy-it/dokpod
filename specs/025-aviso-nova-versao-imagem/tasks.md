---

description: "Tarefas da implementação do aviso de nova versão de imagem"
---

# Tarefas: Aviso de nova versão de imagem

**Entrada**: Documentos de design em `/specs/025-aviso-nova-versao-imagem/`

> Redija todas as tarefas e descrições em português brasileiro (pt-BR), preservando identificadores, comandos e caminhos técnicos.

**Pré-requisitos**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/agent-protocol.md](contracts/agent-protocol.md), [contracts/api.md](contracts/api.md), [quickstart.md](quickstart.md)

**Testes**: obrigatórios (constituição, princípio VI):
- registro OCI real em container;
- agentes Docker e Podman reais;
- PostgreSQL real;
- E2E pela borda nginx;
- canário de credencial.

**Organização**: agrupadas por história da spec para implementação e validação independentes.

**Gate**: nenhuma tarefa de código começa antes de T001 (threat model) aceito pelo revisor humano. `/speckit-implement` exige autorização humana explícita.

## Formato: `[ID] [P?] [Story] Descricao`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência pendente)
- **[Story]**: história da spec (US1, US2, US3)

## Convencoes de Caminhos

- Agente: `backend/libs/Dokpod.Agent.Application/Images/`, `backend/libs/Dokpod.Agent.Infrastructure/Registries/`, `backend/apps/Dokpod.Agent/AgentControlWorker.cs`
- Plano de controle:
  - aplicação: `backend/libs/Dokpod.ControlPlane.Application/ImageUpdates/`;
  - infraestrutura: `backend/libs/Dokpod.ControlPlane.Infrastructure/ImageUpdates/`;
  - API, só na composição Business: `backend/apps/Dokpod.ControlPlane.Api/ImageUpdates/`.
- Testes: `backend/tests/Dokpod.Agent.Application.Tests/Images/`, `backend/tests/Dokpod.Agent.Infrastructure.Tests/Registries/`, `backend/tests/Dokpod.ControlPlane.Application.Tests/ImageUpdates/`, `backend/tests/Dokpod.ControlPlane.Api.Tests/ImageUpdates/`, `backend/tests/Dokpod.Architecture.Tests/`
- Frontend: `frontend/web/src/app/features/{images,container-list,container-detail,alerts}/`
- E2E simulado: `frontend/tests/e2e/mock_ui/` e `frontend/tests/e2e/mock/control_plane_mock.py`

---

## Fase 1: Preparação (Infraestrutura Compartilhada)

**Objetivo**: fronteira de segurança aceita e registro de teste antes de qualquer código de produto.

- [x] T001 Threat model em `specs/025-aviso-nova-versao-imagem/threat-model.md`, no formato do de `specs/027-exportacao-auditoria-siem/threat-model.md`. Cobrir:
  - saída nova do agente para registros ([D-001](research.md#d-001-onde-e-como-consultar-o-registro)), proxy e TLS;
  - uso da credencial do host fora do engine e o desafio Bearer/Basic, sem `Authorization` em redirecionamento para outro host ([D-005](research.md#d-005-limites-no-agente));
  - referências vindas do inventário do próprio host, sem host informado pelo usuário;
  - registros fora da política e inseguros ([D-003](research.md#d-003-referências-não-verificáveis));
  - mensagens 17 e 27 do protocolo e respostas forjadas por agente comprometido;
  - limite de "Verificar agora" e cota do registro;
  - telemetria sem referência nem registro ([D-012](research.md#d-012-observabilidade-e-segredos));
  - comportamento sob licença restrita (FR-009).

  Registrar lacunas, riscos residuais e a decisão do revisor humano. Bloqueia todas as tarefas de código. Nota (2026-11-01): aceito pelo solicitante ("executar suas recomendações"), com as lacunas L1–L4 aplicadas na T009 e na T011, L5 e os riscos R1–R5 aceitos, e o agente Windows sem a capability por enquanto. A saída direta do agente para registros está no [ADR 2026-0009](../../docs/adr/2026-0009-agente-consulta-registros-oci.md), aceito em 2026-11-01.
- [x] T002 [P] Registro OCI de teste para os testes do agente:
  - receptor HTTPS em processo, em `backend/tests/Dokpod.Agent.Infrastructure.Tests/Registries/OciTestRegistry.cs`, com desafio `Bearer` e `Basic`, índice multiarquitetura, respostas `401/403/404/429/5xx`, redirecionamento e corpo grande configuráveis, e certificado de uma CA gerada na execução;
  - fixture `OciRegistryFixture.cs` para um `registry:2` real em container, com versão fixada, licença Apache-2.0 conferida e `htpasswd` gerado na execução. A fixture usa a URL de `DOKPOD_TEST_OCI_REGISTRY` e fica ignorada sem ela.
  - Nota de execução: `docker.io/library/registry:3.1.2` (a linha 3 do CNCF Distribution, Apache-2.0) no lugar de `registry:2`. O receptor em processo exigiu `FrameworkReference` `Microsoft.AspNetCore.App` no projeto de testes (parte do SDK, sem pacote). A fixture real foi validada em 2026-11-01 com o registro em container, TLS de uma CA de teste e `htpasswd` gerados na execução (`OciRegistryFixtureTests`); no CI fica ignorada sem `DOKPOD_TEST_OCI_REGISTRY`.

---

## Fase 2: Fundação (Pré-requisitos Bloqueadores)

**Objetivo**: protocolo, domínio, tabelas e composição Business.

**CRÍTICO**: nenhuma história começa antes desta fase.

- [x] T003 Protocolo em `contracts/agent/v1/agent.proto` exatamente como em [contracts/agent-protocol.md](contracts/agent-protocol.md):
  - `CAPABILITY_IMAGE_UPDATE_CHECK = 23`;
  - `ControlPlaneMessage.image_update_check = 17` e `AgentMessage.image_update_check_result = 27`;
  - mensagens `ImageUpdateCheckRequest`, `ImageUpdateCheckItem`, `ImageUpdateCheckResultPage` e `ImageUpdateCheckResult`, e o enum `ImageUpdateState`.
  - Nota de execução: teste de tags e ida e volta em `backend/tests/Dokpod.Agent.Application.Tests/Images/ImageUpdateProtocolTests.cs`.

  Atualizar o registro de números em `contracts/agent/README.md` e os testes de contrato existentes (compatibilidade N/N-1: agente sem a capability não recebe a mensagem 17).
- [x] T004 [P] Domínio em `backend/libs/Dokpod.Domain/Images/ImageUpdateCheck.cs`, com testes em `backend/tests/Dokpod.Domain.Tests/Images/ImageUpdateCheckTests.cs`:
  - estados `up_to_date`, `update_available`, `not_checkable`, `failed`, e o derivado `container_behind`;
  - motivos de [data-model.md](data-model.md#image_update_checks):
    - `not_checkable`: `digest_reference`, `local_image`, `registry_not_allowed`, `insecure_registry`, `agent_unsupported`;
    - `failed`: `registry_unavailable`, `rate_limited`, `unauthorized`, `reference_not_found`, `timeout`, `invalid_response`;
  - plataforma `os/arquitetura[/variante]` com até 64 caracteres;
  - referência normalizada pelo `ImageReference` existente, com até 512 caracteres;
  - a regra do estado derivado do container ([D-008](research.md#d-008-container-com-a-versão-anterior)).
  - Nota de execução: o tamanho da referência segue `ImageReference.MaxLength` (255), dentro da coluna de 512.
- [x] T005 Migration `backend/libs/Dokpod.ControlPlane.Infrastructure/Migrations/Script/<AAAAMMDDNNNN>_ImageUpdateCheck/Up/01-ImageUpdateCheck.sql` e `Down/01-ImageUpdateCheck.sql`, com a migration EF correspondente:
  - `image_update_schedules`: `environment_id uuid` PK com FK em `environments` e `ON DELETE CASCADE`; `next_due_at_utc timestamptz NOT NULL`; `last_requested_at_utc`, `last_manual_at_utc`, `pending_request_id uuid` e `lease_owner varchar(128)` nulos; `lease_until_utc` obrigatório quando houver `lease_owner` (CHECK); índice em `next_due_at_utc`;
  - `image_update_checks`: PK `(environment_id, reference varchar(512), platform varchar(64))` com FK em `environments` e `ON DELETE CASCADE`; `local_image_id varchar(80)` nulo; `state varchar(24) NOT NULL` com CHECK dos quatro estados; `reason varchar(32)` com CHECK dos motivos de T004 e nulo em `up_to_date`/`update_available`; `remote_digest varchar(80)`; `checked_at_utc timestamptz NOT NULL`; `last_success_at_utc` e `outdated_since_utc timestamptz` nulos; índice `(environment_id, state)`;
  - `alert_rules.outdated_days int NULL` com `CHECK (outdated_days BETWEEN 1 AND 365)` (expand-contract).
  - Nota de execução: os testes acharam que `reason IN (...)` e `outdated_days BETWEEN` com `NULL` passavam no CHECK (lógica de três valores); a migration exige `IS NOT NULL` explicitamente. O `Down` remove as regras `image_outdated` e os alertas delas, porque o código anterior lê qualquer condição diferente de `event` como métrica.

  Testes em `backend/tests/Dokpod.ControlPlane.Api.Tests/ImageUpdates/ImageUpdateMigrationTests.cs` com PostgreSQL real: `Up`, `Down` e `Up` de novo, constraints e cascata ao excluir o ambiente.
- [x] T006 `ImageUpdateStore` em `backend/libs/Dokpod.ControlPlane.Infrastructure/ImageUpdates/ImageUpdateStore.cs`, com portas em `backend/libs/Dokpod.ControlPlane.Application/ImageUpdates/ImageUpdatePorts.cs`:
  - hosts vencidos e lease de 2 min, com `UPDATE ... WHERE lease_until_utc < now()`, como o `AuditExportStore`;
  - `next_due_at` entre 5 h 24 min e 6 h depois do pedido (variação só para menos, para não passar das 6 h do SC-001);
  - adiantar para agora e o limite de 1 pedido manual por minuto por host;
  - aplicar uma página de resultado e, na última página, remover as referências do host que não vieram no pedido;
  - `outdated_since_utc` preenchido na transição para `update_available`, mantido em `failed` e apagado nos demais estados;
  - `failed` preserva `remote_digest` e `last_success_at_utc`.
  - Nota de execução: `checked_at_utc` é o instante do recebimento no plano de controle, não o relógio do agente, para a limpeza da última página comparar com o instante do pedido mesmo com relógios diferentes (data-model.md).

  Testes com PostgreSQL real em `backend/tests/Dokpod.ControlPlane.Api.Tests/ImageUpdates/ImageUpdateStoreTests.cs`, inclusive duas réplicas disputando o mesmo host.
- [x] T007 Composição Business:
  - `ImageUpdateModule` em `backend/apps/Dokpod.ControlPlane.Api/ImageUpdates/ImageUpdateModule.cs`, registrado só pelo `ProductEdition` Business, como o `AuditExportModule`;
  - uso de `LicenseCapabilities.ImageUpdateNotice` (`image-update-notice`), que já existe em `backend/libs/Dokpod.Licensing/LicenseDocument.cs`;
  - extensão de `backend/tests/Dokpod.Architecture.Tests/BusinessGateTests.cs` para o namespace `ImageUpdates`.
- [x] T008 OpenAPI em `contracts/openapi/dokpod-control-plane.v1.yaml` conforme [contracts/api.md](contracts/api.md):
  - schema `ImageUpdateCheck` e propriedade opcional `updateCheck` nas imagens, nos containers e no resumo;
  - `POST /api/v1/environments/{environmentId}/images/update-check` com `x-dokpod-edition: business`;
  - `conditionKind: image_outdated` com `outdatedDays`;
  - códigos de problema novos (`agent_unsupported`, `environment_disconnected`, `alert_condition_unavailable`).
  - Nota de execução: `AlertRulePage.imageOutdatedAvailable` para a tela oferecer a condição, e resposta `ImageUpdateDenied` própria para o `403`.

  Regerar o cliente do frontend (`npm run generate:api` no container `angular-cli`) e conferir os testes de contrato do OpenAPI.

**Checkpoint**: protocolo, tabelas e módulo prontos; nenhum comportamento visível ainda.

---

## Fase 3: História 1 - Ver quais containers rodam imagem desatualizada (Prioridade: P1) 🎯 MVP

**Objetivo**: a verificação roda a cada 6 h e sob demanda, e Imagens, Containers e o detalhe mostram o estado.

**Teste Independente**: publicar uma nova versão de uma tag no registro de teste, acionar "Verificar agora" e ver "Nova versão disponível" nas três telas, com a data; imagem por digest, construída no host e de registro fora da política aparecem como "Não verificável" com o motivo.

- [ ] T009 [P] [US1] `OciDistributionClient` em `backend/libs/Dokpod.Agent.Infrastructure/Registries/OciDistributionClient.cs` ([D-001](research.md#d-001-onde-e-como-consultar-o-registro), [D-005](research.md#d-005-limites-no-agente)):
  - `HEAD` do manifesto com os quatro tipos de `Accept` e `GET` do índice só quando preciso;
  - desafio `Bearer` (token anônimo ou com a credencial de `RegistryCredentialFile`) e `Basic`;
  - TLS verificado; `HTTPS_PROXY` do container respeitado;
  - 10 s por requisição e 4 MiB de corpo lido;
  - redirecionamento só para o mesmo host, sem `Authorization` para outro;
  - mapeamento: `401/403` → `unauthorized`, `404` → `reference_not_found`, `429` → `rate_limited`, `5xx` e falha de conexão → `registry_unavailable`, tempo esgotado → `timeout`, resposta sem digest ou corpo inválido → `invalid_response`.

  Threat model:
  - L1: `realm` do desafio só `https`; credencial enviada ao `realm` só quando ele é do mesmo host do registro ou no par `docker.io` → `auth.docker.io`; nos demais casos, token anônimo;
  - L2: no caminho da verificação, log só com o tipo da exceção e o motivo, nunca mensagem, URL ou referência;
  - L3: token só em memória e só durante o pedido, por repositório;
  - L4: resposta do token endpoint lida até 64 KiB, acima disso `invalid_response`.

  Nenhum cabeçalho, token ou corpo em log. Testes contra o `OciTestRegistry` (T002) em `backend/tests/Dokpod.Agent.Infrastructure.Tests/Registries/OciDistributionClientTests.cs`, inclusive `realm` em outro host (sem credencial), `realm` `http` (recusado) e token endpoint grande.
- [ ] T010 [P] [US1] `ImageUpdateChecker` em `backend/libs/Dokpod.Agent.Application/Images/ImageUpdateChecker.cs`, com a comparação de [D-002](research.md#d-002-comparação-de-digests-e-plataforma) e os casos não verificáveis de [D-003](research.md#d-003-referências-não-verificáveis):
  - digest remoto entre os `repo_digests` locais do mesmo repositório → `up_to_date`;
  - índice mudou mas o manifesto da plataforma do host não → `up_to_date`;
  - senão → `update_available`;
  - referência só por digest → `digest_reference`; sem `repo_digests` → `local_image`; registro inseguro no `RegistryInventory` → `insecure_registry`, sem chamada HTTP.

  Testes em `backend/tests/Dokpod.Agent.Application.Tests/Images/ImageUpdateCheckerTests.cs`.
- [ ] T011 [US1] Atendimento da mensagem 17 no agente (`backend/apps/Dokpod.Agent/AgentControlWorker.cs` e um handler em `backend/libs/Dokpod.Agent.Application/Images/ImageUpdateCheckHandler.cs`):
  - até 500 itens, 4 consultas simultâneas e 5 min por pedido;
  - resultados em páginas de até 256 KiB, sempre um resultado por item;
  - `CAPABILITY_IMAGE_UPDATE_CHECK` anunciada pelos agentes Linux com Docker e com Podman. O agente Windows não anuncia até passar no mesmo teste (decisão do plano);
  - logs do handler só com tipo de exceção e motivo (L2).

  Testes em `backend/tests/Dokpod.Agent.Application.Tests/Images/ImageUpdateCheckHandlerTests.cs`.
- [ ] T012 [US1] Teste contra registro real em `backend/tests/Dokpod.Agent.Infrastructure.Tests/Registries/OciRegistryIntegrationTests.cs` (fixture de T002):
  - publicar, verificar `up_to_date`, republicar a tag e verificar `update_available`;
  - índice em que só outra arquitetura muda continua `up_to_date`;
  - credencial errada dá `unauthorized`;
  - tag removida dá `reference_not_found`.
- [ ] T013 [US1] `ImageUpdateScheduler` em `backend/libs/Dokpod.ControlPlane.Application/ImageUpdates/ImageUpdateScheduler.cs` e `ImageUpdateWorker` em `backend/apps/Dokpod.ControlPlane.Api/ImageUpdates/ImageUpdateWorker.cs` ([D-006](research.md#d-006-agendamento-réplicas-e-verificar-agora)):
  - pegar o host vencido com lease e montar os itens a partir dos containers do inventário (referência e `image_id`);
  - gravar direto `registry_not_allowed` para registros fora de `RegistryPolicyService` no estágio do host, e `agent_unsupported` para agentes sem a capability;
  - enviar a mensagem 17 pelo `AgentControlService`, guardar `pending_request_id` e marcar o próximo vencimento;
  - host desconectado continua vencido;
  - não agendar quando a licença importada não lista `image-update-notice`; com a licença restrita, continuar (FR-009). O worker não consulta o gate.

  Testes em `backend/tests/Dokpod.ControlPlane.Application.Tests/ImageUpdates/ImageUpdateSchedulerTests.cs` com relógio injetável.
- [ ] T014 [US1] Recebimento da mensagem 27 em `backend/apps/Dokpod.ControlPlane.Api/Agents/AgentControlService.cs`:
  - aplicar as páginas pelo `ImageUpdateStore`;
  - descartar `request_id` que não é o pendente do host ou que chega mais de 6 min depois do envio;
  - validar estado, motivo, digests (`sha256:` + 64 hex) e o tamanho dos campos, recusando a página inteira se algo vier fora do contrato.

  Testes de integração com PostgreSQL real em `backend/tests/Dokpod.ControlPlane.Api.Tests/ImageUpdates/ImageUpdateResultTests.cs`.
- [ ] T015 [US1] Leitura nas listas (`backend/libs/Dokpod.ControlPlane.Infrastructure/` nos stores de imagens e de containers e no resumo do container):
  - junção indexada com `image_update_checks`;
  - `updateCheck` conforme [contracts/api.md](contracts/api.md), com `container_behind` derivado e `stale` quando o host está desconectado;
  - campo ausente na CE ou sem a capability;
  - nenhuma chamada ao agente durante a requisição.

  Testes em `backend/tests/Dokpod.ControlPlane.Api.Tests/ImageUpdates/ImageUpdateReadTests.cs`.
- [ ] T016 [US1] `POST /api/v1/environments/{environmentId}/images/update-check` em `backend/apps/Dokpod.ControlPlane.Api/ImageUpdates/ImageUpdateEndpoints.cs` e `ImageUpdateCheckService` com `[BusinessCapability("image-update-notice")]`:
  - scope `image:pull` no host, com falha do Keycloak negando;
  - `202`, ou `429 rate_limited` com `Retry-After`, `409 agent_unsupported`, `409 environment_disconnected`, `403 license_expired` ou `403 license_invalid`;
  - auditoria `image.update_check_requested` na categoria `images` de `backend/libs/Dokpod.Domain/Auditing/AuditCategories.cs`.

  Testes em `backend/tests/Dokpod.ControlPlane.Api.Tests/ImageUpdates/ImageUpdateEndpointsTests.cs`.
- [ ] T017 [US1] Telas:
  - em `frontend/web/src/app/features/images/images.page.*`, `frontend/web/src/app/features/container-list/` e `frontend/web/src/app/features/container-detail/`, estado em texto ("Nova versão disponível", "Atualizada", "Não verificável", "Falha na verificação", "Versão nova no host; o container usa a anterior"), com a data e a marca de desatualizado quando o host está desconectado;
  - motivos em pt-BR:

    | Motivo | Texto |
    | --- | --- |
    | `digest_reference` | "Referência fixada por digest" |
    | `local_image` | "Imagem construída ou importada no host" |
    | `registry_not_allowed` | "Registro fora da política do estágio" |
    | `insecure_registry` | "Registro sem TLS" |
    | `agent_unsupported` | "Agente sem suporte à verificação" |
    | `registry_unavailable` | "Registro indisponível" |
    | `rate_limited` | "Limite de requisições do registro atingido" |
    | `unauthorized` | "Credencial recusada pelo registro" |
    | `reference_not_found` | "Referência não encontrada no registro" |
    | `timeout` | "O registro não respondeu a tempo" |
    | `invalid_response` | "Resposta inválida do registro" |

  - "Verificar agora" por host com `image:pull`;
  - nada aparece quando a API não envia `updateCheck`.

  Testes nos `.spec.ts`, com rótulos em `frontend/web/src/app/features/images/image-update-labels.ts`.
- [ ] T018 [US1] E2E simulado `frontend/tests/e2e/mock_ui/test_025_image_update.py`, com cenários no `frontend/tests/e2e/mock/control_plane_mock.py`:
  - os quatro estados nas três telas;
  - "Verificar agora" com `202` e `429`;
  - CE sem nada.

**Checkpoint**: História 1 funcional e testável de forma independente (MVP).

---

## Fase 4: História 2 - Baixar a nova versão a partir do aviso (Prioridade: P2)

**Objetivo**: com `image:pull`, baixar a nova versão direto do aviso e ver que o container ainda usa a anterior.

**Teste Independente**: com aviso ativo, acionar "Baixar nova versão"; o pull da spec 012 roda e é auditado; a imagem fica "Atualizada" e o container, "Versão nova no host; o container usa a anterior". Sem `image:pull`, a ação não aparece.

- [ ] T019 [US2] Depois de um pull concluído com sucesso (`backend/libs/Dokpod.ControlPlane.Application/Images/ImagePullService.cs` ou o tratamento do resultado do comando), adiantar a verificação do host para agora, sem o limite manual e só na composição Business com a capability, para o estado refletir a imagem nova. Testes em `backend/tests/Dokpod.ControlPlane.Application.Tests/ImageUpdates/ImageUpdateAfterPullTests.cs`, inclusive o negativo do FR-004: depois do pull e da nova verificação, nenhum comando de criação, recriação ou atualização de container é enfileirado e o container mantém o `image_id` anterior.
- [ ] T020 [US2] Ação "Baixar nova versão" em Imagens, no detalhe do container e na lista de containers, abrindo o `pull-image.drawer` existente (`frontend/web/src/app/features/images/`) com host e referência preenchidos; só com `image:pull` e só em `update_available`. Testes nos `.spec.ts` e caso no `test_025_image_update.py`.

**Checkpoint**: Histórias 1 e 2 funcionando de forma independente.

---

## Fase 5: História 3 - Ser alertado sobre imagens desatualizadas (Prioridade: P3)

**Objetivo**: regra da spec 016 "imagem desatualizada há mais de N dias" abre e resolve alertas.

**Teste Independente**: criar a regra em Produção, simular a defasagem com relógio injetável e conferir o alerta aberto e, depois do container recriado e de nova verificação, resolvido.

- [ ] T021 [US3] Condição no domínio e na governança ([D-010](research.md#d-010-condição-de-alerta)):
  - `AlertConditionKind.ImageOutdated` e `OutdatedDays` (1 a 365, obrigatório só para ela) em `backend/libs/Dokpod.Domain/Alerts/AlertModels.cs`;
  - provedor `IAlertConditionAvailability`, registrado só pelo `ImageUpdateModule`. Sem ele (CE), criar ou editar dá `422 alert_condition_unavailable`;
  - criar e editar regra com a condição passam pelo gate (`403 license_expired` ou `license_invalid`); desativar e excluir, nunca;
  - persistência em `alert_rules.outdated_days`.

  Testes em `backend/tests/Dokpod.ControlPlane.Application.Tests/Alerts/` e `backend/tests/Dokpod.ControlPlane.Api.Tests/` (rotas das regras).
- [ ] T022 [US3] Avaliação em `backend/libs/Dokpod.ControlPlane.Application/Alerts/AlertSweepService.cs`:
  - abre um alerta por container em `update_available` ou `container_behind` cuja referência tem `outdated_since_utc` de N dias ou mais;
  - resolve quando o container deixa esses estados ou some;
  - silenciamentos e auditoria da 016 valem como para as outras condições.

  Testes com relógio injetável.
- [ ] T023 [US3] Condição "Imagem desatualizada há mais de N dias" em `frontend/web/src/app/features/alerts/rule.drawer.ts`, visível só quando a API anuncia a condição, com validação de 1 a 365. Testes em `alerts-forms.spec.ts` e caso no `test_025_image_update.py`.

**Checkpoint**: as três histórias funcionando de forma independente.

---

## Fase Final: Refinamento e Aspectos Transversais

- [ ] T024 [P] Canário de SC-003 em `backend/tests/Dokpod.ControlPlane.Api.Tests/ImageUpdates/ImageUpdateCanaryTests.cs` e `backend/tests/Dokpod.Agent.Infrastructure.Tests/Registries/`. Uma senha e um token plantados no `config.json` do host não aparecem:
  - nas respostas da API;
  - no banco;
  - nos logs do agente e da API;
  - em spans e métricas (pipeline de `ApiTelemetryInstrumentation`).
- [ ] T025 [P] Medição de SC-004 em `backend/tests/Dokpod.ControlPlane.Api.Tests/ImageUpdates/ImageUpdateListImpactTests.cs`, isolada por variável (`DOKPOD_TEST_SC025=1`, como o SC-004 da 027). Com 200 imagens verificadas em um host, o p95 da primeira página de Imagens e de Containers fica no máximo 10% acima do p95 sem dados de verificação, com os dois valores na saída.
- [ ] T026 [P] Métricas `dokpod.image_update.checks` (por estado e motivo) e `dokpod.image_update.outdated` (gauge por host), sem referência nem registro como dimensão, e linha em `docs/runbooks/observabilidade.md`.
- [ ] T027 Documentação:
  - novo `docs/runbooks/imagens-aviso-nova-versao.md`, com estados, motivos, credencial e proxy do agente (`HTTPS_PROXY`), registros inseguros, cota do Docker Hub, agente Windows e licença restrita;
  - linha da capability `image-update-notice` em `docs/licenciamento.md` (seção de capabilities implementadas);
  - seção em `docs/seguranca.md` sobre a saída do agente para registros.
- [ ] T028 Validação com agentes Docker e Podman reais da stack E2E: capability anunciada nos dois engines e cenários 1 e 3 do quickstart contra um registro real.
- [ ] T029 Revisão de segurança e privacidade contra o T001, registrada em `specs/025-aviso-nova-versao-imagem/threat-model.md`.
- [ ] T030 Executar os cenários de [quickstart.md](quickstart.md) e registrar as evidências em `specs/025-aviso-nova-versao-imagem/validation.md`, com `NOT RUN` explícito no que faltar. O teste contra `registry:2` real (T012), a medição de SC-004 (T025) e os agentes reais (T028) não rodam no CI: registrar a execução dedicada de cada um, com comando e resultado, ou `NOT RUN` com o motivo.

---

## Dependências e Ordem de Execução

- **Preparação**: T001 bloqueia todo o código; T002 pode correr junto com o threat model.
- **Fundação**: T003 e T004 primeiro; T005 → T006; T007 e T008 depois de T003. Bloqueia as histórias.
- **História 1**: T009 e T010 em paralelo (dependem de T002–T004); T011 depende de T009 e T010; T012 de T009 e T011; T013 e T014 de T006 e T007; T015 de T006; T016 de T007 e T013; T017 de T008 e T015; T018 de T017.
- **História 2**: depende da História 1 (T013 e T015); T019 e T020 em paralelo.
- **História 3**: depende de T006 e T015; T021 → T022 → T023. Independente da História 2.
- **Refinamento**: depois das histórias entregues; T024, T025 e T026 em paralelo.

## Exemplos de Execução Paralela

- **Fundação**: T004 com T003; depois T007 e T008 juntos.
- **História 1**: T009 e T010 juntos; depois T013 e T015 em paralelo com T011 e T012.
- **História 2**: T019 e T020 juntos.
- **Refinamento**: T024, T025 e T026 juntos.

## Estratégia de Implementação

1. **MVP**: T001 aceito → Fundação → História 1. Validar o cenário 1 do quickstart e entregar.
2. **Incremento 2**: História 2 (baixar do aviso).
3. **Incremento 3**: História 3 (alerta).
4. **Fechamento**: refinamento, revisão de segurança e `validation.md`.
