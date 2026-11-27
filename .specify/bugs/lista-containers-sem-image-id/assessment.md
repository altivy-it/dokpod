# Avaliação de Bug: lista de containers mostra container_behind para container em dia

- **Slug**: lista-containers-sem-image-id
- **Criada em**: 2026-11-27
- **Origem**: texto colado (achado T028-1 de `specs/025-aviso-nova-versao-imagem/validation.md`, seção "Agentes reais (T028)")
- **Veredito**: valid
- **Severidade**: medium

## Relato (literal ou resumido)

> GET /api/v1/containers mostra updateCheck.state=container_behind para todo container cuja referência está
> up_to_date, nos dois engines (Docker 29.9.3 e Podman 6.2.0), enquanto GET …/containers/{id}/summary mostra
> up_to_date. Causa provável: a consulta da lista em PostgresContainerSearchStore.cs não seleciona c.image_id, e
> ImageUpdateCheck.StateForContainer compara null com local_image_id. Autorizado pelo solicitante em 2026-11-27
> ("Bug Fixing: sim").

## Sintoma

Na tela Containers (e em `GET /api/v1/containers`), todo container cuja referência foi verificada como `up_to_date`
aparece como `container_behind` ("versão nova no host; o container usa a anterior"). O esperado, e o que o detalhe do
mesmo container mostra, é `up_to_date`.

## Reprodução

1. Imagem Business com licença que liste `image-update-notice` e agente com `CAPABILITY_IMAGE_UPDATE_CHECK`.
2. Criar um container com uma tag de registro permitido e acionar "Verificar agora".
3. `GET /api/v1/containers?environmentId=<id>`: `updateCheck.state = container_behind`.
4. `GET /api/v1/environments/<id>/containers/<containerId>/summary`: `updateCheck.state = up_to_date`.

Reproduzido no quality em 2026-11-27 (T028), com `t028-web`, `t028-proxy` e `t028-removida` nos dois engines.

## Caminhos de Código Suspeitos

- `backend/libs/Dokpod.ControlPlane.Infrastructure/PostgresContainerSearchStore.cs:59` — o `SELECT` da busca lê 14
  colunas, sem `c.image_id`; a entidade montada à mão (linhas 75–90) fica com `ImageId = null`.
- `backend/libs/Dokpod.Domain/Images/ImageUpdateCheck.cs:95` — `StateForContainer` devolve `container_behind` quando a
  verificação é `up_to_date`, `LocalImageId` não é nulo e o `imageId` do container é diferente, inclusive nulo.
- `backend/apps/Dokpod.ControlPlane.Api/Environments/ConsoleEndpoints.cs:99` — passa `item.Container.ImageId` (nulo)
  para `WithUpdateCheck`.
- `PostgresContainerSearchStore.GetAsync` (linha 139), usado no detalhe, lê a entidade inteira pelo EF e por isso acerta.

## Hipótese de Causa Raiz

A coluna `inventory_containers.image_id` foi acrescentada pela spec 012 e é gravada pela projeção
(`PostgresInventoryProjectionStore.cs:278`), mas a consulta SQL de keyset da lista não foi atualizada. Sem o `image_id`,
a derivação D-008 trata todo container em dia como atrasado. Os testes da 025 (`ImageUpdateReadTests`) chamam
`ForContainer` com o `imageId` explícito e não passam pela consulta da lista. Confiança: high.

## Correção Proposta

**Preferencial**: incluir `c.image_id` no `SELECT` de `PostgresContainerSearchStore.SearchAsync`, ler a coluna
(nulável) e preencher `ImageId` na entidade, mantendo a ordem das colunas existentes e o `sort_key` como último índice.

**Alternativas**:
- Tratar `imageId` nulo como "desconhecido" em `StateForContainer` (devolver o estado da referência). É defensivo para
  agentes N-1 sem `ImageID`, mas sozinho esconde o defeito da consulta; pode acompanhar a preferencial.

**Arquivos Prováveis de Alteração**:
- `backend/libs/Dokpod.ControlPlane.Infrastructure/PostgresContainerSearchStore.cs`
- `backend/tests/Dokpod.ControlPlane.Api.Tests/ContainerSearchIntegrationTests.cs` (ou um teste novo em `ImageUpdates/`)
- `backend/libs/Dokpod.Domain/Images/ImageUpdateCheck.cs` e testes de domínio, se a alternativa for adotada

**Testes a Adicionar ou Atualizar**:
- Integração com PostgreSQL real: projeção de um container com `ImageId`, busca pela lista e conferência de que a linha
  devolvida traz o mesmo `ImageId`.
- Integração ponta a ponta da leitura: resultado `up_to_date` com `local_image_id` igual ao `image_id` do container e
  lista de containers anotada com `up_to_date` (não `container_behind`).

## Riscos e Considerações

- Sem migração: a coluna já existe. Só muda o `SELECT`.
- Desempenho (SC-004 da 025): uma coluna `varchar(160)` a mais na página; impacto desprezível, mas a medição de T025 é
  a referência se houver dúvida.
- Agentes N-1 sem `ImageID` continuam com `image_id` nulo e cairiam em `container_behind`; avaliar a alternativa.
- Os alertas `image_outdated` (`ImageOutdatedSource`) usam `IImageUpdateTargetReader`, outra consulta; não foram
  afetados pela evidência da T028, mas a correção deve confirmar que leem `image_id`.

## Perguntas em Aberto

- Nenhuma.
