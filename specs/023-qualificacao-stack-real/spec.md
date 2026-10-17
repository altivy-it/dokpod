# Especificação da Feature: Qualificação na stack real

**Branch da feature**: `023-qualificacao-stack-real`

**Criada em**: 2026-10-17

**Estado**: `draft` - aguardando aprovação do solicitante.

**Entrada**: Pedido do solicitante em 2026-10-17 ("Siga com a recomendação"): frente 1, fechar as verificações `NOT RUN` das specs 007 a 022 antes da qualificação de release ([005](../005-qualificacao-release-docker-linux/spec.md)).

**Depende de**: specs 007 a 022 (implementadas); alimenta a matriz da [005](../005-qualificacao-release-docker-linux/spec.md).

## Contexto

As specs de console foram validadas em stack real só com **Podman rootless**. Ficaram registradas como `NOT RUN`: engine Docker real, compatibilidade de versões do agente e do servidor (N-1), usuário sem administração e registro privado na stack real, canários ponta a ponta e o terminal real pelo navegador. Esta especificação não acrescenta funcionalidade: executa essas verificações, corrige os defeitos encontrados e atualiza as evidências.

## Esclarecimentos

### Sessão 2026-10-17 (recomendações adotadas, sujeitas à aprovação)

- P: Como obter Docker real nesta máquina sem instalar nada no host? → R: Docker Engine oficial (`docker:28-dind`) dentro de um container Podman, com agente próprio no stack de teste. Prova de viabilidade feita: Docker 28.5.2, overlay2, cgroup v2, executa, pausa e mede containers. É um engine Docker real (mesma API), mas aninhado; a evidência registra essa condição. Docker instalado diretamente num host continua como item da matriz da 005.
- P: O que é "N-1"? → R: O agente e o servidor do commit de `main` anterior às mudanças de protocolo mais recentes (spec 020, antes de pods), construídos a partir do histórico do Git.
- P: Carga (56 hosts, 2.000 containers), agente Windows e Podman rootful? → R: Fora desta entrega; ficam para a frente 2 (qualificação de release) e para a matriz da 005.

## Cenários do Usuário e Testes (obrigatório)

### Jornada 1 - Operar um host Docker real pelo console (Prioridade: P1)

Um operador administra pelo console um host com Docker real, como já faz com Podman.

**Teste independente**: com um agente ligado a um Docker real, executar pelo console a lista, o detalhe (resumo, inspect, logs), as ações (parar, iniciar, reiniciar, pausar, retomar, remover), o terminal, a criação de container, o pull, e as telas de Imagens, Volumes, Redes, Registros e Pods; comparar com o `docker` CLI.

**Cenários de aceite**:

1. **Dado** um host Docker conectado, **Quando** o operador abre as telas de inventário, **Então** containers, imagens, volumes, redes e registros coincidem com o `docker` CLI, e Pods mostra o host como "sem pods".
2. **Dado** um container no host Docker, **Quando** o operador executa cada ação, o terminal e a criação de container, **Então** o resultado no engine é o esperado, a auditoria registra o ator, e as recusas próprias do Docker (por exemplo, reinício por health check) aparecem com mensagem clara.
3. **Dado** métricas do host Docker, **Quando** exibidas, **Então** CPU e memória coincidem com `docker stats` dentro da tolerância da spec 009.

---

### Jornada 2 - Atualizar sem quebrar: agente e servidor de versões vizinhas (Prioridade: P1)

Durante uma atualização, agentes antigos convivem com o servidor novo, e vice-versa.

**Teste independente**: conectar o agente N-1 ao servidor N e o agente N ao servidor N-1; executar inventário e comandos.

**Cenários de aceite**:

1. **Dado** um agente N-1 e o servidor N, **Quando** conectados, **Então** a sessão se mantém, o inventário aparece, os comandos antigos funcionam, e as telas novas mostram "inventário indisponível" para o que o agente não oferece, sem erro.
2. **Dado** um agente N e o servidor N-1, **Quando** conectados, **Então** a sessão se mantém e o agente não envia mensagens que o servidor não conhece.

---

### Jornada 3 - Usuário sem administração e registro privado (Prioridade: P2)

**Teste independente**: com um usuário que só tem leitura e um registro privado com credencial no laboratório, conferir as recusas e o pull autenticado.

**Cenários de aceite**:

1. **Dado** um usuário só de leitura, **Quando** entra no console, **Então** não vê Configurações nem ações que exigem permissão, e a API recusa as ações e as políticas.
2. **Dado** um registro privado com credencial configurada no host, **Quando** o operador faz pull e cria um container a partir dele, **Então** o pull funciona sem que a credencial apareça em respostas, banco, logs ou auditoria; sem credencial, a falha é legível.

---

### Jornada 4 - Canários e terminal pelo navegador (Prioridade: P2)

**Teste independente**: criar containers com valores-canário em variáveis, labels, health check e opções de log; varrer respostas da API, PostgreSQL e logs de nginx, API, BFF e agente; abrir o terminal pelo navegador e encerrá-lo por cada motivo.

**Cenários de aceite**:

1. **Dado** canários nos campos sensíveis, **Quando** o console exibe e audita esses containers, **Então** nenhum canário aparece fora do mascaramento previsto.
2. **Dado** o terminal aberto pelo navegador, **Quando** o operador digita e a sessão termina (fechar, ociosidade, revogação), **Então** a latência e o encerramento atendem a spec 014 e o conteúdo digitado não aparece na auditoria.

## Casos de Borda

- O Docker aninhado pode diferir de um host Docker em métricas de host; a divergência é registrada, não mascarada.
- Defeito encontrado: corrigido nesta entrega com teste que o reproduz, ou registrado como bloqueador da 005 se exigir decisão.
- Recursos de teste (containers, imagens, volumes, redes, registro, usuário) são removidos ao final; os recursos reais do laboratório não são tocados.

## Requisitos (obrigatório)

### Requisitos Funcionais

- **FR-001**: O stack de teste DEVE oferecer um perfil com Docker real e agente próprio, sem instalar nada no host.
- **FR-002**: Cada `NOT RUN` coberto por esta spec DEVE ser executado e ter a evidência registrada no `tasks.md` da spec de origem (resultado, data, condições).
- **FR-003**: Defeitos encontrados DEVEM ser corrigidos com teste automatizado que os reproduza.
- **FR-004**: O agente e o servidor N-1 DEVEM ser construídos de um commit identificado do histórico, e o commit DEVE constar da evidência.
- **FR-005**: Itens que continuarem `NOT RUN` DEVEM ter o motivo e o destino (005 ou frente 2) registrados.

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: 100% das telas e ações do console executadas contra Docker real, com inventário igual ao `docker` CLI.
- **SC-002**: Agente N-1 ↔ servidor N e agente N ↔ servidor N-1 mantêm a sessão por 10 minutos com inventário e comandos funcionando, sem erro de protocolo nos logs.
- **SC-003**: Zero ocorrências de canário e de credencial do registro privado fora do mascaramento previsto.
- **SC-004**: Todos os `NOT RUN` cobertos por esta spec ficam resolvidos (executados ou com destino justificado), e a lista final consta do `tasks.md`.

## Premissas

- O laboratório continua com Podman 5.7 rootless, Keycloak e PostgreSQL da plataforma de identidade.
- Fora do escopo: carga (56 hosts e 2.000 containers), agente Windows, Podman rootful, Docker instalado diretamente no host, assinatura de imagens.
