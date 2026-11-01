# Pesquisa: Aviso de nova versão de imagem

**Spec**: [spec.md](spec.md) · **Plano**: [plan.md](plan.md) · **Data**: 2026-11-01 · **Origem**: IA assistida

Decisões técnicas que resolvem o contexto técnico do plano. Cada uma registra a escolha, a justificativa e as alternativas.

## D-001: Onde e como consultar o registro

- **Decisão**: o agente consulta o registro diretamente pela API OCI Distribution, com `HEAD /v2/<repositório>/manifests/<tag>` e o cabeçalho `Accept` dos quatro tipos de manifesto (índice OCI, lista Docker, manifesto OCI e manifesto Docker v2). O digest vem do cabeçalho `Docker-Content-Digest`. A autenticação segue o desafio `WWW-Authenticate: Bearer` (token anônimo ou com a credencial do host) ou `Basic`, usando as credenciais que o agente já lê de `DOKPOD_AGENT_REGISTRY_AUTH_FILE` (`RegistryCredentialFile`, spec 012/020).
- **Justificativa**:
  - um único caminho para Docker e Podman. Em 2026-11-01, o Podman 5.7.0 deste host respondeu `404 Not Found` a `GET /v1.41/distribution/<nome>/json` na API compatível, então o endpoint do engine não serve aos dois;
  - o `HEAD` de manifesto não consome a cota de pulls do Docker Hub: em 2026-11-01, `ratelimit-remaining` continuou em 100 antes e depois do `HEAD` em `registry-1.docker.io`;
  - a credencial não sai do host (FR-002, FR-007).
- **Alternativas**:
  - `GET /distribution/{name}/json` do Docker Engine: inexistente no Podman compatível;
  - `skopeo` no agente: binário novo na imagem e outra superfície;
  - consultar a partir do plano de controle: levaria a credencial ou a rede do host ao servidor, contra FR-002.

## D-002: Comparação de digests e plataforma

- **Decisão**: para cada referência em uso, o agente:
  1. obtém o digest remoto da tag (D-001);
  2. se ele estiver entre os `repo_digests` locais do mesmo repositório (`ImageState.repo_digests`, spec 012), o estado é `up_to_date`;
  3. senão, faz `GET` do manifesto remoto. Se for índice, escolhe o manifesto da plataforma do host (`os`, `architecture` e, quando houver, `variant` da imagem local). Se esse digest estiver entre os locais, o estado é `up_to_date`, porque só outras plataformas mudaram. Caso contrário, `update_available`.
- **Justificativa**: em 2026-11-01, o `RepoDigests` local de `docker.io/library/postgres:17.6` trazia os dois digests, o do índice (`sha256:00bc…`, igual ao `HEAD` remoto) e o do manifesto da plataforma (`sha256:b865…`). O `GET` só acontece quando o índice mudou, o que poupa cota.
- **Alternativas**: comparar só o índice (falso "nova versão" quando só outra arquitetura muda, contra o caso de borda da spec); baixar o manifesto sempre (gasta cota de pull).

## D-003: Referências não verificáveis

- **Decisão**: o estado é `not_checkable`, com motivo e sem consultar o registro, quando:
  - a referência é só por digest (`digest_reference`);
  - não há `repo_digests` locais, ou seja, imagem construída ou importada localmente (`local_image`);
  - o registro não está na política de registros permitidos do estágio (`registry_not_allowed`, decidido no plano de controle antes de pedir);
  - o registro é declarado inseguro na configuração do engine (`insecure_registry`, a partir de `RegistryInventory.insecure`);
  - o agente não anuncia a capability (`agent_unsupported`).
- **Justificativa**: casos de aceite 3 da Jornada 1 e FR-002. Nunca há consulta HTTP sem TLS.
- **Alternativas**: consultar registros inseguros por HTTP (sem TLS e sem garantia de autenticidade do digest).

## D-004: Protocolo agente-servidor

- **Decisão**:
  - nova capability `CAPABILITY_IMAGE_UPDATE_CHECK = 23`;
  - `ControlPlaneMessage.image_update_check = 17`, com `ImageUpdateCheckRequest`: `request_id`, até 500 itens com `reference` e `image_id` local;
  - `AgentMessage.image_update_check_result = 27`, com `ImageUpdateCheckResultPage`: `request_id`, página até 256 KiB, `is_last_page` e um resultado por item (estado, motivo, digest remoto, digest local que bateu, instante).
  - O servidor só envia o pedido a agentes que anunciaram a capability (padrão 008 D-009).
- **Justificativa**: o plano de controle decide política, agenda e licença; o agente fica sem estado. Compatibilidade N/N-1: campos e capability novos; agente antigo não anuncia e não recebe, e servidor antigo ignora o campo 27. O formato está em [contracts/agent-protocol.md](contracts/agent-protocol.md).
- **Alternativas**:
  - o agente agendar sozinho: espalharia a política e a licença pelo agente;
  - reutilizar `ContainerQueryRequest`: mistura uma consulta de host com o tipo de consulta de container.

## D-005: Limites no agente

- **Decisão**:
  - até 4 consultas simultâneas por pedido;
  - 10 s por requisição HTTP, 5 min por pedido;
  - corpo de manifesto lido até 4 MiB;
  - redirecionamentos só para o mesmo host, porque o `Authorization` nunca segue para outro;
  - `429` e `5xx` viram `failed` (`rate_limited` ou `registry_unavailable`), sem nova tentativa no mesmo pedido;
  - `401` ou `403` depois do desafio viram `failed` (`unauthorized`); `404` vira `failed` (`reference_not_found`, caso de borda "tag removida").
- **Justificativa**: 200 imagens em cerca de 10 s, sem rajada contra o registro; nenhuma falha produz `up_to_date` (SC-002).
- **Alternativas**: tentativas com espera dentro do pedido (estende o pedido além do ciclo; a próxima verificação já é a nova tentativa).

## D-006: Agendamento, réplicas e "Verificar agora"

- **Decisão**:
  - worker no módulo Business da API. Tabela `image_update_schedules`, uma linha por host, com `next_due_at`, `lease_owner` e `lease_until`. O lease é de 2 min, mais longo que os 30 s da 027, porque cobre só o envio do pedido e a marcação da agenda; o resultado chega depois, até 5 min (D-005). Uma réplica assume o host vencido, envia o pedido e marca o próximo vencimento entre 5 h 24 min e 6 h. A variação aleatória é só para menos, para os hosts não coincidirem sem passar das 6 h do SC-001;
  - host desconectado fica vencido e é verificado quando reconectar;
  - "Verificar agora" adianta o `next_due_at` do host para agora, no máximo uma vez por minuto por host (`429 rate_limited`), e responde `202`. A tela relê a lista até o resultado chegar.
- **Justificativa**: SC-001 (até 6 h, ou até 1 minuto a pedido); várias réplicas sem pedido duplicado; o mesmo padrão de lease já testado na 027.
- **Alternativas**:
  - temporizador em memória: duplicaria com várias réplicas e perderia a agenda ao reiniciar;
  - pedido síncrono no "Verificar agora": prende a requisição do console à rede do registro, contra SC-004.

## D-007: Persistência

- **Decisão**:
  - tabela `image_update_checks` com chave `(environment_id, reference, platform)` e só o resultado mais recente ([data-model.md](data-model.md));
  - cada resultado completo de um host substitui o conjunto: linhas de referências que não estão mais em uso saem;
  - falha (`failed`) preserva o digest e a data da última verificação bem-sucedida.
- **Justificativa**: esclarecimento de 2026-11-01 (sem histórico). A tabela cresce com as imagens em uso, por volta de 200 por host, e não com o tempo, então não entra na regra de particionamento da constituição.
- **Alternativas**: histórico particionado por mês (sem uso na spec e com retenção para gerenciar).

## D-008: Container com a versão anterior

- **Decisão**: o estado de cada container é derivado na leitura:
  - se a verificação da sua referência diz `up_to_date`, mas o `image_id` do container difere do `local_image_id` da verificação, o container mostra `container_behind` ("versão nova no host; o container usa a anterior");
  - nos demais casos, o container herda o estado da referência.
- **Justificativa**: Jornada 2, cenário 1: depois do pull, a tag aponta para a imagem nova, e o container continua na antiga até ser recriado pelo operador (FR-004).
- **Alternativas**: verificação por container (multiplica consultas ao registro para containers da mesma imagem).

## D-009: Licença e composição

- **Decisão**:
  - módulo `ImageUpdateCheckModule` registrado só pelo `ProductEdition` Business, como a 027;
  - `[BusinessCapability("image-update-notice")]` com `IBusinessCapabilityGate` em "Verificar agora" e na criação e edição de regra com a condição de imagem desatualizada (FR-008);
  - o worker e a leitura não consultam o gate; com a licença restrita, continuam (FR-009);
  - quando a licença importada não lista `image-update-notice`, o worker não agenda e as respostas omitem o estado (esclarecimento de 2026-11-01). A imagem CE não registra nada e o agente nunca recebe pedidos.
- **Justificativa**: a 031 restringe só as execuções novas; o que já está configurado continua.
- **Alternativas**: parar a verificação na restrição (o cliente perderia o aviso de imagens com correções de segurança justamente quando a licença venceu).

## D-010: Condição de alerta

- **Decisão**:
  - novo `AlertConditionKind.ImageOutdated`, com `OutdatedDays` de 1 a 365. A varredura da spec 016 abre um alerta por container em `update_available` ou `container_behind` cuja referência está desatualizada há pelo menos N dias (`outdated_since`), e resolve quando o container deixa esse estado;
  - a disponibilidade da condição vem de um provedor que só o módulo Business registra. Na CE, uma regra com essa condição é recusada com `422`.
- **Justificativa**: FR-006 pelo fluxo da 016 (abertura, resolução, silenciamento e auditoria já existentes), sem motor novo.
- **Alternativas**: alertas próprios da 025 (duplicaria a governança da 016).

## D-011: API e telas

- **Decisão**:
  - `updateCheck` opcional nos itens de `GET /api/v1/images` e `GET /api/v1/containers` e no resumo do container. Ele só vem quando a composição é Business e a licença lista a capability; na CE o campo não existe;
  - `POST /api/v1/environments/{environmentId}/images/update-check` (`x-dokpod-edition: business`, scope `image:pull`), auditado como `image.update_check_requested`.
  - Contrato em [contracts/api.md](contracts/api.md).
- **Justificativa**: FR-003 nas três telas; leitura por junção indexada com a tabela de verificações, sem chamar o agente durante a requisição (SC-004).
- **Alternativas**: rota separada por tela (mais chamadas por página).

## D-012: Observabilidade e segredos

- **Decisão**:
  - métricas `dokpod.image_update.checks` (por estado e motivo) e `dokpod.image_update.outdated` (gauge por host), sem referência nem registro como dimensão;
  - o agente não registra cabeçalhos, tokens, nem o corpo das respostas;
  - o canário de credencial vai em `config.json`, logs, banco, respostas e na telemetria (SC-003).
- **Justificativa**: FR-007 e as lições da T038 da 027 (host e URL fora de telemetria).
- **Alternativas**: dimensão por registro (vaza o mapa de registros privados do cliente para a telemetria).
