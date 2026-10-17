# Tarefas: Pods

**Entrada**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/pods-api.md), [quickstart.md](quickstart.md).

**Estado**: `done` - implementada e validada em 2026-10-16.

## Fase 1: Contrato e domínio

- [x] T001 Proto aditivo (D-005) e tabela de alocação. Evidência: `agent.proto` só aditivo (payload 26, heartbeat 6, comando 11, parâmetro 11, `CommandResult` 9, escopo 6, capabilities 21/22); tabela de alocação atualizada.
- [x] T002 Domínio: ações de pod, scopes, hash, auditoria `pod.*` (categoria `pods`), saída com containers que falharam; testes. Evidência: `PodDomainTests` (domínio: 163 aprovados).

## Fase 2: Agente

- [x] T003 Engine: `/libpod/pods/json`, ação de pod com releitura e containers fora do estado; Docker sem pods; testes. Evidência: `PodAgentTests` (lista libpod, Docker sem pods, códigos das ações, rotas com prefixo de versão, 304 do Podman tratado membro a membro).
- [x] T004 Pod do agente (identificador próprio), builder do snapshot e publicação por capability; testes. Evidência: `PodAgentTests` (identidade própria por `mountinfo` e hostname, pod do agente marcado, `engine_without_pods`).
- [x] T005 Execução do `POD_ACTION` (recusa do pod do agente, códigos); testes. Evidência: `PodAgentTests` (pod do agente recusado sem chegar ao engine, falha parcial com os membros, 409 que converge conta como sucesso); agente: 127 aprovados.

## Fase 3: Plano de controle

- [x] T006 Migration `202610160001_PodInventory` com Down; store (projeção, busca, resumo, filtro por container); testes em PostgreSQL real. Evidência: Up/Down/Up no banco descartável; `PodsIntegrationTests` (revisão obsoleta, resumo por situação, filtro por container, busca, hosts sem pods).
- [x] T007 Sessão gRPC (capabilities, páginas, revisão, invalidação `pods`, comando e `failed_containers`). Evidência: suíte da API (160 aprovados), incluindo transporte.
- [x] T008 `PodActionService` e endpoints; OpenAPI e cliente; auditoria no console. Evidência: `PodActionServiceTests` (scope por ação, justificativa só para parar e pausar, pod do agente, sem capability); `CommandStore_PersistsPodActionAndFailedContainers`; OpenAPI e cliente; rótulos da Auditoria (40 ações).

## Fase 4: Console

- [x] T009 Tela Operação → Pods (lista, filtros na URL, resumo, detalhe) e pod no detalhe do container; Vitest. Evidência: `pods.spec.ts`; Vitest: 171 aprovados; bundle inicial 419,46 kB.
- [x] T010 Ações por pod e em lote (confirmação, justificativa, acompanhamento, retomada, containers que falharam); Vitest. Evidência: `pods.spec.ts` (elegibilidade com o pod do agente, lote com containers que falharam, justificativa).

## Fase 5: Verificação

- [x] T011 E2E simulada (`test_021_*.py`) com axe. Evidência: `run-mock-e2e.sh`: 99 aprovados, com `test_021_pods.py` (5) e axe sem violações sérias/críticas.
- [x] T012 Carga (SC-005) em PostgreSQL real. Evidência: SC-005: p95 de 16 ms com 1.008 pods em 56 hosts.
- [x] T013 Stack real Podman: quickstart, SC-001 a SC-004. Evidência: stack real Podman. SC-001: pods, situação e contagem de membros iguais ao `podman pod ps` (o Podman conta também o container de infraestrutura; o Dokpod mostra os membros sem ele) e `pod_dokpod` reconhecido como pod do agente; SC-002: pod parado pelo CLI aparece em cerca de 6 s; SC-003: parar `pod_dokpod` pela API → 409 `agent_pod`, pod em execução do começo ao fim; parar `pod-teste` (degradado) pela tela → concluído; iniciar com um container que sempre falha → `pod_partial_failure` com `pt-falha`; SC-004: 10 pods reiniciados em 4,0 s com 4 interações; auditoria `pod.stop`, `pod.start` e `pod.restart` com o ator. Pods e containers de teste removidos ao final.
- [x] T014 Documentação e evidências. Evidência: `docs/seguranca.md` (threat model) e `docs/frontend.md`.

## Achados da validação na stack real

- **Rotas libpod exigem o prefixo de versão**: `/libpod/...` responde 404 e `/v1.41/libpod/...` responde 200. O inventário de pods saía como "sem pods"; corrigido. O mesmo defeito afetava a leitura de `registries.conf` da spec 020 (já no `main`): o agente nunca chegava ao `/libpod/info` e caía no `/info` compatível, vazio no Podman. A correção vale para as duas specs; os testes passaram a exigir o caminho com versão.
- **Parar ou pausar pod degradado pela API libpod responde 304 sem agir** (o CLI para normalmente). O agente agora aplica a ação membro a membro quando recebe 304 em parar ou pausar.
- **Eventos `health_status` contínuos adiavam o envio do inventário de pods** (debounce de 2 s nunca terminava): mudanças externas só apareciam após minutos. O health check deixou de marcar os pods e o envio tem espera máxima de 5 s; medido em cerca de 6 s. O inventário de containers foi conferido no mesmo host (3 a 6 s) e não tem o problema.

## Atualização da spec 023 (2026-10-17)

- **Docker real**: o host Docker aparece como "sem pods"; o agente deixou de anunciar `PodActions` no Docker (só no Podman).
