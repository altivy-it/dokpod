# Correção de Bug: provisionamento UMA falha depois de 100 policies (spec 005, A1)

- **Slug**: spec005-a1-paginacao-policies
- **Corrigido em**: 2026-10-28
- **Avaliação**: ./assessment.md
- **Estado**: applied

## Resumo

A busca da policy passa a filtrar pelo nome exato, no caminho normal e no fallback do `409`, em vez de ler a primeira página de policies do resource server.

## Alterações

| Arquivo | Alteração | Notas |
| --- | --- | --- |
| `tools/scripts/provision-environment-authorization.ps1` | modificado | `$policyQuery` com `name=<nome>&exact=true`, reutilizado no fallback; filtro exato no cliente mantido |

## Testes Adicionados ou Atualizados

- Nenhum automatizado (os scripts de Keycloak não têm suíte); verificação manual em `test.md`.

## Verificação Local

- Ver `test.md`: Keycloak 26.7.0 da VM da spec 005, com 101 ambientes autorizados.

## Desvios da Avaliação

- Nenhum.

## Acompanhamento

- Atualizar a linha do A1 na spec 005 ao integrar.
