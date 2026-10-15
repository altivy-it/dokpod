# Tarefas: Registros

**Entrada**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/registries-api.md), [quickstart.md](quickstart.md).

**Estado**: `in-progress` - implementação iniciada em 2026-10-15.

## Fase 1: Contrato e domínio

- [ ] T001 Proto aditivo (D-005) e tabela de alocação.
- [ ] T002 Domínio: gramática e normalização de registro, auditoria `registry.policy_changed`; testes.

## Fase 2: Agente

- [ ] T003 Engine: configuração de registros de `/info` (Docker) e `/libpod/info` (Podman); nomes de registros do arquivo de credenciais sem valores; testes.
- [ ] T004 Publicação por capability (sessão e mudança); testes.

## Fase 3: Plano de controle

- [ ] T005 Migration `202610150005_RegistryInventory` com Down; store da configuração e da política; testes em PostgreSQL real.
- [ ] T006 Sessão gRPC (capability, mensagem, invalidação `registries`); consolidação com as imagens; política com sobreposição recarregada no pull e na criação.
- [ ] T007 Endpoints de lista, detalhe, política, impacto e edição; OpenAPI e cliente; auditoria no console.

## Fase 4: Console

- [ ] T008 Tela Infraestrutura → Registros (lista, filtros na URL, resumo, detalhe com link para Imagens); Vitest.
- [ ] T009 Vista de política por estágio (leitura, edição com impacto e confirmação); Vitest.

## Fase 5: Verificação

- [ ] T010 E2E simulada (`test_020_*.py`) com axe.
- [ ] T011 Carga (SC-004) em PostgreSQL real.
- [ ] T012 Stack real Podman: quickstart, SC-001 a SC-003.
- [ ] T013 Documentação e evidências.
