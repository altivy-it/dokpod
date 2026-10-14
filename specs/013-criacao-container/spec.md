# Especificação da Feature: Criação de Container

**Branch da feature**: `007-console-operacional`

**Criada em**: 2026-10-12

**Estado**: `implemented` - concluída em 2026-10-14; revisão humana de segurança antes de release; evidências e itens `NOT RUN` em [tasks.md](tasks.md).

**Entrada**: Board `04 · Novo container` ([NovoContainer.dc.html](../../docs/design/app/project/NovoContainer.dc.html)) e componentes `Campo`, `Selecao`, `Marcador`.

**Depende de**: [007](../007-fundacao-console-design-system/spec.md), [008](../008-visao-geral-hosts-eventos/spec.md), [012](../012-imagens-pull/spec.md) (parâmetros de comando, referência e pull). **Usa**: [009](../009-metricas-recursos/spec.md) (CPU do host na seleção), [010](../010-lista-containers-acoes-lote/spec.md) (lista atrás do drawer).

## Contexto

O mockup abre, sobre a lista de containers, um drawer "Criar e iniciar container" com imagem, nome, engine, host, portas, variáveis de ambiente (com "Usar segredo do cofre"), volume, limites de CPU e memória, política de reinício, health check, remoção automática e o comando equivalente (`podman run -d ...`) registrado na auditoria. Hoje o Dokpod só opera containers existentes.

## Cenários do Usuário e Testes (obrigatório)

### Jornada do Usuário 1 - Criar e iniciar um container simples (Prioridade: P1)

Como operador com `container:create`, quero escolher imagem, nome, engine e host, publicar portas e criar e iniciar o container, acompanhando até ele estar em execução.

**Valor desta prioridade**: Fluxo principal do board 04.

**Teste independente**: Criar `nginx` com porta 8081→80/tcp em host Docker e em host Podman; conferir no engine, inventário, evento e auditoria.

**Cenários de aceite**:

1. **Dado** o drawer aberto pelo botão "Novo container", **Quando** preenche imagem, nome, engine, host e porta e aciona "Criar e iniciar", **Então** um comando idempotente é enviado ao host, a imagem é baixada se não existir, o container é criado e iniciado, e o drawer mostra o progresso até "Em execução" com link para o detalhe.
2. **Dado** o seletor de Engine, **Quando** muda entre Podman e Docker, **Então** o seletor de Host mostra apenas hosts autorizados do estágio com aquela engine, com CPU atual como legenda, e os chips do drawer mostram host e engine selecionados.
3. **Dado** nome já usado no host, **Quando** informado, **Então** a validação avisa antes de enviar; se o conflito ocorrer no engine, o resultado mostra `name_conflict`.
4. **Dado** porta do host já publicada por outro container do host, **Quando** informada, **Então** a validação avisa; conflito no engine resulta em `port_conflict` e o container criado parcialmente é removido.
5. **Dado** falha ao iniciar após criar, **Quando** ocorre, **Então** o resultado mostra a falha, o container permanece criado e parado (visível na lista), e o operador decide removê-lo.
6. **Dado** o formulário, **Quando** preenchido, **Então** a caixa informativa mostra o comando equivalente da engine selecionada, sem valores de segredos.

### Jornada do Usuário 2 - Variáveis de ambiente e segredos (Prioridade: P1)

Como operador, quero informar variáveis de ambiente e referenciar segredos já cadastrados no host, sem que valores sensíveis fiquem expostos.

**Valor desta prioridade**: Aplicações reais precisam de configuração e credenciais.

**Teste independente**: Criar container com variáveis comuns e com referência a segredo do host; verificar no container e varrer API, banco, auditoria e logs por canários.

**Cenários de aceite**:

1. **Dado** variável marcada como segredo, **Quando** digitada, **Então** o valor fica mascarado no formulário e nunca é exibido de volta.
2. **Dado** "Usar segredo do cofre", **Quando** acionado, **Então** o operador escolhe um segredo existente no host pelo nome (sem ver o valor) e o nome da variável; o agente resolve o valor no host.
3. **Dado** o comando persistido, **Quando** consultado na API, auditoria ou banco, **Então** valores de variáveis aparecem apenas como nomes e "valor oculto"; os valores são removidos do armazenamento ao fim do comando.

### Jornada do Usuário 3 - Recursos, volume, reinício e saúde (Prioridade: P2)

Como operador, quero definir limites de CPU (vCPU) e memória (MiB), um volume nomeado, política de reinício, health check e remoção automática ao parar.

**Valor desta prioridade**: Opções do mockup para workloads de produção.

**Teste independente**: Criar container com todas as opções e comparar com `inspect`.

**Cenários de aceite**:

1. **Dado** limite de CPU 2 e memória 512, **Quando** criado, **Então** o engine aplica 2 vCPU e 512 MiB.
2. **Dado** o campo único "Volume" do mockup com `vol-pedidos-uploads:/srv/app/uploads:rw` (formato `origem:destino[:ro|rw]`, padrão `rw`), **Quando** criado, **Então** o volume nomeado é montado (criado se não existir); caminhos do host (bind) só são aceitos dentro da lista permitida configurada no agente. A interface oferece um volume (conforme o mockup); a API aceita até 10.
3. **Dado** o marcador "Ativar health check em GET /healthz" (mockup), **Quando** criado, **Então** aplica o preset: requisição HTTP GET `/healthz` na primeira porta de container informada (ou 80 se não houver), a cada 15 s, 3 tentativas, tempo limite 5 s; a ajuda alerta que a imagem precisa de `curl` ou `wget`. Ajustes de comando, intervalo, tentativas e tempo limite ficam em "Opções avançadas" recolhidas (fora do mockup, padrão fechado).
4. **Dado** health check ativado em host Podman, **Quando** o container fica `unhealthy`, **Então** o engine o reinicia (`health-on-failure=restart`) e a ajuda diz "A cada 15 s, 3 tentativas antes de reiniciar", como no mockup; em host Docker, que não reinicia por health check, a ajuda diz "A cada 15 s, 3 tentativas antes de marcar como não saudável" e o agente recusa `onFailure: restart` com `unsupported_option`.
5. **Dado** "Remover automaticamente ao parar", **Quando** criado, **Então** o container é removido pelo engine ao parar, e a interface avisa que logs e estado se perdem nesse caso.
6. **Dado** "Remover automaticamente ao parar" e uma imagem que declara `VOLUME` sem volume nomeado montado no mesmo destino, **Quando** solicitado, **Então** o agente recusa com `anonymous_volume_with_auto_remove`, porque a remoção automática do engine apagaria esses volumes anônimos (constituição, princípio II); a interface explica como montar um volume nomeado no destino.

### Jornada do Usuário 4 - Proteções de segurança na criação (Prioridade: P1)

Como responsável pela segurança, quero que a criação não permita escalar privilégios no host.

**Valor desta prioridade**: Criar container é a operação mais poderosa do console.

**Teste independente**: Tentar, via API diretamente, opções privilegiadas e bind mounts proibidos; todas recusadas pela API e pelo agente.

**Cenários de aceite**:

1. **Dado** requisição com qualquer campo fora da allowlist (privileged, capabilities, devices, rede/PID/IPC do host, montagem do socket do engine, security-opt), **Quando** recebida, **Então** a API rejeita com 400 e o agente também rejeitaria (defesa em profundidade).
2. **Dado** bind mount fora da lista permitida do agente, **Quando** solicitado, **Então** o agente recusa com `mount_not_allowed`.
3. **Dado** imagem de registro fora da lista permitida do estágio (quando configurada), **Quando** solicitada, **Então** é recusada com `registry_not_allowed`.

## Casos de Borda

- Retry do navegador após timeout: mesma chave de idempotência não cria segundo container.
- Resposta perdida após criação: reconciliação localiza o container pelo label de comando (`dokpod.command-id`) e conclui sem recriar.
- Host desconectado durante preenchimento: botão desabilitado com motivo.
- Imagem grande: pull respeita prazo da 012; progresso exibido. O prazo padrão do comando de criação é o prazo de pull da 012 (15 min) mais 2 min para create/start.
- Agente reinicia após gravar o comando no journal e antes do `create`: o journal não tem valores sensíveis, então o agente pede reentrega; o plano de controle reenvia os valores enquanto o comando não for terminal; se já foram purgados ou o prazo expirou, o comando falha com `parameters_unavailable`, sem criar container.
- Nome inválido (regex do engine) ou > 63 caracteres: validação local e na API.
- Muitas portas/variáveis: limites fixos de 32 portas, 100 variáveis e 10 volumes; excedente resulta em 400 por campo.
- Podman rootless e portas < 1024: falha `privileged_port` com explicação.

## Requisitos (obrigatório)

### Requisitos Funcionais

- **FR-001**: A API DEVE aceitar a criação de container em um host com scope `container:create`, idempotência, prazo (padrão: prazo de pull da 012 + 2 min), auditoria e capability, executada pelo agente como um único comando "baixar se necessário, criar e iniciar".
- **FR-002**: Os parâmetros aceitos DEVEM ser uma allowlist fechada: imagem, nome, portas (host IP opcional, porta do host, porta do container, protocolo), variáveis (valor literal ou referência a segredo do host, marcação de sensível), volumes (nomeado, ou bind de um diretório exato da lista permitida do agente, sem subcaminhos; modo ro/rw), rede existente opcional, limite de CPU, limite de memória, política de reinício (`no`, `on-failure` com máximo, `always`, `unless-stopped`), health check (comando, intervalo, tentativas, tempo limite, período inicial, ação em falha `none` | `restart`, esta somente no Podman), remoção automática e labels de rastreio do Dokpod. A rede é parâmetro somente da API: o mockup não tem campo de rede e a interface não o exibe.
- **FR-003**: A API e o agente DEVEM rejeitar qualquer opção privilegiada (privileged, cap-add, devices, namespaces do host, socket do engine, security-opt, sysctls, user-ns do host).
- **FR-004**: Valores de variáveis DEVEM ser protegidos: cifrados em repouso enquanto o comando não estiver terminal, nunca retornados pela API, nunca incluídos em auditoria, eventos, logs ou telemetria, e removidos após o estado terminal; o hash de idempotência continua verificável.
- **FR-005**: Segredos "do cofre" DEVEM ser referências a segredos existentes no host (segredo do Podman ou arquivo em diretório protegido configurado no agente), resolvidos somente pelo agente; o plano de controle conhece apenas o nome.
- **FR-006**: A API DEVE fornecer ao formulário: hosts elegíveis por engine com CPU atual (009), lista de nomes de segredos disponíveis no host (sem valores), volumes existentes e presença da imagem (012). Redes existentes são devolvidas para clientes da API, sem uso na interface.
- **FR-007**: O agente DEVE marcar o container com label `dokpod.command-id` para reconciliação e, se a criação ou o início falharem após efeitos parciais, reportar o estado observado; em conflito de porta durante a criação, remover o container recém-criado.
- **FR-008**: A auditoria DEVE registrar o comando equivalente da engine com valores de variáveis substituídos por `***` e segredos por referência.
- **FR-009**: A interface DEVE seguir o mockup: drawer com eyebrow "Novo container · {estágio}", título, subtítulo, chips de host/engine, campos e validação inline, caixa com comando equivalente, ações "Criar e iniciar" e "Cancelar", e acompanhamento até estado terminal. Divergências deliberadas: a ajuda do campo Imagem mostra digest, tamanho e data, sem "Assinatura verificada" (verificação de assinatura fora do escopo, ver [012](../012-imagens-pull/spec.md)); a ajuda do health check varia por engine (US3-4). A interface DEVE desabilitar "Criar e iniciar" com motivo visível quando o host estiver desconectado e exibir o aviso de perda de logs e estado ao marcar a remoção automática.
- **FR-010**: A API DEVE aplicar a lista de registros permitidos por estágio definida na 012 (FR-004a, configuração `Images:AllowedRegistriesByStage`); quando configurada, recusa outros registros com `registry_not_allowed`. Esta spec não cria configuração própria.
- **FR-011**: A API e o agente DEVEM recusar remoção automática quando a imagem declarar `VOLUME` sem volume nomeado no mesmo destino (`anonymous_volume_with_auto_remove`), preservando o princípio II da constituição.
- **FR-012**: Após reinício do agente sem resultado registrado, o comando DEVE ser concluído por reconciliação (label) ou por reentrega com valores ainda protegidos; valores purgados ou prazo expirado resultam em `parameters_unavailable`, sem container criado.

### Entidades Principais

- **Especificação de criação**: parâmetros da allowlist; parte sensível (variáveis com `sensitive`, exibidas como "segredo" na interface) cifrada e efêmera.
- **Referência de segredo do host**: nome e tipo (`podman_secret` \| `file`), sem valor.
- **Comando de criação**: comando parametrizado (012) com alvo host e resultado contendo id do container criado.

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: Operador cria e inicia um container com imagem, nome, host e uma porta em até 1 minuto (imagem presente) a partir do clique em "Novo container".
- **SC-002**: 100% das opções privilegiadas e montagens proibidas testadas são recusadas pela API e pelo agente.
- **SC-003**: Zero ocorrências de valores de variáveis/segredos canário em respostas, PostgreSQL (após estado terminal), auditoria, eventos, logs e telemetria.
- **SC-004**: Zero containers duplicados em testes de retry, perda de resposta e reinício do agente durante a criação.
- **SC-005**: Configuração aplicada pelo engine coincide 100% com a solicitada nos testes de Docker e Podman.

## Premissas

- Integração com cofre externo (ex.: HashiCorp Vault, cofre de nuvem) fica fora do escopo e exigiria ADR; "cofre" significa segredos já presentes no host.
- No Docker sem Swarm não há segredos nativos; usa-se arquivo em diretório protegido do agente (`<data>/secrets/<nome>`), injetado como variável pelo agente.
- Lista permitida de bind mounts é vazia por padrão (somente volumes nomeados).
- Edição/recriação de container existente e Compose ficam fora do escopo.
- Agente Windows (Podman, spec 006) fica fora desta entrega: `CAPABILITY_CONTAINER_CREATE` não é anunciada no Windows até qualificação própria (caminhos de bind e de segredos em arquivo diferem); a interface lista apenas hosts com a capability.
- A API cifra valores sensíveis com key ring próprio de Data Protection (D-006), separado do key ring do BFF.
