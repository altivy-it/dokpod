# Tarefas: Detalhe do Container, Inspeção Sanitizada e Logs Sob Demanda

**Entrada**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/contracts-delta.md).

**Estado**: `draft` - aguardando revisão humana e de segurança.

**Pré-requisitos**: Revisão humana; T002 aprovada (threat model); 008 concluída; 010 T011 (lote/ações reutilizáveis).

## Fase 1: Segurança e contratos (bloqueante)

- [ ] T001 Revisão humana da spec, limites, padrões de mascaramento e papéis que recebem `container:inspect`/`container:logs`.
- [ ] T002 Threat model de inspeção e logs sob demanda em `docs/seguranca.md` (atores, abuso de leitura, vazamento por campos não listados e risco residual do mascaramento best-effort, termo de busca em logs de acesso, DoS no agente e no canal gRPC, revogação) com a **lista de testes negativos** implementada em T021; atualização da frase "logs integrais não são coletados"; registrar aprovação.
- [ ] T003 [P] Proto conforme [contracts-delta.md](contracts/contracts-delta.md) (tags 14/15/19/20, capabilities 10/11, `sequence`/`final`/`redacted_paths`, `LogLine.level`, `max_bytes_per_second`) e testes N/N-1 nos dois sentidos (agente N × servidor N-1 e servidor N × agente N-1).
- [ ] T004 [P] OpenAPI: `getContainer`, `getContainerSummary`, `inspectContainer`, `getContainerLogs` (com 429), scopes `container:inspect`/`container:logs`; provisionamento dos scopes no Keycloak (`tools/` ou runbook existente) e inclusão na tabela "Scopes iniciais" de `docs/configuracao-keycloak.md`, com os papéis definidos em T001.

## Fase 2: Agente

- [ ] T005 [P] [US2] `ConfigSummaryBuilder` (allowlist) e `InspectSanitizer` (denylist incluindo `Config.CreateCommand` + marcação `redactedPaths`) em `backend/libs/Dokpod.Agent.Application/Queries/`, ambos seguidos do `SecretMasker` sobre todo valor de texto (labels, opções de log, health check, `Cmd`/`Entrypoint`/`Args`, campos desconhecidos); testes de tabela e canários por campo (D-003).
- [ ] T006 [P] [US4] `SecretMasker`, `LogLineParser` (ANSI, nível, truncamento) e leitor de logs com demultiplexação (Docker e Podman) em `Logs/` e `Engines/`; filtro `contains` literal; testes com engines reais.
- [ ] T007 [US4] `ContainerQueryHandler` e `LogStreamManager` (partes ≤ 256 KiB, 4 consultas simultâneas, taxa de linhas e bytes, expiração) integrados ao `AgentControlWorker.cs`, substituindo a fila única `outbound` por filas de alta e baixa prioridade (D-002); testes de que um comando não espera atrás de logs, de carga leve e de encerramento.

## Fase 3: Plano de controle e API

- [ ] T008 [US1] `AgentQueryDispatcher` (correlação, montagem de partes até o limite, timeout, cancelamento, agente desconectado, limite por agente com 429) em `backend/libs/Dokpod.ControlPlane.Application/Queries/`; testes, incluindo resposta máxima sem derrubar o `Connect`.
- [ ] T009 [US1] `ContainerDetailService` e `getContainer` (reinícios 24 h via `container.auto_restarted` + `command.succeeded` de restart, D-005); autorização sem revelar existência; teste com reinício por política em Docker e Podman reais (se o engine não emitir `restart`, abrir ajuste no agente da 008).
- [ ] T010 [US2] Migration expand de `audit_events` (`subject_id`, `details` com `CHECK` de tamanho) e sobrecarga de `dokpod_append_audit_event`, com teste de rollback (D-006); `getContainerSummary` e `inspectContainer` com `container:inspect` e auditoria `container.inspected`.
- [ ] T011 [US4] `getContainerLogs` com `container:logs`, auditoria `container.logs.read` e garantia de não logar conteúdo nem o termo `contains` (filtro de logging da API, redação do parâmetro no BFF e no `log_format` do nginx).
- [ ] T012 [US4] `LogSessionService` + `StreamContainerLogs` no hub (10 sessões por agente, 3 por usuário, reavaliação de autorização a cada 60 s via `StreamingAuthorizationMonitor` em `Dokpod.ControlPlane.Application/Realtime/` (reutilizado pela 014), encerramento por cancelamento, expiração, logout e expiração da sessão do BFF); verificar/ajustar relay do BFF para streaming; testes.

## Fase 4: Frontend

- [ ] T013 [US1] `features/container-detail/container-detail.page.*` + fachada: cabeçalho conforme `Detalhe.dc.html`, ações reutilizando lote/confirmação da 010, "Abrir terminal" desabilitado com "em breve" até 014, aba na URL, container removido, estados.
- [ ] T014 [P] [US2] Aba Resumo e card "Rede e armazenamento" (`dok-key-value`), "sem permissão".
- [ ] T015 [P] [US3] Aba Inspecionar (JSON somente texto, copiar, truncado, caminhos ocultos).
- [ ] T016 [US4] Aba Logs: busca (literal, enviada ao agente), filtro de nível (somente linhas carregadas), "Ao vivo" com indicador pulsante, buffer limitado a 10.000 linhas/5 MiB com aviso de descarte, rolagem que respeita posição do usuário, "Baixar" do buffer, linhas omitidas, aviso de mascaramento best-effort, mensagem de 429; nunca `innerHTML`.
- [ ] T017 [P] [US5] Card "Recursos agora" e aba Estatísticas com dados da 009 ou "indisponível"; aba Terminal "em breve" até 014.
- [ ] T018 Playwright em `frontend/tests/e2e/`: abas, logs ao vivo e encerramento (saída da aba e logout em ≤ 5 s, revogação em ≤ 60 s — SC-005), permissões parciais, agente desconectado; medir SC-001 (500 linhas em ≤ 2 s) e SC-002 (latência ao vivo ≤ 2 s); acessibilidade.
- [ ] T019 Loop visual desktop/mobile de todas as abas e estados; comparar com `Detalhe.dc.html`.

## Fase 5: Verificação de segurança

- [ ] T020 Teste end-to-end com canários (SC-003/SC-004) por campo (variável, label, health check, opção de log, `CreateCommand` no Podman, termo de busca) varrendo respostas, PostgreSQL, logs de nginx/API/BFF/agente e telemetria; evidência registrada.
- [ ] T021 Suíte de testes negativos de T002 na API, no hub e no agente: container de ambiente não autorizado (404 indistinguível), scope ausente em cada endpoint e no hub, revogação durante sessão ao vivo, limites de sessões/consultas (429), `contains` com metacaracteres e tamanho excedido, `tail` acima do máximo, consulta a agente sem capability, `ContainerQueryKind` desconhecido recusado pelo agente.
- [ ] T022 Carga do canal (SC-006): resposta de logs máxima + 10 sessões ao vivo na taxa máxima com comando concorrente; agente permanece conectado e o comando não atrasa mais de 2 s; evidência registrada.

## Dependências e Ordem de Execução

T001 -> T002 (gate) -> T003/T004 -> T005-T007 -> T008-T012 -> T013-T019 -> T020-T022.
