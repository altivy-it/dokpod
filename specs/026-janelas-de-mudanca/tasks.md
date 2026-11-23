---

description: "Tarefas da implementação das janelas de mudança por estágio"
---

# Tarefas: Janelas de mudança por estágio

**Entrada**: Documentos de design em `/specs/026-janelas-de-mudanca/`

> Redija todas as tarefas e descrições em português brasileiro (pt-BR), preservando identificadores, comandos e caminhos técnicos.

**Pré-requisitos**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/api.md](contracts/api.md), [quickstart.md](quickstart.md)

**Testes**: obrigatórios (constituição, princípio VI): unidades com relógio injetável, integração com PostgreSQL real, testes de arquitetura, E2E pela borda nginx e validação com agentes reais.

**Organização**: agrupadas por história da spec para implementação e validação independentes.

**Gate**: nenhuma tarefa de código começa antes de:

- a spec aprovada explicitamente pelo solicitante, incluindo as respostas da sessão de 2026-10-27, marcadas "sujeitas à aprovação";
- os itens de "Itens para a revisão humana" do [plano](plan.md) decididos (host sem estágio e o código `409` mudam o guard da T007);
- T001 (threat model) aceito pelo revisor humano.

Esses três gates valem a partir da T003. `/speckit-implement` exige autorização humana explícita.

## Formato: `[ID] [P?] [Story] Descricao`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência pendente)
- **[Story]**: história da spec (US1, US2, US3)

## Convencoes de Caminhos

- Domínio: `backend/libs/Dokpod.Domain/ChangeWindows/`
- Aplicação: `backend/libs/Dokpod.ControlPlane.Application/ChangeWindows/`
- Infraestrutura: `backend/libs/Dokpod.ControlPlane.Infrastructure/ChangeWindows/`
- API (rotas só na composição Business): `backend/apps/Dokpod.ControlPlane.Api/ChangeWindows/`
- Testes: `backend/tests/Dokpod.ControlPlane.Application.Tests/ChangeWindows/`, `backend/tests/Dokpod.ControlPlane.Api.Tests/ChangeWindows/`, `backend/tests/Dokpod.Architecture.Tests/`
- Frontend: `frontend/web/src/app/features/settings/` e `frontend/web/src/app/features/change-window/` (fora do design-system, porque lê a API); E2E simulado: `frontend/tests/e2e/mock_ui/` e `frontend/tests/e2e/mock/control_plane_mock.py`
- Backend roda em container: `podman run --rm --user 0 -v "$PWD:/src:Z" -w /src docker.io/lzocateli/dotnet-sdk:10.0.403-noble-v1 dotnet test ...`; frontend pelo alias `ng` em `frontend/web`.

---

## Fase 1: Preparação (Infraestrutura Compartilhada)

**Objetivo**: fronteira de segurança aceita e scope criado no Keycloak antes de qualquer código.

- [x] T001 Threat model em `specs/026-janelas-de-mudanca/threat-model.md`, no formato do de `specs/027-exportacao-auditoria-siem/threat-model.md`. Cobrir:
  - contorno da janela por cliente da API que ignora a UI, por ação nova sem guard e pelo terminal ([D-001](research.md#d-001-ponto-único-de-aplicação));
  - abuso do scope `change:override` (concessão ampla, exceção repetida, justificativa vazia ou trivial) e a falha fechada do Keycloak ([D-005](research.md#d-005-exceção-de-emergência));
  - manipulação do relógio e do fuso (relógio do navegador, réplicas sem NTP, tzdata desatualizado) ([D-004](research.md#d-004-fuso-e-cálculo-da-janela), [D-010](research.md#d-010-relógio-e-comandos-em-andamento));
  - vazamento do horário da janela para quem não pode executar a ação (ordem das verificações em [contracts/api.md](contracts/api.md#ações-controladas-ce-e-business));
  - licença restrita e troca de edição para a CE ([D-002](research.md#d-002-composição-entre-ce-e-business), [D-007](research.md#d-007-licença));
  - volume de `change_window.denied` gerado por repetição ([D-006](research.md#d-006-auditoria)).

  Registrar lacunas, riscos residuais e a decisão do revisor humano. Bloqueia todas as tarefas de código. Nota (2026-11-20): aceito pelo solicitante, com as lacunas L1, L4 e L6 aplicadas na T007 e na T022, L3 no contrato, L2 e L5 aceitas sem mudança, e os riscos R1–R7 aceitos.
- [x] T002 [P] Scope `change:override` em `tools/scripts/configure-keycloak.ps1` (`Ensure-AuthorizationScopes`, com o comentário "Spec 026: exceção de emergência fora da janela de mudança") e no `ValidateSet` de `-Scope` em `tools/scripts/provision-environment-authorization.ps1`. Nenhuma permissão nem papel recebe o scope por padrão ([D-009](research.md#d-009-scope-changeoverride-no-keycloak)). Conferir com `-Drift` e com o `--help` dos dois scripts. Nota (2026-11-20): validado com o `Keycloak.Tests.ps1` do instalador contra um Keycloak real (13 aprovados, 3 pulados por exigirem `DOKPOD_TEST_SHARED`): a reconciliação cria o scope e o `-Drift` seguinte sai vazio; `--help` dos dois scripts e `provision-environment-authorization.ps1 -Scope change:override -DryRun` conferidos. Instalações existentes acusam o scope ausente no `-Drift` até a próxima reconciliação (upgrade).

---

## Fase 2: Fundação (Pré-requisitos Bloqueadores)

**Objetivo**: modelo, calendário, guard obrigatório na fila, persistência e composição Business.

**CRÍTICO**: nenhuma história começa antes desta fase.

- [x] T003 [P] Modelo em `backend/libs/Dokpod.Domain/ChangeWindows/ChangeWindow.cs`, conforme [data-model.md](data-model.md):
  - `ChangeWindowRange(IReadOnlySet<DayOfWeek> Days, TimeOnly Start, TimeOnly End)`: de 1 a 7 dias sem repetição, `Start` e `End` em minutos inteiros de `00:00` a `23:59`, `End != Start`; `End < Start` cruza a meia-noite e pertence ao dia de início;
  - `ChangeWindow(EnvironmentStage Stage, string TimeZone, IReadOnlyList<ChangeWindowRange> Ranges, int Version, string UpdatedBy, DateTimeOffset UpdatedAtUtc)`: `TimeZone` IANA de até 64 caracteres aceito por `TimeZoneInfo.TryFindSystemTimeZoneById`; de 1 a 14 faixas;
  - `ChangeWindow.Validate(...)` devolve a lista de erros por campo (`timeZone`, `ranges[i].days`, `ranges[i].start`, `ranges[i].end`), sem lançar exceção, para o `400 change_window_invalid`.
- [x] T004 [P] Testes de `ChangeWindowCalendar` em `backend/tests/Dokpod.ControlPlane.Application.Tests/ChangeWindows/ChangeWindowCalendarTests.cs`, com instantes UTC fixos e sem depender do fuso do host:
  - seg-sex 22:00-06:00 em `America/Sao_Paulo`: terça 15:00 fechado com `nextOpensAt` terça 22:00; sábado 03:00 aberto (faixa de sexta); sábado 07:00 fechado com `nextOpensAt` segunda 22:00;
  - faixas sobrepostas valem como união; faixas contíguas (22:00-00:00 e 00:00-06:00 no dia seguinte) dão um só `closesAt`;
  - horário de verão: entrada e saída em `Europe/Lisbon` e numa data histórica com horário de verão em `America/Sao_Paulo`, com início no buraco (primeiro instante válido depois) e fim no horário repetido (segunda ocorrência), conforme [D-004](research.md#d-004-fuso-e-cálculo-da-janela);
  - estágio sem janela dá `none`; a busca de `nextOpensAt` cobre 8 dias locais.
- [x] T005 `ChangeWindowCalendar` (função pura, `Evaluate(ChangeWindow?, DateTimeOffset utc)` → `ChangeWindowStatus(State, ClosesAt, NextOpensAt, TimeZone)`, com `State` igual a `Open`, `Closed` ou `None`) em `backend/libs/Dokpod.ControlPlane.Application/ChangeWindows/ChangeWindowCalendar.cs`, até T004 passar.
- [x] T006 Portas em `backend/libs/Dokpod.ControlPlane.Application/ChangeWindows/ChangeWindowPorts.cs`:
  - `IChangeWindowSchedule.GetAsync(EnvironmentStage, CancellationToken)` → `ChangeWindow?` e `ListAsync(CancellationToken)`;
  - `NoChangeWindows`, que sempre devolve nulo, registrado na composição da CE em `ControlPlaneInfrastructureServiceCollectionExtensions` (o módulo Business o substitui);
  - `IChangeWindowStore` (só Business): `SaveAsync(window, expectedVersion | criar)` e `DeleteAsync(stage, expectedVersion)`, com o conflito de versão como resultado, não como exceção.
- [x] T007 Guard e liberação em `backend/libs/Dokpod.ControlPlane.Application/ChangeWindows/ChangeWindowGuard.cs`:
  - `ChangeWindowClearance` com `EnvironmentId`, `Kind` (`AgentCommandKind` ou o marcador de terminal), `Mode` (`NoWindow`, `Open`, `Override`, `Replay`) e `OverrideAuditEvent?`. O construtor é privado e a única fábrica fica no `ChangeWindowGuard`: um construtor `internal` não basta, porque a fila e os serviços estão no mesmo assembly;
  - antes de avaliar a janela, o guard procura o comando com `IAgentCommandStore.GetAsync(environmentId, commandId)`. Se ele existe, a liberação sai em `Replay`, sem avaliar a janela nem consultar o Keycloak ([D-001](research.md#d-001-ponto-único-de-aplicação), princípio IV);
  - `ChangeWindowGuard.EvaluateAsync(registration, kind, commandId, actor, accessToken, correlationId, ChangeWindowOverrideRequest?, ct)` → liberação ou recusa (`change_window_closed` com `nextWindowStartsAt`, `justification_required` ou `change_override_denied`, e `authorization_unavailable` quando o Keycloak não responde), conforme [D-005](research.md#d-005-exceção-de-emergência);
  - host sem estágio, estágio sem janela ou janela aberta: liberação sem consultar o Keycloak; com janela aberta, a liberação leva o prazo máximo (`closesAt` + 5 min), e o serviço encurta o `deadlineUtc` pedido para ele antes de montar o comando (threat model L1);
  - a busca do `commandId` (`Replay`) acontece dentro do guard, depois da autorização da ação e da checagem do ambiente feitas pelo serviço (threat model L6);
  - nenhuma lista de ações liberadas: todo `AgentCommandKind` é controlado ([D-001](research.md#d-001-ponto-único-de-aplicação)).
- [x] T008 Fila exige a liberação:
  - `AgentCommandQueueService.EnqueueAsync(command, auditEvent, ChangeWindowClearance clearance, ct)` em `backend/libs/Dokpod.ControlPlane.Application/Commands/AgentCommandQueueService.cs` recusa (`ArgumentException`) liberação de outro ambiente ou de outro tipo. Com `Replay`, devolve só `Duplicate` ou `ConflictingPayload` e nunca cria comando. Se a linha sumiu (expurgo), devolve um resultado que leva o serviço a pedir nova avaliação ao guard como pedido novo;
  - `IAgentCommandStore.EnqueueAuditedAsync` em `backend/libs/Dokpod.ControlPlane.Application/Commands/IAgentCommandStore.cs` e `backend/libs/Dokpod.ControlPlane.Infrastructure/PostgresAgentCommandStore.cs` recebe a lista de eventos (o da ação e, com `Override`, o `change_window.override`) e grava todos na mesma transação do comando ([D-006](research.md#d-006-auditoria));
  - ajustar os seis chamadores (`ContainerCommandService`, `ContainerCreationService`, `ImagePullService`, `VolumeRemovalService`, `NetworkRemovalService`, `PodActionService`) para compilar, pedindo a liberação ao guard logo depois da autorização e da checagem do ambiente, antes da justificativa da 016. Nesta tarefa, o pedido de exceção ainda é nulo.
- [x] T009 [P] Teste de arquitetura em `backend/tests/Dokpod.Architecture.Tests/ChangeWindowGuardTests.cs`:
  - todo tipo de `backend/libs/Dokpod.ControlPlane.Application` que depende de `AgentCommandQueueService` também depende de `ChangeWindowGuard`, e o `TerminalSessionService` depende de `ChangeWindowGuard`;
  - nenhum tipo além do `ChangeWindowGuard` cria `ChangeWindowClearance` (por reflexão: o tipo só tem construtores privados e as fábricas estáticas só aparecem no guard);
  - inclui o caso negativo (um tipo de teste sem o guard reprova).
- [x] T010 [P] Categoria `governance` em `backend/libs/Dokpod.Domain/Auditing/AuditCategories.cs` com `change_window.updated`, `change_window.removed`, `change_window.denied` e `change_window.override`.
- [x] T011 Migration `backend/libs/Dokpod.ControlPlane.Infrastructure/Migrations/Script/<AAAAMMDDNNNN>_ChangeWindows/Up/01-ChangeWindows.sql` e `Down/01-ChangeWindows.sql`:
  - tabela `dokpod.change_windows` com `stage text PRIMARY KEY CHECK (stage IN ('production','staging','development'))`, `time_zone text NOT NULL CHECK (length(time_zone) BETWEEN 1 AND 64)`, `ranges jsonb NOT NULL CHECK (jsonb_typeof(ranges) = 'array' AND jsonb_array_length(ranges) BETWEEN 1 AND 14)`, `version integer NOT NULL CHECK (version >= 1)`, `updated_by text NOT NULL`, `updated_at_utc timestamptz NOT NULL`;
  - `GRANT SELECT, INSERT, UPDATE, DELETE` ao `dokpod_runtime`;
  - acrescentar `change:override` ao `scopes` de todos os ambientes cadastrados, no padrão de `202610150004_NetworkInventory` (a concessão por usuário continua no Keycloak); o `Down` remove a tabela e o scope;
  - `change:override` em `EnvironmentResourceScopes` (`ChangeOverride`, e em `Supported`) em `backend/libs/Dokpod.Domain/Environments/EnvironmentRegistration.cs`, e no cadastro de ambientes novos.
- [x] T012 [P] Teste da migration em `backend/tests/Dokpod.ControlPlane.Api.Tests/ChangeWindows/ChangeWindowMigrationTests.cs`, com PostgreSQL real: `Up` cria a tabela e os `CHECK` recusam estágio, fuso e faixas inválidos; ambientes existentes ganham `change:override` sem duplicar; `Down` validado via `psql` numa transação (EF não reverte migrations neste repositório).
- [x] T013 Store `ChangeWindowStore` (implementa `IChangeWindowSchedule` e `IChangeWindowStore`) em `backend/libs/Dokpod.ControlPlane.Infrastructure/ChangeWindows/ChangeWindowStore.cs`: leitura por chave primária, `INSERT` só sem linha, `UPDATE`/`DELETE ... WHERE version = @expected`, serialização das faixas com dias `mon`…`sun` e horas `HH:mm`.
- [x] T014 Módulo `ChangeWindowModule` em `backend/apps/Dokpod.ControlPlane.Api/ChangeWindows/ChangeWindowModule.cs`, registrado só em `backend/apps/Dokpod.ControlPlane.Api/ProductEdition.cs` sob `DOKPOD_BUSINESS`: substitui `IChangeWindowSchedule` pelo `ChangeWindowStore` e mapeia as rotas. Estender o teste de composição da CE para confirmar que `/api/v1/change-windows` não existe e que o `IChangeWindowSchedule` da CE é o `NoChangeWindows`.
- [x] T015 Contrato OpenAPI em `contracts/openapi/dokpod-control-plane.v1.yaml`, conforme [contracts/api.md](contracts/api.md):
  - rotas `GET /api/v1/change-windows`, `PUT` e `DELETE /api/v1/change-windows/{stage}` com `x-dokpod-edition: business`, `ETag`, `If-Match`, `If-None-Match`, `400 change_window_invalid`, `403 license_*` e `412`;
  - `changeWindowOverride` opcional nos corpos das seis ações e em `OpenTerminalRequest`;
  - `409 change_window_closed` (com `nextWindowStartsAt` e `timeZone`) e `403 change_override_denied` nas respostas dessas ações;
  - `changeWindow` (`state`, `closesAt`, `nextOpensAt`, `timeZone`) nas respostas de host.

  Regenerar o cliente do frontend com `npm run generate:api` no container `angular-cli` e conferir o teste de contrato existente.

**Checkpoint**: o guard compila em todos os caminhos, a CE libera tudo e a Business tem persistência.

**Execução da fundação (2026-11-22, IA assistida)**: T003–T015 validadas com a suíte do backend contra PostgreSQL 17.6 real (migrations aplicadas por `dotnet ef database update`; Application 304, Architecture 34, Api 432 com 1 falha da própria expectativa, corrigida e reexecutada no recorte da 026: 26 aprovados), o build da edição Business e os testes do frontend (201). Diferenças em relação ao texto das tarefas, para a revisão humana:

- A liberação é o tipo aninhado `ChangeWindowGuard.Clearance`: em C#, só um tipo aninhado deixa o construtor privado e a fábrica no guard ao mesmo tempo.
- A recusa por horário e a exceção negada gravam `change_window.denied` no próprio guard, para não repetir a regra nos sete pontos; a T025 só monta a resposta `409`/`403`.
- No `Replay`, a fila compara o comando lido pelo guard e não toca o store, então não existe o caso "a linha sumiu"; o snapshot ganhou `PayloadHash` e o prazo gravado menor que o pedido (encurtado pela L1) conta como duplicado.
- O `NoChangeWindows` e o guard são registrados no `ApiHost`, e não em `ControlPlaneInfrastructureServiceCollectionExtensions`, para valer também sem banco.
- O terminal já consulta o guard (exigido pelo teste da T009); a exceção no terminal e o motivo no hub seguem na T024/T031.
- As rotas Business entram com a T018; o `ChangeWindowModule` desta fase só troca o calendário.
- Os serviços ganharam o resultado `ChangeWindowDenied`, que as rotas só mapeiam na T025; até lá ele é inalcançável, porque sem a T018 nenhuma janela pode ser gravada.
- O contrato não tem `OpenTerminalRequest` (o terminal é SignalR); o pedido de exceção do terminal fica documentado em `contracts/api.md`.
- A migration só converte `scopes` que são listas JSON, como o cadastro grava; `manifest.json` e `expected-schema.json` do catálogo SQL foram regravados com `DOKPOD_UPDATE_SQL_CATALOG=1`.

---

## Fase 3: História de Usuário 1 - Definir janelas por estágio (Prioridade: P1)

**Objetivo**: o administrador cria, edita e remove a janela de um estágio em Configurações → Políticas, com auditoria e leitura para quem não pode editar.

**Teste independente**: criar a janela de Produção, conferir na vista Políticas e na auditoria, editar com `If-Match`, remover; com outro usuário, ver só para leitura.

### Testes da História 1

- [x] T016 [P] [US1] Testes das rotas em `backend/tests/Dokpod.ControlPlane.Api.Tests/ChangeWindows/ChangeWindowEndpointsTests.cs`, com PostgreSQL real e a composição Business:
  - `PUT` com `If-None-Match: *` cria (`200`, `ETag: "1"`); com `If-Match` errado dá `412`; corpo com propriedade desconhecida, fuso inválido, `start == end`, 15 faixas ou dia repetido dá `400 change_window_invalid` com o campo;
  - sem `environment:manage` em algum host legível do estágio, `PUT` e `DELETE` dão `403`; o `GET` mostra `editable: false`;
  - licença restrita ou sem `change-windows`: `PUT` dá `403 license_*`; `DELETE` dá `204`; `GET` responde;
  - cada `PUT` e `DELETE` grava `change_window.updated` ou `change_window.removed` em cada host do estágio, com fuso e faixas anteriores e novos.

### Implementação da História 1

- [x] T017 [US1] `ChangeWindowSettingsService` em `backend/libs/Dokpod.ControlPlane.Application/ChangeWindows/ChangeWindowSettingsService.cs`:
  - `[BusinessCapability(LicenseCapabilities.ChangeWindows)]`, com o gate só em `SaveAsync` ([D-007](research.md#d-007-licença));
  - autorização "`environment:manage` em todos os hosts legíveis do estágio", reaproveitando a regra de `AlertGovernanceService.SetPolicyAsync` (016 D-007) sem duplicá-la;
  - auditoria por host do estágio, como o `registry.policy_changed`.
- [x] T018 [US1] Rotas em `backend/apps/Dokpod.ControlPlane.Api/ChangeWindows/ChangeWindowEndpoints.cs`, seguindo `backend/apps/Dokpod.ControlPlane.Api/AuditExport/AuditExportEndpoints.cs`: `ETag` com a versão, `problem+json`, `404` para estágio desconhecido e `status` calculado com o `ChangeWindowCalendar` no `GET`.
- [x] T019 [US1] Seção de janelas na vista Políticas:
  - `frontend/web/src/app/features/settings/change-windows-api.service.ts` sobre o cliente gerado;
  - `frontend/web/src/app/features/settings/change-windows-section.component.{ts,html}`, com faixas (dias, início, fim), um fuso por estágio, autor e horário, edição só com `editable`, aviso de licença restrita (editar bloqueado, remover permitido) e textos em `settings-labels.ts`;
  - exibida só com `edition` Business e a capability, como a seção de exportação da 027;
  - `change-windows-section.spec.ts` com edição, leitura, `412` e licença restrita.
- [x] T020 [US1] E2E pela borda nginx com respostas simuladas em `frontend/tests/e2e/mock_ui/test_026_change_windows.py` e rotas em `frontend/tests/e2e/mock/control_plane_mock.py`: criar, editar e remover a janela; usuário sem permissão vê só para leitura.

**Checkpoint**: janelas configuráveis e auditadas; nenhuma ação é recusada ainda além do que a Fase 2 já faz.

**Execução da US1 (2026-11-22, IA assistida)**: T016–T020 validadas com a suíte do backend contra PostgreSQL 17.6 real (Api 442 aprovados e 3 pulados; Application 304; Architecture 34), o build Business, os testes do frontend (208), o build de produção do Angular e a E2E simulada completa pela borda nginx (125 aprovados, incluindo os 4 da `test_026`). Diferenças em relação ao texto das tarefas, para a revisão humana:

- A regra de edição da 016 virou dois métodos públicos do `AlertGovernanceService` (`StageEditorAsync` para a tela e `AuthorizeStageAsync` para gravar), usados pela política de justificativa e pelas janelas; o comportamento da 016 não mudou. Num estágio sem host legível, a regra herdada decide pelos hosts legíveis e audita neles, como a política de justificativa.
- Sem `If-Match` nem `If-None-Match`, a resposta é `400 if_match_required`, como na exportação da 027; o contrato foi ajustado (o rascunho tinha `428`).
- O alvo dos eventos `change_window.updated` e `change_window.removed` é o estágio, e as faixas vão em texto (`mon,tue 22:00-06:00`).
- A vista Políticas passou a ler a instalação para saber a edição, porque antes só a aba Instalação a lia; na CE nada muda.
- O mock da E2E ganhou os cenários `business-cw`, `business-cw-readonly` e `business-cw-restricted`.
- O build de produção do Angular avisa que o bundle inicial passou 2 kB do orçamento de 420 kB; o aviso já existe no `main` (422,18 kB) e não vem desta história.

---

## Fase 4: História de Usuário 2 - Ser impedido de mudar fora da janela (Prioridade: P1)

**Objetivo**: toda ação que muda o host é recusada fora da janela, pela tela e pela API, com a próxima abertura; as leituras nunca são afetadas.

**Teste independente**: com uma janela gravada direto pelo store (não depende da US1), tentar cada ação controlada fora do horário e conferir `409`, a ausência do comando na fila e a auditoria; repetir as leituras.

### Testes da História 2

- [x] T021 [P] [US2] Testes do guard em `backend/tests/Dokpod.ControlPlane.Application.Tests/ChangeWindows/ChangeWindowGuardTests.cs`: cada valor de `AgentCommandKind` (iteração sobre o enum, para um tipo novo entrar sozinho) e o terminal são recusados fora da janela e liberados dentro; host sem estágio e estágio sem janela liberam; `NoChangeWindows` libera tudo.
- [x] T022 [P] [US2] Testes de integração em `backend/tests/Dokpod.ControlPlane.Api.Tests/ChangeWindows/ChangeWindowEnforcementTests.cs`, com PostgreSQL real e `TimeProvider` falso:
  - fora da janela, as rotas de comando de container, criação, pull, remoção de volume e de rede e ações de pod dão `409 change_window_closed` com `nextWindowStartsAt`, sem linha nova em `agent_commands`, e com `change_window.denied`;
  - quem não tem o scope da ação recebe `403` da ação, e não `409`, inclusive com um `commandId` existente (threat model L6);
  - com o servidor fora da janela e cabeçalhos de data do cliente (`Date`) dentro dela, a recusa vale (threat model L4);
  - dentro da janela, um `deadlineUtc` de dias é encurtado para `closesAt` + 5 min; com exceção, o prazo pedido é mantido (threat model L1);
  - logs, inspeção, alertas, auditoria e listas respondem `200` (SC-003);
  - comando aceito dentro da janela continua `Pending` depois do fechamento, e o reenvio com o mesmo `commandId` e o mesmo payload depois do fechamento devolve o resultado de duplicado (e não `409`), sem nova linha nem `change_window.denied`; com payload diferente, devolve o conflito de hoje ([D-010](research.md#d-010-relógio-e-comandos-em-andamento));
  - com licença restrita, a recusa continua (FR-007).
- [x] T023 [P] [US2] Teste do terminal em `backend/tests/Dokpod.ControlPlane.Api.Tests/ChangeWindows/ChangeWindowTerminalTests.cs`: `OpenTerminal` fora da janela termina com `change_window_closed` e `container.exec.denied` com esse motivo; uma sessão aberta antes do fechamento continua.

### Implementação da História 2

- [x] T024 [US2] Guard no terminal em `backend/libs/Dokpod.ControlPlane.Application/Terminal/TerminalSessionService.cs` e `backend/apps/Dokpod.ControlPlane.Api/Realtime/ControlPlaneHub.cs` (`OpenTerminal`), na mesma ordem das ações: autorização, ambiente, janela, justificativa.
- [x] T025 [US2] Respostas `409 change_window_closed` e auditoria `change_window.denied` nas rotas de `backend/apps/Dokpod.ControlPlane.Api/Endpoints/ContainerCommandEndpoints.cs`, `backend/apps/Dokpod.ControlPlane.Api/Environments/{ContainerDetailEndpoints,VolumeEndpoints,NetworkEndpoints,PodEndpoints,ImageEndpoints}.cs` e `backend/apps/Dokpod.ControlPlane.Api/Containers/ContainerCreationEndpoints.cs`, com o texto "Fora da janela de mudança" e `nextWindowStartsAt`.
- [x] T026 [US2] `changeWindow` nas respostas de `backend/apps/Dokpod.ControlPlane.Api/Environments/HostResponses.cs`, calculado uma vez por estágio por resposta, com `state: none` na CE.
- [x] T027 [US2] Aviso nos botões:
  - componente compartilhado em `frontend/web/src/app/features/change-window/` que, a partir de `changeWindow`, mostra o texto "Fora da janela de mudança" e a próxima abertura no fuso do estágio, desabilita a ação e agenda a troca de estado para `closesAt` e `nextOpensAt` (sem polling);
  - aplicar nas ações de container (lista e detalhe), criação, pull de imagem, remoção de volume e de rede, ações de pod e terminal, com o texto visível e não só cor (FR-006);
  - num lote misto, só os itens fora da janela são recusados, cada um com o motivo;
  - testes `.spec.ts` das features afetadas.
- [x] T028 [US2] E2E com respostas simuladas em `frontend/tests/e2e/mock_ui/test_026_change_windows.py`: botão com o aviso antes do clique, recusa `409` mostrada com a próxima janela e lote misto com recusa por item (SC-004).

**Checkpoint**: a janela é aplicada de ponta a ponta; MVP da feature junto com a US1.

**Execução da US2 (2026-11-22, IA assistida)**: T021–T028 validadas com a suíte do backend contra PostgreSQL 17.6 real, os testes do frontend (212), o build de produção do Angular e a E2E simulada completa pela borda nginx (128 aprovados, incluindo os 3 da História 2 em `test_026_change_windows.py`). Diferenças em relação ao texto das tarefas, para a revisão humana:

- O repositório não tem teste HTTP em processo; a T022 monta os seis serviços reais de ação sobre PostgreSQL real e confere a resposta `409` pelo `ChangeWindowProblems`, o mesmo usado pelas rotas. A L4 é coberta pelo prazo do cliente dentro da janela com o relógio do servidor fora dela: o guard só usa o `TimeProvider` da API.
- "Leituras respondem `200`" (SC-003) e "a licença restrita não desliga a recusa" (FR-007) viraram regras de arquitetura: só os sete casos de uso que mudam o host dependem do guard, e o guard não depende do gate da licença.
- A T023 ficou em `Dokpod.ControlPlane.Application.Tests/Terminal/TerminalSessionServiceTests.cs`, ao lado dos demais testes do terminal; o hub não mudou, porque a recusa já chega como o erro do terminal com o motivo `change_window_closed`.
- O estado da janela por host fica em `frontend/web/src/app/core/change-window.ts`, porque o serviço de ações de container (em `core`) também o usa; o componente do aviso fica em `features/change-window/`. O diretório de ambientes publica o estado, e um único temporizador troca a tela em `closesAt` ou `nextOpensAt`.
- Fora da janela, criar, baixar imagem, remover volume e rede e as ações de pod aparecem como inelegíveis com o motivo e a próxima abertura, no padrão de "host desconectado"; o detalhe do container desabilita as ações e o terminal com o aviso visível.
- O bundle inicial do Angular passou de 422,2 kB (já acima do aviso de 420 kB no `main`) para 423,5 kB; o limite de erro é 500 kB.

---

## Fase 5: História de Usuário 3 - Exceção de emergência (Prioridade: P2)

**Objetivo**: quem tem `change:override` no host executa a ação fora da janela com justificativa, uma ação por vez, com auditoria destacada.

**Teste independente**: com e sem o scope, pedir a exceção fora da janela e conferir resultado, auditoria na mesma transação e o pedido de nova exceção na ação seguinte.

### Testes da História 3

- [x] T029 [P] [US3] Testes de unidade no `ChangeWindowGuardTests.cs`: exceção sem justificativa dá `justification_required`; justificativa com menos de 10 ou mais de 500 caracteres é recusada; scope negado dá `change_override_denied`; Keycloak indisponível dá `authorization_unavailable`; dentro da janela, o pedido de exceção é ignorado sem chamada ao Keycloak.
- [x] T030 [P] [US3] Testes de integração em `backend/tests/Dokpod.ControlPlane.Api.Tests/ChangeWindows/ChangeWindowOverrideTests.cs`, com PostgreSQL real:
  - exceção aceita grava o evento da ação e `change_window.override` com o mesmo `commandId`, autor e justificativa mascarada, na mesma transação (falha simulada no `INSERT` do comando não deixa nenhum dos dois);
  - repetição idempotente com o mesmo `commandId` não duplica o `change_window.override`;
  - a ação seguinte, sem novo pedido, dá `409`;
  - no estágio com a política de justificativa da 016, o mesmo texto atende às duas regras e fica em `action_justifications`;
  - com licença restrita, a exceção continua funcionando.

### Implementação da História 3

- [x] T031 [US3] Pedido de exceção nas seis ações e no terminal: ler `changeWindowOverride` e `justification` dos corpos e de `OpenTerminalRequest`, repassar ao guard como `ChangeWindowOverrideRequest`, e responder `403 change_override_denied` e `422 justification_required` nas rotas da T025.
- [x] T032 [US3] `change:override` na leitura de grants (`IEnvironmentGrantReader` e `EnvironmentGrants.Restrict`) para o console saber quando oferecer a exceção, sem substituir a decisão da API.
- [x] T033 [US3] Confirmação da exceção no console: no componente da T027, para quem tem `change:override` no host, um diálogo "Executar fora da janela (exceção de emergência)" com justificativa obrigatória de 10 a 500 caracteres; num lote, uma justificativa enviada em cada item, e os hosts sem o scope mostrados como recusados. Testes `.spec.ts`.
- [x] T034 [US3] E2E com respostas simuladas em `frontend/tests/e2e/mock_ui/test_026_change_windows.py`: a exceção não aparece sem o scope; com o scope, a ação é executada e a ação seguinte pede nova exceção; lote com hosts com e sem o scope.

**Checkpoint**: as três histórias funcionam de forma independente.

**Execução da US3 (2026-11-22, IA assistida)**: T029–T034 validadas com a suíte do backend contra PostgreSQL 17.6 real, os testes do frontend (214), o build de produção do Angular e a E2E simulada completa (131 aprovados, incluindo os 3 da História 3, um deles na remoção de volume). Diferenças em relação ao texto das tarefas, para a revisão humana:

- **T033**: a exceção aparece nas ações de container (lista e detalhe), no terminal, na remoção de volume e de rede, nas ações de pod, no download de imagem e na criação de container. Decisão do solicitante (2026-11-22, "Siga com a recomendação 1"): completar todas as telas nesta história, antes do PR. A opção é o componente compartilhado `features/change-window/change-window-override.component.ts`.
- A exceção aparece no drawer de confirmação como "Executar fora da janela (exceção de emergência)", só quando algum host fora da janela tem `change:override` (leitura de grants). Marcada, a justificativa fica obrigatória, os hosts sem o scope aparecem como ignorados com o motivo, e cada item do lote leva o pedido e a mesma justificativa. A exceção nunca fica marcada na abertura seguinte do drawer.
- A falha simulada no `INSERT` do comando (T030) usa a chave estrangeira de `agent_commands`: o comando aponta para um host que só o cadastro em memória conhece, e nem o evento da ação nem o `change_window.override` ficam gravados.
- "Com licença restrita, a exceção continua" é coberto pela regra de arquitetura da US2: o guard não depende do gate da licença.
- Na lista de ignorados do drawer, o motivo passou a quebrar a linha; antes ele rolava na horizontal, e o axe acusava `scrollable-region-focusable` com o motivo longo da exceção.

---

## Fase Final: Refinamento e Aspectos Transversais

- [x] T035 [P] Medição de desempenho em `backend/tests/Dokpod.ControlPlane.Api.Tests/ChangeWindows/ChangeWindowGuardImpactTests.cs`: p95 de 30 execuções de uma ação com e sem janela configurada, falhando acima de 5 ms de diferença ([D-011](research.md#d-011-desempenho)); fora do CI, com a variável `DOKPOD_TEST_026_IMPACT=1`, como a SC-004 da 027. Nota (2026-11-22): 0,16 ms depois de a busca do `commandId` passar a acontecer só com a janela fechada (antes, 10,7 ms); ver D-001 e D-011.
- [x] T036 Documentação:
  - `docs/seguranca.md`: seção da fronteira nova com a tabela de controles, apontando para o threat model;
  - `docs/configuracao-keycloak.md`: scope `change:override` e como concedê-lo por ambiente;
  - `docs/runbooks/janelas-de-mudanca.md`: configurar, exceção de emergência, licença restrita, tzdata da imagem e troca de edição para a CE;
  - `docs/licenciamento.md`: comportamento de `change-windows` com a licença restrita;
  - `specs/README.md`: estado da 026.
- [x] T037 Revisão de segurança contra o T001 (ordem das verificações, falha fechada, ausência de segredo e de justificativa sem máscara em logs, métricas e traces), registrada em `specs/026-janelas-de-mudanca/threat-model.md`. Nota (2026-11-22): revisão feita por IA assistida, com os achados A1 e A2 para a decisão do revisor humano e o A3 corrigido (rótulos na auditoria do console).
- [ ] T038 Executar os cinco cenários de [quickstart.md](quickstart.md) com agentes reais e registrar as evidências em `specs/026-janelas-de-mudanca/validation.md`, com `NOT RUN` explícito no que faltar. Nota (2026-11-22): `validation.md` registra a cobertura automatizada de cada cenário; a parte com agentes reais está NOT RUN, aguardando o solicitante decidir entre a stack E2E local e o quality com a `v0.12.0-rc.N`.

---

## Dependências e Ordem de Execução

- **Fase 1** (T001, T002): sem dependências; T001 bloqueia todo código.
- **Fase 2** (T003–T015): depende dos três gates (spec aprovada, itens de revisão do plano decididos e T001 aceito). T003, T004, T009, T010 e T012 podem andar em paralelo; T005 depende de T003 e T004; T007 depende de T005 e T006; T008 depende de T007; T013 depende de T011; T014 depende de T013; T015 pode andar junto com T011–T014.
- **US1** (T016–T020): depende da Fase 2.
- **US2** (T021–T028): depende da Fase 2; não depende da US1 (os testes gravam a janela pelo store).
- **US3** (T029–T034): depende da US2 (T025 e T027).
- **Fase Final**: depende das histórias desejadas.

### Dentro de cada história

Testes primeiro, e falhando antes da implementação; serviço antes das rotas; rotas antes da tela; E2E por último.

## Exemplos de Execução Paralela

```text
Fase 2:  T003 (modelo) | T004 (testes do calendário) | T009 (arquitetura) | T010 (categorias) | T012 (teste da migration)
US1:     T016 (testes das rotas) em paralelo com o início de T019 (tela sobre o cliente gerado)
US2:     T021 (guard) | T022 (integração) | T023 (terminal)
US3:     T029 (unidade) | T030 (integração)
Final:   T035 (desempenho) | T036 (documentação)
```

## Estratégia de Implementação

### MVP primeiro (US1 + US2)

1. Fase 1 e Fase 2 (threat model aceito, guard obrigatório e persistência).
2. US1 (configurar) e US2 (aplicar). Validar os cenários 1, 2, 4 e 5 do quickstart.
3. Parar e validar: sem a US3, uma emergência fora da janela exige remover a janela, o que é auditado.

### Entrega incremental

1. Fundação → US1 → US2 (MVP) → US3 (exceção) → Fase Final.
2. Cada história é integrada num PR próprio, com CI verde, sem quebrar as anteriores.
