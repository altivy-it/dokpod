# Tarefas: Qualificação Windows e Podman

**Entrada**: [spec.md](spec.md) e [plan.md](plan.md).

**Estado**: `draft` - rascunho de migração, aguardando revisão humana.

**Pré-requisitos**: Revisão/autorização humanas, host Windows limpo e engines sintéticos isolados; nenhuma instalação/elevação autorizada agora.

**Testes**: Obrigatórios por plataforma/capability, com engines reais; não executados nesta migração.

**Organização**: IDs locais e jornadas `[US1]` a `[US3]`; avaliação/validação/residual, sem recriar agente ou script.

## Fase 1: Avaliação

- [ ] T001 Submeter os três artefatos deste diretório à revisão humana e decidir quais capabilities qualificar/adiar, sem promover status de suporte.
- [ ] T002 Confrontar MVP P-01/P-02/P-05/P-06/P-07 e desbloqueio P-05 com `backend/apps/Dokpod.Agent/`, `deploy/agent/` e `backend/tests/`; registrar em [plan.md](plan.md) evidências de console/DryRun/publish separadas das lacunas de serviço real e Podman.

## Fase 2: Windows Service (US1)

**Teste independente**: Ciclo de serviço em host limpo sem runtime .NET.

- [ ] T003 [US1] Avaliar e validar publish self-contained por RID, pacote/script existente `deploy/agent/manage-windows-service.ps1`, conta dedicada, named pipe/ACL, diretório de dados, startup atrasado, recuperação limitada, instalação/update atômico/rollback/remoção preservando identidade/journal em host limpo autorizado; complementar somente diferenças comprovadas, sem refazer script.

## Fase 3: Durabilidade Windows (US2)

**Teste independente**: Crash antes/depois do ACK e resultado com replay seguro.

- [ ] T004 [US2] Confrontar journal Windows em `backend/libs/` com garantias do [003](../003-inventario-lifecycle-reconciliacao/spec.md) e complementar provas de `backend/tests/` para flush/publicação durável antes de ACK/resultado, falha de disco/crash/restart, ID/hash, fencing, prazo, N/N-1 e reconciliação com engine real; rejeitar equivalência baseada só em console.

## Fase 4: Podman (US3)

**Teste independente**: Matriz real rootless/rootful e rejeição de capability ausente.

- [ ] T005 [US3] Avaliar diferença do adapter e executar somente provas autorizadas 2/3 de [viabilidade](../../docs/viabilidade.md) aplicáveis à matriz, com `backend/tests/` e `deploy/agent/`: Podman Linux rootless/rootful, versões, Libpod, mounts/permissões, capabilities, rejeição de acesso não autorizado, inventário/lifecycle/replay e delete sem volumes implícitos.

## Fase 5: Residual e Matriz

- [ ] T006 Registrar em [tasks.md](tasks.md) lacunas comprovadas para revisão e conclusão/revalidação autorizadas; consolidar evidências sanitizadas por SO/engine/RID em [plan.md](plan.md), obter decisão humana stable/experimental/adiada e encaminhá-la ao [005](../005-qualificacao-release-docker-linux/tasks.md), exigindo seus gates HIGH/assinatura/provenance para artefatos anunciados, sem publicar suporte nesta migração.

## Dependências e Ordem de Execução

T001 -> T002 -> T003/T005; T004 depende da base Windows T003 e das garantias de 003; T006 depende das provas selecionadas ou adiamento humano explícito. Não marcar tarefas como concluídas por adiamento: registrar decisão separada. Execução futura de build/testes em containers preserva a exceção instalação/smoke Windows nativo; acesso HTTP continua via nginx containerizado.
