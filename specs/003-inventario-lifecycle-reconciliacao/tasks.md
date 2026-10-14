# Tarefas: Inventário, Lifecycle e Reconciliação

**Entrada**: [spec.md](spec.md) e [plan.md](plan.md).

**Estado**: `draft` - rascunho de migração, aguardando revisão humana.

**Pré-requisitos**: Autorização humana do recorte e laboratório isolado; não operar workloads reais nesta migração.

**Testes**: Obrigatórios contra engine/PostgreSQL reais para os efeitos e durabilidade.

**Organização**: IDs locais, jornadas `[US1]` a `[US3]`; não repetir entregas registradas no MVP.

## Fase 1: Avaliação

- [ ] T001 Submeter [spec.md](spec.md), [plan.md](plan.md) e [tasks.md](tasks.md) à revisão humana e registrar autorização específica.
- [ ] T002 Confrontar provas 1/4/5/8 de [viabilidade](../../docs/viabilidade.md), MVP P-01/P-02/P-04/P-05, `backend/libs/`, `backend/tests/` e `contracts/agent/`; mapear implementado/evidenciado/residual em [plan.md](plan.md).

## Fase 2: Inventário (US1)

**Teste independente**: Delta/lacuna/snapshot ordenado e consulta autorizada.

- [ ] T003 [US1] Validar testes existentes e complementar somente lacunas de revisão/sequência, snapshot paginado/atômico, limites, reconnect, heartbeat/jitter, snapshot periódico e invalidação limitada em `backend/tests/` e `frontend/tests/`; conferir idade/paginação/UI em `frontend/web/src/app/` sem recriar inventário.

## Fase 3: Lifecycle e Compatibilidade (US2)

**Teste independente**: Replay idêntico/divergente e crash entre journal e ACK.

- [ ] T004 [US2] Validar com engine real o journal-before-ack e resultado-before-report, flush/rename/fsync Linux, reabertura, replay, fencing stale, deadline após espera, ID/hash/revisão/capability, alvo recriado e delete sem volumes em `backend/tests/`; separar a qualificação Windows do [006](../006-qualificacao-windows-podman/tasks.md).
- [ ] T005 [US2] Avaliar e validar N/N-1 em `contracts/agent/`, `contracts/openapi/` e `backend/tests/` com servidor/agente consecutivos, tags protobuf preservadas, unknown fields seguros, enums/comandos rejeitados e rollback expand-contract; encaminhar evidência ao [005](../005-qualificacao-release-docker-linux/tasks.md).

## Fase 4: Recuperação (US3)

**Teste independente**: Resposta perdida, nova sessão e estado observado reconciliado.

- [ ] T006 [US3] Validar claim concorrente, transições monotônicas, cancelamento de stream fenced, fencing original/redespacho, sweep Failed/Indeterminate e resultado tardio em `backend/tests/` com PostgreSQL real, usando fila/journal existentes em `backend/libs/`.
- [ ] T007 [US3] Confrontar chave global entre meses, partições/DEFAULT/rollover, tombstones/retenção e auditoria atômica com [runbook](../../docs/runbooks/operacao-comandos-postgresql.md), [001](../001-identidade-ambientes-auditoria/tasks.md) e UI em `frontend/tests/`; registrar decisões pendentes sem apagar eventos ou habilitar descarte.

## Fase 5: Residual

- [ ] T008 Registrar em [tasks.md](tasks.md) lacunas demonstradas por T002-T007 para revisão humana; concluir e revalidar somente o residual autorizado, referenciando carga/recuperação integrada no [005](../005-qualificacao-release-docker-linux/tasks.md), sem refazer domínio, adapter, fila ou UI já entregues.

## Dependências e Ordem de Execução

T001 -> T002 -> T003-T007 -> T008; T006 usa T004, T007 usa 001. N/N-1 e recuperação alimentam 005; testes HTTP futuros usam 004/nginx. Cada prova registra commit, ambiente, cenário e evidência sanitizada; os checkboxes não herdam PASS nem autorização do histórico.
