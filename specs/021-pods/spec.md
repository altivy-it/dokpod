# Especificação da Feature: Pods

**Branch da feature**: `021-pods`

**Criada em**: 2026-10-16

**Estado**: `implemented` - aprovada pelo solicitante em 2026-10-16 ("Aprovado, pode implementar"); implementada e integrada em 2026-10-16 (PR #26); evidências em [tasks.md](tasks.md).

**Entrada**: Pedido do solicitante em 2026-10-16: "siga com sua recomendação" (Pods antes de Configurações). Item "Pods" da seção Operação do menu, hoje uma página "Em breve" ("Gestão de pods do Podman ficará disponível com o adapter Libpod").

**Depende de**: [003](../003-inventario-lifecycle-reconciliacao/spec.md) (inventário e comandos duráveis), [010](../010-lista-containers-acoes-lote/spec.md) (ações e lote de containers), [011](../011-detalhe-container-logs/spec.md) (detalhe do container), [016](../016-governanca-alertas/spec.md) (justificativa por estágio), [018](../018-volumes/spec.md)-[020](../020-registros/spec.md) (inventário por capability e telas de infraestrutura).

**Design**: não há board de Pods em `docs/design/app`. O mockup define o item "Pods" na seção Operação; a tela segue Containers (lista, situação, ações por linha e em lote) e as telas de Volumes e Redes (detalhe lateral).

## Esclarecimentos

### Sessão 2026-10-16 (recomendações adotadas, sujeitas à aprovação)

- P: Pods existem em todos os hosts? → R: Só no Podman. Hosts Docker aparecem como "sem pods (o Docker não tem pods)", sem erro; hosts Podman com agente anterior aparecem como "inventário indisponível".
- P: Quem pode agir em um pod? → R: Agir em um pod é agir em todos os seus containers, então valem os scopes de container já existentes no host (`container:start`, `container:stop`, `container:restart`, `container:pause`), sem scope novo.
- P: Quais ações? → R: Iniciar, parar, reiniciar, pausar e retomar o pod inteiro, uma por pod ou em lote (até 50), pelo pipeline de comandos duráveis. Parar e pausar pedem justificativa conforme a política do estágio (spec 016), como nos containers.
- P: Remover pod? → R: Fora desta entrega. No Podman, remover um pod remove os containers dele; a remoção continua container a container (spec 010), que já tem confirmação e auditoria próprias.
- P: E o pod em que o próprio Dokpod roda? → R: O pod que contém o container do agente é marcado como "pod do agente" e o console recusa parar, pausar e reiniciar esse pod: o host ficaria sem agente e não haveria como religá-lo pelo console.
- P: Criar pods? → R: Fora desta entrega (a criação de container da spec 013 continua sem pod).

## Cenários do Usuário e Testes (obrigatório)

### Jornada do Usuário 1 - Ver os pods dos hosts (Prioridade: P1)

Um operador abre Operação → Pods e vê os pods de todos os hosts Podman que pode ler, com a situação de cada um e dos seus containers.

**Valor desta prioridade**: em hosts Podman, serviços inteiros (por exemplo, um compose) vivem em pods; hoje o console mostra só os containers soltos, sem o agrupamento.

**Teste independente**: com um pod em execução, um degradado (algum container parado) e um parado, abrir a tela e conferir host, nome, situação, quantidade de containers por situação e criação, comparando com `podman pod ps`.

**Cenários de aceite**:

1. **Dado** hosts Podman autorizados, **Quando** o usuário abre Pods, **Então** vê nome, host, situação (em execução, degradado, parado, pausado, criado, com erro), quantidade de containers (e quantos em execução), redes e criação.
2. **Dado** o resumo, **Quando** exibido, **Então** mostra o total e a quantidade por situação, e cada chip filtra a lista.
3. **Dado** um host Docker ou um agente anterior, **Quando** listado, **Então** a tela informa "sem pods" ou "inventário indisponível" para aquele host, sem esconder os demais.
4. **Dado** o pod que contém o agente do host, **Quando** listado, **Então** aparece com o rótulo "Pod do agente".

---

### Jornada do Usuário 2 - Investigar um pod (Prioridade: P1)

O operador abre um pod para ver seus containers e chegar a cada um deles.

**Valor desta prioridade**: diagnosticar um pod degradado exige saber qual container parou.

**Teste independente**: abrir o detalhe de um pod degradado e conferir cada container com situação, reinícios e link para o detalhe do container; no detalhe do container, ver o pod e voltar a ele.

**Cenários de aceite**:

1. **Dado** um pod, **Quando** aberto, **Então** o detalhe mostra host, situação, criação, redes, labels e os containers (nome, situação, reinícios, se é o container de infraestrutura) com link para o detalhe de cada um.
2. **Dado** o detalhe de um container que pertence a um pod, **Quando** aberto, **Então** mostra o pod com link para o detalhe do pod.

---

### Jornada do Usuário 3 - Agir em pods (Prioridade: P2)

O operador inicia, para, reinicia, pausa ou retoma um ou vários pods e acompanha o resultado.

**Valor desta prioridade**: reiniciar um serviço composto de vários containers em uma ação, na ordem que o Podman garante.

**Teste independente**: parar um pod em Homologação com justificativa, iniciá-lo de novo, reiniciar dois pods em lote e conferir o resultado por pod, os containers e a auditoria; tentar parar o pod do agente e ver a recusa.

**Cenários de aceite**:

1. **Dado** pods selecionados, **Quando** o usuário escolhe uma ação, **Então** a confirmação lista os afetados e os ignorados (sem permissão, host desconectado, situação incompatível, pod do agente) e pede justificativa para parar e pausar conforme a política do estágio.
2. **Dado** o envio, **Quando** cada comando termina, **Então** o resultado aparece por pod (concluído, falha com motivo) e a situação do pod e dos containers é atualizada.
3. **Dado** o pod do agente, **Quando** o usuário tenta parar, pausar ou reiniciar, **Então** a ação não é oferecida e a API recusa com motivo claro.
4. **Dado** um usuário sem o scope da ação no host, **Quando** vê o pod, **Então** a ação não é oferecida para aquele host.

---

## Casos de Borda

- Pod sem container de infraestrutura (criado sem infra, como o do `podman-compose`): listado normalmente.
- Pod vazio (só a infraestrutura ou nenhum container): listado; ações seguem o Podman.
- Container que entra ou sai do pod fora do Dokpod: o inventário reflete a mudança sem ação do usuário.
- Ação parcialmente bem-sucedida (um container do pod falha ao iniciar): resultado "falha" com os containers que falharam; o pod fica "degradado".
- Mesmo nome de pod em hosts diferentes: a chave é host + identificador.
- Agente desconectado: inventário desatualizado, sem ações.

## Requisitos (obrigatório)

### Requisitos Funcionais

**Inventário**

- **FR-001**: O agente DEVE publicar, com capability própria e só em hosts Podman, o inventário de pods (identificador, nome, situação, criação, labels, redes, container de infraestrutura e containers membros com situação e reinícios), por snapshot e atualizações.
- **FR-002**: O agente DEVE identificar o pod que contém o seu próprio container e marcá-lo como "pod do agente".
- **FR-003**: A API DEVE listar pods dos hosts autorizados (`environment:read`) com filtros (estágio, host, situação), busca (nome, label, container), ordenação, paginação e resumo por situação; hosts Docker e agentes sem a capability DEVEM ser informados à parte.
- **FR-004**: O detalhe do pod DEVE listar os containers com link para o detalhe de cada um, e o detalhe do container DEVE mostrar o pod com link.

**Ações**

- **FR-005**: Iniciar, parar, reiniciar, pausar e retomar um pod DEVEM exigir o scope de container correspondente no host, seguir o pipeline de comandos duráveis (idempotência, prazo, auditoria, capability) e ser recusados para o pod do agente (parar, pausar, reiniciar).
- **FR-006**: Parar e pausar DEVEM pedir justificativa conforme a política do estágio (spec 016), registrada na auditoria.
- **FR-007**: O lote DEVE enviar um comando por pod (até 50), acompanhar o resultado de cada um e permitir retomar após recarregar sem reenviar automaticamente.
- **FR-008**: O resultado DEVE informar os containers que falharam quando a ação for parcial.

**Navegação**

- **FR-009**: A tela Pods DEVE substituir a página "Em breve".

## Entidades Principais

- **Pod**: host, identificador, nome, situação, criação, labels, redes, container de infraestrutura, containers membros (com situação e reinícios), "pod do agente".
- **Comando de pod**: host, pod, ação, autor, justificativa (quando exigida), prazo, resultado e containers que falharam.

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: A lista de pods, a situação e a contagem de containers coincidem com `podman pod ps` em 100% dos hosts Podman de teste.
- **SC-002**: Uma mudança de situação de pod feita fora do Dokpod aparece na tela em até 30 segundos com o agente conectado.
- **SC-003**: Nenhuma ação de parar, pausar ou reiniciar é executada no pod do agente nos testes.
- **SC-004**: Reiniciar 10 pods em lote leva no máximo 4 interações a partir da tela, mais a justificativa quando exigida.
- **SC-005**: Com 56 hosts e 1.000 pods, a primeira página responde em até 2 s no p95.
- **SC-006**: A tela, o detalhe e a confirmação não têm violações sérias ou críticas de acessibilidade e são operáveis só por teclado.

## Premissas

- O Podman expõe pods pela API libpod (`/libpod/pods`); o Docker não tem pods.
- O agente conhece o próprio container (identificador do container em que roda); quando roda fora de container, nenhum pod é "do agente".
- As ações de pod reutilizam a política de justificativa da spec 016 e os scopes de container, sem configuração nova.
- Fora do escopo: criar, remover ou editar pods; ações em pods de Kubernetes (`podman kube`); geração de YAML.
