# Verificação de Bug: imagem construída no host não é reconhecida como local_image

- **Slug**: imagem-local-com-repodigests
- **Testado em**: 2026-11-27
- **Avaliação**: ./assessment.md
- **Correção**: ./fix.md
- **Resultado**: verified

## Resumo

Pela D-003a, o comportamento observado na T028 passa a ser o esperado. A repetição no quality mostrou a imagem do host
com nome permitido como `failed`/`reference_not_found`, sem falso "Atualizada", nos dois engines; o teste novo fixa isso.

## Verificações Executadas

| Verificação | Comando / Ação | Resultado | Notas |
|-------|------------------|--------|-------|
| Reprodução após correção | Cenário 1, passo 4 da T028 no quality (`t028-local2`) | pass | `failed`/`reference_not_found` em Imagens, Containers e detalhe, Docker e Podman |
| Testes novos / atualizados | `dotnet test backend/tests/Dokpod.Agent.Application.Tests` | pass | 165 aprovados |
| Suíte de regressão | `dotnet test Dokpod.slnx` (antes deste teste) | pass | Código do agente sem mudança de comportamento |
| Lint / verificação de tipos | `dotnet format --verify-no-changes` | pass | |

## Riscos Residuais

- Imagem do host com o mesmo nome de uma remota é comparada com a remota (`update_available` se diferente), como registra
  a D-003a.

## Recomendação

Encerre o bug: comportamento alinhado à decisão D-003a e verificado com agentes reais.
