# Plano de Implementação: Lista Unificada de Containers, Pausa e Ações em Lote

**Branch**: `007-console-operacional` | **Data**: 2026-10-12 | **Spec**: [spec.md](spec.md)

**Estado**: `draft` - aguardando revisão humana.

## Resumo

Backend: novo endpoint `GET /api/v1/containers` sobre a projeção de inventário (join com ambientes e descritor; coluna nova `display_status`; métricas do cache em memória da 009 anexadas à página, sem ordenação no servidor), com autorização por host resolvida antes da consulta; ações `pause`/`unpause` adicionadas de ponta a ponta (OpenAPI, domínio de comandos, proto, agente Docker/Podman, capability). Frontend: nova feature `containers` com `dok-data-grid`, fachada de lote que reutiliza `submitContainerCommand`/`getContainerCommand`, e substituição da tela atual.

## Contexto Técnico

**Linguagem/Versão**: .NET 10, Angular 22, OpenAPI 3.1, Protobuf v1.

**Dependências Principais**: Existentes.

**Armazenamento**: Projeção existente; colunas novas `display_status` e `ports_text` em `inventory_containers`; índices (`environment_id, name`), (`display_status`), (`created_at`); busca por `ILIKE` com mínimo de 2 caracteres (`pg_trgm` só se SC-001 falhar, com aprovação). Constraint `ck_agent_commands_kind` ampliada para 1-6.

**Testes**: Aplicação (filtros, cursor, autorização), PostgreSQL real (desempenho SC-001), contrato, agente com engines reais (pause/unpause), Playwright (lote, recarga, remoção reforçada), visual.

**Restrições**: Comandos individuais e auditados; sem lote no agente; foco/ordem estáveis em tempo real.

## Verificação da Constituição

| Princípio | Situação |
| --- | --- |
| IV. Comandos com autorização, ID idempotente, expiração, auditoria e reconciliação | OK: pausa/retomada reutilizam o pipeline; lote = N comandos. |
| Capabilities distintas Docker/Podman | OK: `CAPABILITY_CONTAINER_PAUSE`/`UNPAUSE` anunciadas só após teste real. |
| V. Compatibilidade | OK: enum `action` expandido; servidor só despacha pausa a agentes que anunciaram a capability; agente N-1 recusa explicitamente; testes nos dois sentidos. |
| III. Identidade externa | OK: scope `container:pause` provisionado no Keycloak; autorização por host via `IEnvironmentAuthorizationDecider` existente. |
| Exclusão sem volume implícito | OK: inalterado e explicitado na UI. |

## Estrutura do Projeto

```text
contracts/openapi/dokpod-control-plane.v1.yaml     # GET /containers; action pause/unpause; scope container:pause
contracts/agent/v1/agent.proto                     # CommandKind 5/6; Capability 8/9
backend/libs/Dokpod.Domain/Commands/AgentCommand.cs              # Pause/Unpause, gates
backend/libs/Dokpod.ControlPlane.Application/Inventory/ContainerSearchService.cs   # novo
backend/libs/Dokpod.ControlPlane.Application/Commands/ContainerCommandService.cs   # novas ações/scope
backend/libs/Dokpod.ControlPlane.Infrastructure/PostgresInventoryProjectionStore.cs # busca multi-host + índices
backend/apps/Dokpod.ControlPlane.Api/Environments/InventoryEndpoints.cs             # GET /api/v1/containers
backend/libs/Dokpod.Agent.Application/Protocol/AgentCommandMapper.cs                # pause/unpause
backend/libs/Dokpod.Agent.Infrastructure/Engines/{Docker,Podman}/                   # POST /containers/{id}/pause|unpause
frontend/web/src/app/features/containers/
├── containers.page.*            # substitui container-list.page.*
├── containers.facade.ts         # listagem, filtros, cursor, invalidação
├── batch-actions.facade.ts      # lote, idempotência persistida, acompanhamento
└── confirm-batch.dialog.*       # confirmação contextual/reforçada
```

## Fases

1. Contratos e scope no Keycloak. 2. Domínio e aplicação (pausa, busca). 3. Persistência (`display_status`, índices, desempenho). 4. Agente (pausa/retomada Docker/Podman). 5. API. 6. Frontend (lista, filtros, lote, linha, exportação, redirecionamento). 7. Verificação.
