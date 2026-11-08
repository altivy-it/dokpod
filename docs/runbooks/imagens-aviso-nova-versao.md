# Runbook: aviso de nova versão de imagem

Recurso Business da [spec 025](../../specs/025-aviso-nova-versao-imagem/spec.md), capability `image-update-notice`.
O agente compara, a cada até 6 h, o digest da tag remota de cada imagem em uso com a imagem local e o resultado aparece
em Imagens, Containers e no resumo do container. Nada é atualizado sozinho: o operador decide se baixa a nova versão e
quando recria o container. Decisão de arquitetura: [ADR 2026-0009](../adr/2026-0009-agente-consulta-registros-oci.md).

## Antes de usar

- Imagem Business da API ([distribuição](licenciamento-imagem-business.md)) com licença que contenha
  `image-update-notice`. Sem a capability na licença importada, o worker não agenda verificações e as respostas não têm o
  campo `updateCheck`.
- Agente Linux com a capability 23 (`CAPABILITY_IMAGE_UPDATE_CHECK`). Agente antigo ou Windows não a anuncia, e as
  referências dele aparecem como `not_checkable` (`agent_unsupported`).
- "Verificar agora" exige o scope `image:pull` no host; o botão de baixar a nova versão usa o mesmo download da
  [spec 012](../../specs/012-imagens-pull/spec.md).

## Estados

| Estado | Significado na tela | O que fazer |
| --- | --- | --- |
| `up_to_date` | A tag remota aponta para a imagem local | Nada |
| `update_available` | A tag remota mudou para a plataforma do host | Baixar a nova versão e, depois, recriar o container na janela de mudança |
| `container_behind` | A imagem nova já está no host, mas o container ainda usa a anterior | Recriar o container (o Dokpod nunca recria sozinho) |
| `not_checkable` | A referência não pode ser comparada; o motivo diz por quê | Ver a tabela de motivos |
| `failed` | A última consulta falhou; o último resultado bom e a data dele continuam visíveis | Ver a tabela de motivos; a próxima verificação é a nova tentativa |

Nenhuma falha vira `up_to_date`. Um índice multiarquitetura que mudou só em outra plataforma continua `up_to_date`.
Com o host desconectado, o último resultado aparece marcado como desatualizado (`stale`), e o host é verificado assim que
reconectar.

## Motivos

| Motivo | Estado | Causa | Ação |
| --- | --- | --- | --- |
| `digest_reference` | `not_checkable` | O container usa a imagem por digest (`app@sha256:...`), que por definição não muda | Nenhuma; use uma tag se quiser o aviso |
| `local_image` | `not_checkable` | A imagem não tem `RepoDigests`: foi construída ou importada no host | Nenhuma, ou publique a imagem num registro e baixe de lá |
| `registry_not_allowed` | `not_checkable` | O registro está fora da [política de registros](../../specs/020-registros/spec.md) do estágio; o plano de controle nem pede a verificação | Incluir o registro na política, se fizer sentido |
| `insecure_registry` | `not_checkable` | O registro é declarado inseguro (`insecure-registries` no Docker, `insecure = true` no Podman) | Nenhuma: o agente nunca consulta registro sem TLS |
| `agent_unsupported` | `not_checkable` | Agente sem a capability 23 (versão antiga ou Windows) | Atualizar o agente Linux |
| `unauthorized` | `failed` | Registro privado sem credencial no host, credencial errada ou sem permissão de leitura | Ver [Credencial do registro](#credencial-do-registro) |
| `rate_limited` | `failed` | O registro respondeu `429` | Ver [Cota do Docker Hub](#cota-do-docker-hub) |
| `registry_unavailable` | `failed` | Erro de rede, DNS, TLS ou `5xx` do registro | Conferir a saída do host ao registro e o proxy |
| `timeout` | `failed` | Mais de 10 s numa requisição ou 5 min no pedido inteiro | Conferir a latência até o registro e o proxy |
| `reference_not_found` | `failed` | A tag foi removida do registro (`404`) | Trocar a tag do container |
| `invalid_response` | `failed` | Resposta fora do protocolo OCI: sem digest, corpo acima de 4 MiB, redirecionamento para outro host | Conferir o registro ou o proxy que o intermedeia |

## Credencial do registro

O agente usa o mesmo arquivo de credenciais do download de imagens: `DOKPOD_AGENT_REGISTRY_AUTH_FILE`, no formato
`config.json` do Docker (campo `auths`, valor `auth` com `usuário:segredo` em Base64). `identitytoken` e helpers de
credencial (`credsStore`, `credHelpers`) não são lidos.

- A credencial só vai ao próprio registro (`Basic`) ou ao serviço de token do desafio `Bearer` quando ele está no mesmo
  host do registro, ou em `auth.docker.io` para o Docker Hub. O serviço de token precisa estar em `https`.
- O token recebido vale só para aquele pedido de verificação e não é gravado.
- A credencial nunca sai do host: não vai ao plano de controle, ao banco, aos logs nem à telemetria. O canário
  `ImageUpdateCanaryTests` cobre esse caminho.
- Para registros que exigem token de acesso (GCR, Artifact Registry, ECR), grave o par usuário e token no `auth`. Quando o
  token expira, o estado passa a `unauthorized` até o arquivo ser atualizado.

## Proxy e saída do agente

O agente faz `HEAD` (e `GET` só quando o índice mudou) direto em `https://<registro>/v2/<repositório>/manifests/<tag>`.
No Docker Hub, o host é `registry-1.docker.io`, e o token vem de `auth.docker.io`.

- Com proxy de saída, defina `HTTPS_PROXY` (e `NO_PROXY` para os registros internos) no container do agente. O proxy do
  engine (`daemon.json`, `containers.conf`) não vale para o agente, que faz as consultas por conta própria.
- O TLS é sempre verificado pelo repositório de certificados do sistema do container do agente. Um registro com CA
  interna precisa dessa CA na imagem ou montada no repositório de confiança do agente.
- Até 4 consultas simultâneas por pedido e 500 referências por host.

## Registros inseguros

Registro declarado inseguro no engine nunca é consultado (`insecure_registry`), mesmo que responda em HTTPS. Para receber
o aviso, publique o registro com TLS válido e retire-o da lista de inseguros.

## Cota do Docker Hub

O `HEAD` de manifesto não consome a cota de pulls do Docker Hub; o `GET` do índice só acontece quando o digest da tag
mudou. Sem credencial, a verificação usa token anônimo. Se o Docker Hub responder `429`, o estado é `failed`
(`rate_limited`), sem nova tentativa no mesmo pedido; a próxima verificação vem no ciclo seguinte. Para evitar a cota
anônima compartilhada por IP, configure uma conta do Docker Hub no arquivo de credenciais do host.

## Agente Windows

O agente Windows não anuncia a capability nesta versão: as referências dos hosts Windows aparecem como `not_checkable`
(`agent_unsupported`), e nenhum pedido é enviado a eles.

## Licença

| Situação | Comportamento |
| --- | --- |
| Licença ativa com `image-update-notice` | Verificação a cada até 6 h, "Verificar agora", regras de alerta de imagem desatualizada |
| Licença restrita (vencida, inválida ou revogada) | As verificações e os alertas já configurados continuam; "Verificar agora" e criar, editar ou reativar regra de imagem desatualizada são recusados com o código da licença; desativar e excluir sempre funcionam |
| Licença sem a capability | O worker não agenda, o campo `updateCheck` some das respostas e a condição de alerta não é oferecida |
| Community Edition | Nada é registrado, e o agente nunca recebe pedidos; regra com a condição é recusada com `422` |

## Verificar e diagnosticar

- "Verificar agora" adianta a verificação do host, no máximo uma vez por minuto (`429 rate_limited`), e é auditado como
  `image.update_check_requested` (categoria `images`). Depois de um download com sucesso, a verificação do host é
  adiantada sozinha.
- Métricas: `dokpod_image_update_checks_total{image_update_state, image_update_reason}` e
  `dokpod_image_update_outdated{environment_id}` ([observabilidade](observabilidade.md)). Nenhuma dimensão leva
  referência, registro ou digest.
- Falha inesperada no agente gera só `Image update check failed: <tipo da exceção> (<motivo>)` no log dele, sem
  referência, URL ou mensagem.
- Os resultados ficam em `dokpod.image_update_checks` (só o mais recente por host, referência e plataforma) e a agenda em
  `dokpod.image_update_schedules`; o modelo está em
  [data-model.md](../../specs/025-aviso-nova-versao-imagem/data-model.md).
