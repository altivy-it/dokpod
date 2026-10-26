# Tarefas: Distribuição Containerizada

**Entrada**: [spec.md](spec.md) e [plan.md](plan.md).

**Estado**: `superseded` - encerrada em 2026-10-25 por substituição: o conteúdo foi validado pelas specs 005 a 023, conforme a referência em cada tarefa. Nenhuma tarefa foi executada por este conjunto.

**Pré-requisitos**: Revisão/autorização humanas; plataforma central e laboratório disponíveis antes de futura execução.

**Testes**: Build, inspeção e smoke, somente depois de autorização; não executados nesta migração.

**Organização**: IDs locais e jornadas `[US1]`/`[US2]`; tarefas de diferença e residual, não reconstrução da stack.

## Fase 1: Avaliação

- [X] T001 Submeter os artefatos deste diretório à revisão humana, sem herdar `approved` do [P04](../../docs/plan/p04-padronizacao-dockerfiles-compose.md). *(Encerrada por substituição: a migração foi superada pelas specs 005 a 023, aprovadas e executadas.)*
- [X] T002 Avaliar evidências, pendências e falhas temporais do P04 e MVP P-01/P-02/P-06 frente a [distribuição](../../docs/distribuicao.md), `deploy/e2e/` e Dockerfiles proprietários; registrar diferença em [plan.md](plan.md), sem alterar plataforma externa. *(Encerrada por substituição: confronto com o legado feito na [005 T002](../005-qualificacao-release-docker-linux/plan.md#matriz-de-gates-t018--2026-10-24).)*

## Fase 2: Builds e Runtime (US1)

**Teste independente**: Quatro builds limpos identificados e runtime inspecionado.

- [X] T003 [US1] Validar targets/contextos/bases e smokes existentes em `backend/apps/Dokpod.Bff/Dockerfile`, `backend/apps/Dokpod.ControlPlane.Api/Dockerfile`, `frontend/web/Dockerfile` e `deploy/agent/Dockerfile`; conferir contratos protobuf incluídos, usuário não root, runtime sem SDK/Node e filesystem/mounts do agente, sem recriar Dockerfiles entregues. *(Encerrada por substituição: Dockerfiles sobre as bases do repositório `containers`, SBOM, Trivy e smoke no job Images (gates 1 e 2 da [005](../005-qualificacao-release-docker-linux/plan.md#matriz-de-gates-t018--2026-10-24)).)*

## Fase 3: Borda e Persistência (US2)

**Teste independente**: Startup e recriação pela borda nginx, sem bypass ou perda de dados.

- [X] T004 [US2] Avaliar/validar `deploy/e2e/docker-compose-dokpod.yaml` e `deploy/nginx/`: rede central, portas, mounts, forwarded headers, WebSocket, health dependencies e ausência de infraestrutura duplicada; resolver documentalmente a divergência `identity-client`/`identity-global` antes de qualquer residual runtime autorizado. *(Encerrada por substituição: compose e borda de `deploy/e2e/` usados na stack real da [023](../023-qualificacao-stack-real/tasks.md) e nos testes de laboratório da 005.)*
- [X] T005 [US2] Validar startup, health/readiness, gRPC mTLS e persistência de key ring/identidade/journal após recriação em `deploy/e2e/`, articulando login/relay e bloqueio de rota direta com o [002](../002-sessao-bff-relay-realtime/tasks.md); não ler/exibir secrets externos. *(Encerrada por substituição: startup, mTLS e persistência de identidade e dados na atualização e no rollback (gate 10 da [005](../005-qualificacao-release-docker-linux/plan.md#matriz-de-gates-t018--2026-10-24)).)*

## Fase 4: Inspeções e Residual

- [X] T006 Confrontar inspeções de usuário/portas/mounts/redes e contexto limpo com evidências de `deploy/`, registrar em [tasks.md](tasks.md) somente diferenças para revisão e conclusão autorizada; encaminhar inspeções automatizadas e supply chain ao [005](../005-qualificacao-release-docker-linux/tasks.md), pacote Windows ao [006](../006-qualificacao-windows-podman/tasks.md). *(Encerrada por substituição: inspeções de usuário, portas, mounts e redes no `validate-release-images.ps1` do job Images (gate 1 da [005](../005-qualificacao-release-docker-linux/plan.md#matriz-de-gates-t018--2026-10-24)).)*

## Dependências e Ordem de Execução

T001 -> T002 -> T003/T004 -> T005 -> T006. T005 depende da plataforma externa autorizada e do BFF existente. Esta migração não autoriza executar Compose/build/smoke, alterar runtime, CI ou outro repositório; nenhum checkbox significa aprovação de produção.
