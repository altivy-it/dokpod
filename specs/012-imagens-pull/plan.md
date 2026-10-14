# Plano de Implementação: Inventário de Imagens e Download (Pull)

**Branch**: `007-console-operacional` | **Data**: 2026-10-12 | **Spec**: [spec.md](spec.md)

**Estado**: `draft` - aguardando revisão humana.

## Resumo

Estender o protocolo com snapshot/delta de imagens e com o primeiro comando **parametrizado** (`PULL_IMAGE` com referência). A parametrização de comandos é desenhada aqui de forma reutilizável pela spec 013 (`oneof parameters` em `AgentCommand`, hash canônico incluindo parâmetros, parâmetros persistidos com o comando). API ganha listagem de imagens e submissão de pull por host; frontend ganha tela Imagens e drawer "Baixar imagem".

## Contexto Técnico

**Linguagem/Versão**: .NET 10, Protobuf v1, OpenAPI 3.1, Angular 22.

**Dependências Principais**: Existentes.

**Armazenamento**: Novas tabelas `inventory_images` (projeção por host) e `agent_command_parameters` (parâmetros do comando, 1:1 com `agent_commands`, com particionamento alinhado ao de `agent_commands`).

**Testes**: Domínio (validação de referência OCI, hash canônico com parâmetros), PostgreSQL real, Docker/Podman reais com registro local de laboratório (inclusive privado autenticado no host), contrato N/N-1, Playwright.

**Restrições**: Sem credenciais no plano de controle; idempotência por chave e hash `v2` de parâmetros (sem prazo); prazo longo e reconciliação de pull indeterminado; páginas de imagens ≤ 256 KiB (limite gRPC de 1 MiB).

## Verificação da Constituição

| Item | Situação |
| --- | --- |
| Operações privilegiadas / novas fronteiras exigem threat model e testes negativos | Pendente: threat model do pull (D-009) aprovado em T000; suíte negativa em T015. |
| IV. Comandos duráveis | OK: pull usa o pipeline; resultado indeterminado reconciliado por digest ou por data da tag após o aceite (D-003). |
| Schemas expand-contract | OK: colunas de `agent_commands` passam a aceitar nulo com `CHECK` por tipo; `image_id` anulável. |
| III. Secrets fora | OK: credenciais permanecem no host (`~/.docker/config.json`/`auth.json` do Podman). |
| Capabilities distintas | OK: `CAPABILITY_IMAGE_INVENTORY`, `CAPABILITY_IMAGE_PULL`. |
| Tabelas de alto crescimento | `agent_commands` (hoje sem retenção) e `agent_command_parameters` particionadas por `created_at_utc` com retenção pela rotina genérica da 008 (D-008). |

## Estrutura do Projeto

```text
contracts/agent/v1/agent.proto        # ImageSnapshotPage(21), ImageDelta(22), PULL_IMAGE(7), AgentCommand.parameters
contracts/openapi/...                 # /images, /environments/{id}/images/pull, image:pull
backend/libs/Dokpod.Domain/Images/    # ImageReference (parse/validação), ObservedImage
backend/libs/Dokpod.Domain/Commands/  # parâmetros de comando + hash canônico
backend/libs/Dokpod.ControlPlane.Application/Images/  # ImageProjectionService, ImageQueryService, ImagePullService
backend/libs/Dokpod.ControlPlane.Infrastructure/      # migrations inventory_images, agent_command_parameters; stores
backend/apps/Dokpod.ControlPlane.Api/Images/ImageEndpoints.cs
backend/libs/Dokpod.Agent.Application/Images/         # snapshot/delta de imagens; executor de pull
backend/libs/Dokpod.Agent.Infrastructure/Engines/{Docker,Podman}/  # /images/json, /images/create
frontend/web/src/app/features/images/                 # images.page, pull-image.drawer, images.facade
```

## Fases

1. Contratos (imagens + parâmetros de comando). 2. Domínio. 3. Persistência. 4. Agente. 5. API. 6. Frontend. 7. Verificação com registro de laboratório.
