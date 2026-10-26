# Tarefas: Sessão BFF, Relay e Realtime

**Entrada**: [spec.md](spec.md) e [plan.md](plan.md).

**Estado**: `superseded` - encerrada em 2026-10-25 por substituição: o conteúdo foi validado pelas specs 005 a 023, conforme a referência em cada tarefa. Nenhuma tarefa foi executada por este conjunto.

**Pré-requisitos**: Revisão humana, autorização específica e laboratório com identidade sintética.

**Testes**: Obrigatórios para a fronteira de confiança; não executados nesta migração.

**Organização**: Tarefas residuais por jornada, com IDs locais e marcadores `[US1]` a `[US3]`. Sem paralelismo presumido.

## Fase 1: Avaliação

- [X] T001 Submeter os três artefatos deste diretório à revisão humana, registrando autorização específica em [plan.md](plan.md), sem herdar status do [P05](../../docs/plan/p05-bff-keycloak-relay.md). *(Encerrada por substituição: a migração foi superada pelas specs 005 a 023, aprovadas e executadas.)*
- [X] T002 Avaliar diferença de P05-01 a P05-09, P04-05 a P04-07 e evidências posteriores do [MVP](../../docs/plan/mvp.md) em `backend/apps/Dokpod.Bff/`, `backend/apps/Dokpod.ControlPlane.Api/` e `frontend/web/src/app/`; catalogar cenário/commit/evidência em [plan.md](plan.md). *(Encerrada por substituição: confronto com o legado feito na [005 T002](../005-qualificacao-release-docker-linux/plan.md#matriz-de-gates-t018--2026-10-24).)*

## Fase 2: Sessão (US1)

**Teste independente**: Login/refresh/logout sem credenciais no browser.

- [X] T003 [US1] Validar e complementar somente cenários ausentes de sessão, callback/state/nonce/returnUrl, refresh concorrente/rejeitado, expiração, reinício, Data Protection e indisponibilidade em `backend/tests/Dokpod.Bff.Tests/` e `frontend/tests/`. *(Encerrada por substituição: sessão, callback e refresh nos testes de `Dokpod.Bff.Tests`; E2E de sessão no gate 14 da [005](../005-qualificacao-release-docker-linux/plan.md#matriz-de-gates-t018--2026-10-24), com residual aceito (sessão expirada e indisponibilidade do Keycloak sem E2E dedicado); logout corrigido no #37 e coberto por E2E no #41.)*

## Fase 3: Relay e API PEP (US2)

**Teste independente**: Downstream capturado e mutações negadas antes da chamada.

- [X] T004 [US2] Avaliar/validar relay existente em `backend/apps/Dokpod.Bff/Endpoints/` e `backend/tests/Dokpod.Bff.Tests/`: headers/cookies, métodos, query/corpo chunked, Bearer server-side, 401/403/502/504, cancelamento, SSRF/redirect, antiforgery/Origin e limites; conferir contrato em `contracts/openapi/` sem refazer endpoints entregues. *(Encerrada por substituição: relay nos testes `DownstreamProxyTests` e na jornada autenticada pela borda (gate 14 da 005).)*

## Fase 4: Realtime e Frontend (US3)

**Teste independente**: Grupo autorizado, upgrade, encerramento e reconexão via borda.

- [X] T005 [US3] Validar hub/relay existente em `backend/tests/` e `frontend/tests/`: grupos A/B, Origin, negotiate, subprotocol, token expirado, encerramento dos pumps, reconexão e invalidação mínima seguida de REST durável do [003](../003-inventario-lifecycle-reconciliacao/spec.md). *(Encerrada por substituição: hub e tempo real nas specs 011 e 014 e no terminal real da [023 T007](../023-qualificacao-stack-real/tasks.md); reconnect storm no gate 7 da [005](../005-qualificacao-release-docker-linux/plan.md#matriz-de-gates-t018--2026-10-24).)*
- [X] T006 [US2] [US3] Conferir cliente Angular em `frontend/web/src/app/` e testes em `frontend/tests/`: rotas relativas/base path, antiforgery, ausência de Web Storage/Bearer/query token, estados 401/403/503, polling limitado e uso de credenciais técnicas sem `accessTokenFactory`. *(Encerrada por substituição: cliente Angular com antiforgery, sem Web Storage nem Bearer, e estados 401/expirado nas specs 007 e 008 (Vitest e E2E simulada).)*

## Fase 5: Integração e Residual

- [X] T007 Validar cenários públicos e recuperação em `frontend/tests/` pela borda de `deploy/e2e/`, usando a topologia verificada pelo [004](../004-distribuicao-containerizada/tasks.md); inspecionar somente evidências sanitizadas de rede, DOM, cookies, logs/traces e bloqueio de acesso direto à API. *(Encerrada por substituição: jornada pública pela borda de `deploy/e2e/` no gate 14 da [005](../005-qualificacao-release-docker-linux/plan.md#matriz-de-gates-t018--2026-10-24).)*
- [X] T008 Confrontar threat model/limites/TTL/egress, documentação [BFF](../../docs/bff.md) e instância única com resultados T002-T007; registrar em [tasks.md](tasks.md) somente residual demonstrado para revisão e conclusão autorizada, encaminhando gate HIGH e NO-GO ao [005](../005-qualificacao-release-docker-linux/tasks.md). *(Encerrada por substituição: threat model consolidado no gate 4 da [005](../005-qualificacao-release-docker-linux/plan.md#matriz-de-gates-t018--2026-10-24).)*

## Dependências e Ordem de Execução

T001 -> T002 -> T003-T006 -> T007 -> T008. T007 depende de 004 e fixtures de 001; T005 usa estado de 003, mas pode validar transporte isoladamente. Reutilizar testes, não reconstruir BFF/hub existentes. A migração não autoriza executar testes, aplicação, CI remoto ou publicação.
