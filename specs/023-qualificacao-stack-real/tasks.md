# Tarefas: Qualificação na stack real

**Entrada**: [spec.md](spec.md), [plan.md](plan.md).

**Estado**: `in-progress` - execução iniciada em 2026-10-17.

- [ ] T001 Perfil `docker-real` no stack de teste (Docker 28 aninhado + agente) e documentação em `deploy/e2e/README.md`.
- [ ] T002 [US1] Docker real: inventário (containers, imagens, volumes, redes, registros, Pods "sem pods") igual ao `docker` CLI; ações, lote, pausa, logs, inspect, métricas, terminal, criação e pull pelo console.
- [ ] T003 [US2] N-1: imagens do commit `2867c25`; agente N-1 ↔ servidor N e agente N ↔ servidor N-1 por 10 minutos com inventário e comandos.
- [ ] T004 [US3] Usuário só de leitura na stack real: menu, telas e recusas da API.
- [ ] T005 [US3] Registro privado com credencial: pull e criação; sem credencial, falha legível; varredura da credencial.
- [ ] T006 [US4] Canários ponta a ponta (variável, label, health check, opção de log, termo de busca) em respostas, PostgreSQL e logs.
- [ ] T007 [US4] Terminal real pelo navegador: prompt, eco, encerramento por fechamento, ociosidade e revogação; conteúdo digitado fora da auditoria.
- [ ] T008 Correções de defeitos encontrados, com testes.
- [ ] T009 Evidências nas specs de origem, lista final de `NOT RUN` com destino (SC-004), limpeza do laboratório.
