# Plano de Implementação: Qualificação na stack real

**Branch**: `023-qualificacao-stack-real` | **Data**: 2026-10-17 | **Spec**: [spec.md](spec.md)

**Estado**: `approved` - plano derivado da spec aprovada em 2026-10-17.

## Resumo

Executar na stack real (Keycloak, BFF, API, PostgreSQL, agentes) as verificações `NOT RUN` das specs 007 a 022, com um perfil novo de Docker real aninhado no Podman, agente e servidor N-1 construídos do histórico, usuário só de leitura e registro privado temporários; corrigir defeitos com teste; registrar evidência na spec de origem.

## Contexto Técnico

- **Docker real**: perfil `docker-real` em `deploy/e2e/docker-compose-dokpod.yaml`: serviço `docker-engine` (`docker:28-dind`, privilegiado, volumes nomeados para `/var/lib/docker` e para o socket) e `agent-docker` com a identidade do perfil `agent` existente (D-001).
- **N-1**: commit `2867c25` (spec 020 em `main`, anterior a pods e Configurações): imagens `dokpod-agent:n1` e `dokpod-api:n1` construídas de uma worktree descartável (D-002).
- **Usuário só de leitura**: usuário temporário `dokpod-leitor` no realm, com `environment:read` em todos os hosts (sem `environment:manage`), criado e removido pela API de administração do Keycloak (D-003).
- **Registro privado**: `registry:2` com htpasswd em rede do stack, credencial de teste gerada na hora e montada só durante a validação (D-004).
- **Varreduras**: canários gerados por execução, procurados em respostas, PostgreSQL (`pg_dump` do schema `dokpod`) e logs de nginx, API, BFF e agentes.

## Verificação da Constituição

| Princípio | Situação |
| --- | --- |
| I. Documentação em pt-BR | OK. |
| II. Engine local como fonte | OK: comparação com o CLI de cada engine. |
| III. Identidade externa e menor privilégio | OK: usuário de teste com só leitura; credenciais temporárias fora do Git. |
| IV. Operações duráveis | OK: comandos pelos fluxos existentes. |
| V. Modularidade e compatibilidade | OK: SC-002 valida N/N-1 nos dois sentidos. |
| VI. Qualidade verificável | OK: é o objetivo desta spec. |
