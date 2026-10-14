# Plano de Implementação: Terminal Interativo no Container

**Branch**: `007-console-operacional` | **Data**: 2026-10-12 | **Spec**: [spec.md](spec.md)

**Estado**: `approved-for-implementation` - execução autorizada em 2026-10-13 (specs/README.md); ADR 2026-0006 (T001) e threat model (T002) continuam bloqueantes.

## Resumo

1. Navegador ↔ API: streaming bidirecional SignalR (`OpenTerminal` com stream de entrada do cliente e stream de saída do servidor), passando pelo proxy WebSocket já existente do BFF.
2. API ↔ agente: o servidor envia `ExecOpenRequest` pelo stream `Connect`; o agente abre um **novo RPC** `Exec` (bidirecional, mesmo canal mTLS) vinculado a sessão/fencing e ao `exec_session_id`, mantendo o tráfego do terminal fora do stream de controle.
3. Agente ↔ engine: `POST /containers/{id}/exec` (Tty, AttachStdin/Stdout/Stderr, Cmd fixo) + `POST /exec/{id}/start` com conexão "hijacked"; `POST /exec/{id}/resize`. Podman pela API compatível.

## Contexto Técnico

**Linguagem/Versão**: .NET 10 (gRPC, SignalR), Protobuf v1, Angular 22.

**Dependências Principais**: Nova no frontend: emulador de terminal (xterm.js, MIT, versão fixada) com addon de ajuste de tamanho; requer aprovação no ADR 2026-0006.

**Armazenamento**: Auditoria existente com `details` da 011 (exceção de texto apenas para `justification`, ver D-009); sessões ativas em memória da API (efêmeras, réplica única); IDs de exec ativos em arquivo no diretório de estado do agente.

**Testes**: Agente (exec hijack Docker/Podman, resize, término, limites), API (autorização periódica, limites, justificativa), transporte (vínculo fencing, revogação), canários, Playwright (terminal real), carga (SC-004).

**Restrições**: Comando fixo; sem troca de usuário; sem proxy genérico; sem gravação de conteúdo.

## Verificação da Constituição

| Item | Situação |
| --- | --- |
| Somente o agente acessa o engine; sem proxy genérico | OK: exec tipado com comando fixo; sem repasse de rotas. |
| IV. Operações duráveis | Exceção declarada: sessão interativa at-most-once, sem journal, com `exec_session_id`, expiração, auditoria e varredura de órfãos (D-006); registrada no ADR 2026-0006. |
| VI. Plataforma | Docker e Podman Linux qualificados separadamente; Windows não anuncia a capability nesta entrega. |
| Operações privilegiadas e novas fronteiras exigem threat model e testes negativos | Pendente (T002). |
| Mudança de protocolo | Adição compatível (novo RPC e mensagens); agentes N-1 sem capability. |
| Nova dependência | ADR `proposed` 2026-0006 (terminal e xterm.js). |
| Telemetria sem logs/conteúdo | OK: conteúdo não registrado. |

## Estrutura do Projeto

```text
docs/adr/2026-0006-terminal-interativo.md            # novo ADR (proposed)
contracts/agent/v1/agent.proto                       # rpc Exec; ExecOpenRequest (16); ExecFrame; capability 15
contracts/openapi/...                                # GET terminal-policy; scope container:exec
backend/libs/Dokpod.ControlPlane.Application/Terminal/  # TerminalSessionService, TerminalPolicy, ExecSessionRegistry
backend/apps/Dokpod.ControlPlane.Api/Agents/AgentExecService.cs     # implementação do rpc Exec
backend/apps/Dokpod.ControlPlane.Api/Realtime/ControlPlaneHub.cs    # OpenTerminal(stream input) → stream output
backend/libs/Dokpod.Agent.Application/Terminal/      # ExecSessionManager (limites, término)
backend/libs/Dokpod.Agent.Infrastructure/Engines/{Docker,Podman}/ExecClient.cs   # hijack/resize
backend/libs/Dokpod.Agent.Infrastructure/Protocol/AgentExecClient.cs
frontend/web/src/app/features/container-detail/terminal.tab.*, terminal.facade.ts
```

## Fases

1. ADR + threat model (bloqueante). 2. Contratos. 3. Agente (exec engine + RPC). 4. API (sessões, políticas, hub). 5. Frontend (emulador acessível). 6. Segurança, carga e E2E.

## Riscos

| Risco | Mitigação |
| --- | --- |
| Uso indevido do shell | Scope dedicado, justificativa, MFA opcional, limites, auditoria, label de bloqueio. |
| Saturação do canal do agente | RPC separado, limites de taxa e de sessões. |
| Sessão órfã no engine | Engine não tem label nem kill para exec: fechar o TTY (SIGHUP), confirmar `Running=false` por inspeção em até 5 s, persistir IDs de exec ativos no estado local do agente e auditar `orphan_suspected`; processos em segundo plano do usuário são risco residual (D-007). |
| Revogação tardia | Monitor de autorização compartilhado com a 011, a cada 60 s; janela máxima documentada (D-008). |
| Emulador renderiza sequências perigosas | Configuração restritiva (sem OSC 52/links automáticos). |
