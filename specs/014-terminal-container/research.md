# Pesquisa: Terminal Interativo

## D-001 Canal entre API e agente

- **Decisão**: Novo RPC `Exec(stream ExecFrame) returns (stream ExecFrame)` no serviço `AgentControl`, aberto pelo agente após `ExecOpenRequest`. O primeiro frame carrega `exec_session_id`, `session_id` e `fencing_token`; a API valida contra a sessão ativa e o registro de sessões de terminal pendentes.
- **Alternativa**: Multiplexar no `Connect` (head-of-line blocking com comandos; rejeitado).

## D-002 Canal entre navegador e API

- **Decisão**: SignalR com streaming do cliente (`IAsyncEnumerable<TerminalInput>`) e do servidor (`IAsyncEnumerable<TerminalOutput>`) no hub existente; WebSocket obrigatório para o terminal (o BFF já faz proxy WebSocket em `DownstreamProxy.ProxyWebSocketAsync`).
- **Alternativa**: endpoint WebSocket dedicado na API (duplicaria autenticação/relay).

## D-003 Shell e usuário

- Detecção: criar o exec diretamente com `Cmd ["/bin/bash"]`; se o runtime responder executável não encontrado (exit 126/127 ou erro OCI), repetir com `/bin/sh`; se ambos falharem, `shell_not_available`. Evita exec extra e dependência do binário `test`. Usuário: padrão da imagem; sem `User` no exec.
- **Revisão na implementação (2026-10-14)**: em Podman real, fechar a conexão do exec **não** encerra o shell (sem SIGHUP), o que quebraria D-007/SC-002. O comando fixo passou a ser um único exec `/bin/sh -c` que escolhe `/bin/bash` ou `/bin/sh` com o `[` embutido do sh (sem binário `test`), anuncia o PID do shell por uma sequência OSC privada (removida da saída) e faz `exec` no shell. O término envia `SIGHUP` (e `SIGKILL` após 5 s) a esse PID por um exec destacado com `kill` embutido do sh. Imagem sem `/bin/sh` → `shell_not_available`.

## D-004 Emulador no navegador

- **Candidato**: xterm.js (MIT), mantido, usado amplamente; opções: `screenReaderMode` configurável, `allowProposedApi: false`, sem addon de links ou clipboard OSC 52. Alternativa: emulador próprio (custo alto, rejeitado).

## D-005 MFA reforçada

- Keycloak pode exigir `acr` mínimo via política; a API verifica `acr`/`auth_time` do token relayado quando `Terminal:RequireRecentMfa` estiver ativo. Depende de configuração do realm (documentar em `docs/configuracao-keycloak.md`).

## D-006 Sessão interativa e o princípio IV

- A sessão não é comando durável: `ExecOpenRequest` é entregue no máximo uma vez, sem journal nem retry; o agente deduplica por `exec_session_id` e recusa após `expires_at`. Auditoria `requested/started/ended/denied` e varredura de órfãos (D-007) cumprem rastreabilidade e reconciliação. Exceção registrada no ADR 2026-0006.

## D-007 Término e órfãos

- Docker e Podman não aceitam labels em exec nem expõem endpoint para encerrar exec. **Decisão** (ajustada pela revisão de D-003): o agente fecha o stream do TTY e envia SIGHUP ao PID do shell, consulta `GET /exec/{id}/json` até `Running=false` (até 5 s) e, se continuar em execução, audita `orphan_suspected`. IDs de exec ativos são gravados em `<data>/exec-sessions.json`; após reinício, o agente verifica cada um e audita os que seguem em execução. Processos que o usuário mandou para segundo plano ficam fora da garantia (risco residual no threat model).

## D-008 Reavaliação de autorização

- Componente `StreamingAuthorizationMonitor` compartilhado com a 011 (logs ao vivo): a cada 60 s verifica a sessão do BFF e refaz a decisão de autorização da API com o access token atual (renovado pelo BFF).
- **Implementação (2026-10-14)**: o token do WebSocket é fixo desde a negociação; para não encerrar o terminal a cada expiração do access token, o navegador chama `POST /api/v1/terminal-sessions/{id}/renew` a cada 2 min (o BFF injeta token renovado) e a API reavalia `container:exec` com ele; a reavaliação periódica da sessão usa o token mais recente. Sem renovação, a sessão termina com `session_refresh_required` perto do `exp`. Revogação no Keycloak passa a valer em até 60 s mais a vida útil restante do token; o realm deve manter access token curto (≤ 5 min), documentado em `docs/configuracao-keycloak.md`.

## D-009 Justificativa na auditoria

- A 011 (D-006) restringe `audit_events.details` a números, booleanos e enums. **Decisão**: exceção única para a chave `justification` da ação `container.exec.requested` (texto de 10-500 caracteres, mascarado pelo `SecretMasker`, dentro do limite de 2 KiB de `details`); nenhuma outra chave aceita texto livre.

## D-010 Keepalive do hub

- Para o terminal: `KeepAliveInterval` 15 s e `ClientTimeoutInterval` 30 s, garantindo término em até 30 s após queda do navegador.
