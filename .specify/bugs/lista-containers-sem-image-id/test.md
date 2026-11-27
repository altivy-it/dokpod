# Verificação de Bug: lista de containers mostra container_behind para container em dia

- **Slug**: lista-containers-sem-image-id
- **Testado em**: 2026-11-27
- **Avaliação**: ./assessment.md
- **Correção**: ./fix.md
- **Resultado**: verified

## Resumo

O teste novo com PostgreSQL real prova que a lista passa a devolver o `ImageId` e que a linha fica `up_to_date`, e a suíte
backend completa passa. A reprodução original (quality, agentes reais) ainda não foi repetida: exige publicar uma imagem
com a correção e implantá-la no quality, o que depende de pedido explícito.

## Verificações Executadas

| Verificação | Comando / Ação | Resultado | Notas |
|-------|------------------|--------|-------|
| Reprodução após correção | Cenário 1, passo 2 da T028 no quality, API trocada por imagem local com a correção | pass | `t028-web` `up_to_date` na lista e no detalhe, Docker e Podman (02:02 UTC) |
| Testes novos / atualizados | `dotnet test … --filter FullyQualifiedName~ContainerSearch` | pass | `SearchStore_ReturnsTheContainerImageId_SoAnUpToDateContainerIsNotBehind` |
| Suíte de regressão | `dotnet test Dokpod.slnx` com PostgreSQL 17.6 | pass | 1 345 aprovados, 9 ignorados por variável |
| Lint / verificação de tipos | `dotnet format --verify-no-changes` nos arquivos alterados | pass | Arquitetura (`BusinessGateTests`) aprovada |

## Trechos de Saída

```text
Passed!  - Failed:     0, Passed:   455, Skipped:     4, Total:   459 - Dokpod.ControlPlane.Api.Tests.dll (net10.0)
Passed!  - Failed:     0, Passed:    35, Skipped:     0, Total:    35 - Dokpod.Architecture.Tests.dll (net10.0)
```

## Riscos Residuais

- Agentes N-1 sem `ImageID` continuam caindo em `container_behind` (comportamento anterior, fora desta correção).

## Reprodução no quality (adendo de 2026-11-27)

A reprodução, antes `skipped`, foi feita depois: a API do quality foi trocada temporariamente por `localhost/dokpod-api-business:t028`, construída do working tree com as duas correções, e o passo 2 do cenário 1 foi repetido. Evidência em `specs/025-aviso-nova-versao-imagem/validation.md`, seção "Agentes reais (T028)".

## Recomendação

Encerre o bug: verificado ponta a ponta com agentes reais; falta só a correção chegar a uma release.
