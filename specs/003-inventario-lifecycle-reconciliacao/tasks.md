# Tarefas: Inventário, Lifecycle e Reconciliação

**Entrada**: [spec.md](spec.md) e [plan.md](plan.md).

**Estado**: `superseded` - encerrada em 2026-10-25 por substituição: o conteúdo foi validado pelas specs 005 a 023, conforme a referência em cada tarefa. Nenhuma tarefa foi executada por este conjunto.

**Pré-requisitos**: Autorização humana do recorte e laboratório isolado; não operar workloads reais nesta migração.

**Testes**: Obrigatórios contra engine/PostgreSQL reais para os efeitos e durabilidade.

**Organização**: IDs locais, jornadas `[US1]` a `[US3]`; não repetir entregas registradas no MVP.

## Fase 1: Avaliação

- [X] T001 Submeter [spec.md](spec.md), [plan.md](plan.md) e [tasks.md](tasks.md) à revisão humana e registrar autorização específica. *(Encerrada por substituição: a migração foi superada pelas specs 005 a 023, aprovadas e executadas.)*
- [X] T002 Confrontar provas 1/4/5/8 de [viabilidade](../../docs/viabilidade.md), MVP P-01/P-02/P-04/P-05, `backend/libs/`, `backend/tests/` e `contracts/agent/`; mapear implementado/evidenciado/residual em [plan.md](plan.md). *(Encerrada por substituição: confronto com o legado feito na [005 T002](../005-qualificacao-release-docker-linux/plan.md#matriz-de-gates-t018--2026-10-24).)*

## Fase 2: Inventário (US1)

**Teste independente**: Delta/lacuna/snapshot ordenado e consulta autorizada.

- [X] T003 [US1] Validar testes existentes e complementar somente lacunas de revisão/sequência, snapshot paginado/atômico, limites, reconnect, heartbeat/jitter, snapshot periódico e invalidação limitada em `backend/tests/` e `frontend/tests/`; conferir idade/paginação/UI em `frontend/web/src/app/` sem recriar inventário. *(Encerrada por substituição: snapshot, reconnect e heartbeat nas cargas nominal e de margem e na recuperação (gates 5 a 7 da [005](../005-qualificacao-release-docker-linux/plan.md#matriz-de-gates-t018--2026-10-24)).)*

## Fase 3: Lifecycle e Compatibilidade (US2)

**Teste independente**: Replay idêntico/divergente e crash entre journal e ACK.

- [X] T004 [US2] Validar com engine real o journal-before-ack e resultado-before-report, flush/rename/fsync Linux, reabertura, replay, fencing stale, deadline após espera, ID/hash/revisão/capability, alvo recriado e delete sem volumes em `backend/tests/`; separar a qualificação Windows do [006](../006-qualificacao-windows-podman/tasks.md). *(Encerrada por substituição: journal e resposta perdida no Linux no gate 7 da [005](../005-qualificacao-release-docker-linux/plan.md#matriz-de-gates-t018--2026-10-24) (`Indeterminate` corrigido e coberto por teste); a parte Windows segue na [006 T004](../006-qualificacao-windows-podman/tasks.md).)*
- [X] T005 [US2] Avaliar e validar N/N-1 em `contracts/agent/`, `contracts/openapi/` e `backend/tests/` com servidor/agente consecutivos, tags protobuf preservadas, unknown fields seguros, enums/comandos rejeitados e rollback expand-contract; encaminhar evidência ao [005](../005-qualificacao-release-docker-linux/tasks.md). *(Encerrada por substituição: N/N-1 com binários reais na [023 T003](../023-qualificacao-stack-real/tasks.md) e no gate 8 da [005](../005-qualificacao-release-docker-linux/plan.md#matriz-de-gates-t018--2026-10-24).)*

## Fase 4: Recuperação (US3)

**Teste independente**: Resposta perdida, nova sessão e estado observado reconciliado.

- [X] T006 [US3] Validar claim concorrente, transições monotônicas, cancelamento de stream fenced, fencing original/redespacho, sweep Failed/Indeterminate e resultado tardio em `backend/tests/` com PostgreSQL real, usando fila/journal existentes em `backend/libs/`. *(Encerrada por substituição: claim, fencing e sweep nos testes de backend com PostgreSQL real (gate 1) e no restart da API com backlog (gate 7) da [005](../005-qualificacao-release-docker-linux/plan.md#matriz-de-gates-t018--2026-10-24).)*
- [X] T007 [US3] Confrontar chave global entre meses, partições/DEFAULT/rollover, tombstones/retenção e auditoria atômica com [runbook](../../docs/runbooks/operacao-comandos-postgresql.md), [001](../001-identidade-ambientes-auditoria/tasks.md) e UI em `frontend/tests/`; registrar decisões pendentes sem apagar eventos ou habilitar descarte. *(Encerrada por substituição: partições, retenção e auditoria atômica nos testes de backend com PostgreSQL real (gate 1 da 005) e no [runbook de comandos](../../docs/runbooks/operacao-comandos-postgresql.md).)*

## Fase 5: Residual

- [X] T008 Registrar em [tasks.md](tasks.md) lacunas demonstradas por T002-T007 para revisão humana; concluir e revalidar somente o residual autorizado, referenciando carga/recuperação integrada no [005](../005-qualificacao-release-docker-linux/tasks.md), sem refazer domínio, adapter, fila ou UI já entregues. *(Encerrada por substituição: o residual vive na matriz da [005](../005-qualificacao-release-docker-linux/plan.md#matriz-de-gates-t018--2026-10-24) e na [006](../006-qualificacao-windows-podman/tasks.md).)*

## Dependências e Ordem de Execução

T001 -> T002 -> T003-T007 -> T008; T006 usa T004, T007 usa 001. N/N-1 e recuperação alimentam 005; testes HTTP futuros usam 004/nginx. Cada prova registra commit, ambiente, cenário e evidência sanitizada; os checkboxes não herdam PASS nem autorização do histórico.
