# Especificação da Feature: Terminal Interativo no Container

**Branch da feature**: `007-console-operacional`

**Criada em**: 2026-10-12

**Estado**: `implemented` - concluída em 2026-10-14; ADR 2026-0006 `proposed` e revisão humana de segurança antes de release; evidências e itens `NOT RUN` em [tasks.md](tasks.md).

**Entrada**: Board `03 · Detalhe do container` (botão "Abrir terminal", aba Terminal) e ação de linha "Abrir terminal" do board `02 · Containers`.

**Depende de**: [007](../007-fundacao-console-design-system/spec.md), [008](../008-visao-geral-hosts-eventos/spec.md), [011](../011-detalhe-container-logs/spec.md) (página de detalhe e canal de streaming).

## Contexto

Abrir um shell dentro de um container equivale a execução arbitrária de comandos no workload, por vezes com acesso a dados e credenciais da aplicação. A arquitetura proíbe proxy genérico e exige threat model para novas fronteiras privilegiadas. Esta spec define um terminal **limitado, auditado e revogável**, intermediado pelo agente, sem expor o socket do engine.

## Cenários do Usuário e Testes (obrigatório)

### Jornada do Usuário 1 - Abrir um shell para diagnóstico (Prioridade: P1)

Como operador com `container:exec`, quero abrir um terminal interativo em um container em execução, digitar comandos e ver a saída como em um terminal local, com redimensionamento e cópia.

**Valor desta prioridade**: Diagnóstico que logs e inspeção não resolvem.

**Teste independente**: Abrir terminal em containers Docker e Podman reais, executar comandos, redimensionar, fechar; conferir auditoria e encerramento no agente.

**Cenários de aceite**:

1. **Dado** container em execução com shell disponível, **Quando** o operador abre a aba Terminal (diretamente, pelo botão "Abrir terminal" do cabeçalho ou pela ação de linha da lista, que navegam para o detalhe com `?tab=terminal`), **Então** a sessão abre sem etapa de confirmação (exceto a justificativa em Produção) e em até 3 s vê, no painel escuro monoespaçado do mockup, o prompt do shell (`/bin/bash`, senão `/bin/sh`) com o usuário padrão do container.
2. **Dado** terminal aberto, **Quando** redimensiona a janela, **Então** o tamanho do pseudo-terminal acompanha.
3. **Dado** terminal aberto, **Quando** o operador fecha a aba, navega para outra página, digita `exit` ou o container para, **Então** a sessão termina no agente e no engine, e o fim é anunciado.
4. **Dado** imagem sem shell (distroless), **Quando** tenta abrir, **Então** recebe "shell não disponível nesta imagem" sem erro genérico.

### Jornada do Usuário 2 - Controle e rastreabilidade (Prioridade: P1)

Como responsável pela segurança, quero que só pessoas autorizadas abram terminais, com justificativa em Produção, limites de tempo, auditoria completa de início/fim e possibilidade de revogação imediata.

**Valor desta prioridade**: Torna aceitável uma capacidade de alto risco.

**Teste independente**: Testes negativos de autorização, justificativa obrigatória, limites, revogação durante sessão e conferência de auditoria.

**Cenários de aceite**:

1. **Dado** usuário sem `container:exec`, **Quando** tenta abrir, **Então** a ação não aparece e a API recusa com 403.
2. **Dado** container em estágio Produção, **Quando** o operador abre o terminal, **Então** deve informar justificativa (10-500 caracteres), registrada na auditoria.
3. **Dado** sessão ociosa por 10 minutos ou ativa por 30 minutos (padrões configuráveis, ver data-model), **Quando** o limite é atingido, **Então** a sessão é encerrada com aviso prévio de 1 minuto.
4. **Dado** permissão revogada no Keycloak ou identidade do agente revogada, **Quando** a reavaliação ocorre (a cada 60 s, pelo monitor de autorização compartilhado com a 011, que refaz a decisão de autorização da API com o token atual renovado pelo BFF) ou o stream do agente cai, **Então** a sessão termina. A revogação no Keycloak é efetiva em até 60 s mais a vida útil restante do access token (documentada em `docs/configuracao-keycloak.md`).
5. **Dado** qualquer sessão, **Quando** termina, **Então** a auditoria registra ator, host, container, início, fim, motivo de término, duração, bytes trafegados e justificativa, sem o conteúdo digitado ou exibido.
6. **Dado** container com label `dokpod.exec=disabled`, **Quando** alguém tenta abrir terminal, **Então** é recusado com motivo.

### Jornada do Usuário 3 - Limites de uso (Prioridade: P2)

Como operador da plataforma, quero limitar sessões simultâneas por usuário, por container e por agente, para que terminais não prejudiquem o canal de controle.

**Valor desta prioridade**: Protege comandos e inventário do mesmo agente.

**Teste independente**: Abrir sessões até o limite e verificar recusa clara; medir latência de comandos com sessões ativas.

**Cenários de aceite**:

1. **Dado** limite atingido (padrões configuráveis: 2 por usuário, 1 por usuário por container, 4 por agente), **Quando** tenta abrir outra, **Então** recebe recusa com o limite atingido.
2. **Dado** saída massiva (ex.: `cat` de arquivo grande), **Quando** ocorre, **Então** a taxa é limitada sem afetar comandos e inventário do agente.

## Casos de Borda

- Conexão do navegador cai: sessão termina em até 30 s no agente (sem reconexão a sessão anterior); o hub usa `KeepAliveInterval` 15 s e `ClientTimeoutInterval` 30 s.
- Processo deixado em segundo plano pelo usuário (`nohup`, `&`, `setsid`): pode sobreviver ao fim da sessão, pois o engine não oferece encerramento de exec; é risco residual registrado no threat model e o fim da sessão garante apenas o processo do shell.
- Agente reinicia com sessão ativa: a sessão termina; na volta, o agente verifica os execs registrados no seu estado local e audita `orphan_suspected` para os que ainda estiverem em execução.
- Sequências de controle maliciosas na saída: emulador de terminal com recursos perigosos desativados (links automáticos, escrita na área de transferência sem gesto, OSC 52).
- Colar texto com quebra de linha: confirmação antes de enviar várias linhas (bracketed paste).
- Container reiniciado durante a sessão: sessão termina.
- Agente N-1 sem a capability: aba mostra "não suportado pelo agente".
- Usuário root no container: exibido no cabeçalho do terminal como alerta; o Dokpod não oferece troca de usuário.

## Requisitos (obrigatório)

### Requisitos Funcionais

- **FR-001**: O sistema DEVE permitir abrir sessão interativa (TTY) em container em execução, com scope `container:exec`, capability do agente e container elegível. A sessão é uma operação interativa efêmera, não um comando durável: entrega no máximo uma vez (sem journal e sem retry automático), deduplicada por `exec_session_id`, com expiração (`expires_at`), auditoria e reconciliação por varredura de execs órfãos (exceção ao modelo de comando registrada no ADR 2026-0006).
- **FR-002**: O comando executado DEVE ser fixo pelo sistema (shell padrão detectado: `/bin/bash`, senão `/bin/sh`), com o usuário padrão do container; o usuário não escolhe comando, usuário, privilégios nem variáveis.
- **FR-003**: A sessão DEVE ser intermediada pelo agente em canal separado do stream de controle, com mTLS e vínculo à sessão/fencing do agente; o plano de controle nunca acessa o engine nem expõe socket.
- **FR-004**: Em Produção, a abertura DEVE exigir justificativa textual (10-500 caracteres, mascarada pelo `SecretMasker` antes da auditoria); a exigência de MFA recente é controlada por `Terminal:RequireRecentMfa` (padrão desligado).
- **FR-005**: A sessão DEVE respeitar limites configuráveis de ociosidade, duração total, sessões simultâneas e taxa de bytes, com aviso antes de encerrar por tempo.
- **FR-006**: Autorização DEVE ser reavaliada periodicamente e a sessão encerrada em revogação de permissão, de identidade do agente, de sessão do usuário (logout) ou parada do container.
- **FR-007**: A auditoria DEVE registrar início e fim com metadados; o conteúdo da sessão NÃO é gravado por padrão.
- **FR-008**: Containers DEVEM poder optar por bloquear terminal via label `dokpod.exec=disabled`.
- **FR-009**: A interface DEVE oferecer a aba Terminal no detalhe (e o atalho "Abrir terminal" no cabeçalho e na lista, que navegam para a aba) conforme o mockup (painel escuro monoespaçado, altura mínima de 360 px), substituindo o estado "em breve" da 011 por: "sem permissão" sem scope, "não suportado pelo agente" sem capability e habilitado nos demais casos; barra de estado e alerta de root são acréscimos de segurança ao mockup. O emulador de terminal DEVE ser acessível: foco claro, atalho para sair do foco do terminal, leitura por leitor de tela habilitável, copiar/colar seguro e indicação de estado (conectando, conectado, encerrado, motivo).
- **FR-010**: O término DEVE fechar o TTY do exec (o shell recebe SIGHUP) e confirmar no engine, por inspeção do exec, que o processo não está em execução em até 5 s; caso contrário, registrar `orphan_suspected` na auditoria. O agente DEVE persistir os IDs de exec ativos no seu estado local para verificar órfãos após reinício.

### Entidades Principais

- **Sessão de terminal**: id, ator, host, container, shell, justificativa, início, fim, motivo, bytes entrada/saída, tamanho do terminal.
- **Política de terminal**: limites, exigência de justificativa por estágio, exigência de MFA.

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: Prompt visível em até 3 s; eco de tecla em até 150 ms p95 em rede de laboratório.
- **SC-002**: 100% dos processos de shell das sessões terminam no engine em até 5 s após fechamento, expiração, revogação ou queda (verificado por inspeção do exec no engine, `Running=false`).
- **SC-003**: 100% dos testes negativos de autorização, justificativa, limites e label de bloqueio passam.
- **SC-004**: Com 4 sessões ativas no agente, latência de comandos de lifecycle aumenta no máximo 10%.
- **SC-005**: Nenhum conteúdo de sessão é encontrado em PostgreSQL, logs ou telemetria (canários).

## Premissas

- Gravação de conteúdo (session recording) fica desativada; se exigida por compliance no futuro, exige ADR (armazenamento, retenção, acesso).
- Emulador de terminal no navegador é dependência nova (candidato: xterm.js, MIT) sujeita à aprovação no ADR da feature.
- Cópia de arquivos, port-forward e execução de comando não interativo ficam fora do escopo.
- Agente Windows (Podman, spec 006) fica fora desta entrega: `CAPABILITY_CONTAINER_EXEC` não é anunciada no Windows até qualificação própria.
- Sessões ativas ficam em memória da API; a primeira release tem réplica única (`docs/arquitetura.md`), e reinício da API encerra as sessões.
