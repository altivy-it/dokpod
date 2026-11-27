# Verificação de Bug: Imagens do Podman escolhe a referência por digest como tag da imagem

- **Slug**: imagens-podman-tag-por-digest
- **Testado em**: 2026-11-27
- **Avaliação**: ./assessment.md
- **Correção**: ./fix.md
- **Resultado**: verified

## Resumo

O teste novo cobre as duas ordens de `RepoTags` com PostgreSQL real, e a suíte backend completa passa, inclusive a regra
de arquitetura que separa CE e Business. A reprodução no host Podman do quality ainda não foi repetida: exige publicar e
implantar a correção, o que depende de pedido explícito.

## Verificações Executadas

| Verificação | Comando / Ação | Resultado | Notas |
|-------|------------------|--------|-------|
| Reprodução após correção | Cenário 1, passo 2 da T028 no host Podman do quality, API trocada por imagem local com a correção | pass | Linha `teste/web` com tag `1.0` e `up_to_date` (02:02 UTC) |
| Testes novos / atualizados | `dotnet test … --filter FullyQualifiedName~ImageUpdate` | pass | `ImageWithTagAndDigestReferences_TakesTheTagState_InEitherRepoTagsOrder` |
| Suíte de regressão | `dotnet test Dokpod.slnx` com PostgreSQL 17.6 | pass | 1 345 aprovados, 9 ignorados por variável |
| Lint / verificação de tipos | `dotnet format --verify-no-changes` nos arquivos alterados | pass | `BusinessGateTests` aprovado depois de mover a regra para `Dokpod.Domain` |

## Trechos de Saída

```text
Passed!  - Failed:     0, Passed:   455, Skipped:     4, Total:   459 - Dokpod.ControlPlane.Api.Tests.dll (net10.0)
Passed!  - Failed:     0, Passed:    35, Skipped:     0, Total:    35 - Dokpod.Architecture.Tests.dll (net10.0)
```

## Riscos Residuais

- O frontend não tem teste próprio para a ordem de `repoTags`; a garantia está na resposta da API.

## Reprodução no quality (adendo de 2026-11-27)

A reprodução, antes `skipped`, foi feita depois: a API do quality foi trocada temporariamente por `localhost/dokpod-api-business:t028`, construída do working tree com as duas correções, e o passo 2 do cenário 1 foi repetido. Evidência em `specs/025-aviso-nova-versao-imagem/validation.md`, seção "Agentes reais (T028)".

## Recomendação

Encerre o bug: verificado ponta a ponta com agentes reais; falta só a correção chegar a uma release.
