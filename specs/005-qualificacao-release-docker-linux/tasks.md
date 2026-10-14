# Tarefas: Qualificação de Release Docker Linux

**Entrada**: [spec.md](spec.md) e [plan.md](plan.md).

**Estado**: `draft` - rascunho de migração, aguardando revisão humana.

**Pré-requisitos**: Revisão humana, autorização específica por gate, build identificado e ambiente isolado.

**Testes**: Obrigatórios; resultados históricos não são reexecuções desta branch.

**Organização**: IDs locais e jornadas `[US1]` a `[US3]`; todos os checkboxes permanecem desmarcados.

## Fase 1: Inventário de Gates

- [ ] T001 Submeter os três artefatos deste diretório à revisão humana e definir escopo autorizado, sem tratar a migração como autorização de CI, carga, assinatura ou publicação.
- [ ] T002 Confrontar [MVP](../../docs/plan/mvp.md), [desbloqueio](../../docs/plan/desbloqueio-release.md), [release-readiness](../../docs/release-readiness.md) e evidências de 001-004; registrar commit/digest/data/cenário, divergências e gaps em [plan.md](plan.md), sem editar veredito.

## Fase 2: CI e Segurança (US1)

**Teste independente**: Rastrear candidata por execução/artifacts e testar rejeição de origem inválida.

- [ ] T003 [US1] Após autorização externa específica, verificar execução remota do CI existente e artifacts de backend/migrations/PostgreSQL sem skips, frontend, quatro imagens, SBOM/scan e inspeções de usuário/portas/mounts/redes; registrar URL/ID/commit em [plan.md](plan.md), referenciando `deploy/` e `backend/tests/` sem criar workflow concorrente.
- [ ] T004 [US1] Avaliar os 34 HIGH históricos e scans atuais das quatro imagens em `artifacts/`; registrar correções/re-scan ou classificação/aceite humano com responsável, impacto e expiração em [plan.md](plan.md). Adicionar somente residual demonstrado para autorização; não liberar HIGH por ausência de CRITICAL.
- [ ] T007 [US1] Confrontar threat model, licenças e artefatos identificados de `deploy/` com [segurança](../../docs/seguranca.md); verificar assinatura/provenance/emissor/digest/revogação em ambiente limpo, incluindo testes negativos. Obter autorização separada se geração/assinatura for necessária; não publicar.

## Fase 3: Carga e Recuperação (US2)

**Teste independente**: Dois perfis aprovados previamente com backlog recuperado.

- [ ] T005 [US2] Avaliar harness existente em `tools/scripts/` e `deploy/`, aprovar baseline/thresholds humanos e validar nominal 56/1.120/30 por >=30min e margem >=100/2.000/50 em VM isolada; registrar CPU/memória/rede/PostgreSQL/conexões/filas/idade/p95/p99/erro/convergência e resultados sanitizados em [plan.md](plan.md).
- [ ] T006 [US2] Validar snapshots/deltas/leituras, replay, resposta perdida, reconnect storm, inventário degradado, comandos concorrentes por ambiente e restart/recuperação do control plane, usando `backend/tests/`, `frontend/tests/` e garantias do [003](../003-inventario-lifecycle-reconciliacao/tasks.md); comprovar ausência de perda/efeito duplicado/acesso indevido e retorno do backlog ao baseline.

## Fase 4: Operação e Decisão (US3)

**Teste independente**: Matriz obrigatória sem NOT RUN nem exceção vencida.

- [ ] T008 [US3] Confrontar evidências de N/N-1 do [003](../003-inventario-lifecycle-reconciliacao/tasks.md), backup/restore/auditoria, atualização/rollback, observabilidade/alertas, runbooks em `docs/runbooks/`, licença/avisos/código correspondente e CE sem chave/licenciamento externo; registrar matriz Docker Linux e decisões de adiamento do [006](../006-qualificacao-windows-podman/tasks.md) em [plan.md](plan.md).
- [ ] T009 [US3] Registrar gaps específicos restantes em [tasks.md](tasks.md), obter revisão para concluir/revalidar somente residual e submeter matriz a decisão humana GO/NO-GO referenciada em [plan.md](plan.md); obrigatório NOT RUN ou HIGH/CRITICAL sem tratamento mantém NO-GO. Não alterar `docs/release-readiness.md`, publicar, fazer deploy, commit ou push sob esta migração.

## Dependências e Ordem de Execução

T001 -> T002 -> T003 -> T004; T005 exige baseline e laboratório de 004, T006 usa 003 e T005; T007 usa artifacts finais de T004; T008 agrega T003-T007 e decisões de 006; T009 depende de todos. A posição numérica de T007 não implica execução antes da carga: os gates podem ser agendados somente por autorização explícita. Sem evidência, registrar bloqueio, não PASS.
