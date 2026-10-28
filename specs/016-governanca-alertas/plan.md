# Plano de Implementação: Governança de alertas e parada justificada

**Branch**: `016-governanca-alertas` | **Data**: 2026-10-14 | **Spec**: [spec.md](spec.md)

**Estado**: `implemented` - plano derivado da spec aprovada em 2026-10-14 (ajuste: justificativa para parar, pausar e remover); executado e integrado em 2026-10-15 (PR #21).

## Resumo

Novo módulo de governança de alertas no plano de controle: regras (padrões semeados na migration), avaliação de eventos operacionais no momento da gravação, varredura periódica para condições observáveis (resolução automática e limites de CPU e memória), silenciamentos, histórico com retenção e auditoria. O console ganha a tela Governança → Alertas (Abertos, Histórico, Regras, Silenciamentos), o contador do menu e o sino passam a mostrar alertas abertos, e as ações parar, pausar e remover pedem justificativa conforme a política do estágio. A justificativa é persistida, recuperável por API e marca o container como parado ou pausado intencionalmente.

## Contexto Técnico

**Linguagem/Versão**: .NET 10, EF Core com scripts SQL embutidos, OpenAPI 3.1, Angular 22 (Signals).

**Dependências Principais**: Existentes; nenhuma nova.

**Armazenamento**: migration `202610140001_AlertGovernance` com `alert_rules`, `alerts`, `alert_timeline`, `alert_silences`, `action_justification_policies` e `container_action_justifications` no schema `dokpod`; regras padrão e política de Produção semeadas na migration.

**Testes**: domínio (regras, ciclo de vida, mascaramento), aplicação (avaliação, deduplicação, silenciamento, resolução, justificativa obrigatória), PostgreSQL real (stores, índice único de alerta aberto, rollback), Vitest, E2E simulada com axe e stack real (Linux, Podman).

**Plataforma-alvo**: API do plano de controle e console web; sem mudança no agente nem no protocolo gRPC.

**Metas de desempenho**: alerta visível em até 15 s (SC-001); lista de alertas abertos em até 2 s no p95 com 2.000 containers (SC-007).

**Restrições**: uma ocorrência aberta por regra e alvo (índice único parcial); justificativa fora do hash do payload do agente; nenhuma revelação de hosts não legíveis; réplica única da API (estado de duração dos limites em memória, como as métricas da spec 009).

**Escala/Escopo**: até 56 hosts e 2.000 containers; 9 regras padrão; retenção de 90 dias.

## Verificação da Constituição

| Princípio | Situação |
| --- | --- |
| I. Documentação em pt-BR | OK. |
| II. Engine local como fonte | OK: alertas derivam de eventos e amostras do agente; nenhuma ação automática no engine. |
| III. Identidade externa e menor privilégio | OK: scopes UMA novos `alert:read`/`alert:manage` por ambiente; política de justificativa exige `environment:manage` em todos os hosts do estágio (D-007). |
| IV. Operações duráveis | OK: justificativa gravada com chave por comando e reconciliada em nova tentativa idempotente (D-005); comandos seguem o pipeline durável existente. |
| V. Modularidade e compatibilidade | OK: campo `justification` opcional no comando (aditivo); clientes antigos recebem erro legível só onde a política exige; migration expand com Down. |
| VI. Qualidade verificável | OK: testes por camada, PostgreSQL real com rollback, E2E simulada e stack real. Docker real `NOT RUN`. |

Reavaliação após o desenho: sem violações.

## Estrutura do Projeto

```text
specs/016-governanca-alertas/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md
├── contracts/alerts-api.md
└── checklists/requirements.md

backend/libs/Dokpod.Domain/Alerts/                    # regra, alerta, silenciamento, justificativa (validação e mascaramento)
backend/libs/Dokpod.ControlPlane.Application/Alerts/  # avaliação, varredura, consultas, governança, justificativas
backend/libs/Dokpod.ControlPlane.Infrastructure/      # stores PostgreSQL + migration 202610140001_AlertGovernance
backend/apps/Dokpod.ControlPlane.Api/Alerts/          # endpoints, worker de varredura e retenção
backend/apps/Dokpod.ControlPlane.Api/Endpoints/ContainerCommandEndpoints.cs  # campo justification
contracts/openapi/dokpod-control-plane.v1.yaml
tools/scripts/configure-keycloak.ps1                  # scopes alert:read e alert:manage
frontend/web/src/app/features/alerts/                 # tela Alertas (4 vistas e drawers)
frontend/web/src/app/shell/                           # contador do menu e sino com alertas abertos
frontend/web/src/app/features/container-list/ e container-detail/  # diálogo com justificativa e rótulo intencional
frontend/tests/e2e/mock/ e mock_ui/test_016_*.py
```

## Rastreabilidade

| Requisito | Desenho |
| --- | --- |
| FR-001 a FR-005 | `AlertEvaluator` chamado por `OperationalEventService.AppendAsync`; `AlertSweeper` a cada 30 s (D-002, D-003). |
| FR-006, FR-007 | `GET /api/v1/alerts/summary` e lista; `InvalidationKinds.Alerts` no hub (D-006). |
| FR-008 | Filtro por hosts com `alert:read`; ações exigem `alert:manage` em todos os hosts do alvo. |
| FR-009 a FR-011 | Regras padrão semeadas; edição com versão e auditoria do antes/depois (D-004). |
| FR-012, FR-013 | `alert_silences`; avaliação consulta silenciamentos ativos (D-008). |
| FR-014 a FR-020 | Política por estágio; validação no `ContainerCommandService`; `container_action_justifications` (D-005, D-007). |
| FR-021 a FR-023 | Histórico paginado, retenção no worker, auditoria com ações novas. |

## Rastreio de Complexidade

| Item | Por que é necessário | Alternativa mais simples rejeitada |
| --- | --- | --- |
| Estado de duração dos limites em memória | Regras de limite exigem "acima de X por N minutos" | Persistir cada amostra (escrita contínua; a spec 009 já evita isso) |
