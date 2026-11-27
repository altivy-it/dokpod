# Correção de Bug: imagem construída no host não é reconhecida como local_image

- **Slug**: imagem-local-com-repodigests
- **Corrigido em**: 2026-11-27
- **Avaliação**: ./assessment.md
- **Estado**: applied

## Resumo

Opção A da avaliação, escolhida pelo solicitante em 2026-11-27 ("Opção recomendada"): nos engines que dão `RepoDigests` a
imagens do host (Podman 6 e Docker 29 com containerd image store), essas imagens são verificadas como as demais. A regra
foi registrada como D-003a na spec 025; o código do agente não muda, só o comentário e um teste que fixa o comportamento.

## Alterações

| Arquivo | Alteração | Notas |
|------|--------|-------|
| `specs/025-aviso-nova-versao-imagem/research.md` | modificado | D-003a, decisão humana referenciada |
| `specs/025-aviso-nova-versao-imagem/quickstart.md` | modificado | Cenário 1, passo 4: ressalva para os engines que sintetizam `RepoDigests` |
| `backend/libs/Dokpod.Agent.Application/Images/ImageUpdateChecker.cs` | modificado | Comentário com a D-003a |
| `backend/tests/Dokpod.Agent.Application.Tests/Images/ImageUpdateCheckerTests.cs` | teste adicionado | `RepoDigests` sintético |

## Testes Adicionados ou Atualizados

- `ImageUpdateCheckerTests.HostBuiltImageWithSynthesizedRepoDigests_IsCheckedLikeAnyOther_NeverUpToDate` — tag ausente no
  registro dá `failed`/`reference_not_found` com uma consulta; conteúdo diferente dá `update_available`; nunca `up_to_date`.
- `ImageWithoutRepoDigests_IsLocalImage` continua cobrindo o armazenamento clássico do Docker.

## Verificação Local

- `dotnet test backend/tests/Dokpod.Agent.Application.Tests` → 165 aprovados; `dotnet format --verify-no-changes` nos
  dois arquivos do agente sem apontamentos.

## Desvios da Avaliação

- A avaliação cita a D-002 como decisão a emendar; a regra de `local_image` está na **D-003** ("Referências não
  verificáveis"), e a emenda foi registrada como D-003a.

## Acompanhamento

- Se um engine passar a expor a origem da imagem (pull ou build), reavaliar o `local_image` nesses engines.
