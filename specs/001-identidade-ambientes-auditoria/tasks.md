# Tarefas: Identidade, Ambientes e Auditoria

**Entrada**: [spec.md](spec.md) e [plan.md](plan.md).

**Estado**: `draft` - rascunho de migração, aguardando revisão humana.

**Pré-requisitos**: Revisão humana dos artefatos e autorização específica; nenhum checkbox comprova aprovação.

**Testes**: Necessários por segurança e integridade; não executados nesta migração.

**Organização**: Avaliação, validação e residual por jornada; não duplicar implementação histórica.

## Formato: `[ID] [P?] [Story?] Descrição com caminho`

IDs T são locais ao conjunto; `[US1]` a `[US3]` referem-se às jornadas. Sem `[P]` enquanto dependências e ambiente não forem resolvidos.

## Fase 1: Avaliação e Pré-requisitos

- [ ] T001 Submeter [spec.md](spec.md), [plan.md](plan.md) e [tasks.md](tasks.md) à revisão humana, registrando escopo autorizado sem herdar aprovações legadas.
- [ ] T002 Confrontar MVP P-02/P-03 e P03-01/P03-02 com `backend/libs/`, `backend/tests/` e as decisões referenciadas no [P03](../../docs/plan/p03-auditoria-persistente.md); registrar diferença, evidência e conflitos de status em [plan.md](plan.md), sem alterar ADR.

## Fase 2: Ambientes Autorizados (US1)

**Teste independente**: Cadastro concorrente e acesso A/B com dependência indisponível.

- [ ] T003 [US1] Avaliar e validar registro/aprovação/suspensão e retry/resposta perdida em `backend/tests/` e `frontend/tests/`; conferir autorização anterior à leitura, auditoria e persistência em `backend/libs/`, incluindo falha do writer.

## Fase 3: Identidade Individual (US2)

**Teste independente**: Inscrição concorrente e revogação do stream com identidade sintética.

- [ ] T004 [US2] Confrontar bootstrap oficial, rotação, clone, certificado/EKU, ambiente divergente e revogação/reconexão com `contracts/agent/`, `backend/apps/Dokpod.ControlPlane.Api/` e `backend/tests/`; registrar cenários não comprovados em [plan.md](plan.md).

## Fase 4: Auditoria e Recuperação (US3)

**Teste independente**: Append, conflito, privilégios e rollover em PostgreSQL real isolado.

- [ ] T005 [US3] Validar evidência existente de `backend/libs/Dokpod.ControlPlane.Infrastructure/` e `backend/tests/` para idempotência, concorrência, cancelamento, privilégio runtime, isolamento do schema, partições mensais/futuras/DEFAULT, pruning e rollover; não recriar writer ou migrations já entregues.
- [ ] T006 [US3] Avaliar retenção, consulta autorizada e recuperação conforme [runbook](../../docs/runbooks/operacao-auditoria-postgresql.md), registrar decisão humana pendente e vincular o exercício de release ao [005](../005-qualificacao-release-docker-linux/tasks.md), sem ativar descarte.

## Fase 5: Contratos e Residual

- [ ] T007 Validar OpenAPI, erros, readiness e métricas sanitizadas em `contracts/openapi/`, `backend/tests/` e `frontend/tests/`, referenciando os cenários de sessão do [002](../002-sessao-bff-relay-realtime/tasks.md).
- [ ] T008 Registrar em [tasks.md](tasks.md) somente lacunas demonstradas por T002-T007, submetê-las à revisão humana e concluir/revalidar apenas o residual autorizado nos módulos existentes; conservar as evidências originais no [P03](../../docs/plan/p03-auditoria-persistente.md).

## Dependências e Ordem de Execução

T001 bloqueia execução; T002 antecede T003-T007; T008 depende de seus resultados. Reutilizar testes existentes, complementar somente cenários ausentes. Containers e nginx são obrigatórios para futuras validações HTTP; esta autorização cobre apenas documentos.
