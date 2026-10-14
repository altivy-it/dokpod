# Plano de Implementação: Detalhe do Container, Inspeção Sanitizada e Logs Sob Demanda

**Branch**: `007-console-operacional` | **Data**: 2026-10-12 | **Spec**: [spec.md](spec.md)

**Estado**: `implemented` - aprovada em 2026-10-13 ([specs/README.md](../README.md)); revisão humana de segurança antes de release; evidências e itens `NOT RUN` em [tasks.md](tasks.md).

## Resumo

Introduzir no protocolo do agente um mecanismo de **consulta sob demanda** (request/response correlacionado) e de **fluxo de logs** limitado, ambos sem persistência. A API expõe endpoints REST para detalhe, resumo, inspeção e trecho de logs, e um método de streaming no hub SignalR para logs ao vivo. O agente sanitiza e mascara antes de enviar. O frontend cria a feature `container-detail` com abas.

## Contexto Técnico

**Linguagem/Versão**: .NET 10, Protobuf v1, OpenAPI 3.1, SignalR (streaming de servidor), Angular 22.

**Dependências Principais**: Existentes.

**Armazenamento**: Nenhum para conteúdo; auditoria existente para metadados (`container.inspected`, `container.logs.read`, `container.logs.stream_started|ended`), com colunas `subject_id` e `details` adicionadas por migration expand (D-006).

**Testes**: Agente (sanitização por allowlist/denylist, mascaramento, demultiplexação de stream Docker, ANSI, limites), aplicação (correlação, timeout, autorização), API (contratos), SignalR (encerramento), canários de segredo end-to-end, Docker/Podman reais, Playwright.

**Objetivos de Desempenho**: SC-001/SC-002.

**Restrições**: Sem conteúdo persistido; sanitização no agente com mascaramento universal de texto; limites; sem proxy genérico (consultas são tipos fechados); nenhuma mensagem gRPC de consulta/log acima de 256 KiB (limite do servidor: 1 MiB); comandos com prioridade sobre consultas no agente.

## Verificação da Constituição

| Item | Situação | Observação |
| --- | --- | --- |
| III. "telemetria não inclui ... variáveis de ambiente ou logs integrais" | OK com revisão | Logs são exibidos sob demanda ao usuário autorizado, não coletados como telemetria; conteúdo nunca persiste nem entra em logs da plataforma. Requer atualização aprovada de `docs/seguranca.md` ("logs integrais não são coletados no MVP"). |
| Sem proxy genérico | OK | Mensagens tipadas (`INSPECT`, `CONFIG_SUMMARY`, `LOGS`); nenhum repasse de rota/payload arbitrário ao engine. |
| Operações privilegiadas / novas fronteiras exigem threat model e testes negativos | Pendente | Threat model e lista de testes negativos em `docs/seguranca.md` (T002); suíte negativa implementada em T021. |
| Schemas expand-contract | OK | Colunas novas e anuláveis em `audit_events` e sobrecarga da função de append; rollback remove somente o que foi adicionado. |
| Compatibilidade | OK | Mensagens/tags novas e capabilities 10/11; negociação do README testada nos dois sentidos. |

## Estrutura do Projeto

```text
contracts/agent/v1/agent.proto            # ContainerQueryRequest(14)/Response(19), LogStreamControl(15)/LogChunk(20)
contracts/openapi/dokpod-control-plane.v1.yaml
backend/libs/Dokpod.Agent.Application/Queries/      # ContainerQueryHandler, InspectSanitizer, ConfigSummaryBuilder
backend/libs/Dokpod.Agent.Application/Logs/         # LogReader, LogLineParser, SecretMasker, LogStreamManager
backend/libs/Dokpod.Agent.Infrastructure/Engines/{Docker,Podman}/  # /containers/{id}/json, /logs (multiplexado)
backend/libs/Dokpod.ControlPlane.Application/Queries/  # AgentQueryDispatcher (correlação + timeout), ContainerDetailService
backend/libs/Dokpod.ControlPlane.Application/Logs/     # LogSessionService (limites, expiração, revogação)
backend/apps/Dokpod.ControlPlane.Api/Containers/ContainerDetailEndpoints.cs
backend/apps/Dokpod.ControlPlane.Api/Realtime/ControlPlaneHub.cs  # StreamContainerLogs (IAsyncEnumerable)
backend/apps/Dokpod.Bff/                                          # garantir relay de streaming/WebSocket do hub
frontend/web/src/app/features/container-detail/
├── container-detail.page.*  container-detail.facade.ts
├── summary.tab.*  logs.tab.*  inspect.tab.*  stats.tab.*  terminal.tab.* (placeholder até 014)
└── resources.card.*  network-storage.card.*
```

## Fases

1. Threat model e revisão de segurança (bloqueante). 2. Contratos. 3. Agente (consulta, sanitização, logs, mascaramento). 4. Plano de controle (despachante de consultas, sessões de logs, auditoria). 5. API REST + hub + BFF. 6. Frontend. 7. Canários e E2E.

## Riscos

| Risco | Mitigação |
| --- | --- |
| Vazamento de segredo em logs | Mascaramento no agente + canários automatizados + aviso na UI de que mascaramento é best-effort. |
| Consulta concorrente sobrecarrega agente/canal | 4 consultas por agente, 10 sessões por agente e 3 por usuário, 256 KiB/s por sessão, timeout, fila de prioridade no agente (hoje fila única). |
| Mensagem acima do limite gRPC derruba o `Connect` | Partes de até 256 KiB (`sequence`/`final`); teste com resposta máxima (SC-006). |
| Termo de busca vazado em logs de acesso | Redação de `contains` no nginx, BFF e API; canários no T020. |
| Contagem de reinícios incorreta | Validação em Docker e Podman reais (D-005). |
| BFF e streaming | O BFF já faz proxy WebSocket (`DownstreamProxy.ProxyWebSocketAsync`); validar streaming SignalR de ponta a ponta pela borda nginx. |
