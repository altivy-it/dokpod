# Correção de Bug: lista de containers mostra container_behind para container em dia

- **Slug**: lista-containers-sem-image-id
- **Corrigido em**: 2026-11-27
- **Avaliação**: ./assessment.md
- **Estado**: applied

## Resumo

A consulta de keyset da lista de containers passou a ler `inventory_containers.image_id`. Com o `ImageId` preenchido, a
derivação D-008 compara a imagem real do container com `local_image_id` e o container em dia volta a aparecer como
`up_to_date`.

## Alterações

| Arquivo | Alteração | Notas |
|------|--------|-------|
| `backend/libs/Dokpod.ControlPlane.Infrastructure/PostgresContainerSearchStore.cs` | modificado | `c.image_id` no `SELECT` (antes do `sort_key`, que passa ao índice 15) e `ImageId` na entidade |
| `backend/tests/Dokpod.ControlPlane.Api.Tests/ContainerSearchIntegrationTests.cs` | teste adicionado | PostgreSQL real |

## Testes Adicionados ou Atualizados

- `ContainerSearchIntegrationTests.SearchStore_ReturnsTheContainerImageId_SoAnUpToDateContainerIsNotBehind` — a busca da
  lista e o `GetAsync` devolvem o mesmo `ImageId` gravado pela projeção, e uma verificação `up_to_date` com esse
  `local_image_id` resulta em `up_to_date` para a linha da lista.

## Verificação Local

- `dotnet test Dokpod.slnx --filter 'FullyQualifiedName~ContainerSearch|FullyQualifiedName~ImageUpdate|FullyQualifiedName~SqlCatalog|FullyQualifiedName~ExpectedSchema'`
  no SDK `10.0.403-noble-v1`, com PostgreSQL `17.6-bookworm` descartável e migrations aplicadas → 115 aprovados, 1
  ignorado (medição isolada do SC-004), nenhuma falha.

- Suíte backend completa (`dotnet test Dokpod.slnx`, mesma base descartável) com as duas correções da T028 aplicadas →
  1 345 aprovados, 9 ignorados por variável de ambiente, nenhuma falha; `dotnet format --verify-no-changes` nos arquivos
  alterados sem apontamentos.

## Desvios da Avaliação

- A alternativa (tratar `imageId` nulo como desconhecido em `StateForContainer`) não foi adotada: mudaria o
  comportamento para agentes N-1 sem decisão da spec. Fica como acompanhamento.

## Acompanhamento

- Repetir o passo 2 do cenário 1 da T028 no quality depois da publicação.
- Avaliar, na spec 025, o comportamento de `container_behind` para agentes N-1 sem `ImageID`.
