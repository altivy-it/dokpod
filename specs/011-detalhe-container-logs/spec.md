# Especificação da Feature: Detalhe do Container, Inspeção Sanitizada e Logs Sob Demanda

**Branch da feature**: `007-console-operacional`

**Criada em**: 2026-10-12

**Estado**: `implemented` - aprovada em 2026-10-13 ([specs/README.md](../README.md)); revisão humana de segurança antes de release; evidências e itens `NOT RUN` em [tasks.md](tasks.md).

**Entrada**: Board `03 · Detalhe do container` ([Detalhe.dc.html](../../docs/design/app/project/Detalhe.dc.html)): cabeçalho com situação, engine, imagem, ID, host, tempo ativo, reinícios e health check; ações (Abrir terminal, Reiniciar, Parar, Remover container); abas Resumo, Logs, Inspecionar, Estatísticas e Terminal; cards "Recursos agora" e "Rede e armazenamento".

**Depende de**: [007](../007-fundacao-console-design-system/spec.md), [008](../008-visao-geral-hosts-eventos/spec.md), [010](../010-lista-containers-acoes-lote/spec.md) (ações). **Usa**: [009](../009-metricas-recursos/spec.md) (Recursos agora, Estatísticas), [014](../014-terminal-container/spec.md) (aba Terminal).

## Contexto

A projeção atual não guarda configuração do container (comando, usuário, diretório, health check, labels, driver de log, rede, IP, volumes, política de reinício) e a política de segurança diz que "variáveis de ambiente, conteúdo de secrets, arquivos e logs integrais não são coletados no MVP". O mockup exige inspeção e logs. Esta spec introduz consultas **sob demanda, não persistidas e sanitizadas**, intermediadas pelo agente.

## Cenários do Usuário e Testes (obrigatório)

### Jornada do Usuário 1 - Entender um container em uma tela (Prioridade: P1)

Como operador, quero abrir um container e ver no cabeçalho nome, situação, engine e versão, imagem, ID curto, host, há quanto tempo está ativo, reinícios nas últimas 24 horas e situação do health check, com as ações disponíveis.

**Valor desta prioridade**: Ponto de entrada do diagnóstico.

**Teste independente**: Abrir detalhe de containers reais em várias situações e conferir os dados com o engine.

**Cenários de aceite**:

1. **Dado** container em execução reiniciado 2 vezes nas últimas 24 h, **Quando** o detalhe abre, **Então** o cabeçalho mostra "ativo há N min · 2 reinícios nas últimas 24 h · health check: saudável".
2. **Dado** ações (Reiniciar, Parar/Iniciar, Pausar/Retomar, Remover), **Quando** acionadas, **Então** seguem as mesmas regras de permissão, confirmação, idempotência e acompanhamento da spec 010.
3. **Dado** container removido enquanto a página está aberta, **Quando** a invalidação chega, **Então** a página informa "Container removido" e oferece voltar à lista.
4. **Dado** usuário sem `environment:read` no host, **Quando** acessa a URL, **Então** recebe "acesso negado" sem revelar se o container existe.

### Jornada do Usuário 2 - Resumo, rede e armazenamento (Prioridade: P1)

Como operador, quero ver comando, usuário, diretório de trabalho, health check, labels, configuração de logs, portas, rede, IP, volumes, política de reinício e a quantidade de variáveis de ambiente (sem valores).

**Valor desta prioridade**: Responde "como este container foi configurado?" sem acesso ao host.

**Teste independente**: Container com configuração conhecida (incluindo variáveis sensíveis) comparado à aba Resumo e ao card Rede e armazenamento; confirmar ausência de valores de variáveis em respostas, logs da API e telemetria.

**Cenários de aceite**:

1. **Dado** container com 14 variáveis, **Quando** o resumo carrega, **Então** mostra "14 definidas", sem nomes nem valores, e "N segredos" quando houver secrets montados pelo engine.
2. **Dado** labels, **Quando** exibidas, **Então** aparecem como texto escapado, truncadas no limite configurado.
3. **Dado** usuário sem `container:inspect`, **Quando** abre o detalhe, **Então** vê cabeçalho e recursos, e as seções de configuração mostram "sem permissão".

### Jornada do Usuário 3 - Inspeção técnica sanitizada (Prioridade: P2)

Como operador avançado, quero ver o JSON de inspeção do engine, com campos sensíveis removidos, e copiá-lo.

**Valor desta prioridade**: Diagnóstico profundo sem shell no host.

**Teste independente**: Comparar a aba Inspecionar com `docker inspect`, verificando que a lista de campos removidos foi aplicada.

**Cenários de aceite**:

1. **Dado** container com `Env`, `Config.Env`, `HostConfig.Binds` contendo caminhos sensíveis e `Mounts` de secrets, **Quando** inspecionado, **Então** valores de variáveis e conteúdo de secrets nunca aparecem; campos removidos são marcados como `"<oculto>"`.
2. **Dado** resposta maior que o limite (proposto: 256 KiB), **Quando** retornada, **Então** é truncada com aviso.

### Jornada do Usuário 4 - Logs sob demanda e ao vivo (Prioridade: P1)

Como operador, quero ver as últimas linhas de log de um container, filtrar por texto e nível, acompanhar ao vivo e baixar o trecho exibido.

**Valor desta prioridade**: Diagnóstico mais frequente; mockup abre o detalhe na aba Logs.

**Teste independente**: Container gerando logs com níveis e padrões de segredo conhecidos; abrir a aba, filtrar, ativar ao vivo, baixar; verificar limites, mascaramento, auditoria e ausência de persistência.

**Cenários de aceite**:

1. **Dado** permissão `container:logs`, **Quando** abre a aba Logs, **Então** vê as últimas 500 linhas (padrão) com horário, nível (quando reconhecível) e mensagem, em fonte mono, sem renderizar HTML.
2. **Dado** "Ao vivo", **Quando** ativo, **Então** novas linhas chegam em até 2 s, a rolagem acompanha o fim apenas se o usuário já estiver no fim, e a sessão ao vivo encerra sozinha após o tempo máximo (proposto: 15 min), ao sair da aba, no logout ou na expiração da sessão do BFF. O navegador mantém no máximo 10.000 linhas ou 5 MiB, descartando as mais antigas com aviso.
3. **Dado** filtro de texto ou de nível, **Quando** aplicado, **Então** filtra as linhas carregadas; para busca no histórico, solicita novo trecho com o filtro de texto (literal, sem expressão regular) aplicado no agente. O filtro de nível atua somente sobre as linhas carregadas.
4. **Dado** linha contendo padrão de segredo conhecido (token, senha em URL, chave privada), **Quando** exibida, **Então** o trecho é mascarado antes de sair do agente.
5. **Dado** "Baixar", **Quando** acionado, **Então** baixa somente as linhas mantidas no navegador (limite: 5 MiB), com nome `<container>-<data>.log`.
6. **Dado** qualquer consulta de logs, **Quando** concluída, **Então** a auditoria registra quem consultou, qual container, período e volume, sem conteúdo.
7. **Dado** agente desconectado, **Quando** abre a aba, **Então** vê "logs indisponíveis: agente desconectado"; nenhum log é servido de cache.

### Jornada do Usuário 5 - Estatísticas e recursos (Prioridade: P2)

Como operador, quero o card "Recursos agora" e a aba Estatísticas (CPU e memória dos últimos 30 minutos).

**Valor desta prioridade**: Contexto de recursos no diagnóstico (dados da spec 009).

**Teste independente**: Ver quickstart da spec 009.

**Cenários de aceite**:

1. **Dado** spec 009 entregue, **Quando** o detalhe abre, **Então** "Recursos agora" e Estatísticas exibem os dados conforme o mockup; sem 009, mostram "indisponível".

## Casos de Borda

- Logs binários ou com sequências de controle ANSI: removidas ou convertidas em texto seguro; linhas acima de 16 KiB truncadas.
- Driver de log sem leitura (`none`, `syslog` remoto): aba mostra "driver não permite leitura".
- Container com TTY (stdout/stderr misturados) e sem TTY (multiplexado): ambos suportados.
- Alto volume de logs ao vivo: limite de taxa no agente (proposto: 200 linhas/s), com aviso "linhas omitidas: N".
- Vários usuários com logs ao vivo no mesmo host: limite de sessões simultâneas por agente (proposto: 10) e por usuário (proposto: 3); consultas simultâneas por agente limitadas (proposto: 4); excesso responde "muitas requisições, tente novamente".
- Resposta de logs maior que uma mensagem do canal do agente: entregue em partes de até 256 KiB e montada pela API até o limite de 1 MiB; nenhuma resposta derruba a conexão do agente.
- Termo de busca nunca aparece em logs de acesso ou de aplicação (nginx, BFF, API) nem na auditoria.
- Fuso: horários exibidos no fuso do navegador, com origem UTC.
- ID inválido/inexistente: 404 sem diferenciar de não autorizado para hosts não autorizados.

## Requisitos (obrigatório)

### Requisitos Funcionais

- **FR-001**: A API DEVE fornecer o detalhe de um container (projeção enriquecida + host + engine + reinícios nas últimas 24 h a partir de eventos) com autorização `environment:read`.
- **FR-001a**: Reinícios nas últimas 24 h DEVEM ser contados a partir de `container.auto_restarted` e `command.succeeded` de reinício (spec 008), validados em Docker e Podman reais.
- **FR-002**: A API DEVE fornecer resumo de configuração e inspeção sanitizada obtidos sob demanda do agente, com autorização `container:inspect`, prazo máximo (proposto: 10 s) e sem persistência.
- **FR-003**: A sanitização DEVE ocorrer no agente, por allowlist de campos para o resumo e por denylist aplicada ao JSON de inspeção; além disso, todo valor de texto (resumo e inspeção, incluindo labels, opções do driver de log, comando do health check, argumentos e campos desconhecidos) passa pelo mascarador de segredos. Valores de variáveis de ambiente, conteúdo de secrets e argumentos com padrão de segredo nunca saem do host.
- **FR-004**: A API DEVE fornecer leitura de logs sob demanda com autorização `container:logs`: últimas N linhas (padrão 500, máximo 5.000) ou período, filtro de texto opcional, limite de bytes por resposta, mascaramento de padrões de segredo no agente.
- **FR-005**: A API DEVE permitir acompanhamento ao vivo de logs por sessão limitada em duração, taxa de linhas e de bytes, e quantidade de sessões simultâneas por agente e por usuário, entregue pelo canal em tempo real autenticado via BFF; o encerramento ocorre ao sair da aba, ao expirar, no logout/expiração da sessão do BFF ou por perda de permissão, com autorização reavaliada no máximo a cada 60 s (igual à spec 014).
- **FR-005a**: Comandos, resultados e heartbeat DEVEM ter prioridade sobre consultas e logs no canal do agente; respostas e trechos ao vivo são enviados em partes de até 256 KiB.
- **FR-006**: Logs NUNCA DEVEM ser persistidos no PostgreSQL, em telemetria ou em logs da API/BFF/nginx; o termo de busca é removido de logs de acesso; somente metadados de consulta (sem texto livre) entram na auditoria.
- **FR-007**: O nível de log DEVE ser inferido no agente por padrões comuns (`INFO`, `WARN`/`WARNING`, `ERROR`, `DEBUG`, `TRACE`, JSON com campo `level`); sem padrão, nível "—".
- **FR-008**: A interface DEVE seguir o mockup: cabeçalho, ações, abas Resumo/Logs/Inspecionar/Estatísticas/Terminal, cards "Recursos agora" e "Rede e armazenamento"; a aba selecionada fica na URL.
- **FR-009**: Abas e seções sem permissão DEVEM mostrar "sem permissão"; abas e ações dependentes de specs não entregues (aba Terminal e botão "Abrir terminal") DEVEM aparecer desabilitadas com "em breve".
- **FR-010**: O agente DEVE anunciar capabilities de inspeção e logs; sem elas, as abas mostram "não suportado pelo agente".
- **FR-011**: A documentação de segurança DEVE ser atualizada para refletir a leitura sob demanda de logs e inspeção sanitizada, com aprovação humana.

### Entidades Principais

- **Detalhe do container**: projeção + host + engine + reinícios 24 h + ações permitidas.
- **Resumo de configuração (efêmero)**: comando, usuário, diretório, health check, labels, logging, portas, redes, IP, volumes, política de reinício, contagem de variáveis e secrets.
- **Trecho de logs (efêmero)**: linhas (horário, fluxo stdout/stderr, nível, texto mascarado), truncamento, linhas omitidas.
- **Sessão de logs ao vivo**: usuário, container, início, expiração, linhas entregues.

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: Aba Logs exibe as últimas 500 linhas em até 2 s com agente conectado.
- **SC-002**: Linhas ao vivo chegam em até 2 s após emitidas pelo container.
- **SC-003**: Zero ocorrências de valores de variáveis de ambiente ou padrões de segredo de teste em respostas da API, logs de API/BFF/agente, auditoria e telemetria (teste automatizado com canários).
- **SC-004**: Nenhum conteúdo de log encontrado no PostgreSQL após uso intenso (varredura com canário).
- **SC-005**: Sessões ao vivo são encerradas em 100% dos casos de saída da aba, expiração e logout em até 5 s, e de revogação de permissão em até 60 s.
- **SC-006**: Uma resposta de logs no tamanho máximo e 10 sessões ao vivo na taxa máxima não derrubam a conexão do agente nem atrasam um comando em mais de 2 s.

## Premissas

- Limites propostos: 500/5.000 linhas, 1 MiB por resposta (montada de partes de 256 KiB), 5 MiB por download e no buffer do navegador (10.000 linhas), 15 min por sessão ao vivo, 200 linhas/s e 256 KiB/s por sessão, 10 sessões por agente, 3 por usuário, 4 consultas simultâneas por agente, 256 KiB para inspeção, reavaliação de autorização a cada 60 s. Todos configuráveis e sujeitos a revisão.
- Padrões de mascaramento iniciais: `Authorization: Bearer ...`, `password=`, `secret=`, `token=`, credenciais em URL (`scheme://user:pass@`), `-u user:pass`, blocos `-----BEGIN ... PRIVATE KEY-----`, JWT (`eyJ...`). Lista mantida no agente.
- Novos scopes `container:inspect` e `container:logs` são criados no Keycloak pelo provisionador e documentados em `docs/configuracao-keycloak.md`, com os papéis que os recebem definidos na revisão humana.
