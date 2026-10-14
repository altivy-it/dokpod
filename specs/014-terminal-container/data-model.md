# Modelo de Dados: Terminal Interativo

## Auditoria (tipos novos)

| Tipo | Dados |
| --- | --- |
| `container.exec.requested` | ator, ambiente, container, estágio, justificativa (≤ 500, mascarada; única exceção de texto em `details`, D-009), correlação |
| `container.exec.started` | sessão, shell, usuário do container (se root, sinalizado) |
| `container.exec.ended` | sessão, motivo (`user_closed`, `exit`, `idle_timeout`, `max_duration`, `permission_revoked`, `agent_disconnected`, `container_stopped`, `client_disconnected`, `agent_restarted`, `error`), duração, bytes in/out, `orphan_suspected` (bool) |
| `container.exec.denied` | ator, ambiente, container, motivo (`scope`, `label_disabled`, `limit`, `justification_missing`, `mfa_required`, `not_running`, `not_supported`, `websocket_required`, `shell_not_available`) |

## Sessão (memória da API)

`TerminalSession`: `sessionId`, `actor`, `environmentId`, `containerId`, `connectionId`, `agentSessionId`, `fencingToken`, `startedAt`, `lastInputAt`, `expiresAt`, `bytesIn`, `bytesOut`, `cols`, `rows`, `state` (`opening`, `open`, `closing`, `closed`).

## Política (configuração)

`Terminal:IdleTimeout` 10 min, `Terminal:MaxDuration` 30 min, `Terminal:MaxPerUser` 2, `Terminal:MaxPerUserPerContainer` 1, `Terminal:MaxPerAgent` 4, `Terminal:MaxOutputBytesPerSecond` 256 KiB, `Terminal:RequireJustificationStages` [`production`], `Terminal:RequireRecentMfa` false, `Terminal:AuthorizationRecheckInterval` 60 s, `Terminal:TerminationVerifyTimeout` 5 s, `Terminal:HubKeepAliveInterval` 15 s, `Terminal:HubClientTimeout` 30 s. Valores são padrões configuráveis.

## Estado local do agente

`<data>/exec-sessions.json`: `execSessionId`, `engineExecId`, `containerId`, `startedAt`; removido ao confirmar término; verificado após reinício (D-007).
