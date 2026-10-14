# ADR 2026-0006: Terminal interativo no container

- **Status:** accepted
- **Data:** 2026-10-14
- **Origem:** IA assistida, a pedido do solicitante (spec 014)
- **Revisor humano:** zocatel (solicitante e responsável pelo produto)
- **Aprovação de execução:** 2026-10-13, registrada em `specs/README.md` ("Aprovação humana").
- **Aprovação:** 2026-10-14, decisão explícita do solicitante na sessão de trabalho: "ADR revisado e aprovado".

## Contexto

O board `03 · Detalhe do container` tem a aba Terminal e o botão "Abrir terminal". Um shell no container é execução arbitrária de comandos no workload. A arquitetura proíbe proxy genérico para o engine e exige comandos duráveis (constituição, princípio IV) e threat model para novas fronteiras privilegiadas.

## Decisão

1. **Fronteira de exec tipada**: o agente executa somente `POST /containers/{id}/exec` com comando fixo (`/bin/bash`, senão `/bin/sh`), TTY e usuário padrão da imagem; o usuário não escolhe comando, usuário, privilégios nem variáveis. O plano de controle nunca acessa o engine.
2. **Canal separado**: o servidor pede a sessão pelo stream `Connect` (`ExecOpenRequest`, tag 16) e o agente abre um RPC próprio `Exec` (bidirecional, mesmo canal mTLS), vinculado à sessão e ao fencing token do agente. O tráfego do terminal não disputa o stream de controle (sem head-of-line blocking com comandos).
3. **Sem gravação de conteúdo**: entrada e saída não são persistidas nem registradas; a auditoria guarda apenas metadados (`container.exec.requested|started|ended|denied`), com exceção única de texto livre para a justificativa mascarada.
4. **Exceção ao princípio IV**: a sessão é interativa e efêmera, entregue no máximo uma vez, sem journal nem retry automático, deduplicada por `exec_session_id` e recusada após `expires_at`. Rastreabilidade e reconciliação vêm da auditoria e da varredura de execs órfãos (`<data>/exec-sessions.json`, `orphan_suspected`).
5. **Dependência nova**: emulador de terminal xterm.js no frontend, `@xterm/xterm` 6.0.0 e `@xterm/addon-fit` 0.11.0, licença MIT (compatível com AGPL-3.0-only), versões fixadas (sem `^`), configuração restritiva (sem OSC 52, sem links automáticos, `allowProposedApi: false`), modo leitor de tela habilitável.
6. **Limites e revogação**: scope `container:exec` por ambiente, justificativa em Produção, MFA recente opcional, ociosidade 10 min, duração 30 min, 2 sessões por usuário, 1 por usuário por container, 4 por agente, saída limitada por segundo, reavaliação de autorização a cada 60 s pelo `StreamingAuthorizationMonitor` (compartilhado com a 011), WebSocket obrigatório e keepalive do hub 15 s/30 s.
7. Agente Windows não anuncia `CAPABILITY_CONTAINER_EXEC` nesta entrega.

## Consequências

- Capacidade de alto risco disponível somente a grupos restritos; risco residual: processos que o usuário deixar em segundo plano podem sobreviver ao fim da sessão (o engine não encerra execs).
- Sessões vivem em memória da API (réplica única); reinício da API encerra as sessões.
- Gravação de sessão (session recording) exige novo ADR.
- Especificação: [specs/014-terminal-container](../../specs/014-terminal-container/spec.md); threat model em `docs/seguranca.md`.
