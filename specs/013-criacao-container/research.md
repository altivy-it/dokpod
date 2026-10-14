# Pesquisa: Criação de Container

## D-001 Origem dos segredos ("Usar segredo do cofre")

| Opção | Prós | Contras |
| --- | --- | --- |
| A. Segredos já presentes no host (Podman `secret`; Docker: arquivo em diretório protegido do agente) (**recomendada**) | Nenhum valor trafega pelo plano de controle; sem datastore novo. | Operador provisiona segredos fora do Dokpod. |
| B. Cofre externo (Vault/nuvem) | Gestão centralizada. | Nova integração e dependência: exige ADR. |
| C. Segredos guardados no PostgreSQL do Dokpod | UX simples. | Transforma o Dokpod em cofre; contraria o princípio III. |

## D-002 Proteção de valores literais de variáveis

- Data Protection com propósito `Dokpod.CommandParameters.v1`; valores cifrados em `protected_values`; o protobuf enviado ao agente leva valores em claro **somente dentro do canal mTLS**; o journal do agente guarda hash e nomes, não valores; após terminal (ou expiração), job de purga zera `protected_values` e marca `purged_at`. O hash canônico é calculado antes da cifragem e preservado.
- Reinício do agente: como o journal não guarda valores, um comando journalado sem `create` concluído é tratado assim: (1) o agente procura container com o label `dokpod.command-id` e, se existir, conclui por reconciliação; (2) se não existir, pede reentrega (at-least-once) e o plano de controle reenvia os valores decifrados enquanto o comando não for terminal nem expirado; (3) se `purged_at` estiver preenchido ou o prazo tiver expirado, o resultado é `parameters_unavailable`, sem criar container.

## D-003 Tradução para o engine

- Docker/Podman compatível: `POST /containers/create?name=` com `Image`, `Env`, `ExposedPorts`, `HostConfig{PortBindings, Binds/Mounts (volume), NanoCpus, Memory, RestartPolicy, AutoRemove, NetworkMode (rede existente)}`, `Healthcheck{Test, Interval, Timeout, Retries, StartPeriod}`, `Labels{dokpod.command-id, dokpod.created-by}`; depois `POST /containers/{id}/start`.
- Segredos Podman: a API compatível não aceita segredos fora do Swarm; o endpoint Libpod `/libpod/containers/create` aceita `secret_env`, que injeta o segredo sem o agente ler o valor. **Decisão**: no Podman, usar um adapter Libpod mínimo somente para criação com segredos; nos demais casos, a API compatível. Alternativa aceita: arquivo protegido do agente, como no Docker. Ler o valor com `podman secret inspect --showsecret` é rejeitado. Validar em teste real e registrar o resultado.

- Health check: o mockup promete "3 tentativas antes de reiniciar". O Docker apenas marca `unhealthy`; o Podman reinicia com `health_on_failure: restart`, disponível somente em `/libpod/containers/create`. **Decisão**: campo `onFailure` (`none` | `restart`); `restart` usa o adapter Libpod do Podman; no Docker o agente recusa `restart` com `unsupported_option` e a interface mostra a ajuda adaptada.
- Remoção automática: `HostConfig.AutoRemove` (Docker) e `--rm` (Podman) apagam os volumes anônimos do container. **Decisão**: o agente inspeciona a imagem e recusa com `anonymous_volume_with_auto_remove` se algum `VOLUME` declarado não estiver coberto por volume nomeado no mesmo destino (princípio II).

## D-004 Reconciliação

- Label `dokpod.command-id=<uuid>`; após crash, o agente lista containers com o label para concluir sem recriar.

## D-005 Comando equivalente

- Renderizado pelo domínio (`EquivalentCommandRenderer`) para `docker run`/`podman run`, com `-e NOME=***` e `--secret`/`--env-file` por referência; mesmo texto no formulário (gerado no frontend a partir das mesmas regras, com teste de paridade) e na auditoria (gerado no servidor).

## D-006 Key ring de Data Protection da API

- Hoje só o BFF configura Data Protection (`backend/apps/Dokpod.Bff/BffHost.cs`, chaves em sistema de arquivos). **Decisão**: a API ganha key ring próprio (nome de aplicação `Dokpod.ControlPlane.Api`), persistido em volume dedicado com permissão restrita ao usuário do container, sem compartilhar com o BFF; rotação padrão do framework (90 dias) e chaves antigas mantidas até o maior prazo de comando. Sem novo datastore nem dependência. Perda do key ring: comandos não terminais falham com `parameters_unavailable` e o evento é auditado.
