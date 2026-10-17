# Tarefas: Terminal Interativo no Container

**Entrada**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/contracts-delta.md).

**Estado**: `implemented` - concluída em 2026-10-14; ADR 2026-0006 `accepted` em 2026-10-14; revisão humana de segurança pendente antes de release. `NOT RUN`: Docker real, terminal real pelo navegador (Playwright com Keycloak/BFF/hub; o mock não tem SignalR), eco ponta a ponta no navegador (SC-001 medido no agente com Podman real), carga com 4 sessões (SC-004) e varredura de canários nos logs de processo da stack real.

**Pré-requisitos**: T001-T003 aprovadas; 011 concluída (página de detalhe, hub, BFF).

## Fase 1: Governança e segurança (bloqueante)

- [x] T001 Criar `docs/adr/2026-0006-terminal-interativo.md` (`proposed`): fronteira de exec, canal separado, sem gravação de conteúdo, exceção ao modelo de comando durável (D-006), dependência xterm.js (licença MIT compatível com AGPL-3.0-only verificada, versão fixada), revisor humano. — `docs/adr/2026-0006-terminal-interativo.md` (`proposed`): fronteira, canal separado, sem gravação, exceção ao princípio IV, xterm.js 6.0.0/addon-fit 0.11.0 (MIT, versões exatas, registrado em `docs/licenciamento.md`). Aprovado pelo revisor humano em 2026-10-14 (`accepted`).
- [x] T002 Threat model do terminal em `docs/seguranca.md` (abuso de shell, escalada, sessão órfã e processos em segundo plano como risco residual, sequências maliciosas, DoS do agente, janela de revogação, justificativa com segredo) e lista de testes negativos; registrar aprovação. — threat model e testes negativos em `docs/seguranca.md`; aprovação de execução em `specs/README.md` (revisão de segurança pendente).
- [x] T003 Confirmar limites padrão, justificativa e MFA (D-005); documentar em `docs/configuracao-keycloak.md` a vida útil curta do access token (≤ 5 min) e a janela de revogação (D-008). — limites padrão em `TerminalOptions`; justificativa só em Produção; MFA opcional (`Terminal__RequireRecentMfa`); token ≤ 5 min e janela de revogação em `docs/configuracao-keycloak.md`.

## Fase 2: Contratos

- [x] T004 [P] Proto: `rpc Exec`, `ExecOpenRequest` (16), `ExecFrame`, capability 15; testes N/N-1 e de vínculo fencing. — `rpc Exec`, `ExecOpenRequest` (16), `ExecFrame`, capability 15 e `EXEC_ORPHAN_SUSPECTED` (registrado em `specs/README.md`); vínculo de fencing testado em `TransportBoundaryTests.Terminal_FlowsThroughHubServiceExecRpcAndAgentAndRejectsStaleFencing`.
- [x] T005 [P] OpenAPI `getTerminalPolicy` e scope `container:exec`; provisionar scope no Keycloak. — `getTerminalPolicy` e `renewTerminalSession` no OpenAPI (contrato testado); scope `container:exec` já provisionado (010); cliente regenerado.

## Fase 3: Agente (US1, US3)

- [x] T006 [US1] `ExecClient` Docker/Podman (create com `Cmd` fixo e `Tty`, fallback `/bin/bash` → `/bin/sh` por erro de executável (D-003), start hijacked, resize, término por fechamento do TTY e verificação `Running=false` em até 5 s) com testes reais. — `StartExecAsync` com conexão sequestrada (`EngineRawConnector`), resize, inspeção e `SignalContainerProcessAsync`; revisão de D-003 documentada (o engine não encerra exec ao fechar a conexão); `ExecPodmanTests` em Podman real.
- [x] T007 [US1] `ExecSessionManager` + `AgentExecClient`: abrir RPC `Exec` após `ExecOpenRequest`, handshake com sessão/fencing, limites de taxa e de sessões, término em perda de stream/container parado, deduplicação por `exec_session_id` e recusa após `expires_at`, persistência de execs ativos em `<data>/exec-sessions.json` e verificação de órfãos após reinício (`orphan_suspected`); nunca anunciar `CAPABILITY_CONTAINER_EXEC` no agente Windows; testes. — `ExecSessionManager` + `GrpcExecChannel` + `FileActiveExecStore` (handshake, at-most-once, `expires_at`, limite por agente, label `dokpod.exec=disabled`, taxa de saída, término confirmado em ≤ 5 s, órfãos após reinício com `EXEC_ORPHAN_SUSPECTED`); capability só fora do Windows; `ExecSessionPodmanTests` (motivos `user_closed`, `exit`, `client_disconnected`, `container_stopped`; SC-001 no agente: abertura < 3 s e eco < 150 ms).

## Fase 4: API (US1-US3)

- [x] T008 [US2] `TerminalPolicy` e `getTerminalPolicy` (scope, label, estágio, MFA, limites, container em execução, capability). — `TerminalSessionService.EvaluatePolicyAsync` + `GET terminal-policy` (scope, estágio, MFA, container em execução, capability, conexão).
- [x] T009 [US1] `AgentExecService` (servidor do RPC `Exec`) + `ExecSessionRegistry` (pendentes/ativas, validação de handshake). — `AgentControlService.Exec` (certificado → identidade, sessão ativa e fencing, sessão pendente) + `TerminalSessionRegistry`. Correção da revisão de 2026-10-14: quando a sessão termina no plano de controle, o RPC `Exec` é encerrado para o agente derrubar o shell mesmo sem `Closed` (antes, a queda do navegador durante a abertura deixava o shell vivo até a duração máxima); regressão em `TransportBoundaryTests.Terminal_FlowsThroughHubServiceExecRpcAndAgentAndRejectsStaleFencing`.
- [x] T010 [US2] `TerminalSessionService` + `OpenTerminal` no hub: justificativa, auditoria `requested/started/ended/denied`, ociosidade, duração, aviso de 1 min, reavaliação de autorização a cada 60 s via `StreamingAuthorizationMonitor` compartilhado com a 011 (D-008), término em logout/revogação; justificativa mascarada e exceção `justification` na allowlist de `details` (D-009); exigir WebSocket (`websocket_required`); keepalive 15 s/timeout 30 s (D-010); testes. — `TerminalSessionService.RunAsync` + `OpenTerminal` no hub (WebSocket obrigatório, justificativa mascarada, auditoria `requested/started/ended/denied` sem conteúdo, ociosidade/duração com aviso de 1 min, reavaliação a cada 60 s com o token renovado via `renew` — revisão de D-008 documentada), keepalive 15/30 s; `TerminalSessionServiceTests` (16 testes).

## Fase 5: Frontend (US1)

- [x] T011 [US1] Adicionar xterm.js (versão fixada, após aprovação) e `terminal.tab.*` + `terminal.facade.ts` em `features/container-detail/`, conforme a aba Terminal de `Detalhe.dc.html` (painel escuro monoespaçado, mínimo 360 px, sessão abre ao ativar a aba, sem confirmação): estados conectando/conectado/encerrado, diálogo de justificativa em Produção, alerta de usuário root, colar multi-linha com confirmação, atalho para sair do foco, modo leitor de tela, configuração restritiva (sem OSC 52/links). — `terminal.tab.*` + `terminal.facade.ts` com xterm.js (painel escuro ≥ 360 px, abre ao ativar a aba, justificativa em Produção, alerta de root, colar multi-linha com confirmação, Ctrl+] sai do foco, modo leitor de tela, sem OSC 52/links); Vitest.
- [x] T012 [US1] Habilitar "Abrir terminal" no cabeçalho do detalhe (011) e na ação de linha (010) conforme `allowedActions`, navegando para o detalhe com `?tab=terminal`; substituir o "em breve" da 011 por "sem permissão" (sem scope), "não suportado pelo agente" (sem capability) ou habilitado. — "Abrir terminal" no cabeçalho e na linha da lista conforme `allowedActions` (`exec`); estados "sem permissão", "não suportado pelo agente" e "somente em execução".

## Fase 6: Verificação

- [x] T013 Playwright com terminal real (Docker/Podman), testes negativos (SC-003), término em todos os motivos com verificação por inspeção do exec (SC-002), queda do navegador em ≤ 30 s, medição do SC-001 (prompt ≤ 3 s, eco ≤ 150 ms p95) com evidência registrada, acessibilidade. — `frontend/tests/e2e/mock_ui/test_014_terminal.py` (5 testes com axe: justificativa, painel, estados de bloqueio, ação de linha, mobile); término por todos os motivos verificado por inspeção do exec em Podman real; fluxo completo hub-serviço → RPC → agente no teste .NET. Terminal real no navegador e Docker: **NOT RUN**. Na stack real em 2026-10-14 (Linux, Podman 5.7.0 rootless; Keycloak, BFF, API, web e agente Podman; ver `deploy/e2e/README.md`): terminal aberto pelo navegador num container real em Homologação, comando digitado no xterm executado pelo hub, BFF, API, RPC `Exec` e agente, com a saída de volta na tela.
- [x] T014 Carga com 4 sessões por agente (SC-004) e canários (SC-005); evidência registrada. — canários: conteúdo digitado ausente da auditoria (`TerminalSessionServiceTests`), justificativa mascarada. Carga com 4 sessões (SC-004) e logs de processo da stack real: **NOT RUN**.
- [x] T015 Loop visual desktop/mobile da aba Terminal e diálogos; comparar com `Detalhe.dc.html` (estado tab 4). — capturas desktop/mobile da aba Terminal e da justificativa em `artifacts/e2e/mock` (painel escuro conforme o board 03, estado tab 4).

## Dependências e Ordem de Execução

T001-T003 (gates) -> T004/T005 -> T006/T007 -> T008-T010 -> T011/T012 -> T013-T015.

## Atualização da spec 023 (2026-10-17)

- **Terminal real pelo navegador (Keycloak, BFF, hub e agente)**: Docker 28.5.2 — prompt em 0,19 s e eco em 29 ms; Podman 5.7 — 0,22 s e 8 ms; encerramento pelo botão, por queda do navegador (shell encerrado antes de o script terminar), por ociosidade (60 s com o limite temporário de 1 min) e por revogação de `container:exec` (55 s, motivo `permission_revoked`). Conteúdo digitado ausente da auditoria.
- **Achado corrigido**: na revogação, o console derrubava a sessão ao atualizar o detalhe e o fim era auditado como `user_closed`.
- Continua `NOT RUN`: carga com 4 sessões por agente (SC-004) — frente 2.
