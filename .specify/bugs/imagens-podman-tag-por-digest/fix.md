# Correção de Bug: Imagens do Podman escolhe a referência por digest como tag da imagem

- **Slug**: imagens-podman-tag-por-digest
- **Corrigido em**: 2026-11-27
- **Avaliação**: ./assessment.md
- **Estado**: applied

## Resumo

O estado da imagem e a tag exibida deixam de depender da ordem de `RepoTags`: as referências com tag vêm antes das
referências por digest que o Podman também lista ali. A linha da imagem passa a mostrar a tag e o estado da tag nos dois
engines.

## Alterações

| Arquivo | Alteração | Notas |
|------|--------|-------|
| `backend/libs/Dokpod.Domain/Images/ImageReference.cs` | modificado | `ImageReference.IsDigestOnly`, regra compartilhada pela CE e pelo módulo Business |
| `backend/libs/Dokpod.ControlPlane.Application/ImageUpdates/ImageUpdateReadService.cs` | modificado | `ForImage` percorre as tags antes das referências por digest (ordenação estável) |
| `backend/apps/Dokpod.ControlPlane.Api/Environments/ImageEndpoints.cs` | modificado | `ToItem` ordena `repoTags` com a mesma regra e tira `repository`/`tag` da primeira |
| `backend/tests/Dokpod.ControlPlane.Api.Tests/ImageUpdates/ImageUpdateReadTests.cs` | teste adicionado | PostgreSQL real |

## Testes Adicionados ou Atualizados

- `ImageUpdateReadTests.ImageWithTagAndDigestReferences_TakesTheTagState_InEitherRepoTagsOrder` — nas duas ordens, a
  imagem fica com o estado da tag; só com a referência por digest, `digest_reference`; a ordenação usada pela lista põe a
  tag primeiro, e `repo:tag@digest` não conta como referência só por digest.

## Verificação Local

- `dotnet test Dokpod.slnx --filter 'FullyQualifiedName~ContainerSearch|FullyQualifiedName~ImageUpdate|FullyQualifiedName~SqlCatalog|FullyQualifiedName~ExpectedSchema|FullyQualifiedName~ControlPlaneHttpSurface|FullyQualifiedName~Image'`
  com PostgreSQL `17.6-bookworm` descartável → 195 aprovados, 2 ignorados por variável de ambiente, nenhuma falha.

- Suíte backend completa (`dotnet test Dokpod.slnx`, mesma base descartável) com as duas correções da T028 aplicadas →
  1 345 aprovados, 9 ignorados por variável de ambiente, nenhuma falha; `dotnet format --verify-no-changes` nos arquivos
  alterados sem apontamentos.

## Desvios da Avaliação

- A regra ficou em `Dokpod.Domain` (`ImageReference.IsDigestOnly`), não em `ImageUpdateLookup`: `ImageEndpoints` é
  código da CE e `BusinessGateTests.CommunityCode_DoesNotReferenceTheLicensingModule` proíbe a referência ao módulo
  `ImageUpdates`.
- Escopo ampliado: além de `repository`/`tag`, a resposta passa a ordenar o próprio `repoTags`. O frontend
  (`frontend/web/src/app/features/images/images.page.ts:65` e `:228`) usa `repoTags[0]` para o nome exibido e para a
  referência de "Baixar nova versão"; com a ordem do Podman, o pull iria pela referência por digest. A ordem de `repoTags`
  não é definida no contrato, então não há mudança de OpenAPI.

## Acompanhamento

- Repetir o passo 2 do cenário 1 da T028 no host Podman do quality depois da publicação.
