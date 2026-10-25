# Tarefas: Identidade, Ambientes e Auditoria

**Entrada**: [spec.md](spec.md) e [plan.md](plan.md).

**Estado**: `superseded` - encerrada em 2026-10-25 por substituição: o conteúdo foi validado pelas specs 005 a 023, conforme a referência em cada tarefa. Nenhuma tarefa foi executada por este conjunto.

**Pré-requisitos**: Revisão humana dos artefatos e autorização específica; nenhum checkbox comprova aprovação.

**Testes**: Necessários por segurança e integridade; não executados nesta migração.

**Organização**: Avaliação, validação e residual por jornada; não duplicar implementação histórica.

## Formato: `[ID] [P?] [Story?] Descrição com caminho`

IDs T são locais ao conjunto; `[US1]` a `[US3]` referem-se às jornadas. Sem `[P]` enquanto dependências e ambiente não forem resolvidos.

## Fase 1: Avaliação e Pré-requisitos

- [X] T001 Submeter [spec.md](spec.md), [plan.md](plan.md) e [tasks.md](tasks.md) à revisão humana, registrando escopo autorizado sem herdar aprovações legadas. *(Encerrada por substituição: a migração foi superada pelas specs 005 a 023, aprovadas e executadas; esta revisão não é mais necessária.)*
- [X] T002 Confrontar MVP P-02/P-03 e P03-01/P03-02 com `backend/libs/`, `backend/tests/` e as decisões referenciadas no [P03](../../docs/plan/p03-auditoria-persistente.md); registrar diferença, evidência e conflitos de status em [plan.md](plan.md), sem alterar ADR. *(Encerrada por substituição: confronto com o legado feito na [005 T002](../005-qualificacao-release-docker-linux/plan.md#matriz-de-gates-t018--2026-10-24).)*

## Fase 2: Ambientes Autorizados (US1)

**Teste independente**: Cadastro concorrente e acesso A/B com dependência indisponível.

- [X] T003 [US1] Avaliar e validar registro/aprovação/suspensão e retry/resposta perdida em `backend/tests/` e `frontend/tests/`; conferir autorização anterior à leitura, auditoria e persistência em `backend/libs/`, incluindo falha do writer. *(Encerrada por substituição: registro, aprovação e suspensão cobertos pelos testes de backend com PostgreSQL real (gate 1) e pela autorização na stack real ([023 T004](../023-qualificacao-stack-real/tasks.md); gate 13).)*

## Fase 3: Identidade Individual (US2)

**Teste independente**: Inscrição concorrente e revogação do stream com identidade sintética.

- [X] T004 [US2] Confrontar bootstrap oficial, rotação, clone, certificado/EKU, ambiente divergente e revogação/reconexão com `contracts/agent/`, `backend/apps/Dokpod.ControlPlane.Api/` e `backend/tests/`; registrar cenários não comprovados em [plan.md](plan.md). *(Encerrada por substituição: identidade, cadeia, EKU, nome do servidor e revogação no gate 4 (threat model consolidado, achado A1, `ClientCertificateTrustTests`, `AgentControlClientTrustTests` e [runbook de troca da CA](../../docs/runbooks/troca-da-ca-dos-agentes.md)) e revogação no gate 13 da [005](../005-qualificacao-release-docker-linux/plan.md#matriz-de-gates-t018--2026-10-24).)*

## Fase 4: Auditoria e Recuperação (US3)

**Teste independente**: Append, conflito, privilégios e rollover em PostgreSQL real isolado.

- [X] T005 [US3] Validar evidência existente de `backend/libs/Dokpod.ControlPlane.Infrastructure/` e `backend/tests/` para idempotência, concorrência, cancelamento, privilégio runtime, isolamento do schema, partições mensais/futuras/DEFAULT, pruning e rollover; não recriar writer ou migrations já entregues. *(Encerrada por substituição: idempotência, concorrência e partições nos 681 testes de backend com PostgreSQL real (gate 1), recuperação (gate 7) e auditoria íntegra no restore (gate 9) da [005](../005-qualificacao-release-docker-linux/plan.md#matriz-de-gates-t018--2026-10-24).)*
- [X] T006 [US3] Avaliar retenção, consulta autorizada e recuperação conforme [runbook](../../docs/runbooks/operacao-auditoria-postgresql.md), registrar decisão humana pendente e vincular o exercício de release ao [005](../005-qualificacao-release-docker-linux/tasks.md), sem ativar descarte. *(Encerrada por substituição: consulta e retenção da auditoria na [spec 017](../017-auditoria/tasks.md); recuperação no gate 9 da [005](../005-qualificacao-release-docker-linux/plan.md#matriz-de-gates-t018--2026-10-24).)*

## Fase 5: Contratos e Residual

- [X] T007 Validar OpenAPI, erros, readiness e métricas sanitizadas em `contracts/openapi/`, `backend/tests/` e `frontend/tests/`, referenciando os cenários de sessão do [002](../002-sessao-bff-relay-realtime/tasks.md). *(Encerrada por substituição: contrato OpenAPI conferido no CI (`check:api-contract`), readiness e métricas no gate 11 da [005](../005-qualificacao-release-docker-linux/plan.md#matriz-de-gates-t018--2026-10-24).)*
- [X] T008 Registrar em [tasks.md](tasks.md) somente lacunas demonstradas por T002-T007, submetê-las à revisão humana e concluir/revalidar apenas o residual autorizado nos módulos existentes; conservar as evidências originais no [P03](../../docs/plan/p03-auditoria-persistente.md). *(Encerrada por substituição: o residual vive na matriz da [005](../005-qualificacao-release-docker-linux/plan.md#matriz-de-gates-t018--2026-10-24).)*

## Dependências e Ordem de Execução

T001 bloqueia execução; T002 antecede T003-T007; T008 depende de seus resultados. Reutilizar testes existentes, complementar somente cenários ausentes. Containers e nginx são obrigatórios para futuras validações HTTP; esta autorização cobre apenas documentos.
