# Avaliação de Bug: imagem construída no host não é reconhecida como local_image

- **Slug**: imagem-local-com-repodigests
- **Criada em**: 2026-11-27
- **Origem**: texto colado (achado T028-3 de `specs/025-aviso-nova-versao-imagem/validation.md`, seção "Agentes reais (T028)")
- **Veredito**: valid
- **Severidade**: low

## Relato (literal ou resumido)

Achado T028-3, autorizado pelo solicitante em 2026-11-27 ("Bug Fixing: sim"): Podman 6 e Docker 29 (containerd image
store) criam `RepoDigests` também para imagens importadas ou construídas no host. Por isso a regra de
`ImageUpdateChecker` ("sem `RepoDigests` é `local_image`") não identifica essas imagens: o agente consulta o registro e
mostra falha em vez de "Não verificável".

## Sintoma

Uma imagem criada no host (`export | import`) e marcada com um nome de registro permitido (`127.0.0.1:5443/teste/local:1.0`)
mostra `failed`/`reference_not_found` ("Falha na verificação") nos dois engines, e o registro recebe
`HEAD /v2/teste/local/manifests/1.0`. A spec (research.md, D-002; quickstart, cenário 1 passo 4) espera `not_checkable`/
`local_image`, sem consulta ao registro.

## Reprodução

1. Host com Podman 6.2.0 ou Docker 29.9.3 (containerd image store) e agente 0.12.3.
2. `podman export <c> | podman import - localhost/t028/local:1.0` e `podman tag … 127.0.0.1:5443/teste/local:1.0`
   (no Docker, `docker import` e `docker tag`); container com essa referência.
3. "Verificar agora": `failed`/`reference_not_found`; o log do registro mostra o `HEAD` do manifesto.
4. `image inspect` dessa imagem traz `RepoDigests` (Podman: `…/teste/local@sha256:374d…`; Docker:
   `…/teste/local@sha256:08a2…`, igual ao `Id`).

Com o nome fora da política, o resultado é `registry_not_allowed`, decidido no plano de controle antes do agente.

## Caminhos de Código Suspeitos

- `backend/libs/Dokpod.Agent.Application/Images/ImageUpdateChecker.cs:37-41` — `local_image` só quando não há
  `RepoDigests` do mesmo repositório.
- `specs/025-aviso-nova-versao-imagem/research.md` (D-002) — define `local_image` como "não há `repo_digests` locais".

## Hipótese de Causa Raiz

A premissa vale para o armazenamento clássico do Docker, que só grava `RepoDigests` ao puxar ou enviar. O Podman calcula
um digest para cada nome, e o containerd image store do Docker 29 usa o digest do manifesto local como identidade, então
toda imagem tem `RepoDigests`. Os metadados que o agente lê (`image inspect`) não separam imagem puxada de imagem
construída nesses engines. Os testes usaram um engine simulado. Confiança: high.

## Correção Proposta

Não há sinal confiável no engine para manter a regra atual. Por isso, a correção depende de uma decisão de produto que
emende a D-002 da spec 025.

**Preferencial (A)**: aceitar que, nos engines que sintetizam `RepoDigests`, a imagem do host é verificada como qualquer
outra. O agente compara o digest remoto com os `RepoDigests` locais: se a tag não existe no registro, o resultado é
`failed`/`reference_not_found`; se existe com outro conteúdo, `update_available`; se for a mesma, `up_to_date`. Nenhum
desses casos mostra falso "Atualizada" (SC-002). Mudanças: emenda da D-002 e do passo 4 do cenário 1 (o `local_image`
continua para engines sem `RepoDigests`), comentário de `ImageUpdateChecker` e um teste que fixe o comportamento com
`RepoDigests` sintéticos. Sem mudança de protocolo.

**Alternativas**:
- (B) Marcar como `local_image` a imagem cujo `RepoDigest` do repositório é igual ao seu `Id` e que nunca veio de um pull
  registrado nos eventos do engine. Exige guardar histórico de pulls no agente (o engine local é a fonte de verdade e o
  agente não persiste isso hoje) e falha para imagens puxadas antes do agente subir.
- (C) Tratar `reference_not_found` de uma imagem cujo `RepoDigest` coincide com o `Id` como `not_checkable`/
  `local_image`. Mantém o rótulo, mas confunde a tag removida de uma imagem puxada pelo containerd store (onde também
  `Id` = digest do manifesto), quebrando o caso "tag removida" do cenário 3 no Docker 29.

**Arquivos Prováveis de Alteração** (opção A):
- `specs/025-aviso-nova-versao-imagem/research.md`, `quickstart.md` (emenda registrada como decisão humana)
- `backend/libs/Dokpod.Agent.Application/Images/ImageUpdateChecker.cs` (comentário)
- `backend/tests/Dokpod.Agent.Application.Tests/` (teste com `RepoDigests` sintéticos)

**Testes a Adicionar ou Atualizar**:
- Imagem com `RepoDigests` sintético e tag ausente no registro: `failed`/`reference_not_found`, nunca `up_to_date`.
- Imagem sem `RepoDigests` (Docker clássico): continua `not_checkable`/`local_image` sem HTTP.

## Riscos e Considerações

- A opção A faz o agente consultar o registro para imagens do host; a política de registros do estágio continua limitando
  esses pedidos, e a credencial usada é a do host, como nas demais.
- Muda o texto da spec: é decisão humana (a IA não emenda a D-002 por conta própria).

## Perguntas em Aberto

- [NEEDS CLARIFICATION: qual opção adotar para imagens do host em engines que sintetizam `RepoDigests` (A, B ou C)? A
  recomendação é A.]
