# Plano de Implementação: Pods

**Branch**: `021-pods` | **Data**: 2026-10-16 | **Spec**: [spec.md](spec.md)

**Estado**: `implemented` - plano derivado da spec aprovada em 2026-10-16; executado e integrado em 2026-10-16 (PR #26).

## Resumo

Inventário de pods do Podman publicado pelo agente pela API libpod (capability nova, snapshots paginados com revisão no heartbeat, como volumes e redes), com o "pod do agente" identificado pelo próprio container; projeção no plano de controle; tela Operação → Pods com detalhe e ações de pod (iniciar, parar, reiniciar, pausar, retomar) por comando durável com os scopes de container, justificativa para parar e pausar e recusa no pod do agente.

## Contexto Técnico

**Linguagem/Versão**: .NET 10, protobuf v1 (aditivo), EF Core com scripts SQL, OpenAPI 3.1, Angular 22.

**Armazenamento**: migration `202610160001_PodInventory`: `inventory_pod_projections`, `inventory_pods` (membros em `jsonb`), comando tipo 11 com parâmetro `pod_action` e coluna `failed_containers` em `agent_commands`.

**Testes**: domínio (ações, scopes, hash), agente (libpod, ausência no Docker, pod do agente, ação parcial), PostgreSQL real (projeção, busca, carga de 1.000 pods), aplicação (scopes, justificativa, recusa do pod do agente), Vitest, E2E simulada e stack real (Podman, com pods de teste; o pod do Dokpod só é usado para conferir a recusa).

**Restrições**: nunca parar, pausar ou reiniciar o pod do agente; sem remoção nem criação de pods; hosts Docker sem pods.

## Verificação da Constituição

| Princípio | Situação |
| --- | --- |
| I. Documentação em pt-BR | OK. |
| II. Engine local como fonte | OK: pods vêm do Podman pelo agente; ações só pelo agente, que confere o pod do agente de novo. |
| III. Identidade externa e menor privilégio | OK: scopes de container por host (decisão do Keycloak); leitura com `environment:read`. |
| IV. Operações duráveis | OK: ações pelo pipeline de comandos (journal-before-ack, idempotência, reconciliação). |
| V. Modularidade e compatibilidade | OK: mensagens e enums novos; agente N-1 e Docker ficam sem pods; servidor N-1 ignora a capability. |
| VI. Qualidade verificável | OK: testes por camada, E2E simulada e stack real Podman. |

## Rastreabilidade

| Requisito | Desenho |
| --- | --- |
| FR-001, FR-002 | `PodSnapshotPage`, `Heartbeat.pod_revision`, capability `POD_INVENTORY`, identificação do próprio container (D-001, D-002). |
| FR-003, FR-004 | `IPodInventoryStore`, endpoints de lista e detalhe, filtro por container (D-003). |
| FR-005 a FR-008 | `CommandKind.POD_ACTION` + `PodActionParameters`, capability `POD_ACTIONS`, `PodActionService`, `CommandResult.failed_containers` (D-004). |
| FR-009 | Rota `/pods`. |
