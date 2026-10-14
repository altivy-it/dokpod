# Plano de Implementação: Criação de Container

**Branch**: `007-console-operacional` | **Data**: 2026-10-12 | **Spec**: [spec.md](spec.md)

**Estado**: `implemented` - concluída em 2026-10-14; revisão humana de segurança antes de release; evidências e itens `NOT RUN` em [tasks.md](tasks.md).

## Resumo

Novo comando parametrizado `CREATE_CONTAINER` (reaproveitando `AgentCommand.parameters` da 012) com especificação em allowlist, validada na API e no agente. Valores sensíveis são cifrados com ASP.NET Core Data Protection em key ring próprio da API (hoje só o BFF configura Data Protection, em `BffHost.cs`), persistido em volume protegido conforme D-006, enquanto o comando está ativo e removidos depois; reentrega após reinício do agente usa esses valores enquanto não terminal (D-002). O agente executa pull-se-necessário → create → start com label de rastreio, reporta o id do container e reconcilia por label. Consultas auxiliares (segredos, redes, volumes do host) usam o mecanismo de consulta sob demanda da 011. Frontend: drawer conforme board 04.

## Contexto Técnico

**Linguagem/Versão**: .NET 10, Protobuf v1, OpenAPI 3.1, Angular 22 (Reactive Forms com componentes `dok-field`/`dok-select`/`dok-checkbox`).

**Dependências Principais**: Existentes; ASP.NET Core Data Protection já faz parte do framework (sem pacote novo, salvo provider de persistência de chaves).

**Armazenamento**: `agent_command_parameters` (012) com campo `protected_values` cifrado e `purged_at`; `ck_agent_commands_kind` ampliada para `BETWEEN 1 AND 8` e `CHECK` de alvo de host cobrindo o kind 8 (migration expand); key ring da API em volume protegido (D-006).

**Testes**: Domínio (validação de allowlist, limites, geração do comando equivalente com redaction), aplicação (cifragem/purga, idempotência, hash), agente (tradução para `/containers/create` Docker e compatível Podman, rollback de conflito de porta, reconciliação por label, resolução de segredos), PostgreSQL real, canários, Playwright.

**Restrições**: Sem opções privilegiadas; sem secrets em repouso após terminal; journal do agente não guarda valores sensíveis (guarda hash e referência).

## Verificação da Constituição

| Item | Situação | Observação |
| --- | --- | --- |
| Arquitetura: "suporte futuro a criação declarativa" é decisão adiada | OK após T001 | Aprovação de 2026-10-13 (specs/README.md); T001 registra em `docs/arquitetura.md` o encerramento só para container único; Compose/stacks continuam adiados. |
| II. Excluir container não remove volume implicitamente | OK | Remoção automática recusada quando a imagem declara `VOLUME` sem volume nomeado no destino (FR-011). |
| III. Secrets fora do repositório e da telemetria | OK | Cifragem efêmera + purga; segredos do host por referência. |
| IV. Comandos duráveis | OK | Journal sem valores sensíveis; reconciliação por label `dokpod.command-id`; reentrega após reinício com valores protegidos ou `parameters_unavailable` (FR-012). |
| VI. Plataforma | OK | Docker Linux e Podman Linux qualificados separadamente; Windows fora desta entrega (capability não anunciada). |
| Operações privilegiadas exigem threat model | Pendente | Tarefa T002. |
| Capabilities distintas | OK | `CAPABILITY_CONTAINER_CREATE`; Podman rootless com limitações explícitas. |

## Estrutura do Projeto

```text
contracts/agent/v1/agent.proto              # CreateContainerParameters (AgentCommand.parameters = 8), CREATE_CONTAINER (8), capability 14; consultas HOST_SECRETS/NETWORKS/VOLUMES
contracts/openapi/...                       # POST /environments/{id}/containers; GET /environments/{id}/create-options
backend/libs/Dokpod.Domain/Containers/ContainerCreationSpec.cs   # allowlist, limites, validação
backend/libs/Dokpod.Domain/Containers/EquivalentCommandRenderer.cs
backend/libs/Dokpod.ControlPlane.Application/Containers/ContainerCreationService.cs
backend/libs/Dokpod.ControlPlane.Application/Commands/SensitiveParameterProtector.cs   # cifra/purga
backend/libs/Dokpod.ControlPlane.Infrastructure/ (migration: protected_values, purged_at; job de purga)
backend/apps/Dokpod.ControlPlane.Api/Containers/ContainerCreationEndpoints.cs
backend/libs/Dokpod.Agent.Application/Containers/ContainerCreator.cs   # pull→create→start, rollback, reconciliação
backend/libs/Dokpod.Agent.Application/Secrets/HostSecretResolver.cs    # podman secret / arquivo protegido
backend/libs/Dokpod.Agent.Infrastructure/Engines/{Docker,Podman}/
frontend/web/src/app/features/containers/new-container.drawer.*, new-container.facade.ts
```

## Fases

1. Decisão humana + threat model (bloqueante). 2. Contratos. 3. Domínio (allowlist, renderer). 4. Proteção de parâmetros. 5. Agente. 6. API. 7. Frontend. 8. Testes de segurança/canários e E2E.

## Riscos

| Risco | Mitigação |
| --- | --- |
| Criar container = execução de código arbitrário no host | Scope dedicado, allowlist sem privilégios, registros permitidos por estágio, auditoria com comando equivalente, threat model. |
| Vazamento de valores sensíveis | Cifragem, purga, redaction, canários. |
| Divergência Docker x Podman (health check, rootless, segredos) | Adapters separados, capabilities e testes reais; `health-on-failure=restart` só no Podman via Libpod. |
| Perda do key ring da API | Comandos não terminais falham com `parameters_unavailable`; nenhum valor é recuperável fora da API (D-006). |
