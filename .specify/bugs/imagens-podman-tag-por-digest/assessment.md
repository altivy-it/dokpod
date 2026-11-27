# Avaliação de Bug: Imagens do Podman escolhe a referência por digest como tag da imagem

- **Slug**: imagens-podman-tag-por-digest
- **Criada em**: 2026-11-27
- **Origem**: texto colado (achado T028-2 de `specs/025-aviso-nova-versao-imagem/validation.md`, seção "Agentes reais (T028)")
- **Veredito**: valid
- **Severidade**: medium

## Relato (literal ou resumido)

Achado T028-2, autorizado pelo solicitante em 2026-11-27 ("Bug Fixing: sim"): no Podman, uma imagem com tag e digest
locais aparece em Imagens como `not_checkable`/`digest_reference` e sem tag. O Podman lista primeiro o `RepoTag` por
digest; `ImageUpdateLookup.ForImage` usa a primeira referência com resultado e a tela de Imagens usa a primeira tag. No
Docker a ordem é inversa e a tela fica certa.

## Sintoma

Com um container por tag (`127.0.0.1:5443/teste/web:1.0`) e outro por digest da mesma imagem no host Podman, a linha da
imagem em `GET /api/v1/images` traz `tag: null` e `updateCheck` `not_checkable`/`digest_reference`, embora a tag tenha sido
verificada como `up_to_date`. O esperado é a tag `1.0` e o estado da tag, como no Docker.

## Reprodução

1. Host Podman 6.2.0 com agente 0.12.3 e licença com `image-update-notice`.
2. `podman pull <reg>/teste/web:1.0` e `podman pull <reg>/teste/web@<digest>`; um container de cada.
3. "Verificar agora" e `GET /api/v1/images?environmentId=<id>`.
4. `repoTags` vem `["<reg>/teste/web@sha256:…", "<reg>/teste/web:1.0"]`; `repository` correto, `tag: null`,
   `updateCheck.state = not_checkable`, `reason = digest_reference`.

Observado no quality em 2026-11-27 (T028). No Docker 29.9.3 a mesma situação lista a tag primeiro e o resultado é certo.

## Caminhos de Código Suspeitos

- `backend/apps/Dokpod.ControlPlane.Api/Environments/ImageEndpoints.cs:104` — `repository`/`tag` saem do primeiro item
  de `RepoTags`, que no Podman pode ser uma referência por digest.
- `backend/libs/Dokpod.ControlPlane.Application/ImageUpdates/ImageUpdateReadService.cs` (`ImageUpdateLookup.ForImage`) —
  devolve o resultado da primeira referência de `RepoTags` que tem verificação, inclusive a por digest, que é sempre
  `digest_reference`.
- Origem dos dados: o Podman inclui em `RepoTags` os nomes `repo@sha256:…` das imagens puxadas por digest; o agente
  repassa a lista como veio do engine.

## Hipótese de Causa Raiz

As duas escolhas dependem da ordem de `RepoTags`, que não é garantida entre engines, e não distinguem tag de referência
por digest. Com o Podman colocando a referência por digest antes, a imagem herda o estado `digest_reference` do container
por digest e perde a tag exibida. Confiança: high.

## Correção Proposta

**Preferencial**: no plano de controle, preferir referências com tag (sem digest): em `ToItem`, usar a primeira entrada de
`RepoTags` que tenha tag e não tenha digest, caindo na primeira entrada só se não houver nenhuma; em `ForImage`, procurar
primeiro entre as referências com tag e só depois entre as por digest. Assim o resultado não depende da ordem do engine.

**Alternativas**:
- Normalizar no agente, movendo de `RepoTags` para `RepoDigests` as entradas por digest. Corrige na origem, mas exige
  novo agente (N/N-1: agentes 0.12.x continuariam mandando a lista do Podman) e muda o inventário de imagens da spec 012.

**Arquivos Prováveis de Alteração**:
- `backend/apps/Dokpod.ControlPlane.Api/Environments/ImageEndpoints.cs`
- `backend/libs/Dokpod.ControlPlane.Application/ImageUpdates/ImageUpdateReadService.cs`
- `backend/tests/Dokpod.ControlPlane.Api.Tests/ImageUpdates/ImageUpdateReadTests.cs` e testes de `ImageEndpoints`

**Testes a Adicionar ou Atualizar**:
- `ForImage` com `["repo@sha256:…", "repo:1.0"]`, a tag `up_to_date` e o digest `digest_reference`: devolve `up_to_date`;
  com só a referência por digest, devolve `digest_reference`.
- Lista de imagens com `RepoTags` na ordem do Podman: `tag = "1.0"`.

## Riscos e Considerações

- Mudança só de leitura: sem migração, sem protocolo.
- Ordenação e busca por repositório (`sort`, `q`) usam outra consulta; conferir se dependem da primeira tag.
- O frontend exibe `repository`/`tag`; nenhuma mudança de contrato.

## Perguntas em Aberto

- Nenhuma.
