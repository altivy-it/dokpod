# Plano de Implementação: Auditoria

**Branch**: `017-auditoria` | **Data**: 2026-10-15 | **Spec**: [spec.md](spec.md)

**Estado**: `approved` - plano derivado da spec aprovada em 2026-10-15.

## Resumo

Consulta da trilha de auditoria existente (spec 001) pelo console: API de leitura paginada por período com filtros, detalhe com eventos relacionados, resumo de acesso para o menu e exportação CSV auditada. Eventos novos passam a guardar o nome exibível do ator, preenchido num único ponto (o writer de auditoria). O console ganha Governança → Auditoria e o atalho "Ver auditoria" no detalhe do container.

## Contexto Técnico

**Linguagem/Versão**: .NET 10, EF Core com scripts SQL embutidos, OpenAPI 3.1, Angular 22 (Signals).

**Dependências Principais**: existentes; nenhuma nova.

**Armazenamento**: migration `202610150001_AuditConsole`: coluna `actor_name` em `audit_events`, nova sobrecarga de `dokpod_append_audit_event` com o nome e índice de consulta por período (D-005). A tabela continua append-only (gatilho de rejeição de mutação existente).

**Testes**: domínio (nome do ator, detalhes da exportação), PostgreSQL real (gravação com nome, consulta com filtros e cursor, carga de 1 milhão de eventos), aplicação (filtro por `audit:read`, relacionados, exportação auditada), Vitest, E2E simulada com axe e stack real.

**Plataforma-alvo**: API do plano de controle e console web; sem mudança no agente nem no protocolo.

**Metas de desempenho**: primeira página em até 2 s no p95 com 1 milhão de eventos em 56 hosts (SC-003); exportação de 10.000 eventos em até 10 s (SC-005).

**Restrições**: período obrigatório de até 90 dias; nenhuma revelação de hosts sem `audit:read`; trilha imutável; detalhes exibidos como gravados (já mascarados).

**Escala/Escopo**: 56 hosts, milhões de eventos, 10.000 eventos por exportação.

## Verificação da Constituição

| Princípio | Situação |
| --- | --- |
| I. Documentação em pt-BR | OK. |
| II. Engine local como fonte | OK: só leitura da trilha do plano de controle. |
| III. Identidade externa e menor privilégio | OK: `audit:read` por host decidido no Keycloak; sem scope novo. |
| IV. Operações duráveis | OK: exportação registrada como evento de auditoria. |
| V. Modularidade e compatibilidade | OK: coluna nova anulável e sobrecarga nova da função (a anterior continua válida); Down remove a sobrecarga e o índice e mantém a coluna, para não apagar evidência. |
| VI. Qualidade verificável | OK: testes por camada, PostgreSQL real, E2E simulada e stack real. |

Reavaliação após o desenho: sem violações.

## Estrutura do Projeto

```text
specs/017-auditoria/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md
├── contracts/audit-api.md
└── checklists/requirements.md

backend/libs/Dokpod.Domain/Auditing/                     # nome do ator, ação audit.exported, categorias
backend/libs/Dokpod.ControlPlane.Application/Auditing/   # consulta, relacionados, exportação, provedor do nome do ator
backend/libs/Dokpod.ControlPlane.Infrastructure/         # leitor PostgreSQL, writer com nome + migration 202610150001
backend/apps/Dokpod.ControlPlane.Api/Auditing/           # endpoints e provedor do nome a partir do token
contracts/openapi/dokpod-control-plane.v1.yaml
frontend/web/src/app/features/audit/                     # tela Auditoria e drawer de detalhe
frontend/web/src/app/shell/navigation.ts                 # item de menu pela API
frontend/web/src/app/features/container-detail/          # atalho "Ver auditoria"
frontend/tests/e2e/mock/ e mock_ui/test_017_*.py
```

## Rastreabilidade

| Requisito | Desenho |
| --- | --- |
| FR-001 a FR-004 | `GET /api/v1/audit/events` com cursor e filtros; `GET /api/v1/audit/events/{eventId}` com relacionados (D-002, D-003). |
| FR-005 | Detalhes devolvidos como gravados; nenhuma rota de desmascaramento. |
| FR-006 | Link do detalhe do container com `environmentId` e `q=<containerId>`. |
| FR-007 | Coluna `actor_name`, sobrecarga da função e `IAuditActorNameProvider` no writer (D-001). |
| FR-008, FR-009 | `GET /api/v1/audit/export` (CSV, até 10.000) e evento `audit.exported` por host (D-004). |
| FR-010, FR-012 | Decisão `audit:read` por host; `GET /api/v1/audit/summary` decide o menu (D-003, D-006). |
| FR-011 | Sem rotas de escrita; gatilho existente rejeita `UPDATE`/`DELETE`. |
