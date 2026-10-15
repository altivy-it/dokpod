# Especificação da Feature: Redes

**Branch da feature**: `019-redes`

**Criada em**: 2026-10-15

**Estado**: `draft` - aguardando aprovação do solicitante.

**Entrada**: Pedido do solicitante em 2026-10-15: "siga para próxima etapa de desenvolvimento", após a spec 018. Próximo item "Em breve" da seção Operação do menu: "Redes" ("Redes bridge, overlay e macvlan por host").

**Depende de**: [003](../003-inventario-lifecycle-reconciliacao/spec.md) (inventário e comandos duráveis), [008](../008-visao-geral-hosts-eventos/spec.md) (eventos do engine), [010](../010-lista-containers-acoes-lote/spec.md) (convenções de lista e lote), [011](../011-detalhe-container-logs/spec.md) (rede no detalhe do container), [013](../013-criacao-container/spec.md) (escolha de rede na criação), [016](../016-governanca-alertas/spec.md) (política de justificativa por estágio), [018](../018-volumes/spec.md) (inventário por capability, projeção "em uso", remoção em lote retomável).

**Design**: não há board de Redes em `docs/design/app`. O mockup define o item "Redes" na seção Operação e o card "Rede e armazenamento" no detalhe do container (rede, driver e IP). A tela segue as telas Imagens e Volumes: lista, filtros, chips de resumo, detalhe lateral, ações por linha e em lote.

## Esclarecimentos

### Sessão 2026-10-15 (recomendações adotadas, sujeitas à aprovação)

- P: Quais redes aparecem? → R: Todas as redes do engine, no Docker e no Podman, de qualquer driver (bridge, overlay, macvlan, ipvlan, host, none e de plugins).
- P: O que é "em uso"? → R: Rede à qual algum container do host está conectado, **inclusive parado**. O engine só informa os containers em execução na própria rede, então o "em uso" vem das conexões de cada container no inventário.
- P: Redes do sistema (`bridge`, `host`, `none` no Docker; `podman` no Podman; `ingress` e `docker_gwbridge` em Swarm)? → R: Listadas com o rótulo "Do sistema" e nunca removíveis pelo console.
- P: Pode remover rede em uso? → R: Não. Só redes sem nenhum container conectado (inclusive parado); o agente confere de novo antes de remover e nunca desconecta containers para remover.
- P: Quem remove? → R: Scope novo `network:delete` por host. Remover rede não apaga dados, então a confirmação pede digitar a quantidade só em lote com rede de Produção (como remover containers, spec 010) e justificativa conforme a política do estágio (spec 016).
- P: Criar rede, conectar ou desconectar container? → R: Fora desta entrega. Criar rede envolve sub-redes e drivers com impacto na rede do host e merece decisão própria; conectar e desconectar container altera a conectividade de serviços em execução. Esta spec entrega inventário, investigação e limpeza.
- P: Redes Swarm (escopo `swarm`)? → R: Listadas para leitura, sem remoção pelo console (pertencem ao cluster, não ao host).
- P: Opções do driver (por exemplo, a interface física de uma macvlan)? → R: Não trafegam para o plano de controle; a tela mostra driver, escopo, sub-redes e gateways, que bastam para investigar conflitos de endereçamento.

## Cenários do Usuário e Testes (obrigatório)

### Jornada do Usuário 1 - Ver as redes dos hosts e as sem uso (Prioridade: P1)

Um operador abre Operação → Redes e vê as redes de todos os hosts que pode ler, quantos containers usam cada uma e quais estão sem uso.

**Valor desta prioridade**: redes órfãs (de compose removidos, testes, deploys antigos) acumulam sub-redes reservadas e causam conflitos de endereçamento; hoje não há como vê-las pelo console.

**Teste independente**: criar redes com e sem container conectado (inclusive parado), abrir a tela e conferir host, nome, driver, escopo, sub-redes, containers conectados e o rótulo "sem uso", comparando com o engine.

**Cenários de aceite**:

1. **Dado** hosts autorizados com redes, **Quando** o usuário abre Redes, **Então** vê nome, host, engine, driver, escopo, sub-redes, criação e os containers conectados (inclusive parados), com o rótulo "Sem uso" quando não há nenhum.
2. **Dado** o resumo, **Quando** exibido, **Então** mostra total de redes e quantas estão sem uso (excluindo as do sistema), e o chip "sem uso" filtra a lista.
3. **Dado** uma rede do sistema, **Quando** listada, **Então** aparece com o rótulo "Do sistema" e sem ação de remover.
4. **Dado** um host com agente sem suporte a inventário de redes, **Quando** listado, **Então** a tela informa que o inventário daquele host está indisponível, sem esconder os demais.

---

### Jornada do Usuário 2 - Investigar uma rede (Prioridade: P1)

O operador abre uma rede para ver configuração de endereçamento e quem está conectado, com acesso direto aos containers.

**Valor desta prioridade**: antes de limpar ou de diagnosticar falha de comunicação, é preciso saber quem está na rede e com qual endereço.

**Teste independente**: abrir o detalhe de uma rede em uso e de uma sem uso; conferir driver, escopo, sub-redes e gateways, flags (interna, IPv6, anexável), labels e containers conectados com IP e link.

**Cenários de aceite**:

1. **Dado** uma rede, **Quando** aberta, **Então** o detalhe mostra nome, identificador curto, host, driver, escopo, sub-redes e gateways, se é interna, se tem IPv6, se é anexável, criação, labels e os containers conectados (com link para o detalhe do container e os endereços IPv4/IPv6 quando o container está em execução).
2. **Dado** o detalhe do container (spec 011), **Quando** o card "Rede e armazenamento" lista uma rede, **Então** o nome leva ao detalhe da rede.
3. **Dado** dois hosts com redes de sub-redes sobrepostas, **Quando** o usuário busca pela sub-rede, **Então** encontra as duas redes.

---

### Jornada do Usuário 3 - Remover redes sem uso (Prioridade: P2)

O operador remove uma ou várias redes sem uso, com confirmação e justificativa quando exigida, e acompanha o resultado por rede.

**Valor desta prioridade**: libera sub-redes reservadas e reduz ruído, com rastreabilidade.

**Teste independente**: selecionar redes sem uso de hosts diferentes, remover em lote, acompanhar cada resultado e conferir a auditoria; tentar remover uma rede em uso e uma do sistema e ver as recusas.

**Cenários de aceite**:

1. **Dado** redes sem uso selecionadas, **Quando** o usuário escolhe "Remover", **Então** a confirmação lista as afetadas e as ignoradas (em uso, do sistema, Swarm, sem permissão, host desconectado) e pede justificativa conforme a política do estágio.
2. **Dado** mais de uma rede com alguma de Produção, **Quando** confirma, **Então** a confirmação exige digitar a quantidade.
3. **Dado** o envio, **Quando** cada comando termina, **Então** o resultado aparece por rede (removida, recusada por estar em uso, não encontrada, falha) e a rede some da lista.
4. **Dado** uma rede à qual um container foi conectado entre a listagem e a remoção, **Quando** o agente recebe o comando, **Então** recusa sem remover e informa "em uso".
5. **Dado** um usuário sem `network:delete` no host, **Quando** vê a lista, **Então** não tem a ação de remover para aquele host.

---

## Casos de Borda

- Rede usada só por container parado: conta como "em uso" e não pode ser removida.
- Rede criada ou removida fora do Dokpod: aparece ou some pelo inventário, sem ação do usuário.
- Mesmo nome de rede em hosts diferentes (ou, no Docker, duas redes com o mesmo nome no mesmo host): a chave é host + identificador; o nome é exibido com o identificador curto quando há duplicidade.
- Container com modo de rede `host`, `none` ou `container:<id>`: não conta como conexão de rede nomeada, exceto para as redes `host` e `none`, que são do sistema.
- Rede de driver de plugin indisponível: listada; a remoção segue o engine (o driver pode recusar).
- Agente desconectado: inventário mostrado como desatualizado, sem ações.
- Rede de pod do Podman: listada como rede comum; pods ficam para a spec de Pods.

## Requisitos (obrigatório)

### Requisitos Funcionais

**Inventário**

- **FR-001**: O agente DEVE publicar o inventário de redes do host (identificador, nome, driver, escopo, sub-redes e gateways, interna, IPv6, anexável, criação, labels e containers conectados, inclusive parados, com os endereços quando em execução), por snapshot e atualizações, com capability própria; as opções do driver NÃO DEVEM ser publicadas.
- **FR-002**: O plano de controle DEVE manter a projeção de redes reconstruível por host, marcar "em uso" quando qualquer container do host, em qualquer situação, está conectado, e marcar as redes do sistema.
- **FR-003**: A API DEVE listar redes dos hosts autorizados (`environment:read`) com filtros (estágio, host, engine, driver, sem uso, incluir as do sistema), busca (nome, identificador, sub-rede, label, container), ordenação, paginação e resumo (total, sem uso).
- **FR-004**: O detalhe da rede DEVE mostrar configuração de endereçamento, flags, labels e containers conectados com link e endereços.

**Remoção**

- **FR-005**: A remoção DEVE exigir o scope `network:delete` no host, seguir o pipeline de comandos duráveis (idempotência, prazo, auditoria, capability) e valer só para rede sem uso, de escopo local e que não seja do sistema; o agente DEVE conferir o uso imediatamente antes e NUNCA desconectar containers.
- **FR-006**: A confirmação DEVE listar afetadas e ignoradas com o motivo, exigir digitar a quantidade em lote com rede de Produção e pedir justificativa conforme a política do estágio (spec 016), registrada na auditoria.
- **FR-007**: O lote DEVE enviar um comando por rede (até 50), acompanhar o resultado de cada uma e permitir retomar após recarregar sem reenviar automaticamente, como o lote de volumes (spec 018).
- **FR-008**: O resultado DEVE informar códigos padronizados (removida, em uso, não encontrada, rede do sistema, falha).

**Navegação**

- **FR-009**: A tela Redes DEVE substituir a página "Em breve"; o detalhe do container DEVE ligar as redes ao detalhe da rede.
- **FR-010**: Hosts sem a capability de inventário de redes DEVEM aparecer como "inventário indisponível" na tela, sem impedir os demais.

## Entidades Principais

- **Rede**: host, identificador, nome, driver, escopo, sub-redes e gateways, interna, IPv6, anexável, criação, labels, do sistema (sim/não) e containers conectados (container, endereços IPv4/IPv6 quando em execução).
- **Comando de remoção de rede**: host, rede (identificador e nome), autor, justificativa (quando exigida), prazo, resultado e código de falha.

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: A lista de redes e a contagem "sem uso" coincidem com o engine em 100% dos hosts de teste (Docker e Podman), considerando containers parados.
- **SC-002**: Uma rede criada ou removida fora do Dokpod aparece ou some da tela em até 30 segundos com o agente conectado.
- **SC-003**: Nenhuma rede em uso (inclusive por container parado) nem do sistema é removida nos testes, inclusive quando passa a ser usada entre a listagem e o comando.
- **SC-004**: Remover 20 redes sem uso em lote leva no máximo 4 interações a partir da tela (filtrar sem uso, selecionar, remover, confirmar), mais a justificativa e a quantidade quando exigidas.
- **SC-005**: Com 56 hosts e 2.000 redes, a primeira página responde em até 2 s no p95.
- **SC-006**: A tela e a confirmação não têm violações sérias ou críticas de acessibilidade e são operáveis só por teclado.

## Premissas

- Docker e Podman expõem redes pela API compatível (`/networks`); as conexões de containers parados vêm das configurações de rede de cada container no inventário do agente.
- A remoção usa a política de justificativa por estágio da spec 016 sem nova configuração.
- O scope `network:delete` é novo e concedido por host pelo provisionamento; como na spec 018, a migration o acrescenta aos registros que já permitem `container:delete`, e o Keycloak recebe o scope pelos scripts de configuração.
- Fora do escopo: criar rede, conectar ou desconectar container, editar rede, redes de cluster Swarm (só leitura), pods (spec própria), regras de firewall e DNS do host.
