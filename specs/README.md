# Migração Documental para Spec Kit

**Data**: 2026-10-06 | **Branch da migração**: `chore/adotar-spec-kit`

**Estado**: `draft` - seis conjuntos em rascunho, aguardando revisão humana.

**Origem**: IA assistida, migração documental autorizada pelo solicitante nesta sessão.

**Revisor humano**: A confirmar; autorização documental não é aprovação dos artefatos nem conclusão técnica.

## Conjuntos e Autoridade

| Conjunto | Requisitos | Desenho | Tarefas |
| --- | --- | --- | --- |
| 001 - Identidade, ambientes e auditoria | [spec](001-identidade-ambientes-auditoria/spec.md) | [plan](001-identidade-ambientes-auditoria/plan.md) | [tasks](001-identidade-ambientes-auditoria/tasks.md) |
| 002 - Sessão BFF, relay e realtime | [spec](002-sessao-bff-relay-realtime/spec.md) | [plan](002-sessao-bff-relay-realtime/plan.md) | [tasks](002-sessao-bff-relay-realtime/tasks.md) |
| 003 - Inventário, lifecycle e reconciliação | [spec](003-inventario-lifecycle-reconciliacao/spec.md) | [plan](003-inventario-lifecycle-reconciliacao/plan.md) | [tasks](003-inventario-lifecycle-reconciliacao/tasks.md) |
| 004 - Distribuição containerizada | [spec](004-distribuicao-containerizada/spec.md) | [plan](004-distribuicao-containerizada/plan.md) | [tasks](004-distribuicao-containerizada/tasks.md) |
| 005 - Qualificação de release Docker Linux | [spec](005-qualificacao-release-docker-linux/spec.md) | [plan](005-qualificacao-release-docker-linux/plan.md) | [tasks](005-qualificacao-release-docker-linux/tasks.md) |
| 006 - Qualificação Windows e Podman | [spec](006-qualificacao-windows-podman/spec.md) | [plan](006-qualificacao-windows-podman/plan.md) | [tasks](006-qualificacao-windows-podman/tasks.md) |

Convenções documentais dos templates e rascunhos do AltivyNotes: jornadas
priorizadas com teste independente e Dado/Quando/Então; FR/SC identificados;
contexto técnico, verificação da constituição pendente, estrutura, rastreabilidade;
tarefas `- [ ] Tnnn [USn]` com caminhos e dependências. IDs T são locais ao conjunto.
Os rascunhos foram convertidos documentalmente, sem execução de `/speckit-analyze`
ou certificação técnica. Spec Kit 1.0.13 e Bug Fixing 1.0.0 foram instalados
oficialmente; resolver e pré-requisitos têm validação estrutural separada.

[README](../README.md), [arquitetura](../docs/arquitetura.md) e
[segurança](../docs/seguranca.md) restringem requisitos/desenho. ADRs registram
decisões, não são substituídos por tarefas nem têm status alterado aqui. A
[avaliação de release](../docs/release-readiness.md) permanece **NO-GO**.

## Cobertura Integral das Etapas

Cada linha identifica uma etapa original e seu proprietário principal; recortes
complementares são explícitos para não duplicar trabalho. O plano de cada conjunto
detalha o tratamento. Nenhuma etapa/entrega histórica foi convertida em `[x]`.

| Origem legada | Status histórico da etapa | Destino principal e tarefas | Recorte complementar |
| --- | --- | --- | --- |
| mvp:P-01 | in-progress | [003 T002/T004/T005](003-inventario-lifecycle-reconciliacao/tasks.md) | [004 T003/T005](004-distribuicao-containerizada/tasks.md): imagem/smoke; [006 T002/T003](006-qualificacao-windows-podman/tasks.md): Windows |
| mvp:P-02 | in-progress | [004 T002/T003/T006](004-distribuicao-containerizada/tasks.md) | [003 T002/T005](003-inventario-lifecycle-reconciliacao/tasks.md): contratos; [001 T002/T007](001-identidade-ambientes-auditoria/tasks.md): persistência/API; [006 T003](006-qualificacao-windows-podman/tasks.md): publish |
| mvp:P-03 | in-progress | [001 T003-T007](001-identidade-ambientes-auditoria/tasks.md) | [002 T003/T005/T007](002-sessao-bff-relay-realtime/tasks.md): sessão/CSRF/hub |
| mvp:P-04 | in-progress | [003 T003](003-inventario-lifecycle-reconciliacao/tasks.md) | [005 T005/T006](005-qualificacao-release-docker-linux/tasks.md): carga e recuperação |
| mvp:P-05 | in-progress | [003 T004/T006/T007](003-inventario-lifecycle-reconciliacao/tasks.md) | [001 T005/T006](001-identidade-ambientes-auditoria/tasks.md): auditoria/retenção; [006 T004](006-qualificacao-windows-podman/tasks.md): journal Windows; [005 T006](005-qualificacao-release-docker-linux/tasks.md): recuperação integrada |
| mvp:P-06 | in-progress | [005 T003-T009](005-qualificacao-release-docker-linux/tasks.md) | [004 T006](004-distribuicao-containerizada/tasks.md): inspeções; [006 T003/T005/T006](006-qualificacao-windows-podman/tasks.md): matriz/pacote |
| mvp:P-07 | not-started | [006 T003-T006](006-qualificacao-windows-podman/tasks.md) | [005 T008/T009](005-qualificacao-release-docker-linux/tasks.md): decisão matriz/adiamento |
| p03:P03-01 | completed, aprovação pendente na evidência | [001 T002](001-identidade-ambientes-auditoria/tasks.md) | Decisão ADR permanece humana; nenhum status alterado |
| p03:P03-02 | in-progress | [001 T002/T005](001-identidade-ambientes-auditoria/tasks.md) | Infraestrutura existente, não recriação |
| p03:P03-03 | completed | [001 T005/T006](001-identidade-ambientes-auditoria/tasks.md) | Encerramento humano narrado; residual operacional distinto |
| p03:P03-04 | in-progress, encerramento narrado | [001 T003/T005](001-identidade-ambientes-auditoria/tasks.md) | Preservar divergência status/evidência |
| p03:P03-05 | in-progress | [001 T003/T004](001-identidade-ambientes-auditoria/tasks.md) | Cadastro/identidade, não política Keycloak local |
| p03:P03-06 | in-progress | [001 T007](001-identidade-ambientes-auditoria/tasks.md) | Contrato, readiness, métricas e cliente existentes |
| p03:P03-07 | not-started | [001 T004-T007](001-identidade-ambientes-auditoria/tasks.md) | [002 T003/T005/T007](002-sessao-bff-relay-realtime/tasks.md): sessão; [005 T005-T008](005-qualificacao-release-docker-linux/tasks.md): carga/recuperação |
| p04:P04-01 | não individualizado | [004 T003](004-distribuicao-containerizada/tasks.md) | BFF Dockerfile já registrado |
| p04:P04-02 | não individualizado | [004 T003](004-distribuicao-containerizada/tasks.md) | Web/API/agente Dockerfiles já registrados |
| p04:P04-03 | não individualizado | [004 T004](004-distribuicao-containerizada/tasks.md) | Compose/rede central, não stack nova |
| p04:P04-04 | não individualizado | [004 T005](004-distribuicao-containerizada/tasks.md) | [002 T007](002-sessao-bff-relay-realtime/tasks.md): jornada pública |
| p04:P04-05 | não individualizado | [002 T004/T005](002-sessao-bff-relay-realtime/tasks.md) | Mesmo relay de P05-03/P05-04 |
| p04:P04-06 | não individualizado | [002 T006](002-sessao-bff-relay-realtime/tasks.md) | Mesma integração de P05-06 |
| p04:P04-07 | não individualizado | [002 T003-T007](002-sessao-bff-relay-realtime/tasks.md) | E2E compartilhado, sem segunda suíte |
| p05:P05-01 | in-progress | [002 T002/T008](002-sessao-bff-relay-realtime/tasks.md) | Contrato/threat model; aprovação histórica não herdada |
| p05:P05-02 | in-progress | [002 T003](002-sessao-bff-relay-realtime/tasks.md) | Fundação existente |
| p05:P05-03 | in-progress | [002 T004](002-sessao-bff-relay-realtime/tasks.md) | Relay existente |
| p05:P05-04 | in-progress | [002 T005](002-sessao-bff-relay-realtime/tasks.md) | Hub/relay existentes e fallback REST |
| p05:P05-05 | in-progress | [002 T004/T005](002-sessao-bff-relay-realtime/tasks.md) | [001 T003/T007](001-identidade-ambientes-auditoria/tasks.md): identidade/auditoria; [003 T003-T007](003-inventario-lifecycle-reconciliacao/tasks.md): inventário/comandos |
| p05:P05-06 | in-progress | [002 T006](002-sessao-bff-relay-realtime/tasks.md) | Angular/antiforgery sem token |
| p05:P05-07 | not-started | [004 T004/T005](004-distribuicao-containerizada/tasks.md) | [002 T007](002-sessao-bff-relay-realtime/tasks.md): ausência de bypass/leakage |
| p05:P05-08 | not-started | [002 T003-T007](002-sessao-bff-relay-realtime/tasks.md) | E2E/indisponibilidade/reinício e inspeção sanitizada |
| p05:P05-09 | not-started | [002 T008](002-sessao-bff-relay-realtime/tasks.md) | [005 T007-T009](005-qualificacao-release-docker-linux/tasks.md): gate global/decisão |
| desbloqueio:P-01 | not-started | [005 T003](005-qualificacao-release-docker-linux/tasks.md) | CI remoto, não configuração como PASS |
| desbloqueio:P-02 | not-started | [005 T004](005-qualificacao-release-docker-linux/tasks.md) | HIGH, correção/aceite formal e novo scan |
| desbloqueio:P-03 | not-started | [005 T005/T006](005-qualificacao-release-docker-linux/tasks.md) | Margem da arquitetura/release-readiness preservada |
| desbloqueio:P-04 | not-started | [005 T007](005-qualificacao-release-docker-linux/tasks.md) | Threat model e supply chain; assinatura exige autorização |
| desbloqueio:P-05 | not-started | [006 T003-T006](006-qualificacao-windows-podman/tasks.md) | [005 T008](005-qualificacao-release-docker-linux/tasks.md): Docker Linux e matriz inicial |
| desbloqueio:P-06 | not-started | [005 T008/T009](005-qualificacao-release-docker-linux/tasks.md) | Decisão humana GO/NO-GO, não publicação |

Fontes congeladas: [MVP](../docs/plan/mvp.md),
[P03](../docs/plan/p03-auditoria-persistente.md),
[P04](../docs/plan/p04-padronizacao-dockerfiles-compose.md),
[P05](../docs/plan/p05-bff-keycloak-relay.md) e
[desbloqueio](../docs/plan/desbloqueio-release.md).
Cobertura: 7 + 7 + 7 + 9 + 6 = **36 etapas**, sem etapa órfã.

## Evidências, Inconsistências e Gaps

- P03-01 `completed` coexistindo com ADR `proposed`/aprovação pendente; P03-04 `in-progress` com encerramento humano narrado. Não arbitrar nem mudar ADR.
- P04 não individualiza status; builds/stack saudáveis coexistem com startup pendente e falhas históricas. Rede `identity-client` no P03 versus `identity-global` no P04 requer contexto/revisão, sem alterar Compose.
- P05-07/P05-08 `not-started` e pendências de relay/horizontal coexistem com evidências posteriores do MVP; P-04 ainda cita horizontal pendente e P-05 registra aprovação. Screenshots constam verificados e pendentes. Não concluir nem reabrir implementação automaticamente.
- Contagens de testes/partições variam por data. Evidências de outra branch/commit e laboratório de 16 containers não certificam este checkout ou produção; não executar a aplicação para conciliá-las nesta migração.
- Enrollment oficial/rotação/bootstrap concorrente, retenção/replay e cenários negativos de sessão/dependências precisam avaliação de diferença e validação residual.
- CI remoto, triagem dos 34 HIGH históricos, nominal 56/1.120/30 por >=30min, margem >=100/2.000/50, N/N-1, observabilidade, threat model e assinatura/provenance continuam gates explícitos de 005.
- Windows console/DryRun não substituem host limpo sem runtime, serviço/ACL/crash durability/update/rollback. Podman rootless/rootful/Libpod precisa engine real; nenhuma equivalência presumida.
- Customizações `.github/` e `.specify/` foram migradas nesta entrega para o fluxo exclusivo Spec Kit. ADRs históricos e o relatório de release-readiness não foram alterados; campos e administração remotos não foram certificados. Este índice não equivale à análise ou aprovação dos seis drafts.

## Regras de Continuidade

Revisão humana dos artefatos -> autorização da tarefa -> avaliação de diferença
-> validação pertinente -> conclusão do residual comprovado. Somente depois de
execução validada uma tarefa pode receber `[x]`; isso não aceita ADR ou autoriza
release. Planos originais ficam congelados, com conteúdo integral preservado e
aviso de que status históricos não autorizam execução. Não apagar evidências.

Qualificação de release deve preservar engine como fonte, journal-before-ack,
fencing/dedup/at-least-once, N/N-1, gRPC mTLS, BFF/Keycloak, Windows self-contained
e os gates HIGH/NO-GO. Adiamento por capability é decisão humana separada, não
conclusão de tarefas ou dispensa implícita de gate obrigatório da matriz declarada.

## Console Operacional (007-014)

**Data**: 2026-10-12 | **Branch**: `007-console-operacional` | **Estado**: autorizado para implementação.

**Aprovação humana (2026-10-13)**: zocatel (solicitante) aprovou o ADR 2026-0005 e autorizou a execução de 007-014 com as recomendações das análises (`/speckit-analyze`): "abandone o POUI e implemente o novo Design" e "Execute tudo baseado no novo design, ele é a fonte da verdade de UI e de funcionalidade de backend". Os mockups de `docs/design/app` prevalecem em caso de divergência de UI. Gates de threat model de 011-014 continuam obrigatórios e são registrados em `docs/seguranca.md` antes da implementação da respectiva fronteira.

**Origem**: mockups do canvas "Gestão de Containers" em [docs/design/app](../docs/design/app/project/canvas.json) e design system Altivy. Decisões do solicitante em 2026-10-12: componentes Angular próprios fiéis ao mockup (substituindo a regra PO UI via [ADR 2026-0005](../docs/adr/2026-0005-design-system-proprio-altivy.md)), escopo completo em várias specs, uma spec por capacidade com backend antes do frontend.

| Ordem | Conjunto | Board / menu | Gates humanos antes de implementar |
| --- | --- | --- | --- |
| 1 | [007 - Fundação do console e design system](007-fundacao-console-design-system/spec.md) | moldura, componentes `Campo`/`Selecao`/`Marcador` | ADR 2026-0005; instruções frontend |
| 2 | [008 - Visão geral, hosts, eventos e inventário enriquecido](008-visao-geral-hosts-eventos/spec.md) | `01 · Visão geral`, Hosts | limites e retenção de eventos |
| 3 | [009 - Métricas de recursos](009-metricas-recursos/spec.md) | KPIs/gráficos de CPU e memória | armazenamento de séries (arquitetura) |
| 4 | [010 - Lista unificada, pausa e lote](010-lista-containers-acoes-lote/spec.md) | `02 · Containers` | limite de lote |
| 5 | [011 - Detalhe, inspeção e logs](011-detalhe-container-logs/spec.md) | `03 · Detalhe` | threat model; política de logs em `docs/seguranca.md` |
| 6 | [012 - Imagens e pull](012-imagens-pull/spec.md) | "Baixar imagem", Imagens | — |
| 7 | [013 - Criação de container](013-criacao-container/spec.md) | `04 · Novo container` | decisão adiada "criação declarativa"; threat model; origem de segredos |
| 8 | [014 - Terminal interativo](014-terminal-container/spec.md) | aba Terminal | ADR 2026-0006; threat model |

Dependências: 007 → 008 → {009, 010, 012} → 011 → 013 → 014 (013 usa 011 para consultas ao host; 014 usa a página e o hub de 011). Itens de menu sem spec (Pods, Volumes, Redes, Registros, Alertas, Auditoria, Configurações) abrem página "Em breve".

### Registro de alocação de contratos

Evita colisão de tags entre specs desenvolvidas em paralelo. Alterar somente com atualização desta tabela.

| Contrato | 008 | 009 | 010 | 011 | 012 | 013 | 014 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `AgentMessage.payload` | 17 `engine_events` | 18 `resource_samples` | — | 19 `container_query_response`, 20 `log_chunk` | 21 `image_snapshot_page`, 22 `image_delta` | — | — |
| `SessionEstablished` | 6 `accepted_capabilities` | — | — | — | — | — | — |
| `ContainerState` | 7-13 (14 não alocada) | — | — | — | 15 `image_id` | — | — |
| `Heartbeat` | — | — | — | — | 3 `image_revision` | — | — |
| `CommandResult` | — | — | — | — | 6 `resolved_image_digest`, 7 `image_size_bytes` | 8 `created_container_id` | — |
| `SnapshotRequest` | — | — | — | — | 2 `scope` | — | — |
| `ControlPlaneMessage.payload` | — | — | — | 14 `container_query`, 15 `log_stream_control` | — | — | 16 `exec_open` |
| `Capability` | 6 `ENGINE_EVENTS` | 7 `RESOURCE_METRICS` | 8 `CONTAINER_PAUSE`, 9 `CONTAINER_UNPAUSE` | 10 `CONTAINER_INSPECT`, 11 `CONTAINER_LOGS` | 12 `IMAGE_INVENTORY`, 13 `IMAGE_PULL` | 14 `CONTAINER_CREATE` | 15 `CONTAINER_EXEC` |
| `CommandKind` | — | — | 5 `PAUSE`, 6 `UNPAUSE` | — | 7 `PULL_IMAGE` | 8 `CREATE_CONTAINER` | — |
| `AgentCommand.parameters` | — | — | — | — | 7 `pull_image` | 8 `create_container` | — |
| `ContainerQueryKind` | — | — | — | 1-3 | — | 4-6 | — |
| RPC | — | — | — | — | — | — | `Exec` |
| `EngineEventType` | 1-10 | — | — | — | — | — | 11 `EXEC_ORPHAN_SUSPECTED` |
| Scope Keycloak | — | — | `container:pause` | `container:inspect`, `container:logs` | `image:pull` | `container:create` | `container:exec` |

**Regra de negociação (008 D-009)**: o agente só envia payloads novos de `AgentMessage` (17-22) cujas capabilities o servidor confirmou em `SessionEstablished.accepted_capabilities`; lista ausente = servidor N-1. O servidor só envia payloads novos de `ControlPlaneMessage` (14-16) e `CommandKind` novos a agentes que anunciaram a capability no `Hello`. Cada spec testa agente N contra servidor N-1.

### Rotas do frontend

`/` Visão geral (008) · `/containers` (010) · `/containers/:environmentId/:containerId?aba=` detalhe (011/014) · `/imagens` (012) · `/hosts` (008) · `/em-breve/:item` (007) · `/dev/componentes` somente desenvolvimento (007). A rota legada `/environments/:environmentId/containers` redireciona para `/containers?environmentId=`.
